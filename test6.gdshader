shader_type spatial;
#include "Assets/Shaders/Includes/functions.gdshaderinc"
#include "Assets/Shaders/Includes/tesselation_functions.gdshaderinc"

uniform sampler2DArray images;
varying vec3 position;
varying vec3 normal;
varying vec2 uv;
void vertex() {
	normal = NORMAL;
	uv = get_cube_uv(get_normal_id(ivec3(normal)), VERTEX);
	VERTEX = point_on_cube_to_point_on_sphere(2.0 * VERTEX);
	position = VERTEX;
	NORMAL = VERTEX;
}

void fragment() {
	int normal_id = get_normal_id(ivec3(normal));
	
	ALBEDO = texture(images, vec3(uv, float(normal_id))).xyz;
	// Called for every pixel the material is visible on.
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
