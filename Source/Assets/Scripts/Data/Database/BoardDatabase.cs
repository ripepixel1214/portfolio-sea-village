using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "BoardDatabase", menuName = "SeaVillage/Data/Board Database")]
    public class BoardDatabase : ScriptableObject
    {
        [SerializeField] private List<BoardData> boards = new List<BoardData>();

        public IReadOnlyList<BoardData> Boards => boards;

        public List<BoardData> GetBoardsByTown(string town)
        {
            if (string.IsNullOrWhiteSpace(town))
                return new List<BoardData>();

            return boards
                .Where(board => string.Equals(board.Town, town, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public List<BoardData> GetStartBoardsByTown(string town)
        {
            if (string.IsNullOrWhiteSpace(town))
                return new List<BoardData>();

            return boards
                .Where(board => string.Equals(board.Town, town, StringComparison.OrdinalIgnoreCase) && board.StartQuest)
                .ToList();
        }

        public BoardData GetBoard(string town, int itemId)
        {
            return boards.FirstOrDefault(board =>
                string.Equals(board.Town, town, StringComparison.OrdinalIgnoreCase)
                && board.ItemID == itemId);
        }

        public void SetBoards(List<BoardData> newBoards)
        {
            boards = newBoards ?? new List<BoardData>();
        }

        public void ClearBoards()
        {
            boards.Clear();
        }
    }
}