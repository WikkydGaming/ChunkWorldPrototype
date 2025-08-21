using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public Transform player;              // assign your player/camera
    public Material chunkMaterial;        // simple lit/unlit works; uses vertex colors
    public int loadRadius = 3;            // chunks in each direction (3 => 7x7 loaded)
    public bool buildColliders = false;

    [SerializeField] float metersPerTileRepeat = 2f; // UV tiling
    [SerializeField, Range(0.05f, 1.0f)] float blendRadiusTiles = 0.35f; // edge softness in tiles

    readonly Dictionary<ChunkKey, ChunkRenderer> _active = new();
    readonly Queue<ChunkRenderer> _pool = new();

    void Start()
    {
        if (!player) player = Camera.main?.transform;
        InvokeRepeating(nameof(RefreshWindow), 0f, 0.25f); // quarter-second window refresh
    }

    void RefreshWindow()
    {
        if (!player) return;

        var centerKey = ChunkMath.KeyFromWorld(player.position.x, player.position.z);
        var want = new HashSet<ChunkKey>();

        for (int dy = -loadRadius; dy <= loadRadius; dy++)
        {
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
            {
                var key = new ChunkKey(centerKey.cx + dx, centerKey.cz + dy);
                want.Add(key);
                if (_active.ContainsKey(key)) continue;
                LoadChunk(key);
            }
        }

        // Unload those we no longer want
        var toRemove = new List<ChunkKey>();
        foreach (var kv in _active)
        {
            if (!want.Contains(kv.Key)) toRemove.Add(kv.Key);
        }
        foreach (var k in toRemove) UnloadChunk(k);
    }

    void LoadChunk(ChunkKey key)
    {
        var go = GetOrCreateChunkGO();
        go.name = $"Chunk {key.cx},{key.cz}";
        go.transform.position = ChunkMath.ChunkOriginWorldCoords(key);

        var cr = go.GetComponent<ChunkRenderer>();
        cr.buildCollider = buildColliders; 
        //cr.MetersPerTileRepeat = metersPerTileRepeat;
        //cr.BlendRadiusTiles = blendRadiusTiles;
        cr.BuildChunk(key);

        _active[key] = cr;
    }

    void UnloadChunk(ChunkKey key)
    {
        if (!_active.TryGetValue(key, out var cr)) return;
        _active.Remove(key);
        ReturnChunkGO(cr.gameObject);
    }

    GameObject GetOrCreateChunkGO()
    {
        if (_pool.Count > 0)
        {
            var cr = _pool.Dequeue();
            cr.gameObject.SetActive(true);
            return cr.gameObject;
        }
        var go = new GameObject("Chunk");
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = chunkMaterial;
        go.AddComponent<ChunkRenderer>();
        return go;
    }

    void ReturnChunkGO(GameObject go)
    {
        go.SetActive(false);
        var cr = go.GetComponent<ChunkRenderer>();
        _pool.Enqueue(cr);
    }
}
