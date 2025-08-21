using UnityEngine;

public static class WorldGen
{
    // ======= Master seed =======
    public static int Seed = 12345;

    // ======= Biome types =======
    public enum Biome { Mountains, Hills, Flats }

    // New 
    static float Sigmoid01(float n01, float mid, float k)
    {
        // logistic centered at 'mid'
        return 1f / (1f + Mathf.Exp(-k * (n01 - mid)));
    }

    static float Normalize01(float value, float min, float max)
    {
        return Mathf.Clamp01((value - min) / Mathf.Max(1e-6f, max - min));
    }


    // ======= Per-biome noise profile =======
    [System.Serializable]
    public class NoiseProfile
    {
        public int octaves = 5;
        public float baseFrequency = 0.003f;  // lower = larger features
        public float lacunarity = 2.0f;       // freq multiplier per octave
        public float persistence = 0.5f;      // amplitude multiplier per octave
        public float amplitude = 20f;         // meters
        public bool ridge = false;            // turn FBM into ridged noise
        public float warpAmount = 0f;         // domain warp strength (in grid units)
        public float warpFrequency = 0.0015f; // warp field frequency

        // --- NEW: shaping ---
        public bool useSigmoid = false;  // push values to 0/1 softly
        public float sigmoidMid = 0.5f;   // midpoint in [0,1]
        public float sigmoidSharp = 10f;    // ~6..16 feels good

        public bool useTerraces = false;  // quantize after shaping
        public float terraceStepM = 2f;     // meters between steps
    }

    // Example profiles (tweak in code or expose in inspector by wrapping in a MonoBehaviour)
    static readonly NoiseProfile Mountains = new NoiseProfile
    {
        octaves = 7,
        baseFrequency = 0.0020f,
        lacunarity = 2.1f,
        persistence = 0.52f,
        amplitude = 300f,
        ridge = false,
        warpAmount = 50f,
        warpFrequency = 0.0012f,

        useSigmoid = true,  // push values to 0/1 softly
        sigmoidMid = 0.5f,   // midpoint in [0,1]
        sigmoidSharp = 10f,   // ~6..16 feels good

        useTerraces = false,  // quantize after shaping
        terraceStepM = 1f    // meters between steps
};
    static readonly NoiseProfile Hills = new NoiseProfile
    {
        octaves = 3,
        baseFrequency = 0.0035f,
        lacunarity = 1.0f,
        persistence = 0.5f,
        amplitude = 175f,
        ridge = false,
        warpAmount = 20f,
        warpFrequency = 0.0018f
    };

    static readonly NoiseProfile Dunes = new NoiseProfile
    {
        octaves = 3,
        baseFrequency = 0.0035f,
        lacunarity = 3.0f,
        persistence = 0.5f,
        amplitude = 25f,
        ridge = true,
        warpAmount = 20f,
        warpFrequency = 0.0018f
    };

    static readonly NoiseProfile Flats = new NoiseProfile
    {
        octaves = 3,
        baseFrequency = 0.0060f,
        lacunarity = 2.0f,
        persistence = 0.35f,
        amplitude = 4f,
        ridge = false,
        warpAmount = 0f,
        warpFrequency = 0.0f
    };

