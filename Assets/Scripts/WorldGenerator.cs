using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;

// My first attempt at a simple strategy design pattern

public interface IWorldGenStrategy
{
    public float GetVertexHeight(float gx, float gz);
    public TerrainType GetTileTerrainType(float gx, float gz);
}

public class WorldGenerator
{
    private IWorldGenStrategy strategy = null;

    public WorldGenerator()
    {
        this.strategy = null;
    }

    public WorldGenerator( IWorldGenStrategy strategy )
    {
        bool isNull;

       if ( this.strategy == null ) isNull = true; else isNull = false;
        
        Debug.Log("STRATEGY BEFORE: " +  strategy + " " + isNull);
        this.strategy = strategy;
        Debug.Log("STRATEGY AFTER: " + strategy + " " + isNull);
    }

    public void SetWorldGenStrategy( IWorldGenStrategy strategy )
    {
        this.strategy = strategy;
    }

    public float GetVertexHeight( float gx, float gz )
    {
        //bool isNull;

        //if (strategy == null) isNull = true; else isNull = false;
        //    Debug.Log("STRATEGY BEFORE: " + strategy + " " + isNull);

        if (strategy != null) 
            return strategy.GetVertexHeight(gx, gz);
        else
        {
            Debug.LogError("GetVertexHeight: Interface for World Generation Strategy not set! Coords GX: " + gx + " GZ: " + gz);
            return -1.0f;
        }
    }

    public TerrainType GetTileTerrainType( float gx, float gz )
    {
        if (strategy != null)
            return strategy.GetTileTerrainType(gx, gz);
        else
        {
            Debug.LogError("GetTileTerrainType: Interface for World Generation Strategy not set! Coords GX: " + gx + " GZ: " + gz);
            return TerrainType.Soil;
        }
    }
}


public class DebugWrldGenStrategy : IWorldGenStrategy
{
    private int originX;
    private int originZ;

    private int chunkSize;
    private int centerX;
    private int centerZ;

    public DebugWrldGenStrategy( int originX, int originZ, int chunkSize )
    {
        this.chunkSize = chunkSize;
        this.originX = originX;
        this.originZ = originZ;
        centerX = Mathf.FloorToInt(originX + chunkSize / 2.0f);
        centerZ = Mathf.FloorToInt(originZ + chunkSize / 2.0f);
    }

    public float GetVertexHeight( float gx, float gz )
    {
        return 0.0f;
    }

    public TerrainType GetTileTerrainType( float gx, float gz )
    {
        TerrainType tType;
        
        int dist = Mathf.FloorToInt( Mathf.Sqrt( Mathf.Pow( gx - centerX, 2 ) + Mathf.Pow( gz - centerZ, 2 )));
        
        if (dist > (int)( chunkSize / 2.0f) )
                tType = TerrainType.Soil;
        else if (dist <= (int)( chunkSize / 2.0f ) && dist > (int)( chunkSize / 3.0f ))
            tType = TerrainType.Grass;
        else
            tType = TerrainType.Tree;

        return tType;
    }
}
