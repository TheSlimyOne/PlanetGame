shader_type spatial;
//render_mode blend_add;
uniform vec4[60] position_list;
uniform float radius;
varying float elevation;
uniform sampler2D albedo_map : repeat_disable;
uniform sampler2D image_texture : repeat_disable;
uniform bool is_debug;
uniform bool is_cube;
uniform float height_scale;
uniform float normal_strength;
varying vec3 object_space_pos;
uniform float morphFactor;

int findMSB64(uvec2 key)
{
    return (key.x == 0u) ? int(findMSB(key.y)) : int((findMSB(key.x) + 32u));
}

uvec2 leftShift64(uvec2 nodeID, uint shift)
{
    uvec2 result;
    if (shift == 0u) return nodeID;

    if (shift < 32u)
    {
        result.x = (nodeID.x << shift) | (nodeID.y >> (32u - shift));
        result.y = nodeID.y << shift;
    }
    else
    {
        result.x = nodeID.y << (shift - 32u);
        result.y = 0u;
    }

    return result;
}

uvec2 rightShift64(uvec2 nodeID, uint shift)
{
    uvec2 result;
    if (shift == 0u) return nodeID;

    if (shift < 32u)
    {
        result.y = (nodeID.y >> shift) | (nodeID.x << (32u - shift));
        result.x = nodeID.x >> shift;
    }
    else
    {
        result.y = nodeID.x >> (shift - 32u);
        result.x = 0u;
    }

    return result;
}

vec2 morphVertex(vec2 vertex, float morphK)
{
    float patchTessFactor = 4.0;
    vec2 fracPart = fract(vertex * patchTessFactor * 0.5);
    vec2 intPart = floor(vertex * patchTessFactor * 0.5);
    vec2 signVec = mod(intPart, 2.0) * vec2(-2.0) + vec2(1.0);
    return vertex.xy - (signVec * fracPart) * morphK;
}

vec2 getTranslation(uint b1)
{
    vec2 translation;
    translation.x = float(b1 & 0x1u);
    translation.y = float(b1 ^ 0x1u);
    return translation * 0.5;
}

ivec2 quickPI_2(int a)
{
    int b = a & 3;
    int b1 = b >> 1;
    int b2 = b & 1;
    int bn2 = b2 ^ 1;
    int c = bn2 - (2 * (b1 & bn2));
    int s = b2 - (2 * (b1 & b2));
    return ivec2(c, s);
}

vec2 rotate(int rotationIndex, vec2 translation)
{
    vec2 r;
    ivec2 trig = quickPI_2(rotationIndex);
    r.x = float(trig.x) * translation.x - float(trig.y) * translation.y;
    r.y = float(trig.y) * translation.x + float(trig.x) * translation.y;
    return r;
}

int getRotation(uint b1b2, uint b1, uint b2)
{
    uint a = (b1b2 ^ 0x2u);
    uint b = (a | 0x1u);
    uint c = (b1 ^ b2);
    return int(b * c);
}

vec3 point_on_cube_to_point_on_sphere(vec3 p)
{
    float x2 = p.x * p.x;
    float y2 = p.y * p.y;
    float z2 = p.z * p.z;

    float x = p.x * sqrt(1.0 - (y2 + z2) / 2.0 + y2 * z2 / 3.0);
    float y = p.y * sqrt(1.0 - (z2 + x2) / 2.0 + z2 * x2 / 3.0);
    float z = p.z * sqrt(1.0 - (x2 + y2) / 2.0 + x2 * y2 / 3.0);

    return vec3(x, y, z);
}

uint getBranching(uvec2 key, int level, int msb)
{
    return (rightShift64(key, uint(msb - 2) - uint(level * 2)).y & 0x3u);
}

vec3 localPointToWorldPoint(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC)
{
    return vertexA * point.x + vertexB * point.y + vertexC * (1.0 - point.x - point.y);
}

vec3 quad_space_to_world_space(vec2 point, uint meshPolygonID, uint rootID, float morphK)
{
    uint vertexBaseIndex = meshPolygonID * 5u;
    uint vertexKeyA = rootID;
    uint vertexKeyB = ((rootID >> 1u) ^ 1u) + ((rootID & 1u) << 1u);

    vec3 base_Triangle_a = (position_list[vertexBaseIndex + vertexKeyA + 1u]).xyz;
    vec3 base_Triangle_b = (position_list[vertexBaseIndex + vertexKeyB + 1u]).xyz;
    vec3 base_Triangle_c = (position_list[vertexBaseIndex]).xyz;

    return localPointToWorldPoint(morphVertex(point, morphK), base_Triangle_a, base_Triangle_b, base_Triangle_c);
}

mat3 leaf_space(uvec4 key)
{
    int msb = findMSB64(key.xy);
    vec2 translation = vec2(0, 0);
    vec2 temp = vec2(0, 0);
    int theta = 0;
    float scale = 1.0;


    for (int i = 0; i < msb / 2; i++)
    {
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

vec2 leaf_space_to_quad_space(vec3 point, uvec4 key)
{
    mat3 leaf_matrix = leaf_space(key);
    return (point * leaf_matrix).xy;
}

int getLevelInKey(uvec2 key)
{
    return findMSB64(key) / 2;
}

vec3 height_to_normal(float height, float strength, vec3 vertex, vec3 _normal)
{
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

vec2 point_on_sphere_to_UV(vec3 p)
{
	p = normalize(p);
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / (2.0 * PI) + 0.5);
	float v = (latitude / PI) + 0.5;
	return vec2(u, v);
}

varying flat uvec4 key;
void vertex()
{
	
	key.x = floatBitsToUint(INSTANCE_CUSTOM.x);
	key.y = floatBitsToUint(INSTANCE_CUSTOM.y);
	key.z = floatBitsToUint(INSTANCE_CUSTOM.z);
	key.w = floatBitsToUint(INSTANCE_CUSTOM.w);
	//key = uvec4(1u, 0u, 0u, 0u);
	//key = uvec4(0u, 16777218u, 0u, 0u);
	//key = uvec4(0u, 16777219u, 0u, 0u);
	

	vec2 quad_position = leaf_space_to_quad_space(VERTEX, key);
	float distanceValue = COLOR.x;
	distanceValue = fract(distanceValue);

	vec3 world_position = quad_space_to_world_space(quad_position, key.z, key.w, 0);
	VERTEX = is_cube ? world_position : point_on_cube_to_point_on_sphere(world_position);


	NORMAL = VERTEX;
	object_space_pos = VERTEX;
	elevation = textureLod(image_texture, point_on_sphere_to_UV(VERTEX), 0).r;
	//UV = point_on_sphere_to_UV(VERTEX);
	VERTEX *= radius;
	VERTEX += NORMAL * height_scale * pow(elevation, 4);

	int msb = getLevelInKey(key.xy);
	//VERTEX += vec3(0,1,0) * float(msb);
}

void fragment()
{
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	int msb = getLevelInKey(key.xy);
	int a = (msb & 1) != 0 ? 1 : 0;
	int b = (msb & 2) != 0 ? 1 : 0;
	int c = (msb & 4) != 0 ? 1 : 0;
	vec3 debug_color = vec3(float(a), float(b), float(c));

	//vec3 surface_texture = textureLod(albedo_map, point_on_sphere_to_UV(object_space_pos), 0).rgb;
	vec3 surface_texture = textureLod(albedo_map, vec2(elevation, 0.0), 0).rgb;
	ALBEDO = is_debug ? debug_color :  surface_texture ;
}

//void light() {
// Called for every pixel for every light affecting the material.
// Uncomment to replace the default light processing function with this one.
//}
