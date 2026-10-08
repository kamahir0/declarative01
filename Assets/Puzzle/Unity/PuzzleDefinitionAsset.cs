using System;
using System.Linq;
using DeclarativePuzzle.Domain;
using UnityEngine;

namespace DeclarativePuzzle.Unity
{
    [CreateAssetMenu(menuName = "Declarative Puzzle/Puzzle")]
    public sealed class PuzzleDefinitionAsset : ScriptableObject
    {
        [SerializeField, Min(1)] int width = 6;
        [SerializeField, Min(1)] int height = 6;
        [SerializeField] PieceDefinitionAsset[] pieces;
        public PieceDefinitionAsset[] Pieces => (PieceDefinitionAsset[])pieces.Clone();
        public PuzzleDefinition ToDomain()
        {
            if (pieces == null || pieces.Any(p => p == null)) throw new ArgumentException("Puzzle has a missing piece asset.");
            return new PuzzleDefinition(width, height, pieces.Select(p => p.ToDomain()));
        }
        public void Configure(int boardWidth, int boardHeight, PieceDefinitionAsset[] definitions)
        { width = boardWidth; height = boardHeight; pieces = (PieceDefinitionAsset[])definitions.Clone(); }
    }
}
