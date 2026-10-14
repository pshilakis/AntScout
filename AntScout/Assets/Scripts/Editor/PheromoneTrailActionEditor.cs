#if UNITY_EDITOR
using AntScout.Characters;
using PGS.Combat.Editor;
using PGS.Combat.Shapes;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Custom Tabbed Inspector for <see cref="PheromoneTrailAction"/>.
    /// Locks trail nodes to a circular shape and organizes chemistry, self-contained
    /// duration/recharge tuning, and optional combat/hazard effects into 3 tabs.
    /// </summary>
    [CustomEditor(typeof(PheromoneTrailAction))]
    public sealed class PheromoneTrailActionEditor : UnityEditor.Editor
    {
        private SerializedProperty _pheromoneTypeProp;
        private SerializedProperty _nodeRadiusProp;
        private SerializedProperty _nodePlacementDistanceProp;
        private SerializedProperty _nodeLifetimeProp;

        private SerializedProperty _maxCapacityProp;
        private SerializedProperty _chemicalCostPerSecondProp;
        private SerializedProperty _rechargeRatePerSecondProp;

        private SerializedProperty _actionNameProp;
        private SerializedProperty _iconProp;
        private SerializedProperty _telegraphColorProp;
        private SerializedProperty _targetAffiliationProp;
        private SerializedProperty _basePotencyProp;
        private SerializedProperty _tickIntervalProp;

        private int _selectedTab = 0;
        private static readonly string[] TabNames = new string[]
        {
            "Trail & Nodes",
            "Duration & Recharge",
            "Effects & Presentation"
        };

        private void OnEnable()
        {
            _pheromoneTypeProp = serializedObject.FindProperty("_pheromoneType");
            _nodeRadiusProp = serializedObject.FindProperty("_nodeRadius");
            _nodePlacementDistanceProp = serializedObject.FindProperty("_nodePlacementDistance");
            _nodeLifetimeProp = serializedObject.FindProperty("_nodeLifetime");

            _maxCapacityProp = serializedObject.FindProperty("_maxCapacity");
            _chemicalCostPerSecondProp = serializedObject.FindProperty("_chemicalCostPerSecond");
            _rechargeRatePerSecondProp = serializedObject.FindProperty("_rechargeRatePerSecond");

            _actionNameProp = serializedObject.FindProperty("_actionName");
            _iconProp = serializedObject.FindProperty("_icon");
            _telegraphColorProp = serializedObject.FindProperty("_telegraphColor");
            _targetAffiliationProp = serializedObject.FindProperty("_targetAffiliation");
            _basePotencyProp = serializedObject.FindProperty("_basePotency");
            _tickIntervalProp = serializedObject.FindProperty("_tickInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var trailAction = (PheromoneTrailAction)target;

            EditorGUILayout.Space(4);
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, GUILayout.Height(26));
            EditorGUILayout.Space(8);

            switch (_selectedTab)
            {
                case 0:
                    DrawTrailAndNodesTab(trailAction);
                    break;
                case 1:
                    DrawDurationAndRechargeTab();
                    break;
                case 2:
                    DrawEffectsAndPresentationTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Tab 0: Trail & Nodes

        private void DrawTrailAndNodesTab(PheromoneTrailAction trailAction)
        {
            EditorGUILayout.PropertyField(_pheromoneTypeProp);
            EditorGUILayout.PropertyField(_nodeRadiusProp, new GUIContent("Circular Node Radius (m)"));
            EditorGUILayout.PropertyField(_nodePlacementDistanceProp, new GUIContent("Node Spacing (m)"));
            EditorGUILayout.PropertyField(_nodeLifetimeProp, new GUIContent("Node Decay Lifetime (s)"));

            EditorGUILayout.HelpBox(
                $"Drops circular scent nodes ({_nodeRadiusProp.floatValue:F2}m radius) every {_nodePlacementDistanceProp.floatValue:F2}m moved. Each node persists for {_nodeLifetimeProp.floatValue:F1}s.",
                MessageType.Info);

            CombatShapePreviewDrawer.DrawPreview(
                new CircleShape(_nodeRadiusProp.floatValue),
                trailAction.TelegraphColor);
        }

        #endregion

        #region Tab 1: Duration & Recharge

        private void DrawDurationAndRechargeTab()
        {
            EditorGUILayout.PropertyField(_maxCapacityProp, new GUIContent("Max Reservoir Capacity"));
            EditorGUILayout.PropertyField(_chemicalCostPerSecondProp, new GUIContent("Drain Cost (/s)"));
            EditorGUILayout.PropertyField(_rechargeRatePerSecondProp, new GUIContent("Passive Recharge (/s)"));

            float capacity = _maxCapacityProp.floatValue;
            float drain = _chemicalCostPerSecondProp.floatValue;
            float recharge = _rechargeRatePerSecondProp.floatValue;

            float maxDuration = drain > 0f ? capacity / drain : 0f;
            float fullRechargeTime = recharge > 0f ? capacity / recharge : 0f;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Max Sustained Trail Time: {maxDuration:F1}s  |  Full Reservoir Recharge: {fullRechargeTime:F1}s",
                MessageType.Info);
        }

        #endregion

        #region Tab 2: Effects & Presentation

        private void DrawEffectsAndPresentationTab()
        {
            EditorGUILayout.PropertyField(_actionNameProp);
            EditorGUILayout.PropertyField(_iconProp);
            EditorGUILayout.PropertyField(_telegraphColorProp, new GUIContent("Trail Preview / Node Color"));

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_targetAffiliationProp, new GUIContent("Affected Affiliation"));
            EditorGUILayout.PropertyField(_basePotencyProp, new GUIContent("Tick Potency (0 = Pure Scent)"));
            if (_basePotencyProp.floatValue > 0f)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_tickIntervalProp, new GUIContent("Effect Tick Interval (s)"));
                EditorGUI.indentLevel--;
            }
        }

        #endregion
    }
}
#endif
