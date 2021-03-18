using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class SmokeRenderer : MonoBehaviour
{
    public Texture3D tex3d;
    public Transform domain;

    public Material material;

    public float step = 0.1f;

    public Vector4 phaseParams;

    float2 rayBoxIntersect(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir)
    {
        float3 t0 = (boundsMin - rayOrigin) * invRaydir;
        float3 t1 = (boundsMax - rayOrigin) * invRaydir;
        float3 tmin = min(t0, t1);
        float3 tmax = max(t0, t1);

        float dstA = max(max(tmin.x, tmin.y), tmin.z);
        float dstB = min(tmax.x, min(tmax.y, tmax.z));

        return float2(dstA, dstB);
    }

    void OnDrawGizmos()
    {
        float3 origin = Camera.main.transform.position;
        float3 dir = Camera.main.transform.forward;
        float3 boundsMin = domain.position - domain.localScale / 2.0f;
        float3 boundsMax = domain.position + domain.localScale / 2.0f;

        float2 hit = rayBoxIntersect(boundsMin, boundsMax, origin, 1.0f / dir);

        float3 entry = origin + dir * hit.x;
        Gizmos.DrawRay(origin, dir * hit.x);
        Gizmos.DrawWireSphere(entry, 0.01f);
        Gizmos.color = Color.cyan;
        float3 end = origin + dir * hit.y;
        Gizmos.DrawLine(entry, end);
        Gizmos.DrawWireSphere(end, 0.01f);

        float3 size = boundsMax - boundsMin;

        Gizmos.color = Color.red;
        float dstTravelled = 0;

        // float density = 0;
        while (dstTravelled < hit.y - hit.x)
        {
            float3 rayPos = entry + dir * dstTravelled;
            Gizmos.DrawWireSphere(rayPos, 0.01f);
            float3 uvw = (rayPos - boundsMin) / size;
            // Color c = tex3d.GetPixelBilinear(uvw.x, uvw.y, uvw.z);
            Gizmos.DrawRay(boundsMin, uvw);
            dstTravelled += step;
        }
        // Debug.Log(density);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(domain.position, domain.localScale);
    }

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        material.SetVector("phaseParams", phaseParams);
        material.SetTexture("VolumeTex", tex3d);
        material.SetTexture("_Volume", tex3d);
        // material.SetVector("boundsMin", domain.position - domain.localScale / 2);
        // material.SetVector("boundsMax", domain.position + domain.localScale / 2);
        Graphics.Blit(src, dest, material);
    }
}
