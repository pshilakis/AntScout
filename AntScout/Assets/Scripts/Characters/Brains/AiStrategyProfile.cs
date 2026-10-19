using System;
using AntScout.Characters.Brains.Behaviors;
using PGS.Core;
using UnityEngine;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// ScriptableObject defining the tactical mind, strategic archetype, and difficulty scaling
    /// for autonomous AI Hero commanders (AntAiBrain).
    /// Decoupled from physical chassis capabilities (CharacterDefinition) following Single Responsibility (SRP)
    /// and Open/Closed Principle (OCP).
    /// Uses polymorphic IAiBehavior with SubclassSelector to enable arbitrary behaviors without enums.
    /// </summary>
    [CreateAssetMenu(fileName = "AiStrategyProfile_New", menuName = "AntScout/AI/Strategy Profile")]
    public partial class AiStrategyProfile : ScriptableObject
    {
        [Header("Macro Behavior")]
        [Tooltip("Polymorphic behavior driving exploration, resource foraging, or territorial defense.")]
        [SerializeReference, SubclassSelector]
        private IAiBehavior _behavior = new ResourceScoutBehavior();

        [Tooltip("Minimum gland capacity ratio before embarking from nest to execute behavior.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _minGlandRatioToEmbark = 0.5f;

        [Header("Combat & Survivability")]
        [Tooltip("Health percentage threshold (0.0 to 0.8) where the AI disengages combat and retreats home to heal.")]
        [Range(0.0f, 0.8f)]
        [SerializeField] private float _retreatHealthRatio = 0.3f;

        [Tooltip("Maximum pursuit distance from nest or patrol anchor before disengaging.")]
        [Range(4.0f, 50.0f)]
        [SerializeField] private float _leashRadius = 14.0f;

        [Tooltip("If true, uses mobility abilities (Dash/Leap) to close distance on fleeing targets.")]
        [SerializeField] private bool _useMobilityToEngage = true;

        [Tooltip("If true, uses mobility abilities to escape when retreating at low health.")]
        [SerializeField] private bool _useMobilityToRetreat = true;

        [Header("Difficulty & Reflex Scaling")]
        [Tooltip("Reaction time delay in seconds between sensory scans. Easy = 0.35s (sluggish), Normal = 0.15s, Hard = 0.05s (reflexive).")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float _scanInterval = 0.15f;

        [Tooltip("Multiplier applied to the chassis detection radius (e.g. 0.8x for tunnel-vision Easy, 1.25x for keen Hard).")]
        [Range(0.5f, 2.0f)]
        [SerializeField] private float _perceptionMultiplier = 1.0f;

        [Tooltip("Horizontal arrival distance threshold in meters to consider an objective, food source, or nest depot reached.")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float _arrivalDistance = 1.2f;

        public IAiBehavior Behavior => _behavior;
        public float MinGlandRatioToEmbark => _minGlandRatioToEmbark;
        public float RetreatHealthRatio => _retreatHealthRatio;
        public float LeashRadius => _leashRadius;
        public bool UseMobilityToEngage => _useMobilityToEngage;
        public bool UseMobilityToRetreat => _useMobilityToRetreat;
        public float ScanInterval => _scanInterval;
        public float PerceptionMultiplier => _perceptionMultiplier;
        public float ArrivalDistance => _arrivalDistance;

        private void OnEnable()
        {
            Validate();
        }
    }
}
