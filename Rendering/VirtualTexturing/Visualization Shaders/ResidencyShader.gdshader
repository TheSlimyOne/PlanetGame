shader_type canvas_item;
uniform int total_mips;

const vec3[6] A = {vec3(1,0,1), vec3(1,0,0), vec3(0,1,0), vec3(0,0,1), vec3(1,1,0), vec3(0,1,1)};
void fragment() {
	ivec2 tex_size = textureSize(TEXTURE, 0);
    ivec2 pixel_coords = ivec2(UV * vec2(tex_size));
	uvec4 tile_data = floatBitsToUint(texelFetch(TEXTURE, pixel_coords, 0));
	
	
	{
		uint packed_indirection_index = tile_data.x;
	    uint mip_index = tile_data.y;
		uint normal_id = tile_data.z;

	    ivec3 indirection_index = ivec3(uvec3(
	        (packed_indirection_index >> 24u) & 0xFFu,
	        (packed_indirection_index >> 16u) & 0xFFu,
	        (packed_indirection_index >> 8u) & 0xFFu
	    ));

		int lod_size = int(pow(2.0, float(mip_index)));
		int packed = int((mip_index << 4u) | normal_id);
		float b = float(packed) / 255.0;
		float lod_scale = pow(2, float(total_mips - 1 - int(mip_index)));

		float x = float(indirection_index.x) / lod_scale;
		float y = float(indirection_index.y) / lod_scale;

		COLOR = vec4(x, y, 0, 1);
	}
}
