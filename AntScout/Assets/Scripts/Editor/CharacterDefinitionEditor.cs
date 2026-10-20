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
        private SerializedProperty _materialProp;

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
        private SerializedProperty _tertiaryActionProp;
        private SerializedProperty _tertiaryInputProp;
        private SerializedProperty _ultimateActionProp;
        private SerializedProperty _ultimateInputProp;
        private SerializedProperty _recruitmentTrailActionProp;
        private SerializedProperty _recruitmentTrailInputProp;
        private SerializedProperty _alarmTrailActionProp;
        private SerializedProperty _alarmTrailInputProp;
        private SerializedProperty _trailActivationModeProp;

        private SerializedProperty _prioritiesProp;
        private SerializedProperty _aiBehaviorProp;
        private SerializedProperty _retreatHealthRatioProp;
        private SerializedProperty _leashRadiusProp;
        private SerializedProperty _scanIntervalProp;
        private SerializedProperty _arrivalDistanceProp;
        private SerializedProperty _minGlandRatioToEmbarkProp;
        private SerializedProperty _useMobilityToEngageProp;
        private SerializedProperty _useMobilityToRetreatProp;
        private SerializedProperty _foodDetectionRadiusProp;
        private SerializedProperty _hostileNestAvoidanceRadiusProp;
        private SerializedProperty _scoutDistanceRangeProp;
        private SerializedProperty _scoutDurationRangeProp;
        private SerializedProperty _scoutAngleJitterProp;
        private SerializedProperty _scoutPauseChanceProp;
        private SerializedProperty _scoutPauseDurationRangeProp;

        private int _selectedTab = 0;
        private static readonly string[] TabNames = new string[]
        {
            "Identity & Stats",
            "Action Kit",
            "Hero AI & Behavior"
        };

        private SerializedProperty _enableHealthBarProp;
        private SerializedProperty _healthBarHeightOffsetProp;
        private SerializedProperty _healthBarWidthProp;
        private SerializedProperty _healthBarHeightProp;
        private SerializedProperty _healthBarBorderThicknessProp;
        private SerializedProperty _healthBarSegmentSizeProp;
        private SerializedProperty _healthBarSegmentGapProp;
        private SerializedProperty _healthBarNotchWidthProp;
        private SerializedProperty _healthBarVisibilityProp;
        private SerializedProperty _healthBarColorProp;
        private SerializedProperty _healthGhostColorProp;

        private SerializedProperty _enablePheromoneBarProp;
        private SerializedProperty _pheromoneBarSpacingProp;
        private SerializedProperty _pheromoneBarHeightProp;
        private SerializedProperty _pheromoneBarBorderThicknessProp;
        private SerializedProperty _pheromoneBarColorProp;
        private SerializedProperty _pheromoneGhostColorProp;
        private SerializedProperty _pheromoneBarVisibilityProp;

        private void OnEnable()
        {
            _characterNameProp = serializedObject.FindProperty("_characterName");
            _iconProp = serializedObject.FindProperty("_icon");
            _materialProp = serializedObject.FindProperty("_material");

            _maxHealthProp = serializedObject.FindProperty("_maxHealth");
            _defenseProp = serializedObject.FindProperty("_defense");
            _attackProp = serializedObject.FindProperty("_attack");
            _baseMoveSpeedProp = serializedObject.FindProperty("_baseMoveSpeed");
            _visionRadiusProp = serializedObject.FindProperty("_visionRadius");

            _enableHealthBarProp = serializedObject.FindProperty("_enableHealthBar");
            _healthBarHeightOffsetProp = serializedObject.FindProperty("_healthBarHeightOffset");
            _healthBarWidthProp = serializedObject.FindProperty("_healthBarWidth");
            _healthBarHeightProp = serializedObject.FindProperty("_healthBarHeight");
            _healthBarBorderThicknessProp = serializedObject.FindProperty("_healthBarBorderThickness");
            _healthBarSegmentSizeProp = serializedObject.FindProperty("_healthBarSegmentSize");
            _healthBarSegmentGapProp = serializedObject.FindProperty("_healthBarSegmentGap");
            _healthBarNotchWidthProp = serializedObject.FindProperty("_healthBarNotchWidth");
            _healthBarVisibilityProp = serializedObject.FindProperty("_healthBarVisibility");
            _healthBarColorProp = serializedObject.FindProperty("_healthBarColor");
            _healthGhostColorProp = serializedObject.FindProperty("_healthGhostColor");

            _enablePheromoneBarProp = serializedObject.FindProperty("_enablePheromoneBar");
            _pheromoneBarSpacingProp = serializedObject.FindProperty("_pheromoneBarSpacing");
            _pheromoneBarHeightProp = serializedObject.FindProperty("_pheromoneBarHeight");
            _pheromoneBarBorderThicknessProp = serializedObject.FindProperty("_pheromoneBarBorderThickness");
            _pheromoneBarColorProp = serializedObject.FindProperty("_pheromoneBarColor");
            _pheromoneGhostColorProp = serializedObject.FindProperty("_pheromoneGhostColor");
            _pheromoneBarVisibilityProp = serializedObject.FindProperty("_pheromoneBarVisibility");

            _prioritiesProp = serializedObject.FindProperty("_priorities");
            _aiBehaviorProp = serializedObject.FindProperty("_aiBehavior");
            _retreatHealthRatioProp = serializedObject.FindProperty("_retreatHealthRatio");
            _leashRadiusProp = serializedObject.FindProperty("_leashRadius");
            _scanIntervalProp = serializedObject.FindProperty("_scanInterval");
            _arrivalDistanceProp = serializedObject.FindProperty("_arrivalDistance");
            _minGlandRatioToEmbarkProp = serializedObject.FindProperty("_minGlandRatioToEmbark");
            _useMobilityToEngageProp = serializedObject.FindProperty("_useMobilityToEngage");
            _useMobilityToRetreatProp = serializedObject.FindProperty("_useMobilityToRetreat");
            _foodDetectionRadiusProp = serializedObject.FindProperty("_foodDetectionRadius");
            _hostileNestAvoidanceRadiusProp = serializedObject.FindProperty("_hostileNestAvoidanceRadius");
            _scoutDistanceRangeProp = serializedObject.FindProperty("_scoutDistanceRange");
            _scoutDurationRangeProp = serializedObject.FindProperty("_scoutDurationRange");
            _scoutAngleJitterProp = serializedObject.FindProperty("_scoutAngleJitter");
            _scoutPauseChanceProp = serializedObject.FindProperty("_scoutPauseChance");
            _scoutPauseDurationRangeProp = serializedObject.FindProperty("_scoutPauseDurationRange");

            _primaryActionProp = serializedObject.FindProperty("_primaryAction");
            _primaryInputProp = serializedObject.FindProperty("_primaryInput");
            _secondaryActionProp = serializedObject.FindProperty("_secondaryAction");
            _secondaryInputProp = serializedObject.FindProperty("_secondaryInput");
            _utilityActionProp = serializedObject.FindProperty("_utilityAction");
            _utilityInputProp = serializedObject.FindProperty("_utilityInput");
            _tertiaryActionProp = serializedObject.FindProperty("_tertiaryAction");
            _tertiaryInputProp = serializedObject.FindProperty("_tertiaryInput");
            _ultimateActionProp = serializedObject.FindProperty("_ultimateAction");
            _ultimateInputProp = serializedObject.FindProperty("_ultimateInput");
            _recruitmentTrailActionProp = serializedObject.FindProperty("_recruitmentTrailAction");
            _recruitmentTrailInputProp = serializedObject.FindProperty("_recruitmentTrailInput");
            _alarmTrailActionProp = serializedObject.FindProperty("_alarmTrailAction");
            _alarmTrailInputProp = serializedObject.FindProperty("_alarmTrailInput");
            _trailActivationModeProp = serializedObject.FindProperty("_trailActivationMode");
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
                case 2:
                    DrawHeroAiTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Tab 0: Identity & Stats

        private void DrawIdentityAndStatsTab()
        {
            EditorGUILayout.PropertyField(_characterNameProp);
            EditorGUILayout.PropertyField(_iconProp);
            EditorGUILayout.PropertyField(_materialProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_maxHealthProp);
            EditorGUILayout.PropertyField(_defenseProp);
            EditorGUILayout.PropertyField(_attackProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_enableHealthBarProp);
            if (_enableHealthBarProp != null && _enableHealthBarProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_healthBarHeightOffsetProp, new GUIContent("Height Offset (Above Unit)"));
                EditorGUILayout.PropertyField(_healthBarWidthProp, new GUIContent("Health Bar Width (Meters)"));
                EditorGUILayout.PropertyField(_healthBarHeightProp, new GUIContent("Bar Thickness / Height (Meters)"));
                EditorGUILayout.PropertyField(_healthBarBorderThicknessProp, new GUIContent("Border Thickness (Meters)"));
                EditorGUILayout.PropertyField(_healthBarSegmentSizeProp, new GUIContent("Segment Pip Size (HP Per Pip)"));
                EditorGUILayout.PropertyField(_healthBarSegmentGapProp, new GUIContent("Segment Gap (0 = Solid SC2)", "Transparent cutout gap width. Set to 0 for solid StarCraft 2 style."));
                EditorGUILayout.PropertyField(_healthBarNotchWidthProp, new GUIContent("Divider Line Width (SC2 Notches)", "Thickness of the solid dark divider lines separating pips."));
                EditorGUILayout.PropertyField(_healthBarVisibilityProp, new GUIContent("Visibility Policy"));
                EditorGUILayout.PropertyField(_healthBarColorProp, new GUIContent("Health Fill Color"));
                EditorGUILayout.PropertyField(_healthGhostColorProp, new GUIContent("Damage Ghost Color"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_enablePheromoneBarProp, new GUIContent("Enable Pheromone Bar", "Attaches a secondary resource bar directly under the health bar displaying remaining chemical capacity."));
            if (_enablePheromoneBarProp != null && _enablePheromoneBarProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_pheromoneBarSpacingProp, new GUIContent("Vertical Gap Below HP Bar (m)"));
                EditorGUILayout.PropertyField(_pheromoneBarHeightProp, new GUIContent("Bar Thickness / Height (m)"));
                EditorGUILayout.PropertyField(_pheromoneBarBorderThicknessProp, new GUIContent("Border Thickness (m)"));
                EditorGUILayout.PropertyField(_pheromoneBarColorProp, new GUIContent("Pheromone Fill Color"));
                EditorGUILayout.PropertyField(_pheromoneGhostColorProp, new GUIContent("Drain Ghost Lag Color"));
                EditorGUILayout.PropertyField(_pheromoneBarVisibilityProp, new GUIContent("Visibility Policy"));
                EditorGUI.indentLevel--;
            }

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

            DrawSlotGroup(_tertiaryActionProp, _tertiaryInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_ultimateActionProp, _ultimateInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_recruitmentTrailActionProp, _recruitmentTrailInputProp);
            EditorGUILayout.Space(6);

            DrawSlotGroup(_alarmTrailActionProp, _alarmTrailInputProp);
            EditorGUILayout.Space(8);

            EditorGUILayout.PropertyField(_trailActivationModeProp);
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

        #region Tab 2: Hero AI & Behavior

        private void DrawHeroAiTab()
        {
            EditorGUILayout.HelpBox(
                "Autonomous AI settings utilized when this character chassis is piloted by an AI commander (AntAiBrain). " +
                "Configures polymorphic macro behavior via SubclassSelector, combat survivability thresholds, and expedition limits.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("AI Decision Priorities (Utility Weights)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_prioritiesProp, true);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Macro Behavior (SubclassSelector)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_aiBehaviorProp, true);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Combat & Survivability", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_retreatHealthRatioProp);
            EditorGUILayout.PropertyField(_leashRadiusProp);
            EditorGUILayout.PropertyField(_useMobilityToEngageProp);
            EditorGUILayout.PropertyField(_useMobilityToRetreatProp);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Macro Timing & Reflexes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_scanIntervalProp);
            EditorGUILayout.PropertyField(_arrivalDistanceProp);
            EditorGUILayout.PropertyField(_minGlandRatioToEmbarkProp);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Expedition & Sensory Limits", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_foodDetectionRadiusProp);
            EditorGUILayout.PropertyField(_hostileNestAvoidanceRadiusProp);
            EditorGUILayout.PropertyField(_scoutDistanceRangeProp);
            EditorGUILayout.PropertyField(_scoutDurationRangeProp);
            EditorGUILayout.PropertyField(_scoutAngleJitterProp);
            EditorGUILayout.PropertyField(_scoutPauseChanceProp);
            EditorGUILayout.PropertyField(_scoutPauseDurationRangeProp);
        }

        #endregion
    }
}
#endif
