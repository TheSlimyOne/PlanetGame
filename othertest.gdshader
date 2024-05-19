shader_type spatial;

varying flat uvec4 key;
void vertex() {

	key = uvec4(INSTANCE_CUSTOM);
	// Called for every vertex the material is visible on.
}

void fragment() {
	ALBEDO = vec3(key.xwz);
	// Called for every pixel the material is visible on.
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
