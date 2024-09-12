shader_type spatial;
//render_mode unshaded;

uniform vec4[60] position_list;
uniform float radius;
varying float elevation;
uniform sampler2D albedo_map : repeat_disable;
uniform sampler2D height_map : repeat_disable;
uniform sampler2D normal_map : repeat_disable;
uniform sampler2D height_gradient : repeat_disable;
uniform bool is_colorize_lod;
uniform bool is_cube;
uniform bool is_texture_1D;
uniform bool is_culling;
uniform float height_scale;
uniform float normal_strength;
varying vec3 object_space_pos;
uniform int resolution;
uniform mat4 transformations;

uniform int octaves : hint_range(0, 12) = 5;
uniform float persistence = 0.5;
uniform float lacunarity = 2.0;
uniform float noise_scale = 1;
uniform float exaggeration_factor = 1;
uniform vec3 offset = vec3(0, 0, 0);
uniform float seed = 1207.01;

uniform sampler2D key_image : repeat_disable;
uniform sampler2D global_key_data : repeat_disable;

int findMSB64(uvec2 key) {
    return (key.x == 0u) ? int(findMSB(key.y)) : int((findMSB(key.x) + 32u));
}

vec2 morphVertex(vec2 vertex, float current_lod, float morphK) {
	float resolution_index = log2(float(resolution - 1));
	
	float patchTessFactor = pow(2, resolution_index + current_lod); // = nb of intervals per side of the primitive
	vec2 fracPart = fract(vertex * patchTessFactor * 0.5);
	fracPart *= 2.0 / patchTessFactor;

	vec2 intPart = floor(vertex * patchTessFactor * 0.5);
	vec2 signVec = mod(intPart, 2.0) * vec2(-2.0) + vec2(1.0);
	return vertex - (signVec * fracPart) * morphK;
}

uvec2 leftShift64(uvec2 nodeID, uint shift) {
    uvec2 result;
    if (shift == 0u) return nodeID;

    if (shift < 32u) {
        result.x = (nodeID.x << shift) | (nodeID.y >> (32u - shift));
        result.y = nodeID.y << shift;
    } else {
        result.x = nodeID.y << (shift - 32u);
        result.y = 0u;
    }
    return result;
}

uvec2 rightShift64(uvec2 nodeID, uint shift) {
    uvec2 result;
    if (shift == 0u) return nodeID;

    if (shift < 32u) {
        result.y = (nodeID.y >> shift) | (nodeID.x << (32u - shift));
        result.x = nodeID.x >> shift;
    } else {
        result.y = nodeID.x >> (shift - 32u);
        result.x = 0u;
    }

    return result;
}

vec2 getTranslation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1u);
    translation.y = float(b1 ^ 0x1u);
    return translation * 0.5;
}

ivec2 quickPI_2(int a) {
    int b = a & 3;
    int b1 = b >> 1;
    int b2 = b & 1;
    int bn2 = b2 ^ 1;
    int c = bn2 - (2 * (b1 & bn2));
    int s = b2 - (2 * (b1 & b2));
    return ivec2(c, s);
}

vec2 rotate(int rotationIndex, vec2 translation) {
    vec2 r;
    ivec2 trig = quickPI_2(rotationIndex);
    r.x = float(trig.x) * translation.x - float(trig.y) * translation.y;
    r.y = float(trig.y) * translation.x + float(trig.x) * translation.y;
    return r;
}

