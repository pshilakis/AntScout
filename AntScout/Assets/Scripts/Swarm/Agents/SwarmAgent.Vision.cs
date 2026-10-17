using System;
using PGS.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class implementing IVisionRevealer for SwarmAgent castes (Workers, Soldiers, Guards).
    /// Swarm agents provide localized vision as they traverse trails and roam territories, driven by their CharacterDefinition.
    /// </summary>
    public abstract partial class SwarmAgent : IVisionRevealer
    {
        public Vector3 RevealerPosition => transform.position;

        public float VisionRadius => _characterDefinition != null
            ? _characterDefinition.VisionRadius
            : throw new InvalidOperationException($"[{name}] SwarmAgent requires a valid CharacterDefinition to determine VisionRadius.");

        int IVisionRevealer.TeamId => ColonyTeamId;

        public bool IsRevealerActive => IsAlive && gameObject.activeInHierarchy;
    }
}
