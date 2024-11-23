shader_type spatial;
uniform sampler2D albedo_map : source_color;
uniform sampler2DArray yt;
void vertex() {
	// Called for every vertex the material is visible on.
	COLOR = vec4(0);
}

void fragment() {
	ALBEDO = textureLod(albedo_map, UV, 0.0).xyz;

}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
