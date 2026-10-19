using System;
using AntScout.Characters;
using AntScout.Core.Interfaces;
using AntScout.Match;
using PGS.Core.Time;
using PGS.Effects;
using UnityEngine;

namespace AntScout.UI.Overhead
{
    /// <summary>
    /// Explicit visibility policies for overhead floating healthbars.
    /// Decouples event-based combat linger from state-based wounded detection.
    /// </summary>
    public enum HealthBarVisibilityPolicy
    {
        /// <summary>
        /// Permanently visible while the entity is alive.
        /// </summary>
        AlwaysVisible = 0,

        /// <summary>
        /// State-based: Visible whenever CurrentHealth &lt; MaxHealth. Fades out when healed to 100%.
        /// </summary>
        InjuredOnly = 1,

        /// <summary>
        /// Event-based: Pops into view upon taking damage and stays visible for a combat linger duration.
        /// </summary>
        CombatLinger = 2,

        /// <summary>
        /// Hybrid: Visible during active combat OR whenever below 100% health.
        /// </summary>
        CombatOrInjured = 3
    }

    /// <summary>
    /// Ultra high-performance, GPU-billboarded diegetic floating healthbar with segmented pips.
    /// Uses a single-pass procedural URP shader on a 4-vertex quad mesh with GPU Instancing.
    /// Completely eliminates CPU camera-rotation matrix dirtying, Canvas rebuilds, and uGUI layout overhead.
    /// Driven deterministically by PGS.Core.Time and sleeps (0 CPU cycles) when idle/settled.
    /// Follows Single Responsibility (SRP), Interface Segregation (ISP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class OverheadHealthBar : MonoBehaviour, IUpdatable
    {
        [Header("Target & Anchor")]
        [Tooltip("Optional explicit source component implementing IHealthEntity. If null, auto-searches parent hierarchy.")]
        [SerializeField] private Component _healthSource;

        [Tooltip("Height offset in meters above the target's root transform.")]
        [SerializeField] private float _heightOffset = 1.2f;

        [Header("Dimensions (Meters)")]
        [Tooltip("Width of the health bar in world meters.")]
        [Min(0.2f)]
        [SerializeField] private float _barWidth = 1.0f;

        [Tooltip("Height of the health bar in world meters.")]
        [Min(0.04f)]
        [SerializeField] private float _barHeight = 0.12f;

        [Tooltip("Border frame thickness in world meters.")]
        [Min(0.002f)]
        [SerializeField] private float _borderThickness = 0.015f;

        [Header("Segmentation & Styling")]
        [Tooltip("HP per segment pip. Set to 0 for a smooth non-segmented bar.")]
        [SerializeField] private float _segmentSize = 20.0f;

        [Tooltip("Normalized gap between segment pips (0 = none, 0.02 = standard).")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _segmentGap = 0.02f;

        [Tooltip("Slant angle for angled chevrons (-1 to 1, 0 = vertical).")]
        [Range(-1f, 1f)]
        [SerializeField] private float _slant = 0f;

        [Tooltip("Divider notch width.")]
        [SerializeField] private float _notchWidth = 0.01f;

        [Header("Color Palette")]
        [Tooltip("If true, queries colony team ID from parent and uses the match team color.")]
        [SerializeField] private bool _useTeamColor = false;
        [SerializeField] private Color _healthColor = new Color(0.20f, 0.85f, 0.30f, 1.0f);
        [SerializeField] private Color _ghostColor = new Color(0.95f, 0.60f, 0.15f, 1.0f);
        [SerializeField] private Color _backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private Color _borderColor = new Color(0.02f, 0.02f, 0.02f, 1.0f);

        [Header("Distance Scaling (GPU-Evaluated)")]
        [Tooltip("Dynamically adjusts quad scale with camera distance to keep the bar legible across the combat arena.")]
        [SerializeField] private bool _enableDistanceScaling = true;

        [Tooltip("Camera distance in meters where the health bar is at 1.0x baseline scale.")]
        [Min(1f)]
        [SerializeField] private float _referenceDistance = 8f;

        [Tooltip("Minimum scale multiplier when close to the camera.")]
        [Min(0.2f)]
        [SerializeField] private float _minScaleMultiplier = 0.75f;

        [Tooltip("Maximum scale multiplier when far across the arena.")]
        [Min(1f)]
        [SerializeField] private float _maxScaleMultiplier = 2.0f;

        [Header("Damage Catch-Up & Hit Flash")]
        [Tooltip("Delay in seconds before the ghost catch-up bar begins draining after damage.")]
        [Min(0f)]
        [SerializeField] private float _ghostDelay = 0.35f;

        [Tooltip("Linear drain speed in normalized fill units per second.")]
        [Min(0.1f)]
        [SerializeField] private float _ghostDrainSpeed = 1.2f;

        [Tooltip("Duration in seconds the bar flashes pure white upon taking damage.")]
        [Min(0.02f)]
        [SerializeField] private float _flashDuration = 0.18f;

        [Header("Visibility Policy")]
        [SerializeField] private HealthBarVisibilityPolicy _visibilityPolicy = HealthBarVisibilityPolicy.AlwaysVisible;

        [Tooltip("Duration in seconds the bar stays visible after the last damage event under CombatLinger policies.")]
        [Min(0.5f)]
        [SerializeField] private float _combatLingerDuration = 3.5f;

        [Tooltip("Smooth fade transition duration in seconds.")]
        [Min(0.05f)]
        [SerializeField] private float _fadeDuration = 0.20f;

        [Header("Time Synchronization")]
        [SerializeField] private TimeChannel _timeChannel = TimeChannel.World;

        // Static shader property IDs for zero-allocation MaterialPropertyBlock updates
        private static readonly int HealthRatioId = Shader.PropertyToID("_HealthRatio");
        private static readonly int GhostRatioId = Shader.PropertyToID("_GhostRatio");
        private static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");
        private static readonly int FlashIntensityId = Shader.PropertyToID("_FlashIntensity");
        private static readonly int BarDimensionsId = Shader.PropertyToID("_BarDimensions");
        private static readonly int DistanceScaleParamsId = Shader.PropertyToID("_DistanceScaleParams");
        private static readonly int MaxHealthId = Shader.PropertyToID("_MaxHealth");
        private static readonly int SegmentSizeId = Shader.PropertyToID("_SegmentSize");
        private static readonly int SegmentGapId = Shader.PropertyToID("_SegmentGap");
        private static readonly int SlantId = Shader.PropertyToID("_Slant");
        private static readonly int NotchWidthId = Shader.PropertyToID("_NotchWidth");
        private static readonly int HealthColorId = Shader.PropertyToID("_HealthColor");
        private static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        private static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");

        private static MaterialPropertyBlock _sharedPropBlock;
        private static Mesh _sharedQuadMesh;
        private static Material _sharedMaterial;

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private IHealthEntity _health;
        private Transform _targetTransform;

        private float _maxHealth = 100.0f;
        private float _currentHealthRatio = 1.0f;
        private float _ghostRatio = 1.0f;
        private float _ghostTimer;
        private float _flashTimer;
        private float _combatLingerTimer;
        private float _currentAlpha = 1.0f;
        private bool _isTicking;

        public IHealthEntity BoundHealth => _health;

        public float HeightOffset
        {
            get => _heightOffset;
            set
            {
                _heightOffset = value;
                if (_targetTransform != null)
                {
                    transform.position = _targetTransform.position + Vector3.up * _heightOffset;
                }
                else
                {
                    transform.localPosition = new Vector3(0f, _heightOffset, 0f);
                }
            }
        }

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();

            _sharedPropBlock ??= new MaterialPropertyBlock();

            _meshFilter.sharedMesh = GetOrCreateSharedQuadMesh();
            _meshRenderer.sharedMaterial = GetOrCreateSharedMaterial();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            ResolveTargetTransform();
            transform.position = (_targetTransform != null ? _targetTransform.position : transform.position) + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.identity;

            ResolveHealthEntity();
            ResolveTeamColor();

            if (_health != null)
            {
                _maxHealth = _health.MaxHealth;
                _currentHealthRatio = _health.NormalizedHealth;
                _ghostRatio = _currentHealthRatio;
            }

            _currentAlpha = (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible) ? 1.0f : 0.0f;
            PushPropertiesToGpu();
        }

        private void OnEnable()
        {
            ResolveTargetTransform();

            if (_health == null)
            {
                ResolveHealthEntity();
            }

            if (_health != null)
            {
                BindHealthInternal(_health);
                _maxHealth = _health.MaxHealth;
                _currentHealthRatio = _health.NormalizedHealth;
                _ghostRatio = _currentHealthRatio;
            }

            ResolveTeamColor();
            _currentAlpha = (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible) ? 1.0f : 0.0f;
            PushPropertiesToGpu();

            if (ShouldBeVisible())
            {
                EnsureTicking();
            }
        }

        private void LateUpdate()
        {
            if (_targetTransform == null)
            {
                ResolveTargetTransform();
                if (_targetTransform == null) return;
            }

            // Frame-perfect visual sync: Follow interpolated model position in world space
            transform.position = _targetTransform.position + Vector3.up * _heightOffset;

            // Neutralize rotation so the GPU billboarding vertex shader always starts from an un-tilted coordinate basis
            transform.rotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            UnbindHealthInternal();
            StopTicking();
        }

        private void ResolveTargetTransform()
        {
            if (transform.parent == null)
            {
                _targetTransform = transform;
                return;
            }

            // 1. AntCharacter: use TargetTransform (which is _modelTransform interpolated at 144Hz+)
            var antChar = transform.parent.GetComponent<AntCharacter>();
            if (antChar != null && antChar.TargetTransform != null)
            {
                _targetTransform = antChar.TargetTransform;
                return;
            }

            // 2. SwarmAgent / Worker / Soldier: use child "Visual" transform if present
            Transform visual = transform.parent.Find("Visual");
            if (visual != null)
            {
                _targetTransform = visual;
                return;
            }

            // 3. Fallback to parent root
            _targetTransform = transform.parent;
        }

        private void ResolveHealthEntity()
        {
            if (_healthSource != null)
            {
                _health = _healthSource as IHealthEntity ?? _healthSource.GetComponent<IHealthEntity>();
            }
            if (_health == null)
            {
                _health = GetComponentInParent<IHealthEntity>();
            }
        }

        private void ResolveTeamColor()
        {
            if (!_useTeamColor) return;

            var factionMember = GetComponentInParent<IColonyFactionMember>();
            if (factionMember != null && MatchDirector.Instance != null)
            {
                _healthColor = MatchDirector.Instance.GetTeamColor(factionMember.ColonyTeamId);
            }
        }

        /// <summary>
        /// Explicitly binds this health bar to an IHealthEntity provider.
        /// </summary>
        public void BindHealth(IHealthEntity health)
        {
            if (_health == health) return;

            UnbindHealthInternal();
            _health = health;

            if (isActiveAndEnabled && _health != null)
            {
                BindHealthInternal(_health);
                _maxHealth = _health.MaxHealth;
                _currentHealthRatio = _health.NormalizedHealth;
                _ghostRatio = _currentHealthRatio;
                PushPropertiesToGpu();
                if (ShouldBeVisible())
                {
                    EnsureTicking();
                }
            }
        }

        public void SetColorTheme(Color healthColor, Color ghostColor)
        {
            _healthColor = healthColor;
            _ghostColor = ghostColor;
            PushPropertiesToGpu();
        }

        public void Configure(
            float heightOffset,
            float width,
            float height,
            float borderThickness,
            float segmentSize,
            float segmentGap,
            float notchWidth,
            HealthBarVisibilityPolicy policy)
        {
            _heightOffset = heightOffset;
            _barWidth = width;
            _barHeight = height;
            _borderThickness = borderThickness;
            _segmentSize = segmentSize;
            _segmentGap = segmentGap;
            _notchWidth = notchWidth;
            _visibilityPolicy = policy;

            ResolveTargetTransform();
            transform.position = (_targetTransform != null ? _targetTransform.position : transform.position) + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.identity;

            _currentAlpha = (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible) ? 1.0f : 0.0f;
            PushPropertiesToGpu();
            if (ShouldBeVisible())
            {
                EnsureTicking();
            }
        }

        public void Initialize(
            IHealthEntity health,
            float heightOffset = 1.2f,
            float segmentSize = 20.0f,
            HealthBarVisibilityPolicy policy = HealthBarVisibilityPolicy.AlwaysVisible)
        {
            _heightOffset = heightOffset;
            _segmentSize = segmentSize;

            ResolveTargetTransform();
            transform.position = (_targetTransform != null ? _targetTransform.position : transform.position) + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.identity;

            _visibilityPolicy = policy;
            _currentAlpha = (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible) ? 1.0f : 0.0f;

            if (health != null)
            {
                BindHealth(health);
            }
            else
            {
                PushPropertiesToGpu();
            }

            if (ShouldBeVisible())
            {
                EnsureTicking();
            }
        }

        /// <summary>
        /// Factory helper to attach an overhead health bar directly under an entity root.
        /// </summary>
        public static OverheadHealthBar Attach(
            GameObject characterRoot,
            float heightOffset = 1.2f,
            float width = 1.0f,
            float height = 0.12f,
            float borderThickness = 0.015f,
            float segmentSize = 20.0f,
            float segmentGap = 0.0f,
            float notchWidth = 0.025f,
            HealthBarVisibilityPolicy policy = HealthBarVisibilityPolicy.AlwaysVisible)
        {
            if (characterRoot == null) throw new ArgumentNullException(nameof(characterRoot));

            var health = characterRoot.GetComponent<IHealthEntity>() ?? characterRoot.GetComponentInParent<IHealthEntity>();

            var existing = characterRoot.GetComponentInChildren<OverheadHealthBar>();
            if (existing != null)
            {
                existing.Configure(heightOffset, width, height, borderThickness, segmentSize, segmentGap, notchWidth, policy);
                if (health != null) existing.BindHealth(health);
                return existing;
            }

            var barGo = new GameObject("[UI_OverheadHealthBar]");
            barGo.transform.SetParent(characterRoot.transform, false);

            var bar = barGo.AddComponent<OverheadHealthBar>();
            bar.Configure(heightOffset, width, height, borderThickness, segmentSize, segmentGap, notchWidth, policy);
            if (health != null) bar.BindHealth(health);

            return bar;
        }

        private void BindHealthInternal(IHealthEntity health)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamaged += HandleDamaged;
        }

