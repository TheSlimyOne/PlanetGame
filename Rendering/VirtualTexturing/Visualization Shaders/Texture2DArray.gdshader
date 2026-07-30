shader_type canvas_item;
render_mode unshaded;
uniform ivec2 grid_size;
uniform sampler2DArray image : repeat_disable, source_color, filter_linear;


void vertex() {

}

void fragment() {
	vec2 grid_position = UV * vec2(grid_size);

	ivec2 cell_position = ivec2(floor(grid_position));
	vec2 tile_uv = fract(grid_position);

	int array_index = cell_position.y * grid_size.x + cell_position.x;

	vec3 texture_coordinate = vec3(tile_uv, float(array_index));
	
	vec4 color = textureLod(image, texture_coordinate, 0.0);
	
	if (color.w != 0.0)
		COLOR = textureLod(image, texture_coordinate, 0.0);
	else
		COLOR = vec4(0, 0, 0, 1);
	
		
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
