#if UNITY_EDITOR
using AntScout.Characters;
using PGS.Input.Editor;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Custom Tabbed Inspector for <see cref="CharacterDefinition"/>.
    /// Mirrors the tabbed layout and [ColoredHeader] presentation of CombatActionDefinitionEditor.
    /// Unifies all combat and pheromone trail actions into a single Action Kit tab.
    /// </summary>
    [CustomEditor(typeof(CharacterDefinition))]
    public sealed class CharacterDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _characterNameProp;
        private SerializedProperty _iconProp;

        private SerializedProperty _maxHealthProp;
        private SerializedProperty _defenseProp;
        private SerializedProperty _attackProp;
        private SerializedProperty _baseMoveSpeedProp;
        private SerializedProperty _visionRadiusProp;

        private SerializedProperty _primaryActionProp;
        private SerializedProperty _primaryInputProp;
        private SerializedProperty _secondaryActionProp;
        private SerializedProperty _secondaryInputProp;
        private SerializedProperty _utilityActionProp;
        private SerializedProperty _utilityInputProp;
        private SerializedProperty _recruitmentTrailActionProp;
        private SerializedProperty _recruitmentTrailInputProp;
        private SerializedProperty _alarmTrailActionProp;
        private SerializedProperty _alarmTrailInputProp;

        private int _selectedTab = 0;
        private static readonly string[] TabNames = new string[]
        {
            "Identity & Stats",
            "Action Kit"
        };

        private void OnEnable()
        {
            _characterNameProp = serializedObject.FindProperty("_characterName");
            _iconProp = serializedObject.FindProperty("_icon");

            _maxHealthProp = serializedObject.FindProperty("_maxHealth");
            _defenseProp = serializedObject.FindProperty("_defense");
            _attackProp = serializedObject.FindProperty("_attack");
            _baseMoveSpeedProp = serializedObject.FindProperty("_baseMoveSpeed");
            _visionRadiusProp = serializedObject.FindProperty("_visionRadius");

            _primaryActionProp = serializedObject.FindProperty("_primaryAction");
            _primaryInputProp = serializedObject.FindProperty("_primaryInput");
            _secondaryActionProp = serializedObject.FindProperty("_secondaryAction");
            _secondaryInputProp = serializedObject.FindProperty("_secondaryInput");
            _utilityActionProp = serializedObject.FindProperty("_utilityAction");
            _utilityInputProp = serializedObject.FindProperty("_utilityInput");
            _recruitmentTrailActionProp = serializedObject.FindProperty("_recruitmentTrailAction");
            _recruitmentTrailInputProp = serializedObject.FindProperty("_recruitmentTrailInput");
            _alarmTrailActionProp = serializedObject.FindProperty("_alarmTrailAction");
            _alarmTrailInputProp = serializedObject.FindProperty("_alarmTrailInput");
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
                    DrawActionKitTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Tab 0: Identity & Stats

        private void DrawIdentityAndStatsTab()
        {
            EditorGUILayout.PropertyField(_characterNameProp);
            EditorGUILayout.PropertyField(_iconProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_maxHealthProp);
            EditorGUILayout.PropertyField(_defenseProp);
            EditorGUILayout.PropertyField(_attackProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_baseMoveSpeedProp);
            EditorGUILayout.PropertyField(_visionRadiusProp);
        }

        #endregion

        #region Tab 1: Action Kit

        private void DrawActionKitTab()
        {
            DrawSlotGroup(_primaryActionProp, _primaryInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_secondaryActionProp, _secondaryInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_utilityActionProp, _utilityInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_recruitmentTrailActionProp, _recruitmentTrailInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_alarmTrailActionProp, _alarmTrailInputProp);
        }

        private static void DrawSlotGroup(SerializedProperty actionProp, SerializedProperty inputProp)
        {
            EditorGUILayout.PropertyField(actionProp);

            bool hasAction = actionProp.objectReferenceValue != null;
            using (new EditorGUI.DisabledScope(!hasAction))
            {
                InputActionReferencePropertyDrawer.DrawLayoutField(inputProp, new GUIContent("Input Action"));
            }
        }

        #endregion
    }
}
#endif
