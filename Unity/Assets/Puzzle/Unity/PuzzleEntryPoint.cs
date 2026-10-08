using DeclarativePuzzle.Application;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using DG.Tweening;
using UnityEngine;

namespace DeclarativePuzzle.Unity
{
    public sealed class PuzzleEntryPoint : MonoBehaviour
    {
        [SerializeField] PuzzleDefinitionAsset definition;
        [SerializeField] PuzzleView view;
        PuzzlePresenter presenter;
        public void Configure(PuzzleDefinitionAsset master, PuzzleView puzzleView) { definition = master; view = puzzleView; }
        void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly).SetCapacity(256, 64);
            var repository = new InMemoryGameRepository(GameState.Create(definition.ToDomain()));
            var useCases = new PuzzleUseCases(repository, new LocalSubmissionGateway());
            view.Initialize(definition);
            presenter = new PuzzlePresenter(repository, useCases, view, Debug.LogException);
        }
        void OnDisable()
        {
            presenter?.Dispose();
            presenter = null;
            if (view != null) view.Shutdown();
        }
    }
}
