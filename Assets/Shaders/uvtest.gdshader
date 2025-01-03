shader_type spatial;
uniform float A;
vec3 morph_vertex(vec2 vertex, float morph_k) {
	float patch_tess_factor = 4.0;
	vec2 fract_part = fract(vertex * patch_tess_factor * 0.5f);
	fract_part *= 2.0 / patch_tess_factor;

	//return vec3(vertex - (fract_part) * morph_k, 1);
	vec2 int_part = floor(vertex.xy * patch_tess_factor * 0.5);
	vec2 sign_vec = mod(int_part, 2.0) * vec2(-2.0) + vec2(1.0);
	return vec3(vertex - (sign_vec * fract_part) * morph_k, 1);
}

void vertex() {
	VERTEX = morph_vertex(UV, A);
	// Called for every vertex the material is visible on.
}



void fragment() {
	ALBEDO = vec3(UV, 0);
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
