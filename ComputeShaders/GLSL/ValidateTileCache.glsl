#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, r32ui) restrict uniform uimage2DArray indirectionTable;

layout(set = 0, binding = 1, rgba32f) restrict uniform image2D residencyTable;

layout (std430, binding = 2) buffer restrict virtual_texture_data  {
    uint grid_size;
    uint total_mips;
    uint tile_offset;
    uint total_texture_slots;
};

uvec4 pack_rgba8(uint r, uint g, uint b, uint a){
    return floatBitsToUint(vec4(r,g,b,a) / 255.0);
}

uvec4 unpack_rgba8(uvec4 rgba){
    return uvec4(uintBitsToFloat(rgba) * 255.0);
}

void main() {
    ivec2 texture_size = imageSize(residencyTable);
    ivec2 invocation_id = ivec2(gl_GlobalInvocationID.xy);
    if (invocation_id.x >= texture_size.x || invocation_id.y >= texture_size.y) return;

    uvec4 tile_data = floatBitsToUint(imageLoad(residencyTable, invocation_id));
    if (tile_data == vec4(0)) return;

    uint packed_indirection_index = tile_data.x;
    int lod_size = int(packed_indirection_index & 0xFF);
    ivec3 indirection_index = ivec3(
        (packed_indirection_index >> 24) & 0xFF,
        (packed_indirection_index >> 16) & 0xFF,
        (packed_indirection_index >> 8) & 0xFF
    );

    uvec4 indirection_data = unpack_rgba8(imageLoad(indirectionTable, indirection_index));
    uint indirection_slot = indirection_data.x;
    imageStore(indirectionTable, indirection_index, pack_rgba8(indirection_data.x, indirection_data.y, indirection_data.z, 1));
}
