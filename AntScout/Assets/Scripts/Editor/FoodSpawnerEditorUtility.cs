using System;
using System.IO;
using AntScout.Config;
using AntScout.Resources.Components;
using AntScout.Resources.Enums;
using AntScout.Swarm.Nest;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor utility to generate food source prefabs (Sugar Granule, Fallen Fruit) and
    /// spawn test food drops or spawner systems in the active scene.
    /// </summary>
    public static class FoodSpawnerEditorUtility
    {
        private const string ResourcesFolder = "Assets/Game Assets/Resources";
        private const string MaterialsFolder = "Assets/Game Assets/Resources/Materials";
        private const string SugarPrefabPath = "Assets/Game Assets/Resources/Food_SugarGranule.prefab";
        private const string FruitPrefabPath = "Assets/Game Assets/Resources/Food_FallenFruit.prefab";
        private const string SugarMaterialPath = "Assets/Game Assets/Resources/Materials/SugarMaterial.mat";
        private const string FruitMaterialPath = "Assets/Game Assets/Resources/Materials/FruitMaterial.mat";
        private const string DefaultConfigPath = "Assets/Game Assets/ResourceConfig.asset";

        [MenuItem("Tools/AntScout/Resources/Create or Update Food Prefabs", priority = 20)]
        public static void CreateOrUpdateFoodPrefabs()
        {
            EnsureFoldersExist();

            ResourceConfigSO config = ResolveResourceConfig();
            Material sugarMat = GetOrCreateMaterial(SugarMaterialPath, new Color(0.92f, 0.96f, 1.0f, 1.0f), 0.85f, 0.1f);
            Material fruitMat = GetOrCreateMaterial(FruitMaterialPath, new Color(0.85f, 0.15f, 0.25f, 1.0f), 0.6f, 0.0f);

            CreateOrUpdatePrefab(SugarPrefabPath, PrimitiveType.Cube, new Vector3(0.6f, 0.6f, 0.6f), sugarMat, config, ResourceType.SugarGranule);
            CreateOrUpdatePrefab(FruitPrefabPath, PrimitiveType.Sphere, new Vector3(1.2f, 1.2f, 1.2f), fruitMat, config, ResourceType.FallenFruit);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FoodSpawnerEditorUtility] Food prefabs successfully generated at '{ResourcesFolder}'.");
        }

        [MenuItem("Tools/AntScout/Resources/Spawn Test Sugar Cache in Match Scene", priority = 21)]
        public static void SpawnTestSugarCacheInScene()
        {
            CreateOrUpdateFoodPrefabs();

            GameObject sugarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SugarPrefabPath);
            if (sugarPrefab == null)
            {
                throw new InvalidOperationException($"[FoodSpawnerEditorUtility] Failed to load prefab at '{SugarPrefabPath}'.");
            }

            ColonyNest playerNest = null;
            ColonyNest[] nests = UnityEngine.Object.FindObjectsByType<ColonyNest>(FindObjectsSortMode.None);
            for (int i = 0; i < nests.Length; i++)
            {
                if (nests[i].ColonyTeamId == 0)
                {
                    playerNest = nests[i];
                    break;
                }
            }

            if (playerNest == null && nests.Length > 0)
            {
                playerNest = nests[0];
            }

            Vector3 spawnOrigin = playerNest != null ? playerNest.SpawnPosition : Vector3.zero;
            Vector3 forwardOffset = playerNest != null ? playerNest.transform.forward * 9.0f : new Vector3(0f, 0f, 9.0f);
            Vector3 testTargetPos = spawnOrigin + forwardOffset;

            // Snap to nearest walkable node if Astar is present
            if (AstarPath.active != null)
            {
                var nearest = AstarPath.active.GetNearest(testTargetPos, NearestNodeConstraint.Walkable);
                if (nearest.node != null)
                {
                    testTargetPos = (Vector3)nearest.node.position;
                }
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sugarPrefab);
            instance.name = "Food_SugarGranule_TestCache";
            instance.transform.position = testTargetPos;

            Undo.RegisterCreatedObjectUndo(instance, "Spawn Test Sugar Cache");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Debug.Log($"[FoodSpawnerEditorUtility] Spawned test Sugar Granule cache at {testTargetPos} (~9m from player nest).");
        }

        [MenuItem("Tools/AntScout/Resources/Setup Bonanza Spawner in Match Scene", priority = 22)]
        public static void SetupBonanzaSpawnerInScene()
        {
            CreateOrUpdateFoodPrefabs();

            ResourceConfigSO config = ResolveResourceConfig();
            GameObject sugarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SugarPrefabPath);
            GameObject fruitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FruitPrefabPath);

            BonanzaSpawner existingSpawner = UnityEngine.Object.FindAnyObjectByType<BonanzaSpawner>();
            GameObject spawnerObj = existingSpawner != null ? existingSpawner.gameObject : new GameObject("[ Bonanza Spawner ]");

            if (existingSpawner == null)
            {
                existingSpawner = spawnerObj.AddComponent<BonanzaSpawner>();
                Undo.RegisterCreatedObjectUndo(spawnerObj, "Create Bonanza Spawner");
            }
            else
            {
                Undo.RecordObject(existingSpawner, "Configure Bonanza Spawner");
            }

            SerializedObject so = new SerializedObject(existingSpawner);
            so.FindProperty("_config").objectReferenceValue = config;

            SerializedProperty prefabsProp = so.FindProperty("_foodPrefabs");
            prefabsProp.arraySize = 2;
            prefabsProp.GetArrayElementAtIndex(0).objectReferenceValue = sugarPrefab;
            prefabsProp.GetArrayElementAtIndex(1).objectReferenceValue = fruitPrefab;

            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(spawnerObj.scene);
            Selection.activeGameObject = spawnerObj;

            Debug.Log("[FoodSpawnerEditorUtility] Configured [ Bonanza Spawner ] in active scene.");
        }

        private static void EnsureFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game Assets"))
            {
                AssetDatabase.CreateFolder("Assets", "Game Assets");
            }
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/Game Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder(ResourcesFolder, "Materials");
            }
        }

        private static ResourceConfigSO ResolveResourceConfig()
        {
            ResourceConfigSO config = AssetDatabase.LoadAssetAtPath<ResourceConfigSO>(DefaultConfigPath);
            if (config != null) return config;

            string[] guids = AssetDatabase.FindAssets("t:ResourceConfigSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<ResourceConfigSO>(path);
                if (config != null) return config;
            }

            throw new InvalidOperationException(
                $"[FoodSpawnerEditorUtility] Could not find a valid ResourceConfigSO asset at '{DefaultConfigPath}'. " +
                $"Please create one via Assets -> Create -> AntScout -> Config -> ResourceConfig.");
        }

        private static Material GetOrCreateMaterial(string materialPath, Color albedoColor, float smoothness, float metallic)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");

                mat = new Material(litShader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(materialPath)
                };

                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", albedoColor);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", albedoColor);
                }

                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", smoothness);
                }

                if (mat.HasProperty("_Metallic"))
                {
                    mat.SetFloat("_Metallic", metallic);
                }

                AssetDatabase.CreateAsset(mat, materialPath);
            }
            return mat;
        }

        private static void CreateOrUpdatePrefab(
            string prefabPath,
            PrimitiveType primitiveType,
            Vector3 localScale,
            Material material,
            ResourceConfigSO config,
            ResourceType resourceType)
        {
            GameObject temp = GameObject.CreatePrimitive(primitiveType);
            try
            {
                temp.name = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
                temp.transform.localScale = localScale;

                Renderer rend = temp.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = material;
                }

                Collider col = temp.GetComponent<Collider>();
                if (col != null)
                {
                    col.isTrigger = true;
                }

                FoodSource foodSource = temp.AddComponent<FoodSource>();
                SerializedObject so = new SerializedObject(foodSource);
                so.FindProperty("_config").objectReferenceValue = config;
                so.FindProperty("_type").enumValueIndex = (int)resourceType;
                so.FindProperty("_shrinkOnDeplete").boolValue = true;
                so.FindProperty("_minScaleRatio").floatValue = 0.25f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }
    }
}
