using System;
using System.Collections.Generic;
using PGS.Core;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Configuration entry for a single permitted team color.
    /// </summary>
    [Serializable]
    public struct TeamColorEntry
    {
        [Tooltip("Descriptive name or tag for this color (e.g. 'Blue', 'Red', 'Green', 'Yellow').")]
        public string Name;

        [Tooltip("The RGBA color multiplier applied to materials and emissive masks.")]
        public Color Color;
    }

    /// <summary>
    /// Centralized palette asset defining allowed team colors across the project.
    /// Operates like an inspector-configured equivalent to ColoredHeader.HeaderColor.
    /// Strictly adheres to Single Responsibility (SRP) and Anti-Hardcoding standards.
    /// </summary>
    [CreateAssetMenu(fileName = "AllowedTeamColors", menuName = "AntScout/Config/Allowed Team Colors")]
    public class AllowedTeamColors : ScriptableObject
    {
        [ColoredHeader("Allowed Team Colors", HeaderColor.Lavender)]
        [Tooltip("Designer-defined list of allowed team colors.")]
        [SerializeField] private TeamColorEntry[] _colors = new TeamColorEntry[]
        {
            new TeamColorEntry { Name = "Blue", Color = new Color(0.0f, 0.8f, 1.0f, 1.0f) }, // Team 0
            new TeamColorEntry { Name = "Red",  Color = new Color(1.0f, 0.15f, 0.0f, 1.0f) }  // Team 1
        };

        public IReadOnlyList<TeamColorEntry> Colors => _colors;

        /// <summary>
        /// Retrieves the color multiplier for a given ColonyTeamId.
        /// Throws descriptive exceptions rather than silent fallbacks (Fail-Fast).
        /// </summary>
        public Color GetColor(int teamId)
        {
            if (_colors == null || _colors.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[{name}] AllowedTeamColors has no colors configured! Please add entries in the Inspector.");
            }

            if (teamId < 0 || teamId >= _colors.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(teamId),
                    $"[{name}] ColonyTeamId '{teamId}' is not configured in AllowedTeamColors (configured count: {_colors.Length}).");
            }

            return _colors[teamId].Color;
        }

        public bool TryGetColorByName(string colorName, out Color color)
        {
            if (_colors != null)
            {
                for (int i = 0; i < _colors.Length; i++)
                {
                    if (string.Equals(_colors[i].Name, colorName, StringComparison.OrdinalIgnoreCase))
                    {
                        color = _colors[i].Color;
                        return true;
                    }
                }
            }

            color = default;
            return false;
        }
    }
}
