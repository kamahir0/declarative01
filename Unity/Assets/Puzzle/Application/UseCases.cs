using System;
using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Domain;

namespace DeclarativePuzzle.Application
{
    public abstract class StateUseCase
    {
        protected readonly IGameRepository Repository;
        protected StateUseCase(IGameRepository repository) => Repository = repository;
        protected bool Commit(Func<GameState, GameState> transition)
        {
            var before = Repository.Load();
            var after = transition(before);
            if (ReferenceEquals(before, after) || before == after) return false;
            Repository.Save(after);
            return true;
        }
    }
    public sealed class PickUpUseCase : StateUseCase
    {
        public PickUpUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute(string id) => Commit(s => GameRules.PickUp(s, id));
    }
    public sealed class MoveUseCase : StateUseCase
    {
        public MoveUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute(Cell direction) => Commit(s => GameRules.Move(s, direction));
    }
    public sealed class SpinUseCase : StateUseCase
    {
        public SpinUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute(RotationDirection direction = RotationDirection.Clockwise) => Commit(s => GameRules.Spin(s, direction));
    }
    public sealed class SetUseCase : StateUseCase
    {
        public SetUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute() => Commit(GameRules.Set);
    }
    public sealed class ReturnToStockUseCase : StateUseCase
    {
        public ReturnToStockUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute() => Commit(GameRules.ReturnToStock);
    }
    public sealed class FinishCelebrationUseCase : StateUseCase
    {
        public FinishCelebrationUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute() => Commit(s => s.Phase == SubmissionPhase.Celebrating
            ? s with { Phase = SubmissionPhase.Finished } : s);
    }
    public sealed class RestartUseCase : StateUseCase
    {
        public RestartUseCase(IGameRepository repository) : base(repository) { }
        public bool Execute() => Commit(s => s.Phase == SubmissionPhase.Playing || s.Phase == SubmissionPhase.Finished
            ? GameState.Create(s.Definition) : s);
    }

    public enum SubmissionResultKind { Ignored, Accepted, Rejected, Cancelled, Failed }
    public sealed record SubmissionResult(SubmissionResultKind Kind, string Message = "");

    public sealed class SubmitUseCase
    {
        readonly IGameRepository repository;
        readonly ISubmissionGateway gateway;
        public SubmitUseCase(IGameRepository repository, ISubmissionGateway gateway)
        { this.repository = repository; this.gateway = gateway; }

        public async Task<SubmissionResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            var snapshot = repository.Load();
            if (!GameRules.CanSubmit(snapshot)) return new SubmissionResult(SubmissionResultKind.Ignored);
            // This happens before the first await: even same-frame repeated submissions are blocked.
            repository.Save(snapshot with { Phase = SubmissionPhase.Submitting });
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await gateway.SubmitAsync(snapshot, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!response.Accepted) return new SubmissionResult(SubmissionResultKind.Rejected, response.Message);
                repository.Save(snapshot with { Phase = SubmissionPhase.Celebrating });
                return new SubmissionResult(SubmissionResultKind.Accepted);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { return new SubmissionResult(SubmissionResultKind.Cancelled); }
            catch (Exception exception)
            { return new SubmissionResult(SubmissionResultKind.Failed, exception.Message); }
            finally
            {
                if (repository.Load().Phase == SubmissionPhase.Submitting) repository.Save(snapshot);
            }
        }
    }

    public sealed class PuzzleUseCases
    {
        public PickUpUseCase PickUp { get; }
        public MoveUseCase Move { get; }
        public SpinUseCase Spin { get; }
        public SetUseCase Set { get; }
        public ReturnToStockUseCase ReturnToStock { get; }
        public SubmitUseCase Submit { get; }
        public FinishCelebrationUseCase FinishCelebration { get; }
        public RestartUseCase Restart { get; }
        public PuzzleUseCases(IGameRepository repository, ISubmissionGateway gateway)
        {
            PickUp = new PickUpUseCase(repository); Move = new MoveUseCase(repository);
            Spin = new SpinUseCase(repository); Set = new SetUseCase(repository);
            ReturnToStock = new ReturnToStockUseCase(repository); Submit = new SubmitUseCase(repository, gateway);
            FinishCelebration = new FinishCelebrationUseCase(repository); Restart = new RestartUseCase(repository);
        }
    }
}
