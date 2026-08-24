#include "res://Assets/Shaders/Constants.gdshaderinc"

const int EDGE_LEFT = 0;
const int EDGE_RIGHT = 1;
const int EDGE_UP = 2;
const int EDGE_DOWN = 3;

struct EdgeMapping {
	int normal_id;
	int edge;
	bool reversed;
};

const ivec4 ADJACENT_NORMAL_IDS[6] = {
	ivec4(4, 5, 3, 2),
	ivec4(5, 4, 3, 2),
	ivec4(0, 1, 5, 4),
	ivec4(1, 0, 5, 4),
	ivec4(1, 0, 3, 2),
	ivec4(0, 1, 3, 2)
};

const ivec4 ADJACENT_EDGES[6] = {
	ivec4(EDGE_RIGHT, EDGE_LEFT, EDGE_RIGHT, EDGE_LEFT),
	ivec4(EDGE_RIGHT, EDGE_LEFT, EDGE_LEFT, EDGE_RIGHT),
	ivec4(EDGE_DOWN, EDGE_DOWN, EDGE_DOWN, EDGE_DOWN),
	ivec4(EDGE_UP, EDGE_UP, EDGE_UP, EDGE_UP),
	ivec4(EDGE_RIGHT, EDGE_LEFT, EDGE_DOWN, EDGE_DOWN),
	ivec4(EDGE_RIGHT, EDGE_LEFT, EDGE_UP, EDGE_UP)
};

const bvec4 ADJACENT_REVERSED[6] = {
	bvec4(false, false, false, false),
	bvec4(false, false, true, true),
	bvec4(false, true, false, true),
	bvec4(true, false, true, false),
	bvec4(false, false, false, true),
	bvec4(false, false, true, false)
};

EdgeMapping get_adjacent_edge(int normal_id, int edge) {
	return EdgeMapping(
		ADJACENT_NORMAL_IDS[normal_id][edge],
		ADJACENT_EDGES[normal_id][edge],
		ADJACENT_REVERSED[normal_id][edge]
	);
}

vec2 map_edge_uv(vec2 uv, int edge, EdgeMapping mapping) {
	float edge_position = edge < 2 ? uv.y : uv.x;

	if (mapping.reversed)
		edge_position = 1.0 - edge_position;

	vec2 result = vec2(edge_position);

	if (mapping.edge == EDGE_LEFT)
		result.x = 0.0;
	else if (mapping.edge == EDGE_RIGHT)
		result.x = 1.0;
	else if (mapping.edge == EDGE_DOWN)
		result.y = 0.0;
	else
		result.y = 1.0;

	return clamp(result, vec2(0.0), vec2(1.0 - EPS));
}

vec2 get_plane_uv(int normal_id, vec3 point) {
	point = (point + 1.0) * 0.5;

	vec2 uv;
	uv.x = normal_id == 0 || normal_id == 1 ? point.z : point.x;
	uv.x = normal_id == 0 || normal_id == 2 || normal_id == 5 ? 1.0 - uv.x : uv.x;
	uv.y = normal_id == 2 || normal_id == 3 ? 1.0 - point.z : 1.0 - point.y;

	return clamp(uv, vec2(0.0), vec2(1.0 - EPS));
}

vec3 get_cube_point(int normal_id, vec2 uv) {
	uv = uv * 2.0 - 1.0;

	switch (normal_id) {
		case 0:
			return vec3(1.0, -uv.y, -uv.x);
		case 1:
			return vec3(-1.0, -uv.y, uv.x);
		case 2:
			return vec3(-uv.x, 1.0, -uv.y);
		case 3:
			return vec3(uv.x, -1.0, -uv.y);
		case 4:
			return vec3(uv.x, -uv.y, 1.0);
		case 5:
			return vec3(-uv.x, -uv.y, -1.0);
	}

	return vec3(0.0);
}

vec3 point_on_cube_to_point_on_sphere(vec3 point) {
	vec3 square = point * point;
	return point * sqrt(1.0 - (square.yxx + square.zzy) * 0.5 + square.yxx * square.zzy / 3.0);
}

vec4 get_tile(sampler2DArray tile_cache, vec3 tile_uv) {
	return textureLod(tile_cache, tile_uv, 0.0);
}

ivec3 get_indirection_index(vec2 uv, int normal_id, int low_resolution_mip_count, int high_resolution_mip_count) {
	int total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count;
	float grid_size = exp2(float(total_resolution_mip_count - 1));

	return ivec3(ivec2(floor(uv * grid_size)), normal_id);
}

bool is_tile_in_cache(sampler2DArray indirection_table, int mip_index, vec2 uv, int normal_id, int low_resolution_mip_count, int high_resolution_mip_count) {
	ivec3 indirection_index = get_indirection_index(
		uv,
		normal_id,
		low_resolution_mip_count,
		high_resolution_mip_count
	);

	uvec4 indirection_data = floatBitsToUint(texelFetch(indirection_table, indirection_index, 0));

	return indirection_data.b == 255u && indirection_data.g == uint(mip_index);
}

