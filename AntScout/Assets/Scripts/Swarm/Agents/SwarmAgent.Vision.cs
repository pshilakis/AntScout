using AntScout.FogOfWar.Core;
using AntScout.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class implementing IVisionRevealer for SwarmAgent castes (Workers, Soldiers, Guards).
    /// Swarm agents provide localized vision as they traverse trails and roam territories.
    /// </summary>
    public abstract partial class SwarmAgent : IVisionRevealer
    {
        public Vector3 RevealerPosition => transform.position;

        public float VisionRadius => FogOfWarManager.Instance != null && FogOfWarManager.Instance.Config != null
            ? FogOfWarManager.Instance.Config.WorkerVisionRadius
            : 5.0f;

        public bool IsRevealerActive => IsAlive && gameObject.activeInHierarchy;
    }
}
