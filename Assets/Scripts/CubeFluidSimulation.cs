using UnityEngine;
using UnityEngine.UI;

public class CubeFluidSimulation : MonoBehaviour
{
    public Slider vAngleSlider;
    public Slider hAngleSlider;

    [Space]
    [SerializeField]
    int N = 16;
    [SerializeField]
    int iter = 4;

    [Space]
    [SerializeField]
    float diffRate = 0;
    [SerializeField]
    float visc = 0;
    [SerializeField]
    float vel_scale = 3.0f;

    [Space]
    [SerializeField]
    [Range(0, 1f)]
    float fadeAmount = 0.05f;

    Texture3D texture;
    float[] preD;//previous density array
    float[] density;// keep in mind this is dye's density, not water's
    float[] Vx;
    float[] Vy;
    float[] Vz;
    float[] preVx;
    float[] preVy;
    float[] preVz;
    Color[] colorArray;

    public Material volumeMat;

    void Start()
    {
        N += 1; // add boundary
        texture = new Texture3D(N, N, N, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Repeat;// in case that the edge color will show on the oppsite side
        // volumeMat = GetComponent<Renderer>().material;
        volumeMat.SetTexture("NoiseTex", texture);

        preD = new float[N * N * N];
        density = new float[N * N * N];
        Vx = new float[N * N * N];
        Vy = new float[N * N * N];
        Vz = new float[N * N * N];
        preVx = new float[N * N * N];
        preVy = new float[N * N * N];
        preVz = new float[N * N * N];
        colorArray = new Color[N * N * N];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }

    void Update()
    {
        volumeMat.SetVector("boundsMin", transform.position - transform.localScale / 2f);
        volumeMat.SetVector("boundsMax", transform.position + transform.localScale / 2f);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            string path = "Assets/frame.asset";
            UnityEditor.AssetDatabase.CreateAsset(texture, path);
        }

        if (Input.GetMouseButton(1))
        {
            AddDensity(8, 14, 3, 0.5f);

            AddVelocity(8, 14, 3, 0f, -3f, 6.0f);
        }

        if (Input.GetMouseButton(0))
        {
            float angleVertical = Mathf.Deg2Rad * vAngleSlider.value;
            float angleHorizontal = Mathf.Deg2Rad * hAngleSlider.value;
            AddDensity(N / 2, N / 2, N / 2, Random.Range(0.2f, 0.5f));
            AddVelocity(N / 2, N / 2, N / 2, Mathf.Cos(angleVertical) * Mathf.Cos(angleHorizontal) * vel_scale,
                                Mathf.Cos(angleVertical) * Mathf.Sin(angleHorizontal) * vel_scale,
                                 Mathf.Sin(angleVertical) * vel_scale);
        }

        Simualtion();
        Render();
        Fade();
    }

    int IX(int x, int y, int z)
    {
        return Mathf.Clamp(x + y * N + z * N * N, 0, N * N * N);
    }

    void AddDensity(int x, int y, int z, float amount)
    {
        density[IX(x, y, z)] += amount;
    }
    void AddVelocity(int x, int y, int z, float amountX, float amountY, float amountZ)
    {
        int index = IX(x, y, z);
        Vx[index] += amountX;
        Vy[index] += amountY;
        Vz[index] += amountZ;
    }

