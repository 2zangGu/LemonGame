using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LemonPuzzle
{
    /// <summary>
    /// Pure data model for the puzzle grid - no Unity UI dependencies beyond UnityEngine.Random
    /// and Vector2Int, so it stays easy to reason about (and unit test) on its own.
    ///
    /// Mirrors the rules validated in the web prototype:
    ///  - random 1-9 grid
    ///  - a drag-selected rectangle clears when its non-empty cells sum to the current Target
    ///  - a clear needs at least MinClearCells apples (blocks the "single lemon" exploit)
    ///  - the next Target is always guaranteed to be reachable on the CURRENT board
    /// </summary>
    public class BoardModel
    {
        public readonly int Rows;
        public readonly int Cols;
        public int[,] Grid { get; private set; }

        public BoardModel(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            Grid = new int[rows, cols];
        }

        public void GenerateRandom(int minValue = 1, int maxValue = 9)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    Grid[r, c] = Random.Range(minValue, maxValue + 1); // inclusive
        }

        public int CountNonZero()
        {
            int n = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (Grid[r, c] != 0) n++;
            return n;
        }

        /// <summary>Non-empty cells inside the rectangle spanned by the two corners (inclusive, any order).</summary>
        public List<Vector2Int> GetNonEmptyCellsInRect(Vector2Int a, Vector2Int b)
        {
            int r0 = Mathf.Min(a.y, b.y), r1 = Mathf.Max(a.y, b.y);
            int c0 = Mathf.Min(a.x, b.x), c1 = Mathf.Max(a.x, b.x);
            var cells = new List<Vector2Int>();
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                    if (Grid[r, c] != 0) cells.Add(new Vector2Int(c, r));
            return cells;
        }

        /// <summary>Every remaining non-empty cell on the whole board (used to auto-clear a leftover
        /// that can no longer reach any in-range target).</summary>
        public List<Vector2Int> GetAllNonEmptyCells()
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (Grid[r, c] != 0) cells.Add(new Vector2Int(c, r));
            return cells;
        }

        public int SumOf(List<Vector2Int> cells)
        {
            int sum = 0;
            foreach (var cell in cells) sum += Grid[cell.y, cell.x];
            return sum;
        }

        public void ClearCells(List<Vector2Int> cells)
        {
            foreach (var cell in cells) Grid[cell.y, cell.x] = 0;
        }

        struct Prefix
        {
            public int[,] Sum;
            public int[,] Count;
        }

        Prefix BuildPrefix()
        {
            var sumP = new int[Rows + 1, Cols + 1];
            var cntP = new int[Rows + 1, Cols + 1];
            for (int r = 1; r <= Rows; r++)
            {
                for (int c = 1; c <= Cols; c++)
                {
                    int v = Grid[r - 1, c - 1];
                    sumP[r, c] = sumP[r - 1, c] + sumP[r, c - 1] - sumP[r - 1, c - 1] + v;
                    cntP[r, c] = cntP[r - 1, c] + cntP[r, c - 1] - cntP[r - 1, c - 1] + (v != 0 ? 1 : 0);
                }
            }
            return new Prefix { Sum = sumP, Count = cntP };
        }

        static int RectSum(Prefix p, int r0, int c0, int r1, int c1) =>
            p.Sum[r1 + 1, c1 + 1] - p.Sum[r0, c1 + 1] - p.Sum[r1 + 1, c0] + p.Sum[r0, c0];

        static int RectCount(Prefix p, int r0, int c0, int r1, int c1) =>
            p.Count[r1 + 1, c1 + 1] - p.Count[r0, c1 + 1] - p.Count[r1 + 1, c0] + p.Count[r0, c0];

        /// <summary>
        /// Every sum reachable by SOME rectangle covering at least minClearCells apples/lemons.
        /// O(rows^2 * cols^2) rectangles, each an O(1) prefix-sum lookup - a few thousand ops even
        /// at 17x10, so it's cheap enough to call every time a new target is needed.
        /// </summary>
        public HashSet<int> AchievableSums(int minClearCells)
        {
            var prefix = BuildPrefix();
            var set = new HashSet<int>();
            for (int r0 = 0; r0 < Rows; r0++)
                for (int r1 = r0; r1 < Rows; r1++)
                    for (int c0 = 0; c0 < Cols; c0++)
                        for (int c1 = c0; c1 < Cols; c1++)
                        {
                            if (RectCount(prefix, r0, c0, r1, c1) >= minClearCells)
                            {
                                int s = RectSum(prefix, r0, c0, r1, c1);
                                if (s > 0) set.Add(s);
                            }
                        }
            return set;
        }

        /// <summary>
        /// Picks a target that is guaranteed to be clearable on the current board. Prefers a value
        /// inside [minTarget, maxTarget]; if the board can no longer make anything in that band
        /// (e.g. only a couple of low-value lemons remain), falls back to whatever sum IS reachable
        /// so the player is never shown an impossible number.
        /// </summary>
        public int PickTarget(int prevTarget, int minTarget, int maxTarget, int minClearCells)
        {
            var achievable = AchievableSums(minClearCells);
            if (achievable.Count == 0) return prevTarget; // no legal move at all - caller should regenerate the board first

            var inRange = achievable.Where(s => s >= minTarget && s <= maxTarget && s != prevTarget).ToList();
            if (inRange.Count == 0) inRange = achievable.Where(s => s >= minTarget && s <= maxTarget).ToList();
            if (inRange.Count > 0) return inRange[Random.Range(0, inRange.Count)];

            var all = achievable.ToList();
            var noRepeat = all.Where(s => s != prevTarget).ToList();
            if (noRepeat.Count > 0) all = noRepeat;
            return all[Random.Range(0, all.Count)];
        }
    }
}
