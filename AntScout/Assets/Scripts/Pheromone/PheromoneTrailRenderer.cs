using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Visualizes active pheromone scent nodes in 3D space using Unity's LineRenderer.
    /// Encapsulates all rendering concerns, adhering strictly to the Single Responsibility Principle (SRP).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PheromoneTrailRenderer : MonoBehaviour
    {
        public enum TrailAlignmentMode
        {
            [Tooltip("Lies flat on the floor (normal points straight UP, width spans horizontal ground).")]
            FlatOnGround = 0,

            [Tooltip("Faces the camera viewport (billboarded ribbon, visible from all camera angles).")]
            FaceCamera = 1
        }

        [Header("References")]
        [Tooltip("The trail emitter supplying the active scent node coordinates.")]
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

        private LineRenderer _lineRenderer;
        private Vector3[] _positionBuffer = new Vector3[64];

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();

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

            ConfigureLineRenderer();
        }

        private void OnEnable()
        {
            _emitter.OnTrailUpdated += RenderTrail;
        }

        private void OnDisable()
        {
            _emitter.OnTrailUpdated -= RenderTrail;
        }

        public void SetAlignmentMode(TrailAlignmentMode mode)
        {
            _alignmentMode = mode;
            ConfigureLineRenderer();
        }

        private void ConfigureLineRenderer()
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = _trailWidth;
            _lineRenderer.endWidth = _trailWidth;
            _lineRenderer.numCapVertices = 4;
            _lineRenderer.numCornerVertices = 4;
            _lineRenderer.positionCount = 0;

            if (_alignmentMode == TrailAlignmentMode.FlatOnGround)
            {
                _lineRenderer.alignment = LineAlignment.TransformZ;
                // Rotate transform so local +Z points directly world UP (+Y)
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                _lineRenderer.alignment = LineAlignment.View;
            }
        }

        private void LateUpdate()
        {
            // Maintain world upright orientation if parent scout rotates
            if (_alignmentMode == TrailAlignmentMode.FlatOnGround)
            {
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Continuously update alpha fading even when ant is stationary
            if (_emitter.ActiveNodeCount > 0)
            {
                RenderTrail();
            }
            else if (_lineRenderer.positionCount > 0)
            {
                _lineRenderer.positionCount = 0;
            }
        }

        private void RenderTrail()
        {
            IReadOnlyList<PheromoneNode> nodes = _emitter.ActiveNodes;
            int count = nodes.Count;

            if (count < 2)
            {
                _lineRenderer.positionCount = 0;
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

            _lineRenderer.positionCount = count;
            _lineRenderer.SetPositions(_positionBuffer);

            PheromoneType primaryType = nodes[count - 1].Type;
            Color baseColor = primaryType switch
            {
                PheromoneType.Recruitment => _recruitmentColor,
                PheromoneType.Alarm => _alarmColor,
                PheromoneType.Repellent => _repellentColor,
                _ => _recruitmentColor
            };

            float currentTime = Time.time;
            float lifetime = _config.NodeLifetime;
            float oldestIntensity = nodes[0].GetCurrentIntensity(currentTime, lifetime);
            float newestIntensity = nodes[count - 1].GetCurrentIntensity(currentTime, lifetime);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(baseColor, 0.0f), new GradientColorKey(baseColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(oldestIntensity * baseColor.a, 0.0f), new GradientAlphaKey(newestIntensity * baseColor.a, 1.0f) }
            );

            _lineRenderer.colorGradient = gradient;
        }
    }
}
