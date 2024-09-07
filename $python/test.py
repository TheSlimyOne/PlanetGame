import math
from PIL import Image
import numpy as np
# base = Image.open("6bdc82820e21da8018052aa4873f4a9cd25f1273.png")
base = Image.new('RGB', (64, 32))
img = Image.new('RGB', base.size)
# img_r = Image.new('RGB', base.size)
# img_g = Image.new('RGB', base.size)
# img_b = Image.new('RGB', base.size)
img_debug = Image.new('RGB', base.size)


def wrap(index, offset):
    return [int((index[0] + offset[0] + base.width) % base.width), index[1] + offset[1]]


def uv_to_point_on_sphere(uv):
    longitude = (uv[0] - 0.5) * 2 * math.pi
    latitude = (uv[1] - 0.5) * math.pi

    y = math.sin(latitude)
    r = math.cos(latitude)
    x = -math.sin(longitude) * r
    z = math.cos(longitude) * r
    return (x, y, z)


def calculate_world_point(index):
    uv = index[0] / (img.size[0] - 1), index[1] / (img.size[1] - 1)

    height = 1  # base.getpixel([int(p) for p in index])

    point = uv_to_point_on_sphere(uv)
    # printpoint)
    return [p * height * 0.05 for p in point]


def normalize(point):
    # print(point)
    length = math.sqrt(
        math.pow(point[0], 2) + math.pow(point[1], 2) + math.pow(point[2], 2))
    # if (length == 0):
    #     return False
    return [p/length for p in point]


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]]


for i in range(base.size[0]):
    for j in range(base.size[1]):
        index = [i, j]
        north = calculate_world_point(wrap(index, (0, 1)))
        south = calculate_world_point(wrap(index, (0, -1)))
        west = calculate_world_point(wrap(index, (-1, 0)))
        east = calculate_world_point(wrap(index, (1, 0)))

        dir_north = normalize([a - b for a, b in zip(north, south)])
        dir_east = normalize([a - b for a, b in zip(east, west)])

        # if (not dir_north or not dir_east):
        #     continue
        normal_vector = normalize(cross(dir_east, dir_north))
        # normal_vector = [int(256 * (p + 1.0)/2.0) for p in normal_vector]

        TBN = np.matrix(
            [dir_north,
            dir_east,
            normal_vector]
        )

        TBN_inv = np.linalg.inv(TBN)

        tangent_space_normal = np.dot(normal_vector, TBN_inv)

        x = tangent_space_normal[0, 0]
        y = tangent_space_normal[0, 1]
        z = tangent_space_normal[0, 2]


        tangent_space_normal = [int(256 * (p + 1) * 0.5) for p in [x,y,z]]
        
        # print(normal_vector)
        # print(tangent_space_normal)
        # print(TBN)
        # print("===========================")
        
        img_debug.putpixel(index, tuple(tangent_space_normal))
        
        # break
    # break

        # img.putpixel(index, (int(256 * tangent_space_normal[0]), int(256 * tangent_space_normal[1]), int(256 * tangent_space_normal[2])))
        # img_r.putpixel(index, (int(256 * normal_vector[0]), 0, 0))
        # img_g.putpixel(index, (0, int(256 * normal_vector[1]), 0))
        # img_b.putpixel(index, (0, 0, int(256 * normal_vector[2])))

        # uv = [int(256 * a) for a in normalize(normal_vector)]
        # normal_vector.append(int(1))
        # print(len(uv))
# img.save("$python\\output\\output.png")
# img_r.save("$python\\output\\output_r.png")
# img_g.save("$python\\output\\output_g.png")
# img_b.save("$python\\output\\output_b.png")
img_debug.save("$python\\output\\output_debug.png")
# img.getpixel((-1, -1))