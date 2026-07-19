#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) restrict uniform image2DArray indirectionTable;

layout(set = 0, binding = 1, rgba32f) restrict uniform image2D residencyTable;

#[include] res://ComputeShaders/GLSL/ShaderIncludes/VirtualTextureFunctions.inc.glsl

void main() {
    ivec2 texture_size = imageSize(residencyTable);
    ivec2 invocation_id = ivec2(gl_GlobalInvocationID.xy);
    if (invocation_id.x >= texture_size.x || invocation_id.y >= texture_size.y) return;

    uvec4 tile_data = floatBitsToUint(imageLoad(residencyTable, invocation_id));
    if (tile_data.w == 0) return;

    uint packed_indirection_index = tile_data.x;
    int lod_size = int(pow(2, tile_data.y));
    
    ivec3 indirection_index = ivec3(
        (packed_indirection_index >> 24) & 0xFF,
        (packed_indirection_index >> 16) & 0xFF,
        (packed_indirection_index >> 8) & 0xFF
    );

    uvec4 indirection_data = floatBitsToUint(imageLoad(indirectionTable, indirection_index));

    set_indirection_entry(indirection_index, lod_size, uvec4(indirection_data.xy, 255u, indirection_data.w));
}
