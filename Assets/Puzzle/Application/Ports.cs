using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Domain;

namespace DeclarativePuzzle.Application
{
    public interface IGameStateReader { GameState Load(); }
    public interface IGameRepository : IGameStateReader { void Save(GameState state); }

    public sealed record SubmissionResponse(bool Accepted, string Message = "");
    public interface ISubmissionGateway
    {
        Task<SubmissionResponse> SubmitAsync(GameState snapshot, CancellationToken cancellationToken);
    }
}
