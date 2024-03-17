shader_type spatial;
uniform float radius;
varying vec3 normal;

void vertex() {
	VERTEX = VERTEX + NORMAL * radius;
	normal = NORMAL;
}

void fragment() {
	ALBEDO = cross(vec3(1,1,1), normal);
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
