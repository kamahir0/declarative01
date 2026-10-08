using System;
using System.Collections.Generic;
using System.Linq;

namespace DeclarativePuzzle.Domain
{
    /// <summary>Pure calculations. These methods never read or write a repository.</summary>
    public static class GameRules
    {
        public static bool CanOperate(GameState state) => state.Phase == SubmissionPhase.Playing;
        public static bool CanSubmit(GameState state) => CanOperate(state)
            && state.Interaction is InteractionState.Idle && Analyze(state).IsComplete;

        // Shapes are normalized to their lower-left bounding box after each clockwise rotation.
        public static ValueList<Cell> Shape(PieceDefinition piece, QuarterTurn rotation)
        {
            var cells = piece.Cells.Select(c =>
            {
                var x = c.X; var y = c.Y;
                for (var i = 0; i < (int)rotation; i++) { var oldX = x; x = y; y = -oldX; }
                return new Cell(x, y);
            }).ToArray();
            var minX = cells.Min(c => c.X); var minY = cells.Min(c => c.Y);
            return new ValueList<Cell>(cells.Select(c => new Cell(c.X - minX, c.Y - minY))
                .OrderBy(c => c.Y).ThenBy(c => c.X));
        }

        public static ValueList<Cell> Occupied(PuzzleDefinition definition, string id, Cell origin, QuarterTurn rotation)
            => new ValueList<Cell>(Shape(definition.Piece(id), rotation).Select(c => c + origin));

        public static bool IsInside(PuzzleDefinition definition, Cell cell) => cell.X >= 0 && cell.Y >= 0
            && cell.X < definition.Width && cell.Y < definition.Height;

        static bool TryFit(PuzzleDefinition definition, string id, Cell origin, QuarterTurn rotation, out Cell fitted)
        {
            var shape = Shape(definition.Piece(id), rotation);
            var width = shape.Max(c => c.X) + 1; var height = shape.Max(c => c.Y) + 1;
            if (width > definition.Width || height > definition.Height) { fitted = origin; return false; }
            fitted = new Cell(Math.Max(0, Math.Min(origin.X, definition.Width - width)),
                Math.Max(0, Math.Min(origin.Y, definition.Height - height)));
            return true;
        }

        public static GameState PickUp(GameState state, string id)
        {
            if (!CanOperate(state) || !(state.Interaction is InteractionState.Idle)
                || !state.Definition.Pieces.Any(p => p.Id == id)) return state;
            Cell origin; QuarterTurn rotation;
            if (state.Puzzle.Piece(id) is PieceLocation.Placed placed)
            { origin = placed.Origin; rotation = placed.Rotation; }
            else
            {
                rotation = Enum.GetValues(typeof(QuarterTurn)).Cast<QuarterTurn>().First(r =>
                    Shape(state.Definition.Piece(id), r).All(c => c.X < state.Definition.Width && c.Y < state.Definition.Height));
                var shape = Shape(state.Definition.Piece(id), rotation);
                origin = new Cell((state.Definition.Width - shape.Max(c => c.X) - 1) / 2,
                    (state.Definition.Height - shape.Max(c => c.Y) - 1) / 2);
            }
            return state with { Interaction = new InteractionState.Holding(id, origin, rotation) };
        }

        public static GameState Move(GameState state, Cell direction)
        {
            if (!CanOperate(state) || !(state.Interaction is InteractionState.Holding held)
                || Math.Abs(direction.X) + Math.Abs(direction.Y) != 1) return state;
            var origin = held.Origin + direction;
            if (!Occupied(state.Definition, held.PieceId, origin, held.Rotation).All(c => IsInside(state.Definition, c))) return state;
            return state with { Interaction = held with { Origin = origin } };
        }

        public static GameState Spin(GameState state, RotationDirection direction = RotationDirection.Clockwise)
        {
            if (!CanOperate(state) || !(state.Interaction is InteractionState.Holding held)) return state;
            if (direction != RotationDirection.Clockwise && direction != RotationDirection.CounterClockwise) return state;
            var rotation = (QuarterTurn)(((int)held.Rotation + (int)direction + 4) % 4);
            if (!TryFit(state.Definition, held.PieceId, held.Origin, rotation, out var origin)) return state;
            return state with { Interaction = held with { Origin = origin, Rotation = rotation } };
        }

        public static GameState Set(GameState state)
        {
            if (!CanOperate(state) || !(state.Interaction is InteractionState.Holding held)) return state;
            if (!Occupied(state.Definition, held.PieceId, held.Origin, held.Rotation).All(c => IsInside(state.Definition, c))) return state;
            return state with { Puzzle = state.Puzzle.Replace(new PieceLocation.Placed(held.PieceId, held.Origin, held.Rotation)),
                Interaction = new InteractionState.Idle() };
        }

        public static GameState ReturnToStock(GameState state)
        {
            if (!CanOperate(state) || !(state.Interaction is InteractionState.Holding held)) return state;
            return state with { Puzzle = state.Puzzle.Replace(new PieceLocation.InStock(held.PieceId)),
                Interaction = new InteractionState.Idle() };
        }

        // Completion measures committed placements. A held piece keeps its original location here.
        public static BoardAnalysis Analyze(GameState state)
        {
            var occupancy = new Dictionary<Cell, int>(); var outside = 0; var placedCount = 0;
            foreach (var placed in state.Puzzle.Pieces.OfType<PieceLocation.Placed>())
            {
                placedCount++;
                foreach (var cell in Occupied(state.Definition, placed.PieceId, placed.Origin, placed.Rotation))
                {
                    if (!IsInside(state.Definition, cell)) { outside++; continue; }
                    occupancy[cell] = occupancy.TryGetValue(cell, out var count) ? count + 1 : 1;
                }
            }
            return new BoardAnalysis(occupancy.Count, occupancy.Count(p => p.Value > 1), outside, placedCount,
                state.Definition.Width * state.Definition.Height, state.Definition.Pieces.Count);
        }

        public static PlacementPreview Preview(GameState state)
        {
            if (!(state.Interaction is InteractionState.Holding held))
                return new PlacementPreview(ValueList<Cell>.Empty, ValueList<Cell>.Empty);
            var others = new HashSet<Cell>(state.Puzzle.Pieces.OfType<PieceLocation.Placed>()
                .Where(p => p.PieceId != held.PieceId)
                .SelectMany(p => Occupied(state.Definition, p.PieceId, p.Origin, p.Rotation)));
            var cells = Occupied(state.Definition, held.PieceId, held.Origin, held.Rotation);
            return new PlacementPreview(cells, new ValueList<Cell>(cells.Where(others.Contains)));
        }
    }
}
