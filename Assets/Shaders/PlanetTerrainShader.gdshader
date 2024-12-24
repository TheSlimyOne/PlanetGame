#define sqrt2 1.414213562
shader_type spatial;

uniform vec4[30] position_list;
uniform float radius;
uniform float border_size;
uniform float center_size;
uniform int total_texture_subdivisions;
uniform float grid_size;
uniform int desired_chunk_size;
varying float elevation;

uniform sampler2D albedo_map : repeat_disable, source_color;
uniform sampler2D height_map : repeat_disable, source_color;
uniform sampler2D normal_map : repeat_disable, source_color;
uniform sampler2D tile_cache : repeat_disable, source_color;
uniform sampler2DArray indirection_table : repeat_disable, source_color;

uniform bool is_colorize_lod;
uniform bool is_cube;
uniform bool is_texture_1D;
uniform bool is_culling;
uniform bool is_morphing;

uniform float height_scale;
uniform float normal_strength;
uniform int resolution;
uniform float maximum_lod;
uniform mat4 planet_transform_matrix;

uniform vec3 camera_position;
uniform float fovy;
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

int find_msb_64(uvec2 key) {
	return (key.x == 0u) ? int(findMSB(key.y)) : int((findMSB(key.x) + 32u));
}

vec3 morph_vertex(vec2 vertex, float morph_k) {
	float patch_tess_factor = float(resolution - 1);
	vec2 fract_part = fract(vertex.xy * patch_tess_factor * 0.5);
	fract_part *= 2.0 / patch_tess_factor;

	vec2 int_part = floor(vertex.xy * patch_tess_factor * 0.5);
	vec2 sign_vec = mod(int_part, 2.0) * vec2(-2.0) + vec2(1.0);
	return vec3(vertex.xy - (sign_vec * fract_part) * morph_k, 1);
}

uvec2 right_shift_64(uvec2 node_id, uint shift) {
	uvec2 result;
	if (shift == 0u) return node_id;

	if (shift < 32u) {
		result.y = (node_id.y >> shift) | (node_id.x << (32u - shift));
		result.x = node_id.x >> shift;
	} else {
		result.y = node_id.x >> (shift - 32u);
		result.x = 0u;
	}

	return result;
}

vec2 get_translation(uint b1) {
	vec2 translation;
	translation.x = float(b1 & 0x1u);
	translation.y = float(b1 ^ 0x1u);
	return translation * 0.5;
}

ivec2 quick_pi_2(int a) {
	int b = a & 3;
	int b1 = b >> 1;
	int b2 = b & 1;
	int bn2 = b2 ^ 1;
	int c = bn2 - (2 * (b1 & bn2));
	int s = b2 - (2 * (b1 & b2));
	return ivec2(c, s);
}

vec2 rotate(int rotation_index, vec2 translation) {
	vec2 r;
	ivec2 trig = quick_pi_2(rotation_index);
	r.x = float(trig.x) * translation.x - float(trig.y) * translation.y;
	r.y = float(trig.y) * translation.x + float(trig.x) * translation.y;
	return r;
}

