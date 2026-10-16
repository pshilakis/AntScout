using System;
using AntScout.Match;
using PGS.Core.Scenes;
using PGS.UI.Stack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AntScout.UI
{
    /// <summary>
    /// Modal dialog displayed when an RTS skirmish match concludes.
    /// Inherits from <see cref="UIView"/> to integrate with the PGS UI Stack.
    /// Blocks underlying HUD interaction and 3D raycasting via <see cref="PGS.UI.Occlusion.UIOcclusionService"/>.
    /// Displays outcome (Victory vs Defeat), match duration, and provides a restart button.
    /// Strictly adheres to Single Responsibility (SRP) and Fail-Fast standards.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchConcludedModal : UIView
    {
        #region Serialized Fields

        [Header("Banner & Titles")]
        [Tooltip("Large outcome header text (VICTORY / DEFEAT).")]
        [SerializeField] private TextMeshProUGUI _outcomeTitleText;

        [Tooltip("Subheading text describing why the match ended.")]
        [SerializeField] private TextMeshProUGUI _outcomeSubtitleText;

        [Tooltip("Color applied to the title when the local player wins.")]
        [SerializeField] private Color _victoryColor = new Color(0.2f, 0.85f, 0.3f, 1.0f);

        [Tooltip("Color applied to the title when the local player is defeated.")]
        [SerializeField] private Color _defeatColor = new Color(0.9f, 0.2f, 0.2f, 1.0f);

        [Header("Telemetry Details")]
        [Tooltip("Text label displaying final match duration (MM:SS).")]
        [SerializeField] private TextMeshProUGUI _durationValueText;

        [Tooltip("Text label displaying the winning colony name.")]
        [SerializeField] private TextMeshProUGUI _winnerValueText;

        [Header("Actions")]
        [Tooltip("Button allowing the player to reload the scene and play again.")]
        [SerializeField] private Button _playAgainButton;

        [Header("Scene Navigation")]
        [Tooltip("Type-safe SceneReference asset to reload the match or launch the next map. If unassigned, reloads the current active scene.")]
        [SerializeField] private SceneReference _rematchScene;

        #endregion

        #region Lifecycle & Initialization

        protected override void Awake()
        {
            base.Awake();
            ValidateComponentBindings();
            SetVisible(false);

            if (_playAgainButton != null)
            {
                _playAgainButton.onClick.AddListener(HandlePlayAgainClicked);
            }
        }

        private void Start()
        {
            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.OnMatchConcluded += HandleMatchConcluded;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_playAgainButton != null)
            {
                _playAgainButton.onClick.RemoveListener(HandlePlayAgainClicked);
            }

            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.OnMatchConcluded -= HandleMatchConcluded;
            }
        }

        #endregion

        #region Match Conclusion Handling

        private void HandleMatchConcluded(MatchResultContext result)
        {
            int playerTeamId = MatchDirector.Instance != null ? MatchDirector.Instance.PlayerTeamId : 0;
            bool isVictory = result.WinningTeamId == playerTeamId;

            // Configure Visuals
            _outcomeTitleText.text = isVictory ? "VICTORY" : "DEFEAT";
            _outcomeTitleText.color = isVictory ? _victoryColor : _defeatColor;

            if (_outcomeSubtitleText != null)
            {
                _outcomeSubtitleText.text = isVictory
                    ? "Enemy Colony Nest Annihilated"
                    : "Your Colony Nest Has Fallen";
            }

            if (_durationValueText != null)
            {
                int minutes = Mathf.FloorToInt(result.ActivePlayDuration / 60f);
                int seconds = Mathf.FloorToInt(result.ActivePlayDuration % 60f);
                _durationValueText.text = $"{minutes:00}:{seconds:00}";
            }

            if (_winnerValueText != null)
            {
                _winnerValueText.text = result.WinningTeamId >= 0
                    ? $"Colony {result.WinningTeamId + 1}"
                    : "Draw";
            }

            // Push to UI Stack Manager or activate directly
            if (UIStackManager.Instance != null)
            {
                UIStackManager.Instance.PushView(this);
            }
            else
            {
                OnEnter();
            }
        }

        private void HandlePlayAgainClicked()
        {
            if (_rematchScene != null && _rematchScene.IsAssigned)
            {
                _rematchScene.LoadScene(LoadSceneMode.Single);
            }
            else
            {
                int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
                SceneManager.LoadScene(activeSceneIndex);
            }
        }

        #endregion

        #region Validation

        private void ValidateComponentBindings()
        {
            if (_outcomeTitleText == null)
            {
                throw new InvalidOperationException($"[{name}] Outcome Title TextMeshProUGUI is unassigned!");
            }

            if (_playAgainButton == null)
            {
                throw new InvalidOperationException($"[{name}] Play Again Button is unassigned!");
            }
        }

        #endregion
    }
}
