Shader "Custom/VolumetricRenderer"
{
    Properties
    {
        _Volume("Volume", 3D) = ""
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    #define ITERATIONS 150

    sampler3D _Volume;

    struct Ray
    {
        float3 origin;
        float3 dir;
    };

    struct AABB
    {
        float3 vmin;
        float3 vmax;
    };

    bool intersect(Ray r, AABB aabb, out float t0, out float t1)
    {
        float3 invR = 1.0 / r.dir;
        
        float3 tbot = invR * (aabb.vmin - r.origin);// t0x, t0y, t0z minimum in x,y,z
        float3 ttop = invR * (aabb.vmax - r.origin);// t1x, t1y  t1z maximum in x,y,z
        
        // remaining minimum(maximum) x\y\z components
        float3 tmin = min(ttop, tbot); 
        float3 tmax = max(ttop, tbot);

        float2 t = max(tmin.xx, tmin.yz); // compare x to y and z to get biggest 2 componets out of 3
        t0 = max(t.x, t.y);// comparign left 2 components, remaining the max value for minimum intersection
        t = min(tmax.xx, tmax.yz);
        t1 = min(t.x, t.y);
        return t0 <= t1; // only if t0 <= t1 means intersection
    }

    float3 world2object(float3 p)
    {
        return mul(unity_WorldToObject, float4(p, 1)).xyz;
    }

    float3 get_uv(float3 p)
    {
        return (p + 0.5);
    }

    struct a2v
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 vertex: SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 objectPos : TEXCOORD1;
        float3 worldPos : TEXCOORD2;
    };

    v2f vert(a2v v)
    {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.objectPos = v.vertex.xyz;
        o.worldPos = mul(unity_ObjectToWorld, o.vertex).xyz;
        return o;
    }

    fixed4 frag(v2f i) : SV_TARGET
    {
        Ray ray;
        ray.origin = UNITY_MATRIX_IT_MV[3].xyz;// object space camera pos
        ray.dir = normalize(i.objectPos - ray.origin);

        AABB aabb;
        aabb.vmin = float3(-0.5, -0.5, -0.5);
        aabb.vmax = float3(0.5, 0.5, 0.5);
        float tnear;
        float tfar;
        intersect(ray, aabb, tnear, tfar);

        // tnear = max(0.0, tnear); //tnear < 0 means it's at the back side of camera, should be discard

        float3 start = ray.origin + ray.dir * tnear;
        float3 end = ray.origin + ray.dir * tfar;
        float dist = abs(tfar - tnear);
        float3 stepSize = dist / float(ITERATIONS);
        float3 ds = normalize(end - start) * stepSize;

        float4 dst = float4(0, 0, 0, 0);
        float3 p = start;

        [unroll]
        for (int iter = 0; iter < ITERATIONS; iter++)
        {
            float3 uv = get_uv(p);
            float v = tex3D(_Volume, uv).r;

            dst.a = (1.0 - dst.a) * v + dst.a;
            p += ds;

            if (dst.a > 1.0)
            {
                break;
            }
        }
        return float4(1.0, 1.0, 1.0, dst.a);
    }

    ENDCG

    SubShader
    {
        Tags {"Queue" = "Transparent"}
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}
