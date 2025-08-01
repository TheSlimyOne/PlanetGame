shader_type canvas_item;
uniform int total_mips;

uvec4 pack_rgba8(uint r, uint g, uint b, uint a){
    return floatBitsToUint(vec4(float(r),float(g),float(b),float(a)) / 255.0);
}

uvec4 unpack_rgba8(uvec4 rgba){
    return uvec4(uintBitsToFloat(rgba) * 255.0);
}

const vec3[6] A = {vec3(1,0,1), vec3(1,0,0), vec3(0,1,0), vec3(0,0,1), vec3(1,1,0), vec3(0,1,1)};
void fragment() {
	ivec2 tex_size = textureSize(TEXTURE, 0);
    ivec2 pixel_coords = ivec2(UV * vec2(tex_size));
	uvec4 tile_data = floatBitsToUint(texelFetch(TEXTURE, pixel_coords, 0));

	if (tile_data == uvec4(0))
		COLOR = vec4(1,1,1,1);
	else
	{
		uint packed_indirection_index = tile_data.x;
	    uint mip_index = tile_data.y;
		uint normal_id = tile_data.z;
//
	    //ivec3 indirection_index = ivec3(uvec3(
	        //(packed_indirection_index >> 24u) & 0xFFu,
	        //(packed_indirection_index >> 16u) & 0xFFu,
	        //(packed_indirection_index >> 8u) & 0xFFu
	    //));
//
		//int lod_size = int(pow(2.0, float(mip_index)));


		//int packed = int((mip_index << 4) | normal_id);
		//float b = float(packed) / 255.0;

		//float x = float(indirection_index.x) / float(16);
		//float y = float(indirection_index.y) / float(16);


		//COLOR = vec4(x, y, b, 1);
		COLOR = vec4(A[normal_id].xyz, 1);

	}
	//uint x_coord =

    // Optional: visualize or use tile_value
    //COLOR = vec4(vec3(tile_value) / 255.0, 1.0); // visualize as grayscale
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
