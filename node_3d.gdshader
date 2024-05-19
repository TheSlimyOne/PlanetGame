shader_type spatial;
varying flat int msb;

void vertex() {
	VERTEX = VERTEX * INSTANCE_CUSTOM.x;
	msb = int(INSTANCE_CUSTOM.y);
}

void fragment() {
	int a = (msb & 1) != 0 ? 1 : 0;
	int b = (msb & 2) != 0 ? 1 : 0;
	int c = (msb & 4) != 0 ? 1 : 0;

	ALBEDO = vec3(float(a), float(b), float(c));
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
