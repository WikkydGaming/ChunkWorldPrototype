using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkRenderer : MonoBehaviour
{
    Mesh _mesh;
    MeshCollider _collider;
    public bool buildCollider = false;

    [SerializeField] float metersPerTileRepeat = 128f; // tiling density


    // Build a (N+1)x(N+1) vertex grid; triangles form N*N tiles
    public void BuildChunk(ChunkKey key)
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = $"Chunk {key}" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        int N = ChunkMath.CHUNK_SIZE;
        int vCount = (N + 1) * (N + 1);
        var verts = new Vector3[vCount];
        var cols = new Color[vCount];
        //var uvs = new Vector2[vCount];

        // New for Tile TextureArray
        var uvs = new Vector2[vCount];
        var uv2s = new Vector2[vCount];

        // world-space origin of this chunk in grid coords  
        int gx0 = key.cx * N;
        int gy0 = key.cy * N;

        // vertices
        int vi = 0;
        for (int y = 0; y <= N; y++)
        {
            for (int x = 0; x <= N; x++)
            {
                int gx = gx0 + x;
                int gy = gy0 + y;

                float h = WorldGen.GetHeightM(gx, gy);
                verts[vi] = new Vector3(x * ChunkMath.TILE_SIZE, h, y * ChunkMath.TILE_SIZE);

                // color by type at the nearest cell (clamp to N-1 to avoid out-of-range)
                int cx = Mathf.Clamp(x, 0, N - 1);
                int cy = Mathf.Clamp(y, 0, N - 1);
                //float hc = WorldGen.GetHeightM(gx0 + cx, gy0 + cy);
                //var type = WorldGen.GetTypeAt(gx0 + cx, gy0 + cy, hc);

                // Pick a layer index from the NEAREST cell (or choose a rule you prefer)
                int cxn = Mathf.Clamp(x, 0, N - 1);
                int cyn = Mathf.Clamp(y, 0, N - 1);
                float hc = WorldGen.GetHeightM(gx0 + cxn, gy0 + cyn);
                var type = WorldGen.GetTypeAt(gx0 + cxn, gy0 + cyn, hc);
                int layerIndex = TerrainLayers.IndexFor(type);


                //Debug.Log(type);
                cols[vi] = WorldGen.ColorFor(type);
                //uvs[vi] = new Vector2((float)x / N, (float)y / N);

                // world coords for this vertex (relative to world)
                float wx = (gx) * ChunkMath.TILE_SIZE;
                float wy = (gy) * ChunkMath.TILE_SIZE;
                uvs[vi] = new Vector2(wx / metersPerTileRepeat, wy / metersPerTileRepeat);

                // Store normalized index in uv2.x (0..1). Support up to 256 layers by encoding /255.
                uv2s[vi] = new Vector2(layerIndex / 255f, 0f);

                vi++;
            }
        }

        // triangles
        int quadCount = N * N;
        var tris = new int[quadCount * 6];
        int ti = 0;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int v00 = y * (N + 1) + x;
                int v10 = v00 + 1;
                int v01 = v00 + (N + 1);
                int v11 = v01 + 1;

                // two triangles per quad
                tris[ti++] = v00; tris[ti++] = v01; tris[ti++] = v10;
                tris[ti++] = v10; tris[ti++] = v01; tris[ti++] = v11;
            }
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.colors = cols;
        _mesh.uv = uvs;

        // New for Tile TextureArray
        _mesh.uv2 = uv2s;


        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        if (buildCollider)
        {
            if (_collider == null) _collider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
            _collider.sharedMesh = null;        // ensure refresh
            _collider.sharedMesh = _mesh;       // note: collider build is expensive; avoid frequent rebuilds
        }
        else if (_collider != null)
        {
            _collider.sharedMesh = null;
            Destroy(_collider);
            _collider = null;
        }
    }
}
