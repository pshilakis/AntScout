using System;
using System.Collections.Generic;
using AntScout.Core.Enums;
using AntScout.Match;
using PGS.Core.Time;
using PGS.FogOfWar.Core;
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

        public enum EnemyTrailVisibilityMode
        {
            [Tooltip("Render enemy trails with subdued width, faint opacity, and muted tint.")]
            Subdued = 0,

            [Tooltip("Completely hide enemy trails from the local player's view.")]
            Hidden = 1,

            [Tooltip("Render enemy trails with full standard styling (identical to player trails).")]
            Full = 2
        }

        [Header("References")]
        [Tooltip("The trail emitter supplying active scent segments.")]
        [SerializeField] private PheromoneTrailEmitter _emitter;

        [Header("Enemy Trail Filtering")]
        [Tooltip("Visibility mode for trails belonging to enemy colonies.")]
        [SerializeField] private EnemyTrailVisibilityMode _enemyTrailVisibility = EnemyTrailVisibilityMode.Subdued;

        [Tooltip("When enabled, enemy pheromone trail segments outside the local player's active Fog of War vision are completely culled.")]
        [SerializeField] private bool _cullEnemyTrailsInFog = true;

        [Tooltip("Width scale multiplier for enemy trails when in Subdued mode.")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float _enemySubduedWidthScale = 0.65f;

        [Tooltip("Alpha/opacity scale multiplier for enemy trails when in Subdued mode.")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float _enemySubduedAlphaScale = 0.65f;

        [Tooltip("Custom color override for enemy trails in Subdued mode (e.g. muted red/orange).")]
        [SerializeField] private Color _enemyTrailTint = new Color(0.95f, 0.4f, 0.2f, 0.65f);

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

        private struct NodeRun
        {
            public int Start;
            public int Count;
        }

        private Material _trailMaterial;
        private readonly Dictionary<PheromoneTrailSegment, LineRenderer> _segmentRenderers = new Dictionary<PheromoneTrailSegment, LineRenderer>();
        private readonly Dictionary<PheromoneTrailSegment, List<LineRenderer>> _auxiliaryRenderers = new Dictionary<PheromoneTrailSegment, List<LineRenderer>>();
        private readonly Queue<LineRenderer> _linePool = new Queue<LineRenderer>();
        private Vector3[] _positionBuffer = new Vector3[64];
        private readonly NodeRun[] _runBuffer = new NodeRun[16];

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
            if (segment == null) return;

            if (_segmentRenderers.TryGetValue(segment, out LineRenderer line))
            {
                _segmentRenderers.Remove(segment);
                RecycleLineRenderer(line);
            }

            RecycleAuxiliaryLines(segment);
            _auxiliaryRenderers.Remove(segment);
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

            foreach (var auxList in _auxiliaryRenderers.Values)
            {
                for (int i = 0; i < auxList.Count; i++)
                {
                    RecycleLineRenderer(auxList[i]);
                }
            }
            _auxiliaryRenderers.Clear();
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

        public EnemyTrailVisibilityMode EnemyTrailVisibility
        {
            get => _enemyTrailVisibility;
            set => _enemyTrailVisibility = value;
        }

        public void OnLateUpdate(float deltaTime)
        {
            float currentTime = PgsTime.ElapsedTime;
            float lifetime = _emitter != null ? _emitter.NodeLifetime : 12.0f;

            bool isEnemy = false;
            if (_emitter != null && MatchDirector.Instance != null)
            {
                isEnemy = _emitter.ColonyTeamId != MatchDirector.Instance.PlayerTeamId;
            }

            if (isEnemy && _enemyTrailVisibility == EnemyTrailVisibilityMode.Hidden)
            {
                foreach (var line in _segmentRenderers.Values)
                {
                    if (line.enabled) line.enabled = false;
                }
                foreach (var auxList in _auxiliaryRenderers.Values)
                {
                    for (int i = 0; i < auxList.Count; i++)
                    {
                        if (auxList[i].enabled) auxList[i].enabled = false;
                    }
                }
                return;
            }

            // Render each active segment independently
            foreach (var kvp in _segmentRenderers)
            {
                PheromoneTrailSegment segment = kvp.Key;
                LineRenderer line = kvp.Value;

                if (!line.enabled) line.enabled = true;

                if (_alignmentMode == TrailAlignmentMode.FlatOnGround)
                {
                    line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }

                RenderSingleSegment(segment, line, currentTime, lifetime, isEnemy);
            }
        }

        private void RenderSingleSegment(PheromoneTrailSegment segment, LineRenderer line, float currentTime, float lifetime, bool isEnemy)
        {
            IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
            int count = nodes.Count;

            if (count < 2)
            {
                line.positionCount = 0;
                RecycleAuxiliaryLines(segment);
                return;
            }

            Color baseColor = segment.Type switch
            {
                PheromoneType.Recruitment => _recruitmentColor,
                PheromoneType.Alarm => _alarmColor,
                PheromoneType.Repellent => _repellentColor,
                _ => _recruitmentColor
            };

            float alphaMultiplier = 1.0f;
            float widthMultiplier = 1.0f;

            if (isEnemy && _enemyTrailVisibility == EnemyTrailVisibilityMode.Subdued)
            {
                baseColor = _enemyTrailTint;
                alphaMultiplier = _enemySubduedAlphaScale;
                widthMultiplier = _enemySubduedWidthScale;
            }

            int localTeamId = MatchDirector.Instance != null ? MatchDirector.Instance.PlayerTeamId : 0;
            bool applyFogCull = isEnemy && _cullEnemyTrailsInFog && FogOfWarManager.Instance != null;

            if (applyFogCull)
            {
                // Scan contiguous visible runs (within active player line-of-sight)
                int runCount = 0;
                int currentStart = -1;

                for (int i = 0; i < count; i++)
                {
                    bool isVisible = FogOfWarManager.Instance.IsVisible(nodes[i].Position, localTeamId);
                    if (isVisible)
                    {
                        if (currentStart == -1) currentStart = i;
                    }
                    else
                    {
                        if (currentStart != -1)
                        {
                            int runLen = i - currentStart;
                            if (runLen >= 2 && runCount < _runBuffer.Length)
                            {
                                _runBuffer[runCount++] = new NodeRun { Start = currentStart, Count = runLen };
                            }
                            currentStart = -1;
                        }
                    }
                }

                if (currentStart != -1)
                {
                    int runLen = count - currentStart;
                    if (runLen >= 2 && runCount < _runBuffer.Length)
                    {
                        _runBuffer[runCount++] = new NodeRun { Start = currentStart, Count = runLen };
                    }
                }

                // If no run has at least 2 visible nodes, hide the line completely
                if (runCount == 0)
                {
                    line.positionCount = 0;
                    RecycleAuxiliaryLines(segment);
                    return;
                }

                // Render primary run on main LineRenderer
                RenderNodeSlice(line, nodes, _runBuffer[0].Start, _runBuffer[0].Count, baseColor, alphaMultiplier, widthMultiplier, currentTime, lifetime);

                // If there are multiple disjoint visible runs, render them on pooled auxiliary lines
                if (runCount > 1)
                {
                    if (!_auxiliaryRenderers.TryGetValue(segment, out List<LineRenderer> auxList))
                    {
                        auxList = new List<LineRenderer>(2);
                        _auxiliaryRenderers[segment] = auxList;
                    }

                    int neededAux = runCount - 1;
                    while (auxList.Count < neededAux)
                    {
                        LineRenderer extraLine = GetOrCreateLineRenderer();
                        ConfigureLineRenderer(extraLine, segment.Type);
                        auxList.Add(extraLine);
                    }

                    for (int r = 1; r < runCount; r++)
                    {
                        LineRenderer extraLine = auxList[r - 1];
                        if (!extraLine.enabled) extraLine.enabled = true;
                        if (_alignmentMode == TrailAlignmentMode.FlatOnGround)
                        {
                            extraLine.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                        }
                        RenderNodeSlice(extraLine, nodes, _runBuffer[r].Start, _runBuffer[r].Count, baseColor, alphaMultiplier, widthMultiplier, currentTime, lifetime);
                    }

                    // Recycle any surplus aux lines
                    for (int a = neededAux; a < auxList.Count; a++)
                    {
                        RecycleLineRenderer(auxList[a]);
                    }
                    if (auxList.Count > neededAux)
                    {
                        auxList.RemoveRange(neededAux, auxList.Count - neededAux);
                    }
                }
                else
                {
                    RecycleAuxiliaryLines(segment);
                }
            }
            else
            {
                // Allied trail or Fog of War disabled: render full segment
                RecycleAuxiliaryLines(segment);
                RenderNodeSlice(line, nodes, 0, count, baseColor, alphaMultiplier, widthMultiplier, currentTime, lifetime);
            }
        }

        private void RecycleAuxiliaryLines(PheromoneTrailSegment segment)
        {
            if (_auxiliaryRenderers.TryGetValue(segment, out List<LineRenderer> auxList))
            {
                for (int i = 0; i < auxList.Count; i++)
                {
                    RecycleLineRenderer(auxList[i]);
                }
                auxList.Clear();
            }
        }

        private void RenderNodeSlice(
            LineRenderer line,
            IReadOnlyList<PheromoneNode> nodes,
            int startIdx,
            int sliceCount,
            Color baseColor,
            float alphaMultiplier,
            float widthMultiplier,
            float currentTime,
            float lifetime)
        {
            if (_positionBuffer.Length < sliceCount)
            {
                _positionBuffer = new Vector3[Mathf.NextPowerOfTwo(sliceCount)];
            }

            for (int i = 0; i < sliceCount; i++)
            {
                Vector3 pos = nodes[startIdx + i].Position;
                _positionBuffer[i] = new Vector3(pos.x, pos.y + _groundYOffset, pos.z);
            }

            line.positionCount = sliceCount;
            line.SetPositions(_positionBuffer);

            float oldestIntensity = nodes[startIdx].GetCurrentIntensity(currentTime, lifetime);
            float newestIntensity = nodes[startIdx + sliceCount - 1].GetCurrentIntensity(currentTime, lifetime);
            float avgStrength = (nodes[startIdx].Strength + nodes[startIdx + sliceCount - 1].Strength) * 0.5f;
            float strengthMultiplier = Mathf.Lerp(1.0f, 1.5f, Mathf.Clamp01((avgStrength - 1f) / (PheromoneNode.MaxStrength - 1f)));
            float currentWidth = _trailWidth * strengthMultiplier * widthMultiplier;
            line.startWidth = currentWidth;
            line.endWidth = currentWidth;

            _colorKeys[0] = new GradientColorKey(baseColor, 0.0f);
            _colorKeys[1] = new GradientColorKey(baseColor, 1.0f);
            _alphaKeys[0] = new GradientAlphaKey(oldestIntensity * baseColor.a * alphaMultiplier, 0.0f);
            _alphaKeys[1] = new GradientAlphaKey(newestIntensity * baseColor.a * alphaMultiplier, 1.0f);

            _gradientCache.SetKeys(_colorKeys, _alphaKeys);
            line.colorGradient = _gradientCache;
        }
    }
}
