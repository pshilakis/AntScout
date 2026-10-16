using System;
using AntScout.UI;
using PGS.Core.Scenes;
using PGS.UI.Gauges;
using PGS.UI.Stack;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AntScout.Editor
{
    /// <summary>
    /// Editor utility for constructing the complete RTS Match UI hierarchy in the active scene with a single click.
    /// Sets up the Canvas, EventSystem, UIStackManager, MatchHudView, shader-driven ResourceBars, and MatchConcludedModal.
    /// Eliminates manual inspector wiring errors and sets up pixel-perfect responsive layouts.
    /// </summary>
    public static class MatchUiBuilder
    {
        private const string CanvasName = "[ Match UI Canvas - Screen Space ]";
        private const string ShaderName = "PGS/UI/FillBar";

        [MenuItem("Tools/AntScout/Setup Match Scene UI", false, 100)]
        public static void BuildMatchUi()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Error", "Please open a scene before running Setup Match Scene UI.", "OK");
                return;
            }

            // Check if UI already exists
            var existingCanvas = GameObject.Find(CanvasName);
            if (existingCanvas != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Match UI Already Exists",
                    $"A GameObject named '{CanvasName}' already exists in the scene. Do you want to replace it?",
                    "Replace",
                    "Cancel");

                if (!replace) return;
                Undo.DestroyObjectImmediate(existingCanvas);
            }

            Shader fillBarShader = Shader.Find(ShaderName);
            if (fillBarShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Missing Shader",
                    $"Could not locate shader '{ShaderName}'. Please ensure it is compiled in com.pgs.ui.",
                    "OK");
                return;
            }

            // 1. Ensure EventSystem exists in scene
            EnsureEventSystem();

            // 2. Create Master Canvas (Pure Presentation Root)
            GameObject canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Match UI Canvas");

            // 3. Create Match HUD View
            BuildMatchHudView(canvasGo.transform, fillBarShader);

            // 4. Create Match Concluded Modal
            BuildMatchConcludedModal(canvasGo.transform);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = canvasGo;

            Debug.Log("[MatchUiBuilder] Successfully created and wired Match UI hierarchy in the active scene!");
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
        }

        private static void BuildMatchHudView(Transform canvasTransform, Shader fillBarShader)
        {
            // Root HUD View (Sub-Canvas with isolated batching)
            GameObject hudGo = CreateRectTransform("Match HUD View", canvasTransform);
            StretchFull(hudGo.GetComponent<RectTransform>());

            var hudCanvas = hudGo.AddComponent<Canvas>();
            hudCanvas.overrideSorting = true;
            hudCanvas.sortingOrder = 10;
            hudGo.AddComponent<GraphicRaycaster>();

            var hudCg = hudGo.AddComponent<CanvasGroup>();
            var hudView = hudGo.AddComponent<MatchHudView>();

            // --- Top Header Panel ---
            GameObject headerGo = CreateRectTransform("Top Header Bar", hudGo.transform);
            RectTransform headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -15f);
            headerRt.sizeDelta = new Vector2(900f, 80f);

            var headerBg = headerGo.AddComponent<Image>();
            headerBg.color = new Color(0.04f, 0.05f, 0.08f, 0.82f);

            // Match Clock Text
            GameObject clockGo = CreateRectTransform("Clock Text", headerGo.transform);
            RectTransform clockRt = clockGo.GetComponent<RectTransform>();
            clockRt.anchorMin = new Vector2(0.5f, 0.5f);
            clockRt.anchorMax = new Vector2(0.5f, 0.5f);
            clockRt.pivot = new Vector2(0.5f, 0.5f);
            clockRt.anchoredPosition = new Vector2(0f, 0f);
            clockRt.sizeDelta = new Vector2(160f, 45f);

            var clockText = clockGo.AddComponent<TextMeshProUGUI>();
            clockText.text = "00:00";
            clockText.fontSize = 32f;
            clockText.fontStyle = FontStyles.Bold;
            clockText.alignment = TextAlignmentOptions.Center;
            clockText.color = Color.white;

            // Player Nest Bar (Left side)
            var (playerBar, playerHpText) = BuildNestHealthMeter(
                headerGo.transform,
                "Player Nest Gauge",
                "COLONY 1 (YOU)",
                new Vector2(-280f, 0f),
                new Color(0.2f, 0.85f, 0.3f, 1f),
                fillBarShader);

            // Enemy Nest Bar (Right side)
            var (enemyBar, enemyHpText) = BuildNestHealthMeter(
                headerGo.transform,
                "Enemy Nest Gauge",
                "COLONY 2 (ENEMY)",
                new Vector2(280f, 0f),
                new Color(0.9f, 0.25f, 0.25f, 1f),
                fillBarShader);

            // --- Pre-Match Countdown Overlay ---
            GameObject countdownGo = CreateRectTransform("Countdown Banner", hudGo.transform);
            RectTransform countdownRt = countdownGo.GetComponent<RectTransform>();
            countdownRt.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRt.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRt.pivot = new Vector2(0.5f, 0.5f);
            countdownRt.anchoredPosition = new Vector2(0f, 60f);
            countdownRt.sizeDelta = new Vector2(400f, 140f);

            var countdownCg = countdownGo.AddComponent<CanvasGroup>();
            countdownCg.blocksRaycasts = false;

            GameObject countdownTextGo = CreateRectTransform("Countdown Text", countdownGo.transform);
            StretchFull(countdownTextGo.GetComponent<RectTransform>());

            var countdownText = countdownTextGo.AddComponent<TextMeshProUGUI>();
            countdownText.text = "3";
            countdownText.fontSize = 90f;
            countdownText.fontStyle = FontStyles.Bold;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.color = new Color(1f, 0.85f, 0.2f, 1f);

            // Wire Serialized Properties on MatchHudView
            SerializedObject so = new SerializedObject(hudView);
            so.FindProperty("_clockText").objectReferenceValue = clockText;
            so.FindProperty("_countdownCanvasGroup").objectReferenceValue = countdownCg;
            so.FindProperty("_countdownText").objectReferenceValue = countdownText;
            so.FindProperty("_playerNestBar").objectReferenceValue = playerBar;
            so.FindProperty("_playerNestHealthText").objectReferenceValue = playerHpText;
            so.FindProperty("_enemyNestBar").objectReferenceValue = enemyBar;
            so.FindProperty("_enemyNestHealthText").objectReferenceValue = enemyHpText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (ResourceBar bar, TextMeshProUGUI hpText) BuildNestHealthMeter(
            Transform parent,
            string rootName,
            string title,
            Vector2 anchoredPos,
            Color fillColor,
            Shader fillBarShader)
        {
            GameObject container = CreateRectTransform(rootName, parent);
            RectTransform containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = anchoredPos;
            containerRt.sizeDelta = new Vector2(280f, 50f);

            // Title Label
            GameObject titleGo = CreateRectTransform("Title Label", container.transform);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, 0f);
            titleRt.sizeDelta = new Vector2(0f, 18f);

            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 13f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = fillColor;

            // Resource Bar Graphic
            GameObject barGo = CreateRectTransform("Bar Graphic", container.transform);
            RectTransform barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 4f);
            barRt.sizeDelta = new Vector2(0f, 22f);

            var barImg = barGo.AddComponent<Image>();
            barImg.color = Color.white;

            var resourceBar = barGo.AddComponent<ResourceBar>();

            // Configure ResourceBar serialized fields
            SerializedObject barSo = new SerializedObject(resourceBar);
            barSo.FindProperty("_shader").objectReferenceValue = fillBarShader;
            barSo.FindProperty("_fillColor").colorValue = fillColor;
            barSo.FindProperty("_emptyColor").colorValue = new Color(0.1f, 0.12f, 0.15f, 0.85f);
            barSo.FindProperty("_borderColor").colorValue = new Color(0.02f, 0.02f, 0.04f, 1f);
            barSo.FindProperty("_current").floatValue = 1000f;
            barSo.FindProperty("_max").floatValue = 1000f;
            barSo.FindProperty("_segmentSize").floatValue = 100f;
            barSo.FindProperty("_majorSegmentMultiplier").intValue = 5;
            barSo.FindProperty("_segmentGap").floatValue = 0.02f;
            barSo.FindProperty("_slant").floatValue = 0f;
            barSo.FindProperty("_enableGhostLag").boolValue = true;
            barSo.FindProperty("_ghostColor").colorValue = new Color(0.95f, 0.6f, 0.15f, 1f);
            barSo.ApplyModifiedPropertiesWithoutUndo();

            // HP Text Numbers
            GameObject hpTextGo = CreateRectTransform("HP Text", container.transform);
            RectTransform hpRt = hpTextGo.GetComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0f, 0f);
            hpRt.anchorMax = new Vector2(1f, 0f);
            hpRt.pivot = new Vector2(0.5f, 0f);
            hpRt.anchoredPosition = new Vector2(0f, 4f);
            hpRt.sizeDelta = new Vector2(0f, 22f);

            var hpText = hpTextGo.AddComponent<TextMeshProUGUI>();
            hpText.text = "1,000 / 1,000";
            hpText.fontSize = 13f;
            hpText.fontStyle = FontStyles.Bold;
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.color = Color.white;

            return (resourceBar, hpText);
        }

        private static void BuildMatchConcludedModal(Transform canvasTransform)
        {
            // Root Modal Dialog (Sub-Canvas with isolated batching and higher sorting order)
            GameObject modalGo = CreateRectTransform("Match Concluded Modal", canvasTransform);
            StretchFull(modalGo.GetComponent<RectTransform>());

            var modalCanvas = modalGo.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = 100;
            modalGo.AddComponent<GraphicRaycaster>();

            var modalCg = modalGo.AddComponent<CanvasGroup>();
            var modal = modalGo.AddComponent<MatchConcludedModal>();

            // Dark Dimmer Backdrop
            GameObject backdropGo = CreateRectTransform("Backdrop Dimmer", modalGo.transform);
            StretchFull(backdropGo.GetComponent<RectTransform>());
            var backdropImg = backdropGo.AddComponent<Image>();
            backdropImg.color = new Color(0.02f, 0.02f, 0.04f, 0.75f);

            // Centered Modal Window
            GameObject windowGo = CreateRectTransform("Window Panel", modalGo.transform);
            RectTransform windowRt = windowGo.GetComponent<RectTransform>();
            windowRt.anchorMin = new Vector2(0.5f, 0.5f);
            windowRt.anchorMax = new Vector2(0.5f, 0.5f);
            windowRt.pivot = new Vector2(0.5f, 0.5f);
            windowRt.anchoredPosition = Vector2.zero;
            windowRt.sizeDelta = new Vector2(520f, 380f);

            var windowBg = windowGo.AddComponent<Image>();
            windowBg.color = new Color(0.06f, 0.07f, 0.1f, 0.95f);

            // Outcome Title (VICTORY / DEFEAT)
            GameObject titleGo = CreateRectTransform("Outcome Title", windowGo.transform);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            titleRt.sizeDelta = new Vector2(0f, 60f);

            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "VICTORY";
            titleText.fontSize = 46f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.2f, 0.85f, 0.3f, 1f);

            // Subtitle
            GameObject subtitleGo = CreateRectTransform("Outcome Subtitle", windowGo.transform);
            RectTransform subtitleRt = subtitleGo.GetComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0f, 1f);
            subtitleRt.anchorMax = new Vector2(1f, 1f);
            subtitleRt.pivot = new Vector2(0.5f, 1f);
            subtitleRt.anchoredPosition = new Vector2(0f, -95f);
            subtitleRt.sizeDelta = new Vector2(0f, 30f);

            var subtitleText = subtitleGo.AddComponent<TextMeshProUGUI>();
            subtitleText.text = "Enemy Colony Nest Annihilated";
            subtitleText.fontSize = 17f;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.7f, 0.72f, 0.8f, 1f);

            // Telemetry Box (Duration & Winner)
            GameObject telemetryGo = CreateRectTransform("Telemetry Panel", windowGo.transform);
            RectTransform telemetryRt = telemetryGo.GetComponent<RectTransform>();
            telemetryRt.anchorMin = new Vector2(0.5f, 0.5f);
            telemetryRt.anchorMax = new Vector2(0.5f, 0.5f);
            telemetryRt.pivot = new Vector2(0.5f, 0.5f);
            telemetryRt.anchoredPosition = new Vector2(0f, -20f);
            telemetryRt.sizeDelta = new Vector2(400f, 80f);

            var telBg = telemetryGo.AddComponent<Image>();
            telBg.color = new Color(0.03f, 0.04f, 0.06f, 0.6f);

            // Duration Label & Value
            GameObject durGo = CreateRectTransform("Duration Value", telemetryGo.transform);
            RectTransform durRt = durGo.GetComponent<RectTransform>();
            durRt.anchorMin = new Vector2(0f, 0.5f);
            durRt.anchorMax = new Vector2(1f, 1f);
            durRt.pivot = new Vector2(0.5f, 0.5f);
            durRt.anchoredPosition = Vector2.zero;
            durRt.sizeDelta = Vector2.zero;

            var durText = durGo.AddComponent<TextMeshProUGUI>();
            durText.text = "Duration: 02:45";
            durText.fontSize = 18f;
            durText.alignment = TextAlignmentOptions.Center;
            durText.color = Color.white;

            // Winner Label & Value
            GameObject winGo = CreateRectTransform("Winner Value", telemetryGo.transform);
            RectTransform winRt = winGo.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0f, 0f);
            winRt.anchorMax = new Vector2(1f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.anchoredPosition = Vector2.zero;
            winRt.sizeDelta = Vector2.zero;

            var winText = winGo.AddComponent<TextMeshProUGUI>();
            winText.text = "Winning Faction: Colony 1";
            winText.fontSize = 16f;
            winText.alignment = TextAlignmentOptions.Center;
            winText.color = new Color(0.85f, 0.85f, 0.9f, 1f);

            // Play Again Button
            GameObject buttonGo = CreateRectTransform("Play Again Button", windowGo.transform);
            RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.5f, 0f);
            buttonRt.anchorMax = new Vector2(0.5f, 0f);
            buttonRt.pivot = new Vector2(0.5f, 0f);
            buttonRt.anchoredPosition = new Vector2(0f, 35f);
            buttonRt.sizeDelta = new Vector2(240f, 50f);

            var buttonImg = buttonGo.AddComponent<Image>();
            buttonImg.color = new Color(0.18f, 0.65f, 0.35f, 1f);

            var button = buttonGo.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.78f, 0.45f, 1f);
            colors.pressedColor = new Color(0.12f, 0.5f, 0.28f, 1f);
            button.colors = colors;

            GameObject btnTextGo = CreateRectTransform("Button Text", buttonGo.transform);
            StretchFull(btnTextGo.GetComponent<RectTransform>());

            var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnText.text = "PLAY AGAIN";
            btnText.fontSize = 18f;
            btnText.fontStyle = FontStyles.Bold;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            // Wire Serialized Properties on MatchConcludedModal
            SerializedObject modalSo = new SerializedObject(modal);
            modalSo.FindProperty("_outcomeTitleText").objectReferenceValue = titleText;
            modalSo.FindProperty("_outcomeSubtitleText").objectReferenceValue = subtitleText;
            modalSo.FindProperty("_durationValueText").objectReferenceValue = durText;
            modalSo.FindProperty("_winnerValueText").objectReferenceValue = winText;
            modalSo.FindProperty("_playAgainButton").objectReferenceValue = button;
            modalSo.FindProperty("_victoryColor").colorValue = new Color(0.2f, 0.85f, 0.3f, 1f);
            modalSo.FindProperty("_defeatColor").colorValue = new Color(0.9f, 0.25f, 0.25f, 1f);
            modalSo.FindProperty("_blocksUnderlyingViews").boolValue = true;
            modalSo.FindProperty("_registerAsOcclusionBlocker").boolValue = true;

            const string sceneRefPath = "Assets/Game Assets/Scenes/SceneRef_MatchScene.asset";
            var sceneRef = AssetDatabase.LoadAssetAtPath<SceneReference>(sceneRefPath);
            if (sceneRef != null)
            {
                modalSo.FindProperty("_rematchScene").objectReferenceValue = sceneRef;
            }

            modalSo.ApplyModifiedPropertiesWithoutUndo();

            // Modal is hidden initially
            modalCg.alpha = 0f;
            modalCg.interactable = false;
            modalCg.blocksRaycasts = false;
        }

        private static GameObject CreateRectTransform(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
