using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using DG.Tweening;

namespace DeclarativePuzzle.Unity
{
    /// <summary>One FIFO for the screen, including rotations of different pieces.</summary>
    public sealed class RotationPlayback
    {
        sealed record Cue(PieceView Piece, int Generation, RotationDirection Direction, PieceRenderState Target);
        readonly Queue<Cue> pending = new Queue<Cue>();
        Cue active;
        Tween tween;
        TaskCompletionSource<bool> drained;
        public int PendingCount => pending.Count + (active == null ? 0 : 1);

        public void Enqueue(PieceView piece, RotationDirection direction, PieceRenderState target)
        {
            if (PendingCount == 0) drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            piece.ReserveRotation();
            pending.Enqueue(new Cue(piece, piece.AnimationGeneration, direction, target));
            if (active == null) StartNext();
        }
        void StartNext()
        {
            while (pending.Count > 0)
            {
                var cue = pending.Dequeue();
                if (cue.Piece == null || cue.Piece.AnimationGeneration != cue.Generation || !cue.Piece.CanPlayRotation) continue;
                active = cue;
                tween = cue.Piece.StartRotation(cue.Direction, cue.Target);
                tween.OnComplete(() => Finish(cue, true));
                tween.OnKill(() => Finish(cue, false));
                return;
            }
            drained?.TrySetResult(true);
        }
        void Finish(Cue cue, bool completed)
        {
            // OnComplete is followed by auto-kill; cancellation also kills the tween.
            if (!ReferenceEquals(active, cue)) return;
            active = null; tween = null;
            if (cue.Piece != null) cue.Piece.CompleteRotation(cue.Target, completed);
            StartNext();
        }
        public void Cancel()
        {
            var pieces = new HashSet<PieceView>();
            if (active != null) pieces.Add(active.Piece);
            foreach (var cue in pending) pieces.Add(cue.Piece);
            active = null; pending.Clear();
            var previous = tween; tween = null;
            previous?.Kill(false);
            foreach (var piece in pieces) if (piece != null) piece.StopAnimations();
            drained?.TrySetResult(true);
        }
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PendingCount == 0) return;
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetCanceled()))
                await await Task.WhenAny(drained.Task, cancelled.Task);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
