using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkRenderer : MonoBehaviour
{
    [HideInInspector] public bool buildCollider = false;

    public float MetersPerTileRepeat { get; set; } = 2f;    // UV tiling density (meters per repeat)
    public float BlendRadiusTiles { get; set; } = 0.35f; // edge softness in tiles

    Mesh _mesh;
    MeshCollider _collider;

    void Awake()
    {
        var mf = GetComponent<MeshFilter>();
        _mesh = mf.sharedMesh = new Mesh();
        _mesh.name = "ChunkMesh";
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    }

    enum EdgeSide : byte { None = 0, Left = 1, Right = 2, Bottom = 3, Top = 4 }

    public void BuildChunk(ChunkKey key)
    {
        int N = ChunkMath.CHUNK_SIZE;     // tiles per side
        float TS = ChunkMath.TILE_SIZE;    // meters per tile
        float uvDenom = Mathf.Max(0.0001f, MetersPerTileRepeat);
        float blendRadiusM = Mathf.Max(0.001f, BlendRadiusTiles * TS);

        int vCount = (N + 1) * (N + 1);
        int iCount = N * N * 6;

        var verts = new Vector3[vCount];
        var norms = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var uv2s = new Vector2[vCount];     // x = idxA/255, y = idxB/255
        var cols = new Color[vCount];       // a = blend weight
        var tris = new int[iCount];

        // world-grid origin (tile coords) of this chunk
        int gx0 = key.cx * N;
        int gy0 = key.cy * N;

        // ---------- PASS 1: per-cell primary/secondary & chosen edge ----------
        var cellA = new int[N, N];
        var cellB = new int[N, N];
        var cellEdge = new EdgeSide[N, N];

        for (int cy = 0; cy < N; cy++)
        {
            for (int cx = 0; cx < N; cx++)
            {
                int cgx = gx0 + cx;
                int cgy = gy0 + cy;

                float hC = WorldGen.GetHeightM(cgx, cgy);
                var tC = WorldGen.GetTypeAt(cgx, cgy, hC);
                int idxA = TerrainLayers.IndexFor(tC);

                // Neighbor types + heights
                int idxL = idxA, idxR = idxA, idxD = idxA, idxU = idxA;
                float hL = hC, hR = hC, hD = hC, hU = hC;

                // left
                {
                    int ngx = cgx - 1, ngy = cgy; float nh = WorldGen.GetHeightM(ngx, ngy);
                    idxL = TerrainLayers.IndexFor(WorldGen.GetTypeAt(ngx, ngy, nh)); hL = nh;
                }
                // right
                {
                    int ngx = cgx + 1, ngy = cgy; float nh = WorldGen.GetHeightM(ngx, ngy);
                    idxR = TerrainLayers.IndexFor(WorldGen.GetTypeAt(ngx, ngy, nh)); hR = nh;
                }
                // down (bottom)
                {
                    int ngx = cgx, ngy = cgy - 1; float nh = WorldGen.GetHeightM(ngx, ngy);
                    idxD = TerrainLayers.IndexFor(WorldGen.GetTypeAt(ngx, ngy, nh)); hD = nh;
                }
                // up (top)
                {
                    int ngx = cgx, ngy = cgy + 1; float nh = WorldGen.GetHeightM(ngx, ngy);
                    idxU = TerrainLayers.IndexFor(WorldGen.GetTypeAt(ngx, ngy, nh)); hU = nh;
                }

                // choose a single differing neighbor (if any), prefer the one with the largest height delta
                EdgeSide edge = EdgeSide.None;
                int idxB = idxA;
                float bestDelta = -1f;

                void Consider(EdgeSide side, int idxN, float hN)
                {
                    if (idxN == idxA) return;
                    float d = Mathf.Abs(hN - hC);
                    if (d > bestDelta) { bestDelta = d; edge = side; idxB = idxN; }
                }

                Consider(EdgeSide.Left, idxL, hL);
                Consider(EdgeSide.Right, idxR, hR);
                Consider(EdgeSide.Bottom, idxD, hD);
                Consider(EdgeSide.Top, idxU, hU);

                cellA[cx, cy] = idxA;
                cellB[cx, cy] = idxB;
                cellEdge[cx, cy] = edge;
            }
        }

        // ---------- PASS 2: vertices/uvs/weights using the cell choice ----------
        int vi = 0;
        for (int y = 0; y <= N; y++)
        {
            for (int x = 0; x <= N; x++)
            {
                int gx = gx0 + x;
                int gy = gy0 + y;

                float h = WorldGen.GetHeightM(gx, gy);
                verts[vi] = new Vector3(x * TS, h, y * TS);

                float wx = gx * TS, wz = gy * TS;
                uvs[vi] = new Vector2(wx / uvDenom, wz / uvDenom);

                // owning cell (clamped)
                int cx = Mathf.Clamp(x, 0, N - 1);
                int cy = Mathf.Clamp(y, 0, N - 1);

                int idxA = cellA[cx, cy];
                int idxB = cellB[cx, cy];
                EdgeSide edge = cellEdge[cx, cy];

                // distance to the chosen edge ONLY
                float localX = (x - cx) * TS;       // 0..TS
                float localY = (y - cy) * TS;       // 0..TS
                float distToEdgeM =
                    edge == EdgeSide.Left ? localX :
                    edge == EdgeSide.Right ? (TS - localX) :
                    edge == EdgeSide.Bottom ? localY :
                    edge == EdgeSide.Top ? (TS - localY) :
                    float.PositiveInfinity;

                //float w = 0f;
                //if (distToEdgeM < float.PositiveInfinity)
                //    w = 1f - Mathf.SmoothStep(0f, blendRadiusM, distToEdgeM);

                // ----- compute A/B weights, normalize -----
                float wB = 0f; // weight of secondary (idxB)
                if (distToEdgeM < float.PositiveInfinity)
                {
                    // normalized distance 0 at edge ? 1 at/after radius
                    float t = Mathf.Clamp01(distToEdgeM / blendRadiusM);

                    // choose a smooth falloff (pick ONE of these)
                    // 1) classic smoothstep feather:
                    // wB = 1f - (t * t * (3f - 2f * t));

                    // 2) slightly sharper plateau ? nicer visual blend:
                    wB = 1f - Mathf.Pow(t, 2.2f);
                }

                // primary weight is the complement
                float wA = 1f - wB;

                // normalize (defensive; keeps sums == 1)
                float sum = wA + wB;
                if (sum > 1e-5f) { wA /= sum; wB /= sum; } else { wA = 1f; wB = 0f; }

                // pack for shader
                uv2s[vi] = new Vector2(idxA / 255f, idxB / 255f);
                cols[vi] = new Color(1f, 1f, 1f, wB);  // we pass only wB; shader uses A = 1 - wB

                //uv2s[vi] = new Vector2(idxA / 255f, idxB / 255f);
                //cols[vi] = new Color(1, 1, 1, w); 

                vi++;
            }
        }

        // ---------- normals (central differences across world) ----------
        for (int y = 0; y <= N; y++)
        {
            for (int x = 0; x <= N; x++)
            {
                int gx = gx0 + x, gy = gy0 + y;
                float hL = WorldGen.GetHeightM(gx - 1, gy);
                float hR = WorldGen.GetHeightM(gx + 1, gy);
                float hD = WorldGen.GetHeightM(gx, gy - 1);
                float hU = WorldGen.GetHeightM(gx, gy + 1);
                norms[y * (N + 1) + x] = new Vector3(hL - hR, 2f, hD - hU).normalized;
            }
        }

        // ---------- triangles ----------
        int ti = 0;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int v00 = y * (N + 1) + x;
                int v10 = v00 + 1;
                int v01 = v00 + (N + 1);
                int v11 = v01 + 1;

                tris[ti++] = v00; tris[ti++] = v01; tris[ti++] = v10;
                tris[ti++] = v10; tris[ti++] = v01; tris[ti++] = v11;
            }
        }

        // ---------- upload ----------
        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.normals = norms;
        _mesh.uv = uvs;
        _mesh.uv2 = uv2s;
        _mesh.colors = cols;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();

        if (buildCollider)
        {
            if (!_collider) _collider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
        }
        else if (_collider)
        {
            Destroy(_collider);
            _collider = null;
        }
    }
}




