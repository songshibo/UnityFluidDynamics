using UnityEngine;
using UnityEngine.UI;

public class FluidSimulation : MonoBehaviour
{
    public enum DiffMethod
    {
        stable,
        unstable
    };

    public enum DebugModel
    {
        Normalized,
        Unnormalized
    };

    public Slider angleSlider;
    public Text viscosityText;
    public Text diffRateText;
    public Camera cam;
    public Color color_h;
    public Color color_l;
    [SerializeField]
    int N = 128;
    [SerializeField]
    int iter = 4;
    [SerializeField]
    float velocityScale = 10.0f;
    [SerializeField]
    [Range(0.0f, 1.0f)]
    float fadeAmount = 0.5f;
    public Color dieColor;
    Texture2D texture;

    public DiffMethod diffMethod;
    public DebugModel velocityDebug;
    private float nVelLength = 1.0f;
    [SerializeField]
    [Range(0.0f, 0.1f)]
    [Tooltip("density diffusion rate")]
    float diff = 0.1f;
    [SerializeField]
    [Range(0.0f, 0.1f)]
    [Tooltip("fluid viscosity")]
    float visc = 0;
    float[] preD;//previous density array
    float[] density;// keep in mind this is dye's density, not water's
    float[] Vx;
    float[] Vy;
    float[] preVx;
    float[] preVy;

    void Start()
    {
        texture = new Texture2D(N, N);
        texture.wrapMode = TextureWrapMode.Clamp;// in case that the edge color will show on the oppsite side
        GetComponent<Renderer>().material.mainTexture = texture;

        preD = new float[N * N];
        density = new float[N * N];
        Vx = new float[N * N];
        Vy = new float[N * N];
        preVx = new float[N * N];
        preVy = new float[N * N];
    }

