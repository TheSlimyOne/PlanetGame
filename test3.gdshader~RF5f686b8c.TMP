shader_type spatial;
render_mode shadows_disabled;
uniform sampler2D image : repeat_disable;

// Calculates normals from a heightmap
vec3 calculate_normals(sampler2D heightmap, vec2 uv, float texel_size, float height_scale) {
    // Sample neighboring height values
    float height_l = texture(heightmap, uv + vec2(-texel_size, 0.0)).r * height_scale;
    float height_r = texture(heightmap, uv + vec2(texel_size, 0.0)).r * height_scale;
    float height_d = texture(heightmap, uv + vec2(0.0, -texel_size)).r * height_scale;
    float height_u = texture(heightmap, uv + vec2(0.0, texel_size)).r * height_scale;

    // Calculate gradient
    vec3 dx = vec3(2.0 * texel_size, 0.0, height_r - height_l);
    vec3 dy = vec3(0.0, 2.0 * texel_size, height_u - height_d);

    // Calculate normal with cross product
    vec3 normal = normalize(cross(dx, dy));

    return normal;
}
void vertex()
{
	NORMAL = calculate_normals(image, UV, 1.0, 1.0);
}
void fragment() {

	ALBEDO = textureLod(image, UV, 0.0).xyz;
	//ALBEDO = vec3(0);

}