// JAGGED BLENDING FOR SOME REASON
//using UnityEngine;

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//public class ChunkRenderer : MonoBehaviour
//{
//    [HideInInspector] public bool buildCollider = false;

//    // Set by ChunkManager before BuildChunk
//    public float MetersPerTileRepeat { get; set; } = 2f;    // UV tiling density (meters per repeat)
//    public float BlendRadiusTiles { get; set; } = 0.35f; // edge softness in tiles

//    Mesh _mesh;
//    MeshCollider _collider;

//    void Awake()
//    {
//        var mf = GetComponent<MeshFilter>();
//        _mesh = mf.sharedMesh = new Mesh();
//        _mesh.name = "ChunkMesh";
//        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // safe for large meshes
//    }

//    public void BuildChunk(ChunkKey key)
//    {
//        int N = ChunkMath.CHUNK_SIZE;     // tiles per side
//        float TS = ChunkMath.TILE_SIZE;    // meters per tile

//        int vCount = (N + 1) * (N + 1);
//        int iCount = N * N * 6;

//        var verts = new Vector3[vCount];
//        var norms = new Vector3[vCount];
//        var uvs = new Vector2[vCount];
//        var uv2s = new Vector2[vCount];     // x = idxA/255, y = idxB/255 (shader decodes)
//        var cols = new Color[vCount];       // a = blend weight 0..1
//        var tris = new int[iCount];

