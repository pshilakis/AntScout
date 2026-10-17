using System;
using PGS.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class implementing IVisionRevealer for Hero Ants.
    /// Hero ants provide broad scouting line-of-sight in the Fog of War driven by their CharacterDefinition.
    /// </summary>
    public sealed partial class AntCharacter : IVisionRevealer
    {
        public Vector3 RevealerPosition => transform.position;

        public float VisionRadius => _definition != null
            ? _definition.VisionRadius
            : throw new InvalidOperationException($"[{name}] AntCharacter requires a valid CharacterDefinition to determine VisionRadius.");

        int IVisionRevealer.TeamId => _colonyTeamId;

        public bool IsRevealerActive => _isAlive && gameObject.activeInHierarchy;
    }
}
