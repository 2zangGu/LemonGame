using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LemonPuzzle
{
    /// <summary>
    /// Owns game state (score, timer, target, grid) and the rules for submitting a drag selection.
    /// Views subscribe to its events instead of polling - BoardController draws the grid/selection,
    /// HUDController drives score/timer/overlay text. This keeps the rules testable independent of UI.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Grid")]
        public int cols = 17;
        public int rows = 10;
        [Tooltip("드래그로 최소 이 개수 이상 레몬을 묶어야 지울 수 있어요 (1개짜리 선택으로 지우는 것 방지).")]
        public int minClearCells = 2;
        [Tooltip("각 칸에 채워지는 레몬 숫자의 범위 (난이도 조절용).")]
        public int minLemonValue = 1;
        public int maxLemonValue = 5;

        [Header("Target")]
        public int minTarget = 7;
        public int maxTarget = 12;

        [Header("Timer")]
        public float totalTimeSeconds = 180f;

        [Header("Scoring")]
        public int pointsPerLemon = 1;

        public BoardModel Model { get; private set; }
        public int Score { get; private set; }
        public float TimeLeft { get; private set; }
        public int Target { get; private set; }
        public bool IsRunning { get; private set; }

        public event Action OnBoardRebuilt;
        public event Action<int> OnScoreChanged;
        public event Action<float> OnTimeChanged;
        public event Action<int, bool> OnTargetChanged; // (target, wasHit) - drives the badge pop/shake
        public event Action OnGameStarted;
        public event Action<string> OnGameOver; // (title) - "타임 오버" vs "더 이상 지울 수 없음"
        public event Action<List<Vector2Int>, int> OnCellsCleared; // (cells, scoreGained)
        public event Action OnSingleCellBlocked;
        public event Action OnBoardCleared; // fired when the board empties out completely (no bonus - just a moment to celebrate)

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (!IsRunning) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                OnTimeChanged?.Invoke(TimeLeft);
                EndGame("타임 오버!");
                return;
            }
            OnTimeChanged?.Invoke(TimeLeft);
        }

        public void StartGame()
        {
            Model = new BoardModel(rows, cols);
            Model.GenerateRandom(minLemonValue, maxLemonValue);
            Score = 0;
            TimeLeft = totalTimeSeconds;
            Target = Model.PickTarget(0, minTarget, maxTarget, minClearCells);
            IsRunning = true;

            OnGameStarted?.Invoke();
            OnBoardRebuilt?.Invoke();
            OnScoreChanged?.Invoke(Score);
            OnTimeChanged?.Invoke(TimeLeft);
            OnTargetChanged?.Invoke(Target, true);
        }

        public class SelectionResult
        {
            public bool Success;
            public bool WasSingleCellAttempt;
            public bool BoardRegenerated;
            public int ScoreGained;
            public List<Vector2Int> ClearedCells = new List<Vector2Int>();
        }

        /// <summary>Called by BoardController when the player releases a drag selection.</summary>
        public SelectionResult SubmitSelection(List<Vector2Int> cells)
        {
            var result = new SelectionResult();
            if (!IsRunning || cells == null || cells.Count == 0) return result;

            int sum = Model.SumOf(cells);
            bool valid = cells.Count >= minClearCells && sum == Target;

            if (valid)
            {
                Model.ClearCells(cells);
                int gained = cells.Count * pointsPerLemon;
                Score += gained;

                result.Success = true;
                result.ScoreGained = gained;
                result.ClearedCells = cells;

                OnScoreChanged?.Invoke(Score);
                OnCellsCleared?.Invoke(cells, gained);

                // If what's left can never add up to anything in [minTarget, maxTarget] again (e.g.
                // a lone 1 and 2, which only sum to 3), auto-clear the rest right now. No score for
                // those cells - the player didn't actually match them to a target, so they shouldn't
                // count as a "real" clear.
                if (Model.CountNonZero() > 0 && !Model.AchievableSums(minClearCells).Any(s => s >= minTarget && s <= maxTarget))
                {
                    var leftover = Model.GetAllNonEmptyCells();
                    Model.ClearCells(leftover);
                    OnCellsCleared?.Invoke(leftover, 0);
                }

                if (Model.CountNonZero() == 0)
                {
                    // Board's fully empty now - either the player finished it themselves, or the
                    // auto-clear above just did. Either way: fresh board, fresh target, no bonus.
                    OnBoardCleared?.Invoke();
                    Model.GenerateRandom(minLemonValue, maxLemonValue);
                    result.BoardRegenerated = true;
                    OnBoardRebuilt?.Invoke();
                }

                Target = Model.PickTarget(Target, minTarget, maxTarget, minClearCells);
                OnTargetChanged?.Invoke(Target, true);
            }
            else
            {
                if (cells.Count == 1 && sum == Target) OnSingleCellBlocked?.Invoke();
                OnTargetChanged?.Invoke(Target, false);
            }

            return result;
        }

        void EndGame(string title)
        {
            IsRunning = false;
            OnGameOver?.Invoke(title);
        }
    }
}
