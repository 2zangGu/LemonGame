using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LemonPuzzle
{
    /// <summary>Binds GameManager's events to the HUD: score/timer/best, start &amp; game-over overlays, toasts.</summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Refs")]
        public GameManager gameManager;
        public TargetBadgeView targetBadge;

        [Header("HUD Texts")]
        public Text scoreText;
        public Text timerText;
        public Text bestScoreText;

        [Header("Overlays")]
        public GameObject startPanel;
        public GameObject endPanel;
        public Text endTitleText;
        public Text endSubText;
        public Text finalScoreText;
        public Button startButton;
        public Button restartButton;
        public Button resetButton;

        [Header("Toast")]
        public GameObject toastRoot;
        public Text toastText;
        public float toastDuration = 1.1f;

        const string BestScoreKey = "LemonPuzzle.Best";
        int best;
        Coroutine toastRoutine;

        void Awake()
        {
            best = PlayerPrefs.GetInt(BestScoreKey, 0);
            if (bestScoreText != null) bestScoreText.text = best.ToString();
            if (toastRoot != null) toastRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.OnScoreChanged += HandleScoreChanged;
            gameManager.OnTimeChanged += HandleTimeChanged;
            gameManager.OnTargetChanged += HandleTargetChanged;
            gameManager.OnGameStarted += HandleGameStarted;
            gameManager.OnGameOver += HandleGameOver;
            gameManager.OnCellsCleared += HandleCellsCleared;
            gameManager.OnSingleCellBlocked += HandleSingleCellBlocked;
            gameManager.OnBoardCleared += HandleBoardCleared;
        }

        void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnScoreChanged -= HandleScoreChanged;
            gameManager.OnTimeChanged -= HandleTimeChanged;
            gameManager.OnTargetChanged -= HandleTargetChanged;
            gameManager.OnGameStarted -= HandleGameStarted;
            gameManager.OnGameOver -= HandleGameOver;
            gameManager.OnCellsCleared -= HandleCellsCleared;
            gameManager.OnSingleCellBlocked -= HandleSingleCellBlocked;
            gameManager.OnBoardCleared -= HandleBoardCleared;
        }

        void Start()
        {
            if (startButton != null) startButton.onClick.AddListener(() => gameManager.StartGame());
            if (restartButton != null) restartButton.onClick.AddListener(() => gameManager.StartGame());
            // In-game reset - jumps straight back to a fresh board/score/timer without going through
            // the game-over overlay, same underlying call as Start/Restart.
            if (resetButton != null) resetButton.onClick.AddListener(() => gameManager.StartGame());
            if (startPanel != null) startPanel.SetActive(true);
            if (endPanel != null) endPanel.SetActive(false);
        }

        void HandleScoreChanged(int score)
        {
            if (scoreText != null) scoreText.text = score.ToString();
            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt(BestScoreKey, best);
                if (bestScoreText != null) bestScoreText.text = best.ToString();
            }
        }

        void HandleTimeChanged(float timeLeft)
        {
            if (timerText == null) return;
            int total = Mathf.CeilToInt(timeLeft);
            int mm = total / 60, ss = total % 60;
            timerText.text = $"{mm:00}:{ss:00}";
            timerText.color = timeLeft <= 15f ? new Color(0.8f, 0.15f, 0.15f) : new Color(0.16f, 0.13f, 0.09f);
        }

        void HandleTargetChanged(int target, bool hit)
        {
            if (targetBadge == null) return;
            targetBadge.SetTarget(target);
            if (hit) targetBadge.PlayHit(); else targetBadge.PlayMiss();
        }

        void HandleGameStarted()
        {
            if (startPanel != null) startPanel.SetActive(false);
            if (endPanel != null) endPanel.SetActive(false);
        }

        void HandleGameOver(string title)
        {
            if (endPanel == null) return;
            if (finalScoreText != null) finalScoreText.text = gameManager.Score.ToString();
            bool newBest = gameManager.Score > 0 && gameManager.Score >= best;
            if (endTitleText != null) endTitleText.text = title;
            if (endSubText != null) endSubText.text = newBest ? "최고 기록 경신! 다시 도전해볼까요?" : "최종 점수예요. 다시 도전해볼까요?";
            endPanel.SetActive(true);
        }

        void HandleCellsCleared(List<Vector2Int> cells, int gained)
        {
            // No "+N" popup on every clear anymore - it was overlapping the board's bottom rows
            // (the toast sits low on the canvas, which the tall board panel now reaches into).
            // Score is already visible live in ScoreText, so nothing else to show here.
        }

        void HandleSingleCellBlocked()
        {
            ShowToast("한 칸만으로는 지울 수 없어요");
        }

        void HandleBoardCleared()
        {
            // No bonus anymore - clearing the whole board is just the normal loop, so this is
            // purely a "nice job" moment, not a scored event.
            ShowToast("보드 클리어!");
        }

        public void ShowToast(string message)
        {
            if (toastRoot == null || toastText == null) return;
            toastText.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            toastRoot.SetActive(true);
            yield return new WaitForSeconds(toastDuration);
            toastRoot.SetActive(false);
        }
    }
}
