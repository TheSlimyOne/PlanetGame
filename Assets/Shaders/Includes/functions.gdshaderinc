#include "res://Assets/Shaders/Constants.gdshaderinc"

vec2 get_cube_uv(int normal_id, vec3 point) {
	vec2 uv = vec2(0);
	point = (point + 1.0) / 2.0;
	uv.x = normal_id == 0 || normal_id == 1 ? point.z : point.x;
	uv.x = normal_id == 0 || normal_id == 2 || normal_id == 5 ? 1.0 - uv.x : uv.x;
	uv.y = normal_id == 2 || normal_id == 3 ? 1.0 - point.z : 1.0 - point.y;

	return clamp(uv, vec2(0.0), vec2(1.0 - EPS));
}

vec3 get_cube_point(int normal_id, vec2 uv) {
	vec3 point = vec3(0);
	uv = uv * 2.0 - 1.0;

	switch(normal_id) {
		case 0:
			point.x = 1.0;
			point.y = -uv.y;
			point.z = -uv.x;
			break;
		case 1:
			point.x = -1.0;
			point.y = -uv.y;
			point.z = uv.x;
			break;
		case 2:
			point.x = -uv.x;
			point.y = 1.0;
			point.z = -uv.y;
			break;
		case 3:
			point.x = uv.x;
			point.y = -1.0;
			point.z = -uv.y;
			break;
		case 4:
			point.x = uv.x;
			point.y = -uv.y;
			point.z = 1.0;
			break;
		case 5:
			point.x = -uv.x;
			point.y = -uv.y;
			point.z = -1.0;
			break;
	}
	return point;
}

vec3 point_to_point_on_cube(int normal_id, vec2 uv) {
    vec3 point = vec3(0);
    switch (normal_id) {
        case 0:
            point = vec3(1.0, 1.0 - uv.y, 1.0 - uv.x);
            point.yz = 2.0 * point.yz - 1.0;
            break;
        case 1:
            point = vec3(-1.0, 1.0 - uv.y, uv.x);
            point.yz = 2.0 * point.yz - 1.0;
            break;
        case 2:
            point = vec3(1.0 - uv.x, 1.0, 1.0 - uv.y);
            point.xz = 2.0 * point.xz - 1.0;
            break;
        case 3:
            point = vec3(uv.x, -1.0, 1.0 - uv.y);
            point.xz = 2.0 * point.xz - 1.0;
            break;
        case 4:
            point = vec3(uv.x, 1.0 - uv.y, 1.0);
            point.xy = 2.0 * point.xy - 1.0;
            break;
        case 5:
            point = vec3(1.0 - uv.x, 1.0 - uv.y, -1.0);
            point.xy = 2.0 * point.xy - 1.0;
            break;
    }
    return point;
}

int[4] get_adjecent_normal_ids(int normal_id)
{
	switch(normal_id){
		case 0: 
			return {4, 5, 3, 2};
		case 1: 
			return {5, 4, 3, 2};
		case 2: 
			return {0, 1, 5, 4};
		case 3: 
			return {1, 0, 5, 4};
		case 4: 
			return {1, 0, 3, 2};
		case 5: 
			return {0, 1, 3, 2};
	}
	return {-1, -1, -1, -1};
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
    vec3 square = p * p;
    return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

vec4 get_tile(sampler2DArray tile_cache, vec3 tile_uv) {
    //return texture2D_linear(tile_cache, tile_uv);
	return textureLod(tile_cache, tile_uv, 0.0);
}

bool is_tile_in_cache(
    sampler2DArray indirection_table,
    int mip_index,
    vec2 uv,
    int normal_id,
    int low_resolution_mip_count,
    int high_resolution_mip_count) 
{
    int total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count;
    float grid_size = exp2(float(total_resolution_mip_count - 1));

    ivec3 indirection_index = ivec3(
        ivec2(floor(uv * grid_size)),
        normal_id
    );

    uvec4 indirection_data = floatBitsToUint(
        texelFetch(indirection_table, indirection_index, 0)
    );

    uint stored_mip_index = indirection_data.g;
    uint valid_flag = indirection_data.b;

    return valid_flag == 255u && stored_mip_index == uint(mip_index);
}

vec3 get_tile_uv(
	sampler2DArray indirection_table,
	vec2 uv,
	int normal_id, 
	int low_resolution_mip_count,
	int high_resolution_mip_count) 
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
	else
	{
		return vec3(uv, float(normal_id));
	}
}

mat4 get_rotation_from_matrix(mat4 transform_matrix) {
    return mat4(
        vec4(transform_matrix[0].xyz / length(transform_matrix[0].xyz), 0),
        vec4(transform_matrix[1].xyz / length(transform_matrix[1].xyz), 0),
        vec4(transform_matrix[2].xyz / length(transform_matrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );
}
