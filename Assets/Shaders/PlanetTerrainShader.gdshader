shader_type spatial;
#define sqrt2 1.414213562
uniform vec4[30] position_list;
uniform float radius;
uniform float grid_size;
varying float elevation;

uniform sampler2D albedo_map : repeat_disable;
uniform sampler2D height_map : repeat_disable;
uniform sampler2D normal_map : repeat_disable;

uniform bool is_colorize_lod;
uniform bool is_cube;
uniform bool is_texture_1D;
uniform bool is_culling;
uniform bool is_morphing;

uniform float height_scale;
uniform float normal_strength;
uniform int resolution;
uniform mat4 planet_transform_matrix;

uniform sampler2D key_image : repeat_disable;

uniform vec3 camera_position;
uniform float fov;
uniform vec2 morph_range;
uniform float sub_factor;

mat4 get_rotation() {
	return mat4(
		vec4(planet_transform_matrix[0].xyz / length(planet_transform_matrix[0].xyz), 0), 
		vec4(planet_transform_matrix[1].xyz / length(planet_transform_matrix[1].xyz), 0), 
		vec4(planet_transform_matrix[2].xyz / length(planet_transform_matrix[2].xyz), 0),
		vec4(0, 0, 0, 1)
	);
}

int findMSB64(uvec2 key) {
	return (key.x == 0u) ? int(findMSB(key.y)) : int((findMSB(key.x) + 32u));
}

vec3 morphVertex(vec2 vertex, float morphK) {
	float patchTessFactor = float(resolution - 1);
	vec2 fracPart = fract(vertex.xy * patchTessFactor * 0.5);
	fracPart *= 2.0 / patchTessFactor;

	vec2 intPart = floor(vertex.xy * patchTessFactor * 0.5);
	vec2 signVec = mod(intPart, 2.0) * vec2(-2.0) + vec2(1.0);
	return vec3(vertex.xy - (signVec * fracPart) * morphK, 1);
}

uvec2 rightShift64(uvec2 nodeID, uint shift) {
	uvec2 result;
	if (shift == 0u) return nodeID;

	if (shift < 32u) {
		result.y = (nodeID.y >> shift) | (nodeID.x << (32u - shift));
		result.x = nodeID.x >> shift;
	} else {
		result.y = nodeID.x >> (shift - 32u);
		result.x = 0u;
	}

	return result;
}

vec2 getTranslation(uint b1) {
	vec2 translation;
	translation.x = float(b1 & 0x1u);
	translation.y = float(b1 ^ 0x1u);
	return translation * 0.5;
}

ivec2 quickPI_2(int a) {
	int b = a & 3;
	int b1 = b >> 1;
	int b2 = b & 1;
	int bn2 = b2 ^ 1;
	int c = bn2 - (2 * (b1 & bn2));
	int s = b2 - (2 * (b1 & b2));
	return ivec2(c, s);
}

vec2 rotate(int rotationIndex, vec2 translation) {
	vec2 r;
	ivec2 trig = quickPI_2(rotationIndex);
	r.x = float(trig.x) * translation.x - float(trig.y) * translation.y;
	r.y = float(trig.y) * translation.x + float(trig.x) * translation.y;
	return r;
}

int getRotation(uint b1b2, uint b1, uint b2) {
	uint a = (b1b2 ^ 0x2u);
	uint b = (a | 0x1u);
	uint c = (b1 ^ b2);
	return int(b * c);
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

uint getBranching(uvec2 key, int level, int msb) {
	return (rightShift64(key, uint(msb - 2) - uint(level * 2)).y & 0x3u);
}

vec3 local_space_to_world_space(vec3 point) {
	return (vec4(point, 1) * planet_transform_matrix).xyz;
}

vec3 to_local_space(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
	return vertexA * point.x + vertexB * point.y + vertexC * (1.0 - point.x - point.y);
}

vec3 quad_space_to_local_space(vec2 point, uint meshPolygonID, uint rootID) {
	uint vertexBaseIndex = meshPolygonID * 5u;
	uint vertexKeyA = rootID;
	uint vertexKeyB = ((rootID >> 1u) ^ 1u) + ((rootID & 1u) << 1u);

	vec3 base_Triangle_a = (position_list[vertexBaseIndex + vertexKeyA + 1u]).xyz;
	vec3 base_Triangle_b = (position_list[vertexBaseIndex + vertexKeyB + 1u]).xyz;
	vec3 base_Triangle_c = (position_list[vertexBaseIndex]).xyz;

	return to_local_space(point, base_Triangle_a, base_Triangle_b, base_Triangle_c);
}

mat3 leaf_space(uvec2 key) {
	int msb = findMSB64(key);
	vec2 translation = vec2(0, 0);
	vec2 temp = vec2(0, 0);
	int theta = 0;
	float scale = 1.0;

	for (int i = 0; i < msb / 2; i++) {
		uint b1b2 = getBranching(key, i, msb);
		uint b1 = b1b2 >> 1u;
		uint b2 = b1b2 & 1u;
		temp = scale * getTranslation(b1);

		translation += rotate(theta, temp);
		theta += getRotation(b1b2, b1, b2);
		scale *= 0.5;
	}

	ivec2 trig = quickPI_2(theta);

	mat3 transform_matrix = mat3(
		vec3(float(trig.x) * scale, float(-trig.y) * scale, translation.x),
		vec3(float(trig.y) * scale, float(trig.x) * scale, translation.y),
		vec3(0.0, 0.0, 1.0)
	);

	return transform_matrix;
}

vec2 leaf_space_to_quad_space(vec3 point, uvec2 key) {
    mat3 leaf_matrix = leaf_space(key);
    return (point * leaf_matrix).xy;
}

int getLevelInKey(uvec2 key) {
    return findMSB64(key) / 2;
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
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / PI + 1.0) * 0.5;
	float v = latitude / PI + 0.5;
	return vec2(u, v);
}

