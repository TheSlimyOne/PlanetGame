shader_type canvas_item;
render_mode unshaded;

uniform ivec2 grid_size;
uniform sampler2DArray image : repeat_disable, filter_nearest;

void fragment() {
	vec2 grid_position = UV * vec2(grid_size);
	ivec2 tile_position = ivec2(floor(grid_position));

	ivec3 image_size = textureSize(image, 0);

	vec2 tile_uv = fract(grid_position);
	
	ivec2 pixel_coordinates = ivec2(tile_uv * vec2(image_size.xy));

	ivec3 texture_index = ivec3(pixel_coordinates, tile_position.y * grid_size.x + tile_position.x);

	vec4 raw_value = texelFetch(image, texture_index, 0);

	uvec4 indirection_data = floatBitsToUint(raw_value);

	float slot = float(indirection_data.x) / 255.0;
	
	COLOR = vec4(slot, 0.0, 0.0, 1.0);
}