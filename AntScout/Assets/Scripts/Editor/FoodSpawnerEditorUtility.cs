using System;
using System.IO;
using AntScout.Config;
using AntScout.Maps;
using AntScout.Resources.Components;
using PGS.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor utility to generate food source prefabs (Sugar Granule, Fallen Fruit),
    /// physical ResourceDefinition assets, RarityDefinitions, and configure ResourceSpawnManager in the active scene.
    /// </summary>
    public static class FoodSpawnerEditorUtility
    {
        private const string ResourcesFolder = "Assets/Game Assets/Resources";
        private const string MaterialsFolder = "Assets/Game Assets/Resources/Materials";
        private const string SugarPrefabPath = "Assets/Game Assets/Resources/Food_SugarGranule.prefab";
        private const string FruitPrefabPath = "Assets/Game Assets/Resources/Food_FallenFruit.prefab";
        private const string SugarMaterialPath = "Assets/Game Assets/Resources/Materials/SugarMaterial.mat";
        private const string FruitMaterialPath = "Assets/Game Assets/Resources/Materials/FruitMaterial.mat";
        private const string CommonRarityPath = "Assets/Game Assets/Resources/Rarity_Common.asset";
        private const string UncommonRarityPath = "Assets/Game Assets/Resources/Rarity_Uncommon.asset";
        private const string SugarDefPath = "Assets/Game Assets/Resources/ResourceDefinition_SugarGranule.asset";
        private const string FruitDefPath = "Assets/Game Assets/Resources/ResourceDefinition_FallenFruit.asset";
        private const string DefaultMapDefPath = "Assets/Game Assets/MapResourceDefinition_Default.asset";

        [MenuItem("AntScout/Resources/Create or Update Food Prefabs", priority = 20)]
        public static void CreateOrUpdateFoodPrefabs()
        {
            EnsureFoldersExist();

            RarityDefinition commonRarity = GetOrCreateRarity(
                CommonRarityPath, "Common", Color.white, HeaderColor.White, false, 60);
            RarityDefinition uncommonRarity = GetOrCreateRarity(
                UncommonRarityPath, "Uncommon", new Color(0.3f, 0.9f, 0.4f), HeaderColor.Green, true, 40);

            ResourceDefinition sugarDef = GetOrCreateResourceDefinition(
                SugarDefPath, "Sugar Granule", commonRarity, new IntRange(15, 25), 1.0f, 5, new Vector2(0.7f, 1.1f));
            ResourceDefinition fruitDef = GetOrCreateResourceDefinition(
                FruitDefPath, "Fallen Fruit", uncommonRarity, new IntRange(60, 90), 1.8f, 15, new Vector2(0.9f, 1.4f));

            Material sugarMat = GetOrCreateMaterial(SugarMaterialPath, new Color(0.92f, 0.96f, 1.0f, 1.0f), 0.85f, 0.1f);
            Material fruitMat = GetOrCreateMaterial(FruitMaterialPath, new Color(0.85f, 0.15f, 0.25f, 1.0f), 0.6f, 0.0f);

            FoodSource sugarPrefab = CreateOrUpdatePrefab(SugarPrefabPath, PrimitiveType.Cube, new Vector3(0.6f, 0.6f, 0.6f), sugarMat);
            FoodSource fruitPrefab = CreateOrUpdatePrefab(FruitPrefabPath, PrimitiveType.Sphere, new Vector3(1.2f, 1.2f, 1.2f), fruitMat);

            // Wire prefabs back into their definitions
            SerializedObject sugarSo = new SerializedObject(sugarDef);
            sugarSo.FindProperty("_prefab").objectReferenceValue = sugarPrefab;
            sugarSo.FindProperty("_rarity").objectReferenceValue = commonRarity;
            sugarSo.ApplyModifiedProperties();

            SerializedObject fruitSo = new SerializedObject(fruitDef);
            fruitSo.FindProperty("_prefab").objectReferenceValue = fruitPrefab;
            fruitSo.FindProperty("_rarity").objectReferenceValue = uncommonRarity;
            fruitSo.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FoodSpawnerEditorUtility] Food prefabs, RarityDefinitions, and ResourceDefinitions successfully generated at '{ResourcesFolder}'.");
        }

        [MenuItem("AntScout/Resources/Setup Resource Spawn Manager in Match Scene", priority = 21)]
        public static void SetupResourceSpawnManagerInScene()
        {
            CreateOrUpdateFoodPrefabs();

            MapResourceDefinition mapDef = ResolveMapResourceDefinition();

            ResourceSpawnManager existingManager = UnityEngine.Object.FindAnyObjectByType<ResourceSpawnManager>();
            GameObject managerObj = existingManager != null ? existingManager.gameObject : new GameObject("[ Resource Spawn Manager ]");

            if (existingManager == null)
            {
                existingManager = managerObj.AddComponent<ResourceSpawnManager>();
                Undo.RegisterCreatedObjectUndo(managerObj, "Create Resource Spawn Manager");
            }
            else
            {
                Undo.RecordObject(existingManager, "Configure Resource Spawn Manager");
            }

            // Nest under [ Managers ] if present in the scene
            GameObject managersRoot = GameObject.Find("[ Managers ]");
            if (managersRoot != null && managerObj.transform.parent != managersRoot.transform)
            {
                managerObj.transform.SetParent(managersRoot.transform);
            }

            // Ensure any MapDefinition in the project is linked with the resolved MapResourceDefinition if unassigned
            string[] mapDefGuids = AssetDatabase.FindAssets("t:MapDefinition");
            foreach (string guid in mapDefGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mapDefinition = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);
                if (mapDefinition != null && mapDefinition.ResourceDefinition == null)
                {
                    SerializedObject mapDefSo = new SerializedObject(mapDefinition);
                    SerializedProperty resProp = mapDefSo.FindProperty("_resourceDefinition");
                    if (resProp != null)
                    {
                        resProp.objectReferenceValue = mapDef;
                        mapDefSo.ApplyModifiedProperties();
                        EditorUtility.SetDirty(mapDefinition);
                        Debug.Log($"[FoodSpawnerEditorUtility] Assigned MapResourceDefinition to MapDefinition '{mapDefinition.name}'.");
                    }
                }
            }

            // Clean up legacy static test cache object if present
            GameObject testCache = GameObject.Find("Food_SugarGranule_TestCache");
            if (testCache != null)
            {
                Undo.DestroyObjectImmediate(testCache);
                Debug.Log("[FoodSpawnerEditorUtility] Cleaned up legacy 'Food_SugarGranule_TestCache'.");
            }

            EditorSceneManager.MarkSceneDirty(managerObj.scene);
            Selection.activeGameObject = managerObj;

            Debug.Log("[FoodSpawnerEditorUtility] Configured [ Resource Spawn Manager ] in active scene with MapResourceDefinition.");
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

        private static RarityDefinition GetOrCreateRarity(
            string path, string rarityName, Color color, HeaderColor headerColor, bool isHighTier, int weight)
        {
            RarityDefinition rarity = AssetDatabase.LoadAssetAtPath<RarityDefinition>(path);
            if (rarity == null)
            {
                rarity = ScriptableObject.CreateInstance<RarityDefinition>();
                rarity.SetRuntimeValues(rarityName, color, headerColor, isHighTier, new ConstantTierWeight(weight));
                AssetDatabase.CreateAsset(rarity, path);
            }
            return rarity;
        }

        private static ResourceDefinition GetOrCreateResourceDefinition(
            string path, string displayName, RarityDefinition rarity, IntRange capacityRange, float harvestDuration, int biomassPerUnit, Vector2 scaleRange)
        {
            ResourceDefinition def = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ResourceDefinition>();
                def.SetRuntimeValues(displayName, rarity, null, capacityRange, harvestDuration, biomassPerUnit, scaleRange);
                AssetDatabase.CreateAsset(def, path);
            }
            return def;
        }

        private static MapResourceDefinition ResolveMapResourceDefinition()
        {
            MapResourceDefinition def = AssetDatabase.LoadAssetAtPath<MapResourceDefinition>(DefaultMapDefPath);
            if (def != null) return def;

            string[] guids = AssetDatabase.FindAssets("t:MapResourceDefinition");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                def = AssetDatabase.LoadAssetAtPath<MapResourceDefinition>(path);
                if (def != null) return def;
            }

            throw new InvalidOperationException(
                $"[FoodSpawnerEditorUtility] Could not find a valid MapResourceDefinition asset at '{DefaultMapDefPath}'. " +
                $"Please create one via Assets -> Create -> AntScout -> Config -> Map Resource Definition.");
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
                    name = Path.GetFileNameWithoutExtension(materialPath)
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

        private static FoodSource CreateOrUpdatePrefab(
            string prefabPath,
            PrimitiveType primitiveType,
            Vector3 localScale,
            Material material)
        {
            GameObject temp = GameObject.CreatePrimitive(primitiveType);
            try
            {
                temp.name = Path.GetFileNameWithoutExtension(prefabPath);
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
                so.FindProperty("_shrinkOnDeplete").boolValue = true;
                so.FindProperty("_minScaleRatio").floatValue = 0.25f;
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
                return savedPrefab.GetComponent<FoodSource>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }
    }
}
