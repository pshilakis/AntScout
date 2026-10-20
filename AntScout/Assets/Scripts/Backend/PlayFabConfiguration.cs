using System;
using UnityEngine;

namespace AntScout.Backend
{
    /// <summary>
    /// Configuration asset for Microsoft PlayFab title settings and telemetry policies.
    /// Grounded in Single Responsibility (SRP) and Single Source of Truth standards.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayFabConfiguration", menuName = "AntScout/Backend/PlayFab Configuration", order = 100)]
    public class PlayFabConfiguration : ScriptableObject
    {
        #region Serialized Fields

        [Header("PlayFab Title Settings")]
        [Tooltip("The 4-to-6 alphanumeric Title ID from your PlayFab Game Manager dashboard (e.g. 19FC86).")]
        [SerializeField] private string _titleId = "19FC86";

        [Header("Telemetry Policies")]
        [Tooltip("Master switch to enable or disable remote telemetry event collection.")]
        [SerializeField] private bool _enableTelemetry = true;

        [Tooltip("If true, outputs verbose diagnostics to the Unity console on login and event dispatch.")]
        [SerializeField] private bool _verboseLogging = false;

        #endregion

        #region Public Properties

        public string TitleId => _titleId;
        public bool EnableTelemetry => _enableTelemetry;
        public bool VerboseLogging => _verboseLogging;

        #endregion

        #region Validation

        public void ValidateConfiguration()
        {
            if (_enableTelemetry && string.IsNullOrWhiteSpace(_titleId))
            {
                throw new InvalidOperationException($"[{name}] PlayFab Telemetry is enabled, but Title ID is blank! Please configure a valid Title ID in the inspector.");
            }
        }

        #endregion
    }
}
