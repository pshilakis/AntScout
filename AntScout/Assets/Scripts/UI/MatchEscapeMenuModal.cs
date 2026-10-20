using System;
using AntScout.Match;
using PGS.Core.Time;
using PGS.UI.Stack;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AntScout.UI
{
    /// <summary>
    /// In-game Escape/Pause menu modal providing Resume, Restart, Forfeit, and Quit to Desktop options.
    /// Modeled after StarCraft II's in-game tactical menu.
    /// Freezes simulation time deterministically via PgsTime.PauseAll() and Time.timeScale.
    /// Grounded in Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchEscapeMenuModal : UIView
    {
        #region Serialized Fields

        [Header("Buttons")]
        [Tooltip("Button to resume active match and unfreeze simulation.")]
        [SerializeField] private Button _resumeButton;

        [Tooltip("Button to immediately reload the active match scene.")]
        [SerializeField] private Button _restartButton;

        [Tooltip("Button to concede the match, triggering defeat telemetry.")]
        [SerializeField] private Button _forfeitButton;

        [Tooltip("Button to terminate the application executable.")]
        [SerializeField] private Button _quitDesktopButton;

        [Header("Time & Pause Policy")]
        [Tooltip("If true, pauses simulation time via PgsTime.PauseAll() and Time.timeScale while open (StarCraft II singleplayer pause).")]
        [SerializeField] private bool _freezeTimeOnOpen = true;

        #endregion

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        #region Lifecycle

        protected override void Awake()
        {
            base.Awake();
            ValidateComponentBindings();
            SetVisible(false);

            if (_resumeButton != null)
            {
                _resumeButton.onClick.AddListener(ResumeMatch);
            }
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(RestartMatch);
            }
            if (_forfeitButton != null)
            {
                _forfeitButton.onClick.AddListener(ForfeitMatch);
            }
            if (_quitDesktopButton != null)
            {
                _quitDesktopButton.onClick.AddListener(QuitToDesktop);
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Do not intercept Escape if the match has already concluded (Victory/Defeat modal has ownership)
            if (MatchDirector.Instance != null && MatchDirector.Instance.State == MatchState.Concluded)
            {
                return;
            }

            ToggleMenu();
        }

        protected virtual void OnDestroy()
        {
            if (_freezeTimeOnOpen && _isOpen)
            {
                PgsTime.ResumeAll();
                Time.timeScale = 1f;
            }

            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(ResumeMatch);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(RestartMatch);
            if (_forfeitButton != null) _forfeitButton.onClick.RemoveListener(ForfeitMatch);
            if (_quitDesktopButton != null) _quitDesktopButton.onClick.RemoveListener(QuitToDesktop);
        }

        #endregion

        #region Menu Controls

        public void ToggleMenu()
        {
            if (_isOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        public void OpenMenu()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (_freezeTimeOnOpen)
            {
                PgsTime.PauseAll();
                Time.timeScale = 0f;
            }

            if (UIStackManager.Instance != null)
            {
                UIStackManager.Instance.PushView(this);
            }
            else
            {
                OnEnter();
            }
        }

        public void CloseMenu()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (_freezeTimeOnOpen)
            {
                PgsTime.ResumeAll();
                Time.timeScale = 1f;
            }

            if (UIStackManager.Instance != null && ReferenceEquals(UIStackManager.Instance.CurrentView, this))
            {
                UIStackManager.Instance.PopView();
            }
            else
            {
                OnExit();
            }
        }

        public void ResumeMatch()
        {
            CloseMenu();
        }

        public void RestartMatch()
        {
            if (_freezeTimeOnOpen && _isOpen)
            {
                PgsTime.ResumeAll();
                Time.timeScale = 1f;
            }

            int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(activeSceneIndex);
        }

        public void ForfeitMatch()
        {
            CloseMenu();

            if (MatchDirector.Instance != null)
            {
                int playerTeamId = MatchDirector.Instance.PlayerTeamId;
                int enemyTeamId = playerTeamId == 0 ? 1 : 0;
                MatchDirector.Instance.ConcludeMatch(MatchResult.Defeat, enemyTeamId);
            }
        }

        public void QuitToDesktop()
        {
            if (_freezeTimeOnOpen && _isOpen)
            {
                PgsTime.ResumeAll();
                Time.timeScale = 1f;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Validation

        private void ValidateComponentBindings()
        {
            if (_resumeButton == null)
            {
                throw new InvalidOperationException($"[{name}] Resume Button is unassigned!");
            }
            if (_restartButton == null)
            {
                throw new InvalidOperationException($"[{name}] Restart Button is unassigned!");
            }
            if (_forfeitButton == null)
            {
                throw new InvalidOperationException($"[{name}] Forfeit Button is unassigned!");
            }
            if (_quitDesktopButton == null)
            {
                throw new InvalidOperationException($"[{name}] Quit to Desktop Button is unassigned!");
            }
        }

        #endregion
    }
}
