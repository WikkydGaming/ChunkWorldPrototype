using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

//[RequireComponent(typeof(MeshFilter))]
//[RequireComponent(typeof(MeshRenderer))]

public class TestQuad : MonoBehaviour
{
    public Material _material;
    public float metersPerTile = 1.0f;
    public int numTilesX = 3;
    public int numTilesZ = 3;
    public Vector3 meshOrigin = new Vector3(0.0f, 0.0f, 0.0f);

    Mesh _mesh;
    MeshFilter _meshFilter;
    MeshRenderer _meshRenderer;
    GameObject _gameObject;


    Vector3[] vertices;
    Vector3[] normals;
    Vector2[] uvs;
    Vector4[] blendWeights;
    Vector4[] indexLayers;

    int[] triangles;



    void Awake()
    {
        var _gameObject = this.gameObject;
        var _meshFilter = _gameObject.AddComponent<MeshFilter>();
        var _meshRenderer= _gameObject.AddComponent<MeshRenderer>();

        _meshRenderer.sharedMaterial = _material;

        _mesh = _meshFilter.sharedMesh = new Mesh();
        _mesh.name = "ChunkMesh";
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    }

    //Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var vertexCount = (numTilesX + 1) * (numTilesZ + 1);
        vertices = new Vector3[vertexCount];
        normals = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        blendWeights = new Vector4[vertexCount];
        indexLayers = new Vector4[vertexCount];
        //uv2s = new Vector2[vertexCount];
        
        triangles = new int[numTilesX * numTilesZ * 2 * 3]; // arraySize is the number of quads. 2 triangles per quad. 3 vertices per triangle.
        var curVertIndx = 0;

        for (float z = meshOrigin.z; z <= metersPerTile * numTilesZ; z += metersPerTile)
        {
            for (float x = meshOrigin.x; x <= metersPerTile * numTilesX; x += metersPerTile)
            {
                vertices[curVertIndx] = new Vector3(x, 0, z);
                normals[curVertIndx] = new Vector3(0, 1, 0);
                //uvs[curVertIndx++] = new Vector2(x / (metersPerTile * numTilesX), z / (metersPerTile * numTilesZ));

                //if (z == 0 || z == 1)
                    //slice = 0.0;
                uvs[curVertIndx] = new Vector2(x / metersPerTile, z / metersPerTile);
                //blendWeights[curVertIndx] = new Vector4(0.75f, 0.25f, 0.0f, 0.0f);
                indexLayers[curVertIndx] = new Vector4(0.0f, 2.0f, 0.0f, 0.0f);
                curVertIndx++;

            }
        }

        curVertIndx = 0;
        for (float z = meshOrigin.z; z <= metersPerTile * numTilesZ; z += metersPerTile)
        {
            for (float x = meshOrigin.x; x <= metersPerTile * numTilesX; x += metersPerTile)
            {
                if (x == 0.0f || x == 1.0f )
                    blendWeights[curVertIndx] = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
                else if (x == 2.0f)
                    blendWeights[curVertIndx] = new Vector4(0.5f, 0.5f, 0.0f, 0.0f);
                else if (x == 3.0f)
                    blendWeights[curVertIndx] = new Vector4(0.0f, 1.0f, 0.0f, 0.0f);

                curVertIndx++;
            }
        }

        blendWeights[15] = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
        blendWeights[14] = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
        blendWeights[10] = new Vector4(0.0f, 1.00f, 0.0f, 0.0f);


        curVertIndx = 0;
        for (int z = 0; z < numTilesZ; z++)
        {
            for (int x = 0; x < numTilesX; x++)
            {
                //triangles[curVertIndx++] = z * (numTilesX + 1) + x;
                //triangles[curVertIndx++] = z * (numTilesX + 1) + (x + 1);
                //triangles[curVertIndx++] = (z + 1) * (numTilesX + 1) + x;

                //triangles[curVertIndx++] = z * (numTilesZ + 1) + (x + 1);
                //triangles[curVertIndx++] = (z + 1) * (numTilesZ + 1) + (x + 1);
                //triangles[curVertIndx++] = (z + 1) * (numTilesZ + 1) + x;

                triangles[curVertIndx++] = z * (numTilesX + 1) + x;
                triangles[curVertIndx++] = (z + 1) * (numTilesX + 1) + x;
                triangles[curVertIndx++] = z * (numTilesX + 1) + (x + 1);

                triangles[curVertIndx++] = (z + 1) * (numTilesX + 1) + x;
                triangles[curVertIndx++] = (z + 1) * (numTilesZ + 1) + (x + 1);
                triangles[curVertIndx++] = z * (numTilesX + 1) + (x + 1);



                //triangles[curVertIndx++] = z * (numTilesX + 1) + (x + 1);
                //triangles[curVertIndx++] = (z + 1) * (numTilesX + 1) + x;

                //triangles[curVertIndx++] = z * (numTilesZ + 1) + (x + 1);
                //triangles[curVertIndx++] = (z + 1) * (numTilesZ + 1) + (x + 1);
                //triangles[curVertIndx++] = (z + 1) * (numTilesZ + 1) + x;

            }
        }


        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.normals = normals;

        //_mesh.uv = uvs;
        //_mesh.uv2 = uv2s;
        //_mesh.colors = cols;

        List<Vector2> uvList = new List<Vector2>(uvs);
        List<Vector4> blendWeightsList = new List<Vector4>(blendWeights);
        List<Vector4> indexLayersList = new List<Vector4>(indexLayers);

        _mesh.SetUVs(0, uvList);
        _mesh.SetUVs(1, blendWeightsList);
        _mesh.SetUVs(2, indexLayersList);
        //_mesh.SetUVs(1, uv2s);

        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
