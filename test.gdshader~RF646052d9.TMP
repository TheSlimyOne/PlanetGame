shader_type spatial;
varying vec3 wrld_vertex;
uniform sampler2D albedo_map : repeat_disable, source_color;

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

void vertex() {
	VERTEX *= 2.0;
	//wrld_vertex = (wrld_vertex + vec3(1.0)) / 2.0;
	//VERTEX = point_on_cube_to_point_on_sphere(VERTEX);
	wrld_vertex = VERTEX;
}

void fragment() {
	vec3 color = textureLod(albedo_map, UV, 0).xyz;
	ALBEDO = color;
	//ALBEDO = vec3(UV.x, 0, UV.y);

}



//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
