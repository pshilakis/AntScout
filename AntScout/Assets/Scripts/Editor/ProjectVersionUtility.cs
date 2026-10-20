using System;
using UnityEditor;
using UnityEngine;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor utility providing 1-click semantic version bumping for PlayerSettings.
    /// Grounded in Single Responsibility (SRP): Manages project bundle version progression cleanly
    /// without manual file manipulation.
    /// </summary>
    public static class ProjectVersionUtility
    {
        [MenuItem("AntScout/Version/Bump Patch (+0.0.1)", false, 200)]
        public static void BumpPatch()
        {
            ApplyVersionBump(VersionComponent.Patch);
        }

        [MenuItem("AntScout/Version/Bump Minor (+0.1.0)", false, 201)]
        public static void BumpMinor()
        {
            ApplyVersionBump(VersionComponent.Minor);
        }

        [MenuItem("AntScout/Version/Bump Major (+1.0.0)", false, 202)]
        public static void BumpMajor()
        {
            ApplyVersionBump(VersionComponent.Major);
        }

        [MenuItem("AntScout/Version/Print Current Version", false, 220)]
        public static void PrintCurrentVersion()
        {
            string current = PlayerSettings.bundleVersion;
            Debug.Log($"[ProjectVersionUtility] Current Project Version: {current}");
            EditorUtility.DisplayDialog("Project Version", $"Current Project Version: {current}", "OK");
        }

        private enum VersionComponent
        {
            Major,
            Minor,
            Patch
        }

        private static void ApplyVersionBump(VersionComponent component)
        {
            string currentVersion = PlayerSettings.bundleVersion;
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                currentVersion = "0.1.0";
            }

            if (!TryParseSemVer(currentVersion, out int major, out int minor, out int patch))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Version Format",
                    $"The current version '{currentVersion}' cannot be parsed as a semantic version (Major.Minor.Patch). Please set it manually in Project Settings.",
                    "OK");
                return;
            }

            switch (component)
            {
                case VersionComponent.Major:
                    major += 1;
                    minor = 0;
                    patch = 0;
                    break;
                case VersionComponent.Minor:
                    minor += 1;
                    patch = 0;
                    break;
                case VersionComponent.Patch:
                    patch += 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(component), component, null);
            }

            string newVersion = $"{major}.{minor}.{patch}";
            PlayerSettings.bundleVersion = newVersion;
            AssetDatabase.SaveAssets();

            Debug.Log($"[ProjectVersionUtility] Bumped version ({component}): {currentVersion} -> {newVersion}");
            EditorUtility.DisplayDialog(
                "Version Updated",
                $"Project version successfully updated from {currentVersion} to: {newVersion}",
                "OK");
        }

        private static bool TryParseSemVer(string versionStr, out int major, out int minor, out int patch)
        {
            major = 0;
            minor = 0;
            patch = 0;

            if (string.IsNullOrWhiteSpace(versionStr)) return false;

            // Strip build metadata or pre-release suffixes (e.g., "0.2.0-beta" or "0.2.0+123")
            int plusIndex = versionStr.IndexOf('+');
            if (plusIndex >= 0) versionStr = versionStr.Substring(0, plusIndex);

            int dashIndex = versionStr.IndexOf('-');
            if (dashIndex >= 0) versionStr = versionStr.Substring(0, dashIndex);

            string[] parts = versionStr.Split('.');
            if (parts.Length < 2) return false;

            if (!int.TryParse(parts[0], out major)) return false;
            if (!int.TryParse(parts[1], out minor)) return false;

            if (parts.Length >= 3)
            {
                if (!int.TryParse(parts[2], out patch)) return false;
            }
            else
            {
                patch = 0;
            }

            return true;
        }
    }
}
