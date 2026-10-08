using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeclarativePuzzle.Unity
{
    [Serializable]
    public sealed class PuzzleUiReferences
    {
        public TMP_FontAsset Font;
        public RectTransform Page, GridLayer, PieceLayer, MarkerLayer, ProgressTrack;
        public TMP_Text Subtitle;
        public RectTransform Board;
        public RectTransform CelebrationLayer;
        public PieceView[] Pieces;
        public UnityEngine.UI.Button[] MoveButtons;
        public UnityEngine.UI.Button Spin, Set, Return, Submit, Restart;
        public TMP_Text Coverage, PieceCount, Holding, Message, SubmitLabel, PhaseLabel, OverlapLabel;
        public TMP_Text[] ConflictMarkers;
        public RectTransform ProgressFill;
        public PuzzleAudio Audio;
        public float CellSize;
    }

    public sealed class PuzzleView : MonoBehaviour, IPuzzleView
    {
        [SerializeField] PuzzleUiReferences ui;
        Subject<PuzzleIntent> intents;
        CompositeDisposable subscriptions;
        PuzzleDefinition definition;
        PuzzleViewState current;
        string feedbackPiece;
        Tween shakeTween, progressTween;
        readonly System.Collections.Generic.List<GameObject> particles = new System.Collections.Generic.List<GameObject>();
        public Observable<PuzzleIntent> Intents => intents;
        public void Configure(PuzzleUiReferences references) => ui = references;
        static readonly Cell[] Directions = { new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1), new Cell(1, 0) };

        public void Initialize(PuzzleDefinitionAsset master)
        {
            definition = master.ToDomain(); current = null; feedbackPiece = null;
            PuzzleSceneLayout.RefreshMaster(ui, master);
            intents = new Subject<PuzzleIntent>(); subscriptions = new CompositeDisposable();
            var assets = master.Pieces;
            foreach (var piece in ui.Pieces)
                piece.Initialize(assets.First(a => a.Id == piece.Id), ui.CellSize, intents, subscriptions);
            for (var i = 0; i < ui.MoveButtons.Length; i++)
            {
                var direction = Directions[i];
                ui.MoveButtons[i].OnClickAsObservable().Subscribe(_ => intents.OnNext(new PuzzleIntent.Move(direction))).AddTo(subscriptions);
            }
            Bind(ui.Spin, new PuzzleIntent.Spin()); Bind(ui.Set, new PuzzleIntent.Set());
            Bind(ui.Return, new PuzzleIntent.ReturnToStock()); Bind(ui.Submit, new PuzzleIntent.Submit());
            Bind(ui.Restart, new PuzzleIntent.Restart());
            Observable.EveryUpdate().Subscribe(_ => ReadKeyboard()).AddTo(subscriptions);
        }
        void Bind(UnityEngine.UI.Button button, PuzzleIntent intent)
            => button.OnClickAsObservable().Subscribe(_ => intents.OnNext(intent)).AddTo(subscriptions);

        void ReadKeyboard()
        {
            var keys = Keyboard.current;
            if (keys == null || current == null) return;
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) intents.OnNext(new PuzzleIntent.Move(Directions[0]));
            if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) intents.OnNext(new PuzzleIntent.Move(Directions[1]));
            if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) intents.OnNext(new PuzzleIntent.Move(Directions[2]));
            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) intents.OnNext(new PuzzleIntent.Move(Directions[3]));
            if (keys.rKey.wasPressedThisFrame) intents.OnNext(new PuzzleIntent.Spin());
            if (keys.spaceKey.wasPressedThisFrame && current.CanManipulate) intents.OnNext(new PuzzleIntent.Set());
            if (keys.escapeKey.wasPressedThisFrame || keys.backspaceKey.wasPressedThisFrame) intents.OnNext(new PuzzleIntent.ReturnToStock());
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                intents.OnNext(current.CanManipulate ? (PuzzleIntent)new PuzzleIntent.Set() : new PuzzleIntent.Submit());
            for (var i = 0; i < Math.Min(9, ui.Pieces.Length); i++)
                if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) intents.OnNext(new PuzzleIntent.PickUp(ui.Pieces[i].Id));
        }

        public void Render(PuzzleViewState state)
        {
            if (state.HoldingId != null) feedbackPiece = state.HoldingId;
            foreach (var piece in state.Pieces)
                ui.Pieces.First(v => v.Id == piece.PieceId).Render(ViewStateProjection.Piece(state, piece));
            foreach (var button in ui.MoveButtons) button.interactable = state.CanManipulate;
            ui.Spin.interactable = ui.Set.interactable = ui.Return.interactable = state.CanManipulate;
            ui.Submit.interactable = state.CanSubmit; ui.Restart.interactable = state.CanRestart;
            ui.Coverage.text = $"{state.Board.CoveredCells}<size=22> / {state.Board.TotalCells}</size>";
            ui.PieceCount.text = $"{state.Board.PlacedPieces}<size=18> / {state.Board.TotalPieces} ピース</size>";
            ui.Holding.text = state.HoldingId == null ? "ピースを選択してください" : $"ピース {state.HoldingId} を移動中";
            ui.Message.text = state.Message;
            ui.Message.color = state.Board.OverlapCells > 0 || state.Preview.Overlaps.Count > 0 ? Palette.Coral : Palette.Ink;
            ui.OverlapLabel.text = state.Board.OverlapCells > 0 ? $"重なり {state.Board.OverlapCells} マス" : "重なりなし";
            ui.OverlapLabel.color = state.Board.OverlapCells > 0 ? Palette.Coral : Palette.Muted;
            ui.PhaseLabel.text = state.Phase == SubmissionPhase.Finished ? "COMPLETE" : state.Phase == SubmissionPhase.Celebrating
                ? "WELL DONE" : state.Phase == SubmissionPhase.Submitting ? "CHECKING…" : "MAKE IT FIT.";
            ui.SubmitLabel.text = state.Phase == SubmissionPhase.Submitting ? "確認中…" : state.Phase == SubmissionPhase.Finished
                ? "完成！" : state.Phase == SubmissionPhase.Celebrating ? "ぴったり！" : "Submit  /  提出";
            var overlap = state.Pieces.OfType<PieceViewState.Placed>()
                .SelectMany(p => GameRules.Occupied(definition, p.PieceId, p.Origin, p.Rotation))
                .GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).Concat(state.Preview.Overlaps).ToHashSet();
            for (var i = 0; i < ui.ConflictMarkers.Length; i++)
                ui.ConflictMarkers[i].gameObject.SetActive(overlap.Contains(new Cell(i % definition.Width, i / definition.Width)));
            if (current == null || current.Board.CoveredCells != state.Board.CoveredCells)
            {
                var target = new Vector2(ui.Board.rect.width * state.Board.CoveredCells / state.Board.TotalCells, 5);
                progressTween?.Kill();
                if (current == null) ui.ProgressFill.sizeDelta = target;
                else progressTween = DOTween.To(() => ui.ProgressFill.sizeDelta, s => ui.ProgressFill.sizeDelta = s, target, .25f)
                    .SetEase(Ease.OutCubic).SetLink(gameObject);
            }
            current = state;
        }

        public void PlayFeedback(FeedbackKind feedback)
        {
            var target = ui.Pieces.FirstOrDefault(p => p.Id == feedbackPiece);
            switch (feedback)
            {
                case FeedbackKind.PickUp: target?.Punch(.13f); ui.Audio.Play(72); break;
                case FeedbackKind.Move: target?.Punch(.035f); ui.Audio.Play(67, .045f); break;
                case FeedbackKind.Spin: target?.Punch(.11f); ui.Audio.Play(76, .09f); break;
                case FeedbackKind.Set: target?.Punch(.085f); ui.Audio.Play(60); break;
                case FeedbackKind.Return: ui.Audio.Play(55, .08f); break;
                case FeedbackKind.Blocked:
                    shakeTween?.Kill(); ui.Board.localRotation = Quaternion.identity;
                    shakeTween = ui.Board.DOPunchRotation(new Vector3(0, 0, 1.5f), .2f, 5, .35f)
                        .OnComplete(() => ui.Board.localRotation = Quaternion.identity).SetLink(gameObject);
                    ui.Audio.Play(43, .045f); break;
                case FeedbackKind.Submit: ui.Audio.Play(79, .09f); break;
                case FeedbackKind.Restart: ClearParticles(); ui.Audio.Play(64, .09f); break;
            }
        }

        public async Task PlayCelebrationAsync(ValueList<string> pieceIds, CancellationToken cancellationToken)
        {
            var notes = new[] { 60, 62, 64, 65, 67, 69, 71, 72, 76 };
            for (var i = 0; i < pieceIds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ui.Audio.Play(notes[i % notes.Length], .12f);
                await ui.Pieces.First(p => p.Id == pieceIds[i]).GlowAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            SpawnConfetti(); ui.Audio.Play(84, .12f);
            shakeTween?.Kill(); ui.Board.localRotation = Quaternion.identity; ui.Board.localScale = Vector3.one;
            shakeTween = ui.Board.DOPunchScale(new Vector3(.025f, .025f, 0), .45f, 4, .45f).SetLink(gameObject);
            await TweenAwaiter.CompleteAsync(shakeTween, cancellationToken);
        }

        void SpawnConfetti()
        {
            ClearParticles();
            var colors = new[] { Palette.Green, Palette.Coral, Palette.Hex("E7BC55"), Palette.Hex("6DA3D4") };
            for (var i = 0; i < 42; i++)
            {
                var particle = new GameObject("Confetti", typeof(RectTransform), typeof(RoundedImage));
                particle.transform.SetParent(ui.CelebrationLayer, false); particles.Add(particle);
                var rect = (RectTransform)particle.transform; rect.sizeDelta = new Vector2(7, 13);
                rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-640, 640), UnityEngine.Random.Range(300, 490));
                var image = particle.GetComponent<RoundedImage>(); image.color = colors[i % colors.Length]; image.Radius = 2; image.raycastTarget = false;
                var end = rect.anchoredPosition + new Vector2(UnityEngine.Random.Range(-150, 150), -950);
                DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, end, UnityEngine.Random.Range(1.4f, 2.5f))
                    .SetEase(Ease.InQuad).SetLink(particle).OnComplete(() => { particles.Remove(particle); Destroy(particle); });
                rect.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-600, 600)), 2.5f, RotateMode.FastBeyond360).SetLink(particle);
            }
        }
        void ClearParticles() { foreach (var particle in particles) if (particle != null) Destroy(particle); particles.Clear(); }
        public void Shutdown()
        {
            subscriptions?.Dispose(); subscriptions = null;
            intents?.Dispose(); intents = null;
            shakeTween?.Kill(); progressTween?.Kill(); ClearParticles();
            if (ui == null) return;
            foreach (var piece in ui.Pieces) if (piece != null) piece.StopAnimations();
            ui.Board.localRotation = Quaternion.identity; ui.Board.localScale = Vector3.one;
        }
    }
}
