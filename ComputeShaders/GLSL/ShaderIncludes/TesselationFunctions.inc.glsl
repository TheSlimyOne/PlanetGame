// ---------- Common constants ----------
const float PI   = 3.141592653;
const float SQRT2 = 1.414213562;
const float EPS  = 1e-7;
// --------------------

// ---------- Common structs ----------
struct Triangle {
    vec3 origin; // (0.3, 0.3)
    vec3 xNeighbor; // (0.5, -0.5)
    vec3 yNeighbor; // (-0.5, 0.5)

    vec3 v0;
    vec3 v1;
    vec3 v2;
};
// --------------------

vec3 face_normal(uint normal_id) {
    if (normal_id == 0u) return vec3( 1, 0, 0);
    if (normal_id == 1u) return vec3(-1, 0, 0);
    if (normal_id == 2u) return vec3( 0, 1, 0);
    if (normal_id == 3u) return vec3( 0,-1, 0);
    if (normal_id == 4u) return vec3( 0, 0, 1);
    return vec3( 0, 0,-1);
}

vec2 get_cube_uv(uint normal_id, vec3 point) {
	vec2 uv = vec2(0);
	point = (point + 1.0) / 2.0;
	uv.x = normal_id == 0 || normal_id == 1 ? point.z : point.x;
	uv.x = normal_id == 0 || normal_id == 2 || normal_id == 5 ? 1.0 - uv.x : uv.x;
	uv.y = normal_id == 2 || normal_id == 3 ? 1.0 - point.z : 1.0 - point.y;

	return clamp(uv, vec2(0.0), vec2(1.0 - EPS));
}

/*
    +--+-------------+------------+------------+---------+--------+-----------------------+---------------------+
    |b |b >> 1 => b1 |b & 1 => b2 |b2^1 => bn2 |b1 & bn2 |b1 & b2 |bn2 - (2 * (b1 & bn2)) |b2 - (2 * (b1 & b2)) |
    +--+-------------+------------+------------+---------+--------+-----------------------+---------------------+
    |0 |0            |0           |1           |0        |0       |1                      |0                    |
    |1 |0            |1           |0           |0        |0       |0                      |1                    |
    |2 |1            |0           |1           |1        |0       |-1                     |0                    |
    |3 |1            |1           |0           |0        |1       |0                      |-1                   |
    +--+-------------+------------+------------+---------+--------+-----------------------+---------------------+
*/
ivec2 quickPI_2(uint a) {
    int b = int(a & 3);
    int b1 = b >> 1;
    int b2 = b & 1;
    int bn2 = b2 ^ 1;
    int c = bn2 - (2 * (b1 & bn2));
    int s = b2 - (2 * (b1 & b2));
    return ivec2(c, s);
}

int find_msb_64(uvec2 key) {
    return (key.x == 0) ? findMSB(key.y) : (findMSB(key.x) + 32);
}

uvec2 left_shift_64(uvec2 node_id, uint shift) {
    uvec2 result;
    if (shift == 0) return node_id;

    if (shift < 32) {
        result.x = (node_id.x << shift) | (node_id.y >> (32 - shift));
        result.y = node_id.y << shift;
    } else {
        result.x = node_id.y << (shift - 32);
        result.y = 0u;
    }
    return result;
}

uvec2 right_shift_64(uvec2 node_id, uint shift) {
    uvec2 result;
    if (shift == 0) return node_id;

    if (shift < 32) {
        result.y = (node_id.y >> shift) | (node_id.x << (32 - shift));
        result.x = node_id.x >> shift;
    } else {
        result.y = node_id.x >> (shift - 32);
        result.x = 0u;
    }

    return result;
}

/*
    msb - 2 => is the offset to ignore the leading 01
    level * 2 => level is used as an index. Used to tell how much to shift over to get the bits in question.
    Multiplied by 2 so to shift over two each time.
    Returns a 2 digit binary number
*/
uint get_branching(uvec2 key, int level, int msb) {
    return (right_shift_64(key, (msb - 2) - (level * 2)).y & 0x3);
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
    vec3 square = p * p;
    return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

vec2 point_on_sphere_to_uv(vec3 p) {
    p = normalize(p);
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / PI + 1.0) * 0.5;
    float v = latitude / PI + 0.5;
    return vec2(u, v);
}

vec2 get_translation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1);
    translation.y = float(b1 ^ 0x1);
    return translation;
}

int get_rotation(uint b1b2) {
    uint b1 = b1b2 >> 1;
    uint b2 = b1b2 & 1;

    uint a = (b1b2 ^ 0x2);
    uint b = (a | 0x1);
    uint c = (b1 ^ b2);
    return int(b * c);
}

vec2 rotate(uint rotation_index, vec2 translation) {
    vec2 r;
    ivec2 trig = quickPI_2(rotation_index);
    r.x = trig.x * translation.x - trig.y * translation.y;
    r.y = trig.y * translation.x + trig.x * translation.y;
    return r;
}

/*
    +--+-------------+------------+-------------+-------------+----------+---------+
    |b |b >> 1 => b1 |b & 1 => b2 |2 * b2 => l0 |2 * b1 => r0 |-r0 => l1 |l0 => r1 |
    +--+-------------+------------+-------------+-------------+----------+---------+
    |0 |0            |0           |-1           |1            |-1        |-1       |
    |1 |0            |1           |1            |1            |-1        |1        |
    |2 |1            |0           |-1           |-1           |1         |-1       |
    |3 |1            |1           |1            |-1           |1         |1        |
    +--+-------------+------------+-------------+-------------+----------+---------+
*/

