shader_type spatial;
uniform float radius;
uniform float height_scale;
uniform float normal_strength;

uniform bool is_debug;
uniform bool is_cube;

uniform sampler2D albedo_map : repeat_disable;
uniform sampler2D image_texture : repeat_disable;

varying float elevation;
varying flat int subdivision_level;
varying vec3 object_space_pos;

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	float x2 = p.x * p.x;
	float y2 = p.y * p.y;
	float z2 = p.z * p.z;

	float x = p.x * sqrt(1.0 - (y2 + z2) / 2.0 + y2 * z2 / 3.0);
	float y = p.y * sqrt(1.0 - (z2 + x2) / 2.0 + z2 * x2 / 3.0);
	float z = p.z * sqrt(1.0 - (x2 + y2) / 2.0 + x2 * y2 / 3.0);

	return vec3(x, y, z);
}

vec3 height_to_normal(float height, float strength, vec3 vertex, vec3 _normal) {
	vec3 dX = dFdx(vertex);
	vec3 dY = dFdy(vertex);

	vec3 crossX = cross(_normal, dX);
	vec3 crossY = cross(dY, _normal);

	float dt = dot(dX, crossY);
	float sign = dt < 0.0 ? -1.0 : 1.0;
	float surface = sign / max(1.192093e-12, abs(dt));

	float dHdx = dFdx(height);
	float dHdy = dFdy(height);

	vec3 surfGrad = surface * (dHdx * crossY + dHdy * crossX);

	return normalize(_normal - strength * surfGrad);
}

vec2 point_on_sphere_to_UV(vec3 p) {
	p = normalize(p);
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / (2.0 * PI) + 0.5);
	float v = (latitude / PI) + 0.5;
	return vec2(u, v);
}

void vertex() {
	float scaleFactor = pow(2.0, INSTANCE_CUSTOM.a);
	subdivision_level = int(INSTANCE_CUSTOM.w);
	uint intValue = uint(INSTANCE_CUSTOM.z);
	int normSign = (intValue == 1u || intValue == 2u || intValue == 4u) ? 1 : -1;
	uint x = (intValue & 4u) >> 2u;
	uint y = (intValue & 2u) >> 1u;
	uint z =  intValue & 1u;

	x = normSign == 1 ? x : x^1u;
	y = normSign == 1 ? y : y^1u;
	z = normSign == 1 ? z : z^1u;
	
	NORMAL = float(normSign) * vec3(float(x),float(y),float(z));
	
	vec3 axisA = vec3(NORMAL.y, NORMAL.z, NORMAL.x);
	vec3 axisB = cross(NORMAL, axisA);
	vec3 axicC = NORMAL * scaleFactor;
	axisA /= scaleFactor;
	axisB /= scaleFactor;

	vec3 point = NODE_POSITION_WORLD + axisA * (2.0 * UV.x - 1.0) + axisB * (2.0 * UV.y - 1.0);

	VERTEX = is_cube ? point : point_on_cube_to_point_on_sphere(point);
	
	NORMAL = VERTEX;
	object_space_pos = VERTEX;
	VERTEX = radius * VERTEX - NODE_POSITION_WORLD;
	
	UV = point_on_sphere_to_UV(object_space_pos);

	elevation = textureLod(image_texture, point_on_sphere_to_UV(object_space_pos), 0).r;
	elevation = tanh(elevation);
	VERTEX += NORMAL * height_scale * pow(elevation,4);
}


void fragment() {
	
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
    //vec3 surface_texture = textureLod(albedo_map, point_on_sphere_to_UV(object_space_pos), 0).rgb;
	vec3 surface_texture = textureLod(albedo_map, vec2(elevation, 0.0), 0).rgb;

	int a = (subdivision_level & 1) != 0 ? 1 : 0;
	int b = (subdivision_level & 2) != 0 ? 1 : 0;
	int c = (subdivision_level & 4) != 0 ? 1 : 0;
	vec3 debug_color = vec3(float(a), float(b), float(c));
	ALBEDO = is_debug ? debug_color : surface_texture;


}