//        // world-grid origin (tile coords) of this chunk
//        int gx0 = key.cx * N;
//        int gy0 = key.cy * N;

//        float blendRadiusM = Mathf.Max(0.001f, BlendRadiusTiles * TS);
//        float uvDenom = Mathf.Max(0.0001f, MetersPerTileRepeat);

//        // ---- vertices, UVs, indices A/B, blend weights ----
//        int vi = 0;
//        for (int y = 0; y <= N; y++)
//        {
//            for (int x = 0; x <= N; x++)
//            {
//                int gx = gx0 + x;
//                int gy = gy0 + y;

//                // height in meters (WorldGen returns meters already)
//                float h = WorldGen.GetHeightM(gx, gy);
//                verts[vi] = new Vector3(x * TS, h, y * TS);

//                // world-planar UVs so textures align across chunk borders
//                float wx = gx * TS;
//                float wz = gy * TS;
//                uvs[vi] = new Vector2(wx / uvDenom, wz / uvDenom);

//                // nearest cell inside this chunk (clamped to N-1)
//                int cx = Mathf.Clamp(x, 0, N - 1);
//                int cy = Mathf.Clamp(y, 0, N - 1);

//                // Primary (A): type of center cell -> array index
//                float hc = WorldGen.GetHeightM(gx0 + cx, gy0 + cy);
//                var tCenter = WorldGen.GetTypeAt(gx0 + cx, gy0 + cy, hc);
//                int idxA = TerrainLayers.IndexFor(tCenter);

//                // Secondary (B) and distance to the closest differing edge
//                var (indices, distToEdgeM) = PickSecondaryAndWeight(gx0, gy0, N, x, y, cx, cy, TS, idxA);

//                // indices.x/y are already normalized 0..1; replace .x with our actual A to be safe
//                indices.x = idxA / 255f;

//                // Smooth blend inside radius
//                float w = 0f;
//                if (distToEdgeM < float.PositiveInfinity)
//                    w = 1f - Mathf.SmoothStep(0f, blendRadiusM, distToEdgeM);

//                uv2s[vi] = indices;                 // x = idxA/255, y = idxB/255
//                cols[vi] = new Color(1, 1, 1, w);   // a = blend weight

//                vi++;
//            }
//        }

//        // ---- normals via central differences (sample beyond chunk) ----
//        for (int y = 0; y <= N; y++)
//        {
//            for (int x = 0; x <= N; x++)
//            {
//                int gx = gx0 + x;
//                int gy = gy0 + y;

