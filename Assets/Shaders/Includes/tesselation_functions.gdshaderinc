#include "res://Assets/Shaders/Constants.gdshaderinc"



ivec2 quickPI_2(int a) {
    int b = a & 3;
    int b1 = b >> 1;
    int b2 = b & 1;
    int bn2 = b2 ^ 1;
    int c = bn2 - (2 * (b1 & bn2));
    int s = b2 - (2 * (b1 & b2));
    return ivec2(c, s);
}

int find_msb_64(uvec2 key) {
    return (key.x == 0u) ? int(findMSB(key.y)) : int((findMSB(key.x) + 32u));
}

uvec2 left_shift_64(uvec2 node_id, uint shift) {
    uvec2 result;
    if (shift == 0u) return node_id;

    if (shift < 32u) {
        result.x = (node_id.x << shift) | (node_id.y >> (32u - shift));
        result.y = node_id.y << shift;
    } else {
        result.x = node_id.y << (shift - 32u);
        result.y = 0u;
    }
    return result;
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

uint get_branching(uvec2 key, int level, int msb) {
    return (right_shift_64(key, uint(msb - 2) - uint(level * 2)).y & 0x3u);
}

vec2 point_on_sphere_to_uv(vec3 p) {
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / PI + 1.0) * 0.5;
    float v = latitude / PI + 0.5;
    return vec2(u, v);
}

vec2 get_translation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1u);
    translation.y = float(b1 ^ 0x1u);
    return translation;
}

int get_rotation(uint b1b2) {
    uint b1 = b1b2 >> 1u;
    uint b2 = b1b2 & 1u;

    uint a = (b1b2 ^ 0x2u);
    uint b = (a | 0x1u);
    uint c = (b1 ^ b2);
    return int(b * c);
}

vec2 rotate(int rotation_index, vec2 translation) {
	vec2 r;
	ivec2 trig = quickPI_2(rotation_index);
	r.x = float(trig.x) * translation.x - float(trig.y) * translation.y;
	r.y = float(trig.y) * translation.x + float(trig.x) * translation.y;
	return r;
}

mat3 leaf_space_to_quadtree_space(uvec2 key) {
    int msb = find_msb_64(key);
    vec2 translation = vec2(0, 0);
	vec2 temp = vec2(0, 0);
	int theta = 0;
	float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = get_branching(key, i, msb);
        uint b1 = b1b2 >> 1u;

        temp = scale * get_translation(b1) * 0.5;

        translation += rotate(theta, temp);
        theta += get_rotation(b1b2);
        scale *= 0.5;
    }

    ivec2 trig = quickPI_2(theta);
    mat3 transform_matrix = mat3(
        vec3(float(trig.x) * scale, float(-trig.y) * scale, translation.x),
        vec3(float(trig.y) * scale, float(trig.x)  * scale, translation.y),
        vec3(0.0,                   0.0,                    1.0)
    );

    return transform_matrix;
}

vec3[3] get_base_primitive(uint mesh_polygon_id, uint root_id) {
	vec3 normal = normals[mesh_polygon_id];
    vec3 axis_a = normal.yzx;
    vec3 axis_b = cross(normal, axis_a);

	uint b1b2 = root_id;
    int b1 = int(b1b2 >> 1u);
    int b2 = int(b1b2 & 1u);

    int l0 = 2 * b2 - 1;
    int r0 = 2 * b1 - 1;
    int l1 = -r0;
    int r1 = l0;

    vec3[] base_primitive = {
        float(l0) * axis_a + float(l1) * axis_b + normal,
        float(r0) * axis_a + float(r1) * axis_b + normal,
        normal
    };

    return base_primitive;
}

mat3 quadtree_space_to_polygon_space(uint mesh_polygon_id, uint root_id) {
    vec3[3] base_primitive = get_base_primitive(mesh_polygon_id, root_id);

    return mat3(
		base_primitive[0] - base_primitive[2],
		base_primitive[1] - base_primitive[2],
		base_primitive[2]
    );
}

vec3 get_world_space_point(vec3 point, mat4 transform_matrix) {
    point = (vec4(point, 1) * transform_matrix).xyz;
    return point;
}

vec2 get_quadtree_space_point(vec2 point, mat3 quadtree_space_matrix) {
    return (vec3(point, 1) * quadtree_space_matrix).xy;
}

vec3 get_polygon_space_point(vec2 point, mat3 polygon_space_matrix) {
    return (polygon_space_matrix * vec3(point, 1));
}

vec3 get_barycentric_space_point(vec2 point) {
    return (barycentric_space_matrix * vec3(point, 1));
}

int get_lod_of_key(uvec2 key) {
    return find_msb_64(key) / 2;
}

float calculate_lod(float dist, float fovy, float sub_factor, float radius, float maximum_lod, float minimum_lod) {
    float num = sqrt2 * sub_factor * radius;
    float dom = dist * fovy;
    return clamp(log2(num / dom), minimum_lod, maximum_lod);
}

int get_normal_id(ivec3 normal) {
    ivec3 p = ivec3(bvec3(normal.x == 1, normal.y == 1, normal.z == 1));
    ivec3 n = ivec3(bvec3(normal.x == -1, normal.y == -1, normal.z == -1));
    return p.x * 0 + n.x * 1 + p.y * 2 + n.y * 3 + p.z * 4 + n.z * 5;
}

float distance_from_cam(vec3 from, vec3 camera_position, mat4 planet_transform_matrix) {
    return distance(get_world_space_point(from, planet_transform_matrix), camera_position);
}

float calculate_lod_to_cam(vec3 from, vec3 camera_position, mat4 planet_transform_matrix, float fovy, float sub_factor, float radius, float maximum_lod, float minimum_lod) {
    return calculate_lod(distance_from_cam(from, camera_position, planet_transform_matrix), fovy, sub_factor, radius, maximum_lod, minimum_lod);
}

float get_percentage(vec3 point, uvec2 key, vec3 camera_position, mat4 planet_transform_matrix, float fovy, float sub_factor, float radius, float maximum_lod, float minimum_lod) {
    float current_lod = float(get_lod_of_key(key));
    float calculated_lod = clamp(calculate_lod_to_cam(point, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod, minimum_lod), current_lod - 1.0, current_lod);
    return current_lod - calculated_lod;
}

vec2 morph_vertex(vec2 vertex, float morph_k, int resolution) {
    float patch_tess_factor = float(resolution - 1);
    vec2 fract_part = fract(vertex * patch_tess_factor * 0.5f);
    fract_part *= 2.0 / patch_tess_factor;
    vec2 int_part = floor(vertex.xy * patch_tess_factor * 0.5);
    vec2 sign_vec = mod(int_part, 2.0) * vec2(-2.0) + vec2(1.0);
    return vertex - (sign_vec * fract_part) * morph_k;
}

const float quantize_factor = 12.0;
float calculate_morph(vec3 point, uvec2 key, vec2 morph_range, vec3 camera_position, mat4 planet_transform_matrix, float fovy, float sub_factor, float radius, float maximum_lod, float minimum_lod) {
    float percentage = get_percentage(point, key, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod, minimum_lod);

    if (percentage < morph_range.x)
        return 0.0;
    if (percentage > morph_range.y)
        return 1.0;

    float normalized = (percentage - morph_range.x) / (morph_range.y - morph_range.x);

    normalized = round(normalized * quantize_factor) / quantize_factor;
    return normalized;
}