    // ======= Public entry point: world height in meters =======
    public static float GetHeightM(int gx, int gy)
    {
        // 1) Get biome weights in world space (stable across chunks)
        //var w = BiomeWeights(gx, gy); // .x=Mountains, .y=Hills, .z=Flats ; sums ~1
        //Debug.Log(w.ToString());


        // OPTION SIGNMOID BIOME BLEND
        var w = BiomeWeights1D(gx, gy);

        // DEBUG CODE TO TEST ONLY ONE BIOME TYPE
        w = new Vector3(0.0f, 1.0f, 0.0f);
        // END DEBUG CODE

        float h = SampleProfile(Mountains, gx, gy) * w.x
                 + SampleProfile(Hills, gx, gy) * w.y
                 + SampleProfile(Flats, gx, gy) * w.z;
        //Debug.Log(w.ToString());
        // END OPTION SIGNMOID BIOME BLEND


        //// OPTION HARD BIOME CHANGE
        //var w = BiomeWeights(gx, gy); // .x=Mountains, .y=Hills, .z=Flats ; sums ~1
        //NoiseProfile p = (w.x > w.y && w.x > w.z) ? Mountains :
        //                 (w.y > w.x && w.y > w.z) ? Hills : Flats;

        //float h = SampleProfile(p, gx, gy);
        //// END OPTION HARD BIOME CHANGE




        // OPTION 2
        //var w = BiomeWeights(gx, gy); // .x=Mountains, .y=Hills, .z=Flats ; sums ~1
        // 2) Sample each biome’s height with its own params
        //float hm = SampleProfile(Mountains, gx, gy);
        //float hh = SampleProfile(Hills, gx, gy);
        //float hf = SampleProfile(Flats, gx, gy);

        ////w = new Vector3(1, 0, 0);

        //// Biome Blend by weights
        //float h = hm * w.x + hh * w.y + hf * w.z;

        // END OPTION 2



        // 4) Optional base sea level / elevation offset
        float baseOffset = 0f;
        return h + baseOffset;
    }

    // ======= Biome map (returns weights for Mtn/Hills/Flats) =======
    // Smoothly vary biomes using low-frequency noise, then Softmax-like normalization.
    static Vector3 BiomeWeights(int gx, int gy)
    {
        // Use large-scale noise so biomes form big regions
        //float f = 0.0006f; // lower = bigger regions
        float f = 0.016f; // lower = bigger regions
        float nx = (gx + Seed * 11) * f;
        float ny = (gy - Seed * 17) * f;

        float n1 = Perlin01(nx, ny); // raw 0..1
        float n2 = Perlin01(nx + 100.5f, ny - 37.7f);
        float n3 = Perlin01(nx - 23.2f, ny + 71.1f);

        // Bias & sharpen to taste (more contrast ? stronger biome boundaries)
        float mtn = Mathf.Pow(Mathf.SmoothStep(0.4f, 1.0f, n1), 1.3f);
        float hil = Mathf.Pow(Mathf.SmoothStep(0.3f, 1.0f, n2), 1.1f);
        float flt = Mathf.Pow(1.0f - Mathf.SmoothStep(0.0f, 0.7f, n3), 1.2f);

        // Normalize
        float sum = mtn + hil + flt + 1e-6f;
        return new Vector3(mtn / sum, hil / sum, flt / sum);
    }

    /// Returns weights (wm, wh, wf) that sum to ~1
    static Vector3 BiomeWeights1D(int gx, int gy, float freq = 0.0006f, float k = 10f, float mid1 = 0.35f, float mid2 = 0.65f)
    {
        float n = Mathf.PerlinNoise((gx + WorldGen.Seed * 11) * freq,
                                    (gy - WorldGen.Seed * 17) * freq);

        // Two logistic gates
        float gateMtn = Sigmoid01(n, mid1, k);        // low -> 0, high -> 1 (mountain threshold)
        float gateFlat = 1f - Sigmoid01(n, mid2, k);  // low -> 1, high -> 0 (flat threshold)

        float wm = gateMtn;        // mountains dominate high n
        float wf = gateFlat;       // flats dominate low n
        float wh = Mathf.Clamp01(1.0f - wm - wf); // hills fill the middle band

        // Normalize (robust)
        float s = wm + wh + wf + 1e-6f;
        return new Vector3(wm / s, wh / s, wf / s);
    }

