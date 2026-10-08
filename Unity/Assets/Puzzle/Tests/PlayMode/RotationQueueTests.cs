using System.Collections;
using System.Linq;
using System.Reflection;
using DeclarativePuzzle.Application;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using DeclarativePuzzle.Unity;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DeclarativePuzzle.PlayModeTests
{
    public class RotationQueueTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        PuzzleView view;
        RotationPlayback playback;
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("PolyGrid"); yield return null;
            view = Object.FindAnyObjectByType<PuzzleView>();
            playback = (RotationPlayback)typeof(PuzzleView).GetField("rotations", Private).GetValue(view);
        }
        static void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        static RectTransform Visual(string id) => GameObject.Find("Piece_" + id).GetComponent<RectTransform>();
        static GameState State()
        {
            var entry = Object.FindAnyObjectByType<PuzzleEntryPoint>();
            var presenter = (PuzzlePresenter)typeof(PuzzleEntryPoint).GetField("presenter", Private).GetValue(entry);
            return ((IGameStateReader)typeof(PuzzlePresenter).GetField("reader", Private).GetValue(presenter)).Load();
        }
        Tween ActiveTween() => (Tween)typeof(RotationPlayback).GetField("tween", Private).GetValue(playback);
        void Advance(float seconds)
        {
            var tween = ActiveTween();
            Assert.That(tween, Is.Not.Null);
            Assert.That(tween.Duration(), Is.EqualTo(.2f).Within(.0001f));
            tween.SetUpdate(UpdateType.Manual);
            DOTween.ManualUpdate(seconds, seconds);
        }
        static void AssertPose(string id, GameState state)
        {
            var placement = ViewStateProjection.From(state).Pieces.Single(p => p.PieceId == id);
            var origin = placement is PieceViewState.Held h ? h.Origin : ((PieceViewState.Placed)placement).Origin;
            var rotation = placement is PieceViewState.Held held ? held.Rotation : ((PieceViewState.Placed)placement).Rotation;
            var board = GameObject.Find("Board").GetComponent<RectTransform>();
            var cellSize = board.rect.width / state.Definition.Width;
            var visual = Visual(id);
            Assert.That(Mathf.DeltaAngle(visual.localEulerAngles.z, 0), Is.EqualTo(0).Within(.01f));
            var actual = visual.Cast<Transform>().Where(t => t.name == "Tile").Select(t =>
            {
                var point = board.InverseTransformPoint(t.position);
                return new Cell(Mathf.FloorToInt((point.x - board.rect.xMin) / cellSize),
                    Mathf.FloorToInt((point.y - board.rect.yMin) / cellSize));
            });
            Assert.That(actual, Is.EquivalentTo(GameRules.Occupied(state.Definition, id, origin, rotation)));
        }

        [UnityTest] public IEnumerator MixedInputsPlayEveryStepForPointTwoSecondsAndMatchTheLatestState()
        {
            Click("PieceCard_A");
            var expected = State();
            var directions = new[] { RotationDirection.CounterClockwise, RotationDirection.Clockwise,
                RotationDirection.Clockwise, RotationDirection.CounterClockwise, RotationDirection.Clockwise };
            var snapshots = directions.Select(d => expected = GameRules.Spin(expected, d)).ToArray();
            foreach (var direction in directions) Click(direction == RotationDirection.Clockwise ? "Spin" : "SpinLeft");
            Assert.That(State(), Is.EqualTo(expected), "Logical state must advance without waiting for animation.");
            Assert.That(view.PendingRotations, Is.EqualTo(5));
            // Re-rendering the latest target must neither restart nor collapse the pending history.
            view.Render(ViewStateProjection.From(expected)); view.Render(ViewStateProjection.From(expected));
            for (var i = 0; i < directions.Length; i++)
            {
                Advance(.1f);
                var angle = Mathf.DeltaAngle(0, Visual("A").localEulerAngles.z);
                Assert.That(System.Math.Sign(angle), Is.EqualTo(-(int)directions[i]));
                Assert.That(view.PendingRotations, Is.EqualTo(directions.Length - i));
                Advance(.1f);
                Assert.That(view.PendingRotations, Is.EqualTo(directions.Length - i - 1));
                if (i < directions.Length - 1) AssertPose("A", snapshots[i]);
            }
            AssertPose("A", expected);
            yield return null;
        }

        [UnityTest] public IEnumerator FullTurnIsNotDiscardedEvenThoughTheFinalStateEqualsTheInitialState()
        {
            Click("PieceCard_A"); var original = State();
            for (var i = 0; i < 4; i++) Click("Spin");
            Assert.That(State(), Is.EqualTo(original)); Assert.That(view.PendingRotations, Is.EqualTo(4));
            for (var i = 0; i < 4; i++) { Advance(.2f); Assert.That(view.PendingRotations, Is.EqualTo(3 - i)); }
            AssertPose("A", original); yield return null;
        }

        [UnityTest] public IEnumerator QueueOrderIsGlobalAndMoveAndSetReconcileAfterAllAcceptedRotations()
        {
            Click("PieceCard_A"); Click("SpinLeft"); Click("Move3"); Click("Set");
            Click("PieceCard_B"); Click("Spin");
            var expected = State();
            Advance(.1f);
            Assert.That(Mathf.DeltaAngle(0, Visual("A").localEulerAngles.z), Is.GreaterThan(0));
            Assert.That(Visual("B").localEulerAngles.z, Is.EqualTo(0));
            Advance(.1f); AssertPose("A", expected);
            Advance(.1f);
            Assert.That(Mathf.DeltaAngle(0, Visual("B").localEulerAngles.z), Is.LessThan(0));
            Advance(.1f); AssertPose("B", expected);
            Assert.That(view.PendingRotations, Is.Zero); yield return null;
        }

        [UnityTest] public IEnumerator RestartAndEntryDisableCancelActiveAndPendingWorkWithoutLateCallbacks()
        {
            Click("PieceCard_A"); Click("Spin"); Click("SpinLeft"); Advance(.1f);
            var previous = ActiveTween(); Click("Restart");
            Assert.That(view.PendingRotations, Is.Zero); Assert.That(previous.IsActive(), Is.False);
            Click("PieceCard_A"); AssertPose("A", State());
            Click("Spin"); Click("Spin"); previous = ActiveTween();
            var root = GameObject.Find("Puzzle"); root.SetActive(false);
            Assert.That(view.PendingRotations, Is.Zero); Assert.That(previous.IsActive(), Is.False);
            yield return new WaitForSecondsRealtime(.5f);
            root.SetActive(true); yield return null;
            Click("PieceCard_A"); AssertPose("A", State()); Assert.That(view.PendingRotations, Is.Zero);
        }

        [UnityTest] public IEnumerator DestroyingAnActivePieceSkipsItsRemainingCuesAndContinuesWithTheNextPiece()
        {
            Click("PieceCard_A"); Click("Spin"); Click("SpinLeft"); Click("Set");
            Click("PieceCard_B"); Click("SpinLeft");
            var expected = State();
            Object.Destroy(GameObject.Find("PieceCard_A")); yield return null;
            Assert.That(view.PendingRotations, Is.EqualTo(1));
            Advance(.2f); AssertPose("B", expected); Assert.That(view.PendingRotations, Is.Zero);
            Object.Destroy(GameObject.Find("Puzzle")); yield return null;
            Assert.That(playback.PendingCount, Is.Zero);
        }

        [UnityTest] public IEnumerator KeyboardPressesWithinOneFrameKeepTheirOrderIncludingRepeatedRightTurns()
        {
            Click("PieceCard_A");
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.That(keyboard.enabled, Is.True);
                foreach (var key in new[] { Key.Q, Key.R, Key.R })
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                InputSystem.Update();
                Assert.That(view.PendingRotations, Is.EqualTo(3));
                Assert.That(((InteractionState.Holding)State().Interaction).Rotation, Is.EqualTo(QuarterTurn.East));
                Advance(.1f); Assert.That(Mathf.DeltaAngle(0, Visual("A").localEulerAngles.z), Is.GreaterThan(0));
                Advance(.1f); Advance(.1f);
                Assert.That(Mathf.DeltaAngle(0, Visual("A").localEulerAngles.z), Is.LessThan(0));
                Advance(.1f); Advance(.2f); AssertPose("A", State());
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ReturnedAndRepickedPieceDoesNotReplayCuesFromItsPreviousHoldingSession()
        {
            Click("PieceCard_A"); Click("Spin"); Click("Set");
            Click("PieceCard_B"); Click("SpinLeft"); Click("ReturnToStock");
            Click("PieceCard_B"); Click("Spin"); var expected = State();
            Advance(.2f);
            Assert.That(view.PendingRotations, Is.EqualTo(1));
            Advance(.1f);
            Assert.That(Mathf.DeltaAngle(0, Visual("B").localEulerAngles.z), Is.LessThan(0));
            Advance(.1f); AssertPose("B", expected); Assert.That(view.PendingRotations, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator DestroyingTheRootDuringPlaybackCancelsTweensAndReleasesDrainWaiters()
        {
            Click("PieceCard_A"); Click("Spin"); Click("SpinLeft"); Advance(.1f);
            var previous = ActiveTween();
            var waiting = playback.WaitAsync(System.Threading.CancellationToken.None);
            Assert.That(waiting.IsCompleted, Is.False);
            Object.Destroy(GameObject.Find("Puzzle")); yield return null;
            Assert.That(playback.PendingCount, Is.Zero); Assert.That(previous.IsActive(), Is.False);
            for (var i = 0; i < 10 && !waiting.IsCompleted; i++) yield return null;
            Assert.That(waiting.IsCompletedSuccessfully, Is.True);
            yield return new WaitForSecondsRealtime(.3f);
        }
    }
}
