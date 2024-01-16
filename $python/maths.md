d_a = d_g + tan(alpha) * d_na

// Line function of on triangle a
p_a + u_a * d_a

// Line function of on triangle b
p_b + u_b * d_b

// Meeting of two points equation
p_a + u_a * d_a = p_b + u_b * d_b

// Separated into x,y and z components
px_a + u_a * dx_a = px_b + u_b * dx_b
py_a + u_a * dy_a = py_b + u_b * dy_b
pz_a + u_a * dz_a = pz_b + u_b * dz_b

// u_a represented as a function of u_b
  I)  u_a = (px_b - px_a + u_b * dx_b) / dx_a
 II)  u_a = (py_b - py_a + u_b * dy_b) / dy_a
III)  u_a = (pz_b - pz_a + u_b * dz_b) / dz_a

// Choose two of I), II) and III) and solve for u_a and u_b
(px_b - px_a + u_b * dx_b) / dx_a = (py_b - py_a + u_b * dy_b) / dy_a
(px_b - px_a + u_b * dx_b) / dx_a = (pz_b - pz_a + u_b * dz_b) / dz_a
(py_b - py_a + u_b * dy_b) / dy_a = (pz_b - pz_a + u_b * dz_b) / dz_a

(a - b + x * d) / e = (f - g + x * i) / j
(j(a-b) - e(f-g))/(ei - jd)

(dy_a ( px_b - px_a ) - dx_a ( py_b - py_a )) / (( dx_a * dy_b ) - ( dy_a * dx_b )) = u_b
(dz_a ( px_b - px_a ) - dx_a ( pz_b - pz_a )) / (( dx_a * dz_b ) - ( dz_a * dx_b )) = u_b
(dz_a ( py_b - py_a ) - dy_a ( pz_b - pz_a )) / (( dy_a * dz_b ) - ( dz_a * dy_b )) = u_b


p_a+x_a*d_g+f_a(x_a)*d_na = p_b+x_b*d_g+f_b(x_b)*d_nb = p_2


d_g = direction vector of the centroid line normalized from 
a->b if point a
b->a if point b

d_na = normal of triangle of centroid A

u_a = parameter
u_b = parameter

f_a(x_a) = tan(a)*x_a 

p_a = centroid A
p_b = centroid B

p_2 = desired point
