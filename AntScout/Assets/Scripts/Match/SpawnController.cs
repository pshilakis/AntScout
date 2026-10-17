using System;
using System.Collections.Generic;
using System.Threading;
using AntScout.Characters;
using AntScout.Characters.Brains;
using AntScout.Swarm.Nest;
using Cysharp.Threading.Tasks;
using PGS.Camera.VirtualCameras;
using PGS.Combat.Execution;
using PGS.Combat.Targeting;
using PGS.Core;
using PGS.Core.Time;
using PGS.Effects;
using UnityEngine;

namespace AntScout.Match
{
    /// <summary>
    /// Master match lifecycle coordinator for character spawning and respawning.
    /// Operates on the macro match timeline:
    /// 1. Spawns configured characters (Player and AI commanders) at their allied ColonyNest on match start.
    /// 2. Binds local player characters to the IsometricVirtualCamera and sets AntBrainController modes.
    /// 3. Observes character deaths and coordinates PgsTime respawn schedules with zero GC allocations.
    /// 4. Despawns fallen corpses after an inspector-tunable delay.
    /// Strictly adheres to Single Responsibility (SRP) and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    public partial class SpawnController : MonoBehaviour
    {
        [ColoredHeader("Participant Slots", HeaderColor.Cyan)]
        [Tooltip("Configured participants (Player and AI characters) to spawn and manage throughout the match.")]
        [SerializeField] private SpawnSlot[] _slots;

        [ColoredHeader("Camera Tracking", HeaderColor.Mint)]
        [Tooltip("Isometric virtual camera to bind to the player-controlled character upon spawn. Auto-discovered if unassigned.")]
        [SerializeField] private IsometricVirtualCamera _virtualCamera;

        [ColoredHeader("Corpse Cleanup", HeaderColor.Coral)]
        [Tooltip("Delay in seconds after death before despawning the fallen character's GameObject.")]
        [SerializeField] private float _corpseDespawnDelay = 2.0f;

        #region Events (Zero-Allocation UI & Gameplay Telemetry)

        public event Action<SpawnSlot, AntCharacter> OnCharacterSpawned;
        public event Action<SpawnSlot, float> OnCharacterDied; // (slot, respawnDelay)
        public event Action<SpawnSlot, AntCharacter> OnCharacterRespawned;

        #endregion

        #region Public Properties

        public IReadOnlyList<SpawnSlot> Slots => _slots;
        public IsometricVirtualCamera VirtualCamera => _virtualCamera;
        public float CorpseDespawnDelay => _corpseDespawnDelay;
        public bool IsLocomotionLocked => _isLocomotionLocked;

        #endregion

        private bool _isLocomotionLocked;
        private bool _hasSpawned;

        #region Lifecycle & Initialization

        private void Awake()
        {
            ValidateConfiguration();

            if (_virtualCamera == null)
            {
                _virtualCamera = FindFirstObjectByType<IsometricVirtualCamera>();
            }
        }

        private void Start()
        {
            if (!_hasSpawned)
            {
                SpawnAll();
                _hasSpawned = true;
            }
        }

        /// <summary>
        /// Explicit async initialization called by match setup state machine to guarantee
        /// all participants are spawned and camera-bound before the match timeline advances.
        /// Eliminates [DefaultExecutionOrder] race conditions.
        /// </summary>
        public UniTask InitializeAndSpawnAllAsync(CancellationToken ct = default)
        {
            if (!_hasSpawned)
            {
                SpawnAll();
                _hasSpawned = true;
            }
            return UniTask.CompletedTask;
        }

        private void OnDestroy()
        {
            CancelAllRespawnTimers();
        }

        #endregion

        #region Spawning & Respawning Logic

        /// <summary>
        /// Spawns all configured participant slots.
        /// </summary>
        public void SpawnAll()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                SpawnSlot slot = _slots[i];
                if (slot != null)
                {
                    SpawnCharacterForSlot(slot);
                }
            }
        }

        /// <summary>
        /// Instantiates and initializes a persistent character instance for the specified slot at its allied ColonyNest.
        /// </summary>
        public AntCharacter SpawnCharacterForSlot(SpawnSlot slot)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));

            slot.CancelRespawnTimer();

            if (!ColonyNest.TryGetForTeam(slot.TeamId, out ColonyNest nest) || nest == null || !nest.IsStructureAlive)
            {
                throw new InvalidOperationException(
                    $"[{name}] Unable to spawn character for slot '{slot.SlotName}': No alive ColonyNest found for Team {slot.TeamId}!");
            }

            Vector3 spawnPos = nest.SpawnPosition;
            Quaternion spawnRot = nest.transform.rotation;

            AntCharacter characterInstance = Instantiate(slot.CharacterPrefab, spawnPos, spawnRot);
            characterInstance.name = $"{slot.SlotName}_{characterInstance.GetInstanceID()}";

            // Explicit top-down Dependency Injection (DIP): Injects allied nest, trail emitter, and definition override
            characterInstance.Initialize(nest, nest.TrailEmitter, slot.DefinitionOverride);

            AntBrainController brain = characterInstance.GetComponent<AntBrainController>();
            if (brain != null)
            {
                brain.SetBrainMode(slot.IsPlayerControlled ? BrainMode.Player : BrainMode.AI);
            }

            if (_isLocomotionLocked)
            {
                characterInstance.IsLocomotionLocked = true;
                if (brain != null)
                {
                    brain.IsEnabled = false;
                }
            }

            if (slot.IsPlayerControlled && _virtualCamera != null)
            {
                _virtualCamera.Target = characterInstance;
            }

            HookDeathListener(slot, characterInstance);

            slot.BindActiveCharacter(characterInstance);
            OnCharacterSpawned?.Invoke(slot, characterInstance);

            return characterInstance;
        }

        private void HookDeathListener(SpawnSlot slot, AntCharacter character)
        {
            void HandleDied(HitContext context)
            {
                character.OnDied -= HandleDied;
                HandleCharacterDied(slot, character);
            }

            character.OnDied += HandleDied;
        }

        private void HandleCharacterDied(SpawnSlot slot, AntCharacter deadCharacter)
        {
            OnCharacterDied?.Invoke(slot, slot.RespawnDelay);

            // Hide the fallen character's GameObject after corpse delay without destroying it
            if (_corpseDespawnDelay > 0f)
            {
                PgsTime.Schedule(_corpseDespawnDelay, () =>
                {
                    if (deadCharacter != null)
                    {
                        deadCharacter.gameObject.SetActive(false);
                    }
                }, TimeChannel.World);
            }
            else
            {
                if (deadCharacter != null)
                {
                    deadCharacter.gameObject.SetActive(false);
                }
            }

            // Schedule respawn countdown via PgsTime
            TimeHandle respawnHandle = PgsTime.Schedule(slot.RespawnDelay, () =>
            {
                RespawnCharacterForSlot(slot);
            }, TimeChannel.World);

            slot.StartRespawnTimer(respawnHandle);
        }

        private void RespawnCharacterForSlot(SpawnSlot slot)
        {
            // Verify if allied nest is still alive before respawning
            if (!ColonyNest.TryGetForTeam(slot.TeamId, out ColonyNest nest) || nest == null || !nest.IsStructureAlive)
            {
                Debug.LogWarning($"[{name}] Slot '{slot.SlotName}' could not respawn: ColonyNest for Team {slot.TeamId} was destroyed!");
                return;
            }

            AntCharacter character = slot.ActiveCharacter;
            if (character != null)
            {
                character.Revive(nest.SpawnPosition, nest.transform.rotation);
                character.Initialize(nest, nest.TrailEmitter);
                HookDeathListener(slot, character);
                slot.OnRespawnCompleted();

                if (slot.IsPlayerControlled && _virtualCamera != null)
                {
                    _virtualCamera.Target = character;
                }

                OnCharacterRespawned?.Invoke(slot, character);
            }
            else
            {
                // Fallback in case instance was never spawned
                SpawnCharacterForSlot(slot);
            }
        }

        /// <summary>
        /// Cancels all pending participant respawn timers (e.g. on match conclusion or scene exit).
        /// </summary>
        public void CancelAllRespawnTimers()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i]?.CancelRespawnTimer();
            }
        }

        /// <summary>
        /// Locks or unlocks locomotion on all currently active slot characters (e.g. on match conclusion).
        /// </summary>
        public void SetAllCharactersLocomotionLocked(bool locked)
        {
            _isLocomotionLocked = locked;
            if (_slots == null) return;

            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                AntCharacter character = _slots[i]?.ActiveCharacter;
                if (character != null && character.isActiveAndEnabled)
                {
                    character.IsLocomotionLocked = locked;
                    AntBrainController brain = character.GetComponent<AntBrainController>();
                    if (brain != null)
                    {
                        brain.IsEnabled = !locked;
                    }
                    count++;
                }
            }

            Debug.Log($"[SpawnController] Locomotion locked={locked} for {count} character(s).");
        }

        #endregion
    }
}
