using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TextureProcessor
{
    public static Texture2D RTtoTex2D(RenderTexture rt, TextureWrapMode _wrapMode = TextureWrapMode.Repeat, FilterMode _filterMode = FilterMode.Bilinear)
    {
        Texture2D output = new Texture2D(rt.width, rt.height)
        {
            wrapMode = _wrapMode,
            filterMode = _filterMode
        };
        RenderTexture.active = rt;
        output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        output.Apply();
        RenderTexture.active = null;
        return output;
    }

    public static Texture2D[] RTtoTex2DArray(RenderTexture tex3d)
    {
        int res = tex3d.width;
        Texture2D[] slices = new Texture2D[res];

        ComputeShader cs = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/CS/TextureProcessorCS.compute");
        int kernel = cs.FindKernel("Export2DSlice");
        cs.SetTexture(kernel, "Tex3D", tex3d);
        cs.SetInt("resolution", res);

        int numThreadGroups = Mathf.CeilToInt(res / 32.0f);

        for (int layer = 0; layer < res; layer++)
        {
            RenderTexture slice = new RenderTexture(res, res, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true
            };
            slice.Create();

            cs.SetTexture(kernel, "Slice", slice);
            cs.SetInt("layer", layer);
            cs.Dispatch(kernel, numThreadGroups, numThreadGroups, 1);

            slices[layer] = RTtoTex2D(slice);
        }

        return slices;
    }

    public static Texture3D Tex2DArraytoTex3D(Texture2D[] slices, TextureFormat format = TextureFormat.RGBAHalf, FilterMode filterMode = FilterMode.Trilinear)
    {
        int res = slices[0].width;
        Texture3D tex3D = new Texture3D(res, res, res, format, false)
        {
            filterMode = filterMode
        };
        Color[] pixels = tex3D.GetPixels();

        for (int z = 0; z < res; z++)
        {
            Color[] layerPixels = slices[z].GetPixels();
            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    pixels[x + res * (y + z * res)] = layerPixels[x + y * res];
                }
            }
        }

        tex3D.SetPixels(pixels);
        tex3D.Apply();

        return tex3D;
    }

    public static RenderTexture Tex2DtoRT(Texture2D tex2D)
    {
        if (tex2D == null)
        {
            Debug.Log("Can not find matching 2D texture under \"Assets/Resources/VoronoiNoise/\"");
            return null;
        }
        RenderTexture rt = new RenderTexture(tex2D.width, tex2D.height, 0)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        RenderTexture.active = rt;
        Graphics.CopyTexture(tex2D, rt);
        RenderTexture.active = null;
        return rt;
    }

    public static RenderTexture Tex3DtoRT(Texture3D tex3D)
    {
        if (tex3D == null)
        {
            Debug.Log("Can not find matching 3D texture under \"Assets/Resources/VoronoiNoise/\"");
            return null;
        }
        RenderTexture rt = new RenderTexture(tex3D.width, tex3D.height, 0)
        {
            format = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
            volumeDepth = tex3D.depth,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        RenderTexture.active = rt;
        Graphics.CopyTexture(tex3D, rt);
        RenderTexture.active = null;
        return rt;
    }

    public static void SaveRenderTexture(RenderTexture rt, string name)
    {
#if UNITY_EDITOR
        string path = "Assets/" + name + ".asset";
        if (rt.dimension == UnityEngine.Rendering.TextureDimension.Tex2D)
        {
            UnityEditor.AssetDatabase.CreateAsset(RTtoTex2D(rt), path);
        }
        else if (rt.dimension == UnityEngine.Rendering.TextureDimension.Tex3D)
        {
            UnityEditor.AssetDatabase.CreateAsset(Tex2DArraytoTex3D(RTtoTex2DArray(rt)), path);
        }
#endif
    }
}