//                float hL = WorldGen.GetHeightM(gx - 1, gy);
//                float hR = WorldGen.GetHeightM(gx + 1, gy);
//                float hD = WorldGen.GetHeightM(gx, gy - 1);
//                float hU = WorldGen.GetHeightM(gx, gy + 1);

//                Vector3 n = new Vector3(hL - hR, 2f, hD - hU).normalized;
//                norms[y * (N + 1) + x] = n;
//            }
//        }

//        // ---- triangles (two per cell) ----
//        int ti = 0;
//        for (int y = 0; y < N; y++)
//        {
//            for (int x = 0; x < N; x++)
//            {
//                int v00 = y * (N + 1) + x;
//                int v10 = v00 + 1;
//                int v01 = v00 + (N + 1);
//                int v11 = v01 + 1;

//                tris[ti++] = v00; tris[ti++] = v01; tris[ti++] = v10;
//                tris[ti++] = v10; tris[ti++] = v01; tris[ti++] = v11;
//            }
//        }

//        // ---- upload ----
//        _mesh.Clear();
//        _mesh.vertices = verts;
//        _mesh.normals = norms;
//        _mesh.uv = uvs;
//        _mesh.uv2 = uv2s;
//        _mesh.colors = cols;
//        _mesh.triangles = tris;
//        _mesh.RecalculateBounds();

//        // optional collider
//        if (buildCollider)
//        {
//            if (!_collider) _collider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
//            _collider.sharedMesh = null;
//            _collider.sharedMesh = _mesh;
//        }
//        else if (_collider)
//        {
//            Destroy(_collider);
//            _collider = null;
//        }
//    }

//    // Returns: (x=idxA/255, y=idxB/255), distance to closest differing edge in meters.
//    (Vector2 indices, float distToEdgeM) PickSecondaryAndWeight(
//        int gx0, int gy0, int N,
//        int vx, int vy, int cx, int cy, float TS,
//        int idxA /* primary index already known */)
//    {
//        int idxB = idxA;
//        float distToEdgeM = float.PositiveInfinity;

//        // local position inside the center cell (0..TS)
//        float localX = (vx - cx) * TS;
//        float localY = (vy - cy) * TS;

//        // Check 4 neighbors; if different, prefer the closest edge
//        Consider(cx - 1, cy, localX);          // left edge distance
//        Consider(cx + 1, cy, TS - localX);     // right edge
//        Consider(cx, cy - 1, localY);          // bottom edge
//        Consider(cx, cy + 1, TS - localY);     // top edge

//        return (new Vector2(idxA / 255f, idxB / 255f), distToEdgeM);

//        void Consider(int ncx, int ncy, float edgeDist)
//        {
//            ncx = Mathf.Clamp(ncx, 0, N - 1);
//            ncy = Mathf.Clamp(ncy, 0, N - 1);

//            int ngx = gx0 + ncx;
//            int ngy = gy0 + ncy;
//            float nh = WorldGen.GetHeightM(ngx, ngy);
//            int nIdx = TerrainLayers.IndexFor(WorldGen.GetTypeAt(ngx, ngy, nh));

//            if (nIdx == idxA) return;

//            if (edgeDist < distToEdgeM)
//            {
//                distToEdgeM = edgeDist;
//                idxB = nIdx;
//            }
//        }
//    }
//}



// OLD CODE
//using UnityEngine;

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]


//public class ChunkRenderer : MonoBehaviour
//{
//    Mesh _mesh;
//    MeshCollider _collider;
//    public bool buildCollider = false;

//    [SerializeField] float metersPerTileRepeat = 2f; // tiling density
//    [SerializeField, Range(0.05f, 1.0f)] float blendRadiusTiles = 0.35f; // edge softness in tiles


//    // Build a (N+1)x(N+1) vertex grid; triangles form N*N tiles
//    public void BuildChunk(ChunkKey key)
//    {
//        if (_mesh == null)
//        {
//            _mesh = new Mesh { name = $"Chunk {key}" };
//            GetComponent<MeshFilter>().sharedMesh = _mesh;
//        }

