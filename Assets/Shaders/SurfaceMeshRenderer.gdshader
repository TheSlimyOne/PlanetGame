shader_type spatial;
uniform float radius;
uniform float height_scale;
uniform float normal_strength;

uniform int octaves : hint_range(0, 12) = 5;
uniform float persistence = 0.5;
uniform float lacunarity = 2.0;
uniform float noise_scale = 1;
uniform float exaggeration_factor = 1;
uniform vec3 offset = vec3(0, 0, 0);
uniform float seed = 1207.01;
uniform int amount: hint_range(1, 100, 1);
uniform bool isCube;

uniform sampler2D albedo_map : repeat_disable;
uniform sampler2D image_texture : repeat_disable;

varying float elevation;
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

vec3 hash(vec3 p) {
	p = vec3(dot(p, vec3(127.1, 311.7, 74.7)),
		dot(p, vec3(269.5, 183.3, 246.1)),
		dot(p, vec3(113.5, 271.9, 124.6)));
	p += seed;
	return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
}

float noise(vec3 p) {
 	vec3 i = floor(p);
 	vec3 f = fract(p);
 	vec3 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(dot(hash(i + vec3(0.0, 0.0, 0.0)), f - vec3(0.0, 0.0, 0.0)),
                     dot(hash(i + vec3(1.0, 0.0, 0.0)), f - vec3(1.0, 0.0, 0.0)), u.x),
                 mix(dot(hash(i + vec3(0.0, 1.0, 0.0)), f - vec3(0.0, 1.0, 0.0)),
                     dot(hash(i + vec3(1.0, 1.0, 0.0)), f - vec3(1.0, 1.0, 0.0)), u.x), u.y),
             mix(mix(dot(hash(i + vec3(0.0, 0.0, 1.0)), f - vec3(0.0, 0.0, 1.0)),
                     dot(hash(i + vec3(1.0, 0.0, 1.0)), f - vec3(1.0, 0.0, 1.0)), u.x),
                 mix(dot(hash(i + vec3(0.0, 1.0, 1.0)), f - vec3(0.0, 1.0, 1.0)),
                     dot(hash(i + vec3(1.0, 1.0, 1.0)), f - vec3(1.0, 1.0, 1.0)), u.x), u.y), u.z );
}

float param_noise(vec3 p) {
	float frequency = 1.0;
	float amplitude = 1.0;
	float noiseHeight = 0.0;
	
	for (int i = 0; i < octaves; i++) {
		float sampleX = p.x / noise_scale * frequency + offset.x;
		float sampleY = p.y / noise_scale * frequency + offset.y;
		float sampleZ = p.z / noise_scale * frequency + offset.z;
		
		float noiseValue = noise(vec3(sampleX, sampleY, sampleZ) * 5.0) * 0.5;
		noiseValue += noise(vec3(sampleX, sampleY, sampleZ) * 10.0) * 0.25/2.0;
		noiseValue += noise(vec3(sampleX, sampleY, sampleZ) * 20.0) * 0.125/2.0;
		noiseValue += noise(vec3(sampleX, sampleY, sampleZ) * 40.0) * 0.0625/2.0;

		noiseHeight += noiseValue * amplitude;
		
		amplitude += persistence;
		frequency += lacunarity;
	}

	noiseHeight = pow(max(noiseHeight, 0), exaggeration_factor);
	
	return noiseHeight;
}

void vertex() {
	float scaleFactor = pow(2.0, INSTANCE_CUSTOM.a);
	
	switch (int(INSTANCE_CUSTOM.z)) {
        case 1:  NORMAL = vec3(0.0, 1.0, 0.0);  break;
        case 2:  NORMAL = vec3(0.0, -1.0, 0.0); break;
        case 3:  NORMAL = vec3(-1.0, 0.0, 0.0); break;
        case 4:  NORMAL = vec3(1.0, 0.0, 0.0);  break;
        case 5:  NORMAL =  vec3(0.0, 0.0, -1.0); break;
        case 6:  NORMAL = vec3(0.0, 0.0, 1.0); break;
    }
	
	
	vec3 axisA = vec3(NORMAL.y, NORMAL.z, NORMAL.x);
	vec3 axisB = cross(NORMAL, axisA);
	vec3 axicC = NORMAL * scaleFactor;
	axisA /= scaleFactor;
	axisB /= scaleFactor;

	vec3 point = NODE_POSITION_WORLD + axisA * (2.0 * UV.x - 1.0) + axisB * (2.0 * UV.y - 1.0);
	if (isCube)
	{
		VERTEX = point;
	}
	else
	{
		VERTEX = point_on_cube_to_point_on_sphere(point);
	}
	
	NORMAL = VERTEX;
	object_space_pos = VERTEX;
	VERTEX = radius * VERTEX - NODE_POSITION_WORLD;
	

	vec2 uv = point_on_sphere_to_UV(object_space_pos);
	
	float theta = uv.y * 3.14159;
	float phi = uv.x * 3.14159 * 2.0;
	vec3 unit = vec3(0.0);

	unit.x = sin(phi) * sin(theta);
	unit.y = cos(theta) * -1.0;
	unit.z = cos(phi) * sin(theta);
	
	unit = normalize(unit);

	elevation = textureLod(image_texture, point_on_sphere_to_UV(object_space_pos), 0).r;
	VERTEX += NORMAL * height_scale * elevation;
}


void fragment() {
	
	
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
    //vec3 surface_texture = textureLod(image_texture, point_on_sphere_to_UV(object_space_pos), 0).rgb;
	vec3 surface_texture = textureLod(albedo_map, vec2(elevation, 0.0), 0).rgb;

	ALBEDO = surface_texture;
}