        private void UnbindHealthInternal()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnDamaged -= HandleDamaged;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            _maxHealth = max;
            float newRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (newRatio < _currentHealthRatio)
            {
                // Damage taken: snap main bar down immediately, hold ghost catch-up bar
                _currentHealthRatio = newRatio;
                _ghostTimer = _ghostDelay;
            }
            else
            {
                // Healing or respawn: snap both bars immediately
                _currentHealthRatio = newRatio;
                _ghostRatio = newRatio;
                _ghostTimer = 0f;
            }

            PushPropertiesToGpu();
            EnsureTicking();
        }

        private void HandleDamaged(DamageResult result, HitContext context)
        {
            _combatLingerTimer = _combatLingerDuration;
            _flashTimer = _flashDuration;
            EnsureTicking();
        }

        /// <summary>
        /// Deterministic update driven by PGS.Core.Time.
        /// Runs only during active animations (ghost drain, hit flash, or alpha fade). Sleeps otherwise.
        /// Zero CPU transform rotation work (handled entirely on GPU via vertex shader).
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            // 1. Ghost catch-up drain animation
            if (_ghostTimer > 0f)
            {
                _ghostTimer -= deltaTime;
            }
            else if (_ghostRatio > _currentHealthRatio)
            {
                _ghostRatio = Mathf.MoveTowards(_ghostRatio, _currentHealthRatio, _ghostDrainSpeed * deltaTime);
            }
            else
            {
                _ghostRatio = _currentHealthRatio;
            }

            // 2. Hit flash decay
            if (_flashTimer > 0f)
            {
                _flashTimer -= deltaTime;
            }

            // 3. Combat linger timer
            if (_combatLingerTimer > 0f)
            {
                _combatLingerTimer -= deltaTime;
            }

            // 4. Resolve target alpha based on visibility policy
            float targetAlpha = ShouldBeVisible() ? 1.0f : 0.0f;

            // 5. Smooth alpha transition
            if (_fadeDuration > 0f)
            {
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, deltaTime / _fadeDuration);
            }
            else
            {
                _currentAlpha = targetAlpha;
            }

            // 6. Push to GPU
            PushPropertiesToGpu();

            // 7. Idle sleep optimization: If animations settled, sleep (0 CPU cycles)
            bool isAnimSettled = Mathf.Abs(_ghostRatio - _currentHealthRatio) < 0.001f && _flashTimer <= 0f && _ghostTimer <= 0f;
            bool isFullyHidden = _currentAlpha <= 0.001f && targetAlpha <= 0f;
            bool isFullyShownAndStable = _currentAlpha >= 0.999f && targetAlpha >= 1.0f &&
                (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible ||
                 (_visibilityPolicy == HealthBarVisibilityPolicy.InjuredOnly && _currentHealthRatio < 0.999f));

            if (isAnimSettled && (isFullyHidden || isFullyShownAndStable))
            {
                StopTicking();
            }
        }

        private bool ShouldBeVisible()
        {
            if (_health == null || !_health.IsAlive)
            {
                return false;
            }

            switch (_visibilityPolicy)
            {
                case HealthBarVisibilityPolicy.AlwaysVisible:
                    return true;

                case HealthBarVisibilityPolicy.InjuredOnly:
                    return _currentHealthRatio < 0.999f;

                case HealthBarVisibilityPolicy.CombatLinger:
                    return _combatLingerTimer > 0f;

                case HealthBarVisibilityPolicy.CombatOrInjured:
                    return _combatLingerTimer > 0f || _currentHealthRatio < 0.999f;

                default:
                    return true;
            }
        }

        private void EnsureTicking()
        {
            if (!_isTicking && isActiveAndEnabled)
            {
                PgsTime.Register(this, UpdateRate.Continuous, _timeChannel);
                _isTicking = true;
            }
        }

        private void StopTicking()
        {
            if (_isTicking)
            {
                PgsTime.Unregister(this);
                _isTicking = false;
            }
        }

        private void PushPropertiesToGpu()
        {
            if (_meshRenderer == null) return;

            float flashIntensity = _flashDuration > 0f ? Mathf.Clamp01(_flashTimer / _flashDuration) : 0f;

            _sharedPropBlock.Clear();
            _sharedPropBlock.SetFloat(HealthRatioId, _currentHealthRatio);
            _sharedPropBlock.SetFloat(GhostRatioId, _ghostRatio);
            _sharedPropBlock.SetFloat(FadeAlphaId, _currentAlpha);
            _sharedPropBlock.SetFloat(FlashIntensityId, flashIntensity);
            _sharedPropBlock.SetVector(BarDimensionsId, new Vector4(_barWidth, _barHeight, _borderThickness, _borderThickness));
            _sharedPropBlock.SetVector(DistanceScaleParamsId, new Vector4(
                _enableDistanceScaling ? 1.0f : 0.0f,
                _referenceDistance,
                _minScaleMultiplier,
                _maxScaleMultiplier));
            _sharedPropBlock.SetFloat(MaxHealthId, _maxHealth);
            _sharedPropBlock.SetFloat(SegmentSizeId, _segmentSize);
            _sharedPropBlock.SetFloat(SegmentGapId, _segmentGap);
            _sharedPropBlock.SetFloat(SlantId, _slant);
            _sharedPropBlock.SetFloat(NotchWidthId, _notchWidth);
            _sharedPropBlock.SetColor(HealthColorId, _healthColor);
            _sharedPropBlock.SetColor(GhostColorId, _ghostColor);
            _sharedPropBlock.SetColor(BackgroundColorId, _backgroundColor);
            _sharedPropBlock.SetColor(BorderColorId, _borderColor);

            _meshRenderer.SetPropertyBlock(_sharedPropBlock);
        }

        private static Mesh GetOrCreateSharedQuadMesh()
        {
            if (_sharedQuadMesh != null) return _sharedQuadMesh;

            _sharedQuadMesh = new Mesh
            {
                name = "Mesh_Shared_OverheadHealthBarQuad",
                vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3( 0.5f, -0.5f, 0f),
                    new Vector3(-0.5f,  0.5f, 0f),
                    new Vector3( 0.5f,  0.5f, 0f)
                },
                uv = new Vector2[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                },
                triangles = new int[] { 0, 2, 1, 2, 3, 1 }
            };
            _sharedQuadMesh.RecalculateBounds();
            // Expanded bounds prevent frustum culling from clipping the GPU-billboarded quad
            _sharedQuadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
            return _sharedQuadMesh;
        }

        private static Material GetOrCreateSharedMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;

            Shader shader = Shader.Find("PGS/UI/OverheadHealthBar");
            if (shader == null)
            {
                throw new InvalidOperationException("[OverheadHealthBar] Cannot find shader 'PGS/UI/OverheadHealthBar'! Ensure the shader is included in the project.");
            }

            _sharedMaterial = new Material(shader)
            {
                name = "Mat_Shared_OverheadHealthBar",
                enableInstancing = true
            };

            return _sharedMaterial;
        }
    }
}
