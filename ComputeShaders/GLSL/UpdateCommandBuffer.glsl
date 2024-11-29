#[compute]
#version 450
#extension GL_NV_compute_shader_derivatives : enable
layout (local_size_x = 1, local_size_y = 1, local_size_z = 1) in;


layout(set = 0, binding = 0, std430) buffer restrict CommandBuffer { int data[]; } command_buffer;
layout (std430, binding = 1) buffer restrict AtomicCounterBuffer {
    uint primCount_full[3];
    uint primCount_culled[3];
};
layout(set = 0, binding = 2, std430) buffer restrict IndicesBlock {
    uint read_index;
    uint write_index;
    uint delete_index;
    uint maximum_nodes;
};

layout(set = 0, binding = 3, std430) buffer restrict Debug {
    bool culling;
};

void main() {

   
    uint culled_count = primCount_culled[write_index];
    command_buffer.data[1] = int(culled_count);

    // read_index = (read_index + 1) % 3;
    // write_index = (write_index + 1) % 3;
    // delete_index = (delete_index + 1) % 3;
}