    static float SampleProfile(NoiseProfile p, int gx, int gy)
    {
        // Domain warp (optional)
        float x = gx, y = gy;
        if (p.warpAmount > 0f && p.warpFrequency > 0f)
        {
            Vector2 d = Warp(gx, gy, p.warpAmount, p.warpFrequency);
            x += d.x; y += d.y;
        }

        // Raw FBM in ~[0, sum(amp)] space
        float raw = p.ridge ? RidgeFBM(x, y, p) : FBM(x, y, p);

        // Convert to 0..1 for shaping (approximate normalization by div by total amp)
        // Total amplitude of geometric series: A = 1 + pers + pers^2 + ... ≈ 1/(1-persistence)
        float totalAmp = (p.persistence < 0.999f) ? (1f / (1f - p.persistence)) : (float)p.octaves;
        float n01 = Mathf.Clamp01(raw / Mathf.Max(1e-6f, totalAmp));

        // Optional sigmoid shaping (step-like but smooth)
        if (p.useSigmoid)
            n01 = Sigmoid01(n01, p.sigmoidMid, p.sigmoidSharp);

        // Scale to meters
        float h = n01 * p.amplitude;

        // Optional terraces (quantize height)
        if (p.useTerraces && p.terraceStepM > 0.0001f)
            h = Mathf.Round(h / p.terraceStepM) * p.terraceStepM;

        return h;
    }

    // ======= Profile sampler (FBM + optional ridge + optional domain warp) =======
    //static float SampleProfile(NoiseProfile p, int gx, int gy)
    //{
    //    // Domain warp (optional) – pushes coords around for more organic shapes
    //    float x = gx, y = gy;
    //    if (p.warpAmount > 0f && p.warpFrequency > 0f)
    //    {
    //        Vector2 d = Warp(gx, gy, p.warpAmount, p.warpFrequency);
    //        x += d.x; y += d.y;
    //    }

    //    //float h = p.ridge ? RidgeFBM(x, y, p) : FBM(x, y, p);
    //    float h;
    //    if (p.ridge)
    //    {
    //        h = RidgeFBM(x, y, p);
    //        h += RidgeFBM(x, y, p);
    //        h += RidgeFBM(x, y, p);
    //    }
    //    else
    //    {
    //        h = FBM(x, y, p);
    //    }
    //        return h * p.amplitude;
    //}

    // ======= Plain FBM =======
    static float FBM(float x, float y, NoiseProfile p)
    {
        float sum = 0f;
        float amp = 1f;
        float freq = p.baseFrequency;

        // Offset by seed so different seeds produce different worlds deterministically
        float ox = Seed * 0.12345f, oy = Seed * 0.54321f;

        for (int i = 0; i < p.octaves; i++)
        {
            sum += Perlin01((x + ox) * freq, (y + oy) * freq) * amp;
            freq *= p.lacunarity;
            amp *= p.persistence;
        }
        // Normalize to ~0..1 depending on octaves/persistence; keep as-is for terrain feel
        return sum;
    }

    // ======= Ridged FBM (for sharp mountain crests) =======
    static float RidgeFBM(float x, float y, NoiseProfile p)
    {
        float sum = 0f;
        float amp = 1f;
        float freq = p.baseFrequency;

        float ox = Seed * 0.22222f, oy = Seed * 0.77777f;
        //Debug.Log("Ridged");
        for (int i = 0; i < p.octaves; i++)
        {
            float n = Perlin01((x + ox) * freq, (y + oy) * freq);
            n = 1f - Mathf.Abs(2f * n - 1f); // ridged
            sum += n * amp;
            freq *= p.lacunarity;
            amp *= p.persistence;
        }
        return sum;
    }

    // ======= Domain warp vector (uses two noise fields as a vector) =======
    static Vector2 Warp(int gx, int gy, float amount, float freq)
    {
        float ox = Seed * 0.33333f, oy = -Seed * 0.44444f;
        float u = (Perlin01((gx + ox) * freq, (gy + oy) * freq) - 0.5f) * 2f;
        float v = (Perlin01((gx - oy) * freq, (gy + ox) * freq) - 0.5f) * 2f;
        return new Vector2(u, v) * amount;
    }

    // ======= Perlin in 0..1 =======
    static float Perlin01(float x, float y) => Mathf.PerlinNoise(x, y);


