Shader "Hidden/VolumetricRendering"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Volume("Volume", 3D) = ""
        _offset("offset", float) = 0.3
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 viewDirWS : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 viewDirCS = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewDirWS = mul(unity_CameraToWorld, float4(viewDirCS, 0));
                return o;
            }

            Texture3D<float4> VolumeTex;
            SamplerState samplerVolumeTex;
            sampler3D _Volume;
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float4 phaseParams;

            float3 boundsMin = float3(-0.5, -0.5, -0.5);
            float3 boundsMax = float3(0.5, 0.5, 0.5);;
            float densityMultiplyer = 1;
            float _offset;
            float darknessThreshold = 0.15;
            float lightAbsorptionTowardSun = 1.21;
            float lightAbsorptionTowardCloud = 0.75;

            // Return (distance to box, distance inside box)
            // CASE: 0 <= dstA <= dstB -> ray intersects box from outside
            // CASE: dstA < 0 < dstB -> ray intersects box from inside
            // CASE : dstA > dstB -> no intersection
            bool rayBoxIntersect(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir, out float dstToBox, out float dstInsideBox)
            {
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                dstToBox = max(0, dstA);
                dstInsideBox = max(0, dstB - dstToBox);
                return !(dstA > dstB);
            }

            float sampleDensity(float3 rayPos)
            {
                // const int mipLevel = 0;
                // float3 size = boundsMax - boundsMin;
                // float3 uvw = (rayPos - boundsMin) / size;
                float3 uvw = (rayPos + 0.5);
                return tex3D(_Volume, uvw).r * densityMultiplyer;
                // return VolumeTex.SampleLevel(samplerVolumeTex, uvw, mipLevel).r * densityMultiplyer;
            }

            // Henyey-Greenstein
            float hg(float a, float g) {
                float g2 = g*g;
                return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
            }

            float phase(float a) {
                float blend = .5;
                float hgBlend = hg(a,phaseParams.x) * (1-blend) + hg(a,-phaseParams.y) * blend;
                return phaseParams.z + hgBlend*phaseParams.w;
            }

            float beer(float d) {
                float beer = exp(-d);
                return beer;
            }

            float lightmarch(float3 position)
            {
                float3 dirToLight = _WorldSpaceLightPos0.xyz;
                float disInsideBox, disToBox;
                rayBoxIntersect(boundsMin, boundsMax, position, 1/dirToLight, disToBox, disInsideBox);

                float stepSize = disInsideBox / 8;
                float totalDensity = 0;

                [unroll]
                for(int step = 0; step < 8; step++)
                {
                    position += dirToLight * stepSize;
                    totalDensity += max(0, sampleDensity(position) * stepSize);
                } 

                float transmittance = exp(-totalDensity * lightAbsorptionTowardSun);
                return darknessThreshold + transmittance * (1-darknessThreshold);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 bgColor = tex2D(_MainTex, i.uv);
                float3 rayStart = _WorldSpaceCameraPos;
                float3 rayDir = i.viewDirWS;

                float nonlinear_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonlinear_depth);

                
                float disToBox, disInsideBox;
                bool hit = rayBoxIntersect(boundsMin, boundsMax, rayStart, 1/rayDir, disToBox, disInsideBox);
                // if no intersection
                if(!hit)
                {
                    return bgColor;
                }

                float3 entryPoint = rayStart + rayDir * disToBox;

                float disTravelled = 0;
                float disLimit = min(depth-disToBox, disInsideBox);

                // Phase function makes clouds brighter
                float cosAngle = dot(rayDir, _WorldSpaceLightPos0.xyz);
                float phaseVal = phase(cosAngle);

                float transmittance = 1;
                float3 lightEnergy = 0;

                float density = 0;
                int ITER = 128;
                float stepSize = disLimit / ITER;
                [unroll]
                for(int i = 0; i < ITER; i++)
                {
                    float3 rayPos = entryPoint + rayDir * stepSize * i;
                    float density = sampleDensity(rayPos);
                    
                    if(density > 0)
                    {
                        float lightTransmittance = lightmarch(rayPos);
                        lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;
                        transmittance *= exp(-density * stepSize * lightAbsorptionTowardCloud);

                        if(transmittance < 0.01)
                        {
                            break;
                        }
                    }
                }

                float3 cloudColor = lightEnergy * float3(1,1,1);
                return float4(bgColor * transmittance + cloudColor, 0);
            }
            ENDCG
        }
    }
}
