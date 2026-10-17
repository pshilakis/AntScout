using AntScout.FogOfWar.Core;
using AntScout.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class implementing IVisionRevealer for Hero Ants.
    /// Hero ants provide broad scouting line-of-sight in the Fog of War.
    /// </summary>
    public sealed partial class AntCharacter : IVisionRevealer
    {
        public Vector3 RevealerPosition => transform.position;

        public float VisionRadius => FogOfWarManager.Instance != null && FogOfWarManager.Instance.Config != null
            ? FogOfWarManager.Instance.Config.HeroVisionRadius
            : 12.0f;

        public bool IsRevealerActive => _isAlive && gameObject.activeInHierarchy;
    }
}