    // Simple terrain type classification based on biome dominance and/or height
    public static TerrainType GetTypeAt(int gx, int gy, float height)
    {
        // Use the same biome weights used for height blending so results match visuals
        Vector3 w = BiomeWeights(gx, gy); // x=Mountains, y=Hills, z=Flats

        // Pick the dominant biome first
        int dominant = (w.x > w.y && w.x > w.z) ? 0 : (w.y > w.z ? 1 : 2);

        // Example rules (tweak as you like):
        // - Mountains ? Tree (dark green) at mid/high height, Soil if very low
        // - Hills     ? Grass
        // - Flats     ? Soil (with some Grass if slightly elevated)
        switch (dominant)
        {
            case 0: // Mountains
                return (height > 12f) ? TerrainType.Tree : TerrainType.Soil;

            case 1: // Hills
                return TerrainType.Grass;

            default: // Flats
                return (height > 2.5f) ? TerrainType.Grass : TerrainType.Soil;
        }
    }

    // Map terrain types to vertex colors (used by your chunk shader)
    public static Color ColorFor(TerrainType t) => t switch
    {
        TerrainType.Soil => new Color(0.40f, 0.30f, 0.20f),
        TerrainType.Grass => new Color(0.18f, 0.65f, 0.22f),
        TerrainType.Tree => new Color(0.10f, 0.45f, 0.15f),
        _ => Color.gray
    };


    //// Tweak these to taste
    ////public static int Seed = 12345;
    //public static int Seed = 999;

    //public static float GetHeightM(int gx, int gy)
    //{

    //    //// Two octave Perlin for hills
    //    //float h1 = Mathf.PerlinNoise((gx + Seed * 11) * 0.035f, (gy - Seed * 7) * 0.0035f) * 18f;
    //    ////float h1 = Mathf.PerlinNoise((gx + Seed * 11) * 0.135f, (gy - Seed * 7) * 0.135f) * 18f;
    //    //float h2 = Mathf.PerlinNoise((gx + Seed * 3) * 0.015f, (gy + Seed * 17) * 0.015f) * 2.0f;

    //    ////Debug.Log(h1 + " " + h2 + " " + (h1+h2));
    //    //return h1 + h2; // meters


    //    // Two octave Perlin for hills
    //    float h1 = Mathf.PerlinNoise((gx + Seed * 11) * 0.0035f, (gy - Seed * 7) * 0.0035f) * 18f;
    //    float h2 = Mathf.PerlinNoise((gx + Seed * 3) * 0.015f, (gy + Seed * 17) * 0.015f) * 2.0f;
    //    return h1 + h2; // meters
    //}

    //public static TerrainType GetTypeAt(int gx, int gy, float height)
    //{
    //    // Simple: low = Soil; mid = Grass; high or dry = Tree
    //    float moisture = Mathf.PerlinNoise((gx - Seed) * 0.006f, (gy + Seed) * 0.006f);

    //    //Debug.Log("Moisture: " + moisture);


    //    ////Debug.Log("Height: " + height);
    //    //if (height < 1.0f) return TerrainType.Soil;
    //    ////if (height < 20.0f) Debug.Log("Soil picked");
    //    //if (height < 20.0f) return TerrainType.Soil;
    //    //if (moisture > 0.55f) return TerrainType.Grass;
    //    //return TerrainType.Tree;

    //    //float moisture = Mathf.PerlinNoise((gx - Seed) * 0.006f, (gy + Seed) * 0.006f);
    //    if (height < 4.0f) return TerrainType.Soil;
    //    if (moisture > 0.75f) return TerrainType.Grass;
    //    return TerrainType.Tree;
    //}

    //public static Color ColorFor(TerrainType t) => t switch
    //{
    //    TerrainType.Soil => new Color(0.40f, 0.30f, 0.20f),
    //    TerrainType.Grass => new Color(0.18f, 0.65f, 0.22f),
    //    TerrainType.Tree => new Color(0.10f, 0.45f, 0.15f),
    //    //TerrainType.Tree => new Color(0.40f, 0.30f, 0.20f),
    //    _ => Color.gray
    //};



}
