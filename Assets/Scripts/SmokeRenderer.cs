using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class SmokeRenderer : MonoBehaviour
{
    float3 boundsmin = float3(-1, -1, -1);
    float3 boundsmax = float3(1, 1, 1);

    private void OnDrawGizmos()
    {
        float3 rayOrigin = Camera.main.transform.position;
        float3 invRaydir = 1.0f / (float3)Camera.main.transform.forward;

        //Gizmos.DrawRay(rayOrigin, invRaydir);

        float3 t0 = (boundsmin - rayOrigin) * invRaydir;
        float3 t1 = (boundsmax - rayOrigin) * invRaydir;

        float3 tmin = min(t0, t1);
        float3 tmax = max(t0, t1);

        float dstA = max(max(tmin.x, tmin.y), tmin.z);
        float dstB = min(tmax.x, min(tmax.y, tmax.z));

        Gizmos.color = Color.green;
        Gizmos.DrawRay(rayOrigin, Camera.main.transform.forward * dstB);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(rayOrigin, Camera.main.transform.forward * dstA);
    }

    public Material material;
    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest, material);
    }
}
