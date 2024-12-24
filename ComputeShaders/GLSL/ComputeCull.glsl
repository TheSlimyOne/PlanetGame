#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

#define sqrt2   1.414213562
#define PI      3.141592653

//Jad Khoury https://jadkhoury.github.io/files/MasterThesisFinal.pdf
layout(local_size_x = 32, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict AtomicCounterBuffer {
    uint primitive_count_full[3];
    uint primitive_count_culled[3];
};

layout(set = 0, binding = 1, std430) buffer restrict readonly IndicesBlock {
    uint read_index;
    uint write_index;
    uint delete_index;
    uint maximum_nodes;
};

layout(set = 0, binding = 2, std430) buffer restrict readonly ReadList {
    uvec4 read_list[];
};

layout(set = 0, binding = 3, r32f) restrict uniform image2D GlobalKeyData;
layout(set = 0, binding = 4, std430) buffer restrict writeonly WriteFullList {
    uvec4 write_full_list[];
};

layout(set = 0, binding = 5, std430) buffer restrict readonly BaseTriangles {
    vec4 base_triangles[];
};

layout(set = 0, binding = 6, std430) buffer restrict readonly external_data {
    mat4 view_projection_matrix;  
    mat4 planet_transform_matrix; 
    vec4 camera_position;         
    float fovy;                   
    float sub_factor;             
    float height_scale;           
    float max_lod;                
    float radius;                 

    float bias1;                  
    float bias2;                  
    float culling;
    float _padding;
};

layout(set = 0, binding = 7, std430) buffer restrict OutputBuffer { 
    float data[]; 
} output_buffer;


struct Triangle {
    vec3 v0; // (0, 0)
    vec3 v1; // (0, 1)
    vec3 v2; // (1, 0)

    vec3 origin; // (0.5, 0.5)

    vec3 xNeighbor; // (0.5, -0.5)
    vec3 yNeighbor; // (-0.5, 0.5)
};

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

uvec2 left_shift_64(uvec2 nodeID, uint shift) {
    uvec2 result;
    if (shift == 0) return nodeID;

    if (shift < 32) {
        result.x = (nodeID.x << shift) | (nodeID.y >> (32 - shift));
        result.y = nodeID.y << shift;
    } else {
        result.x = nodeID.y << (shift - 32);
        result.y = 0u;
    }
    return result;
}

uvec2 right_shift_64(uvec2 nodeID, uint shift) {
    uvec2 result;
    if (shift == 0) return nodeID;

    if (shift < 32) {
        result.y = (nodeID.y >> shift) | (nodeID.x << (32 - shift));
        result.x = nodeID.x >> shift;
    } else {
        result.y = nodeID.x >> (shift - 32);
        result.x = 0u;
    }

    return result;
}

vec3 apply_rotation(vec3 point){
    mat4 rotationOnly = mat4(
        vec4(normalize(planet_transform_matrix[0].xyz), 0),
        vec4(normalize(planet_transform_matrix[1].xyz), 0),
        vec4(normalize(planet_transform_matrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );

    return (vec4(point, 1) * rotationOnly).xyz;
}

/*
    msb - 2 => is the offset to ignore the leading 01
    level * 2 => level is used as an index. Used to tell how much to shift over to get the bits in question. 
    Multiplied by 2 so to shift over two each time.
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
    float u = (longitude / PI + 1) * 0.5;
    float v = latitude / PI + 0.5;
    return vec2(u, v);
}

vec2 get_translation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1); 
    translation.y = float(b1 ^ 0x1);
    return translation * 0.5;
}

int get_rotation(uint b1b2, uint b1, uint b2) {
    uint a = (b1b2 ^ 0x2);
    uint b = (a | 0x1);
    uint c = (b1 ^ b2);
    return int(b * c);
}

vec2 rotate(uint rotationIndex, vec2 translation) {
    vec2 r;
    ivec2 trig = quickPI_2(rotationIndex);
    r.x = trig.x * translation.x - trig.y * translation.y;
    r.y = trig.y * translation.x + trig.x * translation.y;
    return r;
}

mat3 leaf_space_to_quadtree_space(uvec2 key) { 
    int msb = find_msb_64(key);
    vec2 translation = vec2(0,0);
    vec2 temp;
    int theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = get_branching(key, i, msb);
        uint b1 = b1b2 >> 1;
        uint b2 = b1b2 & 1;
        temp = scale * get_translation(b1);

        translation += rotate(theta, temp);
        theta += get_rotation(b1b2, b1, b2);
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

mat3 quadtree_space_to_polygon_space(vec3 vertex_a, vec3 vertex_b, vec3 vertex_c) {
    return mat3(vertex_a, vertex_b, vertex_c);
}

vec3 polygon_space_to_world_space(vec3 point) {
    point = (vec4(point, 1) * planet_transform_matrix).xyz;
    return point;
}

vec2 get_quadtree_point(vec2 point, mat3 quadtree_space_matrix) {
    return (vec3(point, 1) * quadtree_space_matrix).xy;
}

vec3 get_polygon_space_point(vec2 point, mat3 polygon_space_matrix) {
    return polygon_space_matrix * vec3(point, 1 - point.x - point.y);
}

/*
    +-------+-------+
    | Key A | Key B |
    +-------+-------+
    |     0 |     1 |
    |     1 |     3 |
    |     2 |     0 |
    |     3 |     2 |
    +-------+-------+
*/
Triangle create_triangle(uvec4 key) {
    mat3 quadtree_space = leaf_space_to_quadtree_space(key.xy);

    vec2 point_a = get_quadtree_point(vec2(0.5, 0.5), quadtree_space);
    vec2 point_b = get_quadtree_point(vec2(0.5, -0.5), quadtree_space);
    vec2 point_c = get_quadtree_point(vec2(-0.5, 0.5), quadtree_space);

    vec2 point_v0 = get_quadtree_point(vec2(0, 0), quadtree_space);
    vec2 point_v1 = get_quadtree_point(vec2(0, 1), quadtree_space);
    vec2 point_v2 = get_quadtree_point(vec2(1, 0), quadtree_space);

    uint vertex_base_index = key.z * 5;
    uint vertex_key_a = key.w;
    uint vertex_key_b = ((key.w >> 1) ^ 1) + ((key.w & 1) << 1);

    vec3 base_triangle_a = (base_triangles[vertex_base_index + vertex_key_a + 1]).xyz;
    vec3 base_triangle_b = (base_triangles[vertex_base_index + vertex_key_b + 1]).xyz;
    vec3 base_triangle_c = (base_triangles[vertex_base_index]).xyz;

    mat3 polygon_space = quadtree_space_to_polygon_space(base_triangle_a, base_triangle_b, base_triangle_c);
    
    Triangle t;

    t.v0 = get_polygon_space_point(point_v0, polygon_space);
    t.v1 = get_polygon_space_point(point_v1, polygon_space);
    t.v2 = get_polygon_space_point(point_v2, polygon_space);
    
    t.origin = get_polygon_space_point(point_a, polygon_space);
    t.xNeighbor = get_polygon_space_point(point_b, polygon_space);
    t.yNeighbor = get_polygon_space_point(point_c, polygon_space);

    return t;
}

float calculate_distance(float lod) {
    return sub_factor * radius * pow(2, 0.5 - lod) / fovy;
}

float calculate_lod(float dist) {
    float num = dist * fovy;
    float dom = sqrt2 * sub_factor * radius;
    return clamp(-log2(num/dom), -1, 31);
}

float distance_from_cam(vec3 from) {
    return distance(polygon_space_to_world_space(from), camera_position.xyz);
}

float calculate_lod_to_cam(vec3 from) {
    return calculate_lod(distance_from_cam(point_on_cube_to_point_on_sphere(from)));
}

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

uint get_junction_flags(uvec4 key, Triangle parent_triangle, float lod) {
    uint b1b2 = key.y & 0x3;
    vec3 neighbour;

    if (key.xy == uvec2(0,1)) {
        return 4 << 29;
    }

    if ((b1b2 >> 1) == 0) { // Check parent's Y neighbour
        neighbour = parent_triangle.yNeighbor;
    }
    else if ((b1b2 >> 1) == 1) { // Check parent's X neighbour
        neighbour = parent_triangle.xNeighbor;
    }

    float neighbour_lod = calculate_lod_to_cam(neighbour);

    if (neighbour_lod < lod - 1) {
        return b1b2 << 29;
    }

    return 4 << 29;
   
}

bool point_in_frustum(vec3 point) { 
    vec4 clipSpacePoint = view_projection_matrix * vec4(point, 1);
    vec3 ndcPoint = clipSpacePoint.xyz / clipSpacePoint.w;

    return ndcPoint.x >= -1.0 && ndcPoint.x <= 1.0 &&
           ndcPoint.y >= -1.0 && ndcPoint.y <= 1.0 &&
           ndcPoint.z >= -1.0 && ndcPoint.z <= 1.0;
}

vec3 get_planet_origin() {
    return planet_transform_matrix[3].xyz;
}

bool InHorizon(vec3 point) {
    vec3 sphericalPoint = point_on_cube_to_point_on_sphere(point);
    float distanceFromPlanet = distance_from_cam(get_planet_origin());
    float distanceFromHorizon = sqrt(pow(distanceFromPlanet, 2) - pow(radius, 2));
    vec3 toPlanetFromCam = get_planet_origin() - camera_position.xyz;
    vec3 objectPointIgnoreHeight = polygon_space_to_world_space(sphericalPoint);

    vec3 toPointOnSphereFromCam = objectPointIgnoreHeight - camera_position.xyz;
    float angleOfHorizon = acos(distanceFromHorizon / distanceFromPlanet);
    float angleFromPointToCamToPlanet = acos(dot(normalize(toPointOnSphereFromCam), normalize(toPlanetFromCam)));
    
    // if within view
    // if within horizon's circle
    // if within horizon
    
    bool b0 = point_in_frustum(objectPointIgnoreHeight);
    bool b1 = (distanceFromHorizon + (radius/2.0) * bias1) >= length(toPointOnSphereFromCam);
    bool b2 = angleFromPointToCamToPlanet + bias2 <= angleOfHorizon;
    if ((b0 && b1 && b2) || (b0 && b1) || (b0 && !b2))
        return true;
    return false;
}

bool triangle_in_frustum(Triangle triangle) {
    return InHorizon(triangle.v0) || InHorizon(triangle.v1) || InHorizon(triangle.v2) || InHorizon(triangle.origin);
}

void set_multimesh_data(uint index, uvec4 key) {
    output_buffer.data[20 * index + 0] = 1.0;
    output_buffer.data[20 * index + 1] = 0.0;
    output_buffer.data[20 * index + 2] = 0.0;
    output_buffer.data[20 * index + 3] = 0.0;

    output_buffer.data[20 * index + 4] = 0.0;
    output_buffer.data[20 * index + 5] = 1.0;
    output_buffer.data[20 * index + 6] = 0.0;
    output_buffer.data[20 * index + 7] = 0.0;

    output_buffer.data[20 * index + 8] = 0.0;
    output_buffer.data[20 * index + 9] = 0.0;
    output_buffer.data[20 * index + 10] = 1.0;
    output_buffer.data[20 * index + 11] = 0.0;

    // Not currently using
    output_buffer.data[20 * index + 12] = 0.0;
    output_buffer.data[20 * index + 13] = 0.0;
    output_buffer.data[20 * index + 14] = 0.0;
    output_buffer.data[20 * index + 15] = 0.0;

    // Setting the key data
    output_buffer.data[20 * index + 16] = uintBitsToFloat(key.x);
    output_buffer.data[20 * index + 17] = uintBitsToFloat(key.y);
    output_buffer.data[20 * index + 18] = uintBitsToFloat(key.z);
    output_buffer.data[20 * index + 19] = uintBitsToFloat(key.w);
}

void cull_key(uvec4 key, Triangle triangle, float lod) {
    // bool isCulling = culling == 1;
    // if (isCulling && triangle_in_frustum(triangle)) {
    uint write_culled_index = atomicAdd(primitive_count_culled[write_index], 1);
    set_multimesh_data(write_culled_index, key);
    // } else if (!isCulling) {
    //     uint write_culled_index = atomicAdd(primitive_count_culled[write_index], 1);
    //     set_multimesh_data(write_culled_index, key);
    // }
    // imageAtomicMax(GlobalKeyData, ivec2(0, 0), culling);
    imageAtomicMax(GlobalKeyData, ivec2(0, 0), get_lod_of_key(key.xy));
}

void main() {
    uint leaf_count = uint(atomicExchange(primitive_count_full[read_index], primitive_count_full[read_index]));
    uint invocationID = gl_GlobalInvocationID.x;
    if (invocationID >= leaf_count)
        return;
    
    uvec4 key = read_list[invocationID];
    key.w &= 0xFu;

    uvec4 parent_key = get_parent_key(key);
    uvec4 grand_parent_key = get_parent_key(parent_key);

    Triangle triangle = create_triangle(key);
    Triangle parent_triangle = create_triangle(parent_key);
    Triangle grand_parent_triangle = create_triangle(grand_parent_key);

    float current_LOD = get_lod_of_key(key.xy);
    float parent_target_LOD = calculate_lod_to_cam(parent_triangle.origin);
    float target_LOD = calculate_lod_to_cam(triangle.origin);
  
    if (target_LOD > current_LOD && current_LOD < max_lod) { // subdivide
        uvec4 children_keys[4] = get_child_keys(key);
        for (int i = 0; i < 4; i++) {
            uint write_full_index = atomicAdd(primitive_count_full[write_index], 1);
            Triangle child_triangle = create_triangle(children_keys[i]);
            
            children_keys[i].w |= get_junction_flags(children_keys[i], triangle, current_LOD + 1);
            write_full_list[write_full_index] = children_keys[i];
            
            cull_key(children_keys[i], child_triangle, current_LOD + 1);
     

        }
    } else if (parent_target_LOD < current_LOD - 1 && current_LOD > 0) { // merging
        if (is_upper_left_child(key.xy)) {
            uint write_full_index = atomicAdd(primitive_count_full[write_index], 1);

            parent_key.w |= get_junction_flags(parent_key, grand_parent_triangle, current_LOD - 1);
            write_full_list[write_full_index] = parent_key;

            cull_key(parent_key, parent_triangle, current_LOD - 1);

        }
    } else {
        uint write_full_index = atomicAdd(primitive_count_full[write_index], 1);

        key.w |= get_junction_flags(key, parent_triangle, current_LOD);
        write_full_list[write_full_index] = key;
     
        cull_key(key, triangle, current_LOD);
 
    }
}