int get_rotation(uint b1b2, uint b1, uint b2) {
	uint a = (b1b2 ^ 0x2u);
	uint b = (a | 0x1u);
	uint c = (b1 ^ b2);
	return int(b * c);
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

uint get_branching(uvec2 key, int level, int msb) {
	return (right_shift_64(key, uint(msb - 2) - uint(level * 2)).y & 0x3u);
}


vec3 to_polygon_space(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
	return vertexA * point.x + vertexB * point.y + vertexC * (1.0 - point.x - point.y);
}

vec3 quad_space_to_polygon_space(vec2 point, uint meshPolygonID, uint rootID) {
	uint vertexBaseIndex = meshPolygonID * 5u;
	uint vertexKeyA = rootID;
	uint vertexKeyB = ((rootID >> 1u) ^ 1u) + ((rootID & 1u) << 1u);

	vec3 base_Triangle_a = (position_list[vertexBaseIndex + vertexKeyA + 1u]).xyz;
	vec3 base_Triangle_b = (position_list[vertexBaseIndex + vertexKeyB + 1u]).xyz;
	vec3 base_Triangle_c = (position_list[vertexBaseIndex]).xyz;

	return to_polygon_space(point, base_Triangle_a, base_Triangle_b, base_Triangle_c);
}

mat3 leaf_space(uvec2 key) {
	int msb = find_msb_64(key);
	vec2 translation = vec2(0, 0);
	vec2 temp = vec2(0, 0);
	int theta = 0;
	float scale = 1.0;

	for (int i = 0; i < msb / 2; i++) {
		uint b1b2 = get_branching(key, i, msb);
		uint b1 = b1b2 >> 1u;
		uint b2 = b1b2 & 1u;
		temp = scale * get_translation(b1);

		translation += rotate(theta, temp);
		theta += get_rotation(b1b2, b1, b2);
		scale *= 0.5;
	}

	ivec2 trig = quick_pi_2(theta);

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

int get_lod_of_key(uvec2 key) {
    return find_msb_64(key) / 2;
}

vec3 height_to_normal(float height, float strength, vec3 vertex, vec3 normal) {
	vec3 d_x = dFdx(vertex);
	vec3 d_y = dFdy(vertex);

	vec3 cross_x = cross(normal, d_x);
	vec3 cross_y = cross(d_y, normal);

	float d_t = dot(d_x, cross_y);
	float sign = d_t < 0.0 ? -1.0 : 1.0;
	float surface = sign / max(1.192093e-12, abs(d_t));

	float d_hdx = dFdx(height);
	float d_hdy = dFdy(height);

	vec3 surface_gradient = surface * (d_hdx * cross_y + d_hdy * cross_x);

	return normalize(normal - strength * surface_gradient);
}


vec2 point_on_sphere_to_uv(vec3 p) {
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / PI + 1.0) * 0.5;
	float v = latitude / PI + 0.5;
	return vec2(u, v);
}

float calculate_lod(float dist) {
    float num = dist * fovy;
    float dom = sqrt2 * sub_factor * radius;
    return clamp(-log2(num/dom), -1, maximum_lod);
}

int get_normal_id(ivec3 normal) {
	ivec3 p = ivec3(bvec3(normal.x == 1, normal.y == 1, normal.z == 1));
	ivec3 n = ivec3(bvec3(normal.x == -1, normal.y == -1, normal.z == -1));
	return p.x * 0 + p.y * 1 + p.z * 2 + n.x * 3 + n.y * 4 + n.z * 5;
}

vec3 polygon_space_to_world_space(vec3 point, bool use_height) {
	point = (vec4(point, 1) * planet_transform_matrix).xyz;
	if (use_height) {
		vec2 uv = point_on_sphere_to_uv(point);
		float height = textureLod(height_map, uv, 0).x;
		vec3 normal = (vec4(point, 1) * get_rotation()).xyz;
		point += (normal * height * height_scale);
	}
	return point;
}

float distance_from_cam(vec3 from, bool use_height) {
    return distance(polygon_space_to_world_space(from, use_height), camera_position.xyz);
}

float calculate_lod_to_cam(vec3 from, bool use_height) {
    return calculate_lod(distance_from_cam(from, use_height));
}

float get_percentage(vec3 point, uvec2 key)
{
	float current_lod = float(get_lod_of_key(key));
	//Ensure the lod calculated is in the range of the key's lod
	float calculated_lod = clamp(calculate_lod_to_cam(point, false), current_lod - 1.0, current_lod);
	return current_lod - calculated_lod;
}

float calculate_morph(vec3 point, uvec2 key) {
	float percentage = get_percentage(point, key);

	if (percentage < morph_range.x)
		return 0.0;
	if (percentage > morph_range.y)
		return 1.0;

	if (percentage >= morph_range.x && percentage <= morph_range.y)
		return (percentage - morph_range.x) / (morph_range.y - morph_range.x);

	return percentage;
}

ivec3 get_init_normal(uint polygon_id) {
	uint vertex_base_index = polygon_id * 5u;
	return ivec3(position_list[vertex_base_index].xyz);
}

vec2 get_polygon_uv(vec3 point, ivec3 normal) {
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
	int input_a = (index + 1) % 3;
	int input_b = (index + 2) % 3;

	uv = vec2(point[min(input_a, input_b)], point[max(input_a, input_b)]);
	uv = abs(vec2(0.0, float(flag)) - uv);

	return uv;
}

vec3 hslToRgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

varying flat uvec4 key;
varying flat uint flags;
varying vec3 cube_local_position;
varying vec3 sphere_local_position;
varying float morph;
void vertex() {

	key = floatBitsToUint(INSTANCE_CUSTOM);

	flags = key.w >> 29u;
	key.w &= 0xFu;

	vec2 quad_position = leaf_space_to_quad_space(VERTEX, key.xy);
	vec3 local_position = quad_space_to_polygon_space(quad_position, key.z, key.w);
	local_position = is_cube ? local_position : point_on_cube_to_point_on_sphere(local_position);
	morph = is_morphing ? calculate_morph(local_position, key.xy) : 0.0;

	vec2 morphed_quad_position = leaf_space_to_quad_space(morph_vertex(VERTEX.xy, morph), key.xy);
	vec3 morphed_local_position = quad_space_to_polygon_space(morphed_quad_position, key.z, key.w);

	cube_local_position = morphed_local_position;
	sphere_local_position = point_on_cube_to_point_on_sphere(morphed_local_position);
	morphed_local_position = is_cube ? cube_local_position : sphere_local_position;

	vec3 morphed_world_position = polygon_space_to_world_space(morphed_local_position, false);

	elevation = textureLod(height_map, point_on_sphere_to_uv(morphed_local_position), 0.0).r;
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
	float msb = float(get_lod_of_key(key.xy)) / 12.0;
	vec3 debug_color = hslToRgb(vec3(msb, 1.0, 0.5));
	float child = float(key.y & 3u);

	vec2 sphere_uv_point = point_on_sphere_to_uv(sphere_local_position);
	vec3 surface_texture = textureLod(albedo_map, is_texture_1D ? vec2(elevation, 0.0) : sphere_uv_point, 0.0).rgb;

	ivec3 init_normal = get_init_normal(key.z);
	vec2 polygon_uv = get_polygon_uv(cube_local_position, init_normal);

	int lod = int(calculate_lod_to_cam(sphere_local_position, false));

	// clamp lods out of bounds
	int lod_ratio = min(lod / total_texture_subdivisions, total_texture_subdivisions - 1);

	float lod_grid_size = grid_size / pow(2.0, float(total_texture_subdivisions - 1 - lod_ratio));
	int texture_idx = total_texture_subdivisions * get_normal_id(init_normal) + lod_ratio;

	vec2 cell_uv = (fract(polygon_uv * lod_grid_size));
	vec3 cell_id = vec3((floor(polygon_uv * lod_grid_size) + vec2(0.5)) / lod_grid_size, float(texture_idx));

	vec3 indirection_id = textureLod(indirection_table, cell_id, 0.0).xyz;

	vec2 cell_uv_min = indirection_id.xy;
	vec2 cell_uv_max = cell_uv_min.xy + (1.0 / (grid_size));

	vec2 grid_uv = cell_uv_min + (cell_uv_max - cell_uv_min) * cell_uv;

	vec2 cell = floor(polygon_uv * grid_size);
	float checker = mod(cell.x + cell.y, 2.0);
	vec3 color1 = mix(vec3(0.5), debug_color, 0.3);
	vec3 color2 = mix(vec3(0), debug_color, 0.3);

	//debug_color = mix(color1, color2, checker);
	//debug_color = mix(debug_color, vec3(child/3.0), 0.2);

	//debug_color = mix(flags != 4u ? vec3(1,0,0) : vec3(0), debug_color, 0.5);
	//debug_color = mix(vec3(morph, 0.0, 0.0), debug_color, 0.5);
	debug_color = vec3(morph, 0, 0);
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	ALBEDO = is_colorize_lod ? debug_color : surface_texture;

	//ALBEDO = vec3(0);
	//if (get_normal_id(init_normal) == 2)
	//{
		//ALBEDO = is_colorize_lod ? textureLod(tile_cache, grid_uv, 0.0).rgb : vec3(cell_uv_min, 0);
//
	//}

}
