using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;

namespace DeclarativePuzzle.Unity
{
    static class TweenAwaiter
    {
        public static async Task CompleteAsync(Tween tween, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var context = SynchronizationContext.Current;
            tween.OnComplete(() => completion.TrySetResult(true));
            tween.OnKill(() => completion.TrySetCanceled());
            using (cancellationToken.Register(() =>
            {
                // DOTween, like GameObjects, must only be touched on Unity's main thread.
                if (context != null) context.Post(_ => tween.Kill(), null);
                completion.TrySetCanceled();
            })) await completion.Task;
        }
    }
}
