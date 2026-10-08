using System.Linq;
using DeclarativePuzzle.Domain;
using UnityEngine;

namespace DeclarativePuzzle.Unity
{
    [CreateAssetMenu(menuName = "Declarative Puzzle/Piece")]
    public sealed class PieceDefinitionAsset : ScriptableObject
    {
        [SerializeField] string pieceId;
        [SerializeField] string displayName;
        [SerializeField] Color tint = Color.white;
        [SerializeField] Vector2Int[] cells;
        public string Id => pieceId;
        public string DisplayName => displayName;
        public Color Tint => tint;
        public PieceDefinition ToDomain() => new PieceDefinition(pieceId, cells.Select(c => new Cell(c.x, c.y)));
        public void Configure(string id, string label, Color color, Vector2Int[] shape)
        { pieceId = id; displayName = label; tint = color; cells = (Vector2Int[])shape.Clone(); }
    }
}
