shader_type spatial;
uniform float columns;
uniform float rows;


vec2 random(vec2 uv) {
	return vec2(fract(sin(dot(uv.xy,
		vec2(12.9898,78.233))) * 43758.5453123));
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	float x2 = p.x * p.x;
	float y2 = p.y * p.y;
	float z2 = p.z * p.z;
	
	float x = p.x * sqrt(1.0 - (y2 + z2) / 2.0 + y2 * z2 / 3.0);
	float y = p.y * sqrt(1.0 - (z2 + x2) / 2.0 + z2 * x2 / 3.0);
	float z = p.z * sqrt(1.0 - (x2 + y2) / 2.0 + x2 * y2 / 3.0);

	return vec3(x, y, z);
}

vec2 voronoi(vec2 uv) {
	
	vec2 index_uv = floor(vec2(uv.x * columns, uv.y * rows));
	vec2 fract_uv = fract(vec2(uv.x * columns, uv.y * rows));
	
	float minimum_dist = 1.0;  
	vec2 minimum_point;

	for (int y= -1; y <= 1; y++) {
		for (int x= -1; x <= 1; x++) {
			vec2 neighbor = vec2(float(x),float(y));
			vec2 point = random(index_uv + neighbor);

			vec2 diff = neighbor + point - fract_uv;
			float dist = length(diff);
			
			if(dist < minimum_dist) {
				minimum_dist = dist;
				minimum_point = point;
			}
		}
	}
	return minimum_point;
}

void vertex()
{
}

void fragment() {
	vec3 voronoi = vec3(voronoi(UV).r);
	ALBEDO = voronoi;
}