    // * Solver
    void LinearSolve(int b, float[] x, float[] x0, float a, float c)
    {
        float cRecip = 1.0f / c;
        for (int k = 0; k < iter; k++)
        {
            for (int m = 1; m < N - 1; m++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int i = 1; i < N - 1; i++)
                    {
                        x[IX(i, j, m)] =
                            (x0[IX(i, j, m)] + a * (
                                x[IX(i + 1, j, m)] +
                                x[IX(i - 1, j, m)] +
                                x[IX(i, j + 1, m)] +
                                x[IX(i, j - 1, m)] +
                                x[IX(i, j, m - 1)] +
                                x[IX(i, j, m + 1)])) * cRecip;
                    }
                }
            }
            SetBoundary(b, x);
        }
    }

    // * Boundary
    void SetBoundary(int b, float[] x)
    {
        //  2 z boundaries
        for (int j = 1; j < N - 1; j++)
        {
            for (int i = 1; i < N - 1; i++)
            {
                x[IX(i, j, 0)] = b == 3 ? -x[IX(i, j, 1)] : x[IX(i, j, 1)];
                x[IX(i, j, N - 1)] = b == 3 ? -x[IX(i, j, N - 2)] : x[IX(i, j, N - 2)];
            }
        }

        //  2 y boundaries
        for (int k = 1; k < N - 1; k++)
        {
            for (int i = 1; i < N - 1; i++)
            {
                x[IX(i, 0, k)] = b == 2 ? -x[IX(i, 1, k)] : x[IX(i, 1, k)];
                x[IX(i, N - 1, k)] = b == 2 ? -x[IX(i, N - 2, k)] : x[IX(i, N - 2, k)];
            }
        }

        //  2 x boundaries
        for (int k = 1; k < N - 1; k++)
        {
            for (int j = 1; j < N - 1; j++)
            {
                x[IX(0, j, k)] = b == 1 ? -x[IX(1, j, k)] : x[IX(1, j, k)];
                x[IX(N - 1, j, k)] = b == 1 ? -x[IX(N - 2, j, k)] : x[IX(N - 2, j, k)];
            }
        }

        // 8 corners
        x[IX(0, 0, 0)] = 0.33f * (x[IX(1, 0, 0)] + x[IX(0, 1, 0)] + x[IX(0, 0, 1)]);
        x[IX(N - 1, 0, 0)] = 0.33f * (x[IX(N - 2, 0, 0)] + x[IX(N - 1, 1, 0)] + x[IX(N - 1, 0, 1)]);
        x[IX(0, N - 1, 0)] = 0.33f * (x[IX(1, N - 1, 0)] + x[IX(0, N - 2, 0)] + x[IX(0, N - 1, 1)]);
        x[IX(0, 0, N - 1)] = 0.33f * (x[IX(1, 0, N - 1)] + x[IX(0, 1, N - 1)] + x[IX(0, 0, N - 2)]);
        x[IX(0, N - 1, N - 1)] = 0.33f * (x[IX(1, N - 1, N - 1)] + x[IX(0, N - 2, N - 1)] + x[IX(0, N - 1, N - 2)]);
        x[IX(N - 1, N - 1, 0)] = 0.33f * (x[IX(N - 2, N - 1, 0)] + x[IX(N - 1, N - 2, 0)] + x[IX(N - 1, N - 1, 1)]);
        x[IX(N - 1, 0, N - 1)] = 0.33f * (x[IX(N - 2, 0, N - 1)] + x[IX(N - 1, 1, N - 1)] + x[IX(N - 1, 0, N - 2)]);
        x[IX(N - 1, N - 1, N - 1)] = 0.33f * (x[IX(N - 2, N - 1, N - 1)] + x[IX(N - 1, N - 2, N - 1)] + x[IX(N - 1, N - 1, N - 2)]);
    }

    void Diffuse(int b, float[] x, float[] x0, float diff)
    {
        float a = Time.deltaTime * diff * N * N;
        LinearSolve(b, x, x0, a, 1 + 6 * a);
    }

    void Project(float[] velocX, float[] velocY, float[] velocZ, float[] p, float[] div)
    {
        for (int m = 1; m < N - 1; m++)
        {
            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    div[IX(i, j, m)] = -0.5f * (
                             velocX[IX(i + 1, j, m)]
                            - velocX[IX(i - 1, j, m)]
                            + velocY[IX(i, j + 1, m)]
                            - velocY[IX(i, j - 1, m)]
                            + velocZ[IX(i, j, m + 1)]
                            - velocZ[IX(i, j, m - 1)]
                        ) / N;
                    p[IX(i, j, m)] = 0;
                }
            }
        }
        SetBoundary(0, div);
        SetBoundary(0, p);
        LinearSolve(0, p, div, 1, 6);

        for (int m = 1; m < N - 1; m++)
        {
            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    velocX[IX(i, j, m)] -= 0.5f * (p[IX(i + 1, j, m)] - p[IX(i - 1, j, m)]) * N;
                    velocY[IX(i, j, m)] -= 0.5f * (p[IX(i, j + 1, m)] - p[IX(i, j - 1, m)]) * N;
                    velocZ[IX(i, j, m)] -= 0.5f * (p[IX(i, j, m + 1)] - p[IX(i, j, m - 1)]) * N;
                }
            }
        }

        SetBoundary(1, velocX);
        SetBoundary(2, velocY);
        SetBoundary(3, velocZ);
    }

    void Advect(int b, float[] d, float[] d0, float[] velocX, float[] velocY, float[] velocZ)
    {
        float i0, i1, j0, j1, k0, k1;

        float dtx = Time.deltaTime * (N - 2);
        float dty = Time.deltaTime * (N - 2);
        float dtz = Time.deltaTime * (N - 2);

        float s0, s1, t0, t1, u0, u1;
        float tmp1, tmp2, tmp3, x, y, z;

        float Nfloat = N - 1.0f;
        float ifloat, jfloat, kfloat;
        int i, j, k;

        for (k = 1, kfloat = 1; k < N - 1; k++, kfloat++)
        {
            for (j = 1, jfloat = 1; j < N - 1; j++, jfloat++)
            {
                for (i = 1, ifloat = 1; i < N - 1; i++, ifloat++)
                {
                    // delta distance(need to reduce)
                    tmp1 = dtx * velocX[IX(i, j, k)];
                    tmp2 = dty * velocY[IX(i, j, k)];
                    tmp3 = dtz * velocZ[IX(i, j, k)];
                    // postion(last time step)
                    x = ifloat - tmp1;
                    y = jfloat - tmp2;
                    z = kfloat - tmp3;

                    //when is boundary , just set it to the center of boundary cell 
                    if (x < 0.5f) x = 0.5f;
                    if (x > Nfloat + 0.5f) x = Nfloat + 0.5f;
                    i0 = Mathf.Floor(x);
                    i1 = i0 + 1.0f;
                    if (y < 0.5f) y = 0.5f;
                    if (y > Nfloat + 0.5f) y = Nfloat + 0.5f;
                    j0 = Mathf.Floor(y);
                    j1 = j0 + 1.0f;
                    if (z < 0.5f) z = 0.5f;
                    if (z > Nfloat + 0.5f) z = Nfloat + 0.5f;
                    k0 = Mathf.Floor(z);
                    k1 = k0 + 1.0f;

                    // lerp value
                    s1 = x - i0;
                    s0 = 1.0f - s1;
                    t1 = y - j0;
                    t0 = 1.0f - t1;
                    u1 = z - k0;
                    u0 = 1.0f - u1;

                    // the index to read d0[]
                    int i0i = Mathf.Clamp((int)i0, 0, N * N * N - 1);
                    int i1i = Mathf.Clamp((int)i1, 0, N * N * N - 1);
                    int j0i = Mathf.Clamp((int)j0, 0, N * N * N - 1);
                    int j1i = Mathf.Clamp((int)j1, 0, N * N * N - 1);
                    int k0i = Mathf.Clamp((int)k0, 0, N * N * N - 1);
                    int k1i = Mathf.Clamp((int)k1, 0, N * N * N - 1);

                    d[IX(i, j, k)] =
                        s0 * (t0 * (u0 * d0[IX(i0i, j0i, k0i)]
                                    + u1 * d0[IX(i0i, j0i, k1i)])
                            + t1 * (u0 * d0[IX(i0i, j1i, k0i)]
                                    + u1 * d0[IX(i0i, j1i, k1i)]))
                        + s1 * (t0 * (u0 * d0[IX(i1i, j0i, k0i)]
                                    + u1 * d0[IX(i1i, j0i, k1i)])
                            + t1 * (u0 * d0[IX(i1i, j1i, k0i)]
                                    + u1 * d0[IX(i1i, j1i, k1i)]));
                }
            }
        }
        SetBoundary(b, d);
    }

    void Simualtion()
    {
        Diffuse(1, preVx, Vx, visc);
        Diffuse(2, preVy, Vy, visc);
        Diffuse(3, preVz, Vz, visc);

        // ! Vx Vy are used to store p and div temporarily
        Project(preVx, preVy, preVz, Vx, Vy);

        Advect(1, Vx, preVx, preVx, preVy, preVz);
        Advect(2, Vy, preVy, preVx, preVy, preVz);
        Advect(3, Vz, preVz, preVx, preVy, preVz);

        // ! preVx preVy are used to store p and div temporarily
        Project(Vx, Vy, Vz, preVx, preVy);

        Diffuse(0, preD, density, diffRate);
        Advect(0, density, preD, Vx, Vy, Vz);
    }

    void Render()
    {
        // float maxValue = 0;
        // float minValue = float.MaxValue;
        for (int k = 1; k < N - 1; k++)
        {
            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    float v = density[IX(i, j, k)];
                    // maxValue = v > maxValue ? v : maxValue;
                    // minValue = v < minValue ? v : minValue;
                    colorArray[IX(i, j, k)] = Color.white * v;
                }
            }
        }
        texture.SetPixels(colorArray);
        texture.Apply();
    }

    void Fade()
    {
        for (int i = 0; i < density.Length; i++)
        {
            density[i] = Mathf.Clamp(density[i] * fadeAmount, 0.0f, 1.0f);
        }
    }
}
