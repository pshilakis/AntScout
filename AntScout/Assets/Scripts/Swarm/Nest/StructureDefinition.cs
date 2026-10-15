using System;
using System.Collections.Generic;
using PGS.Combat.Shapes;
using PGS.Combat.Targeting;
using PGS.Core;
using PGS.Effects;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Configuration defining an autonomous continuous spatial aura projected by a structure.
    /// Supports polymorphic shapes (Circle, Donut, Box) and arbitrary effect payloads via <see cref="IHitEffect"/>.
    /// </summary>
    [Serializable]
    public class StructureAuraConfig
    {
        [Tooltip("Descriptive name of this aura (e.g. Nest Healing Fountain, Defensive Spore Cloud).")]
        [SerializeField] private string _auraName = "Healing Aura";

        [Tooltip("Spatial shape defining the physical boundary and ground footprint of the aura.")]
        [SerializeReference, SubclassSelector]
        private ISpatialShape _shape = new CircleShape(5.0f);

        [Tooltip("Which faction affiliations are affected by this aura (Ally, Enemy, Neutral).")]
        [SerializeField] private TargetAffiliation _targetAffiliation = TargetAffiliation.Ally;

        [Tooltip("How often in seconds this aura pulses and applies its effects.")]
        [Min(0.02f)]
        [SerializeField] private float _pulseInterval = 0.25f;

        [Tooltip("Polymorphic effect payloads delivered on every pulse tick (Direct Heal, Direct Damage, Slow, Stun, etc.).")]
        [SerializeReference, SubclassSelector]
        private IHitEffect[] _effects = Array.Empty<IHitEffect>();

        public string AuraName => _auraName;
        public ISpatialShape Shape => _shape;
        public TargetAffiliation TargetAffiliation => _targetAffiliation;
        public float PulseInterval => _pulseInterval;
        public IReadOnlyList<IHitEffect> Effects => _effects;
    }

    /// <summary>
    /// Universal structure definition ScriptableObject (counterpart to <see cref="AntScout.Characters.CharacterDefinition"/>).
    /// Single Source of Truth for a colony structure's identity, physical target radius (<see cref="PGS.Combat.Targeting.IAttackable"/>),
    /// starting stats/vitals (<see cref="StatsController.InitialStatConfig"/>), and autonomous auras/area effects (<see cref="StructureAuraConfig"/>).
    /// Adheres strictly to the Fail-Fast standard with zero silent fallbacks.
    /// </summary>
    [CreateAssetMenu(fileName = "StructureDefinition_New", menuName = "AntScout/Structure/Structure Definition")]
    public partial class StructureDefinition : ScriptableObject
    {
        [ColoredHeader("Structure Identity", HeaderColor.Cyan)]
        [SerializeField] private string _structureName = "Colony Nest";
        [SerializeField] private Sprite _icon;

        [ColoredHeader("Base Stats & Vitals (com.pgs.stats)", HeaderColor.Green)]
        [Tooltip("Maximum Health StatTypeSO and starting base value seeded into StatsController.")]
        [SerializeField] private StatsController.InitialStatConfig _maxHealth;

        [Tooltip("Flat Armor/Defense StatTypeSO and starting base value seeded into StatsController.")]
        [SerializeField] private StatsController.InitialStatConfig _defense;

        [Tooltip("Autonomous spatial auras continuously projected around this structure.")]
        [SerializeField] private StructureAuraConfig[] _auras = Array.Empty<StructureAuraConfig>();

        public string StructureName => _structureName;
        public Sprite Icon => _icon;
        public StatsController.InitialStatConfig MaxHealth => _maxHealth;
        public StatsController.InitialStatConfig Defense => _defense;
        public IReadOnlyList<StructureAuraConfig> Auras => _auras;
        public bool HasAuras => _auras != null && _auras.Length > 0;
    }
}
