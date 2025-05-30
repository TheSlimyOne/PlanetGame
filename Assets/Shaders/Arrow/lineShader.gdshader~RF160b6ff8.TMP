shader_type spatial;
render_mode unshaded;

void vertex() {
	if (VERTEX == vec3(0, 1, 0))
	{
		VERTEX = INSTANCE_CUSTOM.xyz;
	}
}

void fragment() {
	ALBEDO = COLOR.xyz;
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
