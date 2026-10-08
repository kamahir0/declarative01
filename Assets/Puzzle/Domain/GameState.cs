using System.Linq;

namespace DeclarativePuzzle.Domain
{
    public abstract record PieceLocation(string PieceId)
    {
        public sealed record InStock(string PieceId) : PieceLocation(PieceId);
        public sealed record Placed(string PieceId, Cell Origin, QuarterTurn Rotation) : PieceLocation(PieceId);
    }

    public abstract record InteractionState
    {
        public sealed record Idle : InteractionState;
        public sealed record Holding(string PieceId, Cell Origin, QuarterTurn Rotation) : InteractionState;
    }

    public sealed record PuzzleState(ValueList<PieceLocation> Pieces)
    {
        public PieceLocation Piece(string id) => Pieces.First(p => p.PieceId == id);
        public PuzzleState Replace(PieceLocation piece) => new PuzzleState(
            new ValueList<PieceLocation>(Pieces.Select(p => p.PieceId == piece.PieceId ? piece : p)));
    }

    public sealed record GameState(PuzzleDefinition Definition, PuzzleState Puzzle,
        InteractionState Interaction, SubmissionPhase Phase)
    {
        public static GameState Create(PuzzleDefinition definition) => new GameState(definition,
            new PuzzleState(new ValueList<PieceLocation>(definition.Pieces.Select(p =>
                (PieceLocation)new PieceLocation.InStock(p.Id)))),
            new InteractionState.Idle(), SubmissionPhase.Playing);
    }

    public sealed record BoardAnalysis(int CoveredCells, int OverlapCells, int OutsideCells, int PlacedPieces,
        int TotalCells, int TotalPieces)
    {
        public bool IsComplete => CoveredCells == TotalCells && OverlapCells == 0 && OutsideCells == 0
            && PlacedPieces == TotalPieces;
    }

    public sealed record PlacementPreview(ValueList<Cell> Cells, ValueList<Cell> Overlaps);
}
