import numpy as np;
sf = 2
normal = [0, 1, 0]
axisA = [normal[1], normal[2], normal[0]]
axisB = list(np.cross(normal, axisA))
print(1/pow(2, sf))
print(axisA, axisB, normal)