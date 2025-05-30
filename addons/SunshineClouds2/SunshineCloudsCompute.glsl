#[compute]
#version 450
#define PI 3.141592
#define ABSORPTION_COEFFICIENT 0.9

// Invocations in the (x, y, z) dimension
layout(local_size_x = 32, local_size_y = 32, local_size_z = 1) in;

layout(rgba16f, binding = 0) uniform image2D output_data_image;
layout(rgba16f, binding = 1) uniform image2D output_color_image;

layout(rgba32f, binding = 2) uniform image2D accum_1A_image;
layout(rgba32f, binding = 3) uniform image2D accum_1B_image;

layout(rgba32f, binding = 4) uniform image2D accum_2A_image;
layout(rgba32f, binding = 5) uniform image2D accum_2B_image;

layout(binding = 6) uniform sampler2D depth_image;
layout(binding = 7) uniform sampler2D extra_large_noise;
layout(binding = 8) uniform sampler3D large_noise;
layout(binding = 9) uniform sampler3D noise_medium;
layout(binding = 10) uniform sampler3D noise_small;
layout(binding = 11) uniform sampler3D curl_noise;
layout(binding = 12) uniform sampler3D dither_small;
layout(binding = 13) uniform sampler2D heightmask;

layout(binding = 14) uniform uniformBuffer {
	mat4 view;
	mat4 prevview;
	mat4 proj;
	mat4 prevproj;

	vec3 extralargenoiseposition;
	float extralargenoisescale;

	vec3 largenoiseposition;
	float cloud_lighting_sharpness;

	vec3 mediumnoiseposition;
	float lighting_step_distance;

	vec3 smallnoiseposition;
	float atmospheric_density;

	vec4 ambientLightColor;
	vec4 ambientGroundLightColor;
	vec4 ambientfogdistancecolor;
	
	float small_noise_scale;
	float min_step_distance;
	float max_step_distance;
	float lod_bias;

	float cloud_sharpness;
	float directionalLightsCount;
	float pointLightsCount;
	float anisotropy;

	float cloud_floor;
	float cloud_ceiling;
	float max_step_count;
	float max_lighting_step_count;

	float filterIndex;
	float blurPower;
	float blurQuality;
	float curlPower;

	vec2 WindDirection;
	vec2 reserved;
} genericData;

struct DirectionalLight {
	vec4 direction; //w = shadow sample count
	vec4 color; //a = intensity
};

struct PointLight {
	vec4 position; //w = radius
	vec4 color; //a = intensity
};

layout(binding = 15) uniform lightsBuffer {
	DirectionalLight directionalLights[4];
	PointLight pointLights[8];
};

// Our push constant
layout(push_constant, std430) uniform Params {
	vec2 raster_size;
	float large_noise_scale;
	float medium_noise_scale;

	float time;
	float cloud_coverage;
	float cloud_density;
	float small_noise_strength;

	float cloud_lighting_power;
	float accumilation_decay;
	vec2 cameraRotation;
} params;

const int BayerFilter16[16] =
{
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5
};
const int BayerFilter4[4] =
{
    0, 1,
    3, 2,
};

const mat4 bayer_matrix = mat4(
    vec4(00.0 / 16.0, 12.0 / 16.0, 03.0 / 16.0, 15.0 / 16.0),
    vec4(08.0 / 16.0, 04.0 / 16.0, 11.0 / 16.0, 07.0 / 16.0),
    vec4(02.0 / 16.0, 14.0 / 16.0, 01.0 / 16.0, 13.0 / 16.0),
    vec4(10.0 / 16.0, 06.0 / 16.0, 09.0 / 16.0, 05.0 / 16.0));

float quadraticOut(float t) {
  return -t * (t - 2.0);
}

float quadraticIn(float t) {
  return t * t;
}

