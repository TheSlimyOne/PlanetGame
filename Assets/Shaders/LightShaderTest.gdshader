shader_type spatial;
varying vec3 normal;
void vertex(){
	normal = NORMAL;
}

void fragment(){
	ALBEDO = normal;

}

