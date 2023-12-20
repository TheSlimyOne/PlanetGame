shader_type spatial;
uniform sampler2D albedo_map: repeat_disable;
uniform sampler2D height_map: repeat_disable;
uniform float radius;
uniform float sea_level;
uniform float normal_strength: hint_range(0.0, 5.0, 0.01) = 0.01;
uniform float height_scale = 10;


varying vec3 object_space_pos;
varying float elevation;


vec3 heightToNormal(float height, float strength, vec3 vertex, vec3 normal) {
    vec3 dX = dFdx(vertex);
    vec3 dY = dFdy(vertex);

    vec3 crossX = cross(normal, dX);
    vec3 crossY = cross(dY, normal);
    
    float dt = dot(dX, crossY);
    float sign = dt < 0.0 ? -1.0 : 1.0;
    float surface = sign / max(1.192093e-12, abs(dt));

    float dHdx = dFdx(height);
    float dHdy = dFdy(height);

    vec3 surfGrad = surface * (dHdx * crossY + dHdy * crossX);

    return normalize(normal - strength * surfGrad);
}


vec2 pointOnSphereToUV(vec3 p) {
    p = normalize(p);
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / (2.0 * PI) + 0.5);
    float v = (latitude / PI) + 0.5;
    return vec2(u, v);
}

uniform float seed = 1207.01;
vec3 hash(vec3 p) {
    p = vec3(dot(p, vec3(127.1, 311.7, 74.7)),
             dot(p, vec3(269.5, 183.3, 246.1)),
             dot(p, vec3(113.5, 271.9, 124.6)));
	p += seed;
    return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
}

float noise(vec3 p) {
  vec3 i = floor(p);
  vec3 f = fract(p);
  vec3 u = f * f * (3.0 - 2.0 * f);
	

  return mix(mix(mix(dot(hash(i + vec3(0.0, 0.0, 0.0)), f - vec3(0.0, 0.0, 0.0)),
                     dot(hash(i + vec3(1.0, 0.0, 0.0)), f - vec3(1.0, 0.0, 0.0)), u.x),
                 mix(dot(hash(i + vec3(0.0, 1.0, 0.0)), f - vec3(0.0, 1.0, 0.0)),
                     dot(hash(i + vec3(1.0, 1.0, 0.0)), f - vec3(1.0, 1.0, 0.0)), u.x), u.y),
             mix(mix(dot(hash(i + vec3(0.0, 0.0, 1.0)), f - vec3(0.0, 0.0, 1.0)),
                     dot(hash(i + vec3(1.0, 0.0, 1.0)), f - vec3(1.0, 0.0, 1.0)), u.x),
                 mix(dot(hash(i + vec3(0.0, 1.0, 1.0)), f - vec3(0.0, 1.0, 1.0)),
                     dot(hash(i + vec3(1.0, 1.0, 1.0)), f - vec3(1.0, 1.0, 1.0)), u.x), u.y), u.z );
}

uniform int octaves = 5;
uniform float persistence = 0.5; 
uniform float lacunarity = 2.0;
uniform float scale = 1;
uniform float exaggeration_factor = 1;
uniform vec3 offset = vec3(0, 0, 0);

float param_noise(vec3 p) {
	float frequency = 1.0;
	float amplitude = 1.0;
	float noiseHeight = 0.0;
	
	for (int i = 0; i < octaves; i++) {
		float sampleX = p.x / scale * frequency + offset.x;
		float sampleY = p.y / scale * frequency + offset.y;
		float sampleZ = p.z / scale * frequency + offset.z;
		
		float noiseValue = noise(vec3(sampleX, sampleY, sampleZ) * 5.0) * 0.5;
		noiseValue += noise(vec3(sampleX, sampleY, sampleZ) * 10.0) * 0.25/2.0;
		noiseValue += noise(vec3(sampleX, sampleY, sampleZ) * 20.0) * 0.125/2.0;
		noiseValue += noise(vec3(sampleX, sampleY, sampleZ) * 40.0) * 0.0625/2.0;

		noiseHeight += noiseValue * amplitude;
		
		amplitude += persistence;
		frequency += lacunarity;
	}

	noiseHeight = pow(max(noiseHeight, 0), exaggeration_factor);
	return noiseHeight;
}


void vertex() {
	object_space_pos = VERTEX;
	vec2 uv = pointOnSphereToUV(object_space_pos);
	float theta = uv.y * 3.14159;
	float phi = uv.x * 3.14159 * 2.0;
	vec3 unit = vec3(0.0, 0.0, 0.0);

	unit.x = sin(phi) * sin(theta);
	unit.y = cos(theta) * -1.0;
	unit.z = cos(phi) * sin(theta);
	unit = normalize(unit);
	
	//elevation = noise(unit * 5.0) * 5.0;
	//elevation += noise(unit * 10.0) * 0.25;
	//elevation += noise(unit * 20.0) * 0.125;
	//elevation += noise(unit * 40.0) * 0.0625;
	elevation = param_noise(unit);
	

	
	

	VERTEX += NORMAL * height_scale * elevation;
	
	
}

void fragment() {
	vec3 surface_texture = texture(albedo_map, vec2(elevation, 0.0)).rgb;
	float latitude = asin(normalize(object_space_pos).y);
	

	ALBEDO = surface_texture;

	NORMAL = heightToNormal(elevation * height_scale, normal_strength, VERTEX, NORMAL);

}