#if UNITY_EDITOR
using System;
using AntScout.FogOfWar.Config;
using AntScout.FogOfWar.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AntScout.FogOfWar.Editor
{
    /// <summary>
    /// Editor utility providing 1-click configuration for the URP Fog of War FullScreenPassRendererFeature,
    /// material assignment, and scene hierarchy setup without tedious manual inspector wiring.
    /// </summary>
    public static class FogOfWarSetupUtility
    {
        private const string RendererAssetPath = "Assets/Settings/PC_Renderer.asset";
        private const string MaterialAssetPath = "Assets/Game Assets/Materials/M_FogOfWar_Fullscreen.mat";
        private const string DefinitionAssetPath = "Assets/Game Assets/FogOfWarDefinition_Default.asset";

        [MenuItem("AntScout/Fog of War/Setup Renderer Feature & Scene", priority = 100)]
        public static void SetupRendererFeatureAndScene()
        {
            SetupRendererFeature();
            SetupSceneManager();
        }

        [MenuItem("AntScout/Fog of War/Configure Renderer Feature Only", priority = 101)]
        public static void SetupRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererAssetPath);
            if (rendererData == null)
            {
                Debug.LogError($"[FogOfWarSetupUtility] Could not find ScriptableRendererData at '{RendererAssetPath}'!");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
            if (material == null)
            {
                Debug.LogError($"[FogOfWarSetupUtility] Could not find Material at '{MaterialAssetPath}'!");
                return;
            }

            // Check if feature already exists
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is FullScreenPassRendererFeature fullScreenPass && feature.name == "FogOfWar_Fullscreen")
                {
                    fullScreenPass.passMaterial = material;
                    fullScreenPass.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
                    fullScreenPass.requirements = ScriptableRenderPassInput.Depth;
                    fullScreenPass.fetchColorBuffer = true;
                    EditorUtility.SetDirty(rendererData);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[FogOfWarSetupUtility] FogOfWar_Fullscreen renderer feature was already present; verified and refreshed its configuration.");
                    return;
                }
            }

            // Create new feature sub-asset
            var newFeature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            newFeature.name = "FogOfWar_Fullscreen";
            newFeature.passMaterial = material;
            newFeature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            newFeature.requirements = ScriptableRenderPassInput.Depth;
            newFeature.fetchColorBuffer = true;

            AssetDatabase.AddObjectToAsset(newFeature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newFeature, out _, out long localId);

            var so = new SerializedObject(rendererData);
            so.Update();

            var featuresProp = so.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = newFeature;

            var mapProp = so.FindProperty("m_RendererFeatureMap");
            mapProp.arraySize++;
            mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;

            so.ApplyModifiedProperties();
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            Debug.Log("[FogOfWarSetupUtility] Successfully added FogOfWar_Fullscreen to PC_Renderer.asset!");
        }

        [MenuItem("AntScout/Fog of War/Add Fog Manager to Scene", priority = 102)]
        public static void SetupSceneManager()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
            var definition = AssetDatabase.LoadAssetAtPath<FogOfWarDefinition>(DefinitionAssetPath);
            if (definition == null)
            {
                Debug.LogError($"[FogOfWarSetupUtility] Could not find FogOfWarDefinition at '{DefinitionAssetPath}'!");
                return;
            }

            var existingManager = UnityEngine.Object.FindFirstObjectByType<FogOfWarManager>();
            if (existingManager != null)
            {
                var existingSo = new SerializedObject(existingManager);
                existingSo.Update();
                existingSo.FindProperty("_config").objectReferenceValue = definition;
                existingSo.FindProperty("_fogMaterial").objectReferenceValue = material;
                existingSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(existingManager);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(existingManager.gameObject.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(existingManager.gameObject.scene);
                Debug.Log("[FogOfWarSetupUtility] FogOfWarManager already exists in scene; updated its references and saved scene.");
                return;
            }

            var managersGo = GameObject.Find("[ Managers ]");
            var managerGo = new GameObject("[ Fog of War Manager ]");
            if (managersGo != null)
            {
                managerGo.transform.SetParent(managersGo.transform);
            }

            var manager = managerGo.AddComponent<FogOfWarManager>();
            var so = new SerializedObject(manager);
            so.Update();
            so.FindProperty("_config").objectReferenceValue = definition;
            so.FindProperty("_fogMaterial").objectReferenceValue = material;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(managerGo, "Create Fog of War Manager");
            EditorUtility.SetDirty(managerGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(managerGo.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(managerGo.scene);
            Debug.Log("[FogOfWarSetupUtility] Successfully instantiated and configured [ Fog of War Manager ], and saved scene.");
        }
    }
}
#endif
