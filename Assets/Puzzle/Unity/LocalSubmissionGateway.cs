using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Application;
using DeclarativePuzzle.Domain;

namespace DeclarativePuzzle.Unity
{
    /// <summary>Replace this port adapter with HTTP when a server is needed.</summary>
    public sealed class LocalSubmissionGateway : ISubmissionGateway
    {
        public async Task<SubmissionResponse> SubmitAsync(GameState snapshot, CancellationToken cancellationToken)
        {
            await Task.Delay(650, cancellationToken);
            return new SubmissionResponse(GameRules.CanSubmit(snapshot));
        }
    }
}
