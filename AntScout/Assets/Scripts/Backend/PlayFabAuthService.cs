using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace AntScout.Backend
{
    /// <summary>
    /// Authentication service managing anonymous Microsoft PlayFab guest device logins.
    /// Follows the Single Responsibility Principle (SRP):
    /// Handles identity creation, persistent credentials caching in PlayerPrefs, and session lifecycle.
    /// </summary>
    public static class PlayFabAuthService
    {
        private const string CustomIdPrefsKey = "AntScout_PlayFab_CustomId";

        private static UniTaskCompletionSource<string> _loginTaskSource;

        public static bool IsLoggedIn => PlayFabClientAPI.IsClientLoggedIn();
        public static string PlayFabId { get; private set; }

        /// <summary>
        /// Ensures the player is authenticated with PlayFab. If already logged in, returns cached PlayFabId.
        /// If a login is already in progress, awaits the in-flight request to prevent duplicate calls.
        /// </summary>
        public static async UniTask<string> EnsureLoggedInAsync(PlayFabConfiguration config, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "[PlayFabAuthService] PlayFabConfiguration must be provided!");
            }

            config.ValidateConfiguration();

            if (!string.IsNullOrEmpty(config.TitleId))
            {
                PlayFabSettings.staticSettings.TitleId = config.TitleId;
            }

            if (IsLoggedIn && !string.IsNullOrEmpty(PlayFabId))
            {
                return PlayFabId;
            }

            if (_loginTaskSource != null)
            {
                return await _loginTaskSource.Task.AttachExternalCancellation(cancellationToken);
            }

            _loginTaskSource = new UniTaskCompletionSource<string>();
            var loginTask = _loginTaskSource.Task;

            string customId = GetOrCreateCustomId();

            var request = new LoginWithCustomIDRequest
            {
                CustomId = customId,
                CreateAccount = true,
                TitleId = config.TitleId
            };

            if (config.VerboseLogging)
            {
                Debug.Log($"[PlayFabAuthService] Attempting silent guest login for CustomId: {customId} (Title: {config.TitleId})...");
            }

            PlayFabClientAPI.LoginWithCustomID(
                request,
                result =>
                {
                    PlayFabId = result.PlayFabId;
                    Debug.Log($"[PlayFabAuthService] Connected to PlayFab guest account: {PlayFabId} (NewlyCreated: {result.NewlyCreated})");
                    var tcs = _loginTaskSource;
                    _loginTaskSource = null;
                    tcs?.TrySetResult(PlayFabId);
                },
                error =>
                {
                    string errorMsg = error.GenerateErrorReport();
                    Debug.LogWarning($"[PlayFabAuthService] PlayFab silent login failed: {errorMsg}");
                    var tcs = _loginTaskSource;
                    _loginTaskSource = null;
                    tcs?.TrySetException(new InvalidOperationException($"PlayFab login failed: {error.ErrorMessage} ({error.Error})"));
                }
            );

            return await loginTask.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// Retrieves the persistent custom ID from PlayerPrefs, or generates a stable unique device ID.
        /// </summary>
        public static string GetOrCreateCustomId()
        {
            string id = PlayerPrefs.GetString(CustomIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
                {
                    id = Guid.NewGuid().ToString("N");
                }
                PlayerPrefs.SetString(CustomIdPrefsKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        /// <summary>
        /// Clears cached session credentials.
        /// </summary>
        public static void ResetSession()
        {
            PlayFabId = null;
            _loginTaskSource = null;
        }
    }
}
