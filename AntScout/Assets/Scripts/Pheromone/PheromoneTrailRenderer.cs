using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Visualizes independent pheromone scent strokes in 3D space using pooled LineRenderers.
    /// Each continuous keypress stroke maintains its own distinct color and decay lifecycle without bridge lines.
    /// Implements ILateUpdatable to render after physics/movement ticks on TimeChannel.World.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PheromoneTrailRenderer : MonoBehaviour, ILateUpdatable
    {
        public enum TrailAlignmentMode
        {
            [Tooltip("Lies flat on the floor (normal points straight UP, width spans horizontal ground).")]
            FlatOnGround = 0,

            [Tooltip("Faces the camera viewport (billboarded ribbon, visible from all camera angles).")]
            FaceCamera = 1
        }

        [Header("References")]
        [Tooltip("The trail emitter supplying active scent segments.")]
        [SerializeField] private PheromoneTrailEmitter _emitter;

        [Tooltip("Configuration asset for obtaining node lifetime to compute alpha fades.")]
        [SerializeField] private ScoutConfigSO _config;

        [Header("Orientation & Alignment")]
        [Tooltip("Choose whether the ribbon lies flat like paint on the floor, or billboards to face the camera.")]
        [SerializeField] private TrailAlignmentMode _alignmentMode = TrailAlignmentMode.FlatOnGround;

        [Header("Visual Styling")]
        [Tooltip("Base color for Recruitment trails (guides allied workers/soldiers).")]
        [SerializeField] private Color _recruitmentColor = new Color(0.0f, 0.85f, 1.0f, 0.85f);

        [Tooltip("Base color for Alarm trails (focus fire).")]
        [SerializeField] private Color _alarmColor = new Color(1.0f, 0.2f, 0.1f, 0.85f);

        [Tooltip("Base color for Repellent trails (avoidance).")]
        [SerializeField] private Color _repellentColor = new Color(0.8f, 0.1f, 0.9f, 0.85f);

        [Tooltip("Ribbon width at full intensity.")]
        [Range(0.05f, 2.0f)]
        [SerializeField] private float _trailWidth = 0.4f;

        [Tooltip("Vertical elevation offset above ground to prevent Z-fighting with 3D floor or terrain.")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float _groundYOffset = 0.05f;

        private Material _trailMaterial;
        private readonly Dictionary<PheromoneTrailSegment, LineRenderer> _segmentRenderers = new Dictionary<PheromoneTrailSegment, LineRenderer>();
        private readonly Queue<LineRenderer> _linePool = new Queue<LineRenderer>();
        private Vector3[] _positionBuffer = new Vector3[64];

        private readonly Gradient _gradientCache = new Gradient();
        private readonly GradientColorKey[] _colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

        private void Awake()
        {
            LineRenderer rootLine = GetComponent<LineRenderer>();
            _trailMaterial = rootLine.sharedMaterial;
            rootLine.enabled = false; // Root acts as template and pool container

            if (_emitter == null)
            {
                _emitter = GetComponentInParent<PheromoneTrailEmitter>();
            }

            if (_emitter == null)
            {
                throw new InvalidOperationException(
                    $"[PheromoneTrailRenderer] Missing required PheromoneTrailEmitter reference on '{gameObject.name}'. " +
                    $"Please assign it in the Inspector.");
            }

            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[PheromoneTrailRenderer] Missing required ScoutConfigSO reference on '{gameObject.name}'. " +
                    $"Please assign it in the Inspector.");
            }
        }

        private void OnEnable()
        {
            PgsTime.Register((ILateUpdatable)this, TimeChannel.World);
            _emitter.OnSegmentCreated += HandleSegmentCreated;
            _emitter.OnSegmentDestroyed += HandleSegmentDestroyed;
        }

        private void OnDisable()
        {
            PgsTime.Unregister((ILateUpdatable)this);
            _emitter.OnSegmentCreated -= HandleSegmentCreated;
            _emitter.OnSegmentDestroyed -= HandleSegmentDestroyed;
            ClearAllRenderers();
        }

        private void HandleSegmentCreated(PheromoneTrailSegment segment)
        {
            if (segment == null || _segmentRenderers.ContainsKey(segment)) return;

            LineRenderer line = GetOrCreateLineRenderer();
            ConfigureLineRenderer(line, segment.Type);
            _segmentRenderers[segment] = line;
        }

        private void HandleSegmentDestroyed(PheromoneTrailSegment segment)
        {
            if (segment != null && _segmentRenderers.TryGetValue(segment, out LineRenderer line))
            {
                _segmentRenderers.Remove(segment);
                RecycleLineRenderer(line);
            }
        }

        private LineRenderer GetOrCreateLineRenderer()
        {
            if (_linePool.Count > 0)
            {
                LineRenderer pooled = _linePool.Dequeue();
                pooled.gameObject.SetActive(true);
                pooled.enabled = true;
                return pooled;
            }

            GameObject childObj = new GameObject($"SegmentLine_{_segmentRenderers.Count + _linePool.Count}");
            childObj.transform.SetParent(transform, false);

            LineRenderer newLine = childObj.AddComponent<LineRenderer>();
            if (_trailMaterial != null)
            {
                newLine.material = _trailMaterial;
            }

            return newLine;
        }

        private void RecycleLineRenderer(LineRenderer line)
        {
            if (line == null) return;
            line.positionCount = 0;
            line.enabled = false;
            line.gameObject.SetActive(false);
            _linePool.Enqueue(line);
        }

        private void ClearAllRenderers()
        {
            foreach (var kvp in _segmentRenderers)
            {
                RecycleLineRenderer(kvp.Value);
            }
            _segmentRenderers.Clear();
        }

        private void ConfigureLineRenderer(LineRenderer line, PheromoneType type)
        {
            line.useWorldSpace = true;
            line.startWidth = _trailWidth;
            line.endWidth = _trailWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.positionCount = 0;

            if (_alignmentMode == TrailAlignmentMode.FlatOnGround)
            {
                line.alignment = LineAlignment.TransformZ;
                line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                line.alignment = LineAlignment.View;
            }
        }

        public void OnLateUpdate(float deltaTime)
        {
            float currentTime = PgsTime.ElapsedTime;
            float lifetime = _config.NodeLifetime;

            // Render each active segment independently
            foreach (var kvp in _segmentRenderers)
            {
                PheromoneTrailSegment segment = kvp.Key;
                LineRenderer line = kvp.Value;

                if (_alignmentMode == TrailAlignmentMode.FlatOnGround)
                {
                    line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }

                RenderSingleSegment(segment, line, currentTime, lifetime);
            }
        }

        private void RenderSingleSegment(PheromoneTrailSegment segment, LineRenderer line, float currentTime, float lifetime)
        {
            IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
            int count = nodes.Count;

            if (count < 2)
            {
                line.positionCount = 0;
                return;
            }

            if (_positionBuffer.Length < count)
            {
                _positionBuffer = new Vector3[Mathf.NextPowerOfTwo(count)];
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = nodes[i].Position;
                _positionBuffer[i] = new Vector3(pos.x, pos.y + _groundYOffset, pos.z);
            }

            line.positionCount = count;
            line.SetPositions(_positionBuffer);

            Color baseColor = segment.Type switch
            {
                PheromoneType.Recruitment => _recruitmentColor,
                PheromoneType.Alarm => _alarmColor,
                PheromoneType.Repellent => _repellentColor,
                _ => _recruitmentColor
            };

            float oldestIntensity = nodes[0].GetCurrentIntensity(currentTime, lifetime);
            float newestIntensity = nodes[count - 1].GetCurrentIntensity(currentTime, lifetime);

            _colorKeys[0] = new GradientColorKey(baseColor, 0.0f);
            _colorKeys[1] = new GradientColorKey(baseColor, 1.0f);
            _alphaKeys[0] = new GradientAlphaKey(oldestIntensity * baseColor.a, 0.0f);
            _alphaKeys[1] = new GradientAlphaKey(newestIntensity * baseColor.a, 1.0f);

            _gradientCache.SetKeys(_colorKeys, _alphaKeys);
            line.colorGradient = _gradientCache;
        }
    }
}
