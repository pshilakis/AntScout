using PGS.Core;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Universal structure definition ScriptableObject (counterpart to <see cref="AntScout.Characters.CharacterDefinition"/>).
    /// Single Source of Truth for a colony structure's identity, physical target radius (<see cref="PGS.Combat.Targeting.IAttackable"/>),
    /// and starting stats/vitals (<see cref="StatsController.InitialStatConfig"/>).
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

        public string StructureName => _structureName;
        public Sprite Icon => _icon;
        public StatsController.InitialStatConfig MaxHealth => _maxHealth;
        public StatsController.InitialStatConfig Defense => _defense;
    }
}
