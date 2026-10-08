using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Application;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using NUnit.Framework;
using R3;

namespace DeclarativePuzzle.Tests
{
    public class UseCaseAndPresenterTests
    {
        sealed class Repository : IGameRepository
        {
            GameState state;
            public int Saves;
            public Repository(GameState initial) => state = initial;
            public GameState Load() => state;
            public void Save(GameState next) { state = next; Saves++; }
        }
        sealed class Gateway : ISubmissionGateway
        {
            public int Calls;
            public GameState Snapshot;
            public TaskCompletionSource<SubmissionResponse> Response = new TaskCompletionSource<SubmissionResponse>();
            public async Task<SubmissionResponse> SubmitAsync(GameState snapshot, CancellationToken cancellationToken)
            {
                Calls++; Snapshot = snapshot;
                using (cancellationToken.Register(() => Response.TrySetCanceled())) return await Response.Task;
            }
        }
        sealed class View : IPuzzleView, IDisposable
        {
            public Subject<PuzzleIntent> Inputs = new Subject<PuzzleIntent>();
            public Observable<PuzzleIntent> Intents => Inputs;
            public List<PuzzleViewState> Renders = new List<PuzzleViewState>();
            public TaskCompletionSource<bool> Celebration = new TaskCompletionSource<bool>();
            public int CelebrationCalls;
            public ValueList<string> GlowIds;
            public List<RotationDirection> Rotations = new List<RotationDirection>();
            public int Cancellations;
            public void QueueRotation(string pieceId, RotationDirection direction, PieceRenderState target) => Rotations.Add(direction);
            public void CancelAnimations() => Cancellations++;
            public void Render(PuzzleViewState state) => Renders.Add(state);
            public void PlayFeedback(FeedbackKind feedback) { }
            public async Task PlayCelebrationAsync(ValueList<string> ids, CancellationToken cancellationToken)
            {
                CelebrationCalls++; GlowIds = ids;
                using (cancellationToken.Register(() => Celebration.TrySetCanceled())) await Celebration.Task;
            }
            public void Dispose() => Inputs.Dispose();
        }

