using PGS.FogOfWar.Core;
using PGS.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class implementing IFogCullable for Hero Ants.
    /// Hostile Hero ants hide visual renderers and overhead UI when outside active friendly sight.
    /// </summary>
    public sealed partial class AntCharacter : IFogCullable
    {
        private Renderer[] _cachedHeroRenderers;
        private Canvas[] _cachedHeroCanvases;
        private bool _hasCachedHeroVisuals;
        private bool _isHeroCurrentlyVisibleInFog = true;

        Vector3 IFogCullable.CullPosition => transform.position;

        int IFogCullable.TeamId => _colonyTeamId;

        bool IFogCullable.IsCullableActive => _isAlive && gameObject.activeInHierarchy;

        public void OnFogVisibilityEvaluated(FogVisibilityState state)
        {
            // Dynamic heroes are ONLY visually displayed when in active line-of-sight
            bool shouldBeVisible = (state == FogVisibilityState.Visible);

            if (_isHeroCurrentlyVisibleInFog == shouldBeVisible && _hasCachedHeroVisuals)
            {
                return;
            }

            _isHeroCurrentlyVisibleInFog = shouldBeVisible;
            SetHeroVisualsEnabled(shouldBeVisible);
        }

        private void EnsureHeroVisualsCached()
        {
            if (_hasCachedHeroVisuals) return;
            _cachedHeroRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedHeroCanvases = GetComponentsInChildren<Canvas>(true);
            _hasCachedHeroVisuals = true;
        }

        private void SetHeroVisualsEnabled(bool isVisible)
        {
            EnsureHeroVisualsCached();

            if (_cachedHeroRenderers != null)
            {
                for (int i = 0; i < _cachedHeroRenderers.Length; i++)
                {
                    if (_cachedHeroRenderers[i] != null)
                    {
                        _cachedHeroRenderers[i].enabled = isVisible;
                    }
                }
            }

            if (_cachedHeroCanvases != null)
            {
                for (int i = 0; i < _cachedHeroCanvases.Length; i++)
                {
                    if (_cachedHeroCanvases[i] != null)
                    {
                        _cachedHeroCanvases[i].enabled = isVisible;
                    }
                }
            }
        }
    }
}
