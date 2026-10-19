using System;
using AntScout.Match;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using PGS.Core;
using PGS.Core.Time;
using PGS.UI.Gauges;
using PGS.UI.Stack;
using TMPro;
using UnityEngine;

namespace AntScout.UI
{
    /// <summary>
    /// Master Heads-Up Display (HUD) presentation controller for active RTS skirmishes.
    /// Manages the real-time simulation clock, pre-match countdown overlay, and Colony Nest health meters.
    /// Inherits from <see cref="UIView"/> to integrate with the PGS UI Stack and Occlusion services.
    /// Strictly adheres to Single Responsibility (SRP) and Fail-Fast standards.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchHudView : UIView
    {
        #region Serialized Fields

        [Header("Match Clock")]
        [Tooltip("Text label displaying elapsed simulation time in MM:SS.")]
        [SerializeField] private TextMeshProUGUI _clockText;

        [Header("Pre-Match Countdown Overlay")]
        [Tooltip("CanvasGroup containing the centered countdown banner.")]
        [SerializeField] private CanvasGroup _countdownCanvasGroup;

        [Tooltip("Text label displaying '3', '2', '1', or 'START!'.")]
        [SerializeField] private TextMeshProUGUI _countdownText;

        [Header("Colony Nest Health Bars")]
        [Tooltip("Shader-driven ResourceBar for the local player's Colony Nest (Colony 1).")]
        [SerializeField] private ResourceBar _playerNestBar;

        [Tooltip("Text label showing player nest health numbers (e.g. '1,000 / 1,000').")]
        [SerializeField] private TextMeshProUGUI _playerNestHealthText;

        [Tooltip("Shader-driven ResourceBar for the enemy AI's Colony Nest (Colony 2).")]
        [SerializeField] private ResourceBar _enemyNestBar;

        [Tooltip("Text label showing enemy nest health numbers (e.g. '1,000 / 1,000').")]
        [SerializeField] private TextMeshProUGUI _enemyNestHealthText;

        [Header("Player Colony Economy Telemetry")]
        [Tooltip("Shader-driven ResourceBar for the player's colony biomass reserves.")]
        [SerializeField] private ResourceBar _playerBiomassBar;

        [Tooltip("Text label displaying player colony biomass numbers (e.g. '100 / 500').")]
        [SerializeField] private TextMeshProUGUI _playerBiomassText;

        [Tooltip("Text label displaying active worker population (or combined colony population if _playerSoldierText is unassigned).")]
        [SerializeField] private TextMeshProUGUI _playerWorkerText;

        [Tooltip("Optional dedicated text label for soldier population. If null, soldier count is formatted into _playerWorkerText.")]
        [SerializeField] private TextMeshProUGUI _playerSoldierText;

        [Header("Colony Naming & Titles")]
        [Tooltip("Text label for the player's colony/name header.")]
        [SerializeField] private TextMeshProUGUI _playerTitleText;

        [Tooltip("Text label for the enemy's colony/name header.")]
        [SerializeField] private TextMeshProUGUI _enemyTitleText;

        [Tooltip("Display name used for the local player.")]
        [SerializeField] private string _playerDisplayName = "Player 1";

        [Tooltip("Display name used for the enemy AI.")]
        [SerializeField] private string _enemyDisplayName = "Player 2 (AI)";

        [Header("Nest Scene References (Optional Override)")]
        [Tooltip("Direct reference to Player Nest. Auto-resolved if unassigned.")]
        [SerializeField] private ColonyNest _playerNest;

        [Tooltip("Direct reference to Enemy Nest. Auto-resolved if unassigned.")]
        [SerializeField] private ColonyNest _enemyNest;

        #endregion

        private bool _isBound;
        private int _lastCountdownSecond = -1;

        #region Lifecycle & Initialization

        protected override void Awake()
        {
            base.Awake();
            ValidateComponentBindings();
            EnforceResponsiveHeaderLayout();
        }

        private void Start()
        {
            BindToNestsAndMatchDirector();
            ApplyColonyTitlesAndColors();
        }

        protected virtual void OnDestroy()
        {
            UnbindEvents();
        }

        private void Update()
        {
            UpdateClockDisplay();
            UpdateCountdownDisplay();
            UpdateWorkerDisplay();
        }

        #endregion

        #region Responsive Header Layout & Presentation

        private void EnforceResponsiveHeaderLayout()
        {
            // 1. Resolve & format Player Card and Row
            if (_playerWorkerText != null && _playerWorkerText.transform.parent != null)
            {
                Transform headerRow = _playerWorkerText.transform.parent;
                Transform playerCard = headerRow.parent;

                if (playerCard is RectTransform cardRt)
                {
                    cardRt.sizeDelta = new Vector2(550f, 110f);
                    cardRt.anchoredPosition = new Vector2(-365f, 0f);

                    // Ensure Top Header Bar background is wide enough
                    if (cardRt.parent is RectTransform headerBarRt)
                    {
                        headerBarRt.sizeDelta = new Vector2(1320f, 135f);
                    }
                }

                if (headerRow is RectTransform rowRt)
                {
                    rowRt.sizeDelta = new Vector2(0f, 28f);
                    rowRt.anchoredPosition = new Vector2(0f, -4f);
                }

                Transform titleTrans = headerRow.Find("Title Label") ?? playerCard?.Find("Title Label");
                if (titleTrans is RectTransform titleRt)
                {
                    titleRt.anchorMin = new Vector2(0f, 0f);
                    titleRt.anchorMax = new Vector2(0.32f, 1f);
                    titleRt.offsetMin = Vector2.zero;
                    titleRt.offsetMax = Vector2.zero;

                    if (_playerTitleText == null)
                    {
                        _playerTitleText = titleRt.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (_playerWorkerText.transform is RectTransform workerRt)
                {
                    workerRt.anchorMin = new Vector2(0.32f, 0f);
                    workerRt.anchorMax = new Vector2(1f, 1f);
                    workerRt.offsetMin = Vector2.zero;
                    workerRt.offsetMax = Vector2.zero;

                    _playerWorkerText.textWrappingMode = TextWrappingModes.NoWrap;
                    _playerWorkerText.overflowMode = TextOverflowModes.Overflow;
                    _playerWorkerText.alignment = TextAlignmentOptions.Right;
                    _playerWorkerText.fontSize = 16f;
                }
            }

            if (_playerSoldierText != null)
            {
                _playerSoldierText.textWrappingMode = TextWrappingModes.NoWrap;
                _playerSoldierText.overflowMode = TextOverflowModes.Overflow;
                _playerSoldierText.fontSize = 16f;
            }

            // 2. Format Enemy Card
            if (_enemyNestBar != null && _enemyNestBar.transform.parent != null)
            {
                Transform enemyCard = _enemyNestBar.transform.parent;
                if (enemyCard is RectTransform enemyCardRt)
                {
                    enemyCardRt.sizeDelta = new Vector2(550f, 110f);
                    enemyCardRt.anchoredPosition = new Vector2(365f, 0f);
                }

                Transform enemyTitleTrans = enemyCard.Find("Title Label") ?? enemyCard.Find("Header Row/Title Label");
                if (enemyTitleTrans is RectTransform enemyTitleRt)
                {
                    enemyTitleRt.sizeDelta = new Vector2(0f, 28f);
                    enemyTitleRt.anchoredPosition = new Vector2(0f, -4f);

                    if (_enemyTitleText == null)
                    {
                        _enemyTitleText = enemyTitleRt.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            // 3. Format Health and Biomass Meters
            if (_playerNestBar != null && _playerNestBar.transform is RectTransform pBarRt)
            {
                pBarRt.sizeDelta = new Vector2(0f, 26f);
                pBarRt.anchoredPosition = new Vector2(0f, -34f);
            }
            if (_playerNestHealthText != null)
            {
                _playerNestHealthText.fontSize = 13.5f;
            }

            if (_playerBiomassBar != null && _playerBiomassBar.transform is RectTransform pBioRt)
            {
                pBioRt.sizeDelta = new Vector2(0f, 22f);
                pBioRt.anchoredPosition = new Vector2(0f, -66f);
            }
            if (_playerBiomassText != null)
            {
                _playerBiomassText.fontSize = 12.5f;
            }

            if (_enemyNestBar != null && _enemyNestBar.transform is RectTransform eBarRt)
            {
                eBarRt.sizeDelta = new Vector2(0f, 26f);
                eBarRt.anchoredPosition = new Vector2(0f, -34f);
            }
            if (_enemyNestHealthText != null)
            {
                _enemyNestHealthText.fontSize = 13.5f;
            }
        }

        private void ApplyColonyTitlesAndColors()
        {
            if (_playerTitleText != null)
            {
                _playerTitleText.text = _playerDisplayName;
                _playerTitleText.fontSize = 16f;
                _playerTitleText.fontStyle = FontStyles.Bold;
                _playerTitleText.alignment = TextAlignmentOptions.Left;
                _playerTitleText.textWrappingMode = TextWrappingModes.NoWrap;

                if (MatchDirector.Instance != null)
                {
                    _playerTitleText.color = MatchDirector.Instance.GetTeamColor(MatchDirector.Instance.PlayerTeamId);
                }
                else
                {
                    _playerTitleText.color = new Color(0.2f, 0.85f, 0.3f, 1f);
                }
            }

            if (_enemyTitleText != null)
            {
                _enemyTitleText.text = _enemyDisplayName;
                _enemyTitleText.fontSize = 16f;
                _enemyTitleText.fontStyle = FontStyles.Bold;
                _enemyTitleText.alignment = TextAlignmentOptions.Left;
                _enemyTitleText.textWrappingMode = TextWrappingModes.NoWrap;

                if (MatchDirector.Instance != null)
                {
                    int enemyTeamId = (MatchDirector.Instance.PlayerTeamId == 0) ? 1 : 0;
                    _enemyTitleText.color = MatchDirector.Instance.GetTeamColor(enemyTeamId);
                }
                else
                {
                    _enemyTitleText.color = new Color(0.9f, 0.25f, 0.25f, 1f);
                }
            }
        }

        #endregion

        #region Binding & Event Handling

        private void BindToNestsAndMatchDirector()
        {
            if (_isBound) return;

            ResolveNestReferences();

            if (_playerNest != null)
            {
                _playerNest.OnHealthChanged += HandlePlayerNestHealthChanged;
                _playerNest.OnBiomassChanged += HandlePlayerBiomassChanged;
                _playerNest.OnWorkerSpawned += HandleWorkerSpawned;
                _playerNest.OnSoldierSpawned += HandleSoldierSpawned;

                _playerNestBar.SetValues(_playerNest.CurrentHealth, _playerNest.MaxHealth);
                UpdateNestHealthText(_playerNestHealthText, _playerNest.CurrentHealth, _playerNest.MaxHealth);
                UpdateBiomassDisplay(_playerNest.Biomass);
                UpdateWorkerDisplay();
            }

            if (_enemyNest != null)
            {
                _enemyNest.OnHealthChanged += HandleEnemyNestHealthChanged;
                _enemyNestBar.SetValues(_enemyNest.CurrentHealth, _enemyNest.MaxHealth);
                UpdateNestHealthText(_enemyNestHealthText, _enemyNest.CurrentHealth, _enemyNest.MaxHealth);
            }

            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.OnMatchStateChanged += HandleMatchStateChanged;
            }

            ApplyColonyTitlesAndColors();
            _isBound = true;
        }

        private void UnbindEvents()
        {
            if (!_isBound) return;

            if (_playerNest != null)
            {
                _playerNest.OnHealthChanged -= HandlePlayerNestHealthChanged;
                _playerNest.OnBiomassChanged -= HandlePlayerBiomassChanged;
                _playerNest.OnWorkerSpawned -= HandleWorkerSpawned;
                _playerNest.OnSoldierSpawned -= HandleSoldierSpawned;
            }

            if (_enemyNest != null)
            {
                _enemyNest.OnHealthChanged -= HandleEnemyNestHealthChanged;
            }

            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
            }

            _isBound = false;
        }

        /// <summary>
        /// Explicit top-down Dependency Injection (DIP) entrypoint to bind allied and enemy nests.
        /// Invoked deterministically by MatchSetupState. Eliminates ambient scene searching.
        /// </summary>
        public void BindNests(ColonyNest playerNest, ColonyNest enemyNest)
        {
            UnbindEvents();
            _playerNest = playerNest ?? throw new ArgumentNullException(nameof(playerNest));
            _enemyNest = enemyNest ?? throw new ArgumentNullException(nameof(enemyNest));
            BindToNestsAndMatchDirector();
        }

        private void ResolveNestReferences()
        {
            if (_playerNest != null && _enemyNest != null) return;

            int playerTeamId = MatchDirector.Instance != null ? MatchDirector.Instance.PlayerTeamId : 0;

            if (_playerNest == null && ColonyNest.TryGetForTeam(playerTeamId, out ColonyNest pNest))
            {
                _playerNest = pNest;
            }

            if (_enemyNest == null)
            {
                foreach (var nest in ColonyNest.ActiveNests)
                {
                    if (nest != null && nest.ColonyTeamId != playerTeamId)
                    {
                        _enemyNest = nest;
                        break;
                    }
                }
            }

            if (_playerNest == null)
            {
                throw new InvalidOperationException($"[{name}] MatchHudView: Player ColonyNest (Team {playerTeamId}) is not registered!");
            }

            if (_enemyNest == null)
            {
                throw new InvalidOperationException($"[{name}] MatchHudView: Enemy ColonyNest is not registered!");
            }
        }

        private void HandlePlayerNestHealthChanged(float current, float max)
        {
            _playerNestBar.SetValues(current, max);
            UpdateNestHealthText(_playerNestHealthText, current, max);
        }

        private void HandlePlayerBiomassChanged(BoundedInt biomass)
        {
            UpdateBiomassDisplay(biomass);
        }

        private void HandleWorkerSpawned(ISwarmAgent agent)
        {
            UpdateWorkerDisplay();
        }

        private void HandleSoldierSpawned(ISwarmAgent agent)
        {
            UpdateWorkerDisplay();
        }

        private void HandleEnemyNestHealthChanged(float current, float max)
        {
            _enemyNestBar.SetValues(current, max);
            UpdateNestHealthText(_enemyNestHealthText, current, max);
        }

        private void HandleMatchStateChanged(MatchState newState)
        {
            if (newState == MatchState.InProgress)
            {
                SetCountdownVisible(false);
            }
            ApplyColonyTitlesAndColors();
        }

        #endregion

        #region Display Updates

        private void UpdateClockDisplay()
        {
            if (_clockText == null) return;

            float elapsed = MatchDirector.Instance != null
                ? MatchDirector.Instance.ElapsedSimulationTime
                : 0f;

            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            _clockText.text = $"{minutes:00}:{seconds:00}";
        }

        private void UpdateCountdownDisplay()
        {
            if (_countdownCanvasGroup == null || _countdownText == null) return;

            var director = MatchDirector.Instance;
            if (director == null) return;

            if (director.State == MatchState.PreMatch)
            {
                SetCountdownVisible(true);
                float remaining = director.PreMatchRemaining;
                int second = Mathf.CeilToInt(remaining);

                if (second != _lastCountdownSecond)
                {
                    _lastCountdownSecond = second;
                    _countdownText.text = second > 0 ? second.ToString() : "START!";
                }
            }
            else
            {
                SetCountdownVisible(false);
            }
        }

        private void SetCountdownVisible(bool visible)
        {
            if (_countdownCanvasGroup == null) return;
            _countdownCanvasGroup.alpha = visible ? 1f : 0f;
            _countdownCanvasGroup.blocksRaycasts = false;
        }

        private void UpdateNestHealthText(TextMeshProUGUI label, float current, float max)
        {
            if (label == null) return;
            label.text = $"{Mathf.CeilToInt(current):N0} / {Mathf.CeilToInt(max):N0}";
        }

        private void UpdateBiomassDisplay(BoundedInt biomass)
        {
            if (_playerBiomassBar != null)
            {
                _playerBiomassBar.SetValues(biomass.Current, biomass.Max);
            }

            if (_playerBiomassText != null)
            {
                _playerBiomassText.text = $"{biomass.Current:N0} / {biomass.Max:N0}";
            }
        }

        private int _lastWorkerCount = -1;
        private int _lastSoldierCount = -1;

        private void UpdateWorkerDisplay()
        {
            if (_playerWorkerText == null || _playerNest == null) return;

            int workerCount = _playerNest.ActiveWorkerCount;
            int soldierCount = _playerNest.ActiveSoldierCount;

            if (workerCount != _lastWorkerCount || soldierCount != _lastSoldierCount)
            {
                _lastWorkerCount = workerCount;
                _lastSoldierCount = soldierCount;

                if (_playerSoldierText != null)
                {
                    _playerWorkerText.text = $"<color=#8EE4AF>Workers: {workerCount} / {_playerNest.MaxWorkers}</color>";
                    _playerSoldierText.text = $"<color=#FFAA88>Soldiers: {soldierCount} / {_playerNest.MaxSoldiers}</color>";
                }
                else
                {
                    _playerWorkerText.text = $"<color=#8EE4AF>Workers: {workerCount}/{_playerNest.MaxWorkers}</color>   <color=#FFAA88>Soldiers: {soldierCount}/{_playerNest.MaxSoldiers}</color>";
                }
            }
        }

        #endregion

        #region Validation

        private void ValidateComponentBindings()
        {
            if (_clockText == null)
            {
                throw new InvalidOperationException($"[{name}] Clock TextMeshProUGUI is unassigned!");
            }

            if (_countdownCanvasGroup == null)
            {
                throw new InvalidOperationException($"[{name}] Countdown CanvasGroup is unassigned!");
            }

            if (_countdownText == null)
            {
                throw new InvalidOperationException($"[{name}] Countdown TextMeshProUGUI is unassigned!");
            }

            if (_playerNestBar == null)
            {
                throw new InvalidOperationException($"[{name}] Player Nest ResourceBar is unassigned!");
            }

            if (_playerNestHealthText == null)
            {
                throw new InvalidOperationException($"[{name}] Player Nest Health TextMeshProUGUI is unassigned!");
            }

            if (_enemyNestBar == null)
            {
                throw new InvalidOperationException($"[{name}] Enemy Nest ResourceBar is unassigned!");
            }

            if (_enemyNestHealthText == null)
            {
                throw new InvalidOperationException($"[{name}] Enemy Nest Health TextMeshProUGUI is unassigned!");
            }

            if (_playerBiomassBar == null)
            {
                throw new InvalidOperationException($"[{name}] Player Biomass ResourceBar is unassigned!");
            }

            if (_playerBiomassText == null)
            {
                throw new InvalidOperationException($"[{name}] Player Biomass TextMeshProUGUI is unassigned!");
            }

            if (_playerWorkerText == null)
            {
                throw new InvalidOperationException($"[{name}] Player Worker TextMeshProUGUI is unassigned!");
            }
        }

        #endregion
    }
}
