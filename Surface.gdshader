shader_type spatial;
uniform int radius;
uniform int resolution;
uniform vec3 normal;

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	
	float x2 = p.x * p.x;
	float y2 = p.y * p.y;
	float z2 = p.z * p.z;
	
	float x = p.x * sqrt(1.0 - (y2 + z2) / 2.0 + y2 * z2 / 3.0);
	float y = p.y * sqrt(1.0 - (z2 + x2) / 2.0 + z2 * x2 / 3.0);
	float z = p.z * sqrt(1.0 - (x2 + y2) / 2.0 + x2 * y2 / 3.0);

	return vec3(x, y, z);
}

void vertex() {
	
	float scaleFactor = pow(2.0, INSTANCE_CUSTOM.a);
	vec3 axisA = vec3(normal.y, normal.z, normal.x);
	vec3 axisB = cross(normal, axisA);
	vec3 axicC = normal * scaleFactor;
	axisA /= scaleFactor;
	axisB /= scaleFactor;

	vec3 point = NODE_POSITION_WORLD + axisA * (2.0 * UV.x - 1.0) + axisB * (2.0 * UV.y - 1.0);
	VERTEX = float(radius) * point_on_cube_to_point_on_sphere(point) - NODE_POSITION_WORLD;
	
	//VERTEX = point_on_cube_to_point_on_sphere(point);
	// Called for every vertex the material is visible on.
}

void fragment() {

}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
