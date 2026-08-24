const float EPS = 1e-7;
const float sqrt2 = 1.414213562;
const mat3 barycentric_space_matrix = mat3(vec3(-1.0,  1.0, 0.0), vec3(-1.0,  0.0, 1.0), vec3(1.0,  0.0, 0.0));
const vec3 normals[6] = vec3[6](
    vec3(1.0, 0.0, 0.0),
    vec3(-1.0, 0.0, 0.0),
    vec3(0.0, 1.0, 0.0),
    vec3(0.0, -1.0, 0.0),
    vec3(0.0, 0.0, 1.0),
    vec3(0.0, 0.0, -1.0)
);
const vec3 normals_to_colors[6] = vec3[6](
    vec3(1.0, 0.0, 0.0),  // red
    vec3(0.0, 1.0, 0.0),  // green
    vec3(0.0, 0.0, 1.0),  // blue
    vec3(1.0, 1.0, 0.0),  // yellow
    vec3(1.0, 0.0, 1.0),  // magenta
    vec3(0.0, 1.0, 1.0)   // cyan
);

struct FaceMap {
    vec3 dX;
    vec3 dY;
};

const FaceMap face_map[6] = {
	FaceMap(vec3( 0, 0,-1), vec3( 0, 1, 0)),
	FaceMap(vec3( 0, 0, 1), vec3( 0, 1, 0)),
	FaceMap(vec3( 1, 0, 0), vec3( 0, 0, 1)),
	FaceMap(vec3( 1, 0, 0), vec3( 0, 0,-1)),
	FaceMap(vec3( 1, 0, 0), vec3( 0, 1, 0)),
	FaceMap(vec3(-1, 0, 0), vec3( 0, 1, 0))
};

const vec3 mip_to_colors[12] = vec3[](
	vec3(1.0, 0.0, 0.0),
	vec3(0.0, 1.0, 0.0),
	vec3(0.0, 0.0, 1.0),
	vec3(1.0, 1.0, 0.0),
	vec3(1.0, 0.0, 1.0),
	vec3(0.0, 1.0, 1.0),
	vec3(1.0, 0.5, 0.0),
	vec3(0.5, 0.0, 1.0),
	vec3(0.0, 0.5, 1.0),
	vec3(0.5, 1.0, 0.0),
	vec3(1.0, 0.0, 0.5),
	vec3(0.7, 0.7, 0.7)
);