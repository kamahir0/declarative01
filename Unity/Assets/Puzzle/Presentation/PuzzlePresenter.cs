using System;
using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Application;
using DeclarativePuzzle.Domain;
using R3;

namespace DeclarativePuzzle.Presentation
{
    public abstract record PuzzleIntent
    {
        public sealed record PickUp(string PieceId) : PuzzleIntent;
        public sealed record Move(Cell Direction) : PuzzleIntent;
        public sealed record Spin(RotationDirection Direction = RotationDirection.Clockwise) : PuzzleIntent;
        public sealed record Set : PuzzleIntent;
        public sealed record ReturnToStock : PuzzleIntent;
        public sealed record Submit : PuzzleIntent;
        public sealed record Restart : PuzzleIntent;
    }
    public enum FeedbackKind { PickUp, Move, Spin, Set, Return, Blocked, Submit, Restart }

    public interface IPuzzleView
    {
        Observable<PuzzleIntent> Intents { get; }
        void Render(PuzzleViewState state);
        void PlayFeedback(FeedbackKind feedback);
        void QueueRotation(string pieceId, RotationDirection direction, PieceRenderState target);
        void CancelAnimations();
        Task PlayCelebrationAsync(ValueList<string> pieceIds, CancellationToken cancellationToken);
    }

    public sealed class PuzzlePresenter : IDisposable
    {
        readonly IGameStateReader reader;
        readonly PuzzleUseCases useCases;
        readonly IPuzzleView view;
        readonly Action<Exception> onError;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly IDisposable subscription;
        bool disposed;
        public PuzzlePresenter(IGameStateReader reader, PuzzleUseCases useCases, IPuzzleView view, Action<Exception> onError)
        {
            this.reader = reader; this.useCases = useCases; this.view = view; this.onError = onError;
            subscription = view.Intents.Subscribe(HandleEvent);
            Render();
        }
        void Render(string notice = "") { if (!disposed) view.Render(ViewStateProjection.From(reader.Load(), notice)); }
        async void HandleEvent(PuzzleIntent intent)
        {
            try { await HandleAsync(intent); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception exception) { onError?.Invoke(exception); }
        }

        public async Task HandleAsync(PuzzleIntent intent)
        {
            if (disposed) return;
            if (intent is PuzzleIntent.Submit)
            {
                var task = useCases.Submit.ExecuteAsync(lifetime.Token);
                Render();
                if (reader.Load().Phase == SubmissionPhase.Submitting) view.PlayFeedback(FeedbackKind.Submit);
                var result = await task;
                if (disposed) return;
                if (result.Kind == SubmissionResultKind.Accepted)
                {
                    Render();
                    try
                    {
                        var ids = new ValueList<string>(System.Linq.Enumerable.Select(reader.Load().Definition.Pieces, p => p.Id));
                        await view.PlayCelebrationAsync(ids, lifetime.Token);
                    }
                    finally
                    {
                        useCases.FinishCelebration.Execute();
                        Render();
                    }
                }
                else Render(result.Kind == SubmissionResultKind.Rejected || result.Kind == SubmissionResultKind.Failed
                    ? "提出できませんでした。もう一度お試しください。" : "");
                return;
            }
            // The application rules also enforce this; the presenter avoids playing feedback while locked.
            if (!GameRules.CanOperate(reader.Load()) && !(intent is PuzzleIntent.Restart)) return;
            var changed = false; var feedback = FeedbackKind.Blocked;
            switch (intent)
            {
                case PuzzleIntent.PickUp pick: changed = useCases.PickUp.Execute(pick.PieceId); feedback = FeedbackKind.PickUp; break;
                case PuzzleIntent.Move move: changed = useCases.Move.Execute(move.Direction); feedback = FeedbackKind.Move; break;
                case PuzzleIntent.Spin spin:
                    changed = useCases.Spin.Execute(spin.Direction); feedback = FeedbackKind.Spin;
                    if (changed)
                    {
                        var state = ViewStateProjection.From(reader.Load());
                        var piece = System.Linq.Enumerable.First(state.Pieces, p => p.PieceId == state.HoldingId);
                        view.QueueRotation(piece.PieceId, spin.Direction, ViewStateProjection.Piece(state, piece));
                    }
                    break;
                case PuzzleIntent.Set _: changed = useCases.Set.Execute(); feedback = FeedbackKind.Set; break;
                case PuzzleIntent.ReturnToStock _: changed = useCases.ReturnToStock.Execute(); feedback = FeedbackKind.Return; break;
                case PuzzleIntent.Restart _: changed = useCases.Restart.Execute(); feedback = FeedbackKind.Restart; break;
            }
            if (changed && intent is PuzzleIntent.Restart) view.CancelAnimations();
            Render();
            view.PlayFeedback(changed ? feedback : FeedbackKind.Blocked);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            subscription.Dispose();
            lifetime.Cancel();
            view.CancelAnimations();
            lifetime.Dispose();
        }
    }
}
