using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LemonPuzzle.EditorTools
{
    /// <summary>
    /// One-click scene wiring: Tools &gt; Lemon Puzzle &gt; Setup Scene.
    /// Builds the Canvas/EventSystem (if missing), the HUD, the board panel + cell prefab, the
    /// start/game-over overlays, and wires every reference on GameManager / BoardController /
    /// HUDController. Safe to leave in the project - it only touches the currently open scene and
    /// only runs when you invoke the menu item.
    /// </summary>
    public static class LemonPuzzleSceneSetup
    {
        const string ArtPath = "Assets/LemonGame/Art/Lemon.png";
        const string SkyBackgroundPath = "Assets/LemonGame/Art/SkyBackground.png";
        const string StartBackgroundPath = "Assets/LemonGame/Art/StartBackground.png";
        const string PrefabFolder = "Assets/LemonGame/Prefabs";
        const string PrefabPath = PrefabFolder + "/LemonCell.prefab";

        static readonly Vector2 BoardAnchorMin = new Vector2(0.03f, 0.04f);
        static readonly Vector2 BoardAnchorMax = new Vector2(0.97f, 0.78f);

        // Scattered lemon-slice decorations over the sky background: (anchorX, anchorY, size, rotation, alpha).
        // Anchors are 0-1 across the full canvas; most of these sit behind the opaque board panel and
        // only peek out along the top strip and thin side margins, which is the point - a light touch.
        static readonly (float x, float y, float size, float rot, float alpha)[] FruitDecorSpots =
        {
            (0.08f, 0.95f, 60f, -15f, 0.35f),
            (0.18f, 0.85f, 40f, 20f, 0.25f),
            (0.92f, 0.95f, 55f, 25f, 0.30f),
            (0.82f, 0.85f, 35f, -10f, 0.25f),
            (0.35f, 0.97f, 30f, 10f, 0.20f),
            (0.65f, 0.97f, 32f, -20f, 0.20f),
            (0.02f, 0.50f, 45f, 8f, 0.18f),
            (0.98f, 0.50f, 45f, -8f, 0.18f),
        };

        // Start-screen decorative lemons (fruits-box style): (x, y, number), anchored to canvas
        // center so they scatter across the right side while the big Play lemon sits on the left.
        static readonly (float x, float y, string num)[] StartDecorSpots =
        {
            (60f, 220f, "5"), (220f, 220f, "7"), (380f, 220f, "6"), (540f, 220f, "8"),
            (140f, 60f, "3"), (300f, 60f, "2"), (460f, 60f, "9"),
            (60f, -140f, "1"), (380f, -140f, "4"),
        };

        [MenuItem("Tools/Lemon Puzzle/Setup Scene (Create UI + Wire Managers)")]
        public static void SetupScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐 셋업",
                    $"{ArtPath} 위치에서 스프라이트를 찾지 못했어요.\nLemon.png를 선택한 뒤 Inspector에서 Texture Type을 'Sprite (2D and UI)'로 바꾸고 Apply를 눌러주세요.",
                    "확인");
                return;
            }

            if (GameObject.Find("BoardPanel") != null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐 셋업",
                    "씬에 이미 BoardPanel이 있어요. 다시 만들려면 Canvas 하위의 기존 UI 오브젝트들을 지우고 다시 실행해주세요.",
                    "확인");
                return;
            }

            // ---- Canvas & EventSystem ----
            var canvas = Object.FindFirstObjectByType<Canvas>();
            GameObject canvasGO;
            if (canvas == null)
            {
                canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 1000);
                scaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                canvasGO = canvas.gameObject;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(EventSystem));
                // Use reflection so this compiles whether or not the Input System package is installed.
                var inputSystemModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemModuleType != null) esGO.AddComponent(inputSystemModuleType);
                else esGO.AddComponent<StandaloneInputModule>();
            }

            // ---- GameManager ----
            var gmGO = GameObject.Find("LemonGameManager") ?? new GameObject("LemonGameManager");
            var gameManager = gmGO.GetComponent<GameManager>() ?? gmGO.AddComponent<GameManager>();

            // ---- Fresh sky background + scattered lemon decorations (always first, so everything
            // else in this method draws on top of it) ----
            CreateFreshBackground(canvasGO.transform, sprite);

            // ---- Title & HUD texts ----
            var titleText = CreateText("Title", canvasGO.transform, "레몬 퍼즐", 34, TextAnchor.MiddleCenter);
            SetAnchors(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(500, 60));

            // Reset button takes the far-left spot the timer used to sit in; the timer moves into the
            // gap between it and the target badge. Score still flanks the badge on the right.
            var resetButton = CreateSmallButton("ResetButton", canvasGO.transform, "리셋", new Vector2(180, -140), new Vector2(140, 56));

            var timerText = CreateText("TimerText", canvasGO.transform, "03:00", 34, TextAnchor.MiddleCenter);
            SetAnchors(timerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470, -140), new Vector2(220, 60));

            var scoreLabel = CreateText("ScoreText", canvasGO.transform, "0", 34, TextAnchor.MiddleCenter);
            SetAnchors(scoreLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180, -140), new Vector2(220, 60));

            var bestText = CreateText("BestScoreText", canvasGO.transform, "0", 16, TextAnchor.MiddleCenter);
            SetAnchors(bestText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180, -180), new Vector2(220, 26));

            // ---- Target badge (same lemon sprite as the cells, just bigger) ----
            var badgeGO = new GameObject("TargetBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(canvasGO.transform, false);
            var badgeRT = (RectTransform)badgeGO.transform;
            SetAnchors(badgeRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(110, 110));
            var badgeImage = badgeGO.GetComponent<Image>();
            badgeImage.sprite = sprite;
            badgeImage.preserveAspect = true;

            var badgeNumber = CreateText("TargetNumber", badgeGO.transform, "10", 32, TextAnchor.MiddleCenter);
            badgeNumber.color = Color.black;
            badgeNumber.fontStyle = FontStyle.Bold;
            SetAnchors(badgeNumber.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var targetBadgeView = badgeGO.AddComponent<TargetBadgeView>();
            AssignPrivateField(targetBadgeView, "targetText", badgeNumber);
            AssignPrivateField(targetBadgeView, "badgeRect", badgeRT);

            var currentSumText = CreateText("CurrentSumText", canvasGO.transform, "", 18, TextAnchor.MiddleCenter);
            SetAnchors(currentSumText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(200, 30));

            // ---- Board panel ----
            var boardGO = new GameObject("BoardPanel", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup));
            boardGO.transform.SetParent(canvasGO.transform, false);
            var boardRT = (RectTransform)boardGO.transform;
            SetAnchors(boardRT, BoardAnchorMin, BoardAnchorMax, Vector2.zero, Vector2.zero);
            boardGO.GetComponent<Image>().color = new Color(0.95f, 0.9f, 0.72f, 1f);
            var gridLayout = boardGO.GetComponent<GridLayoutGroup>();

            // Selection indicator overlay - top-left pivot so its anchoredPosition/sizeDelta map
            // directly onto BoardController's row/col pixel math.
            var selRT = CreateSelectionIndicator(boardGO.transform);

            // ---- Cell prefab ----
            var cellPrefab = CreateOrLoadCellPrefab(sprite);

            var boardController = boardGO.AddComponent<BoardController>();
            boardController.gameManager = gameManager;
            boardController.boardRect = boardRT;
            boardController.gridLayout = gridLayout;
            boardController.cellPrefab = cellPrefab;
            boardController.selectionIndicator = selRT;
            boardController.targetBadge = targetBadgeView;
            boardController.currentSumText = currentSumText;

            // ---- Overlays ----
            var startPanel = CreateStartOverlay(canvasGO.transform, sprite, out var startButton);

            var endPanel = CreateOverlay("EndOverlay", canvasGO.transform,
                "타임 오버!", "최종 점수예요. 다시 도전해볼까요?",
                "다시 시작", out var restartButton, out var endTitleText, out var endSubText, out var finalScoreText);
            endPanel.SetActive(false);

            // ---- Toast ----
            var toastGO = new GameObject("Toast", typeof(RectTransform), typeof(Image));
            toastGO.transform.SetParent(canvasGO.transform, false);
            SetAnchors((RectTransform)toastGO.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(300, 50));
            toastGO.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.2f, 0.9f);
            var toastText = CreateText("ToastText", toastGO.transform, "", 18, TextAnchor.MiddleCenter);
            toastText.color = Color.white;
            SetAnchors(toastText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            toastGO.SetActive(false);

            // ---- HUDController ----
            var hudGO = GameObject.Find("HUDController") ?? new GameObject("HUDController");
            var hud = hudGO.GetComponent<HUDController>() ?? hudGO.AddComponent<HUDController>();
            hud.gameManager = gameManager;
            hud.targetBadge = targetBadgeView;
            hud.scoreText = scoreLabel;
            hud.timerText = timerText;
            hud.bestScoreText = bestText;
            hud.startPanel = startPanel;
            hud.endPanel = endPanel;
            hud.endTitleText = endTitleText;
            hud.endSubText = endSubText;
            hud.finalScoreText = finalScoreText;
            hud.startButton = startButton;
            hud.restartButton = restartButton;
            hud.resetButton = resetButton;
            hud.toastRoot = toastGO;
            hud.toastText = toastText;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐 셋업 완료",
                "씬에 UI와 매니저를 모두 배치했어요. Play를 눌러 확인해보세요!\n(Ctrl+S로 씬 저장하는 것도 잊지 마세요)",
                "확인");
        }

        /// <summary>
        /// Re-sizes an already-built BoardPanel to the current BoardAnchorMin/Max without touching
        /// anything else. Use this if you already ran Setup Scene once and just want a bigger board -
        /// no need to delete and rebuild the whole UI.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Make Board Taller")]
        public static void ResizeBoardPanel()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var boardGO = GameObject.Find("BoardPanel");
            if (boardGO == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "BoardPanel을 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var rt = (RectTransform)boardGO.transform;
            rt.anchorMin = BoardAnchorMin;
            rt.anchorMax = BoardAnchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐", "보드를 더 크게 늘렸어요. Play로 확인해보세요 (Ctrl+S로 저장하는 것도 잊지 마세요).", "확인");
        }

        /// <summary>
        /// Rebuilds the drag-selection box with a clearly visible border (the plain tinted fill
        /// used before was too subtle to notice against the board). Safe to run on a scene that
        /// already has a SelectionIndicator - the old one is replaced.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Fix Selection Box (Add Border)")]
        public static void FixSelectionBox()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var boardGO = GameObject.Find("BoardPanel");
            if (boardGO == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "BoardPanel을 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var existing = boardGO.transform.Find("SelectionIndicator");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var selRT = CreateSelectionIndicator(boardGO.transform);
            selRT.SetAsLastSibling(); // draw on top of the already-instantiated lemon cells, if any

            var boardController = boardGO.GetComponent<BoardController>();
            if (boardController != null) boardController.selectionIndicator = selRT;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐", "드래그 선택 박스에 테두리를 추가했어요. Play로 확인해보세요.", "확인");
        }

        /// <summary>
        /// Moves the already-placed TimerText / ScoreText / BestScoreText to flank the target badge
        /// (timer on the left, score on the right, at the badge's height) instead of their old spot
        /// near the top corners. Use this on a scene that was already built with Setup Scene - that
        /// menu item refuses to re-run once BoardPanel exists, so this is the targeted fix-up.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Reposition Score & Timer")]
        public static void RepositionScoreAndTimer()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var timerGO = GameObject.Find("TimerText");
            var scoreGO = GameObject.Find("ScoreText");
            var bestGO = GameObject.Find("BestScoreText");

            if (timerGO == null || scoreGO == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "TimerText/ScoreText를 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var timerText = timerGO.GetComponent<Text>();
            SetAnchors((RectTransform)timerGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180, -140), new Vector2(220, 60));
            if (timerText != null)
            {
                timerText.fontSize = 34;
                timerText.alignment = TextAnchor.MiddleCenter;
            }

            var scoreText = scoreGO.GetComponent<Text>();
            SetAnchors((RectTransform)scoreGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180, -140), new Vector2(220, 60));
            if (scoreText != null)
            {
                scoreText.fontSize = 34;
                scoreText.alignment = TextAnchor.MiddleCenter;
            }

            if (bestGO != null)
            {
                var bestText = bestGO.GetComponent<Text>();
                SetAnchors((RectTransform)bestGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180, -180), new Vector2(220, 26));
                if (bestText != null) bestText.alignment = TextAnchor.MiddleCenter;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐", "제한시간은 목표 숫자 왼쪽, 점수는 오른쪽으로 옮겼어요. Play로 확인해보세요 (Ctrl+S로 저장하는 것도 잊지 마세요).", "확인");
        }

        /// <summary>
        /// Adds an in-game Reset button at the timer's old spot (far left, badge height) and moves
        /// the timer into the gap between it and the target badge - the layout the user pointed at.
        /// Safe to re-run: an existing ResetButton is replaced rather than duplicated. Use this on a
        /// scene that was already built with Setup Scene, since that menu item won't re-run once
        /// BoardPanel exists.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Add Reset Button")]
        public static void AddResetButton()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            var timerGO = GameObject.Find("TimerText");
            var hud = Object.FindFirstObjectByType<HUDController>();
            if (canvas == null || timerGO == null || hud == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "씬을 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var existingReset = canvas.transform.Find("ResetButton");
            if (existingReset != null) Object.DestroyImmediate(existingReset.gameObject);

            var resetButton = CreateSmallButton("ResetButton", canvas.transform, "리셋", new Vector2(180, -140), new Vector2(140, 56));
            SetAnchors((RectTransform)timerGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470, -140), new Vector2(220, 60));

            hud.resetButton = resetButton;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐", "리셋 버튼을 추가하고 제한시간과 위치를 바꿨어요. Play로 확인해보세요 (Ctrl+S로 저장하는 것도 잊지 마세요).", "확인");
        }

        /// <summary>
        /// Rebuilds the start-screen overlay in "fruits-box" style: a gingham checkerboard background,
        /// a big lemon-shaped Play button on the left, an orange logo top-left, and a scatter of small
        /// numbered decorative lemons on the right. Safe to re-run - any existing StartOverlay is
        /// replaced. Use this on a scene that was already built with Setup Scene, since that menu item
        /// refuses to re-run once BoardPanel exists.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Redesign Start Screen")]
        public static void RedesignStartScreen()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            var hud = Object.FindFirstObjectByType<HUDController>();
            if (canvas == null || hud == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "씬을 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐",
                    $"{ArtPath} 위치에서 스프라이트를 찾지 못했어요.\nLemon.png를 선택한 뒤 Inspector에서 Texture Type을 'Sprite (2D and UI)'로 바꾸고 Apply를 눌러주세요.",
                    "확인");
                return;
            }

            var existing = canvas.transform.Find("StartOverlay");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var startPanel = CreateStartOverlay(canvas.transform, sprite, out var startButton);
            startPanel.transform.SetAsLastSibling(); // sit above everything else, like the original overlay did

            hud.startPanel = startPanel;
            hud.startButton = startButton; // HUDController.Start() wires the click listener when Play runs

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐", "시작 화면을 과일박스 스타일로 새로 만들었어요. Play로 확인해보세요 (Ctrl+S로 저장하는 것도 잊지 마세요).", "확인");
        }

        /// <summary>
        /// Clears the saved best score (PlayerPrefs key must match HUDController.BestScoreKey) and,
        /// if the scene already has a BestScoreText, resets it to "0" immediately so you don't need
        /// to press Play to see it take effect.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Reset Best Score")]
        public static void ResetBestScore()
        {
            const string bestScoreKey = "LemonPuzzle.Best"; // keep in sync with HUDController.BestScoreKey
            PlayerPrefs.DeleteKey(bestScoreKey);
            PlayerPrefs.Save();

            var bestGO = GameObject.Find("BestScoreText");
            if (bestGO != null)
            {
                var bestText = bestGO.GetComponent<Text>();
                if (bestText != null) bestText.text = "0";
            }

            EditorUtility.DisplayDialog("레몬 퍼즐", "베스트 스코어를 초기화했어요.", "확인");
        }

        /// <summary>
        /// Bumps up the number text inside the LemonCell prefab (it was hard to read at the default
        /// size). Edits the prefab asset directly, so it takes effect for every cell next time the
        /// board is built - no need to re-run Setup Scene or touch the already-open scene.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Make Lemon Numbers Bigger")]
        public static void IncreaseLemonNumberSize()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "LemonCell 프리팹을 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var text = prefab.GetComponentInChildren<Text>(true);
            if (text == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "프리팹 안에서 숫자 Text를 찾을 수 없어요.", "확인");
                return;
            }

            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("레몬 퍼즐", "레몬 안의 숫자를 더 크게 키웠어요. Play로 확인해보세요.", "확인");
        }

        /// <summary>
        /// Swaps in a bright, fresh sky-blue gradient background (with soft clouds + a sun glow) plus
        /// a handful of scattered, translucent lemon-slice decorations. Safe to re-run - any previous
        /// Background/BackgroundFruitDecor is replaced. Use this on a scene that was already built with
        /// Setup Scene, since that menu item refuses to re-run once BoardPanel exists.
        /// </summary>
        [MenuItem("Tools/Lemon Puzzle/Fresh Sky Background")]
        public static void ApplyFreshSkyBackground()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Play 모드에서는 씬을 편집할 수 없어요. 먼저 Play를 멈추고 다시 실행해주세요.", "확인");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("레몬 퍼즐", "Canvas를 찾을 수 없어요. 먼저 Setup Scene을 한 번 실행해주세요.", "확인");
                return;
            }

            var lemonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            CreateFreshBackground(canvas.transform, lemonSprite);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("레몬 퍼즐", "화사한 하늘색 배경과 레몬 장식을 추가했어요. Play로 확인해보세요 (Ctrl+S로 저장하는 것도 잊지 마세요).", "확인");
        }

        /// <summary>
        /// Builds (or rebuilds) the full-screen sky background and its scattered lemon decorations as
        /// the first two children of the canvas, so everything else draws on top of them. Falls back
        /// to a flat sky-blue color if the gradient texture isn't found/importable yet.
        /// </summary>
        static void CreateFreshBackground(Transform canvasTransform, Sprite lemonSprite)
        {
            var oldBg = canvasTransform.Find("Background");
            if (oldBg != null) Object.DestroyImmediate(oldBg.gameObject);
            var oldDecor = canvasTransform.Find("BackgroundFruitDecor");
            if (oldDecor != null) Object.DestroyImmediate(oldDecor.gameObject);

            AssetDatabase.Refresh();
            var skySprite = LoadSpriteEnsured(SkyBackgroundPath);

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvasTransform, false);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImage = bgGO.GetComponent<Image>();
            bgImage.raycastTarget = false;
            if (skySprite != null)
            {
                bgImage.sprite = skySprite;
                bgImage.type = Image.Type.Simple;
                bgImage.preserveAspect = false;
            }
            else
            {
                // SkyBackground.png missing/not imported yet - still land on a fresh, bright sky blue.
                bgImage.color = new Color(0.55f, 0.85f, 0.97f);
            }
            bgGO.transform.SetAsFirstSibling();

            var decorGO = new GameObject("BackgroundFruitDecor", typeof(RectTransform));
            decorGO.transform.SetParent(canvasTransform, false);
            var decorRT = (RectTransform)decorGO.transform;
            decorRT.anchorMin = Vector2.zero;
            decorRT.anchorMax = Vector2.one;
            decorRT.offsetMin = Vector2.zero;
            decorRT.offsetMax = Vector2.zero;
            decorGO.transform.SetSiblingIndex(1); // right after Background, still behind everything else

            if (lemonSprite != null)
            {
                foreach (var spot in FruitDecorSpots)
                {
                    var dot = new GameObject("FruitDot", typeof(RectTransform), typeof(Image));
                    dot.transform.SetParent(decorRT, false);
                    var dotRT = (RectTransform)dot.transform;
                    dotRT.anchorMin = new Vector2(spot.x, spot.y);
                    dotRT.anchorMax = new Vector2(spot.x, spot.y);
                    dotRT.pivot = new Vector2(0.5f, 0.5f);
                    dotRT.sizeDelta = new Vector2(spot.size, spot.size);
                    dotRT.anchoredPosition = Vector2.zero;
                    dotRT.localEulerAngles = new Vector3(0, 0, spot.rot);
                    var dotImg = dot.GetComponent<Image>();
                    dotImg.sprite = lemonSprite;
                    dotImg.preserveAspect = true;
                    dotImg.raycastTarget = false;
                    var c = Color.white;
                    c.a = spot.alpha;
                    dotImg.color = c;
                }
            }
        }

        /// <summary>
        /// Loads a sprite, fixing up its import settings first if needed (Texture Type: Sprite,
        /// Sprite Mode: Single) instead of requiring a manual Inspector step like Lemon.png originally
        /// did. Returns null if the file isn't on disk / importable yet (e.g. right after it was
        /// copied in but before the editor has refreshed).
        /// </summary>
        static Sprite LoadSpriteEnsured(string path)
        {
            if (!File.Exists(path)) return null;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// A container with top-left pivot (so BoardController's row/col pixel math can drive its
        /// anchoredPosition/sizeDelta directly) holding a light fill plus 4 solid border bars that
        /// stretch to match via anchors - a real marquee-style selection box, not just a faint tint.
        /// </summary>
        static RectTransform CreateSelectionIndicator(Transform parent)
        {
            var selGO = new GameObject("SelectionIndicator", typeof(RectTransform));
            selGO.transform.SetParent(parent, false);
            var selRT = (RectTransform)selGO.transform;
            selRT.anchorMin = new Vector2(0, 1);
            selRT.anchorMax = new Vector2(0, 1);
            selRT.pivot = new Vector2(0, 1);
            selRT.sizeDelta = new Vector2(50, 50);
            // The board panel also carries a GridLayoutGroup, which would otherwise fight for
            // control of this child's RectTransform every layout pass. Ignoring layout hands full
            // manual control back to BoardController's drag math.
            var layoutElement = selGO.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(selRT, false);
            var fillRT = (RectTransform)fillGO.transform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.color = new Color(1f, 0.35f, 0.3f, 0.18f);
            fillImg.raycastTarget = false;

            const float thickness = 5f;
            var borderColor = new Color(1f, 0.25f, 0.2f, 0.95f);
            CreateBorderBar(selRT, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, thickness), borderColor);
            CreateBorderBar(selRT, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, thickness), borderColor);
            CreateBorderBar(selRT, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(thickness, 0), borderColor);
            CreateBorderBar(selRT, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(thickness, 0), borderColor);

            selGO.SetActive(false);
            return selRT;
        }

        static void CreateBorderBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>Small top-left-anchored button (used for the in-game Reset button), styled like
        /// the overlay buttons - green fill, bold white label.</summary>
        static Button CreateSmallButton(string name, Transform parent, string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors((RectTransform)go.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPos, size);
            go.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.25f);
            var button = go.GetComponent<Button>();

            var text = CreateText("Text", go.transform, label, 22, TextAnchor.MiddleCenter);
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }

        static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = new Color(0.16f, 0.13f, 0.09f);
            return text;
        }

        static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        static LemonCellView CreateOrLoadCellPrefab(Sprite sprite)
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                var existingView = existing.GetComponent<LemonCellView>();
                if (existingView != null) return existingView;
            }

            var cellGO = new GameObject("LemonCell", typeof(RectTransform), typeof(Image));
            var image = cellGO.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;

            var textGO = new GameObject("Value", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(cellGO.transform, false);
            var text = textGO.GetComponent<Text>();
            text.text = "1";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            var textRT = (RectTransform)textGO.transform;
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var view = cellGO.AddComponent<LemonCellView>();
            AssignPrivateField(view, "lemonImage", image);
            AssignPrivateField(view, "valueText", text);

            var prefab = PrefabUtility.SaveAsPrefabAsset(cellGO, PrefabPath);
            Object.DestroyImmediate(cellGO);
            return prefab.GetComponent<LemonCellView>();
        }

        /// <summary>
        /// Fruits-box-style start screen: a full-screen light gingham background, a big lemon-shaped
        /// Play button on the left, a citrus-colored logo top-left, and a scatter of small numbered
        /// decorative lemons on the right - purely visual, not clickable.
        /// </summary>
        static GameObject CreateStartOverlay(Transform parent, Sprite lemonSprite, out Button startButton)
        {
            var root = new GameObject("StartOverlay", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            SetAnchors((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var bgSprite = LoadSpriteEnsured(StartBackgroundPath);
            var rootImage = root.GetComponent<Image>();
            if (bgSprite != null)
            {
                rootImage.sprite = bgSprite;
                rootImage.type = Image.Type.Simple;
                rootImage.preserveAspect = false;
            }
            else
            {
                // StartBackground.png missing/not imported yet - still land on a soft green tint.
                rootImage.color = new Color(0.93f, 0.97f, 0.88f);
            }

            var titleText = CreateText("StartTitle", root.transform, "레몬 게임", 60, TextAnchor.MiddleLeft);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.95f, 0.55f, 0.12f);
            SetAnchors(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60, -90), new Vector2(700, 100));

            // Scattered numbered lemons, purely decorative (no Button, no raycast).
            foreach (var spot in StartDecorSpots)
            {
                var dotGO = new GameObject("DecorLemon", typeof(RectTransform), typeof(Image));
                dotGO.transform.SetParent(root.transform, false);
                var dotRT = (RectTransform)dotGO.transform;
                dotRT.anchorMin = new Vector2(0.5f, 0.5f);
                dotRT.anchorMax = new Vector2(0.5f, 0.5f);
                dotRT.pivot = new Vector2(0.5f, 0.5f);
                dotRT.anchoredPosition = new Vector2(spot.x, spot.y);
                dotRT.sizeDelta = new Vector2(95, 95);
                var dotImg = dotGO.GetComponent<Image>();
                dotImg.sprite = lemonSprite;
                dotImg.preserveAspect = true;
                dotImg.raycastTarget = false;

                var numText = CreateText("Number", dotGO.transform, spot.num, 28, TextAnchor.MiddleCenter);
                numText.color = Color.black;
                numText.fontStyle = FontStyle.Bold;
                SetAnchors(numText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            // Big lemon Play button, left of center - the main call to action.
            var playGO = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            playGO.transform.SetParent(root.transform, false);
            var playRT = (RectTransform)playGO.transform;
            playRT.anchorMin = new Vector2(0.5f, 0.5f);
            playRT.anchorMax = new Vector2(0.5f, 0.5f);
            playRT.pivot = new Vector2(0.5f, 0.5f);
            playRT.anchoredPosition = new Vector2(-380, 30);
            playRT.sizeDelta = new Vector2(300, 300);
            var playImg = playGO.GetComponent<Image>();
            playImg.sprite = lemonSprite;
            playImg.preserveAspect = true;
            startButton = playGO.GetComponent<Button>();

            var playText = CreateText("PlayText", playGO.transform, "Play", 44, TextAnchor.MiddleCenter);
            playText.color = Color.white;
            playText.fontStyle = FontStyle.Bold;
            SetAnchors(playText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Short instructions, tucked near the bottom so the fruity look stays uncluttered.
            var bodyText = CreateText("BodyText", root.transform,
                "가운데 뜨는 목표 숫자와 합이 같도록 레몬을 드래그로 묶어서 지워보세요!", 20, TextAnchor.MiddleCenter);
            SetAnchors(bodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1100, 40));
            bodyText.color = new Color(0.2f, 0.35f, 0.15f);

            return root;
        }

        static GameObject CreateOverlay(string name, Transform parent, string title, string body, string buttonLabel,
            out Button button, out Text titleText, out Text bodyText, out Text scoreText)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            SetAnchors((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.05f, 0.6f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            SetAnchors((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 320));
            panel.GetComponent<Image>().color = new Color(1f, 0.98f, 0.93f, 1f);

            titleText = CreateText("TitleText", panel.transform, title, 26, TextAnchor.MiddleCenter);
            SetAnchors(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 50));

            bodyText = CreateText("BodyText", panel.transform, body, 16, TextAnchor.MiddleCenter);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetAnchors(bodyText.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.75f), Vector2.zero, Vector2.zero);

            scoreText = CreateText("ScoreText", panel.transform, "0", 40, TextAnchor.MiddleCenter);
            scoreText.fontStyle = FontStyle.Bold;
            SetAnchors(scoreText.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
            scoreText.gameObject.SetActive(name == "EndOverlay");

            var buttonGO = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(panel.transform, false);
            SetAnchors((RectTransform)buttonGO.transform, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(180, 56));
            buttonGO.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.25f);
            button = buttonGO.GetComponent<Button>();
            var buttonText = CreateText("Text", buttonGO.transform, buttonLabel, 20, TextAnchor.MiddleCenter);
            buttonText.color = Color.white;
            SetAnchors(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return root;
        }

        static void AssignPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }
    }
}
