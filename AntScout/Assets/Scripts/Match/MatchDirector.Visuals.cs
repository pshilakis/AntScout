using System;
using AntScout.Characters;
using AntScout.Config;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using UnityEngine;

namespace AntScout.Match
{
    /// <summary>
    /// Partial class handling team visual presentation and material styling for MatchDirector.
    /// Strictly adheres to Single Responsibility (SRP):
    /// - Listens to SpawnController and ColonyNest lifecycle events
    /// - Retrieves authored Material directly from unit's CharacterDefinition
    /// - Applies designer-configured AllowedTeamColors multiplier (via MaterialPropertyBlock)
    /// - Keeps gameplay units (AntCharacter, SwarmAgent) completely decoupled from visual styling
    /// </summary>
    public sealed partial class MatchDirector
    {
        private static readonly int s_baseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_colorPropId = Shader.PropertyToID("_Color");
        private static readonly int s_emissionColorPropId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock s_propertyBlock;

        private void InitializeVisuals()
        {
            if (_spawnController != null)
            {
                _spawnController.OnCharacterSpawned += HandleCharacterSpawned;
                _spawnController.OnCharacterRespawned += HandleCharacterSpawned;
            }

            RefreshAllNestVisualBindings();
        }

        private void CleanupVisuals()
        {
            if (_spawnController != null)
            {
                _spawnController.OnCharacterSpawned -= HandleCharacterSpawned;
                _spawnController.OnCharacterRespawned -= HandleCharacterSpawned;
            }

            foreach (ColonyNest nest in ColonyNest.ActiveNests)
            {
                UnhookNestVisuals(nest);
            }
        }

        public void RefreshAllNestVisualBindings()
        {
            foreach (ColonyNest nest in ColonyNest.ActiveNests)
            {
                HookNestVisuals(nest);
            }
        }

        public void HookNestVisuals(ColonyNest nest)
        {
            if (nest == null) return;

            nest.OnWorkerSpawned -= HandleWorkerSpawned;
            nest.OnWorkerSpawned += HandleWorkerSpawned;

            nest.OnSoldierSpawned -= HandleSoldierSpawned;
            nest.OnSoldierSpawned += HandleSoldierSpawned;

            // Restyle existing active workers/soldiers (handles pre-spawned peacetime squads)
            var activeWorkers = nest.ActiveWorkers;
            if (activeWorkers != null)
            {
                for (int i = 0; i < activeWorkers.Count; i++)
                {
                    HandleWorkerSpawned(activeWorkers[i]);
                }
            }

            var activeSoldiers = nest.ActiveSoldiers;
            if (activeSoldiers != null)
            {
                for (int i = 0; i < activeSoldiers.Count; i++)
                {
                    HandleSoldierSpawned(activeSoldiers[i]);
                }
            }
        }

        private void UnhookNestVisuals(ColonyNest nest)
        {
            if (nest == null) return;
            nest.OnWorkerSpawned -= HandleWorkerSpawned;
            nest.OnSoldierSpawned -= HandleSoldierSpawned;
        }

        private void HandleCharacterSpawned(SpawnSlot slot, AntCharacter character)
        {
            if (character == null) return;
            ApplyUnitVisuals(character.BodyRenderer, character.Definition, slot.TeamId);
        }

        private void HandleWorkerSpawned(ISwarmAgent agent)
        {
            if (agent == null) return;
            int teamId = agent.HomeNest != null ? agent.HomeNest.ColonyTeamId : agent.ColonyTeamId;
            ApplyUnitVisuals(agent.BodyRenderer, agent.CharacterDefinition, teamId);
        }

        private void HandleSoldierSpawned(ISwarmAgent agent)
        {
            if (agent == null) return;
            int teamId = agent.HomeNest != null ? agent.HomeNest.ColonyTeamId : agent.ColonyTeamId;
            ApplyUnitVisuals(agent.BodyRenderer, agent.CharacterDefinition, teamId);
        }

        /// <summary>
        /// Centralized application of unit visuals.
        /// Assigns the material authored in CharacterDefinition strictly to the unit's designated BodyRenderer,
        /// then applies the allowed team color multiplier via MaterialPropertyBlock.
        /// Completely avoids GetComponentsInChildren searches, preserving carried cargo, weapons, and healthbars.
        /// Zero GC allocations at runtime (uses shared MaterialPropertyBlock).
        /// </summary>
        public void ApplyUnitVisuals(Renderer bodyRenderer, CharacterDefinition definition, int teamId)
        {
            if (bodyRenderer == null) return;

            Material authoredMaterial = definition != null ? definition.Material : null;
            if (authoredMaterial != null && bodyRenderer.sharedMaterial != authoredMaterial)
            {
                bodyRenderer.sharedMaterial = authoredMaterial;
            }

            Color teamColor = GetTeamColor(teamId);
            s_propertyBlock ??= new MaterialPropertyBlock();

            bodyRenderer.GetPropertyBlock(s_propertyBlock);
            s_propertyBlock.SetColor(s_baseColorPropId, teamColor);
            s_propertyBlock.SetColor(s_colorPropId, teamColor);
            s_propertyBlock.SetColor(s_emissionColorPropId, teamColor);
            bodyRenderer.SetPropertyBlock(s_propertyBlock);
        }
    }
}
