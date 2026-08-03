z = 5

grid_size = 2 ** (5+2-1)

low_resolution_mip_count = 5
high_resolution_mip_count = 1

total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count

for z in range(5, -1, -1):
    mip_index = z % total_resolution_mip_count

    lod_size = 1 << mip_index

    mip_grid_size = grid_size >> mip_index

    mip_step = grid_size / mip_grid_size

    print(mip_step)
