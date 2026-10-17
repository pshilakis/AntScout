using PGS.FogOfWar.Core;
using PGS.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class implementing IFogCullable for ColonyNest structures.
    /// Hostile base structures remain hidden in Unexplored fog, and are revealed as persistent landmarks once Explored.
    /// </summary>
    public partial class ColonyNest : IFogCullable
    {
        private Renderer[] _cachedNestRenderers;
        private Canvas[] _cachedNestCanvases;
        private bool _hasCachedNestVisuals;
        private bool _isNestCurrentlyVisibleInFog = true;

        Vector3 IFogCullable.CullPosition => SpawnPosition;

        int IFogCullable.TeamId => _colonyTeamId;

        bool IFogCullable.IsCullableActive => IsStructureAlive && gameObject.activeInHierarchy;

        public void OnFogVisibilityEvaluated(FogVisibilityState state)
        {
            // Structures remain visible once discovered (Explored or Visible)
            bool shouldBeVisible = (state != FogVisibilityState.Unexplored);

            if (_isNestCurrentlyVisibleInFog == shouldBeVisible && _hasCachedNestVisuals)
            {
                return;
            }

            _isNestCurrentlyVisibleInFog = shouldBeVisible;
            SetNestVisualsEnabled(shouldBeVisible);
        }

        private void EnsureNestVisualsCached()
        {
            if (_hasCachedNestVisuals) return;
            _cachedNestRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedNestCanvases = GetComponentsInChildren<Canvas>(true);
            _hasCachedNestVisuals = true;
        }

        private void SetNestVisualsEnabled(bool isVisible)
        {
            EnsureNestVisualsCached();

            if (_cachedNestRenderers != null)
            {
                for (int i = 0; i < _cachedNestRenderers.Length; i++)
                {
                    if (_cachedNestRenderers[i] != null)
                    {
                        _cachedNestRenderers[i].enabled = isVisible;
                    }
                }
            }

            if (_cachedNestCanvases != null)
            {
                for (int i = 0; i < _cachedNestCanvases.Length; i++)
                {
                    if (_cachedNestCanvases[i] != null)
                    {
                        _cachedNestCanvases[i].enabled = isVisible;
                    }
                }
            }
        }
    }
}
