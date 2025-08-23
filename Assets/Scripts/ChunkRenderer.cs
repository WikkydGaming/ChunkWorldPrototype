using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]


public class ChunkRenderer : MonoBehaviour
{
    Mesh _mesh;
    MeshCollider _collider;
    public bool buildCollider = false;

    [SerializeField] float metersPerTileRepeat = 2f; // tiling density
    [SerializeField, Range(0.05f, 1.0f)] float blendRadiusTiles = 0.35f; // edge softness in tiles


    // Build a (N+1)x(N+1) vertex grid; triangles form N*N tiles
    public void BuildChunk(ChunkKey key)
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = $"Chunk {key}" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        int chunkSize = ChunkMath.CHUNK_SIZE;
        float tileSize = ChunkMath.TILE_SIZE;

        // ORIGINAL VERT COUNT WITH JUST TWO TRIANGLES PER TILE
        //int vertCount = (chunkSize + 1) * (chunkSize + 1);

        // NEW VERT COUNT WITH 4 TRIANGLES PER TILE
        int vertCount = (2 * chunkSize + 1) * (chunkSize) + (chunkSize + 1);

        Debug.Log("Vert Count: " + vertCount);

        var verts = new Vector3[vertCount];
        //var cols = new Color[vCount];
        //var uvs = new Vector2[vCount];

        // New for Tile TextureArray
        var uvs = new Vector2[vertCount];
        var uv2s = new Vector2[vertCount];
        var vertBlendWeights = new Vector4[vertCount];
        var vertLayerIndices = new Vector4[vertCount];

        // world-space origin of this chunk in grid coords  
        int gx0 = key.cx * chunkSize;
        int gz0 = key.cz * chunkSize;

        // DEBUG CODE
        Debug.Log("CHUNK ORIGIN COORDS GXO: " + gx0 + " GZ0: " + gz0);   
        DebugWrldGenStrategy dbgWrldGenStrat = new DebugWrldGenStrategy(gx0, gz0, chunkSize);
        WorldGenerator worldGenerator = new WorldGenerator(dbgWrldGenStrat);
        // END DEBUG CODE

        // Texture Array Sketchpad
        // 



        // build vertex array
        int vi = 0;
        //float layerIndex;

        Debug.Log("Chunksize: " + chunkSize);

        // X and Z refer to Tile Global Tile Coordinates
        // This loop builds the mesh in local X&Z mesh coordinates but the Y component
        // is built by converting those X/Z coords into World coordingates then
        // computing Y from a Perlin function in World Space
        for (int z = 0; z <= chunkSize; z++)  // Refactor?
        {
            for (int x = 0; x <= chunkSize; x++)
            {
                Debug.Log("START VI " + vi);

                // Compute the World Space X and Z coords of the current 
                int gx = gx0 + x;
                int gz = gz0 + z;

                // ORIGINAL CODE 
                //float h = WorldGen.GetHeightM(gx, gy);
                //verts[vi] = new Vector3(x * ChunkMath.TILE_SIZE, h, z * ChunkMath.TILE_SIZE);
                // END ORIGINAL CODE

                // DEBUG CODE
                float h = worldGenerator.GetVertexHeight(gx, gz);
                verts[vi] = new Vector3(x * tileSize, h, z * tileSize);

                Debug.Log("Vert " + vi + " is " + verts[vi] + " X: " + x + " Z: " + z);

                // world coords for this vertex (relative to world)
                float wx = gx * tileSize;
                float wy = gz * tileSize;
                uvs[vi] = new Vector2(wx / metersPerTileRepeat, wy / metersPerTileRepeat);

                vi++;

                // Now do center point of the tile if we are not on the last row or last
                // column of vertices.

                bool xEqualsCS;


                if (x == chunkSize)
                {
                    xEqualsCS = true;
                }
                else
                {
                    xEqualsCS = false;
                }
                Debug.Log("xEqualsCS: " + xEqualsCS);

                if ( ( x != chunkSize ) && ( z != chunkSize ) )
                {
                    h = worldGenerator.GetVertexHeight(gx + tileSize / 2.0f, gz + tileSize / 2.0f);
                    verts[vi] = new Vector3(x + tileSize / 2.0f, h, z + tileSize / 2.0f);

                    Debug.Log("Vert " + vi + " is " + verts[vi] + " X: " + x + " Z: " + z);

                    wx = (gx + 0.5f) * tileSize;
                    wy = (gz + 0.5f) * tileSize;
                    uvs[vi] = new Vector2(wx / metersPerTileRepeat, wy / metersPerTileRepeat);

                    vi++;
                }
                // END DEBUG CODE


                // color by type at the nearest cell (clamp to N-1 to avoid out-of-range)
                //int cx = Mathf.Clamp(x, 0, chunkSize - 1);
                //int cz = Mathf.Clamp(z, 0, chunkSize - 1);
                //float hc = WorldGen.GetHeightM(gx0 + cx, gy0 + cy);
                //var type = WorldGen.GetTypeAt(gx0 + cx, gy0 + cy, hc);

                // Pick a layer index from the NEAREST cell (or choose a rule you prefer)
                //int cxn = Mathf.Clamp(x, 0, chunkSize - 1);
                //int czn = Mathf.Clamp(z, 0, chunkSize - 1);
                //float hc = WorldGen.GetHeightM(gx0 + cxn, gz0 + czn);
                //var type = WorldGen.GetTypeAt(gx0 + cxn, gz0 + czn, hc);

                //Debug.Log(type);
                //cols[vi] = WorldGen.ColorFor(type);
                //uvs[vi] = new Vector2((float)x / N, (float)y / N);

                // world coords for this vertex (relative to world)
                //float wx = (gx) * ChunkMath.TILE_SIZE;
                //float wy = (gz) * ChunkMath.TILE_SIZE;
                //uvs[vi] = new Vector2(wx / metersPerTileRepeat, wy / metersPerTileRepeat);

                // DEPRECATED
                // Store normalized index in uv2.x (0..1). Support up to 256 layers by encoding /255.
                //uv2s[vi] = new Vector2(layerIndex / 255f, 0f);

                //vi++;
            }
        }

