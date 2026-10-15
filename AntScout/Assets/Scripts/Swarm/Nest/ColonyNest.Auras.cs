using System;
using PGS.Combat.Queries;
using PGS.Combat.Targeting;
using PGS.Effects;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class managing autonomous spatial auras continuously projected by the ColonyNest
    /// as configured on <see cref="StructureDefinition.Auras"/>.
    /// Executes zero-allocation spatial queries via <see cref="SpatialQueryService"/> on TimeChannel.World.
    /// Grounded in Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public partial class ColonyNest
    {
        private const int MAX_AURA_TARGETS = 32;
        private readonly IAttackable[] _auraQueryBuffer = new IAttackable[MAX_AURA_TARGETS];
        private float[] _auraTimers;

        private void EnsureAuraTimers(int count)
        {
            if (_auraTimers == null || _auraTimers.Length != count)
            {
                _auraTimers = new float[count];
            }
        }

        private void ProcessAuras(float deltaTime)
        {
            if (_structureDefinition == null || !_structureDefinition.HasAuras) return;

            var auras = _structureDefinition.Auras;
            int auraCount = auras.Count;
            EnsureAuraTimers(auraCount);

            Vector3 spawnPos = SpawnPosition;
            Vector3 forward = transform.forward;
            GameObject nestObj = gameObject;

            for (int a = 0; a < auraCount; a++)
            {
                var aura = auras[a];
                if (aura == null || aura.Shape == null) continue;

                _auraTimers[a] += deltaTime;
                if (_auraTimers[a] < aura.PulseInterval) continue;

                _auraTimers[a] = 0f;

                int hitCount = SpatialQueryService.QueryTargetsNonAlloc(
                    origin: spawnPos,
                    forward: forward,
                    shape: aura.Shape,
                    targetLayers: ~0,
                    ignoredTarget: this,
                    resultsBuffer: _auraQueryBuffer,
                    instigatorFaction: this,
                    affiliationMask: aura.TargetAffiliation
                );

                if (hitCount == 0) continue;

                var effects = aura.Effects;
                int effectCount = effects != null ? effects.Count : 0;
                if (effectCount == 0)
                {
                    Array.Clear(_auraQueryBuffer, 0, hitCount);
                    continue;
                }

                for (int i = 0; i < hitCount; i++)
                {
                    IAttackable target = _auraQueryBuffer[i];
                    if (target == null || target.Transform == null) continue;

                    GameObject targetObj = target.Transform.gameObject;
                    Collider targetCol = targetObj.GetComponent<Collider>();

                    var hitContext = new HitContext(
                        hitPoint: target.Transform.position,
                        hitNormal: Vector3.up,
                        hitCollider: targetCol,
                        target: targetObj,
                        instigator: nestObj
                    );

                    for (int e = 0; e < effectCount; e++)
                    {
                        effects[e]?.Apply(in hitContext);
                    }
                }

                Array.Clear(_auraQueryBuffer, 0, hitCount);
            }
        }
    }
}
