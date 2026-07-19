#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) restrict uniform readonly image2D framebuffer;

layout(set = 0, binding = 1, rgba32f) restrict uniform image2DArray indirectionTable;

layout(set = 0, binding = 2, r32ui) restrict uniform uimage2DArray stateTable;

layout(set = 0, binding = 3, rgba32f) restrict uniform image2D residencyTable;

layout (std430, binding = 4) buffer restrict virtual_texture_data  {
    uint grid_size;
    uint total_mips;
    uint tile_offset;
    uint total_texture_slots;
};

layout (std430, binding = 5) buffer restrict TextureIDCounter {
    uint requested_tile_id_counter;
};

layout (std430, binding = 6) buffer restrict TileCacheCounter {
    uint virtual_texture_index;
};

layout (std430, binding = 7) buffer restrict TileIDs  {
    uint requested_tile_ids[];
};

#[include] res://ComputeShaders/GLSL/ShaderIncludes/VirtualTextureFunctions.inc.glsl

void main() {
    const uint PROCESSING = 0xFFFFFFFF;
    const uint NOT_PROCESSING = 0x00000000;
    // const uint UNMAPPED = 0x00000000;

    ivec2 texture_size = imageSize(framebuffer);
    ivec2 invocation_id = ivec2(gl_GlobalInvocationID.xy);
    
    if (invocation_id.x >= texture_size.x || invocation_id.y >= texture_size.y) return;
    
    vec3 color = imageLoad(framebuffer, invocation_id).rgb;

    if (color == vec3(1) || color == vec3(0)) return;

    int packed_id = int(round(color.b));

    uint mip_index = uint((packed_id >> 4) & 0xF);
    uint normal_id = uint(packed_id & 0xF);
    int lod_size = int(pow(2, mip_index));
    int lod_scale = int(pow(2, total_mips - 1 - mip_index));
    
    ivec3 indirection_index = ivec3(
        int(color.r * grid_size),
        int(color.g * grid_size),
        total_mips * normal_id + mip_index
    );

    uint results = imageAtomicCompSwap(stateTable, indirection_index, NOT_PROCESSING, PROCESSING);
    if (results == PROCESSING) return;
    
    uvec4 indirection_data = floatBitsToUint(imageLoad(indirectionTable, indirection_index));
    if (indirection_data.w == 0) {
        uint index = atomicAdd(requested_tile_id_counter, 1);
        uint x_coord = uint(color.r * lod_scale) & 0xF;
        uint y_coord = uint(color.g * lod_scale) & 0xF;

        uint tile_index = atomicAdd(virtual_texture_index, 1);
        uint usable_slots = total_texture_slots - tile_offset;
        uint slot = (tile_offset + (tile_index % usable_slots)) & 0xFF;

        ivec2 slot_coords = ivec2(slot % grid_size, slot / grid_size);

        requested_tile_ids[index] = x_coord | (y_coord << 4) | (mip_index << 8) | (normal_id << 12) | (slot << 16);

        uint packed_id_indirection_index = 
            ((indirection_index.x & 0xFF) << 24) |
            ((indirection_index.y & 0xFF) << 16) |
            ((indirection_index.z & 0xFF) << 8);

        uvec4 prev_tile_data = floatBitsToUint(imageLoad(residencyTable, slot_coords));

        // Only invalidate if we actually had a previous tile
        if (prev_tile_data.w != 0u) {
            uint prev_packed_id_indirection_index = prev_tile_data.x;
            uint prev_mip_index = prev_tile_data.y;

            int prev_lod_size = int(pow(2, prev_mip_index));
            ivec3 prev_indirection_index = ivec3(
                (prev_packed_id_indirection_index >> 24) & 0xFF,
                (prev_packed_id_indirection_index >> 16) & 0xFF,
                (prev_packed_id_indirection_index >> 8)  & 0xFF
            );

            set_indirection_entry(prev_indirection_index, prev_lod_size, uvec4(0u));
        }

        imageStore(residencyTable, slot_coords, uintBitsToFloat(uvec4(packed_id_indirection_index, mip_index, normal_id, 255u)));

        set_indirection_entry(indirection_index, lod_size, uvec4(slot, 0u, 0u, 255u));
    }
    // else {
    //     uint slot = indirection_data.x;
    //     ivec2 slot_coords = ivec2(slot % grid_size, slot / grid_size);
    //     vec4 tile_data = imageLoad(residencyTable, slot_coords);
    //     imageStore(residencyTable, slot_coords, vec4(tile_data.xy, 1, tile_data.w));
    // }
}