vec3[3] get_base_primitive(uint mesh_polygon_id, uint root_id) {
    vec3 normal = face_normal(mesh_polygon_id);
    vec3 axis_a = normal.yzx;
    vec3 axis_b = cross(normal, axis_a);

    uint b1b2 = root_id;
    int b1 = int(b1b2 >> 1);
    int b2 = int(b1b2 & 1);

    int l0 = 2 * b2 - 1;
    int r0 = 2 * b1 - 1;
    int l1 = -r0;
    int r1 = l0;

    vec3[] base_primitive = {
            (l0 * axis_a) + (l1 * axis_b) + normal,
            (r0 * axis_a) + (r1 * axis_b) + normal,
            normal
        };

    return base_primitive;
}


// ---------- Transform Helpers ----------
mat3 leaf_space_to_quadtree_space(uvec2 key) {
    int msb = find_msb_64(key);
    vec2 translation = vec2(0, 0);
    vec2 temp;
    int theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = get_branching(key, i, msb);
        uint b1 = b1b2 >> 1;

        temp = scale * get_translation(b1) * 0.5;

        translation += rotate(theta, temp);
        theta += get_rotation(b1b2);
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

mat3 quadtree_space_to_polygon_space(uint mesh_polygon_id, uint root_id) {
    vec3[3] base_primitive = get_base_primitive(mesh_polygon_id, root_id);

    return mat3(
        base_primitive[0] - base_primitive[2],
        base_primitive[1] - base_primitive[2],
        base_primitive[2]
    );
}

vec3 polygon_space_to_world_space(vec3 point, mat4 planet_transform_matrix) {
    return (vec4(point, 1) * planet_transform_matrix).xyz;
} 

vec2 get_quadtree_point(vec2 point, mat3 quadtree_space_matrix) {
    return (vec3(point, 1) * quadtree_space_matrix).xy;
}

vec3 get_polygon_space_point(vec2 point, mat3 polygon_space_matrix) {
    return (polygon_space_matrix * vec3(point, 1)).xyz;
}

Triangle create_triangle(uvec4 key) {
    mat3 quadtree_space = leaf_space_to_quadtree_space(key.xy);

    vec2 point_a = get_quadtree_point(vec2(0.5, 0.5), quadtree_space);
    vec2 point_b = get_quadtree_point(vec2(0.5, -0.5), quadtree_space);
    vec2 point_c = get_quadtree_point(vec2(-0.5, 0.5), quadtree_space);

    vec2 point_d = get_quadtree_point(vec2(0, 0), quadtree_space);
    vec2 point_e = get_quadtree_point(vec2(1, 0), quadtree_space);
    vec2 point_f = get_quadtree_point(vec2(0, 1), quadtree_space);

    mat3 polygon_space = quadtree_space_to_polygon_space(key.z, key.w);

    Triangle t;

    t.origin = get_polygon_space_point(point_a, polygon_space);
    t.xNeighbor = get_polygon_space_point(point_b, polygon_space);
    t.yNeighbor = get_polygon_space_point(point_c, polygon_space);

    t.v0 = get_polygon_space_point(point_d, polygon_space);
    t.v1 = get_polygon_space_point(point_e, polygon_space);
    t.v2 = get_polygon_space_point(point_f, polygon_space);

    return t;
}

float distance_from_cam(vec3 from, mat4 planet_transform_matrix) {
    return distance(polygon_space_to_world_space(from, planet_transform_matrix), camera_position.xyz);
}

// ---------- Calculate Lod Functions ----------
float calculate_lod(float dist, float sub_factor, float radius, float fovy, int minimum_lod, int maximum_lod) {
    float num = SQRT2 * sub_factor * radius;
    float dom = dist * fovy;
    return clamp(log2(num / dom), minimum_lod, maximum_lod);
}

float calculate_lod_to_cam(vec3 from, mat4 planet_transform_matrix, float sub_factor, float radius, float fovy, int minimum_lod, int maximum_lod) {
    float dist = distance_from_cam(point_on_cube_to_point_on_sphere(from), planet_transform_matrix);
    return calculate_lod(dist, sub_factor, radius, fovy, minimum_lod, maximum_lod);
}

// --------------------


int get_lod_of_key(uvec2 key) {
    return find_msb_64(key) / 2;
}

uvec4 get_parent_key(uvec4 key) {
    return uvec4(right_shift_64(key.xy, 2u), key.zw);
}

uvec4[4] get_child_keys(uvec4 key) {
    uvec2 base_key = left_shift_64(key.xy, 2u);
    uvec4[] keys = {
            uvec4(base_key.x, base_key.y | 0, key.zw),
            uvec4(base_key.x, base_key.y | 1, key.zw),
            uvec4(base_key.x, base_key.y | 2, key.zw),
            uvec4(base_key.x, base_key.y | 3, key.zw)
        };
    return keys;
}

bool is_upper_left_child(uvec2 key) {
    return (3 & key[1]) == 0;
}

bool point_in_frustum(vec3 point, mat4 view_projection_matrix, float bias) {
    float margin = bias;

    vec4 clip = view_projection_matrix * vec4(point, 1.0);
    float w = clip.w;

    return
        clip.x >= -w - margin && clip.x <= w + margin &&
        clip.y >= -w - margin && clip.y <= w + margin &&
        clip.z >= -w && clip.z <= w;
}

mat4 get_rotation_from_matrix(mat4 transform_matrix) {
    return mat4(
        vec4(transform_matrix[0].xyz / length(transform_matrix[0].xyz), 0),
        vec4(transform_matrix[1].xyz / length(transform_matrix[1].xyz), 0),
        vec4(transform_matrix[2].xyz / length(transform_matrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );
}