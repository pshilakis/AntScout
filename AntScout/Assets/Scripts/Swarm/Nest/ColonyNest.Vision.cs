using System;
using PGS.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class implementing IVisionRevealer for ColonyNest structures.
    /// The home nest projects broad vision over the base yard driven by its StructureDefinition.
    /// </summary>
    public partial class ColonyNest : IVisionRevealer
    {
        public Vector3 RevealerPosition => SpawnPosition;

        public float VisionRadius => _structureDefinition != null
            ? _structureDefinition.VisionRadius
            : throw new InvalidOperationException($"[{name}] ColonyNest requires a valid StructureDefinition to determine VisionRadius.");

        int IVisionRevealer.TeamId => _colonyTeamId;

        public bool IsRevealerActive => IsStructureAlive && gameObject.activeInHierarchy;
    }
}
