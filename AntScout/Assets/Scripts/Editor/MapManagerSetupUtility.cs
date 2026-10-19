using System.IO;
using AntScout.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor setup utility ensuring MapManager is present, configured, and bound in the active scene.
    /// Strictly adheres to Single Responsibility (SRP).
    /// </summary>
    public static class MapManagerSetupUtility
    {
        [MenuItem("AntScout/Setup/Setup MapManager in Current Scene", priority = 10)]
        public static void SetupMapManagerInScene()
        {
            var existing = Object.FindFirstObjectByType<MapManager>();
            if (existing != null)
            {
                Debug.Log($"[MapManagerSetupUtility] MapManager already exists in scene on '{existing.name}'.");
                EnsureDefinitionAssigned(existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject managersRoot = GameObject.Find("[ Managers ]");
            GameObject mapManagerObj = new GameObject("MapManager");

            if (managersRoot != null)
            {
                mapManagerObj.transform.SetParent(managersRoot.transform, false);
            }

            var mapManager = mapManagerObj.AddComponent<MapManager>();
            EnsureDefinitionAssigned(mapManager);

            Undo.RegisterCreatedObjectUndo(mapManagerObj, "Create MapManager");
            Selection.activeGameObject = mapManagerObj;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[MapManagerSetupUtility] Successfully created and configured MapManager in the active scene!");
        }

        private static void EnsureDefinitionAssigned(MapManager mapManager)
        {
            if (mapManager.ActiveMap != null) return;

            string[] guids = AssetDatabase.FindAssets("t:MapDefinition");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var def = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);
                if (def != null)
                {
                    var so = new SerializedObject(mapManager);
                    SerializedProperty prop = so.FindProperty("_activeMapDefinition");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = def;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(mapManager);
                        Debug.Log($"[MapManagerSetupUtility] Assigned MapDefinition '{def.name}' to MapManager.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[MapManagerSetupUtility] No MapDefinition asset found in the project! Please create one via Assets > Create > AntScout > Maps > Map Definition.");
            }
        }
    }
}
