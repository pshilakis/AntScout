using System.Collections.Generic;
using PGS.Core.Time;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AntScout.Pheromone
{
    /// <summary>
    /// Partial class containing debug visualization and heatmap gizmos for <see cref="PheromoneTrailEmitter"/>
    /// (Single Responsibility Principle).
    /// Displays real-time node freshness color ramps (pure Red-to-Green), elevated ground grid tiles,
    /// vertical sensor beacons, and percentage labels in both Game View (via OnGUI) and Scene View (via Handles).
    /// Toggleable via the "AntScout/Debug/Toggle Pheromone Heatmap Visuals" menu, persisted via EditorPrefs.
    /// </summary>
    public partial class PheromoneTrailEmitter
    {
        [Header("Debug Heatmap Visualization")]
        [Tooltip("Frequency of text labels (1 = every node, 2 = every 2nd node, etc.) to prevent visual clutter.")]
        [Range(1, 10)]
        [SerializeField] private int _labelFrequency = 2;

        public const string DebugHeatmapPrefKey = "AntScout_Debug_PheromoneHeatmapEnabled";
        public const string HeatmapMenuPath = "AntScout/Debug/Toggle Pheromone Heatmap Visuals";

        public static bool IsGlobalDebugVisualsEnabled
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetBool(DebugHeatmapPrefKey, true);
#else
                return false;
#endif
            }
        }

#if UNITY_EDITOR
        [MenuItem(HeatmapMenuPath, priority = 10)]
        public static void ToggleDebugVisualsFromMenu()
        {
            bool currentState = EditorPrefs.GetBool(DebugHeatmapPrefKey, true);
            bool newState = !currentState;
            EditorPrefs.SetBool(DebugHeatmapPrefKey, newState);
            Menu.SetChecked(HeatmapMenuPath, newState);
            SceneView.RepaintAll();
            Debug.Log($"[PheromoneDebug] Trail Heatmap Visuals {(newState ? "ENABLED" : "DISABLED")} (Saved to EditorPrefs).");
        }

        [MenuItem(HeatmapMenuPath, true)]
        private static bool ToggleDebugVisualsValidate()
        {
            Menu.SetChecked(HeatmapMenuPath, EditorPrefs.GetBool(DebugHeatmapPrefKey, true));
            return true;
        }
