using System;
using PGS.Effects;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Core.Stats
{
    /// <summary>
    /// Non-MonoBehaviour vitals coordinator backed strictly by <see cref="StatsController"/> (com.pgs.stats).
    /// Seeds initial base values from CharacterDefinition / StructureDefinition <see cref="StatsController.InitialStatConfig"/>
    /// directly into the entity's <see cref="StatsController"/> at initialization time.
    /// Adheres strictly to the Fail-Fast standard with zero silent fallbacks.
    /// </summary>
    public sealed partial class ColonyVitals
    {
        private StatsController _stats;
        private StatTypeSO _maxHealthStat;
        private StatTypeSO _defenseStat;
        private StatTypeSO _attackStat;
        private float _currentHealth;
        private bool _isInitialized;
        private bool _isAlive;

        public event Action<float, float> OnHealthChanged;
        public event Action<DamageResult, HitContext> OnDamaged;
        public event Action<HitContext> OnDied;

        public float CurrentHealth => _currentHealth;

        public float MaxHealth
        {
            get
            {
                EnsureInitialized();
                return _stats.GetStatValue(_maxHealthStat);
            }
        }

        public float Defense
        {
            get
            {
                EnsureInitialized();
                return _stats.GetStatValue(_defenseStat);
            }
        }

        public float Attack
        {
            get
            {
                EnsureInitialized();
                if (_attackStat == null)
                {
                    throw new InvalidOperationException(
                        $"[{_stats.name}] Attack stat was queried for combat damage, but no Attack StatTypeSO is configured in its Definition!");
                }
                return _stats.GetStatValue(_attackStat);
            }
        }

        public float NormalizedHealth
        {
            get
            {
                float max = MaxHealth;
                if (max <= 0f)
                {
                    throw new InvalidOperationException($"[{_stats.name}] Evaluated MaxHealth is <= 0 ({max}).");
                }
                return Mathf.Clamp01(_currentHealth / max);
            }
        }

        public bool IsAlive => _isInitialized && _isAlive;

        public void Initialize(
            StatsController statsController,
            in StatsController.InitialStatConfig maxHealthConfig,
            in StatsController.InitialStatConfig defenseConfig,
            in StatsController.InitialStatConfig attackConfig = default)
        {
            ValidateInitializationArgs(statsController, in maxHealthConfig, in defenseConfig, in attackConfig);

            if (_stats != null)
            {
                _stats.OnAnyStatChanged -= HandleStatChanged;
            }

            _stats = statsController;
            _maxHealthStat = maxHealthConfig.StatType;
            _defenseStat = defenseConfig.StatType;
            _attackStat = attackConfig.StatType;

            SeedStat(_stats, in maxHealthConfig);
            SeedStat(_stats, in defenseConfig);

            if (_attackStat != null)
            {
                SeedStat(_stats, in attackConfig);
            }

            _stats.OnAnyStatChanged += HandleStatChanged;

            _isInitialized = true;
            float max = MaxHealth;
            if (max <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{statsController.name}] Evaluated MaxHealth value ({max}) on StatsController must be greater than zero!");
            }

            _currentHealth = max;
            _isAlive = true;
            OnHealthChanged?.Invoke(_currentHealth, max);
        }

        private static void SeedStat(StatsController controller, in StatsController.InitialStatConfig config)
        {
            config.StatType.ValidateOrThrow($"StatsController on {controller.name}");
            if (controller.HasStat(config.StatType))
            {
                controller.SetBaseValue(config.StatType, config.BaseValue);
            }
            else
            {
                controller.RegisterStat(config.StatType, config.BaseValue);
            }
        }

        public void Cleanup()
        {
            if (_stats != null)
            {
                _stats.OnAnyStatChanged -= HandleStatChanged;
            }
        }

        public void Reset()
        {
            EnsureInitialized();
            float max = MaxHealth;
            _currentHealth = max;
            _isAlive = _currentHealth > 0f;
            OnHealthChanged?.Invoke(_currentHealth, max);
        }

        private void HandleStatChanged(StatTypeSO statType, float oldValue, float newValue)
        {
            if (!_isInitialized || !_isAlive || statType != _maxHealthStat)
            {
                return;
            }

            float delta = newValue - oldValue;
            if (delta > 0f)
            {
                _currentHealth = Mathf.Min(newValue, _currentHealth + delta);
            }
            else
            {
                _currentHealth = Mathf.Clamp(_currentHealth, 1f, Mathf.Max(1f, newValue));
            }

            OnHealthChanged?.Invoke(_currentHealth, newValue);
        }

        public DamageResult ReceiveDamage(float amount, in HitContext context)
        {
            EnsureInitialized();

            if (!_isAlive)
            {
                return DamageResult.Resisted;
            }

            if (amount <= 0f)
            {
                return DamageResult.Zero;
            }

            float mitigated = Mathf.Max(1f, amount - Defense);
            float actualApplied = Mathf.Min(_currentHealth, mitigated);
            _currentHealth -= actualApplied;

            bool isLethal = _currentHealth <= 0.0001f;
            if (isLethal)
            {
                _currentHealth = 0f;
                _isAlive = false;
            }

            DamageResult result = DamageResult.Applied(actualApplied, isLethal);
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
            OnDamaged?.Invoke(result, context);

            if (isLethal)
            {
                OnDied?.Invoke(context);
            }

            return result;
        }

        public void ReceiveHealing(float amount, in HitContext context)
        {
            EnsureInitialized();

            if (!_isAlive || amount <= 0f)
            {
                return;
            }

            float max = MaxHealth;
            float previous = _currentHealth;
            _currentHealth = Mathf.Min(max, _currentHealth + amount);

            if (Mathf.Abs(_currentHealth - previous) > 0.0001f)
            {
                OnHealthChanged?.Invoke(_currentHealth, max);
            }
        }
    }
}
