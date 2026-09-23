using System.IO;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor utility to generate native Unity Mesh assets for rapid prototyping without external DCC tools.
    /// </summary>
    public static class MeshGeneratorMenu
    {
        [MenuItem("AntScout/Generate Triangle Mesh Asset")]
        public static void GenerateTriangleMesh()
        {
            string folderPath = "Assets/Meshes";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Meshes");
            }

            Mesh mesh = new Mesh
            {
                name = "TriangleMesh"
            };

            // Double-sided flat triangle pointing forward along +Z on the XZ ground plane
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0.0f, 0.0f, 0.6f),   // Tip / Nose
                new Vector3(0.4f, 0.0f, -0.4f),  // Back Right
                new Vector3(-0.4f, 0.0f, -0.4f), // Back Left

                new Vector3(0.0f, 0.0f, 0.6f),   // Bottom Tip
                new Vector3(-0.4f, 0.0f, -0.4f), // Bottom Back Left
                new Vector3(0.4f, 0.0f, -0.4f)   // Bottom Back Right
            };

            int[] triangles = new int[]
            {
                0, 1, 2, // Top face
                3, 4, 5  // Bottom face
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0.5f, 1.0f),
                new Vector2(1.0f, 0.0f),
                new Vector2(0.0f, 0.0f),

                new Vector2(0.5f, 1.0f),
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f)
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,

                Vector3.down,
                Vector3.down,
                Vector3.down
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            string assetPath = $"{folderPath}/TriangleMesh.asset";
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = mesh;
            Debug.Log($"[MeshGenerator] Created triangle mesh asset at '{assetPath}'.");
        }
    }
}