#endif

        private void OnGUI()
        {
            if (!Application.isPlaying || !IsGlobalDebugVisualsEnabled) return;

            // 1. Watermark status in top-left (rendered once by Colony 0 to prevent multi-colony duplication)
            if (_colonyTeamId == 0)
            {
                int totalNodes = ActiveNodeCount;
                GUIStyle badgeStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = totalNodes > 0 ? Color.green : Color.yellow }
                };

                string statusText;
                if (_segments.Count > 0 && _segments[0].NodeCount >= 2)
                {
                    PheromoneTrailSegment primarySeg = _segments[0];
                    int curEff = Mathf.RoundToInt(primarySeg.CurrentEfficiency * 100f);
                    int origEff = Mathf.RoundToInt(primarySeg.OriginalEfficiency * 100f);
                    statusText = $" ● Heatmap Active | Nodes: {totalNodes} | Efficiency: {origEff}% → {curEff}%";
                }
                else if (totalNodes > 0)
                {
                    statusText = $" ● Heatmap Active | Nodes: {totalNodes}";
                }
                else
                {
                    statusText = " ● Heatmap Active | Waiting for trail...";
                }

                float boxWidth = (_segments.Count > 0 && _segments[0].NodeCount >= 2) ? 310f : (totalNodes > 0 ? 190f : 250f);
                GUI.Box(new Rect(15, 120, boxWidth, 24), statusText, badgeStyle);
            }

            // 2. Passive Game View Floating Percentage Badges
            if (_segments == null || _segments.Count == 0) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindFirstObjectByType<Camera>();
                if (cam == null) return;
            }

            float currentTime = PgsTime.ElapsedTime;
            float lifetime = _nodeLifetime;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUIStyle darkBoxStyle = new GUIStyle(GUI.skin.box);

            for (int s = 0; s < _segments.Count; s++)
            {
                PheromoneTrailSegment segment = _segments[s];
                if (segment == null || segment.IsEmpty) continue;

                IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
                int count = nodes.Count;

                for (int i = 0; i < count; i++)
                {
                    if (i % _labelFrequency != 0 && i != count - 1) continue;

                    PheromoneNode node = nodes[i];
                    float intensity = node.GetCurrentIntensity(currentTime, lifetime);
                    if (intensity <= 0f) continue;

                    Vector3 worldPos = node.Position + Vector3.up * 0.45f;
                    Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);

                    // Screen frustum culling
                    if (screenPoint.z <= 0.2f || screenPoint.z > 80f) continue;

                    Color tileColor = GetHeatmapColor(intensity);
                    labelStyle.normal.textColor = tileColor;

                    int pct = Mathf.RoundToInt(intensity * 100f);
                    string text = $"{pct}%";

                    float labelWidth = 38f;
                    float labelHeight = 20f;
                    Rect screenRect = new Rect(
                        screenPoint.x - labelWidth * 0.5f,
                        Screen.height - screenPoint.y - labelHeight * 0.5f,
                        labelWidth,
                        labelHeight
                    );

                    // Dark high-contrast background box
                    GUI.color = new Color(0f, 0f, 0f, 0.75f);
                    GUI.Box(screenRect, GUIContent.none, darkBoxStyle);
                    GUI.color = Color.white;

                    GUI.Label(screenRect, text, labelStyle);
                }
            }
        }

        private void OnDrawGizmos()
        {
            DrawHeatmapGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            DrawHeatmapGizmos();
        }

        private void DrawHeatmapGizmos()
        {
            if (!IsGlobalDebugVisualsEnabled || _segments == null || _segments.Count == 0) return;

            float currentTime = PgsTime.ElapsedTime;
            float lifetime = _nodeLifetime;

            for (int s = 0; s < _segments.Count; s++)
            {
                PheromoneTrailSegment segment = _segments[s];
                if (segment == null || segment.IsEmpty) continue;

                IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
                IReadOnlyList<Vector3> origPositions = segment.OriginalPositions;
                int count = nodes.Count;

                // 1. Draw Direct Optimal Beeline (Nest to Food) in dashed/faint cyan
                if (count >= 2)
                {
                    Vector3 nestAnchor = nodes[0].Position + Vector3.up * 0.18f;
                    Vector3 foodAnchor = nodes[count - 1].Position + Vector3.up * 0.18f;
                    Gizmos.color = new Color(0.2f, 0.95f, 1.0f, 0.45f);
                    Gizmos.DrawLine(nestAnchor, foodAnchor);
                }

                // 2. Draw Original Ghost Blueprint Line (shows the initial player stroke)
                if (origPositions != null && origPositions.Count >= 2)
                {
                    Gizmos.color = new Color(0.85f, 0.85f, 0.9f, 0.35f);
                    for (int o = 0; o < origPositions.Count - 1; o++)
                    {
                        Vector3 p1 = origPositions[o] + Vector3.up * 0.08f;
                        Vector3 p2 = origPositions[o + 1] + Vector3.up * 0.08f;
                        Gizmos.DrawLine(p1, p2);
                    }

                    // Small ghost anchor wire cubes along original curve
                    for (int o = 0; o < origPositions.Count; o++)
                    {
                        Vector3 p = origPositions[o] + Vector3.up * 0.08f;
                        Gizmos.DrawWireCube(p, new Vector3(0.2f, 0.02f, 0.2f));
                    }
                }

                // 3. Draw Live Relaxed Nodes, Heatmap Tiles, and Displacement Vectors
                for (int i = 0; i < count; i++)
                {
                    PheromoneNode node = nodes[i];
                    float intensity = node.GetCurrentIntensity(currentTime, lifetime);
                    if (intensity <= 0f) continue;

                    Vector3 pos = node.Position;
                    Color tileColor = GetHeatmapColor(intensity);

                    // Displacement vector: connects initial drawn point to live relaxed position
                    if (origPositions != null && i < origPositions.Count)
                    {
                        float disp = Vector3.Distance(pos, origPositions[i]);
                        if (disp > 0.08f)
                        {
                            Vector3 origPoint = origPositions[i] + Vector3.up * 0.08f;
                            Gizmos.color = new Color(1.0f, 0.9f, 0.3f, 0.5f);
                            Gizmos.DrawLine(origPoint, pos + Vector3.up * 0.15f);
                        }
                    }

                    // Elevated Ground Heatmap Tile (Floats at y + 0.15m to cleanly clear ground and ribbon)
                    Vector3 tileCenter = pos + Vector3.up * 0.15f;
                    Vector3 tileSize = new Vector3(0.65f, 0.06f, 0.65f);

                    // Solid fill
                    Gizmos.color = new Color(tileColor.r, tileColor.g, tileColor.b, 0.75f);
                    Gizmos.DrawCube(tileCenter, tileSize);

                    // High-contrast wireframe outline
                    Gizmos.color = new Color(tileColor.r * 1.2f, tileColor.g * 1.2f, tileColor.b * 1.2f, 1.0f);
                    Gizmos.DrawWireCube(tileCenter, tileSize);

                    // Vertical sensor stem and top beacon
                    Vector3 beaconTop = tileCenter + Vector3.up * 0.45f;
                    Gizmos.color = new Color(tileColor.r, tileColor.g, tileColor.b, 0.85f);
                    Gizmos.DrawLine(tileCenter, beaconTop);
                    Gizmos.DrawSphere(beaconTop, 0.07f);

#if UNITY_EDITOR
                    // 2. Scene View Handles Label
                    if (i % _labelFrequency == 0 || i == count - 1)
                    {
                        int pct = Mathf.RoundToInt(intensity * 100f);
                        string text = $"{pct}%";

                        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = tileColor },
                            fontSize = 11,
                            alignment = TextAnchor.MiddleCenter
                        };

                        Handles.Label(pos + Vector3.up * 0.7f, text, style);
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Two-phase Red-to-Yellow-to-Green freshness gradient based strictly on normalized scent intensity.
        /// </summary>
        private static Color GetHeatmapColor(float intensity)
        {
            float t = Mathf.Clamp01(intensity);
            if (t >= 0.5f)
            {
                return Color.Lerp(Color.yellow, Color.green, (t - 0.5f) / 0.5f);
            }
            return Color.Lerp(Color.red, Color.yellow, t / 0.5f);
        }
    }
}
