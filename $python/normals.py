import numpy as np;
import math


triangle = [np.array([12, 3, 6]), np.array([0, 3, 4]), np.array([4, 13, 1])]

A = (triangle[1] - triangle[0])
B = (triangle[2] - triangle[0])

normal = np.cross(A, B)

magnitude = normal[0] ** 2 + normal[1] ** 2 + normal[2] ** 2


print(normal," " , magnitude)

# v1 = [20/math.sqrt(16736),-44/math.sqrt(16736),-120/math.sqrt(16736)]
# v2 = [4/math.sqrt(2036),38/math.sqrt(2036),-24/math.sqrt(2036)]
# print(np.dot(v1, v2))