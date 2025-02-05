#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) restrict uniform readonly image2D framebuffer;

layout(set = 0, binding = 1, r32ui) restrict uniform uimage2DArray indirectionTable;

layout(set = 0, binding = 2, r32ui) restrict uniform uimage2DArray indirectionStateTable;

layout(set = 0, binding = 3, rgba32f) restrict uniform image2D residencyTable;

layout (std430, binding = 4) buffer restrict indirectionTableData  {
    uint grid_size;
    uint total_mips;
    uint tile_offset;
};

layout (std430, binding = 5) buffer restrict TextureIDCounter {
    uint texture_id_counter;
};

layout (std430, binding = 6) buffer restrict TileCacheIndex {
    uint virtual_texture_index;
};

layout (std430, binding = 7) buffer restrict TileIDs  {
    uint tiles[];
};

uvec4 pack_rgba8(uint r, uint g, uint b, uint a){
    return floatBitsToUint(vec4(r,g,b,a) / 255.0);
}

uvec4 unpack_rgba8(uvec4 rgba){
    return uvec4(uintBitsToFloat(rgba) * 255.0);
}

void main() {
    const uint PROCESSING = 0xFFFFFFFF;
    const uint NOT_PROCESSING = 0x00000000;
    const uint UNMAPPED = 0x00000000;

    ivec2 texture_size = imageSize(framebuffer);
    
    ivec2 invocation_id = ivec2(gl_GlobalInvocationID.xy);
    
    if (invocation_id.x >= texture_size.x || invocation_id.y >= texture_size.y) return;
    
    vec4 color = imageLoad(framebuffer, invocation_id);
    
    if (color == vec4(1)) return;
    
    int packed = int(round(color.b * 255.0));
    uint mip_index = uint((packed >> 4) & 0xF);
    uint normal_id = uint(packed & 0xF);
    int lod_size = int(pow(2, mip_index));
    int lod_scale = int(pow(2, total_mips - 1 - mip_index));
    
    ivec3 indirection_index = ivec3(
        int(color.x * grid_size),
        int(color.y * grid_size),
        total_mips * normal_id + mip_index
    );

    uint results = imageAtomicCompSwap(indirectionStateTable, indirection_index, NOT_PROCESSING, PROCESSING);
    if (results == PROCESSING) return;
    
    uvec4 indirection_data = unpack_rgba8(imageLoad(indirectionTable, indirection_index));
    if (indirection_data.w == 0) {
        uint index = atomicAdd(texture_id_counter, 1);
        uint x_coord = uint(color.x * lod_scale) & 0xF;
        uint y_coord = uint(color.y * lod_scale) & 0xF;

        uint tile_index = atomicAdd(virtual_texture_index, 1);
        uint usable_slots = grid_size * grid_size - tile_offset;
        uint slot = (tile_offset + (tile_index % usable_slots)) & 0xFF;

        ivec2 slot_coords = ivec2(slot % grid_size, slot / grid_size);
        
        tiles[index] = x_coord | (y_coord << 4) | (mip_index << 8) | (normal_id << 12) | (slot << 16);

        uint packed_indirection_index = 
            ((indirection_index.x & 0xFF) << 24) |
            ((indirection_index.y & 0xFF) << 16) |
            ((indirection_index.z & 0xFF) << 8) |
            ((lod_size & 0xFF)); 

        uvec4 prev_tile_data = floatBitsToUint(imageLoad(residencyTable, slot_coords));
        if (prev_tile_data.y != tiles[index]) {
            uint packed_prev_indirection_index = prev_tile_data.x;
            
            int prev_lod_size = int(packed_prev_indirection_index & 0xFF);
            ivec3 prev_indirection_index = ivec3(
                (packed_prev_indirection_index >> 24) & 0xFF,
                (packed_prev_indirection_index >> 16) & 0xFF,
                (packed_prev_indirection_index >> 8) & 0xFF
            );
            
            for (int i = 0; i < prev_lod_size; i++) {
                for (int j = 0; j < prev_lod_size; j++) {
                    imageStore(indirectionTable, prev_indirection_index + ivec3(i, j, 0), pack_rgba8(0, 0, 0, 0));
                }
            }
        }

        imageStore(residencyTable, slot_coords, uintBitsToFloat(uvec4(packed_indirection_index, tiles[index], 1, 1)));

        for (int i = 0; i < lod_size; i++) {
            for (int j = 0; j < lod_size; j++) {
                imageStore(indirectionTable, indirection_index + ivec3(i, j, 0), pack_rgba8(slot, 0, 0, 1));
            }
        }
    }
    else {
        uint slot = indirection_data.x;
        ivec2 slot_coords = ivec2(slot % grid_size, slot / grid_size);
        vec4 tile_data = imageLoad(residencyTable, slot_coords);
        imageStore(residencyTable, slot_coords, vec4(tile_data.xy, 1, tile_data.w));
    }
}
