public enum TerrainType { Soil, Grass, Tree }

public static class TerrainLayers
{
    // Match the order you used when building the Texture2DArray
    public static int IndexFor(TerrainType t) => t switch
    {
        TerrainType.Soil => 0,
        TerrainType.Tree => 1,
        TerrainType.Grass => 2,
        // add more types here...
        _ => 0
    };
}