int get_tile_mip(sampler2DArray indirection_table, vec2 uv, int normal_id, int low_resolution_mip_count, int high_resolution_mip_count) {
	ivec3 indirection_index = get_indirection_index(
		uv,
		normal_id,
		low_resolution_mip_count,
		high_resolution_mip_count
	);

	uvec4 indirection_data = floatBitsToUint(texelFetch(indirection_table, indirection_index, 0));

	if (indirection_data.b != 255u)
		return -1;

	return int(indirection_data.g);
}

bool try_adjacent_edge(
	sampler2DArray indirection_table,
	vec2 original_uv,
	int original_normal_id,
	int edge,
	float grid_size,
	inout vec2 uv,
	inout int normal_id,
	inout uint tile_index,
	inout int mip_index)
{
	EdgeMapping mapping = get_adjacent_edge(original_normal_id, edge);
	vec2 adjacent_uv = map_edge_uv(original_uv, edge, mapping);

	ivec2 adjacent_indirection_position = ivec2(floor(adjacent_uv * grid_size));

	uvec4 adjacent_indirection_data = floatBitsToUint(
		texelFetch(indirection_table, ivec3(adjacent_indirection_position, mapping.normal_id), 0)
	);

	int adjacent_mip_index = int(adjacent_indirection_data.g);
	uint adjacent_valid_flag = adjacent_indirection_data.b;

	bool use_adjacent =
		adjacent_valid_flag == 255u &&
		(
			adjacent_mip_index > mip_index ||
			(adjacent_mip_index == mip_index && mapping.normal_id < normal_id)
		);

	if (!use_adjacent)
		return false;

	uv = adjacent_uv;
	normal_id = mapping.normal_id;
	tile_index = adjacent_indirection_data.r;
	mip_index = adjacent_mip_index;

	return true;
}

void try_adjacent_edges(
	sampler2DArray indirection_table,
	vec2 original_uv,
	int original_normal_id,
	float grid_size,
	inout vec2 uv,
	inout int normal_id,
	inout uint tile_index,
	inout int mip_index)
{
	bool on_left = original_uv.x <= 0.0;
	bool on_right = original_uv.x >= 1.0 - EPS;
	bool on_up = original_uv.y >= 1.0 - EPS;
	bool on_down = original_uv.y <= 0.0;

	int edge_count = int(on_left) + int(on_right) + int(on_up) + int(on_down);

	if (edge_count == 1) {
		int edge = on_left ? EDGE_LEFT : on_right ? EDGE_RIGHT : on_up ? EDGE_UP : EDGE_DOWN;
		try_adjacent_edge(indirection_table, original_uv, original_normal_id, edge, grid_size, uv, normal_id, tile_index, mip_index);
	}
	else if (edge_count == 2) {
		int horizontal_edge = on_left ? EDGE_LEFT : EDGE_RIGHT;
		int vertical_edge = on_down ? EDGE_DOWN : EDGE_UP;

		try_adjacent_edge(indirection_table, original_uv, original_normal_id, horizontal_edge, grid_size, uv, normal_id, tile_index, mip_index);
		try_adjacent_edge(indirection_table, original_uv, original_normal_id, vertical_edge, grid_size, uv, normal_id, tile_index, mip_index);
	}
}

vec3 get_tile_uv(sampler2DArray indirection_table, vec2 uv, int normal_id, int low_resolution_mip_count, int high_resolution_mip_count) {
	int total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count;
	float grid_size = exp2(float(total_resolution_mip_count - 1));

	ivec3 indirection_index = get_indirection_index(
		uv,
		normal_id,
		low_resolution_mip_count,
		high_resolution_mip_count
	);

	uvec4 indirection_data = floatBitsToUint(texelFetch(indirection_table, indirection_index, 0));

	uint tile_index = indirection_data.r;
	int mip_index = int(indirection_data.g);

	try_adjacent_edges(
		indirection_table,
		uv,
		normal_id,
		grid_size,
		uv,
		normal_id,
		tile_index,
		mip_index
	);

	float lod_grid_size = grid_size / exp2(float(mip_index));
	vec2 tile_uv = fract(uv * lod_grid_size);

	return vec3(tile_uv, float(tile_index));
}

mat4 get_rotation_from_matrix(mat4 transform_matrix) {
	return mat4(
		vec4(transform_matrix[0].xyz / length(transform_matrix[0].xyz), 0.0),
		vec4(transform_matrix[1].xyz / length(transform_matrix[1].xyz), 0.0),
		vec4(transform_matrix[2].xyz / length(transform_matrix[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0)
	);
}