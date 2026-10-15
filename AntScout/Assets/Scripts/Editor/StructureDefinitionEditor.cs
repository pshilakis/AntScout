#if UNITY_EDITOR
using AntScout.Swarm.Nest;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Custom Tabbed Inspector for <see cref="StructureDefinition"/>.
    /// Separates Identity & Stats from Auras across 2 clean tabs,
    /// while preserving Unity's standard native array list rendering for Auras.
    /// </summary>
    [CustomEditor(typeof(StructureDefinition))]
    public sealed class StructureDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _structureNameProp;
        private SerializedProperty _iconProp;
        private SerializedProperty _maxHealthProp;
        private SerializedProperty _defenseProp;
        private SerializedProperty _aurasProp;

        private int _selectedTab = 0;
        private static readonly string[] TabNames = new string[]
        {
            "Identity & Stats",
            "Auras"
        };

        private void OnEnable()
        {
            _structureNameProp = serializedObject.FindProperty("_structureName");
            _iconProp = serializedObject.FindProperty("_icon");
            _maxHealthProp = serializedObject.FindProperty("_maxHealth");
            _defenseProp = serializedObject.FindProperty("_defense");
            _aurasProp = serializedObject.FindProperty("_auras");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, GUILayout.Height(26));
            EditorGUILayout.Space(8);

            switch (_selectedTab)
            {
                case 0:
                    DrawIdentityAndStatsTab();
                    break;
                case 1:
                    DrawAurasTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIdentityAndStatsTab()
        {
            EditorGUILayout.PropertyField(_structureNameProp);
            EditorGUILayout.PropertyField(_iconProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_maxHealthProp);
            EditorGUILayout.PropertyField(_defenseProp);
        }

        private void DrawAurasTab()
        {
            // Standard, native Unity array list rendering
            EditorGUILayout.PropertyField(_aurasProp, true);
        }
    }
}
#endif
