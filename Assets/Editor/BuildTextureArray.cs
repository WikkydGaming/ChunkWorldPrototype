using UnityEngine;
using UnityEditor;

public static class BuildTextureArray
{
    [MenuItem("Tools/Terrain/Build Albedo Texture2DArray")]
    public static void BuildAlbedoArray()
    {
        var texs = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        if (texs.Length == 0) { Debug.LogError("Select textures in the Project view first."); return; }

        int w = texs[0].width, h = texs[0].height;
        var fmt = texs[0].format;
        bool mip = texs[0].mipmapCount > 1;

        var array = new Texture2DArray(w, h, texs.Length, fmt, mip, false);
        array.anisoLevel = 8;
        array.wrapMode = TextureWrapMode.Repeat;
        array.filterMode = FilterMode.Trilinear;

        for (int i = 0; i < texs.Length; i++)
        {
            var t = texs[i];
            if (t.width != w || t.height != h) { Debug.LogError($"Size mismatch at {t.name}"); return; }
            for (int m = 0; m < t.mipmapCount; m++)
            {
                Graphics.CopyTexture(t, 0, m, array, i, m);
            }
        }

        var path = EditorUtility.SaveFilePanelInProject("Save Texture2DArray", "AlbedoArray", "asset", "");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(array, path);
            Debug.Log($"Saved Texture2DArray at {path}");
        }
    }
}