//        int N = ChunkMath.CHUNK_SIZE;
//        int vCount = (N + 1) * (N + 1);
//        var verts = new Vector3[vCount];
//        var cols = new Color[vCount];
//        //var uvs = new Vector2[vCount];

//        // New for Tile TextureArray
//        var uvs = new Vector2[vCount];
//        var uv2s = new Vector2[vCount];

//        // world-space origin of this chunk in grid coords  
//        int gx0 = key.cx * N;
//        int gy0 = key.cy * N;

//        // vertices
//        int vi = 0;
//        for (int y = 0; y <= N; y++)
//        {
//            for (int x = 0; x <= N; x++)
//            {
//                int gx = gx0 + x;
//                int gy = gy0 + y;

//                float h = WorldGen.GetHeightM(gx, gy);
//                verts[vi] = new Vector3(x * ChunkMath.TILE_SIZE, h, y * ChunkMath.TILE_SIZE);

//                // color by type at the nearest cell (clamp to N-1 to avoid out-of-range)
//                int cx = Mathf.Clamp(x, 0, N - 1);
//                int cy = Mathf.Clamp(y, 0, N - 1);
//                //float hc = WorldGen.GetHeightM(gx0 + cx, gy0 + cy);
//                //var type = WorldGen.GetTypeAt(gx0 + cx, gy0 + cy, hc);

//                // Pick a layer index from the NEAREST cell (or choose a rule you prefer)
//                int cxn = Mathf.Clamp(x, 0, N - 1);
//                int cyn = Mathf.Clamp(y, 0, N - 1);
//                float hc = WorldGen.GetHeightM(gx0 + cxn, gy0 + cyn);
//                var type = WorldGen.GetTypeAt(gx0 + cxn, gy0 + cyn, hc);
//                int layerIndex = TerrainLayers.IndexFor(type);


//                //Debug.Log(type);
//                cols[vi] = WorldGen.ColorFor(type);
//                //uvs[vi] = new Vector2((float)x / N, (float)y / N);

//                // world coords for this vertex (relative to world)
//                float wx = (gx) * ChunkMath.TILE_SIZE;
//                float wy = (gy) * ChunkMath.TILE_SIZE;
//                uvs[vi] = new Vector2(wx / metersPerTileRepeat, wy / metersPerTileRepeat);

//                // Store normalized index in uv2.x (0..1). Support up to 256 layers by encoding /255.
//                uv2s[vi] = new Vector2(layerIndex / 255f, 0f);

//                vi++;
//            }
//        }

//        // triangles
//        int quadCount = N * N;
//        var tris = new int[quadCount * 6];
//        int ti = 0;
//        for (int y = 0; y < N; y++)
//        {
//            for (int x = 0; x < N; x++)
//            {
//                int v00 = y * (N + 1) + x;
//                int v10 = v00 + 1;
//                int v01 = v00 + (N + 1);
//                int v11 = v01 + 1;

//                // two triangles per quad
//                tris[ti++] = v00; tris[ti++] = v01; tris[ti++] = v10;
//                tris[ti++] = v10; tris[ti++] = v01; tris[ti++] = v11;
//            }
//        }

//        _mesh.Clear();
//        _mesh.vertices = verts;
//        _mesh.colors = cols;
//        _mesh.uv = uvs;

//        // New for Tile TextureArray
//        _mesh.uv2 = uv2s;


//        _mesh.triangles = tris;
//        _mesh.RecalculateNormals();
//        _mesh.RecalculateBounds();

//        if (buildCollider)
//        {
//            if (_collider == null) _collider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
//            _collider.sharedMesh = null;        // ensure refresh
//            _collider.sharedMesh = _mesh;       // note: collider build is expensive; avoid frequent rebuilds
//        }
//        else if (_collider != null)
//        {
//            _collider.sharedMesh = null;
//            Destroy(_collider);
//            _collider = null;
//        }
//    }
//}
