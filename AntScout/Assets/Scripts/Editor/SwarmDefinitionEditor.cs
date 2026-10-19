#if UNITY_EDITOR
using AntScout.Config;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Custom Tabbed Inspector for <see cref="SwarmDefinition"/>.
    /// Organizes swarm simulation parameters into 3 clean, focused tabs:
    /// 1. Colony & Economy (Nest dispatch, worker capacities, biomass costs)
    /// 2. Navigation & Sensing (A* locomotion, arrival thresholds, pheromone sniffing)
    /// 3. Trail & Swarm (Stigmergic highway relaxation, swarm dispersion, trail tip search)
    /// </summary>
    [CustomEditor(typeof(SwarmDefinition))]
    public sealed class SwarmDefinitionEditor : UnityEditor.Editor
    {
        // Tab 0: Colony & Economy
        private SerializedProperty _nestRecruitmentRadiusProp;
        private SerializedProperty _workerSpawnIntervalProp;
        private SerializedProperty _maxActiveWorkersProp;
        private SerializedProperty _soldierSpawnIntervalProp;
        private SerializedProperty _maxActiveSoldiersProp;
        private SerializedProperty _nestWanderRadiusProp;
        private SerializedProperty _initialBiomassProp;
        private SerializedProperty _initialWorkerCountProp;
        private SerializedProperty _workerBiomassCostProp;
        private SerializedProperty _initialSoldierCountProp;
        private SerializedProperty _soldierBiomassCostProp;
        private SerializedProperty _workersPerSoldierRatioProp;
        private SerializedProperty _enableWartimeSurgeProp;
        private SerializedProperty _enableEmergencyBroodProp;
        private SerializedProperty _emergencyWorkerIncubationTimeProp;

        // Tab 1: Navigation & Sensing
        private SerializedProperty _arrivalThresholdProp;
        private SerializedProperty _nestArrivalThresholdProp;
        private SerializedProperty _trailSamplingRadiusProp;
        private SerializedProperty _foodDetectionRadiusProp;
        private SerializedProperty _waypointLookAheadDistanceProp;
        private SerializedProperty _localSearchNodeWindowProp;

        // Tab 2: Trail & Swarm
        private SerializedProperty _trailOptimizationPercentageProp;
        private SerializedProperty _trailLookaheadDistanceProp;
        private SerializedProperty _trailWanderMaxDeviationProp;
        private SerializedProperty _trailSwarmDispersionProp;
        private SerializedProperty _trailTipWanderRadiusProp;
        private SerializedProperty _trailTipWanderDurationProp;
        private SerializedProperty _workerReturnWanderSpeedProp;
        private SerializedProperty _meanderJitterAngleProp;
        private SerializedProperty _newTrailPriorityDurationProp;
        private SerializedProperty _newTrailPriorityMultiplierProp;
        private SerializedProperty _trailSwitchEfficiencyAdvantageProp;

        // Tab 3: Defense & Aggression
        private SerializedProperty _swarmAggroRadiusProp;
        private SerializedProperty _swarmLeashRadiusProp;
        private SerializedProperty _swarmAlarmRadiusProp;
        private SerializedProperty _swarmCombatTimeoutProp;
        private SerializedProperty _nestDefenseRadiusProp;
        private SerializedProperty _soldierTrailEscortRatioProp;
        private SerializedProperty _soldierHarvestGuardDurationProp;
        private SerializedProperty _soldierFlankOffsetProp;
        private SerializedProperty _buildupPhaseDurationProp;
        private SerializedProperty _buildupForagingRadiusProp;
        private SerializedProperty _suppressEarlyHeroAggressionProp;

        private int _selectedTab = 0;
        private static readonly string[] TabNames = new string[]
        {
            "Colony & Economy",
            "Navigation & Sensing",
            "Trail & Swarm",
            "Defense & Aggression"
        };

        private void OnEnable()
        {
            // Tab 0: Colony & Economy
            _nestRecruitmentRadiusProp = serializedObject.FindProperty("_nestRecruitmentRadius");
            _workerSpawnIntervalProp = serializedObject.FindProperty("_workerSpawnInterval");
            _maxActiveWorkersProp = serializedObject.FindProperty("_maxActiveWorkers");
            _soldierSpawnIntervalProp = serializedObject.FindProperty("_soldierSpawnInterval");
            _maxActiveSoldiersProp = serializedObject.FindProperty("_maxActiveSoldiers");
            _nestWanderRadiusProp = serializedObject.FindProperty("_nestWanderRadius");
            _initialBiomassProp = serializedObject.FindProperty("_initialBiomass");
            _initialWorkerCountProp = serializedObject.FindProperty("_initialWorkerCount");
            _workerBiomassCostProp = serializedObject.FindProperty("_workerBiomassCost");
            _initialSoldierCountProp = serializedObject.FindProperty("_initialSoldierCount");
            _soldierBiomassCostProp = serializedObject.FindProperty("_soldierBiomassCost");
            _workersPerSoldierRatioProp = serializedObject.FindProperty("_workersPerSoldierRatio");
            _enableWartimeSurgeProp = serializedObject.FindProperty("_enableWartimeSurge");
            _enableEmergencyBroodProp = serializedObject.FindProperty("_enableEmergencyBrood");
            _emergencyWorkerIncubationTimeProp = serializedObject.FindProperty("_emergencyWorkerIncubationTime");

            // Tab 1: Navigation & Sensing
            _arrivalThresholdProp = serializedObject.FindProperty("_arrivalThreshold");
            _nestArrivalThresholdProp = serializedObject.FindProperty("_nestArrivalThreshold");
            _trailSamplingRadiusProp = serializedObject.FindProperty("_trailSamplingRadius");
            _foodDetectionRadiusProp = serializedObject.FindProperty("_foodDetectionRadius");
            _waypointLookAheadDistanceProp = serializedObject.FindProperty("_waypointLookAheadDistance");
            _localSearchNodeWindowProp = serializedObject.FindProperty("_localSearchNodeWindow");

            // Tab 2: Trail & Swarm
            _trailOptimizationPercentageProp = serializedObject.FindProperty("_trailOptimizationPercentage");
            _trailLookaheadDistanceProp = serializedObject.FindProperty("_trailLookaheadDistance");
            _trailWanderMaxDeviationProp = serializedObject.FindProperty("_trailWanderMaxDeviation");
            _trailSwarmDispersionProp = serializedObject.FindProperty("_trailSwarmDispersion");
            _trailTipWanderRadiusProp = serializedObject.FindProperty("_trailTipWanderRadius");
            _trailTipWanderDurationProp = serializedObject.FindProperty("_trailTipWanderDuration");
            _workerReturnWanderSpeedProp = serializedObject.FindProperty("_workerReturnWanderSpeed");
            _meanderJitterAngleProp = serializedObject.FindProperty("_meanderJitterAngle");
            _newTrailPriorityDurationProp = serializedObject.FindProperty("_newTrailPriorityDuration");
            _newTrailPriorityMultiplierProp = serializedObject.FindProperty("_newTrailPriorityMultiplier");
            _trailSwitchEfficiencyAdvantageProp = serializedObject.FindProperty("_trailSwitchEfficiencyAdvantage");

            // Tab 3: Defense & Aggression
            _swarmAggroRadiusProp = serializedObject.FindProperty("_swarmAggroRadius");
            _swarmLeashRadiusProp = serializedObject.FindProperty("_swarmLeashRadius");
            _swarmAlarmRadiusProp = serializedObject.FindProperty("_swarmAlarmRadius");
            _swarmCombatTimeoutProp = serializedObject.FindProperty("_swarmCombatTimeout");
            _nestDefenseRadiusProp = serializedObject.FindProperty("_nestDefenseRadius");
            _soldierTrailEscortRatioProp = serializedObject.FindProperty("_soldierTrailEscortRatio");
            _soldierHarvestGuardDurationProp = serializedObject.FindProperty("_soldierHarvestGuardDuration");
            _soldierFlankOffsetProp = serializedObject.FindProperty("_soldierFlankOffset");
            _buildupPhaseDurationProp = serializedObject.FindProperty("_buildupPhaseDuration");
            _buildupForagingRadiusProp = serializedObject.FindProperty("_buildupForagingRadius");
            _suppressEarlyHeroAggressionProp = serializedObject.FindProperty("_suppressEarlyHeroAggression");
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
                    DrawColonyAndEconomyTab();
                    break;
                case 1:
                    DrawNavigationAndSensingTab();
                    break;
                case 2:
                    DrawTrailAndSwarmTab();
                    break;
                case 3:
                    DrawDefenseAndAggressionTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawColonyAndEconomyTab()
        {
            EditorGUILayout.LabelField("Worker Deployment & Dispatch", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_nestRecruitmentRadiusProp);
            EditorGUILayout.PropertyField(_workerSpawnIntervalProp);
            EditorGUILayout.PropertyField(_maxActiveWorkersProp);
            EditorGUILayout.PropertyField(_nestWanderRadiusProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Soldier Deployment", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_soldierSpawnIntervalProp);
            EditorGUILayout.PropertyField(_maxActiveSoldiersProp);
            EditorGUILayout.PropertyField(_workersPerSoldierRatioProp);
            EditorGUILayout.PropertyField(_enableWartimeSurgeProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Biomass Economy", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_initialBiomassProp, true);
            EditorGUILayout.PropertyField(_initialWorkerCountProp);
            EditorGUILayout.PropertyField(_workerBiomassCostProp);
            EditorGUILayout.PropertyField(_initialSoldierCountProp);
            EditorGUILayout.PropertyField(_soldierBiomassCostProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Emergency Brood (Anti-Deadlock)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableEmergencyBroodProp);
            EditorGUILayout.PropertyField(_emergencyWorkerIncubationTimeProp);
        }

        private void DrawNavigationAndSensingTab()
        {
            EditorGUILayout.PropertyField(_arrivalThresholdProp);
            EditorGUILayout.PropertyField(_nestArrivalThresholdProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_trailSamplingRadiusProp);
            EditorGUILayout.PropertyField(_foodDetectionRadiusProp);
            EditorGUILayout.PropertyField(_waypointLookAheadDistanceProp);
            EditorGUILayout.PropertyField(_localSearchNodeWindowProp);
        }

        private void DrawTrailAndSwarmTab()
        {
            EditorGUILayout.PropertyField(_trailOptimizationPercentageProp);
            EditorGUILayout.PropertyField(_trailLookaheadDistanceProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_trailWanderMaxDeviationProp);
            EditorGUILayout.PropertyField(_trailSwarmDispersionProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_trailTipWanderRadiusProp);
            EditorGUILayout.PropertyField(_trailTipWanderDurationProp);
            EditorGUILayout.PropertyField(_workerReturnWanderSpeedProp);
            EditorGUILayout.PropertyField(_meanderJitterAngleProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_newTrailPriorityDurationProp);
            EditorGUILayout.PropertyField(_newTrailPriorityMultiplierProp);
            EditorGUILayout.PropertyField(_trailSwitchEfficiencyAdvantageProp);
        }

        private void DrawDefenseAndAggressionTab()
        {
            EditorGUILayout.LabelField("Swarm Aggro & Leashing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_swarmAggroRadiusProp);
            EditorGUILayout.PropertyField(_swarmLeashRadiusProp);
            EditorGUILayout.PropertyField(_swarmAlarmRadiusProp);
            EditorGUILayout.PropertyField(_swarmCombatTimeoutProp);
            EditorGUILayout.PropertyField(_nestDefenseRadiusProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Soldier Trail Escort & Guard", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_soldierTrailEscortRatioProp);
            EditorGUILayout.PropertyField(_soldierHarvestGuardDurationProp);
            EditorGUILayout.PropertyField(_soldierFlankOffsetProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Opening Buildup & Pacing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_buildupPhaseDurationProp);
            EditorGUILayout.PropertyField(_buildupForagingRadiusProp);
            EditorGUILayout.PropertyField(_suppressEarlyHeroAggressionProp);
        }
    }
}
#endif
