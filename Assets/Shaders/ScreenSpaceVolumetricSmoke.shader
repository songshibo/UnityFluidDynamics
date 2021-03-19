Shader "Hidden/SreenSpaceVolumetricSmoke"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // vertex input: position, UV
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };
            
            v2f vert (appdata v) {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }

            // Textures
            Texture3D<float4> NoiseTex;
            SamplerState samplerNoiseTex;

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            // Shape settings
            float densityMultiplier;
            float densityOffset;
            float smokeScale;
            float4 shapeNoiseWeights;
            float4 phaseParams;
            float scale = 1;

            // March settings
            int numStepsLight;

            float3 boundsMin;
            float3 boundsMax;

            // Light settings
            float lightAbsorptionTowardSun;
            float lightAbsorptionThroughCloud;
            float darknessThreshold;
            float4 _LightColor0;

            // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir)
            {
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            // Henyey-Greenstein
            float hg(float a, float g)
            {
                float g2 = g*g;
                return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
            }

            float phase(float a)
            {
                float blend = .5;
                float hgBlend = hg(a,phaseParams.x) * (1-blend) + hg(a,-phaseParams.y) * blend;
                return phaseParams.z + hgBlend*phaseParams.w;
            }

            float beer(float d)
            {
                float beer = exp(-d);
                return beer;
            }

            float sampleDensity(float3 rayPos)
            {
                // Calculate texture sample positions
                float3 size = boundsMax - boundsMin;
                float3 shapeSamplePos = rayPos + 0.5;// (rayPos - boundsMin) / size;

                // Calculate base shape density
                float4 shapeNoise = NoiseTex.SampleLevel(samplerNoiseTex, shapeSamplePos, 0);
                float4 normalizedShapeWeights = shapeNoiseWeights / dot(shapeNoiseWeights, 1);
                float shapeFBM = dot(shapeNoise, normalizedShapeWeights);
                float baseShapeDensity = shapeFBM + densityOffset;

                // Save sampling from detail tex if shape density <= 0
                if (baseShapeDensity > 0) {
                    return baseShapeDensity * densityMultiplier;
                }
                return 0;
            }

            // Calculate proportion of light that reaches the given point from the lightsource
            float lightmarch(float3 position) {
                float3 dirToLight = _WorldSpaceLightPos0.xyz;
                float dstInsideBox = rayBoxDst(boundsMin, boundsMax, position, 1/dirToLight).y;
                
                float stepSize = dstInsideBox/numStepsLight;
                float totalDensity = 0;

                for (int step = 0; step < numStepsLight; step ++) {
                    position += dirToLight * stepSize;
                    totalDensity += max(0, sampleDensity(position) * stepSize);
                }

                float transmittance = exp(-totalDensity * lightAbsorptionTowardSun);
                return darknessThreshold + transmittance * (1-darknessThreshold);
            }

            
            float4 frag (v2f i) : SV_Target
            {   
                // Create ray
                float3 rayPos = _WorldSpaceCameraPos;
                float3 rayDir = i.viewVector;
                
                // Depth and cloud container intersection info:
                float nonlin_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonlin_depth);
                float2 rayToContainerInfo = rayBoxDst(boundsMin, boundsMax, rayPos, 1/rayDir);
                float dstToBox = rayToContainerInfo.x;
                float dstInsideBox = rayToContainerInfo.y;

                // point of intersection with the cloud container
                float3 entryPoint = rayPos + rayDir * dstToBox;
                
                // Phase function makes clouds brighter around sun
                float cosAngle = dot(rayDir, _WorldSpaceLightPos0.xyz);
                float phaseVal = phase(cosAngle);

                float dstTravelled = 0;
                float dstLimit = min(depth-dstToBox, dstInsideBox);
                
                const float stepSize = 0.03;

                // March through volume:
                float transmittance = 1;
                float3 lightEnergy = 0;

                while (dstTravelled < dstLimit) {
                    rayPos = entryPoint + rayDir * dstTravelled;
                    float density = sampleDensity(rayPos);
                    
                    if (density > 0) {
                        float lightTransmittance = lightmarch(rayPos);
                        lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;
                        transmittance *= exp(-density * stepSize * lightAbsorptionThroughCloud);
                        
                        // Exit early if T is close to zero as further samples won't affect the result much
                        if (transmittance < 0.01) {
                            break;
                        }
                    }
                    dstTravelled += stepSize;
                }

                // Add clouds to background
                float3 backgroundCol = tex2D(_MainTex,i.uv);
                float3 cloudCol = lightEnergy * _LightColor0;
                float3 col = backgroundCol * transmittance + cloudCol;
                return float4(col,0);

            }

            ENDCG
        }
    }
}