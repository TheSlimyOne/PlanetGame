float w0(float a) { return (1.0/6.0)*(a*(a*(-a + 3.0) - 3.0) + 1.0); }
float w1(float a) { return (1.0/6.0)*(a*a*(3.0*a - 6.0) + 4.0); }
float w2(float a) { return (1.0/6.0)*(a*(a*(-3.0*a + 3.0) + 3.0) + 1.0); }
float w3(float a) { return (1.0/6.0)*(a*a*a); }
float g0(float a) { return w0(a) + w1(a); }
float g1(float a) { return w2(a) + w3(a); }
float h0(float a) { return -1.0 + w1(a) / (w0(a) + w1(a)); }
float h1(float a) { return 1.0 + w3(a) / (w2(a) + w3(a)); }

vec4 texture2D_bicubic(sampler2DArray tex, vec3 uv, vec2 res) {
	uv.xy = uv.xy * res + 0.5;
	vec2 iuv = floor( uv.xy );
	vec2 fuv = fract( uv.xy );

	float g0x = g0(fuv.x);
	float g1x = g1(fuv.x);
	float h0x = h0(fuv.x);
	float h1x = h1(fuv.x);
	float h0y = h0(fuv.y);
	float h1y = h1(fuv.y);

	vec2 p0 = (vec2(iuv.x + h0x, iuv.y + h0y) - 0.5) / res;
	vec2 p1 = (vec2(iuv.x + h1x, iuv.y + h0y) - 0.5) / res;
	vec2 p2 = (vec2(iuv.x + h0x, iuv.y + h1y) - 0.5) / res;
	vec2 p3 = (vec2(iuv.x + h1x, iuv.y + h1y) - 0.5) / res;

    return g0(fuv.y) * (g0x * textureLod(tex, vec3(p0, uv.z), 0.0)  +
                        g1x * textureLod(tex, vec3(p1, uv.z), 0.0)) +
           g1(fuv.y) * (g0x * textureLod(tex, vec3(p2, uv.z), 0.0)  +
                        g1x * textureLod(tex, vec3(p3, uv.z), 0.0));
}

vec2 get_cube_uv(int normal_id, vec3 point) {
    point = (point + 1.0) / 2.0;
    vec2 uv = vec2(0);
    uv.x = normal_id == 0 || normal_id == 1 ? point.z : point.x;
    uv.x = normal_id == 0 || normal_id == 2 || normal_id == 5 ? 1.0 - uv.x : uv.x;
    uv.y = normal_id == 2 || normal_id == 3 ? 1.0 - point.z : 1.0 - point.y;
    return uv;
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

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
    vec3 square = p * p;
    return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

vec3 get_sphere_normal(sampler2DArray height_texture, vec3 uv, int normal_id, float sample_pixels, mat4 rotation) {
	float hC = texture2D_bicubic(height_texture, uv, vec2(512.0)).r;
    float hU = texture2D_bicubic(height_texture, uv + vec3(0, sample_pixels, 0), vec2(512.0)).r;
    float hD = texture2D_bicubic(height_texture, uv + vec3(0, -sample_pixels, 0), vec2(512.0)).r;
    float hR = texture2D_bicubic(height_texture, uv + vec3(sample_pixels, 0, 0), vec2(512.0)).r;
	float hL = texture2D_bicubic(height_texture, uv + vec3(-sample_pixels, 0, 0), vec2(512.0)).r;

	vec3 pC = point_on_cube_to_point_on_sphere(point_to_point_on_cube(normal_id, uv.xy));
    vec3 pU = point_on_cube_to_point_on_sphere(point_to_point_on_cube(normal_id, uv.xy + vec2(0, sample_pixels)));
    vec3 pD = point_on_cube_to_point_on_sphere(point_to_point_on_cube(normal_id, uv.xy + vec2(0, -sample_pixels)));
    vec3 pR = point_on_cube_to_point_on_sphere(point_to_point_on_cube(normal_id, uv.xy + vec2(sample_pixels, 0)));
	vec3 pL = point_on_cube_to_point_on_sphere(point_to_point_on_cube(normal_id, uv.xy + vec2(-sample_pixels, 0)));

	float normal_strength = 1.0;
	pC += pC * hC;
	pL += pL * hL;
	pR += pR * hR;
	pU += pU * hU;
	pD += pD * hD;

	vec3 dx = pR - pL;
	vec3 dy = pU - pD;
	vec3 normal_vector = normalize(cross(dy, dx));
	return normal_vector;
}

vec4 get_tile(sampler2DArray tile_cache, vec3 tile_uv) {
    return texture2D_bicubic(tile_cache, tile_uv, vec2(512.0));
}

vec3 get_tile_uv(
    sampler2DArray indirection_table,
    int lod_index,
    float grid_size,
    vec2 uv,
    int total_texture_subdivisions,
    int polygon_id) {

    float lod_grid_size = grid_size / pow(2.0, float(lod_index));
    vec2 cell_uv = floor(uv * lod_grid_size) / lod_grid_size;

    vec3 indirection_index = vec3(
        cell_uv,
        float(total_texture_subdivisions * int(polygon_id) + lod_index)
    );

    uvec4 indirection_table_data = uvec4(textureLod(indirection_table, indirection_index, 0.0) * 255.0);
    vec2 tile_uv = fract(uv * lod_grid_size);
    uint virtual_texture_tile_index = indirection_table_data.r;

    return indirection_table_data.a != 1u ? vec3(uv, float(polygon_id)) : vec3(tile_uv, float(virtual_texture_tile_index));
}

mat4 get_rotation_from_matrix(mat4 transform_matrix) {
    return mat4(
        vec4(transform_matrix[0].xyz / length(transform_matrix[0].xyz), 0),
        vec4(transform_matrix[1].xyz / length(transform_matrix[1].xyz), 0),
        vec4(transform_matrix[2].xyz / length(transform_matrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );
}
