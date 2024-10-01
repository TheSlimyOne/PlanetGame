shader_type spatial;
uniform sampler2D image : repeat_disable;
void vertex() {
	// Called for every vertex the material is visible on.
}


void fragment() {
	ALBEDO = vec3(UV, 0.0);
	ALBEDO = vec3(UV.x, 0.0, 0.0);
	ALBEDO = vec3(UV, 0.0);
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
