shader_type canvas_item;
render_mode unshaded;
uniform int grid_size;
uniform int index;
uniform sampler2DArray table : repeat_disable, source_color, filter_nearest;


void fragment() {
	//vec4 table_value = texture(table, vec3(UV, float(index)), 0);
//
	ivec2 tex_size = textureSize(table, 0).xy;
    ivec2 pixel_coords = ivec2(UV * vec2(tex_size));
	uvec4 indirection_data = floatBitsToUint(texelFetch(table, ivec3(pixel_coords, index), 0));
	float slot = float(indirection_data.x) / 255.0;
	COLOR = vec4(slot, 0, 0, 1);

	//
	////vec4 table_value = texture(table, vec3(UV, float(index)), 0);
////
	////COLOR = vec4(table_value.xyz, 1);
//
	//if (indirection_data == uvec4(0))
		//COLOR = vec4(0, 0, 0, 1);
	//else
	//{
		//uint slot = indirection_data.x;
		//ivec2 slot_coords = ivec2(int(slot) % grid_size, int(slot) / grid_size);
	  	//vec2 normalized_slot_coords = vec2(slot_coords) / float(grid_size - 1);
		//COLOR = vec4(normalized_slot_coords.xy, 0, 1);
	//}
}
