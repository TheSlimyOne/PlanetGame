shader_type spatial;
#include "res://Assets/Shaders/Includes/functions.gdshaderinc"
#include "res://Assets/Shaders/Includes/tesselation_functions.gdshaderinc"

uniform sampler2D albedo : repeat_disable, source_color;
uniform sampler2D height_map : repeat_disable, source_color;

varying vec3 normal;
varying vec3 sphere_point;

void vertex() {
	normal = NORMAL;

	VERTEX = point_on_cube_to_point_on_sphere(2.0 * VERTEX);
	sphere_point = VERTEX;
	NORMAL = VERTEX;
}

void fragment() {
	vec2 uv = point_on_sphere_to_uv(sphere_point);
	ALBEDO = texture(albedo, uv).xyz;

}