        //float[] vertTextures = new float[ChunkMath.numberPossibleTexLayer];
        //Array.Clear(vertTextures, 0, vertTextures.Length);

        // This loop will iterate over every tile in the chunk. And for each of the 4
        // vertices of each tile, set the up to 4 textures to use and their blend weights. 
        // 
        SortedDictionary<float, uint> vertTextures = new SortedDictionary<float, uint>();
        vertTextures.Clear();

        vi = 0;

        for (int z = 0; z <= chunkSize; z++)
        {
            for (int x = 0; x <= chunkSize; x++)
            {
                int gx = gx0 + x;
                int gz = gz0 + z;

                //int cxn = Mathf.Clamp(x, 0, chunkSize - 1);
                //int czn = Mathf.Clamp(z, 0, chunkSize - 1);

                // Get current tile type and add to vertTextures
                // ORIGINAL CODE
                //float hc = WorldGen.GetHeightM(gx, gz);
                //var type = WorldGen.GetTypeAt(gx, gz, hc);
                //vertTextures.Add((float)type, 1);

                ////
                ////vertLayerIndices[type] += 1;

                ////layerIndex = TerrainLayers.IndexFor(type);

                //// Get left adjacent type and add it to vertTextures count.
                //hc = WorldGen.GetHeightM(gx-1, gz);
                //type = WorldGen.GetTypeAt(gx -1, gz, hc);
                //if (vertTextures.ContainsKey((float)type))
                //    vertTextures[(float)type] += 1;
                //else vertTextures.Add((float)type, 1);

                //// Get bottom left adjacent type and add it to vertTextures count.
                //hc = WorldGen.GetHeightM(gx - 1, gz - 1);
                //type = WorldGen.GetTypeAt(gx - 1, gz - 1, hc);
                //if (vertTextures.ContainsKey((float)type))
                //    vertTextures[(float)type] += 1;
                //else vertTextures.Add((float)type, 1);

                //// Get bottom adjacent type and add it to vertTextures count.
                //hc = WorldGen.GetHeightM(gx, gz - 1);
                //type = WorldGen.GetTypeAt(gx, gz - 1, hc);
                //if (vertTextures.ContainsKey((float)type))
                //    vertTextures[(float)type] += 1;
                //else vertTextures.Add((float)type, 1);

                //switch ( vertTextures.Count)
                //{
                //    case 1:
                //        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                //        vertLayerIndices[vi].y = 0.0f;
                //        vertLayerIndices[vi].z = 0.0f;
                //        vertLayerIndices[vi].w = 0.0f;

                //        vertBlendWeights[vi].x = 1.0f;
                //        vertBlendWeights[vi].y = 0.0f;
                //        vertBlendWeights[vi].z = 0.0f;
                //        vertBlendWeights[vi].w = 0.0f;
                //        break;
                //    case 2:
                //        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                //        vertLayerIndices[vi].y = vertTextures.ElementAt(1).Key;
                //        vertLayerIndices[vi].z = 0.0f;
                //        vertLayerIndices[vi].w = 0.0f;

                //        vertBlendWeights[vi].x = 0.5f;
                //        vertBlendWeights[vi].y = 0.5f;
                //        vertBlendWeights[vi].z = 0.0f;
                //        vertBlendWeights[vi].w = 0.0f;
                //        break;
                //    case 3:
                //        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                //        vertLayerIndices[vi].y = vertTextures.ElementAt(1).Key;
                //        vertLayerIndices[vi].z = vertTextures.ElementAt(2).Key;
                //        vertLayerIndices[vi].w = 0.0f;

                //        vertBlendWeights[vi].x = 1.0f / 3.0f;
                //        vertBlendWeights[vi].y = 1.0f / 3.0f;
                //        vertBlendWeights[vi].z = 1.0f - 2.0f / 3.0f;
                //        vertBlendWeights[vi].w = 0.0f;
                //        break;
                //    case 4:
                //        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                //        vertLayerIndices[vi].y = vertTextures.ElementAt(1).Key;
                //        vertLayerIndices[vi].z = vertTextures.ElementAt(2).Key;
                //        vertLayerIndices[vi].w = vertTextures.ElementAt(3).Key;

                //        vertBlendWeights[vi].x = 0.25f;
                //        vertBlendWeights[vi].y = 0.25f;
                //        vertBlendWeights[vi].z = 0.25f;
                //        vertBlendWeights[vi].w = 0.25f;
                //        break;
                //    default:
                //        Debug.LogError("Impossible case with switch for vertTextures");
                //        break;
                //}

                //// END ORIGINAL CODE
                ///


                // DEBUG CODE
                var type = worldGenerator.GetTileTerrainType(gx, gz);
                vertTextures.Add((float)type, 1);

                //
                //vertLayerIndices[type] += 1;

                //layerIndex = TerrainLayers.IndexFor(type);

                // Get left adjacent type and add it to vertTextures count.
                type = worldGenerator.GetTileTerrainType(gx - 1, gz);
                if (vertTextures.ContainsKey((float)type))
                    vertTextures[(float)type] += 1;
                else vertTextures.Add((float)type, 1);

                // Get bottom left adjacent type and add it to vertTextures count.
                //type = worldGenerator.GetTileTerrainType(gx - 1, gz - 1);
                //if (vertTextures.ContainsKey((float)type))
                //    vertTextures[(float)type] += 1;
                //else vertTextures.Add((float)type, 1);

                // Get bottom adjacent type and add it to vertTextures count.
                type = worldGenerator.GetTileTerrainType(gx, gz - 1);
                if (vertTextures.ContainsKey((float)type))
                    vertTextures[(float)type] += 1;
                else vertTextures.Add((float)type, 1);

                switch (vertTextures.Count)
                {
                    case 1:
                        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                        vertLayerIndices[vi].y = 0.0f;
                        vertLayerIndices[vi].z = 0.0f;
                        vertLayerIndices[vi].w = 0.0f;

                        vertBlendWeights[vi].x = 1.0f;
                        vertBlendWeights[vi].y = 0.0f;
                        vertBlendWeights[vi].z = 0.0f;
                        vertBlendWeights[vi].w = 0.0f;
                        break;
                    case 2:
                        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                        vertLayerIndices[vi].y = vertTextures.ElementAt(1).Key;
                        vertLayerIndices[vi].z = 0.0f;
                        vertLayerIndices[vi].w = 0.0f;

                        vertBlendWeights[vi].x = 0.5f;
                        vertBlendWeights[vi].y = 0.5f;
                        vertBlendWeights[vi].z = 0.0f;
                        vertBlendWeights[vi].w = 0.0f;
                        break;
                    case 3:
                        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                        vertLayerIndices[vi].y = vertTextures.ElementAt(1).Key;
                        vertLayerIndices[vi].z = vertTextures.ElementAt(2).Key;
                        vertLayerIndices[vi].w = 0.0f;

                        vertBlendWeights[vi].x = 1.0f / 3.0f;
                        vertBlendWeights[vi].y = 1.0f / 3.0f;
                        vertBlendWeights[vi].z = 1.0f / 3.0f;
                        vertBlendWeights[vi].w = 0.0f;
                        break;
                    case 4:
                        vertLayerIndices[vi].x = vertTextures.ElementAt(0).Key;
                        vertLayerIndices[vi].y = vertTextures.ElementAt(1).Key;
                        vertLayerIndices[vi].z = vertTextures.ElementAt(2).Key;
                        vertLayerIndices[vi].w = vertTextures.ElementAt(3).Key;

                        vertBlendWeights[vi].x = 0.25f;
                        vertBlendWeights[vi].y = 0.25f;
                        vertBlendWeights[vi].z = 0.25f;
                        vertBlendWeights[vi].w = 0.25f;
                        break;
                    default:
                        Debug.LogError("Impossible case with switch for vertTextures");
                        break;
                }



                // END DEBUG CODE

                //Debug.Log("Layers " + vertLayerIndices[vi].ToString());
                //Debug.Log("Weights " + vertBlendWeights[vi].ToString());
                vertTextures.Clear();   
                
                // Calculate the textures to blend. Can be up to 4. One per adjacent tile type.
                // This is hard coded for now because we only have 3 texture types.
                //vertLayerIndices[vi] = new Vector4(0.0f, 1.0f, 2.0f, 0.0f);
                vi++;
            }
        }

