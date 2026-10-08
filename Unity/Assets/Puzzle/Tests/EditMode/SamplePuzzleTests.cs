using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Unity;
using NUnit.Framework;
using UnityEditor;

namespace DeclarativePuzzle.Tests
{
    public class SamplePuzzleTests
    {
        [Test] public void AuthoredSampleHasACompleteSolution()
        {
            var master = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionAsset>("Assets/Puzzle/MasterData/SamplePuzzle.asset");
            Assert.That(master, Is.Not.Null);
            var state = GameState.Create(master.ToDomain());
            var placements = new[]
            {
                new PieceLocation.Placed("A", new Cell(0,0), QuarterTurn.North),
                new PieceLocation.Placed("B", new Cell(2,0), QuarterTurn.East),
                new PieceLocation.Placed("C", new Cell(4,0), QuarterTurn.East),
                new PieceLocation.Placed("I", new Cell(0,1), QuarterTurn.South),
                new PieceLocation.Placed("E", new Cell(2,1), QuarterTurn.East),
                new PieceLocation.Placed("F", new Cell(4,2), QuarterTurn.East),
                new PieceLocation.Placed("D", new Cell(2,3), QuarterTurn.North),
                new PieceLocation.Placed("G", new Cell(0,4), QuarterTurn.North),
                new PieceLocation.Placed("H", new Cell(2,5), QuarterTurn.North)
            };
            foreach (var placement in placements)
            {
                state = GameRules.PickUp(state, placement.PieceId);
                for (var spin = 0; spin < (int)placement.Rotation; spin++) state = GameRules.Spin(state);
                var held = (InteractionState.Holding)state.Interaction;
                for (var step = 0; held.Origin.X != placement.Origin.X && step < 16; step++)
                {
                    state = GameRules.Move(state, new Cell(held.Origin.X < placement.Origin.X ? 1 : -1, 0));
                    held = (InteractionState.Holding)state.Interaction;
                }
                for (var step = 0; held.Origin.Y != placement.Origin.Y && step < 16; step++)
                {
                    state = GameRules.Move(state, new Cell(0, held.Origin.Y < placement.Origin.Y ? 1 : -1));
                    held = (InteractionState.Holding)state.Interaction;
                }
                Assert.That(held.Origin, Is.EqualTo(placement.Origin), placement.PieceId);
                state = GameRules.Set(state);
            }
            Assert.That(GameRules.CanSubmit(state), Is.True); Assert.That(GameRules.Analyze(state).CoveredCells, Is.EqualTo(36));
        }
    }
}