float calculateLOD(float dist, float fovy, float factor) {
    float num = dist * tan(fovy/2.0);
    float dom = sqrt2 * factor;
    return clamp(-log2(num/dom), -1, 31);
}

float distanceFromCam(vec3 from, bool useHeight) {
    vec3 point = local_space_to_world_space(from);
    if (useHeight)
    {
        vec2 uv = point_on_sphere_to_UV(from);
        float height = textureLod(height_map, uv, 0).x;
        vec3 normal = (vec4(from, 1) * get_rotation()).xyz;
        point += (normal * height * height_scale);
    }
    return distance(point, camera_position.xyz);
}

float calculateLODToCam(vec3 from, bool useHeight) {
    return calculateLOD(
        distanceFromCam(from, useHeight),
        fov, // Must be in radians
        sub_factor
    );
}

float calculateMorph(vec3 point, uvec2 key) {
	float current_lod = float(getLevelInKey(key));
	// Ensure the lod calculated is in the range of the key's lod
	float calculated_lod = clamp(calculateLODToCam(point, true), current_lod - 1.0, current_lod);
	float percentage = current_lod - calculated_lod;

	if (percentage < morph_range.x)
		return 0.0;
	if (percentage > morph_range.y)
		return 1.0;
		
	if (percentage >= morph_range.x && percentage <= morph_range.y)
		return (percentage - morph_range.x) / (morph_range.y - morph_range.x);

	return 0.0;
}

vec2 get_polygon_uv(vec3 point, uint polygon_id) {
	uint vertexBaseIndex = polygon_id * 5u;
	ivec3 polygon_normal = ivec3(position_list[vertexBaseIndex].xyz);
	point = (point + 1.0) / 2.0;
	
	int index = -4;
	int flag = 0;
	for(int i = 0; i < 3; i++) {
		if (polygon_normal[i] != 0) {
			index = i;
			flag = polygon_normal[i] == -1 ? 1 : 0;
			break;
		}
	}
	vec2 uv = vec2(0);
	int inputA = (index + 1) % 3;
	int inputB = (index + 2) % 3;
	
	uv = vec2(point[min(inputA, inputB)], point[max(inputA, inputB)]);
	uv = abs(vec2(0.0, float(flag)) - uv);
	
	//return vec2(0, uv.y);
	return uv;
	
	//int inputA = (index + 1) % 3;
	//int inputB = (index + 2) % 3;
	//
	//vec2 uv = vec2(point[min(inputA, inputB)], point[max(inputA, inputB)]);
	//return vec2(uv.x, 0);
	
   
	
}

ivec2 getKeyCoordinate(int idx) {
	ivec2 image_size = ivec2(textureSize(key_image, 0));
	return ivec2(idx % image_size.x, idx / image_size.y);
}

vec3 hslToRgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

varying flat uvec4 key;
varying vec3 cube_local_position;
varying vec3 sphere_local_position;

void vertex() {
	key = is_culling ? floatBitsToUint(texelFetch(key_image, getKeyCoordinate(int(INSTANCE_ID)), 0)) : floatBitsToUint(INSTANCE_CUSTOM);
	
	uint flags = key.w >> 29u;
	key.w &= 0xFu;
	
	vec2 quad_position = leaf_space_to_quad_space(VERTEX, key.xy);
	vec3 local_position = quad_space_to_local_space(quad_position, key.z, key.w);
	local_position = is_cube ? local_position : point_on_cube_to_point_on_sphere(local_position);
	
	float morph = is_morphing ? calculateMorph(local_position, key.xy) : 0.0;
	vec2 morphed_quad_position = leaf_space_to_quad_space(morphVertex(VERTEX.xy, morph), key.xy);
	vec3 morphed_local_position = quad_space_to_local_space(morphed_quad_position, key.z, key.w);

	cube_local_position = morphed_local_position;
	sphere_local_position = point_on_cube_to_point_on_sphere(morphed_local_position);
	morphed_local_position = is_cube ? cube_local_position : sphere_local_position;
	
	vec3 morphed_world_position = local_space_to_world_space(morphed_local_position);
	
	elevation = textureLod(height_map, point_on_sphere_to_UV(morphed_local_position), 0).r;
	NORMAL = (vec4(morphed_local_position, 1) * get_rotation()).xyz;
	VERTEX = morphed_world_position + NORMAL * height_scale * elevation;
}

void fragment() {
	float msb = float(getLevelInKey(key.xy)) / 12.0;
	vec3 debug_color = hslToRgb(vec3(msb, 1.0, 1.0));
	

	vec2 sphere_uv_point = point_on_sphere_to_UV(sphere_local_position);
	vec3 surface_texture = textureLod(albedo_map, is_texture_1D ? vec2(elevation, 0.0) : sphere_uv_point, 0.0).rgb;
	

	vec2 polygon_uv = get_polygon_uv(cube_local_position, key.z);
	vec2 cell = floor(polygon_uv * grid_size);
	float checker = mod(cell.x + cell.y, 2.0);
	vec3 color1 = vec3(1);
	vec3 color2 = vec3(0);
	
	debug_color = mix(color1, color2, checker);

	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	ALBEDO = is_colorize_lod ? debug_color : surface_texture;
	
	
}