        // Check neighbors of interior tiles

        // This is hard coded for now because we only have 3 texture types.
        //vertLayerIndices[vi] = new Vector4(0.0f, 1.0f, 2.0f, 0.0f);



        // triangles
        // ORIGINAL TRIANGLES CODE
        //int quadCount = chunkSize * chunkSize;
        //var tris = new int[quadCount * 6];
        //int ti = 0;
        //for (int z = 0; z < chunkSize; z++)
        //{
        //    for (int x = 0; x < chunkSize; x++)
        //    {
        //        int v00 = z * (chunkSize + 1) + x;
        //        int v10 = v00 + 1;
        //        int v01 = v00 + (chunkSize + 1);
        //        int v11 = v01 + 1;

        //        // two triangles per quad
        //        tris[ti++] = v00; tris[ti++] = v01; tris[ti++] = v10;
        //        tris[ti++] = v10; tris[ti++] = v01; tris[ti++] = v11;
        //    }
        //}
        // END ORIGINAL TRIANGLES CODE


        // triangles
        int quadCount = chunkSize * chunkSize;
        var tris = new int[quadCount * 12];
        int ti = 0;

        Debug.Log("Tris count: " + quadCount * 12);
        for (int z = 0; z < chunkSize; z++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                // Create vars for the 5 vertices of a tile.
                int v00 = z * (2 * chunkSize + 1) + 2*x;
                int v10 = v00 + 2;
                int vM = v00 + 1;

                int v01;
                int v11;

                if (z < chunkSize - 1)
                {

                    v01 = v00 + (2 * chunkSize + 1);
                    v11 = v01 + 2;
                    Debug.Log("A V00: " + v00 + " V10: " + v10 + " V01: " + v01 + " V11: " + v11 + " VM: " + vM);
                }
                else
                {
                    v01 = v00 + 2*(chunkSize - x) + x + 1;
                    v11 = v01 + 1;
                    Debug.Log("B V00: " + v00 + " V10: " + v10 + " V01: " + v01 + " V11: " + v11 + " VM: " + vM);
                }


                // Debug.Log("V00: " + v00 + " V10: " + v10 + " V01: " + v01 + " V11: " + v11 + " VM: " + vM);
                // four triangles per quad
                tris[ti++] = v00; tris[ti++] = vM; tris[ti++] = v10;
                tris[ti++] = v00; tris[ti++] = v01; tris[ti++] = vM;
                tris[ti++] = v01; tris[ti++] = v11; tris[ti++] = vM;
                tris[ti++] = vM; tris[ti++] = v11; tris[ti++] = v10;
            }
            Debug.Log("END X row " + z);
        }


        _mesh.Clear();
        _mesh.vertices = verts;
        //_mesh.colors = cols;
        //_mesh.uv = uvs;

        List<Vector2> uvList = new List<Vector2>(uvs);
        List<Vector4> blendWeightsList = new List<Vector4>(vertBlendWeights);
        List<Vector4> indexLayersList = new List<Vector4>(vertLayerIndices);

        _mesh.SetUVs(0, uvList);
        _mesh.SetUVs(1, blendWeightsList);
        _mesh.SetUVs(2, indexLayersList);


        // New for Tile TextureArray
        //_mesh.uv2 = uv2s;


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
