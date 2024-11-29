shader_type spatial;
#define sqrt2 1.414213562
uniform vec4[30] position_list;
uniform float radius;
uniform float border_size;
uniform float center_size;
uniform int total_texture_subdivisions;
uniform float grid_size;
uniform int desired_chunk_size;
varying float elevation;

uniform sampler2D albedo_map : repeat_disable, source_color;
uniform sampler2D height_map : repeat_disable;
uniform sampler2D normal_map : repeat_disable, source_color;
uniform sampler2DArray indirection_table : repeat_disable, source_color;

uniform bool is_colorize_lod;
uniform bool is_cube;
uniform bool is_texture_1D;
uniform bool is_culling;
uniform bool is_morphing;

uniform float height_scale;
uniform float normal_strength;
uniform int resolution;
uniform mat4 planet_transform_matrix;

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
    float num = dist * fovy;
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
        fov,
        sub_factor
    );
}

float getPercentage(vec3 point, uvec2 key)
{
	float current_lod = float(getLevelInKey(key));
	// Ensure the lod calculated is in the range of the key's lod
	float calculated_lod = clamp(calculateLODToCam(point, true), current_lod - 1.0, current_lod);

	return current_lod - calculated_lod;
}

float calculateMorph(vec3 point, uvec2 key) {
	float percentage = getPercentage(point, key);

	if (percentage < morph_range.x)
		return 0.0;
	if (percentage > morph_range.y)
		return 1.0;

	if (percentage >= morph_range.x && percentage <= morph_range.y)
		return (percentage - morph_range.x) / (morph_range.y - morph_range.x);

	return 0.0;
}

ivec3 get_init_normal(vec3 point, uint polygon_id) {
	uint vertexBaseIndex = polygon_id * 5u;
	return ivec3(position_list[vertexBaseIndex].xyz);
}

vec2 get_polygon_uv(vec3 point, uint polygon_id) {
	ivec3 normal = get_init_normal(point, polygon_id);
	point = (point + 1.0) / 2.0;

	int index = -4;
	int flag = 0;
	for(int i = 0; i < 3; i++) {
		if (normal[i] != 0) {
			index = i;
			flag = normal[i] == -1 ? 1 : 0;
			break;
		}
	}

	vec2 uv = vec2(0);
	int inputA = (index + 1) % 3;
	int inputB = (index + 2) % 3;

	uv = vec2(point[min(inputA, inputB)], point[max(inputA, inputB)]);
	uv = abs(vec2(0.0, float(flag)) - uv);

	return uv;
}

vec3 hslToRgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

varying flat uvec4 key;
varying vec3 cube_local_position;
varying vec3 sphere_local_position;
varying float morph;
void vertex() {

	key = floatBitsToUint(INSTANCE_CUSTOM);

	uint flags = key.w >> 29u;
	key.w &= 0xFu;

	vec2 quad_position = leaf_space_to_quad_space(VERTEX, key.xy);
	vec3 local_position = quad_space_to_local_space(quad_position, key.z, key.w);
	local_position = is_cube ? local_position : point_on_cube_to_point_on_sphere(local_position);

	morph = is_morphing ? calculateMorph(local_position, key.xy) : 0.0;
	vec2 morphed_quad_position = leaf_space_to_quad_space(morphVertex(VERTEX.xy, morph), key.xy);
	vec3 morphed_local_position = quad_space_to_local_space(morphed_quad_position, key.z, key.w);

	cube_local_position = morphed_local_position;
	sphere_local_position = point_on_cube_to_point_on_sphere(morphed_local_position);
	morphed_local_position = is_cube ? cube_local_position : sphere_local_position;

	vec3 morphed_world_position = local_space_to_world_space(morphed_local_position);

	elevation = textureLod(height_map, point_on_sphere_to_UV(morphed_local_position), 0.0).r;
	NORMAL = (vec4(morphed_local_position, 1) * get_rotation()).xyz;
	VERTEX = morphed_world_position + NORMAL * height_scale * elevation;
}

vec2 rotate_uv(vec2 uv, float angle) {
    float cos_angle = cos(angle);
    float sin_angle = sin(angle);
    mat2 rotation_matrix = mat2(vec2(cos_angle, -sin_angle), vec2(sin_angle, cos_angle));
    return rotation_matrix * uv;
}

void fragment() {
	float msb = float(getLevelInKey(key.xy)) / 12.0;
	vec3 debug_color = hslToRgb(vec3(msb, 1.0, 0.5));
	float child = float(key.y & 3u);

	vec2 sphere_uv_point = point_on_sphere_to_UV(sphere_local_position);
	vec3 surface_texture = textureLod(albedo_map, is_texture_1D ? vec2(elevation, 0.0) : sphere_uv_point, 0.0).rgb;

	vec2 polygon_uv = get_polygon_uv(cube_local_position, key.z);
	vec2 rotated_uv = rotate_uv(polygon_uv, radians(45.0));
	rotated_uv *= sqrt(2.0);
	int level = getLevelInKey(key.xy);
	//vec2 cell = floor(polygon_uv * (pow(2.0, float(level))));
	vec2 cell = floor(polygon_uv * grid_size) / grid_size;

	ivec3 init_normal = get_init_normal(cube_local_position, key.z);
	int texture_idx = 0;
	for (int i = 0; i < 3; i++) {
		if (init_normal[i] != 0) {
			texture_idx = i + 3 * (init_normal[i] == 1 ? 0 : 1);
			break;
		}
	}


	//cell = vec2(cell.x + float(id) * grid_size, cell.y) / vec2(grid_size * 6.0, grid_size);
	//cell = cell / grid_size;

	//vec3 color = textureLod(indirection_table, vec3(cell.x, cell.y, float(id)), 0).xyz;

	int lod = int(calculateLODToCam(sphere_local_position, true));
	//vec3 color = vec3(float(f & 1),float(f & 2),float(f & 4));

	texture_idx = total_texture_subdivisions * texture_idx + (lod % total_texture_subdivisions);
	//color = vec3(cell.x, cell.y, float(f & 1));
	vec3 color = textureLod(indirection_table, vec3(0, 0, float(texture_idx)), 0.0).xyz;
	//float checker = mod(cell.x + cell.y, 2.0);
	//vec3 color1 = mix(vec3(0.5), debug_color, 0.3);
	//vec3 color2 = mix(vec3(0), debug_color, 0.3);
//
	//debug_color = mix(color1, color2, checker);
	//debug_color = vec3(float(texture_idx)/18.0, 0, 0);
	//debug_color = vec3(float((texture_idx & 4) >> 2),float((texture_idx & 2) >> 1),float(texture_idx & 1));
	//debug_color = vec3(float(id)/6.0);
	debug_color = color;
	//debug_color = mix(debug_color, vec3(child/3.0), 0.2);
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	//NORMAL_MAP = textureLod(normal_map, sphere_uv_point, 0.0).rgb;
	//NORMAL_MAP_DEPTH = normal_strength;
	ALBEDO = is_colorize_lod ? debug_color : surface_texture;

	//ALBEDO = vec3(morph);

}
