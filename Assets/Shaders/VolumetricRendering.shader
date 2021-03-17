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

            float3 boundsMin;
            float3 boundsMax;
            float densityMultiplyer = 0.9;
            float _offset;

            // Return (distance to box, distance inside box)
            // CASE: 0 <= dstA <= dstB -> ray intersects box from outside
            // CASE: dstA < 0 < dstB -> ray intersects box from inside
            // CASE : dstA > dstB -> no intersection
            float2 rayBoxIntersect(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir)
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

            float sampleDensity(float3 rayPos)
            {
                const int mipLevel = 0;
                
                float3 size = boundsMax - boundsMin;
                float3 uvw = (rayPos - boundsMin) / 2;
                return VolumeTex.SampleLevel(samplerVolumeTex, uvw, mipLevel).r * densityMultiplyer;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 rayStart = _WorldSpaceCameraPos;
                float3 rayDir = i.viewDirWS;

                float nonlinear_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonlinear_depth);

                float2 rayToBox = rayBoxIntersect(boundsMin, boundsMax, rayStart, 1/rayDir);
                float disToBox = rayToBox.x;
                float disInsideBox = rayToBox.y;

                float3 entryPoint = rayStart + rayDir * disToBox;

                float currentDis = 0;
                float disLimit = min(depth-disToBox, disInsideBox);

                const float stepSize = 0.1;

                float transmittance = 1;
                float3 lightEnergy = 0;

                float3 color = tex2D(_MainTex, i.uv);

                float density = 0;

                while(currentDis < disLimit)
                {
                    float3 rayPos = entryPoint + rayDir * currentDis;
                    float d = sampleDensity(rayPos);
                    density += (1.0 - density) * d;
                    currentDis += stepSize;
                }
                // float3 rayPos = entryPoint + rayDir * _offset;
                // float3 size = boundsMax - boundsMin;
                // float3 uvw = (rayPos - boundsMin) / 2;
                // return float4(uvw, 1.0);
                return float4(lerp(color, float3(1,1,1), density), density);
            }
            ENDCG
        }
    }
}
