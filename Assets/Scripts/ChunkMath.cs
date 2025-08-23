using Unity.VisualScripting;
using UnityEngine;

public static class ChunkMath
{
    public const int CHUNK_SIZE = 64;     // tiles per chunk (1m per tile)

    //public const int CHUNK_SIZE = 2;     // tiles per chunk (1m per tile)
    public const float TILE_SIZE = 1f;    // meters per tile
    public const int numberPossibleTexLayer = 30; // Number of possible Texture layers


    public static ChunkKey KeyFromWorld(float wx, float wz)
    {
        // GX and GZ would gives the Tile Coordinates 
        // GX = The Nth tile in the X direction
        // GZ = The Nth tile in the Z direction
        int gx = Mathf.FloorToInt(wx / TILE_SIZE);
        int gz = Mathf.FloorToInt(wz / TILE_SIZE);

        // CX and CZ gives the Chunk Coordinates 
        // CX = The Nth chunk in the X direction
        // CZ = The Nth chunk in the Z direction
        int cx = Mathf.FloorToInt((float)gx / CHUNK_SIZE);
        int cz = Mathf.FloorToInt((float)gz / CHUNK_SIZE);
        return new ChunkKey(cx, cz);
    }

    public static Vector2Int LocalTileCoordsFromWorldCoords(float wx, float wz)
    {
        // Get Tile Coordinates of the input world coordinates
        int gx = Mathf.FloorToInt(wx / TILE_SIZE);
        int gz = Mathf.FloorToInt(wz / TILE_SIZE);

        // Get the local Tile coorinate of the Chunk at wx, z  
        int lx = gx - Mathf.FloorToInt((float)gx / CHUNK_SIZE) * CHUNK_SIZE;
        int lz = gz - Mathf.FloorToInt((float)gz / CHUNK_SIZE) * CHUNK_SIZE;
        return new Vector2Int(lx, lz); // 0..CHUNK_SIZE-1
    }

    // Returns the World Space Origin Coordinates of a Chunk given the Chunk Space Coordinates
    public static Vector3 ChunkOriginWorldCoords(ChunkKey key)
    {
        return new Vector3(key.cx * CHUNK_SIZE * TILE_SIZE, 0f, key.cz * CHUNK_SIZE * TILE_SIZE);
    }
}
