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
    /// Unified diegetic overhead vitals cluster managing both primary Health and secondary Resource (e.g. Pheromone) bars.
    /// Eliminates duplicate child GameObjects and redundant per-frame transform updates by consolidating
    /// multi-bar rendering onto a single coordinator GameObject [UI_OverheadVitals].
    /// Uses the single-pass procedural URP shader on shared 4-vertex quad meshes with GPU Instancing.
    /// Driven deterministically by PGS.Core.Time and sleeps (0 CPU cycles) when idle/settled.
    /// Follows Single Responsibility (SRP), Interface Segregation (ISP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OverheadVitals : MonoBehaviour, IUpdatable
    {
        private sealed class BarChannel
        {
            public GameObject GameObject;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock PropBlock;
            public bool IsActive;

            public float MaxValue = 100f;
            public float CurrentRatio = 1f;
            public float GhostRatio = 1f;
            public float GhostTimer;
            public float FlashTimer;

            public Vector4 Dimensions; // (Width, Height, BorderX, BorderY)
            public Vector4 BarOffset;  // (OffsetX, OffsetY in Billboard space, OffsetZ nudge, 0)
            public float SegmentSize;
            public float SegmentGap;
            public float NotchWidth;
            public float Slant;

            public Color PrimaryColor;
            public Color GhostColor;
            public Color BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            public Color BorderColor = new Color(0.02f, 0.02f, 0.02f, 1.0f);

            public float GhostDelay = 0.35f;
            public float GhostDrainSpeed = 1.2f;
        }

        // Static shader property IDs matching PGS/UI/OverheadHealthBar shader
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
        private static readonly int BarOffsetId = Shader.PropertyToID("_BarOffset");

        private static Mesh _sharedQuadMesh;
        private static Material _sharedMaterial;

        private readonly BarChannel _health = new BarChannel();
        private readonly BarChannel _resource = new BarChannel();

        private IHealthEntity _healthEntity;
        private IResourceEntity _resourceEntity;
        private Transform _targetTransform;

        [Header("Anchor & Stacking")]
        [SerializeField] private float _heightOffset = 1.2f;
        [SerializeField] private float _resourceBarSpacing = 0.015f;

        [Header("Distance Scaling (GPU-Evaluated)")]
        [SerializeField] private bool _enableDistanceScaling = true;
        [SerializeField] private float _referenceDistance = 8f;
        [SerializeField] private float _minScaleMultiplier = 0.75f;
        [SerializeField] private float _maxScaleMultiplier = 2.0f;

        [Header("Animation & Flash")]
        [SerializeField] private float _flashDuration = 0.18f;
        [SerializeField] private float _combatLingerDuration = 3.5f;
        [SerializeField] private float _fadeDuration = 0.20f;
        [SerializeField] private HealthBarVisibilityPolicy _visibilityPolicy = HealthBarVisibilityPolicy.AlwaysVisible;
        [SerializeField] private TimeChannel _timeChannel = TimeChannel.World;

        private float _combatLingerTimer;
        private float _currentAlpha = 1.0f;
        private bool _isTicking;

        public IHealthEntity BoundHealth => _healthEntity;
        public IResourceEntity BoundResource => _resourceEntity;

        public float HeightOffset
        {
            get => _heightOffset;
            set
            {
                _heightOffset = value;
                SyncWorldPosition();
            }
        }

        #region Lifecycle

        private void Awake()
        {
            EnsureSubQuadsInitialized();
            ResolveTargetTransform();
            SyncWorldPosition();

            _currentAlpha = (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible) ? 1.0f : 0.0f;
        }

        private void OnEnable()
        {
            ResolveTargetTransform();
            SyncWorldPosition();

            if (_healthEntity == null)
            {
                ResolveEntities();
            }

            if (_healthEntity != null)
            {
                BindHealthInternal(_healthEntity);
                _health.MaxValue = _healthEntity.MaxHealth;
                _health.CurrentRatio = _healthEntity.NormalizedHealth;
                _health.GhostRatio = _health.CurrentRatio;
                _health.GhostTimer = 0f;
                _health.FlashTimer = 0f;
            }

            if (_resourceEntity != null && _resource.IsActive)
            {
                BindResourceInternal(_resourceEntity);
                _resource.MaxValue = _resourceEntity.MaxResource;
                _resource.CurrentRatio = _resourceEntity.NormalizedResource;
                _resource.GhostRatio = _resource.CurrentRatio;
                _resource.GhostTimer = 0f;
                _resource.FlashTimer = 0f;
            }

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

            SyncWorldPosition();
        }

        private void OnDisable()
        {
            UnbindHealthInternal();
            UnbindResourceInternal();
            StopTicking();
        }

        private void SyncWorldPosition()
        {
            Vector3 targetPos = _targetTransform != null ? _targetTransform.position : transform.position;
            transform.position = targetPos + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.identity;
        }

        #endregion

        #region Sub-Quad Initialization & GPU Push

        private void EnsureSubQuadsInitialized()
        {
            Mesh mesh = GetOrCreateSharedQuadMesh();
            Material mat = GetOrCreateSharedMaterial();

            // 1. Health Bar Sub-Quad (at local position 0, 0, 0)
            if (_health.GameObject == null)
            {
                Transform existingH = transform.Find("HealthBarQuad");
                _health.GameObject = existingH != null ? existingH.gameObject : new GameObject("HealthBarQuad");
                _health.GameObject.transform.SetParent(transform, false);
                _health.GameObject.transform.localPosition = Vector3.zero;
                _health.GameObject.transform.localRotation = Quaternion.identity;
                _health.IsActive = true;
            }

            if (!_health.GameObject.TryGetComponent<MeshFilter>(out var healthFilter))
            {
                healthFilter = _health.GameObject.AddComponent<MeshFilter>();
            }
            healthFilter.sharedMesh = mesh;

            if (!_health.GameObject.TryGetComponent<MeshRenderer>(out _health.Renderer))
            {
                _health.Renderer = _health.GameObject.AddComponent<MeshRenderer>();
            }
            _health.Renderer.sharedMaterial = mat;
            _health.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _health.Renderer.receiveShadows = false;

            if (_health.PropBlock == null)
            {
                _health.PropBlock = new MaterialPropertyBlock();
            }

            // 2. Resource Bar Sub-Quad (at local position stacked beneath health bar)
            if (_resource.GameObject == null)
            {
                Transform existingR = transform.Find("ResourceBarQuad");
                _resource.GameObject = existingR != null ? existingR.gameObject : new GameObject("ResourceBarQuad");
                _resource.GameObject.transform.SetParent(transform, false);
                _resource.GameObject.transform.localRotation = Quaternion.identity;
            }

            if (!_resource.GameObject.TryGetComponent<MeshFilter>(out var resourceFilter))
            {
                resourceFilter = _resource.GameObject.AddComponent<MeshFilter>();
            }
            resourceFilter.sharedMesh = mesh;

            if (!_resource.GameObject.TryGetComponent<MeshRenderer>(out _resource.Renderer))
            {
                _resource.Renderer = _resource.GameObject.AddComponent<MeshRenderer>();
            }
            _resource.Renderer.sharedMaterial = mat;
            _resource.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _resource.Renderer.receiveShadows = false;

            if (_resource.PropBlock == null)
            {
                _resource.PropBlock = new MaterialPropertyBlock();
            }

            UpdateResourceQuadPlacement();
        }

        private void UpdateResourceQuadPlacement()
        {
            if (_health.GameObject != null)
            {
                _health.GameObject.SetActive(_health.IsActive);
                _health.GameObject.transform.localPosition = Vector3.zero;
            }

            if (_resource.GameObject != null)
            {
                _resource.GameObject.SetActive(_resource.IsActive);
                _resource.GameObject.transform.localPosition = Vector3.zero;
            }
        }

        private void PushPropertiesToGpu()
        {
            if (_health.IsActive && _health.Renderer != null)
            {
                PushChannelToGpu(_health);
            }

            if (_resource.IsActive && _resource.Renderer != null)
            {
                PushChannelToGpu(_resource);
            }
        }

        private void PushChannelToGpu(BarChannel channel)
        {
            float flashIntensity = _flashDuration > 0f ? Mathf.Clamp01(channel.FlashTimer / _flashDuration) : 0f;

            channel.PropBlock.Clear();
            channel.PropBlock.SetFloat(HealthRatioId, channel.CurrentRatio);
            channel.PropBlock.SetFloat(GhostRatioId, channel.GhostRatio);
            channel.PropBlock.SetFloat(FadeAlphaId, _currentAlpha);
            channel.PropBlock.SetFloat(FlashIntensityId, flashIntensity);
            channel.PropBlock.SetVector(BarDimensionsId, channel.Dimensions);
            channel.PropBlock.SetVector(BarOffsetId, channel.BarOffset);
            channel.PropBlock.SetVector(DistanceScaleParamsId, new Vector4(
                _enableDistanceScaling ? 1.0f : 0.0f,
                _referenceDistance,
                _minScaleMultiplier,
                _maxScaleMultiplier));
            channel.PropBlock.SetFloat(MaxHealthId, channel.MaxValue);
            channel.PropBlock.SetFloat(SegmentSizeId, channel.SegmentSize);
            channel.PropBlock.SetFloat(SegmentGapId, channel.SegmentGap);
            channel.PropBlock.SetFloat(SlantId, channel.Slant);
            channel.PropBlock.SetFloat(NotchWidthId, channel.NotchWidth);
            channel.PropBlock.SetColor(HealthColorId, channel.PrimaryColor);
            channel.PropBlock.SetColor(GhostColorId, channel.GhostColor);
            channel.PropBlock.SetColor(BackgroundColorId, channel.BackgroundColor);
            channel.PropBlock.SetColor(BorderColorId, channel.BorderColor);

            channel.Renderer.SetPropertyBlock(channel.PropBlock);
        }

        #endregion

        #region Animation & Ticking

        /// <summary>
        /// Deterministic update driven by PGS.Core.Time.
        /// Runs only during active animations (ghost drain, hit flash, or alpha fade). Sleeps otherwise.
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            // 1. Health channel animations
            UpdateChannelAnimations(_health, deltaTime);

            // 2. Resource channel animations
            if (_resource.IsActive)
            {
                UpdateChannelAnimations(_resource, deltaTime);
            }

            // 3. Combat activity linger timer
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

            // 6. Push updated state to GPU
            PushPropertiesToGpu();

            // 7. Idle sleep optimization (0 CPU cycles when visuals settle)
            bool healthSettled = Mathf.Abs(_health.GhostRatio - _health.CurrentRatio) < 0.001f && _health.FlashTimer <= 0f && _health.GhostTimer <= 0f;
            bool resourceSettled = !_resource.IsActive || (Mathf.Abs(_resource.GhostRatio - _resource.CurrentRatio) < 0.001f && _resource.FlashTimer <= 0f && _resource.GhostTimer <= 0f);
            bool isFullyHidden = _currentAlpha <= 0.001f && targetAlpha <= 0f;
            bool isFullyShownAndStable = _currentAlpha >= 0.999f && targetAlpha >= 1.0f &&
                (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible ||
                 (_visibilityPolicy == HealthBarVisibilityPolicy.InjuredOnly && _health.CurrentRatio < 0.999f));

            if (healthSettled && resourceSettled && (isFullyHidden || isFullyShownAndStable))
            {
                StopTicking();
            }
        }

        private static void UpdateChannelAnimations(BarChannel channel, float deltaTime)
        {
            if (channel.GhostTimer > 0f)
            {
                channel.GhostTimer -= deltaTime;
            }
            else if (channel.GhostRatio > channel.CurrentRatio)
            {
                channel.GhostRatio = Mathf.MoveTowards(channel.GhostRatio, channel.CurrentRatio, channel.GhostDrainSpeed * deltaTime);
            }
            else
            {
                channel.GhostRatio = channel.CurrentRatio;
            }

            if (channel.FlashTimer > 0f)
            {
                channel.FlashTimer -= deltaTime;
            }
        }

        private bool ShouldBeVisible()
        {
            if (_healthEntity == null || !_healthEntity.IsAlive)
            {
                return false;
            }

            bool isResourceDraining = _resource.IsActive && _resourceEntity != null && _resourceEntity.IsDraining;

            switch (_visibilityPolicy)
            {
                case HealthBarVisibilityPolicy.AlwaysVisible:
                    return true;

                case HealthBarVisibilityPolicy.InjuredOnly:
                    return _health.CurrentRatio < 0.999f || (_resource.IsActive && _resource.CurrentRatio < 0.999f);

                case HealthBarVisibilityPolicy.CombatLinger:
                    return _combatLingerTimer > 0f || isResourceDraining;

                case HealthBarVisibilityPolicy.CombatOrInjured:
                    return _combatLingerTimer > 0f || _health.CurrentRatio < 0.999f || isResourceDraining;

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

        #endregion

        #region Event Bindings

        private void ResolveTargetTransform()
        {
            if (transform.parent == null)
            {
                _targetTransform = transform;
                return;
            }

            var antChar = transform.parent.GetComponent<AntCharacter>();
            if (antChar != null && antChar.TargetTransform != null)
            {
                _targetTransform = antChar.TargetTransform;
                return;
            }

            Transform visual = transform.parent.Find("Visual");
            if (visual != null)
            {
                _targetTransform = visual;
                return;
            }

            _targetTransform = transform.parent;
        }

        private void ResolveEntities()
        {
            if (_healthEntity == null)
            {
                _healthEntity = GetComponentInParent<IHealthEntity>();
            }

            if (_resourceEntity == null)
            {
                _resourceEntity = GetComponentInParent<IResourceEntity>();
            }
        }

        private void BindHealthInternal(IHealthEntity health)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamaged += HandleDamaged;
        }

        private void UnbindHealthInternal()
        {
            if (_healthEntity != null)
            {
                _healthEntity.OnHealthChanged -= HandleHealthChanged;
                _healthEntity.OnDamaged -= HandleDamaged;
            }
        }

        private void BindResourceInternal(IResourceEntity resource)
        {
            resource.OnResourceChanged += HandleResourceChanged;
        }

        private void UnbindResourceInternal()
        {
            if (_resourceEntity != null)
            {
                _resourceEntity.OnResourceChanged -= HandleResourceChanged;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            _health.MaxValue = max;
            float newRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (newRatio < _health.CurrentRatio)
            {
                _health.CurrentRatio = newRatio;
                _health.GhostTimer = _health.GhostDelay;
            }
            else
            {
                _health.CurrentRatio = newRatio;
                _health.GhostRatio = newRatio;
                _health.GhostTimer = 0f;
            }

            PushPropertiesToGpu();
            EnsureTicking();
        }

        private void HandleDamaged(DamageResult result, HitContext context)
        {
            _combatLingerTimer = _combatLingerDuration;
            _health.FlashTimer = _flashDuration;
            EnsureTicking();
        }

        private void HandleResourceChanged(float current, float max)
        {
            _resource.MaxValue = max;
            float newRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (newRatio < _resource.CurrentRatio)
            {
                _resource.CurrentRatio = newRatio;
                _resource.GhostTimer = _resource.GhostDelay;
                _combatLingerTimer = _combatLingerDuration;

                if (newRatio <= 0.001f)
                {
                    _resource.FlashTimer = _flashDuration;
                }
            }
            else
            {
                _resource.CurrentRatio = newRatio;
                _resource.GhostRatio = newRatio;
                _resource.GhostTimer = 0f;
            }

            PushPropertiesToGpu();
            EnsureTicking();
        }

        #endregion

        #region Factory Attachment API

        /// <summary>
        /// Factory helper to attach an overhead vitals cluster directly under an entity root,
        /// configuring primary health and secondary resource channels from the provided CharacterDefinition.
        /// </summary>
        public static OverheadVitals Attach(GameObject characterRoot, CharacterDefinition definition)
        {
            if (characterRoot == null) throw new ArgumentNullException(nameof(characterRoot));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var existing = characterRoot.GetComponentInChildren<OverheadVitals>();
            if (existing == null)
            {
                var vitalsGo = new GameObject("[UI_OverheadVitals]");
                vitalsGo.transform.SetParent(characterRoot.transform, false);
                existing = vitalsGo.AddComponent<OverheadVitals>();
            }

            existing.Configure(definition);
            return existing;
        }

        public void Configure(CharacterDefinition def)
        {
            EnsureSubQuadsInitialized();

            _heightOffset = def.HealthBarHeightOffset;
            _visibilityPolicy = def.HealthBarVisibility;

            // 1. Configure Health Channel
            _health.IsActive = def.EnableHealthBar;
            _health.Dimensions = new Vector4(def.HealthBarWidth, def.HealthBarHeight, def.HealthBarBorderThickness, def.HealthBarBorderThickness);
            _health.BarOffset = Vector4.zero;
            _health.SegmentSize = def.HealthBarSegmentSize;
            _health.SegmentGap = def.HealthBarSegmentGap;
            _health.NotchWidth = def.HealthBarNotchWidth;
            _health.PrimaryColor = def.HealthBarColor;
            _health.GhostColor = def.HealthGhostColor;
            _health.GhostDelay = 0.35f;
            _health.GhostDrainSpeed = 1.2f;

            // 2. Configure Resource Channel (Stacks below Health Bar, matching width)
            bool hasResource = def.EnablePheromoneBar && def.HasTrailActions;
            _resource.IsActive = hasResource;
            _resourceBarSpacing = def.PheromoneBarSpacing;
            _resource.Dimensions = new Vector4(def.HealthBarWidth, def.PheromoneBarHeight, def.PheromoneBarBorderThickness, def.PheromoneBarBorderThickness);
            float offsetY = -((def.HealthBarHeight + def.PheromoneBarHeight) * 0.5f + def.PheromoneBarSpacing);
            _resource.BarOffset = new Vector4(0f, offsetY, 1f, 0f);
            _resource.SegmentSize = 0f; // Continuous smooth fluid bar
            _resource.SegmentGap = 0f;
            _resource.NotchWidth = 0f;
            _resource.PrimaryColor = def.PheromoneBarColor;
            _resource.GhostColor = def.PheromoneGhostColor;
            _resource.GhostDelay = 0.20f;
            _resource.GhostDrainSpeed = 1.5f;

            UpdateResourceQuadPlacement();

            // Bind entities
            var health = GetComponentInParent<IHealthEntity>();
            if (health != null)
            {
                BindHealth(health);
            }

            if (hasResource)
            {
                var resource = GetComponentInParent<IResourceEntity>();
                if (resource != null)
                {
                    BindResource(resource);
                }
            }

            _currentAlpha = (_visibilityPolicy == HealthBarVisibilityPolicy.AlwaysVisible) ? 1.0f : 0.0f;
            PushPropertiesToGpu();

            if (ShouldBeVisible())
            {
                EnsureTicking();
            }
        }

        /// <summary>
        /// Explicitly binds or rebinds the primary health entity (e.g. upon pooled respawn).
        /// </summary>
        public void BindHealth(IHealthEntity health)
        {
            UnbindHealthInternal();
            _healthEntity = health;
            if (_healthEntity != null)
            {
                BindHealthInternal(_healthEntity);
                _health.MaxValue = _healthEntity.MaxHealth;
                _health.CurrentRatio = _healthEntity.NormalizedHealth;
                _health.GhostRatio = _health.CurrentRatio;
                _health.GhostTimer = 0f;
                _health.FlashTimer = 0f;
            }
            PushPropertiesToGpu();
            if (ShouldBeVisible())
            {
                EnsureTicking();
            }
        }

        /// <summary>
        /// Explicitly binds or rebinds the secondary resource entity (e.g. upon pooled respawn).
        /// </summary>
        public void BindResource(IResourceEntity resource)
        {
            UnbindResourceInternal();
            _resourceEntity = resource;
            if (_resourceEntity != null)
            {
                BindResourceInternal(_resourceEntity);
                _resource.MaxValue = _resourceEntity.MaxResource;
                _resource.CurrentRatio = _resourceEntity.NormalizedResource;
                _resource.GhostRatio = _resource.CurrentRatio;
                _resource.GhostTimer = 0f;
                _resource.FlashTimer = 0f;
            }
            PushPropertiesToGpu();
            if (ShouldBeVisible())
            {
                EnsureTicking();
            }
        }

        #endregion

        #region Shared Mesh & Material Helpers

        private static Mesh GetOrCreateSharedQuadMesh()
        {
            if (_sharedQuadMesh != null) return _sharedQuadMesh;

            _sharedQuadMesh = new Mesh
            {
                name = "Mesh_Shared_OverheadVitalsQuad",
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
            _sharedQuadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
            return _sharedQuadMesh;
        }

        private static Material GetOrCreateSharedMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;

            Shader shader = Shader.Find("PGS/UI/OverheadHealthBar");
            if (shader == null)
            {
                throw new InvalidOperationException("[OverheadVitals] Cannot find shader 'PGS/UI/OverheadHealthBar'! Ensure the shader is included in the project.");
            }

            _sharedMaterial = new Material(shader)
            {
                name = "Mat_Shared_OverheadVitals",
                enableInstancing = true
            };

            return _sharedMaterial;
        }

        #endregion
    }
}
