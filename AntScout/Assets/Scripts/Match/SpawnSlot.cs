using System;
using AntScout.Characters;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Match
{
    /// <summary>
    /// Configuration and runtime state for a single participant spawn slot in a match.
    /// Manages assigned team affiliation, character prefab, definition overrides,
    /// controller type (player vs AI), and PgsTime respawn handles.
    /// Follows the Single Responsibility Principle (SRP) by isolating slot configuration from scene arbitration.
    /// </summary>
    [Serializable]
    public partial class SpawnSlot
    {
        [Tooltip("Descriptive label for debugging and UI (e.g. 'Player 1', 'Enemy Commander').")]
        [SerializeField] private string _slotName = "Player";

        [Tooltip("Colony team affiliation (0 = Player Colony, 1 = Enemy Colony).")]
        [SerializeField] private int _teamId = 0;

        [Tooltip("Character prefab to instantiate (e.g. Scout.prefab or Vanguard.prefab).")]
        [SerializeField] private AntCharacter _characterPrefab;

        [Tooltip("Optional definition override. If unassigned, uses the CharacterDefinition configured on the prefab.")]
        [SerializeField] private CharacterDefinition _definitionOverride;

        [Tooltip("True for human controls (PlayerBrain + Camera binding); false for autonomous AI (AiBrain).")]
        [SerializeField] private bool _isPlayerControlled = true;

        [Tooltip("Delay in seconds before this character respawns at the allied nest after dying.")]
        [SerializeField] private float _respawnDelay = 5.0f;

        // Runtime State (non-serialized)
        [NonSerialized] private AntCharacter _activeCharacter;
        [NonSerialized] private TimeHandle _respawnHandle;
        [NonSerialized] private bool _isAwaitingRespawn;

        #region Public Properties

        public string SlotName => _slotName;
        public int TeamId => _teamId;
        public AntCharacter CharacterPrefab => _characterPrefab;
        public CharacterDefinition DefinitionOverride => _definitionOverride;
        public bool IsPlayerControlled => _isPlayerControlled;
        public float RespawnDelay => _respawnDelay;
        public AntCharacter ActiveCharacter => _activeCharacter;
        public bool IsAwaitingRespawn => _isAwaitingRespawn;
        public TimeHandle RespawnHandle => _respawnHandle;

        #endregion

        #region State Management

        public void BindActiveCharacter(AntCharacter character)
        {
            _activeCharacter = character ?? throw new ArgumentNullException(nameof(character));
            _isAwaitingRespawn = false;
            _respawnHandle = TimeHandle.Invalid;
        }

        public void StartRespawnTimer(TimeHandle handle)
        {
            _isAwaitingRespawn = true;
            _respawnHandle = handle;
        }

        public void OnRespawnCompleted()
        {
            _isAwaitingRespawn = false;
            _respawnHandle = TimeHandle.Invalid;
        }

        public void CancelRespawnTimer()
        {
            if (_respawnHandle.IsValid && PgsTime.IsActive(_respawnHandle))
            {
                PgsTime.Cancel(_respawnHandle);
            }
            _respawnHandle = TimeHandle.Invalid;
            _isAwaitingRespawn = false;
        }

        #endregion
    }
}
