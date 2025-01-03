shader_type spatial;
varying vec2 uv;
void vertex() {
	uv = UV;
	// Called for every vertex the material is visible on.
}

void fragment() {
	ALBEDO = vec3(uv, 0);
	// Called for every pixel the material is visible on.
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
