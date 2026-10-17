using AntScout.FogOfWar.Core;
using AntScout.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class implementing IFogCullable for SwarmAgent castes (Workers, Soldiers).
    /// Dynamic units hide their visual model and overhead canvas whenever they are outside active friendly sight.
    /// Simulation, pathfinding, and vitals continue undisturbed in the background.
    /// </summary>
    public abstract partial class SwarmAgent : IFogCullable
    {
        private Renderer[] _cachedRenderers;
        private Canvas[] _cachedCanvases;
        private bool _hasCachedVisuals;
        private bool _isCurrentlyVisibleInFog = true;

        Vector3 IFogCullable.CullPosition => transform.position;

        int IFogCullable.ColonyTeamId => _homeNest != null ? _homeNest.ColonyTeamId : 0;

        bool IFogCullable.IsCullableActive => IsAlive && gameObject.activeInHierarchy && _homeNest != null;

        public void OnFogVisibilityEvaluated(FogVisibilityState state)
        {
            // Dynamic combatants and workers are ONLY visually displayed when in active line-of-sight
            bool shouldBeVisible = (state == FogVisibilityState.Visible);

            if (_isCurrentlyVisibleInFog == shouldBeVisible && _hasCachedVisuals)
            {
                return;
            }

            _isCurrentlyVisibleInFog = shouldBeVisible;
            SetVisualsEnabled(shouldBeVisible);
        }

        private void EnsureVisualsCached()
        {
            if (_hasCachedVisuals) return;
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedCanvases = GetComponentsInChildren<Canvas>(true);
            _hasCachedVisuals = true;
        }

        private void SetVisualsEnabled(bool isVisible)
        {
            EnsureVisualsCached();

            if (_cachedRenderers != null)
            {
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] != null)
                    {
                        _cachedRenderers[i].enabled = isVisible;
                    }
                }
            }

            if (_cachedCanvases != null)
            {
                for (int i = 0; i < _cachedCanvases.Length; i++)
                {
                    if (_cachedCanvases[i] != null)
                    {
                        _cachedCanvases[i].enabled = isVisible;
                    }
                }
            }
        }
    }
}
