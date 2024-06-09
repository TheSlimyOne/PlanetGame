#[compute]
#version 450
layout (local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

layout (std430, binding = 0) buffer restrict AtomicCounterBuffer {
    uint primCount_full[16];
    uint primCount_culled[16];
    uint primCount_collision[16];
};

layout(set = 0, binding = 1, std430) buffer restrict IndicesBlock {
    uint read_index;
    uint write_index;
    uint delete_index;
    uint maximum_nodes;
};


layout (std430, binding = 2) buffer writeonly restrict DispatchOut {
    uint workgroup_size_x;
    uint workgroup_size_y;
    uint workgroup_size_z;
};

void main() {
    uint full_count = primCount_full[read_index];
    uint culled_count = primCount_culled[read_index];
    uint collision_count = primCount_collision[read_index];

    // Define workgroup size
    workgroup_size_x = uint(full_count / 32) + 1;   

    primCount_full[delete_index] = 0;
    primCount_culled[delete_index] = 0;
    primCount_collision[delete_index] = 0;
}