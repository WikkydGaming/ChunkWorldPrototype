#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Local enum so this window stands alone.
public enum FunctionType { Perlin, FBm, Ridged, SineRipple, Custom }

public class FunctionVisualizerWindow : EditorWindow
{
    // --- Sampling ---
    [Min(16)] int resolution = 256;
    Vector2 domainScale = new Vector2(4f, 4f);
    Vector2 domainOffset = Vector2.zero;

    // --- Function ---
    FunctionType function = FunctionType.Perlin;
    [Range(1, 8)] int octaves = 4;
    [Range(0f, 1f)] float persistence = 0.5f;
    [Range(1.5f, 4f)] float lacunarity = 2.0f;
    [Range(0f, 2f)] float ridgeSharpness = 1.0f;

    // --- Mapping ---
    [SerializeField] Gradient gradient;   // serialized so Unity persists it

    // --- Playback / overlay ---
    bool animate = false;
    [Range(0f, 2f)] float timeSpeed = 0.2f;
    bool overlayInScene = false;

    Texture2D tex;
    float time;
    double lastEditorUpdateTime;

    [MenuItem("Window/Function Visualizer")]
    public static void Open()
    {
        GetWindow<FunctionVisualizerWindow>("Function Visualizer").Show();
    }

    void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;

        if (gradient == null)
        {
            gradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                }
            };
        }

        if (overlayInScene) SceneView.duringSceneGui += OnSceneGUI;
        lastEditorUpdateTime = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;

        if (tex != null)
        {
            DestroyImmediate(tex);
            tex = null;
        }
    }

    void OnEditorUpdate()
    {
        if (!animate) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastEditorUpdateTime);
        lastEditorUpdateTime = now;

        time += dt * timeSpeed;

        Repaint();
        if (overlayInScene) SceneView.RepaintAll();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Sampling", EditorStyles.boldLabel);
        resolution = Mathf.Max(16, EditorGUILayout.IntField("Resolution", resolution));
        domainScale = EditorGUILayout.Vector2Field("Domain Scale", domainScale);
        domainOffset = EditorGUILayout.Vector2Field("Domain Offset", domainOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Function", EditorStyles.boldLabel);
        function = (FunctionType)EditorGUILayout.EnumPopup("Type", function);

        using (new EditorGUI.DisabledScope(function == FunctionType.Perlin ||
                                           function == FunctionType.SineRipple ||
                                           function == FunctionType.Custom))
        {
            if (function == FunctionType.FBm || function == FunctionType.Ridged)
            {
                octaves = EditorGUILayout.IntSlider("Octaves", octaves, 1, 8);
                lacunarity = EditorGUILayout.Slider("Lacunarity", lacunarity, 1.5f, 4f);
            }
            if (function == FunctionType.FBm)
            {
                persistence = EditorGUILayout.Slider("Persistence", persistence, 0f, 1f);
            }
            if (function == FunctionType.Ridged)
            {
                ridgeSharpness = EditorGUILayout.Slider("Ridge Sharpness", ridgeSharpness, 0f, 2f);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mapping", EditorStyles.boldLabel);
        gradient = EditorGUILayout.GradientField("Color Ramp", gradient);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
        animate = EditorGUILayout.Toggle("Animate", animate);
        using (new EditorGUI.DisabledScope(!animate))
        {
            timeSpeed = EditorGUILayout.Slider("Time Speed", timeSpeed, 0f, 2f);
        }

        EditorGUILayout.Space();
        bool newOverlay = EditorGUILayout.Toggle("Overlay in Scene View", overlayInScene);
        if (newOverlay != overlayInScene)
        {
            overlayInScene = newOverlay;
            SceneView.duringSceneGui -= OnSceneGUI;
            if (overlayInScene) SceneView.duringSceneGui += OnSceneGUI;
        }

        if (GUILayout.Button("Regenerate Preview"))
            GenerateTexture();

        EditorGUILayout.Space();
        if (tex == null) GenerateTexture();
        if (tex != null)
        {
            float w = EditorGUIUtility.currentViewWidth - 24f;
            float aspect = (float)tex.height / tex.width;
            Rect r = GUILayoutUtility.GetRect(w, w * aspect, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(r, tex, null, ScaleMode.ScaleToFit);
        }
    }

    void OnSceneGUI(SceneView sv)
    {
        if (tex == null) GenerateTexture();
        if (tex == null) return;

        Handles.BeginGUI();
        Rect rect = new Rect(10, 10, 256, 256);
        GUI.Box(rect, GUIContent.none);
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
        Handles.EndGUI();
    }

    void GenerateTexture()
    {
        int w = resolution;
        int h = resolution;

        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex != null) DestroyImmediate(tex);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "FunctionVisualizerTex",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                float X = (u * domainScale.x) + domainOffset.x;
                float Y = (v * domainScale.y) + domainOffset.y;

                float value = Sample(X, Y, time);
                value = Mathf.Clamp01(value);
                pixels[y * w + x] = gradient != null ? gradient.Evaluate(value) : new Color(value, value, value, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        Repaint();
    }

    float Sample(float x, float y, float t)
    {
        switch (function)
        {
            case FunctionType.Perlin:
                return Mathf.PerlinNoise(x, y);

            case FunctionType.FBm:
                return FBm(x, y, octaves, persistence, lacunarity);

            case FunctionType.Ridged:
                return Ridged(x, y, octaves, lacunarity, ridgeSharpness);

            case FunctionType.SineRipple:
                return 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (x + y + t));

            case FunctionType.Custom:
            // Example custom function; edit as needed
            //float v = Mathf.PerlinNoise(x + t, y - t);
            //return Mathf.Pow(v, 2f);

            //return FBm(x, y, octaves, persistence, lacunarity);

                //float v = Mathf.PerlinNoise(x + t, y - t);  
                //return 1.0f / (1.0f + Mathf.Exp(-25.0f*(v-0.5f)));

                return sigmoidFBm(x, y, octaves, persistence, lacunarity);

            default: return 0f;
        }
    }

    static float FBm(float x, float y, int oct, float pers, float lac)
    {
        float amp = 0.5f, freq = 1f, sum = 0f, norm = 0f;
        for (int i = 0; i < oct; i++)
        {
            sum += amp * Mathf.PerlinNoise(x * freq, y * freq);
            norm += amp;
            amp *= pers;
            freq *= lac;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    static float sigmoidFBm(float x, float y, int oct, float pers, float lac)
    {
        float amp = 0.5f, freq = 1f, sum = 0f, norm = 0f;
        for (int i = 0; i < oct; i++)
        {
            sum += amp * Mathf.PerlinNoise(x * freq, y * freq);
            norm += amp;
            amp *= pers;
            freq *= lac;
        }
        return 1.0f / (1.0f + Mathf.Exp(-15.0f * (sum - 0.75f)));
        //return norm > 0f ? sum / norm : 0f;
    }

    static float Ridged(float x, float y, int oct, float lac, float sharp)
    {
        float sum = 0f, freq = 1f, weight = 1f, norm = 0f;
        for (int i = 0; i < oct; i++)
        {
            float n = Mathf.PerlinNoise(x * freq, y * freq);
            n = 1f - Mathf.Abs(2f * n - 1f);
            n = Mathf.Pow(n, Mathf.Max(0.0001f, sharp)); // avoid 0^0
            n *= weight;
            sum += n;

            norm += 1f;
            weight = Mathf.Clamp01(n * 0.5f);
            freq *= lac;
        }
        return norm > 0f ? sum / norm : 0f;
    }
}
#endif
