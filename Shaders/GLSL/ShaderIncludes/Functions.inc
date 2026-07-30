vec2 get_cell_uv(vec2 global_uv, float grid_size) {
	vec2 grid_position = global_uv * vec2(grid_size);
    vec2 cell_uv = fract(grid_position);
	return cell_uv;
}

vec3 get_tile_cache_uv(
	sampler2DArray indirection_table,
	int mip_index,
	int tile_size,
	vec2 uv,
	int total_texture_subdivisions,
	int normal_id
) {
	int maximum_grid_size = 1 << (total_texture_subdivisions - 1);
	int lod_grid_size = maximum_grid_size >> mip_index;

	vec2 grid_position = uv * float(lod_grid_size);
	vec2 cell_uv = fract(grid_position);

	ivec2 tile_position = ivec2(floor(grid_position));
	ivec2 indirection_texture_size = textureSize(indirection_table, 0).xy;

	ivec2 indirection_coord = tile_position * indirection_texture_size / lod_grid_size;


	uvec4 indirection_data = floatBitsToUint(
		texelFetch(
			indirection_table,
			ivec3(indirection_coord, 
			normal_id * total_texture_subdivisions + mip_index),
			0
		)
	);

	uint tile_index = indirection_data.r;
	uint valid_flag = indirection_data.b;

	if (valid_flag == 255u) {
		return vec3(cell_uv, float(tile_index));
	} else {
		// use default tile
		return vec3(uv, float(normal_id));
	}

}

vec4 get_tile(sampler2DArray tile_cache, vec3 tile_uv) {
    //return texture2D_linear(tile_cache, tile_uv);
	return textureLod(tile_cache, tile_uv, 0.0);
}