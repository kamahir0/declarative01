using System.Linq;
using DeclarativePuzzle.Domain;

namespace DeclarativePuzzle.Presentation
{
    public abstract record PieceViewState(string PieceId)
    {
        public sealed record InStock(string PieceId) : PieceViewState(PieceId);
        public sealed record Held(string PieceId, Cell Origin, QuarterTurn Rotation) : PieceViewState(PieceId);
        public sealed record Placed(string PieceId, Cell Origin, QuarterTurn Rotation) : PieceViewState(PieceId);
    }

    public sealed record PuzzleViewState(ValueList<PieceViewState> Pieces, BoardAnalysis Board,
        PlacementPreview Preview, SubmissionPhase Phase, bool CanSubmit, bool CanManipulate,
        bool CanRestart, string HoldingId, string Message);

    public sealed record PieceRenderState(PieceViewState Placement, bool CanPick, ValueList<Cell> Overlaps);

    public static class ViewStateProjection
    {
        public static PieceRenderState Piece(PuzzleViewState state, PieceViewState piece)
            => new PieceRenderState(piece, state.Phase == SubmissionPhase.Playing && state.HoldingId == null,
                piece is PieceViewState.Held ? state.Preview.Overlaps : ValueList<Cell>.Empty);

        public static PuzzleViewState From(GameState state, string notice = "")
        {
            var held = state.Interaction as InteractionState.Holding;
            var pieces = new ValueList<PieceViewState>(state.Puzzle.Pieces.Select(p =>
            {
                if (held != null && held.PieceId == p.PieceId)
                    return (PieceViewState)new PieceViewState.Held(p.PieceId, held.Origin, held.Rotation);
                return p is PieceLocation.Placed placed
                    ? (PieceViewState)new PieceViewState.Placed(p.PieceId, placed.Origin, placed.Rotation)
                    : new PieceViewState.InStock(p.PieceId);
            }));
            var board = GameRules.Analyze(state);
            var preview = GameRules.Preview(state);
            string message;
            if (!string.IsNullOrEmpty(notice)) message = notice;
            else if (state.Phase == SubmissionPhase.Submitting) message = "配置を確認しています…";
            else if (state.Phase == SubmissionPhase.Celebrating) message = "すべてのピースが、ひとつに。";
            else if (state.Phase == SubmissionPhase.Finished) message = "完成！ もう一度、新しい組み合わせを。";
            else if (held != null) message = preview.Overlaps.Count > 0
                ? "重なっています。Setで仮置きして、あとで調整できます。" : "移動・回転して、Setで配置を確定。";
            else if (board.IsComplete) message = "ぴったり収まりました。Submitで答えを提出。";
            else if (board.OverlapCells > 0) message = "重なりのあるピースを持ち上げて、調整しましょう。";
            else message = "ピースを選んで、盤面に置いてみましょう。";
            return new PuzzleViewState(pieces, board, preview, state.Phase, GameRules.CanSubmit(state),
                GameRules.CanOperate(state) && held != null,
                state.Phase == SubmissionPhase.Playing || state.Phase == SubmissionPhase.Finished,
                held?.PieceId, message);
        }
    }
}
