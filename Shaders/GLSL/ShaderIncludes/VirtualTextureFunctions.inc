void set_indirection_entry(writeonly image2DArray indirectionTable, ivec3 indirection_index, int mip_size, uvec4 data)
{
    for (int i = 0; i < mip_size; i++) {
        for (int j = 0; j < mip_size; j++) {
            imageStore(indirectionTable, indirection_index + ivec3(i, j, 0), uintBitsToFloat(data));
        }
    }
}