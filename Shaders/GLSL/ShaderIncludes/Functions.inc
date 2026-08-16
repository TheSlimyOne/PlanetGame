vec2 get_cell_uv(vec2 global_uv, float grid_size) {
	vec2 grid_position = global_uv * vec2(grid_size);
    vec2 cell_uv = fract(grid_position);
	return cell_uv;
}

vec3 get_tile_cache_uv(
	sampler2DArray indirection_table,
	vec2 uv,
	int normal_id,
	int low_resolution_mip_count,
	int high_resolution_mip_count
	)
	{
	int total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count;

	float grid_size = exp2(float(total_resolution_mip_count - 1));

	ivec3 indirection_index = ivec3(
		ivec2(floor(uv * grid_size)),
		normal_id
	);

	uvec4 indirection_data = floatBitsToUint(
		texelFetch(
			indirection_table,
			indirection_index,
			0
		)
	);

	uint tile_index = indirection_data.r;
	int mip_index = int(indirection_data.g);
	uint valid_flag = indirection_data.b;

	if (valid_flag == 255u)
	{
		float lod_grid_size = grid_size / exp2(float(mip_index));
		vec2 grid_uv = fract(uv * lod_grid_size);

		return vec3(grid_uv, float(tile_index));
	}

	return vec3(uv, float(normal_id));
}

	vec4 get_tile(sampler2DArray tile_cache, vec3 tile_uv) {
	//return texture2D_linear(tile_cache, tile_uv);
	return textureLod(tile_cache, tile_uv, 0.0);
	}