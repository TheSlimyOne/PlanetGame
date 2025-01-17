#[compute]
#version 450
layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) restrict uniform readonly image2D framebuffer;

layout (std430, binding = 1) buffer restrict AtomicCounterBuffer {
    int atomic_index;
};

layout (std430, binding = 2) buffer restrict TextureIDs  {
    vec4 tiles[];
};

void main() {
    ivec2 texture_size = imageSize(framebuffer);
    ivec2 invocation_id = ivec2(gl_GlobalInvocationID.xy);
    if (invocation_id.x > texture_size.x || invocation_id.y > texture_size.y) return;
    vec4 color = imageLoad(framebuffer, invocation_id);
    if (color.a == 0) return;
    int index = atomicAdd(atomic_index, 1);
    
    tiles[index] = color;
}