    void InputHandler()
    {
        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out hit))
        {
            Transform objectHit = hit.transform;

            Vector2 coords = hit.textureCoord * N;
            // Vector3 dirs = Vector3.Normalize(Input.mousePosition - preMousePos) * velocityScale;
            // AddVelocity((int)coords.x, (int)coords.y, dirs.x, dirs.y);
            // * Simulatie gravity
            // AddVelocity((int)coords.x, (int)coords.y, 0.0f, -velocityScale);
            
            float densityAmount = Random.Range(100.0f, 200.0f);

            AddDensity((int)coords.x, (int)coords.y, densityAmount);
            AddDensity((int)coords.x+1, (int)coords.y, densityAmount/2);
            AddDensity((int)coords.x-1, (int)coords.y, densityAmount/2);
            AddDensity((int)coords.x, (int)coords.y+1, densityAmount/2);
            AddDensity((int)coords.x, (int)coords.y-1, densityAmount/2);

            float angle = Mathf.Deg2Rad * angleSlider.value;
            AddVelocity((int)coords.x, (int)coords.y, Mathf.Cos(angle) * velocityScale, Mathf.Sin(angle) * velocityScale);
        }
        else
        {
            Vector2 coords = new Vector2(N/2, N/2);
            float densityAmount = Random.Range(100.0f, 200.0f);

            AddDensity((int)coords.x, (int)coords.y, densityAmount);
            AddDensity((int)coords.x+1, (int)coords.y, densityAmount/2);
            AddDensity((int)coords.x-1, (int)coords.y, densityAmount/2);
            AddDensity((int)coords.x, (int)coords.y+1, densityAmount/2);
            AddDensity((int)coords.x, (int)coords.y-1, densityAmount/2);

            float angle = Mathf.Deg2Rad * angleSlider.value;
            AddVelocity((int)coords.x, (int)coords.y, Mathf.Cos(angle) * velocityScale, Mathf.Sin(angle) * velocityScale);
        }
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            InputHandler();
        }
        
        
        FluidSimulationStep();
        RenderDensity();
        DensityFade();

        VisualizeVelocity();
        viscosityText.text = "viscosity:" + visc.ToString();
        diffRateText.text = "diffusion rate:" + diff.ToString();
    }

    void VisualizeVelocity()
    {
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                Vector3 vel;
                Vector3 left_a;
                Vector3 right_a;
                Vector3 startPos = new Vector3(i * 1.0f/N - 0.5f, j * 1.0f/N - 0.5f, 0.0f);
                Color c;
                if (velocityDebug == DebugModel.Normalized)
                {
                    vel = new Vector3(Vx[IX(i, j)], Vy[IX(i,j)], 0.0f);
                    c = Color.Lerp(color_l, color_h, vel.magnitude / 0.08f);
                    vel = vel.normalized * 1.0f/N * nVelLength;

                    left_a = Vector3.Normalize(new Vector3((0.866f * -vel.x - -0.5f * -vel.y), (-0.5f * -vel.x + 0.866f * -vel.y), 0.0f)) * vel.magnitude * 0.5f;
                    right_a = Vector3.Normalize(new Vector3((0.866f * -vel.x - 0.5f * -vel.y), (0.5f * -vel.x + 0.866f * -vel.y), 0.0f)) * vel.magnitude * 0.5f;

                    Debug.DrawRay(startPos, vel, c, 1.0f/N);
                    Debug.DrawRay(startPos + vel, left_a, c, 1.0f/N);
                    Debug.DrawRay(startPos + vel, right_a, c, 1.0f/N);
                }

                if (velocityDebug == DebugModel.Unnormalized)
                {
                    vel = new Vector3(Vx[IX(i, j)], Vy[IX(i,j)], 0.0f);
                    // ! draw arrow when velocity isn't normalized make it looking chaotic
                    //// left_a = Vector3.Normalize(new Vector3((0.866f * -vel.x + -0.5f * -vel.y), (-0.5f * -vel.x + 0.866f * -vel.y), 0.0f)) * vel.magnitude * 0.25f;
                    //// right_a = Vector3.Normalize(new Vector3((0.866f * -vel.x + 0.5f * -vel.y), (0.5f * -vel.x + 0.866f * -vel.y), 0.0f)) * vel.magnitude * 0.25f;
                    Debug.DrawRay(startPos, vel, Color.red);
                    //// Debug.DrawRay(startPos + vel, left_a, Color.red, 1.0f/N);
                    //// Debug.DrawRay(startPos + vel, right_a, Color.red, 1.0f/N);
                }
            }
        }
    }

    int IX(int x, int y)
    {
        return Mathf.Clamp(x + y * N, 0, N * N - 1);
    }

    void AddDensity(int x, int y, float amount)
    {
        density[IX(x, y)] += amount;
    }

    void AddVelocity(int x, int y, float amountX, float amountY)
    {
        int index = IX(x, y);
        Vx[index] += amountX;
        Vy[index] += amountY;
    }

    void Diffuse(int b, float[] x, float[] x0, float diff)
    {
        float a = Time.deltaTime * diff * N * N;
        if (diffMethod == DiffMethod.unstable)
        {
            UnstableLinearSolve(b, x, x0, a);
        }
        else
        {
            LinearSolve(b, x, x0, a, 1 + 4 * a);
        }
    }
    void Project(float[] velocX, float[] velocY, float[] p, float[] div)
    {
        for (int j = 1; j < N - 1; j++)
        {
            for (int i = 1; i < N - 1; i++)
            {
                div[IX(i, j)] = -0.5f * (
                         velocX[IX(i + 1, j)]
                        - velocX[IX(i - 1, j)]
                        + velocY[IX(i, j + 1)]
                        - velocY[IX(i, j - 1)]
                    ) / N;
                p[IX(i, j)] = 0;
            }
        }
        SetBoundary(0, div);
        SetBoundary(0, p);
        LinearSolve(0, p, div, 1, 6);

        for (int j = 1; j < N - 1; j++)
        {
            for (int i = 1; i < N - 1; i++)
            {
                velocX[IX(i, j)] -= 0.5f * (p[IX(i + 1, j)] - p[IX(i - 1, j)]) * N;
                velocY[IX(i, j)] -= 0.5f * (p[IX(i, j + 1)] - p[IX(i, j - 1)]) * N;
            }
        }
        SetBoundary(1, velocX);
        SetBoundary(2, velocY);
    }

    void Advect(int b, float[] d, float[] d0, float[] velocX, float[] velocY)
    {
        float i0, i1, j0, j1;

        float dtx = Time.deltaTime * (N - 2);
        float dty = Time.deltaTime * (N - 2);

        float s0, s1, t0, t1;
        float tmp1, tmp2, x, y;

        float Nfloat = N;
        float ifloat, jfloat;
        int i, j;

        for (j = 1, jfloat = 1; j < N - 1; j++, jfloat++)
        {
            for (i = 1, ifloat = 1; i < N - 1; i++, ifloat++)
            {
                tmp1 = dtx * velocX[IX(i, j)];
                tmp2 = dty * velocY[IX(i, j)];
                x = ifloat - tmp1;
                y = jfloat - tmp2;

                if (x < 0.5f) x = 0.5f;
                if (x > Nfloat + 0.5f) x = Nfloat + 0.5f;
                i0 = Mathf.Floor(x);
                i1 = i0 + 1.0f;
                if (y < 0.5f) y = 0.5f;
                if (y > Nfloat + 0.5f) y = Nfloat + 0.5f;
                j0 = Mathf.Floor(y);
                j1 = j0 + 1.0f;

                s1 = x - i0;
                s0 = 1.0f - s1;
                t1 = y - j0;
                t0 = 1.0f - t1;

                int i0i = (int)i0;
                int i1i = (int)i1;
                int j0i = (int)j0;
                int j1i = (int)j1;

                d[IX(i, j)] =
                    s0 * (t0 * d0[IX(i0i, j0i)] + (t1 * d0[IX(i0i, j1i)])) + s1 * (t0 * d0[IX(i1i, j0i)] + (t1 * d0[IX(i1i, j1i)]));
            }
        }
        SetBoundary(b, d);
    }

    // * backward method Gauss-Seidle Relaxation
    void LinearSolve(int b, float[] x, float[] x0, float a, float c)
    {
        float cRecip = 1.0f / c;
        for (int k = 0; k < iter; k++)
        {
            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    x[IX(i, j)] =
                        (x0[IX(i, j)] + a * (x[IX(i + 1, j)] + x[IX(i - 1, j)] + x[IX(i, j + 1)] + x[IX(i, j - 1)])) * cRecip;
                }
            }
            SetBoundary(b, x);
        }
    }

    // * straightforward method
    // * oscillated when a is large
    void UnstableLinearSolve(int b, float[] x, float[] x0, float a)
    {
        for (int j = 1; j < N - 1; j++)
        {
            for (int i = 1; i < N - 1; i++)
            {
                x[IX(i, j)] = x0[IX(i, j)] + a * (x0[IX(i+1, j)] + x0[IX(i-1, j)] + x0[IX(i, j+1)] + x0[IX(i, j-1)] - 4 * x0[IX(i, j)]);
            }
        }
        SetBoundary(b, x);
    }

    void SetBoundary(int boarderIndex, float[] x)
    {
        for (int i = 1; i < N - 1; i++)
        {
            x[IX(i, 0)] = boarderIndex == 2 ? -x[IX(i, 1)] : x[IX(i, 1)];
            x[IX(i, N - 1)] = boarderIndex == 2 ? -x[IX(i, N - 2)] : x[IX(i, N - 2)];
        }
        for (int j = 1; j < N - 1; j++)
        {
            x[IX(0, j)] = boarderIndex == 1 ? -x[IX(1, j)] : x[IX(1, j)];
            x[IX(N - 1, j)] = boarderIndex == 1 ? -x[IX(N - 2, j)] : x[IX(N - 2, j)];
        }

        x[IX(0, 0)] = 0.5f * (x[IX(1, 0)] + x[IX(0, 1)]);
        x[IX(0, N - 1)] = 0.5f * (x[IX(1, N - 1)] + x[IX(0, N - 2)]);
        x[IX(N - 1, 0)] = 0.5f * (x[IX(N - 2, 0)] + x[IX(N - 1, 1)]);
        x[IX(N - 1, N - 1)] = 0.5f * (x[IX(N - 1, N - 1)] + x[IX(N - 1, N - 2)]);
    }

    void FluidSimulationStep()
    {
        Diffuse(1, preVx, Vx, visc);
        Diffuse(2, preVy, Vy, visc);

        Project(preVx, preVy, Vx, Vy);

        Advect(1, Vx, preVx, preVx, preVy);
        Advect(2, Vy, preVy, preVx, preVy);

        Project(Vx, Vy, preVx, preVy);

        Diffuse(0, preD, density, diff);
        Advect(0, density, preD, Vx, Vy);
    }

    void DensityFade()//slowly fade density by time
    {
        for (int i = 0; i < density.Length; i++)
        {
            density[i] = Mathf.Clamp(density[i] - fadeAmount, 0.0f, 255.0f);
        }
    }

    void RenderDensity()
    {
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                float d = density[IX(i, j)] / 255.0f;
                texture.SetPixel(i, j, dieColor * d);
            }
        }
        texture.Apply();
    }
}
