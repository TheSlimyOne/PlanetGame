#[compute]
#version 450
layout (local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

layout (std430, binding = 0) buffer restrict AtomicCounterBuffer {
    uint primCount_full[3];
    uint primCount_culled[3];
};

layout(set = 0, binding = 1, std430) buffer restrict IndicesBlock {
    uint read_index;
    uint write_index;
    uint delete_index;
    uint maximum_nodes;
};

layout (set = 0, binding = 2, std430) buffer writeonly restrict DispatchOut {
    uint workgroup_size_x;
    uint workgroup_size_y;
    uint workgroup_size_z;
};

layout(set = 0, binding = 3, r8) restrict uniform image2D GlobalKeyData;

layout(set = 0, binding = 4, std430) buffer restrict CommandBuffer { int data[]; } command_buffer;

void main() {
    read_index = (read_index + 1) % 3;
    write_index = (write_index + 1) % 3;
    delete_index = (delete_index + 1) % 3;
    
    uint full_count = primCount_full[read_index];
    uint culled_count = primCount_culled[read_index];

    workgroup_size_x = uint(full_count / 64) + 1;

    primCount_full[delete_index] = 0;
    primCount_culled[delete_index] = 0;

    imageStore(GlobalKeyData, ivec2(0, 0), vec4(0, 0, 0, 0));

    command_buffer.data[1] = int(culled_count);
}