int getRotation(uint b1b2, uint b1, uint b2) {
    uint a = (b1b2 ^ 0x2u);
    uint b = (a | 0x1u);
    uint c = (b1 ^ b2);
    return int(b * c);
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
    vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

uint getBranching(uvec2 key, int level, int msb) {
    return (rightShift64(key, uint(msb - 2) - uint(level * 2)).y & 0x3u);
}

float getScale(float idx) {
	return 1.0/pow(2, idx);
}

vec3 localPointToWorldPoint(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
    return vertexA * point.x + vertexB * point.y + vertexC * (1.0 - point.x - point.y);
}

vec3 quad_space_to_world_space(vec2 point, uint meshPolygonID, uint rootID) {
    uint vertexBaseIndex = meshPolygonID * 5u;
    uint vertexKeyA = rootID;
    uint vertexKeyB = ((rootID >> 1u) ^ 1u) + ((rootID & 1u) << 1u);

    vec3 base_Triangle_a = (position_list[vertexBaseIndex + vertexKeyA + 1u]).xyz;
    vec3 base_Triangle_b = (position_list[vertexBaseIndex + vertexKeyB + 1u]).xyz;
    vec3 base_Triangle_c = (position_list[vertexBaseIndex]).xyz;

    return localPointToWorldPoint(point, base_Triangle_a, base_Triangle_b, base_Triangle_c);
}

mat3 leaf_space(uvec4 key) {
    int msb = findMSB64(key.xy);
    vec2 translation = vec2(0, 0);
    vec2 temp = vec2(0, 0);
    int theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = getBranching(key.xy, i, msb);
        uint b1 = b1b2 >> 1u;
        uint b2 = b1b2 & 1u;
        temp = scale * getTranslation(b1);

        translation += rotate(theta, temp);
        theta += getRotation(b1b2, b1, b2);
        scale *= 0.5;
    }

    ivec2 trig = quickPI_2(theta);

    mat3 transform_matrix = mat3(
        vec3(float(trig.x) * scale, float(-trig.y) * scale, translation.x),
        vec3(float(trig.y) * scale, float(trig.x) * scale, translation.y),
        vec3(0.0, 0.0, 1.0)
    );

    return transform_matrix;
}

vec2 leaf_space_to_quad_space(vec3 point, uvec4 key) {
    mat3 leaf_matrix = leaf_space(key);
    return (point * leaf_matrix).xy;
}

int getLevelInKey(uvec2 key) {
    return findMSB64(key) / 2;
}

vec3 height_to_normal(float height, float strength, vec3 vertex, vec3 _normal) {
	vec3 dX = dFdx(vertex);
	vec3 dY = dFdy(vertex);

	vec3 crossX = cross(_normal, dX);
	vec3 crossY = cross(dY, _normal);

	float dt = dot(dX, crossY);
	float sign = dt < 0.0 ? -1.0 : 1.0;
	float surface = sign / max(1.192093e-12, abs(dt));

	float dHdx = dFdx(height);
	float dHdy = dFdy(height);

	vec3 surfGrad = surface * (dHdx * crossY + dHdy * crossX);

	return normalize(_normal - strength * surfGrad);
}

vec2 point_on_sphere_to_UV(vec3 p) {
	p = normalize(p);
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / PI + 1.0) * 0.5;
	float v = latitude / PI + 0.5;
	return vec2(u, v);
}

ivec2 getKeyCoordinate(int idx) {
    ivec2 image_size = ivec2(textureSize(key_image, 0));
    return ivec2(idx % image_size.x, idx / image_size.y);
}

mat4 createTransform() {
	float scale = 1.0 / getScale(texelFetch(global_key_data, ivec2(0, 0), 0).r) * radius;
	vec3 scaleFromPoint = vec3(0, 0, 1);
	//scale = radius;
	return mat4(
		vec4( scale, 0, 0, scaleFromPoint.x - scale * scaleFromPoint.x),
		vec4( 0, scale, 0, scaleFromPoint.y - scale * scaleFromPoint.y),
		vec4( 0, 0, scale, scaleFromPoint.z - scale * scaleFromPoint.z - 1.0),
		vec4( 0, 0, 0, 1)
	);
}

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

