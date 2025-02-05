grid_size = 8
# chunk_pixel_size = 128
for index in range(64):
    slot = index % (grid_size * grid_size)
    x = slot % grid_size
    y = (slot // grid_size)
    # print((x, y), end=" ")
    print((x, y), end=" ")
    if (x == grid_size - 1):
        print("")
    # if (x == grid_size):