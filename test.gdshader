shader_type spatial;


void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec2 cell = floor(UV * 4.0)/ 4.0;
	ALBEDO = vec3(cell, 0);
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