float param_noise(vec3 p) {
	float frequency = 1.0;
	float amplitude = 1.0;
	float noiseHeight = 0.0;
	
	for (int i = 0; i < octaves; i++) {
		float sampleX = p.x / noise_scale * frequency + offset.x;
		float sampleY = p.y / noise_scale * frequency + offset.y;
		float sampleZ = p.z / noise_scale * frequency + offset.z;
		
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

varying flat uvec4 key;
varying flat uint flags;
varying float morph;
void vertex() {
	if (is_culling) {
		key = floatBitsToUint(texelFetch(key_image, getKeyCoordinate(int(INSTANCE_ID)), 0));
	} else {
		key = floatBitsToUint(INSTANCE_CUSTOM);
	}
	flags = key.w >> 29u;
	uint half = (key.w & 0xFFFF0u) >> 4u;
	half = half | (half << 16u);
	morph = unpackHalf2x16(half).x;
	key.w &= 0xFu;
	
	
	float skipAmount = 1.0 / float(resolution - 1);

	if (flags == 0u && (int(UV.y) == 0) && (int(UV.x) % 2 == 1)) {
		VERTEX -= vec3(0, skipAmount, 0);
	}
	if (flags == 3u && (int(UV.x) == 0) && (int(UV.y) % 2 == 1)) {
		VERTEX -= vec3(skipAmount, 0, 0);
	}
	if (flags == 1u && (int(UV.x) == 0) && (int(UV.y) % 2 == 1)) {
		VERTEX -= vec3(skipAmount, 0, 0);
	}
	if (flags == 2u && (int(UV.y) == 0) && (int(UV.x) % 2 == 1)) {
		VERTEX -= vec3(0, skipAmount, 0);
	}
	
	vec2 quad_position = leaf_space_to_quad_space(VERTEX, key);
	vec3 world_position = quad_space_to_world_space(morphVertex(quad_position, float(getLevelInKey(key.xy)), 0.0 * morph), key.z, key.w);
	VERTEX = is_cube ? world_position : point_on_cube_to_point_on_sphere(world_position);
	
	// Save the vertex for later
	// After all the transformations use this Vertex as a uv coordinate
	object_space_pos = VERTEX;
	VERTEX = (vec4(VERTEX, 1) * transformations).xyz;

	mat4 rotationOnly = mat4(
		vec4(normalize(transformations[0].xyz), 0), 
		vec4(normalize(transformations[1].xyz), 0), 
		vec4(normalize(transformations[2].xyz), 0),
		vec4(0, 0, 0, 1)
	);
	
	NORMAL = (vec4(object_space_pos, 1) * rotationOnly).xyz;
	elevation = textureLod(height_map, point_on_sphere_to_UV(object_space_pos), 0).r;
	//elevation *= textureLod(height_gradient, vec2(elevation, 0), 0).r;

	VERTEX += NORMAL * height_scale * elevation;
}

void fragment() {
	
	int msb = getLevelInKey(key.xy);
	int a = (msb & 1) != 0 ? 1 : 0;
	int b = (msb & 2) != 0 ? 1 : 0;
	int c = (msb & 4) != 0 ? 1 : 0;
	vec3 debug_color = vec3(float(a), float(b), float(c));
	vec3 child_color;
	uint childType = key.y & 3u;
	switch (childType)
	{
		case 0u:
			child_color = vec3(0);
			break;
		case 1u:
			child_color = vec3(1);
			break;
		case 2u:
			child_color = vec3(0);
			break;
		case 3u:
			child_color = vec3(1);
			break;
	}
	debug_color = mix(debug_color, child_color, 0.25f);
	//debug_color= mix(debug_color, vec3(morph, 0, morph), 0.25f);

	vec2 uv_point = is_texture_1D ? vec2(elevation, 0.0) : point_on_sphere_to_UV(object_space_pos);
	
	vec3 surface_texture = textureLod(albedo_map, uv_point, 0.0).rgb;
	
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	
	//NORMAL_MAP = textureLod(normal_map, uv_point, 0.0).rgb;
	// NORMAL_MAP_DEPTH = height_scale;
	
	ALBEDO = is_colorize_lod ? debug_color : surface_texture;
}

