Shader "Hidden/VolumetricRendering"
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

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            fixed4 frag (v2f i) : SV_Target
            {
                float3 rayStart = _WorldSpaceCameraPos;
                float3 rayDir = i.viewDirWS;

                float nonlinear_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonlinear_depth);


                return float4(depth, depth, depth, 1.0);
            }
            ENDCG
        }
    }
}
