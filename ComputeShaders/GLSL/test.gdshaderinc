#define sqrt2 1.414213562

const vec3 normals[6] = vec3[6](
    vec3(1.0, 0.0, 0.0),
    vec3(-1.0, 0.0, 0.0),
    vec3(0.0, 1.0, 0.0),
    vec3(0.0, -1.0, 0.0),
    vec3(0.0, 0.0, 1.0),
    vec3(0.0, 0.0, -1.0)
);

mat4 get_rotation(mat4 planet_transform_matrix) {
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

vec3 morph_vertex(vec2 vertex, float morph_k, int normal_id, int resolution) {
    float patch_tess_factor = float(resolution - 1);
    vec2 fract_part = fract(vertex * patch_tess_factor * 0.5f);
    fract_part *= 2.0 / patch_tess_factor;
    vec2 int_part = floor(vertex.xy * patch_tess_factor * 0.5);
    vec2 sign_vec = mod(int_part, 2.0) * vec2(-2.0) + vec2(1.0);
    return vec3(vertex - (sign_vec * fract_part) * morph_k, 1);
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

vec2 get_translation(uint b1b2) {
    uint b1 = b1b2 >> 1u;
    uint b2 = b1b2 & 1u;
    vec2 translation;

    translation.x = float(b2);
    translation.y = float(1u - b1);
    return translation;
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
    vec3 square = p * p;
    return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

uint get_branching(uvec2 key, int level, int msb) {
    return (right_shift_64(key, uint(msb - 2) - uint(level * 2)).y & 0x3u);
}

vec3 quad_space_to_polygon_space(
    vec2 point, 
    uint mesh_polygon_id, 
    uint root_id) {
		
    vec3 normal = normals[mesh_polygon_id];
    vec3 axis_a = normal.yzx;
    vec3 axis_b = cross(normal, axis_a);
    vec2 offset = get_translation(root_id);

    vec3 corner_vertex = normal + axis_a * offset.x + axis_b * offset.y;

    mat4 polygon_space_matrix = mat4(
        vec4(corner_vertex, 1),
        vec4(corner_vertex - axis_a, 1),
        vec4(corner_vertex - axis_a - axis_b, 1),
        vec4(corner_vertex - axis_b, 1));

    vec4 weight_matrix = vec4(
        (1.0 - point.x) * (1.0 - point.y),
        point.x * (1.0 - point.y),
        point.x * point.y,
        (1.0 - point.x) * point.y
    );

    return (polygon_space_matrix * weight_matrix).xyz;
}

mat3 leaf_space(uvec2 key) {
    int msb = find_msb_64(key);
    vec2 translation = vec2(0, 0);
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = get_branching(key, i, msb);
        translation += scale * get_translation(b1b2) * 0.5;
        scale *= 0.5;
    }

    mat3 transform_matrix = mat3(
        vec3(scale, 0.0,   translation.x),
        vec3(0.0,   scale, translation.y),
        vec3(0.0,   0.0,   1.0)
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

vec2 point_on_sphere_to_uv(vec3 p) {
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / PI + 1.0) * 0.5;
    float v = latitude / PI + 0.5;
    return vec2(u, v);
}

float calculate_lod(float dist, float fovy, float sub_factor, float radius, float maximum_lod) {
    float num = dist * fovy;
    float dom = sqrt2 * sub_factor * radius;
    return clamp(-log2(num / dom), 0, maximum_lod);
}

int get_normal_id(ivec3 normal) {
    ivec3 p = ivec3(bvec3(normal.x == 1, normal.y == 1, normal.z == 1));
    ivec3 n = ivec3(bvec3(normal.x == -1, normal.y == -1, normal.z == -1));
    return p.x * 0 + n.x * 1 + p.y * 2 + n.y * 3 + p.z * 4 + n.z * 5;
}

vec3 polygon_space_to_world_space(vec3 point, mat4 planet_transform_matrix) {
    return (vec4(point, 1) * planet_transform_matrix).xyz;
}

float distance_from_cam(vec3 from, vec3 camera_position, mat4 planet_transform_matrix) {
    return distance(polygon_space_to_world_space(from, planet_transform_matrix), camera_position);
}

float calculate_lod_to_cam(vec3 from, vec3 camera_position, mat4 planet_transform_matrix, float fovy, float sub_factor, float radius, float maximum_lod) {
    return calculate_lod(distance_from_cam(from, camera_position, planet_transform_matrix), fovy, sub_factor, radius, maximum_lod);
}

float get_percentage(vec3 point, uvec2 key, vec3 camera_position, mat4 planet_transform_matrix, float fovy, float sub_factor, float radius, float maximum_lod) {
    float current_lod = float(get_lod_of_key(key));
    float calculated_lod = clamp(calculate_lod_to_cam(point, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod), current_lod - 1.0, current_lod);
    return current_lod - calculated_lod;
}

float calculate_morph(vec3 point, uvec2 key, vec2 morph_range, vec3 camera_position, mat4 planet_transform_matrix, float fovy, float sub_factor, float radius, float maximum_lod) {
    float percentage = get_percentage(point, key, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod);

    if (percentage < morph_range.x)
        return 0.0;
    if (percentage > morph_range.y)
        return 1.0;

    return (percentage - morph_range.x) / (morph_range.y - morph_range.x);
}

vec2 get_cube_uv(int normal_id, vec3 point) {
    point = (point + 1.0) / 2.0;
    vec2 uv = vec2(0);
    uv.x = normal_id == 0 || normal_id == 1 ? point.z : point.x;
    uv.x = normal_id == 0 || normal_id == 2 || normal_id == 5 ? 1.0 - uv.x : uv.x;
    uv.y = normal_id == 2 || normal_id == 3 ? 1.0 - point.z : 1.0 - point.y;
    return uv;
}