float rand(vec2 co){
    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

float get_dither_value(vec2 pixel) {
    int x = int(pixel.x - 4.0 * floor(pixel.x / 4.0));
    int y = int(pixel.y - 4.0 * floor(pixel.y / 4.0));
    return bayer_matrix[x][y];
}

float remap(float value, float min1, float max1, float min2, float max2) {
  return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

void sampleAtmospherics(
	vec3 curPos, 
	float atmosphericHeight, 
	float distanceTraveled,
	float Rayleighscaleheight, 
	float Miescaleheight, 
	vec3 RayleighScatteringCoef, 
	float MieScatteringCoef, 
	float atmosphericDensity, 
	float density, 
	inout vec3 totalRlh, 
	inout vec3 totalMie, 
	inout float iOdRlh, 
	inout float iOdMie)
	{
	float iHeight = curPos.y / atmosphericHeight;
	float odStepRlh = exp(-iHeight / Rayleighscaleheight) * distanceTraveled;
	float odStepMie = exp(-iHeight / Miescaleheight) * distanceTraveled;
	iOdRlh += odStepRlh;
	iOdMie += odStepMie;

	vec3 attn = exp(-(MieScatteringCoef * (iOdMie + Miescaleheight) + RayleighScatteringCoef * (iOdRlh + Rayleighscaleheight))) * atmosphericDensity * (1.0 - clamp(iHeight, 0.0, 1.0));
	totalRlh += odStepRlh * attn * (1.0 - density);
	totalMie += odStepMie * attn * (1.0 - density);
}

// vec4 sampleAllAtmospherics(
// 	vec3 worldPos, 
// 	vec3 rayDirection,
// 	float linear_depth,
// 	float highestDensityDistance,
// 	float density,
// 	float stepDistance,
// 	float stepCount,
// 	float atmosphericDensity, 
// 	vec3 sunDirection, 
// 	vec3 sunlightColor, 
// 	vec3 ambientLight)
// 	{
// 	vec3 totalRlh = vec3(0,0,0);
//     vec3 totalMie = vec3(0,0,0);
// 	float iOdRlh = 0.0;
//     float iOdMie = 0.0;
// 	// float odStepRlh = 0.0;
// 	// float odStepMie = 0.0;

// 	const float atmosphericHeight = 40000.0;
// 	const vec3 RayleighScatteringCoef = vec3(5.5e-6, 13.0e-6, 22.4e-6);
// 	const float Rayleighscaleheight = 8e3;
// 	const float MieScatteringCoef = 21e-6;
// 	const float Miescaleheight = 1.2e3;
// 	const float MieprefferedDirection = 0.758;

// 	// Calculate the Rayleigh and Mie phases.
//     float mu = dot(rayDirection, sunDirection);
//     float mumu = mu * mu;
//     float gg = MieprefferedDirection * MieprefferedDirection;
//     float pRlh = 3.0 / (16.0 * PI) * (1.0 + mumu);
//     float pMie = 3.0 / (8.0 * PI) * ((1.0 - gg) * (mumu + 1.0)) / (pow(1.0 + gg - 2.0 * mu * MieprefferedDirection, 1.5) * (2.0 + gg));

// 	//Sample all atmospherics
// 	// if (density >= 1.0){
// 	// 	finaldepth = min(maxDistance, highestDensityDistance);
// 	// }
// 	//float stepCount = max(floor(maxDistance / stepDistance), 1.0);

// 	vec3 curPos = vec3(0.0);
// 	float traveledDistance = 0.0;
// 	//bool sampledDistanceAtmo = false;
// 	float currentWeight = 0.0;

// 	for (float i = 0.0; i < stepCount; i++) {
// 		traveledDistance = mix(stepDistance, stepDistance * stepCount, clamp(i / stepCount, 0.0, 1.0));
		
// 		// currentWeight = density * (1.0 - clamp((highestDensityDistance - traveledDistance) / stepDistance, 0.0, 1.0));

// 		if (traveledDistance > linear_depth || currentWeight >= 1.0){
// 			//traveledDistance = traveledDistance - stepDistance;
// 			//currentWeight = 1.0 - clamp((linear_depth - traveledDistance) / stepDistance, 0.0, 1.0);
// 			//sampleAtmospherics(curPos, atmosphericHeight, stepDistance, Rayleighscaleheight, Miescaleheight, RayleighScatteringCoef, MieScatteringCoef, atmosphericDensity, currentWeight, totalRlh, totalMie, iOdRlh, iOdMie); 
// 			break;
// 		}
		
		
// 		curPos = worldPos + rayDirection * traveledDistance;
		
// 		sampleAtmospherics(curPos, atmosphericHeight, stepDistance, Rayleighscaleheight, Miescaleheight, RayleighScatteringCoef, MieScatteringCoef, atmosphericDensity, currentWeight, totalRlh, totalMie, iOdRlh, iOdMie); 
// 	}

// 	// pRlh *= (1.0 - lightingWeight);
// 	// pMie *= (1.0 - lightingWeight);

// 	float AtmosphericsDistancePower = length(vec3(RayleighScatteringCoef * totalRlh + MieScatteringCoef * totalMie));
// 	vec3 atmospherics = 22.0 * (ambientLight * RayleighScatteringCoef * totalRlh + pMie * MieScatteringCoef * sunlightColor * totalMie);
// 	return vec4(atmospherics, AtmosphericsDistancePower);
// }

float sampleScene(
	vec3 extralargeNoisePos,
	vec3 largeNoisePos, 
	vec3 mediumNoisePos, 
	vec3 smallNoisePos, 
	vec3 worldPosition, 
	float cloudceiling, 
	float cloudfloor, 
	float extralargenoisescale,
	float largenoisescale, 
	float mediumnoisescale, 
	float smallnoisescale, 
	float coverage, 
	float smallscalePower, 
	float curlPower, 
	float lod, 
	bool ambientsample)
	{
	float clampedWorldHeight = remap(worldPosition.y, cloudfloor, cloudceiling, 0.0, 1.0);
	vec4 gradientSample = texture(heightmask, vec2(clampedWorldHeight, 0.5)).rgba;
	

	float edgeFade = min(smoothstep(0.0, 0.1, clampedWorldHeight), smoothstep(1.0, 0.9, clampedWorldHeight));

	float extraLargeShape = texture(extra_large_noise, (worldPosition.xz - extralargeNoisePos.xz) / extralargenoisescale).r;
	extraLargeShape = smoothstep(coverage + 0.2 , coverage - 0.2, extraLargeShape * (1.0 - gradientSample.b));
	float smallShape = texture(noise_small, (worldPosition - smallNoisePos) / smallnoisescale).r;
	
	//vec4 PackedNoise = texture(large_noise, (worldPosition - largeNoisePos) / largenoisescale);
	// float whispies = mix(PackedNoise.r, PackedNoise.g, gradientSample.b);
	// float billowyGradient = pow(gradientSample.b, 0.25);
	// float billowynoise = mix(PackedNoise.b * 0.3, PackedNoise.a * 0.3, billowyGradient);

	// float noiseComposite = mix(whispies, billowynoise, clampedWorldHeight);

	if (!ambientsample && min(curlPower, lod) > 0.5){
		vec2 WindDirection = genericData.WindDirection;
		float curlLod = remap(lod, 0.5, 1.0, 0.0, 1.0);
		worldPosition += (((texture(curl_noise, (worldPosition - smallNoisePos) / smallnoisescale / 4.0).xyz * 2.0) - 1.0) * vec3(1.0, 0.2, 1.0) + vec3(WindDirection.x, 0.0, WindDirection.y)) * curlPower * (1.0 - gradientSample.a) * curlLod;
		worldPosition += (((texture(curl_noise, (worldPosition - smallNoisePos) / smallnoisescale / 4.0).xyz * 2.0) - 1.0) * vec3(1.0, 0.2, 1.0) + vec3(WindDirection.x, 0.0, WindDirection.y)) * curlPower * (1.0 - gradientSample.a) * curlLod;
		worldPosition += (((texture(curl_noise, (worldPosition - smallNoisePos) / smallnoisescale / 4.0).xyz * 2.0) - 1.0) * vec3(1.0, 0.2, 1.0) + vec3(WindDirection.x, 0.0, WindDirection.y)) * curlPower * (1.0 - gradientSample.a) * curlLod;
		
		clampedWorldHeight = remap(worldPosition.y, cloudfloor, cloudceiling, 0.0, 1.0);
		gradientSample = texture(heightmask, vec2(clampedWorldHeight, 0.5)).rgba;
		//float gradientResult = gradientSample.r;
	}
	//gradientSample.b = mix(1.0, gradientSample.b, float(ambientsample));
	float largeShape = texture(large_noise, (worldPosition - largeNoisePos) / largenoisescale).r;
	largeShape = smoothstep(coverage , coverage - 0.1, 1.0 - (largeShape * gradientSample.r * extraLargeShape));
	float mediumshape = texture(noise_medium, (worldPosition - mediumNoisePos) / mediumnoisescale).r;
	smallShape = smallShape * gradientSample.g * pow((1.0 - mediumshape), smallscalePower);


	float shape = mediumshape;
	shape = clamp(remap(shape, 1.0 - largeShape, 1.0, 0.0, 1.0), 0.0, 1.0);
	shape = clamp(remap(shape, smallShape, 1.0, 0.0, 1.0), 0.0, 1.0);

	return shape * edgeFade;
}

float BeersLaw (float dist, float absorption) {
  return exp(-dist * absorption);
}

float Powder (float dist, float absorption) {
  return 1.0 - exp(-dist * absorption * 2.0);
}

float HenyeyGreenstein(float g, float costh)
{
    return (1.0 - g * g) / (4.0 * PI * pow(1.0 + g * g - 2.0 * g * costh, 3.0/2.0));
}

float sampleLighting(
	int stepCount, 
	vec3 worldPosition,
	vec3 extralargeNoisePos, 
	vec3 largeNoisePos, 
	vec3 mediumNoisePos, 
	vec3 smallNoisePos, 
	vec3 sunDirection,
	float densityMultiplier,
	float sunUpWeight, 
	float stepDistance,  
	float cloudceiling, 
	float cloudfloor, 
	float extralargenoisescale,
	float largenoisescale, 
	float mediumnoisescale, 
	float smallnoisescale, 
	float coverage, 
	float smallscalePower, 
	float curlPower, 
	float lod)
	{
	float density = 0.0;
	float stepCountFloat = max(float(stepCount) * lod, 2.0);
	//float difference = float(stepCount) / stepCountFloat;
	float eachShortStep = stepDistance / (float(stepCount) / stepCountFloat) / stepCountFloat;
	//float eachLongStep = eachShortStep * 4.0;
	float traveledDistance = 0.0;
	//float totalDistance = stepDistance / difference;
	
	float sunUpValue = 1.0 - sunUpWeight;
	float eachStepWeight = 1.0 / stepCountFloat;

	float heightGradient = 0.0;
	float thisDensity = 0.0;
	float count = 0.0;
	vec3 curPos = worldPosition;
	for (float i = 0.0; i < stepCountFloat; i++) {
		traveledDistance = mix(eachShortStep, stepDistance, clamp(quadraticOut(i / stepCountFloat), 0.0, 1.0));
		curPos = worldPosition + sunDirection * traveledDistance;

		if (density < 1.0 && clamp(curPos.y, cloudfloor, cloudceiling) == curPos.y){
			heightGradient = remap(curPos.y, cloudfloor, cloudceiling, 0.0, 1.0);
			
			heightGradient = clamp(smoothstep(sunUpValue - 0.1, sunUpValue, heightGradient), 0.0, 1.0);


			thisDensity = sampleScene(extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, curPos, cloudceiling, cloudfloor, extralargenoisescale, largenoisescale, mediumnoisescale, smallnoisescale, coverage, smallscalePower, curlPower, lod, true) * densityMultiplier * eachStepWeight;
			density += mix(1.0, thisDensity, heightGradient);
			//count += 1.0;
			//density += thisDensity;
		}
		else{
			break;
		}
	}
	//density /= count;
	//float transmittance = BeersLaw(density, ABSORPTION_COEFFICIENT);
	return density;
}

float sampleAO(
	vec3 extralargeNoisePos,
	vec3 largeNoisePos, 
	vec3 mediumNoisePos, 
	vec3 smallNoisePos, 
	vec3 worldPosition, 
	float lightingSampleRange, 
	float cloudceiling, 
	float cloudfloor,
	float extralargenoisescale,
	float largenoisescale, 
	float mediumnoisescale, 
	float smallnoisescale, 
	float coverage, 
	float smallscalePower, 
	float curlPower, 
	float lod)
	{
	vec3 samplePos = worldPosition;
	samplePos.y += lightingSampleRange * 0.5;
	samplePos.y += lightingSampleRange * (rand(samplePos.xz) * 2.0 - 1.0);
	samplePos.x += lightingSampleRange * (rand(samplePos.zy) * 2.0 - 1.0);
	samplePos.z += lightingSampleRange * (rand(samplePos.yx) * 2.0 - 1.0);

	return sampleScene(extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, samplePos, cloudceiling, cloudfloor, extralargenoisescale, largenoisescale, mediumnoisescale, smallnoisescale, coverage, smallscalePower, curlPower, lod, true);
}

bool renderBayer(ivec2 fragCoord, int framecount)
{
	//int BAYER = 16;
    //int index = framecount % BAYER;
    
    return (fragCoord.x + 4 * fragCoord.y) % 16 == BayerFilter16[framecount];
}

// The code we want to execute in each invocation
void main() {
	ivec2 uv = ivec2(gl_GlobalInvocationID.xy);
	ivec2 size = ivec2(params.raster_size);

	// Prevent reading/writing out of bounds.
	if (uv.x >= size.x || uv.y >= size.y) {
		return;
	}

	// vec3 sphereCenter = vec3(0.0);
	// float sphereRadius = 10.0;
	
	vec2 depthUV = vec2(float(uv.x) / float(size.x), float(uv.y) / float(size.y));
	float depth = texture(depth_image, depthUV).r;
	vec4 view = inverse(genericData.proj) * vec4(depthUV*2.0-1.0,depth,1.0);
	view.xyz /= view.w;
	float linear_depth = length(view); //used to calculate depth based on the view angle, idk just works.

	
	// Convert screen coordinates to normalized device coordinates
	vec2 clipUV = vec2(depthUV.x, depthUV.y);
	vec2 ndc = clipUV * 2.0 - 1.0;	
	// Convert NDC to view space coordinates
	vec4 clipPos = vec4(ndc, 0.0, 1.0);
	vec4 viewPos = inverse(genericData.proj) * clipPos;
	viewPos.xyz /= viewPos.w;
	
	vec3 rd_world = normalize(viewPos.xyz);
	rd_world = mat3(genericData.view) * rd_world;
	// Define the ray properties
	
	vec3 raydirection = normalize(rd_world);
	vec3 rayOrigin = genericData.view[3].xyz; //center of camera for the ray origin, not worried about the screen width playing in, as it's for clouds.

	// Read from our color buffer.
	//vec4 color = imageLoad(color_image, uv);
	vec4 aobase = genericData.ambientGroundLightColor;

	float density = 0.0;
	//float thisStepLightingWeight = 0.0;
	//float lightingWeight = 0.0;
	int lightingSamples = 0;
	float newdensity = 0.0;
	vec3 curPos = rayOrigin;

	vec3 extralargeNoisePos = genericData.extralargenoiseposition;
	vec3 largeNoisePos = genericData.largenoiseposition;
	vec3 mediumNoisePos = genericData.mediumnoiseposition;
	vec3 smallNoisePos = genericData.smallnoiseposition;

	float extralargenoiseScale = genericData.extralargenoisescale;
	float largenoiseScale = params.large_noise_scale;
	float mediumnoiseScale = params.medium_noise_scale;
	float smallnoiseScale = genericData.small_noise_scale;

	float minstep = genericData.min_step_distance;
	float maxstep = genericData.max_step_distance;
	//float stepbias = genericData.step_density_bias;
	float densityMultiplier = params.cloud_density;
	float sharpness = clamp(1.0 - genericData.cloud_sharpness, 0.001, 1.0) * 2.0;
	float lightingSharpness = genericData.cloud_lighting_sharpness;
	float smallNoiseMultiplier = params.small_noise_strength;
	float coverage = params.cloud_coverage * 1.1;
	float lightingdensityMultiplier = params.cloud_lighting_power;
	lightingdensityMultiplier += lightingdensityMultiplier * 3.0 * coverage;

	float cloudfloor = genericData.cloud_floor;
	float cloudceiling = genericData.cloud_ceiling;

	//vec3 sundir = genericData.directionalLightDir;


	//dither
	float ditherScale = 40.037;

	
	vec3 ditherUV = vec3(depthUV.x * ditherScale , depthUV.y * ditherScale , params.time);
	float smallNoise = texture(dither_small, ditherUV).r;

	float ditherValue = smallNoise;
	float newStep = maxstep * ditherValue;
	float traveledDistance = newStep;
	

	int stepCount = int(genericData.max_step_count);
	int lightingStepCount = int(genericData.max_lighting_step_count);
	//float lightingStepDistance = (cloudceiling - cloudfloor) / genericData.max_lighting_step_count;
	//bool depthbreak = false;
	float ambient = 0.0;
	float maxTheoreticalStep = float(stepCount) * maxstep;
	float ceilingSample = 0.0;
	float halfcloudThickness = (cloudceiling - cloudfloor) * 0.5;
	float halfCeiling = cloudceiling - halfcloudThickness;

	float lightingStepDistance = genericData.lighting_step_distance;
	
	//atmospherics
	vec3 ambientfogdistancecolor = genericData.ambientfogdistancecolor.rgb;
	//vec3 attn = vec3(0.0);
	// vec3 lasttotalRlh = vec3(0,0,0);
    // vec3 lasttotalMie = vec3(0,0,0);
	vec3 totalRlh = vec3(0,0,0);
    vec3 totalMie = vec3(0,0,0);
	float iOdRlh = 0.0;
    float iOdMie = 0.0;
	float atmosphericDensity = genericData.atmospheric_density;

	const float atmosphericHeight = 40000.0;
	const vec3 RayleighScatteringCoef = vec3(5.5e-6, 13.0e-6, 22.4e-6);
	const float Rayleighscaleheight = 8e3;
	const float MieScatteringCoef = 21e-6;
	const float Miescaleheight = 1.2e3;
	const float MieprefferedDirection = 0.758;

	bool densityBreak = false;
	bool depthBreak = false;
	//float depthlerp = 1.0;
	float highestDensity = 0.0;
	float highestDensityDistance = maxTheoreticalStep;
	float depthFade = 1.0;
	float curlPower = genericData.curlPower;

	
	//newdensity = pow(sampleScene(largeNoisePos, mediumNoisePos, smallNoisePos, curPos, ceilingSample, cloudfloor, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier) * densityMultiplier, sharpness);
	bool debugCollisions = false;
	int frameIndex = int(genericData.filterIndex);
	
	bool override = false;
	vec4 currentColorAccumilation = vec4(0.0);
	vec4 currentDataAccumilation = vec4(0.0);
	//bool rebuildFrame = renderBayer(uv, frameIndex);
	//override = rebuildFrame;
	//debugCollisions = rebuildFrame;
	bool rebuildFrame = true;
	
	if (!rebuildFrame){
		//accumulation preperation:
		vec4 niaveDataRetreval = vec4(0.0);
		float usingaccumA = params.cameraRotation.x;
		if (usingaccumA > 0.0){
			niaveDataRetreval = imageLoad(accum_2A_image, uv).rgba;
		}
		else{
			niaveDataRetreval = imageLoad(accum_2B_image, uv).rgba;
		}
		//depthBreak = abs(niaveDataRetreval.b) > linear_depth;

		vec3 worldFinalPos = curPos + raydirection * niaveDataRetreval.b;
		worldFinalPos += (rayOrigin - genericData.prevview[3].xyz);
		//Prevview is already actually the inv_view (due to the way retrieving the transform works), so inversing it here is making it the equalivant of View_Matrix.
		vec4 reprojectedClipPos = inverse(genericData.prevview) * vec4(worldFinalPos, 1.0);
		
		
		if (reprojectedClipPos.z > 0.0){
			override = true;
		}
		else{
			vec4 reprojectedScreenPos = genericData.prevproj * reprojectedClipPos;
			
			// Convert clip space to normalized device coordinates
			ndc = (reprojectedScreenPos.xy / reprojectedScreenPos.w);

			// Convert normalized device coordinates to screen space
			vec2 screen_position = ndc * 0.5 + 0.5;
			//screen_position = clamp(screen_position, vec2(0.0), vec2(1.0));
			screen_position = screen_position - depthUV;
			ivec2 adjustedUV = ivec2(int(screen_position.x * size.x), int(screen_position.y * size.y));
			//float change = length(vec2(adjustedUV));
			adjustedUV += uv; //Size is the screen resolution.
			
			ivec2 clampedUV = clamp(adjustedUV, ivec2(0), size - ivec2(1)); //having two lets me check if clamping it changed the reprojected uv, if it did that means it was offscreen, so rebuild data.

			//execute accumilation.
			float accumdecay = params.accumilation_decay;

			//alternate back and forth to avoid stepping on pixels being written too.
			
			float actualDepth = abs(reprojectedClipPos.z);

			//currentAccumilation = niaveDataRetreval;
			
			if (usingaccumA > 0.0){
				currentDataAccumilation = imageLoad(accum_2A_image, adjustedUV).rgba;
				bool lastDepthBreak = currentDataAccumilation.b < 0.0;
				float sampledDepth = abs(currentDataAccumilation.b);
				depthBreak = actualDepth > sampledDepth;
				if (clampedUV != adjustedUV || depthBreak != lastDepthBreak){
					override = true;
					//debugCollisions = true;
				}
				else{
					imageStore(accum_1B_image, uv, imageLoad(accum_1A_image, adjustedUV));
					imageStore(accum_2B_image, uv, currentDataAccumilation);
				}
				
			}
			else{
				currentDataAccumilation = imageLoad(accum_2B_image, adjustedUV).rgba;
				bool lastDepthBreak = currentDataAccumilation.b < 0.0;
				float sampledDepth = abs(currentDataAccumilation.b);
				depthBreak = actualDepth > sampledDepth;
				if (clampedUV != adjustedUV || depthBreak != lastDepthBreak){
					override = true;
					//debugCollisions = true;
				}
				else{
					imageStore(accum_1A_image, uv, imageLoad(accum_1B_image, adjustedUV));
					imageStore(accum_2A_image, uv, currentDataAccumilation);

				}
			}
		}

	}
	
	
	if (rebuildFrame || override){
		//If it is our render, build the data for this pixel
		//float averageLength = 0.0;
		curPos = rayOrigin;
		ceilingSample = cloudceiling;
		// && max(raydirection.y, cloudfloor - curPos.y) > 0.0
		// if (clamp(curPos.y, cloudfloor, ceilingSample) == curPos.y){
		// 	newdensity = pow(sampleScene(extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, curPos, ceilingSample, cloudfloor, extralargenoiseScale, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier, curlPower, 1.0, false) * densityMultiplier, sharpness);
		// 	density += newdensity;
		// }
		float averageCount = 0.0;
		float lodMaxDistance = maxstep * float(stepCount) * genericData.lod_bias;
		float curLod = 1.0;
		//vec3 ambientLight = genericData.ambientLightColor.rgb;
		vec3 sundir = vec3(0.0);
		float sunUpWeight = 0.0;

		
		int directionalLightCount = int(genericData.directionalLightsCount);
		int pointLightCount = int(genericData.pointLightsCount);
		int thislightingStepCount = 0;
		vec3 directionalLightSunUpPower[4] = vec3[4](vec3(0.0), vec3(0.0), vec3(0.0), vec3(0.0));
		//vec4 directionalLightsPower[4] = vec4[4](vec4(0.0), vec4(0.0), vec4(0.0), vec4(0.0));
		bool validLight = false;
		float totalLightPower = 0.0;
		// for (int lightI = 0; lightI < directionalLightCount; lightI++){
		// 	if (directionalLights[lightI].color.a > 0.0){
		// 		validLight = true;
		// 		totalLightPower += directionalLights[lightI].color.a;
		// 	}
		// }
		for (int lightI = 0; lightI < directionalLightCount; lightI++){
			if (directionalLights[lightI].color.a > 0.0){
				validLight = true;
				
				// / totalLightPower
				directionalLightSunUpPower[lightI].r = smoothstep(-0.2, 0.2, dot(directionalLights[lightI].direction.xyz, vec3(0.0, 1.0, 0.0)));
				totalLightPower += directionalLights[lightI].color.a * directionalLightSunUpPower[lightI].r;

				directionalLightSunUpPower[lightI].b = dot(directionalLights[lightI].direction.xyz, raydirection);
				// float mu = dot(raydirection, directionalLights[lightI].direction.xyz);
				// float mumu = mu * mu;
				// float gg = MieprefferedDirection * MieprefferedDirection;
				// float pRlh = 3.0 / (16.0 * PI) * (1.0 + mumu);
				// float pMie = 3.0 / (8.0 * PI) * ((1.0 - gg) * (mumu + 1.0)) / (pow(1.0 + gg - 2.0 * mu * MieprefferedDirection, 1.5) * (2.0 + gg));
				// directionalLightSunUpPower[lightI].g = pRlh;
				// directionalLightSunUpPower[lightI].b = pMie;
			}
		}
		vec4 lightColor = vec4(0.0);
		float initialdistanceSample = -1.0;
		float anisotropy = genericData.anisotropy;
		float thisAmbientOverride = 0.0;
		//float thisStepLightingWeightUnclamped = 0.0;

		//float averageStepLength = 0.0;
		//float averageStepLengthCount = 0.0;
		float stepCountFloat = float(stepCount);
		for (int i = 0; i < stepCount; i++) {
			averageCount += 1.0;
			
			if (traveledDistance > linear_depth){
				depthFade = 1.0 - smoothstep(linear_depth - newStep, linear_depth, traveledDistance);
				depthBreak = true;
				//highestDensityDistance = linear_depth;
				//traveledDistance = linear_depth;
			}
			
			curPos = rayOrigin + raydirection * traveledDistance;
			
			sampleAtmospherics(curPos, atmosphericHeight, newStep , Rayleighscaleheight, Miescaleheight, RayleighScatteringCoef, MieScatteringCoef, atmosphericDensity, newdensity, totalRlh, totalMie, iOdRlh, iOdMie); 
			ceilingSample = halfCeiling + (texture(large_noise, (curPos - largeNoisePos) / largenoiseScale).r) * halfcloudThickness;
			if (clamp(curPos.y, cloudfloor, cloudceiling) == curPos.y){
				curLod = 1.0 - clamp(traveledDistance / lodMaxDistance, 0.0, 1.0);
				newdensity = pow(sampleScene(extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, curPos, ceilingSample, cloudfloor, extralargenoiseScale, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier, curlPower, curLod, false) * densityMultiplier, sharpness) * depthFade;
				
				
				if (newdensity > 0.0){
					if (initialdistanceSample < 0.0){
						initialdistanceSample = traveledDistance;
					}

					lightingSamples += 1;
					for (int lightI = 0; lightI < directionalLightCount; lightI++){
						if (directionalLights[lightI].color.a > 0.0){
							
							sundir = directionalLights[lightI].direction.xyz;
							sunUpWeight = directionalLightSunUpPower[lightI].r;

							thislightingStepCount = min(int(directionalLights[lightI].direction.w), lightingStepCount);
							if (thislightingStepCount > 0){
								float henyeygreenstein = HenyeyGreenstein(anisotropy, directionalLightSunUpPower[lightI].b);
								float densitySample = sampleLighting(thislightingStepCount, curPos, extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, sundir, densityMultiplier * lightingdensityMultiplier, sunUpWeight, lightingStepDistance, ceilingSample, cloudfloor, extralargenoiseScale, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier, curlPower, curLod);
								densitySample = BeersLaw(lightingStepDistance, densitySample * henyeygreenstein);
								//densitySample = Powder(lightingStepDistance, densitySample * henyeygreenstein);
								float thisStepLightingWeight = (clamp(pow(densitySample, lightingSharpness), 0.0, 1.0)) * sunUpWeight;
								

								lightColor.rgb += pow(directionalLights[lightI].color.rgb * directionalLights[lightI].color.a * thisStepLightingWeight, vec3(2.2));
								directionalLightSunUpPower[lightI].g += directionalLights[lightI].color.a * thisStepLightingWeight;
							}
							else{
								lightColor.rgb += pow(directionalLights[lightI].color.rgb * directionalLights[lightI].color.a * sunUpWeight, vec3(2.2));
								directionalLightSunUpPower[lightI].g += directionalLights[lightI].color.a * sunUpWeight;
							}

							
						}
					}
					thisAmbientOverride = 1.0;
					for (int lightI = 0; lightI < pointLightCount; lightI++){
						vec3 lightToOriginDelta = pointLights[lightI].position.xyz - curPos;
						float lightDistanceWeight = length(lightToOriginDelta); 
						if (pointLights[lightI].color.a > 0.0 && lightDistanceWeight < pointLights[lightI].position.w){
							lightToOriginDelta = normalize(lightToOriginDelta);
							float densitySample = sampleLighting(3, curPos, extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, lightToOriginDelta, densityMultiplier, sunUpWeight, min(maxstep, lightDistanceWeight), ceilingSample, cloudfloor, extralargenoiseScale, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier, curlPower, curLod);
							
							float henyeygreenstein = HenyeyGreenstein(anisotropy, dot(directionalLights[lightI].direction.xyz, raydirection));
							densitySample = BeersLaw(lightDistanceWeight, densitySample * henyeygreenstein);
							densitySample = mix(densitySample, newdensity, 0.3);
							lightDistanceWeight = lightDistanceWeight / pointLights[lightI].position.w;
							lightDistanceWeight = pointLights[lightI].color.a * pow((1.0 - lightDistanceWeight), 2.2) * densitySample;
							thisAmbientOverride -= lightDistanceWeight;
							

							lightColor.rgb += pow(pointLights[lightI].color.rgb * lightDistanceWeight, vec3(2.2));
						}
					}
					
					if (aobase.a > 0.0){
						thisAmbientOverride = max(thisAmbientOverride, 0.0);
						ambient += sampleScene(extralargeNoisePos, largeNoisePos, mediumNoisePos, smallNoisePos, curPos + vec3(0.0, 1.0, 0.0) * minstep, ceilingSample, cloudfloor, extralargenoiseScale, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier, curlPower, curLod, true) * densityMultiplier * lightingdensityMultiplier * thisAmbientOverride;
						//ambient += lastAmbient;
					}

					
					//ambient += sampleAO(largeNoisePos, mediumNoisePos, smallNoisePos, curPos, aoBokeh, ceilingSample, cloudfloor, largenoiseScale, mediumnoiseScale, smallnoiseScale, coverage, smallNoiseMultiplier) * densityMultiplier * lightingdensityMultiplier;
					newStep = mix(mix(maxstep, minstep, pow(newdensity, 0.1)), maxstep, float(i) / stepCountFloat);
					//averageStepLength += mix(mix(1.0, 0.0, pow(newdensity, 0.1)), 1.0, float(i) / stepCountFloat);
					//newStep = mix(minstep, maxstep, clamp(quadraticIn(float(i) / stepCountFloat), 0.0, 1.0));
					if (newdensity > highestDensity){
						highestDensity = newdensity;
						highestDensityDistance = traveledDistance;
					}
				}
				else{
					newStep = maxstep;
					//averageStepLength += 1.0;
				}

				//averageLength += mix(0.0, 1.0, newdensity);
				//averageCount += 1.0;
				if (i == 0){
					newdensity = mix(newdensity, 0.0, clamp(traveledDistance / maxstep, 0.0, 1.0));
				}

				density += newdensity;
				if (density >= 1.0){
					densityBreak = true;
					break;
				}
			}
			else{
				if (min(curPos.y - cloudceiling, raydirection.y) > 0.0 || max(curPos.y - cloudfloor, raydirection.y) < 0.0){
					
					//float finaltraveledDistance = min(maxTheoreticalStep, linear_depth);
					//sampleAtmospherics(curPos, atmosphericHeight, finaltraveledDistance - traveledDistance, Rayleighscaleheight, Miescaleheight, RayleighScatteringCoef, MieScatteringCoef, atmosphericDensity, newdensity, totalRlh, totalMie, iOdRlh, iOdMie); 
					
					traveledDistance = min(maxTheoreticalStep, linear_depth);
					curPos = rayOrigin + raydirection * traveledDistance;
					
					debugCollisions = true;
					break;
				}
				
				newStep = maxstep;
				//averageStepLength += 1.0;
				//averageLength += 0.0;
				//averageCount += 1.0;
			}
			
			//averageStepLengthCount += 1.0;

			if (depthBreak){
				break;
			}
			traveledDistance += newStep;

			// if (raydirection.y > 0.0 && curPos.y > cloudceiling){
			// 	debugCollisions = true;
			// 	break;
			// }
		}
		//averageStepLength = averageStepLength / max(averageStepLengthCount, 1.0);
		//averageStepLengthCount = averageStepLengthCount / stepCountFloat;
		ambient = clamp(ambient / float(lightingSamples), 0.0, 1.0);
		vec3 ambientLight = mix(genericData.ambientLightColor.rgb, aobase.rgb, clamp(ambient * aobase.a, 0.0, 1.0)) * clamp(totalLightPower, 0.0, 1.0);
		lightColor.rgb = ambientLight + clamp(lightColor.rgb / float(lightingSamples), vec3(0.0), vec3(2.0));
		lightColor.a = density;


		vec3 ambientfogdistancecolor = genericData.ambientfogdistancecolor.rgb;
		for (int lightI = 0; lightI < directionalLightCount; lightI++){
			if (directionalLights[lightI].color.a > 0.0){
				float sunAOPower = clamp(directionalLightSunUpPower[lightI].g / lightingSamples, 0.0, 1.0);
				float mu = dot(raydirection, directionalLights[lightI].direction.xyz);
				

				float mumu = mu * mu;
				float gg = MieprefferedDirection * MieprefferedDirection;
				float pRlh = 3.0 / (16.0 * PI) * (1.0 + mumu);
				float pMie = 3.0 / (8.0 * PI) * ((1.0 - gg) * (mumu + 1.0)) / (pow(1.0 + gg - 2.0 * mu * MieprefferedDirection, 1.5) * (2.0 + gg));

				float AtmosphericsDistancePower = length(vec3(RayleighScatteringCoef * totalRlh + MieScatteringCoef * totalMie));
				vec3 atmospherics = 22.0 * (ambientfogdistancecolor * RayleighScatteringCoef * totalRlh + pMie * MieScatteringCoef * directionalLights[lightI].color.rgb * sunAOPower * totalMie);

				lightColor.rgb = mix(lightColor.rgb, atmospherics, AtmosphericsDistancePower / directionalLightCount); //causes jitter in the sky
				
				//lightColor.a += AtmosphericsDistancePower;
			}
		}
		initialdistanceSample = max(initialdistanceSample, 0.0);
		// vec3 ambientfogdistancecolor = genericData.ambientfogdistancecolor.rgb;
		
		// float atmosphericDensity = genericData.atmospheric_density;
		// float heightSample = smoothstep(cloudfloor, cloudceiling, rayOrigin.y) * 0.6;
		// float upVec = clamp((1.0 - clamp(dot(raydirection, vec3(0.0, 1.0, 0.0)), 0.0, 1.0) + heightSample) * atmosphericDensity, 0.0, 1.0) * (traveledDistance / linear_depth);

		
		// lightColor.rgb = mix(lightColor.rgb, ambientfogdistancecolor, upVec);
		// lightColor.a += upVec;
		// if (directionalLightCount > 0.0){
		// 	for (float i = 0.0; i < directionalLightCount; i++){
		// 		DirectionalLight light = directionalLights[int(i)];
		// 		vec3 sundir = light.direction.xyz;
		// 		//sampleColor = sundir;
		// 		float sunUpWeight = smoothstep(0.0, 0.4, dot(sundir, vec3(0.0, 1.0, 0.0)));
		// 		float lightPower = light.color.a * sunUpWeight;
		// 		if (lightPower > 0.0){
		// 			vec4 atmosphericData = sampleAllAtmospherics(rayOrigin, raydirection, maxTheoreticalStep, highestDensityDistance, density, (maxTheoreticalStep / 10.0) - 10000.0, 10.0, atmosphericDensity, sundir, light.color.rgb * lightPower, ambientfogdistancecolor);
		// 			lightColor.rgb = mix(lightColor.rgb, atmosphericData.rgb, (atmosphericData.a * lightPower)); //causes jitter in the sky
		// 			lightColor.a += (atmosphericData.a * lightPower);
		// 		}
		// 	}
		// }
	

		//accumulation preperation:
		float finalDensityDistance = min(traveledDistance, highestDensityDistance);
		vec3 worldFinalPos = rayOrigin + raydirection * highestDensityDistance;
		// worldFinalPos.x += ((rand(worldFinalPos.yz) * 2.0) - 1.0) * minstep;
		// worldFinalPos.y += ((rand(worldFinalPos.xz) * 2.0) - 1.0) * minstep;
		// worldFinalPos.z += ((rand(worldFinalPos.yx) * 2.0) - 1.0) * minstep;
		vec3 delta = rayOrigin - genericData.prevview[3].xyz;
		//delta.y = -delta.y;
		worldFinalPos += delta;

		//Prevview is already actually the inv_view (due to the way retrieving the transform works), so inversing it here is making it the equalivant of View_Matrix.
		vec4 reprojectedClipPos = inverse(genericData.prevview) * vec4(worldFinalPos, 1.0);
		
		reprojectedClipPos.z -= 0.01;
		if (reprojectedClipPos.z > 0.0){
			override = true;
		}
		
		vec4 reprojectedScreenPos = genericData.prevproj * reprojectedClipPos;

		// Convert clip space to normalized device coordinates
		ndc = (reprojectedScreenPos.xy / reprojectedScreenPos.w);

		// Convert normalized device coordinates to screen space
		vec2 screen_position = ndc * 0.5 + 0.5;
		//screen_position = clamp(screen_position, vec2(0.0), vec2(1.0));
		screen_position = screen_position - depthUV;

		ivec2 adjustedUV = ivec2(int(screen_position.x * size.x), int(screen_position.y * size.y));
		//float change = length(vec2(adjustedUV));
		adjustedUV += uv; //Size is the screen resolution.
		
		ivec2 clampedUV = clamp(adjustedUV, ivec2(0), size - ivec2(1)); //having two lets me check if clamping it changed the reprojected uv, if it did that means it was offscreen, so rebuild data.

		//execute accumilation.
		float accumdecay = params.accumilation_decay;

		//alternate back and forth to avoid stepping on pixels being written too.
		float usingaccumA = params.cameraRotation.x;
		
		//float finalDensityDistance = max(traveledDistance, highestDensityDistance);
		//linear_depth = max(linear_depth, traveledDistance);
		float travelspeed = length(delta) + maxstep;
		//bool debugCollisions = false;
		if (usingaccumA > 0.0){
			currentColorAccumilation = imageLoad(accum_1A_image, adjustedUV).rgba;
			currentDataAccumilation = imageLoad(accum_2A_image, adjustedUV).rgba;

			bool lastDepthBreak = currentDataAccumilation.a < 0.0;

			if (override || clampedUV != adjustedUV || (depthBreak != lastDepthBreak && abs(linear_depth - currentDataAccumilation.r) > travelspeed)){
				currentColorAccumilation = lightColor;
				//debugCollisions = true;
				currentDataAccumilation.r = linear_depth;
				currentDataAccumilation.g = finalDensityDistance;
				currentDataAccumilation.b = initialdistanceSample;
			}
			else{
				currentColorAccumilation = (currentColorAccumilation * accumdecay) + lightColor * (1.0 - accumdecay);

				currentDataAccumilation.r = mix(currentDataAccumilation.r, linear_depth,  (1.0 - accumdecay));
				currentDataAccumilation.g = mix(currentDataAccumilation.g, finalDensityDistance,  (1.0 - accumdecay));
				currentDataAccumilation.b = mix(currentDataAccumilation.b, initialdistanceSample,  (1.0 - accumdecay));
			}

			if (depthBreak){
				currentDataAccumilation.a = -1.0;
			}
			else{
				currentDataAccumilation.a = 1.0;
			}

			imageStore(accum_1B_image, uv, currentColorAccumilation);
			imageStore(accum_2B_image, uv, currentDataAccumilation);
		}
		else{
			currentColorAccumilation = imageLoad(accum_1B_image, adjustedUV).rgba;
			currentDataAccumilation = imageLoad(accum_2B_image, adjustedUV).rgba;

			bool lastDepthBreak = currentDataAccumilation.a < 0.0;

			if (override || clampedUV != adjustedUV || (depthBreak != lastDepthBreak && abs(linear_depth - currentDataAccumilation.r) > travelspeed)){
				currentColorAccumilation = lightColor;
				//debugCollisions = true;
				currentDataAccumilation.r = linear_depth;
				currentDataAccumilation.g = finalDensityDistance;
				currentDataAccumilation.b = initialdistanceSample;
			}
			else{
				currentColorAccumilation = (currentColorAccumilation * accumdecay) + lightColor * (1.0 - accumdecay);

				currentDataAccumilation.r = mix(currentDataAccumilation.r, linear_depth,  (1.0 - accumdecay));
				currentDataAccumilation.g = mix(currentDataAccumilation.g, finalDensityDistance,  (1.0 - accumdecay));
				currentDataAccumilation.b = mix(currentDataAccumilation.b, initialdistanceSample,  (1.0 - accumdecay));
			}

			if (depthBreak){
				currentDataAccumilation.a = -1.0;
			}
			else{
				currentDataAccumilation.a = 1.0;
			}

			imageStore(accum_1A_image, uv, currentColorAccumilation);
			imageStore(accum_2A_image, uv, currentDataAccumilation);
		}
		//currentColorAccumilation = vec4(float(depthBreak), 0.0, 0.0, 1.0);
		//output Data
		// if (override){
		// 	currentColorAccumilation = vec4(1.0, 0.0, 0.0, 1.0);
		// }
		// if (debugCollisions){
		// 	currentColorAccumilation = vec4(1.0, 0.0, 0.0, 1.0);
		// }
		currentDataAccumilation.a = abs(currentDataAccumilation.a);

		//currentColorAccumilation = vec4(averageStepLength, averageStepLengthCount, float(debugCollisions), 1.0);
		imageStore(output_color_image, uv, currentColorAccumilation);
		//output LightColor
		imageStore(output_data_image, uv, currentDataAccumilation);
	}
	else{
		//If it is not our render, take the depth at this pixel on the previous frame, and assume it hasn't changed, then do reprojection and continue as is.
		

		// density = currentAccumilation.r;
		// lightingWeight = currentAccumilation.g;

		// vec4 sunlightColor = genericData.directionalLightColor;
		// vec3 resultingCloudColor = sunlightColor.rgb * sunlightColor.a;
		// vec3 ambientLight = genericData.ambientLightColor.rgb;
		
		// vec3 ambientColor = mix(ambientLight, aobase.rgb, ambient * aobase.a);
		// resultingCloudColor = mix(resultingCloudColor, ambientColor, lightingWeight);
		
		// // pRlh *= (1.0 - lightingWeight);
		// // pMie *= (1.0 - lightingWeight);

		// // float AtmosphericsDistancePower = length(vec3(RayleighScatteringCoef * totalRlh + MieScatteringCoef * totalMie));
		// // vec3 atmospherics = 22.0 * (ambientLight * RayleighScatteringCoef * totalRlh + pMie * MieScatteringCoef * sunlightColor.rgb * sunlightColor.a * totalMie);
		

		// float hardDensityCutoff = clamp(smoothstep(0.0, mix(maxstep, minstep, coverage), linear_depth), 0.0, 1.0);
		// density = mix(0.0, density, hardDensityCutoff); //hard distance fade.

		// vec4 color = imageLoad(color_image, uv);
		// color.rgb = mix(color.rgb, resultingCloudColor, density);
		// vec4 atmosphericData = sampleAllAtmospherics(rayOrigin, raydirection, currentAccumilation.a, maxstep, atmosphericDensity, 0.0, sundir, resultingCloudColor, ambientLight);
		// color.rgb = mix(color.rgb, atmosphericData.rgb, clamp(atmosphericData.a, 0.0, 1.0));
		// //color.rgb = mix(color.rgb, atmospherics, clamp(AtmosphericsDistancePower, 0.0, 1.0));

		// //color.rgb = vec3(1.0, 0.0, 0.0);
		// imageStore(color_image, uv, color);
		//currentAccumilation.b = abs(currentAccumilation.b);
		//imageStore(output_data_image, uv, currentAccumilation);
	}
	//float filterIndexTest = genericData.filterIndex;
	//imageStore(color_image, uv, vec4(filterIndexTest / 16.0, 0.0, 0.0, 1.0));
	// if (debugCollisions){
	// 	imageStore(color_image, uv, vec4(filterIndexTest / 16.0, 0.0, 0.0, 1.0));
	// }
	
}
