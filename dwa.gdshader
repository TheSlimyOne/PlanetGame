shader_type spatial;
render_mode unshaded;
const float grid_size = 15.0;
uniform float u_svtId;
uniform float u_svtDepth = 5.0;
uniform vec2 u_svtResolution = vec2(512, 1024);

const float svtDetectionScaleFactor = -log2(4.0);

float mipmapLevel(in vec2 texelCoord, in float bias) {
    vec2  dxVtc        = dFdx(texelCoord);
    vec2  dyVtc        = dFdy(texelCoord);
    float deltaMaxSqr  = max(dot(dxVtc, dxVtc), dot(dyVtc, dyVtc));
    return               0.5 * log2(deltaMaxSqr) + bias;
}

void fragment() {


	//cell = vec2(cell.x + grid_size * float(id), cell.y) / vec2(grid_size * 6.0, grid_size);
	//vec2 ar = vec2(u_svtResolution.x / u_svtResolution.y, 1.0);
    //float mip = clamp(floor(mipmapLevel(UV * u_svtResolution, svtDetectionScaleFactor)), 0.0, 5);
	//float svtLevel = u_svtDepth - mip;
	//float nTilesLevel = pow(2.0, svtLevel);
    //vec2 nTilesDimension =  ar * nTilesLevel;
    //ALBEDO.xy = normalize(floor(UV * nTilesDimension));
	//ALBEDO = vec3(mip, 0, 0);

	//ALBEDO.xy = (floor(UV * grid_size));
	//ALBEDO.z = ALBEDO.x + ALBEDO.y;

	//vec2 cell =(floor(UV * grid_size));
	//ALBEDO.xy = cell.xy;
	//float checker = mod(cell.x + cell.y, 2.0);
	//vec3 color1 = vec3(1);
	//vec3 color2 = vec3(0);
	//ALBEDO = mix(color1, color2, checker);

}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
