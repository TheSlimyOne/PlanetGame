shader_type spatial;
render_mode unshaded;
uniform float grid_size;
void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec2 cell = floor(UV * grid_size);
	float checker = mod(cell.x + cell.y, 2.0);
	vec3 color1 = vec3(1);
	vec3 color2 = vec3(0);
	ALBEDO = mix(color1, color2, checker);
	//ALBEDO = vec3(UV.x, UV.y, 0);
	//ALBEDO = vec3(UV.x, 0, 0);
	//ALBEDO = vec3(0, UV.y, 0);
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
