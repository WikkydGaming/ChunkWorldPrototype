using UnityEngine;

public static class ChunkMath
{
    public const int CHUNK_SIZE = 64;     // tiles per chunk (1m per tile)
    public const float TILE_SIZE = 1f;    // meters per tile

    public static ChunkKey KeyFromWorld(float wx, float wz)
    {
        int gx = Mathf.FloorToInt(wx / TILE_SIZE);
        int gy = Mathf.FloorToInt(wz / TILE_SIZE);
        int cx = Mathf.FloorToInt((float)gx / CHUNK_SIZE);
        int cy = Mathf.FloorToInt((float)gy / CHUNK_SIZE);
        return new ChunkKey(cx, cy);
    }

    public static Vector2Int LocalFromWorld(float wx, float wz)
    {
        int gx = Mathf.FloorToInt(wx / TILE_SIZE);
        int gy = Mathf.FloorToInt(wz / TILE_SIZE);
        int lx = gx - Mathf.FloorToInt((float)gx / CHUNK_SIZE) * CHUNK_SIZE;
        int ly = gy - Mathf.FloorToInt((float)gy / CHUNK_SIZE) * CHUNK_SIZE;
        return new Vector2Int(lx, ly); // 0..CHUNK_SIZE-1
    }

    public static Vector3 ChunkOrigin(ChunkKey key)
    {
        return new Vector3(key.cx * CHUNK_SIZE * TILE_SIZE, 0f, key.cy * CHUNK_SIZE * TILE_SIZE);
    }
}
