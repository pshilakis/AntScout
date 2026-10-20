using System.IO;
using AntScout.Backend;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor utility for managing PlayFab backend configuration assets.
    /// Provides 1-click creation, pinging, and inspection of PlayFabConfiguration.
    /// </summary>
    public static class PlayFabEditorUtility
    {
        private const string ConfigDirectory = "Assets/Settings/Resources";
        private const string ConfigAssetPath = "Assets/Settings/Resources/PlayFabConfiguration.asset";

        [MenuItem("AntScout/Backend/Create or Select PlayFab Configuration", false, 150)]
        public static PlayFabConfiguration GetOrCreatePlayFabConfiguration()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayFabConfiguration>(ConfigAssetPath);
            if (config == null)
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                config = ScriptableObject.CreateInstance<PlayFabConfiguration>();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[PlayFabEditorUtility] Successfully created PlayFabConfiguration asset at '{ConfigAssetPath}'!");
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            return config;
        }
    }
}
