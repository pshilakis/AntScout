using System;
using PGS.Core;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Polymorphic contract for calculating a rarity tier's drop weight.
    /// Implements ISubclassSummary for rich, contextual inspector feedback.
    /// </summary>
    public interface ITierWeight : ISubclassSummary
    {
        int GetWeight();
    }

    /// <summary>
    /// Static, unchanging drop weight.
    /// </summary>
    [Serializable]
    public class ConstantTierWeight : ITierWeight
    {
        [Tooltip("Fixed probabilistic weight for this tier.")]
        [Range(1, 1000)]
        [SerializeField] private int _weight = 50;

        public int Weight => _weight;

        public ConstantTierWeight() { }

        public ConstantTierWeight(int weight)
        {
            _weight = Mathf.Max(1, weight);
        }

        public int GetWeight() => _weight;

        public (bool show, string message, SubclassSummaryType summaryType) GetSubclassSummary()
        {
            return (true, $"Constant Drop Weight: {_weight}", SubclassSummaryType.Info);
        }
    }

    /// <summary>
    /// Drop weight that transitions from a start weight to an end weight over match duration.
    /// (e.g. Common drops start high and reduce over time; Rare drops scale up later in the match).
    /// </summary>
    [Serializable]
    public class MatchProgressionTierWeight : ITierWeight
    {
        [Tooltip("Weight at match start (0s).")]
        [Range(0, 1000)]
        [SerializeField] private int _startWeight = 100;

        [Tooltip("Weight once target transition duration is reached.")]
        [Range(0, 1000)]
        [SerializeField] private int _endWeight = 20;

        [Tooltip("Match duration in seconds to transition from start weight to end weight.")]
        [Range(10f, 1200f)]
        [SerializeField] private float _transitionDuration = 300f;

        public MatchProgressionTierWeight() { }

        public MatchProgressionTierWeight(int startWeight, int endWeight, float transitionDuration)
        {
            _startWeight = startWeight;
            _endWeight = endWeight;
            _transitionDuration = Mathf.Max(1f, transitionDuration);
        }

        public int GetWeight()
        {
            float elapsed = Time.timeSinceLevelLoad;
            float t = _transitionDuration > 0f ? Mathf.Clamp01(elapsed / _transitionDuration) : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(_startWeight, _endWeight, t)));
        }

        public (bool show, string message, SubclassSummaryType summaryType) GetSubclassSummary()
        {
            return (true, $"Dynamic Pacing: {_startWeight} -> {_endWeight} over {_transitionDuration:F0}s (Current: {GetWeight()})", SubclassSummaryType.Info);
        }
    }

    /// <summary>
    /// Drop weight evaluated from a custom designer AnimationCurve over normalized match duration.
    /// </summary>
    [Serializable]
    public class CurveTierWeight : ITierWeight
    {
        [Tooltip("Evaluated over 0.0 to 1.0 normalized match duration.")]
        [SerializeField] private AnimationCurve _curve = AnimationCurve.Linear(0f, 100f, 1f, 100f);

        [Tooltip("Target duration in seconds corresponding to curve time 1.0.")]
        [Range(10f, 1800f)]
        [SerializeField] private float _targetDuration = 300f;

        public int GetWeight()
        {
            float elapsed = Time.timeSinceLevelLoad;
            float t = _targetDuration > 0f ? Mathf.Clamp01(elapsed / _targetDuration) : 0f;
            return Mathf.Max(0, Mathf.RoundToInt(_curve.Evaluate(t)));
        }

        public (bool show, string message, SubclassSummaryType summaryType) GetSubclassSummary()
        {
            return (true, $"Curve-driven Weight over {_targetDuration:F0}s (Current: {GetWeight()})", SubclassSummaryType.Info);
        }
    }

    /// <summary>
    /// Rarity tier asset (e.g. Common, Uncommon, Rare) defining drop weights and visual badges.
    /// Configured with a polymorphic [SubclassSelector] tier weight provider.
    /// </summary>
    [CreateAssetMenu(fileName = "Rarity_New", menuName = "AntScout/Config/Rarity Definition", order = 2)]
    public class RarityDefinition : ScriptableObject
    {
        [ColoredHeader("Rarity Identity", HeaderColor.Cyan)]
        [Tooltip("Display name for this rarity tier.")]
        [SerializeField] private string _rarityName = "Common";

        [Tooltip("Color tint representing this rarity in UI and badges.")]
        [SerializeField] private Color _tierColor = Color.white;

        [Tooltip("Header color tag for PGS.Core.ColoredHeader or badge display.")]
        [SerializeField] private HeaderColor _headerColor = HeaderColor.White;

        [Tooltip("If true, drops of this rarity satisfy pity and reset the bad-luck counter. If false, drops count as standard/common.")]
        [SerializeField] private bool _isHighTier = false;

        [ColoredHeader("Weight Configuration", HeaderColor.Green)]
        [Tooltip("Polymorphic weight provider determining how often this tier drops.")]
        [SerializeReference, SubclassSelector]
        private ITierWeight _tierWeight = new ConstantTierWeight(50);

        public string RarityName => _rarityName;
        public Color TierColor => _tierColor;
        public HeaderColor HeaderColor => _headerColor;
        public bool IsHighTier => _isHighTier;
        public ITierWeight TierWeight => _tierWeight;

        public int GetWeight()
        {
            if (_tierWeight == null)
            {
                throw new InvalidOperationException($"[{name}] RarityDefinition has no TierWeight provider configured!");
            }
            return _tierWeight.GetWeight();
        }

        public void SetRuntimeValues(string rarityName, Color tierColor, HeaderColor headerColor, bool isHighTier, ITierWeight tierWeight)
        {
            _rarityName = rarityName;
            _tierColor = tierColor;
            _headerColor = headerColor;
            _isHighTier = isHighTier;
            _tierWeight = tierWeight ?? throw new ArgumentNullException(nameof(tierWeight));
        }
    }
}
