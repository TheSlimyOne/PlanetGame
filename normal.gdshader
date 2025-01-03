shader_type spatial;
varying vec3 normal;
void vertex() {
	normal = NORMAL;
	// Called for every vertex the material is visible on.
}

void fragment() {
	ALBEDO = normal;
	// Called for every pixel the material is visible on.
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