        [Test] public void UseCaseCommitsButRulesAloneDoNot()
        {
            var repository = new Repository(GameState.Create(GameRulesTests.Squares()));
            GameRules.PickUp(repository.Load(), "A"); Assert.That(repository.Saves, Is.Zero);
            new PickUpUseCase(repository).Execute("A"); Assert.That(repository.Saves, Is.EqualTo(1));
            Assert.That(repository.Load().Interaction, Is.TypeOf<InteractionState.Holding>());
        }
        [Test] public async Task PresenterCommitsEveryRotationAndQueuesItsDirectionBeforeRendering()
        {
            var repository = new Repository(GameRules.PickUp(GameState.Create(GameRulesTests.Squares()), "A"));
            using var view = new View();
            using var presenter = new PuzzlePresenter(repository, new PuzzleUseCases(repository, new Gateway()), view, e => Assert.Fail(e.ToString()));
            var directions = new[] { RotationDirection.CounterClockwise, RotationDirection.Clockwise, RotationDirection.Clockwise };
            foreach (var direction in directions) await presenter.HandleAsync(new PuzzleIntent.Spin(direction));
            Assert.That(repository.Saves, Is.EqualTo(3)); Assert.That(view.Rotations, Is.EqualTo(directions));
            Assert.That(((InteractionState.Holding)repository.Load().Interaction).Rotation, Is.EqualTo(QuarterTurn.East));
            await presenter.HandleAsync(new PuzzleIntent.Restart()); Assert.That(view.Cancellations, Is.EqualTo(1));
            await presenter.HandleAsync(new PuzzleIntent.Spin()); Assert.That(view.Rotations.Count, Is.EqualTo(3));
            presenter.Dispose(); Assert.That(view.Cancellations, Is.EqualTo(2));
        }
        [Test] public async Task IncompleteSubmissionDoesNotCallGateway()
        {
            var repository = new Repository(GameState.Create(GameRulesTests.Squares())); var gateway = new Gateway();
            var result = await new SubmitUseCase(repository, gateway).ExecuteAsync(CancellationToken.None);
            Assert.That(result.Kind, Is.EqualTo(SubmissionResultKind.Ignored)); Assert.That(gateway.Calls, Is.Zero);
            Assert.That(repository.Saves, Is.Zero);
        }
        [Test] public async Task SubmissionLocksSynchronouslyAndRejectsConcurrentOperations()
        {
            var repository = new Repository(GameRulesTests.Complete()); var gateway = new Gateway();
            var useCases = new PuzzleUseCases(repository, gateway);
            var first = useCases.Submit.ExecuteAsync(CancellationToken.None);
            Assert.That(repository.Load().Phase, Is.EqualTo(SubmissionPhase.Submitting));
            Assert.That(useCases.PickUp.Execute("A"), Is.False); Assert.That(useCases.Restart.Execute(), Is.False);
            var second = await useCases.Submit.ExecuteAsync(CancellationToken.None);
            Assert.That(second.Kind, Is.EqualTo(SubmissionResultKind.Ignored)); Assert.That(gateway.Calls, Is.EqualTo(1));
            Assert.That(gateway.Snapshot.Phase, Is.EqualTo(SubmissionPhase.Playing));
            gateway.Response.SetResult(new SubmissionResponse(true)); await first;
            Assert.That(repository.Load().Phase, Is.EqualTo(SubmissionPhase.Celebrating));
            Assert.That(useCases.PickUp.Execute("A"), Is.False);
            Assert.That(useCases.FinishCelebration.Execute(), Is.True);
            Assert.That(repository.Load().Phase, Is.EqualTo(SubmissionPhase.Finished));
            Assert.That(useCases.Restart.Execute(), Is.True); Assert.That(GameRules.Analyze(repository.Load()).PlacedPieces, Is.Zero);
        }
        [Test] public async Task RejectionRestoresTheExactPuzzleAndAllowsRetry()
        {
            var initial = GameRulesTests.Complete(); var repository = new Repository(initial); var gateway = new Gateway();
            var submit = new SubmitUseCase(repository, gateway); var task = submit.ExecuteAsync(CancellationToken.None);
            gateway.Response.SetResult(new SubmissionResponse(false, "try again")); var result = await task;
            Assert.That(result.Kind, Is.EqualTo(SubmissionResultKind.Rejected)); Assert.That(repository.Load(), Is.SameAs(initial));
            Assert.That(GameRules.CanSubmit(repository.Load()), Is.True);
        }
        [Test] public async Task GatewayFailureUnlocksThePuzzle()
        {
            var initial = GameRulesTests.Complete(); var repository = new Repository(initial); var gateway = new Gateway();
            var task = new SubmitUseCase(repository, gateway).ExecuteAsync(CancellationToken.None);
            gateway.Response.SetException(new InvalidOperationException("offline")); var result = await task;
            Assert.That(result.Kind, Is.EqualTo(SubmissionResultKind.Failed)); Assert.That(repository.Load(), Is.SameAs(initial));
        }
        [Test] public async Task CancellationUnlocksThePuzzle()
        {
            var initial = GameRulesTests.Complete(); var repository = new Repository(initial); var gateway = new Gateway();
            using var cancellation = new CancellationTokenSource();
            var task = new SubmitUseCase(repository, gateway).ExecuteAsync(cancellation.Token); cancellation.Cancel();
            var result = await task;
            Assert.That(result.Kind, Is.EqualTo(SubmissionResultKind.Cancelled)); Assert.That(repository.Load(), Is.SameAs(initial));
        }
        [Test] public async Task PresenterRendersEachSubmissionPhaseAndWaitsForCelebration()
        {
            var repository = new Repository(GameRulesTests.Complete()); var gateway = new Gateway(); using var view = new View();
            using var presenter = new PuzzlePresenter(repository, new PuzzleUseCases(repository, gateway), view, e => Assert.Fail(e.ToString()));
            var task = presenter.HandleAsync(new PuzzleIntent.Submit());
            Assert.That(view.Renders[view.Renders.Count - 1].Phase, Is.EqualTo(SubmissionPhase.Submitting));
            gateway.Response.SetResult(new SubmissionResponse(true));
            // Yield on the runner's synchronization context so the accepted continuation can render.
            for (var i = 0; i < 20 && view.CelebrationCalls == 0; i++) await Task.Yield();
            Assert.That(view.CelebrationCalls, Is.EqualTo(1)); Assert.That(view.GlowIds, Is.EqualTo(new ValueList<string>(new[] { "A", "B" })));
            Assert.That(view.Renders[view.Renders.Count - 1].Phase, Is.EqualTo(SubmissionPhase.Celebrating));
            Assert.That(view.Renders[view.Renders.Count - 1].CanManipulate, Is.False); Assert.That(task.IsCompleted, Is.False);
            view.Celebration.SetResult(true); await task;
            Assert.That(view.Renders[view.Renders.Count - 1].Phase, Is.EqualTo(SubmissionPhase.Finished));
        }
        [Test] public async Task DuplicatePresenterSubmissionNeverStartsAnotherCelebration()
        {
            var repository = new Repository(GameRulesTests.Complete()); var gateway = new Gateway(); using var view = new View();
            using var presenter = new PuzzlePresenter(repository, new PuzzleUseCases(repository, gateway), view, e => Assert.Fail(e.ToString()));
            var first = presenter.HandleAsync(new PuzzleIntent.Submit()); await presenter.HandleAsync(new PuzzleIntent.Submit());
            gateway.Response.SetResult(new SubmissionResponse(true)); view.Celebration.SetResult(true); await first;
            Assert.That(gateway.Calls, Is.EqualTo(1)); Assert.That(view.CelebrationCalls, Is.EqualTo(1));
        }
        [Test] public async Task PresenterDisposalCancelsSubmissionAndStopsRendering()
        {
            var repository = new Repository(GameRulesTests.Complete()); var gateway = new Gateway(); using var view = new View();
            var presenter = new PuzzlePresenter(repository, new PuzzleUseCases(repository, gateway), view, _ => { });
            var task = presenter.HandleAsync(new PuzzleIntent.Submit()); var count = view.Renders.Count; presenter.Dispose(); await task;
            Assert.That(view.Renders.Count, Is.EqualTo(count)); Assert.That(repository.Load().Phase, Is.EqualTo(SubmissionPhase.Playing));
            view.Inputs.OnNext(new PuzzleIntent.PickUp("A")); Assert.That(repository.Load().Interaction, Is.TypeOf<InteractionState.Idle>());
        }
        [Test] public async Task PresenterDisposalDuringCelebrationFinishesWithoutAnotherRender()
        {
            var repository = new Repository(GameRulesTests.Complete()); var gateway = new Gateway(); using var view = new View();
            var presenter = new PuzzlePresenter(repository, new PuzzleUseCases(repository, gateway), view, _ => { });
            var task = presenter.HandleAsync(new PuzzleIntent.Submit()); gateway.Response.SetResult(new SubmissionResponse(true));
            for (var i = 0; i < 20 && view.CelebrationCalls == 0; i++) await Task.Yield();
            var count = view.Renders.Count; presenter.Dispose();
            try { await task; } catch (OperationCanceledException) { }
            Assert.That(view.Renders.Count, Is.EqualTo(count)); Assert.That(repository.Load().Phase, Is.EqualTo(SubmissionPhase.Finished));
        }
    }
}
