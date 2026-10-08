using System;
using System.Linq;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using NUnit.Framework;

namespace DeclarativePuzzle.Tests
{
    public class GameRulesTests
    {
        internal static PuzzleDefinition Squares() => new PuzzleDefinition(4, 2, new[]
        {
            new PieceDefinition("A", new[] { new Cell(0,0), new Cell(1,0), new Cell(0,1), new Cell(1,1) }),
            new PieceDefinition("B", new[] { new Cell(0,0), new Cell(1,0), new Cell(0,1), new Cell(1,1) })
        });
        internal static GameState Complete()
        {
            var state = GameState.Create(Squares());
            state = GameRules.Move(GameRules.PickUp(state, "A"), new Cell(-1, 0));
            state = GameRules.Set(state);
            state = GameRules.Move(GameRules.PickUp(state, "B"), new Cell(1, 0));
            return GameRules.Set(state);
        }

        [Test] public void PieceAndCollectionsDefensivelyCopyTheirInputs()
        {
            var cells = new[] { new Cell(0,0), new Cell(1,0) };
            var piece = new PieceDefinition("A", cells); cells[0] = new Cell(99,99);
            Assert.That(piece.Cells[0], Is.EqualTo(new Cell(0,0)));
            var source = new[] { piece }; var list = new ValueList<PieceDefinition>(source); source[0] = null;
            Assert.That(list[0], Is.SameAs(piece));
        }
        [Test] public void StatesHaveStructuralValueEquality() => Assert.That(GameState.Create(Squares()), Is.EqualTo(GameState.Create(Squares())));
        [Test] public void InvalidMasterDataIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new PieceDefinition("A", new[] { new Cell(0,0), new Cell(2,0) }));
            Assert.Throws<ArgumentException>(() => new PieceDefinition("A", new[] { new Cell(0,0), new Cell(0,0) }));
            Assert.Throws<ArgumentException>(() => new PuzzleDefinition(3, 3, Squares().Pieces));
            Assert.Throws<ArgumentException>(() => new PuzzleDefinition(4, 2, new[] { Squares().Piece("A"), Squares().Piece("A") }));
        }
        [Test] public void HoldingStockDoesNotChangeItsLocation()
        {
            var before = GameState.Create(Squares()); var held = GameRules.PickUp(before, "A");
            Assert.That(held.Puzzle, Is.SameAs(before.Puzzle));
            Assert.That(held.Puzzle.Piece("A"), Is.TypeOf<PieceLocation.InStock>());
            Assert.That(held.Interaction, Is.TypeOf<InteractionState.Holding>());
        }
        [Test] public void HoldingPlacedPieceKeepsCommittedPoseUntilSet()
        {
            var before = Complete(); var state = GameRules.PickUp(before, "A");
            state = GameRules.Move(state, new Cell(1, 0)); state = GameRules.Spin(state);
            Assert.That(state.Puzzle, Is.SameAs(before.Puzzle));
            var after = GameRules.Set(state);
            Assert.That(((PieceLocation.Placed)after.Puzzle.Piece("A")).Origin, Is.EqualTo(new Cell(1,0)));
            Assert.That(before.Puzzle.Piece("A"), Is.EqualTo(new PieceLocation.Placed("A", new Cell(0,0), QuarterTurn.North)));
        }
        [Test] public void MovementIsOneCellAndCannotLeaveTheBoard()
        {
            var state = GameRules.PickUp(GameState.Create(Squares()), "A");
            state = GameRules.Move(state, new Cell(-1,0));
            Assert.That(GameRules.Move(state, new Cell(-1,0)), Is.SameAs(state));
            Assert.That(GameRules.Move(state, new Cell(1,1)), Is.SameAs(state));
            Assert.That(GameRules.Move(state, new Cell(2,0)), Is.SameAs(state));
        }
        [Test] public void SpinClampsOriginNearTheWall()
        {
            var definition = new PuzzleDefinition(3, 3, new[]
            {
                new PieceDefinition("L", new[] { new Cell(0,0), new Cell(1,0), new Cell(0,1), new Cell(0,2) }),
                new PieceDefinition("T", new[] { new Cell(0,0), new Cell(1,0), new Cell(2,0), new Cell(1,1) }),
                new PieceDefinition("dot", new[] { new Cell(0,0) })
            });
            var state = GameRules.Move(GameRules.PickUp(GameState.Create(definition), "L"), new Cell(1,0));
            state = GameRules.Spin(state); var held = (InteractionState.Holding)state.Interaction;
            Assert.That(held.Origin.X, Is.Zero); Assert.That(held.Rotation, Is.EqualTo(QuarterTurn.East));
            Assert.That(GameRules.Preview(state).Cells.All(c => GameRules.IsInside(definition, c)), Is.True);
        }
        [Test] public void RotationThatCannotFitARectangularBoardIsIgnored()
        {
            var line = new[] { new Cell(0,0), new Cell(1,0), new Cell(2,0), new Cell(3,0) };
            var definition = new PuzzleDefinition(4,2,new[] {new PieceDefinition("A",line),new PieceDefinition("B",line)});
            var state = GameRules.PickUp(GameState.Create(definition), "A");
            Assert.That(GameRules.Spin(state), Is.SameAs(state));
        }
        [Test] public void FourRotationsRecoverTheSameNormalizedShape()
        {
            var piece = new PieceDefinition("L", new[] { new Cell(0,0), new Cell(1,0), new Cell(0,1), new Cell(0,2) });
            var shape = piece;
            for (var i = 0; i < 4; i++) shape = new PieceDefinition("L", GameRules.Shape(shape, QuarterTurn.East));
            Assert.That(shape.Cells, Is.EqualTo(piece.Cells));
        }
        [Test] public void OverlappingSetIsAllowedButCannotBeSubmitted()
        {
            var state = GameRules.Set(GameRules.PickUp(GameState.Create(Squares()), "A"));
            state = GameRules.Set(GameRules.PickUp(state, "B"));
            var board = GameRules.Analyze(state);
            Assert.That(board.PlacedPieces, Is.EqualTo(2)); Assert.That(board.OverlapCells, Is.EqualTo(4));
            Assert.That(board.IsComplete, Is.False); Assert.That(GameRules.CanSubmit(state), Is.False);
        }
        [Test] public void PreviewExcludesTheHeldPiecesOriginalFootprint()
        {
            var state = GameRules.PickUp(Complete(), "A");
            Assert.That(GameRules.Preview(state).Overlaps.Count, Is.Zero);
            state = GameRules.Move(state, new Cell(1,0));
            Assert.That(GameRules.Preview(state).Overlaps.Count, Is.EqualTo(2));
        }
        [Test] public void ReturningPlacedPieceRemovesItsPlacement()
        {
            var state = GameRules.ReturnToStock(GameRules.PickUp(Complete(), "A"));
            Assert.That(state.Puzzle.Piece("A"), Is.TypeOf<PieceLocation.InStock>());
            Assert.That(state.Interaction, Is.TypeOf<InteractionState.Idle>());
            Assert.That(GameRules.Analyze(state).CoveredCells, Is.EqualTo(4));
        }
        [Test] public void CompleteBoardNeedsIdleToSubmit()
        {
            var complete = Complete(); Assert.That(GameRules.CanSubmit(complete), Is.True);
            var held = GameRules.PickUp(complete, "A");
            Assert.That(GameRules.Analyze(held).IsComplete, Is.True); Assert.That(GameRules.CanSubmit(held), Is.False);
        }
        [Test] public void OutsidePlacementCannotBeComplete()
        {
            var state = Complete();
            state = state with { Puzzle = state.Puzzle.Replace(new PieceLocation.Placed("A", new Cell(-1,0), QuarterTurn.North)) };
            Assert.That(GameRules.Analyze(state).OutsideCells, Is.EqualTo(2)); Assert.That(GameRules.CanSubmit(state), Is.False);
        }
        [TestCase(SubmissionPhase.Submitting)] [TestCase(SubmissionPhase.Celebrating)] [TestCase(SubmissionPhase.Finished)]
        public void LifecycleLocksEveryPuzzleOperation(SubmissionPhase phase)
        {
            var state = GameRules.PickUp(Complete(), "A") with { Phase = phase };
            Assert.That(GameRules.Move(state, new Cell(1,0)), Is.SameAs(state));
            Assert.That(GameRules.Spin(state), Is.SameAs(state)); Assert.That(GameRules.Set(state), Is.SameAs(state));
            Assert.That(GameRules.ReturnToStock(state), Is.SameAs(state)); Assert.That(GameRules.PickUp(state, "B"), Is.SameAs(state));
            Assert.That(GameRules.CanSubmit(state), Is.False);
        }
        [Test] public void ProjectionShowsOneHeldPieceWithoutDuplicatingItsCommittedLocation()
        {
            var state = GameRules.PickUp(Complete(), "A"); var view = ViewStateProjection.From(state);
            Assert.That(view.Pieces.OfType<PieceViewState.Held>().Count(), Is.EqualTo(1));
            Assert.That(view.Pieces.OfType<PieceViewState.Placed>().Count(), Is.EqualTo(1));
            Assert.That(view.CanSubmit, Is.False); Assert.That(view.CanManipulate, Is.True);
        }
        [Test] public void PurePreviewDoesNotChangeTheInput()
        {
            var state = GameRules.PickUp(Complete(), "A"); var copy = state with { };
            GameRules.Preview(state); GameRules.Analyze(state); ViewStateProjection.From(state); GameRules.Move(state, new Cell(1,0));
            Assert.That(state, Is.EqualTo(copy));
        }
        [Test] public void ArbitraryOperationSequencesPreserveBoardBoundsAndPieceIdentity()
        {
            var state = GameState.Create(Squares()); var random = new Random(913);
            for (var i = 0; i < 1000; i++)
            {
                var before = state;
                switch (random.Next(6))
                {
                    case 0: state = GameRules.PickUp(state, random.Next(2) == 0 ? "A" : "B"); break;
                    case 1: state = GameRules.Move(state, new[] {new Cell(1,0),new Cell(-1,0),new Cell(0,1),new Cell(0,-1)}[random.Next(4)]); break;
                    case 2: state = GameRules.Spin(state); break;
                    case 3: state = GameRules.Set(state); break;
                    case 4: state = GameRules.ReturnToStock(state); break;
                    case 5: GameRules.Preview(state); break;
                }
                Assert.That(state.Puzzle.Pieces.Select(p => p.PieceId), Is.EquivalentTo(new[] {"A","B"}));
                Assert.That(GameRules.Analyze(state).OutsideCells, Is.Zero);
                Assert.That(GameRules.Preview(state).Cells.All(c => GameRules.IsInside(state.Definition,c)), Is.True);
                if (before.Interaction is InteractionState.Holding && state.Interaction is InteractionState.Holding)
                    Assert.That(state.Puzzle, Is.SameAs(before.Puzzle));
            }
        }
    }
}
