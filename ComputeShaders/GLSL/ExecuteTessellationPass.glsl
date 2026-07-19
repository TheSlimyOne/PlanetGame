#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

//Jad Khoury https://jadkhoury.github.io/files/MasterThesisFinal.pdf
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict AtomicCounterBuffer {
    uint primitive_count_full[3];
    uint primitive_count_culled[3];
    uint primitive_count_rendered[3];
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

layout(set = 0, binding = 3, std430) buffer restrict writeonly WriteFullList {
    uvec4 write_full_list[];
};

layout(set = 0, binding = 4, std430) buffer restrict writeonly WriteCullList {
    uvec4 write_cull_list[];
};

layout(set = 0, binding = 5, std430) buffer restrict readonly external_data {
    mat4 view_projection_matrix;
    mat4 planet_transform_matrix;
    vec4 camera_position;
    float fovy;
    float sub_factor;
    float height_scale;
    float radius;
    float bias1;
    float bias2;
    float culling;

    int maximum_lod;
    int minimum_lod;
};

#[include] res://ComputeShaders/GLSL/ShaderIncludes/TesselationFunctions.inc.glsl

layout(set = 0, binding = 6, std430) buffer restrict OutputBuffer {
    float data[];
} output_buffer;

layout(set = 0, binding = 7, r32f) restrict uniform image2D GlobalKeyData;

// vec3 apply_height_to_point(vec3 point, uint normal_id)
// {
//     vec3 rotated_point = (vec4(point, 1) * get_rotation_from_matrix(planet_transform_matrix)).xyz;
//     vec3 normal = rotated_point;

//     vec3 world_space_point = polygon_space_to_world_space(point_on_cube_to_point_on_sphere(point));
    
//     vec2 cube_uv = get_cube_uv(normal_id, point);
//     // float height = texture(height_maps, vec3(cube_uv, float(normal_id))).r;
//     return world_space_point;// + normal * height * height_scale; 
// }

// float calculate_lod_to_cam_include_height(vec3 from, uint normal_id) {
//     return calculate_lod(distance(apply_height_to_point(from, normal_id), camera_position.xyz));
// }


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

bool triangle_in_horizon(vec3 point) {
    vec3 spherical_point = point_on_cube_to_point_on_sphere(point);
    float distance_from_planet = distance_from_cam(planet_transform_matrix[3].xyz, planet_transform_matrix);
    float distance_from_horizon = sqrt(pow(distance_from_planet, 2) - pow(radius, 2));
    vec3 to_planet_from_cam = planet_transform_matrix[3].xyz - camera_position.xyz;
    vec3 object_point_ignore_height = polygon_space_to_world_space(spherical_point, planet_transform_matrix);

    vec3 to_point_on_sphere_from_cam = object_point_ignore_height - camera_position.xyz;
    float angle_of_horizon = acos(distance_from_horizon / distance_from_planet);
    float angle_from_point_to_cam_to_planet = acos(dot(normalize(to_point_on_sphere_from_cam), normalize(to_planet_from_cam)));

    bool b0 = point_in_frustum(object_point_ignore_height, view_projection_matrix, bias1); // if within view TODO look at bias
    bool b1 = (distance_from_horizon + (radius / 2.0)) >= length(to_point_on_sphere_from_cam); // if within horizon's circle
    bool b2 = angle_from_point_to_cam_to_planet <= angle_of_horizon;  // if within horizon
    if ((b0 && b1 && b2) || (b0 && b1) || (b0 && !b2))
        return true;
    return false;
}

bool triangle_in_frustum(Triangle triangle) {
    return triangle_in_horizon(triangle.v0) || triangle_in_horizon(triangle.v1) || triangle_in_horizon(triangle.v2) || triangle_in_horizon(triangle.origin);
}

void cull_key(uvec4 key, Triangle triangle, float lod) {
    bool isCulling = culling == 1;

    if ((isCulling && triangle_in_frustum(triangle)) ||!isCulling) {
        uint write_culled_index = atomicAdd(primitive_count_culled[write_index], 1);
        write_cull_list[write_culled_index] = key;

        uint render_index = atomicAdd(primitive_count_rendered[write_index], 1);
        set_multimesh_data(render_index, key);
    }

    imageAtomicMax(GlobalKeyData, ivec2(0, 0), get_lod_of_key(key.xy));
    imageAtomicMin(GlobalKeyData, ivec2(1, 0), get_lod_of_key(key.xy));
}

uint get_junction_flags(uvec4 key, Triangle parent_triangle, float lod) {
    uint b1b2 = key.y & 0x3;
    vec3 neighbour;

    if (key.xy == uvec2(0, 1)) {
        return 4 << 29;
    }

    if ((b1b2 >> 1) == 0) { // Check parent's Y neighbour
        neighbour = parent_triangle.yNeighbor;
    }
    else if ((b1b2 >> 1) == 1) { // Check parent's X neighbour
        neighbour = parent_triangle.xNeighbor;
    }

    float neighbour_lod = calculate_lod_to_cam(neighbour, planet_transform_matrix, sub_factor, radius, fovy, minimum_lod, maximum_lod);

    if (neighbour_lod < lod - 1) {
        return b1b2 << 29;
    }

    return 4 << 29;
}

void process_triangle(Triangle source_triangle, uvec4 source_key, Triangle parent_triangle, float lod) {
    uint write_full_index = atomicAdd(primitive_count_full[write_index], 1); 
    
    source_key.w |= get_junction_flags(source_key, parent_triangle, lod); 
    write_full_list[write_full_index] = source_key;
    cull_key(source_key, source_triangle, lod); 
}

void main() {
    uint invocationID = gl_GlobalInvocationID.x;
    if (invocationID >= primitive_count_full[read_index])
        return;

    uvec4 key = read_list[invocationID];
    key.w &= 0xFu;

    uvec4 parent_key = get_parent_key(key);
    uvec4 grand_parent_key = get_parent_key(parent_key);

    Triangle triangle = create_triangle(key);
    Triangle parent_triangle = create_triangle(parent_key);
    Triangle grand_parent_triangle = create_triangle(grand_parent_key);

    float current_LOD = get_lod_of_key(key.xy);
    float parent_target_LOD = calculate_lod_to_cam(parent_triangle.origin, planet_transform_matrix, sub_factor, radius, fovy, minimum_lod, maximum_lod);
    float target_LOD = calculate_lod_to_cam(triangle.origin, planet_transform_matrix, sub_factor, radius, fovy, minimum_lod, maximum_lod);

   if (target_LOD > current_LOD && current_LOD < maximum_lod) { // Subdivide 
        uvec4 children_keys[4] = get_child_keys(key); 
        for (int i = 0; i < 4; i++) { 
            Triangle child_triangle = create_triangle(children_keys[i]); 
            process_triangle(child_triangle, children_keys[i], triangle, current_LOD + 1);
        } 
    } else if (parent_target_LOD < current_LOD - 1 && current_LOD > minimum_lod) { // Merge
        if (is_upper_left_child(key.xy)) 
            process_triangle(parent_triangle, parent_key, grand_parent_triangle, current_LOD - 1);
    } 
    else
        process_triangle(triangle, key, parent_triangle, current_LOD);
    
}
