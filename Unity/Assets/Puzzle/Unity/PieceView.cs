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

namespace DeclarativePuzzle.Unity
{
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] string pieceId;
        [SerializeField] RectTransform visual;
        [SerializeField] RectTransform[] tiles;
        [SerializeField] UnityEngine.UI.Image[] tileImages;
        [SerializeField] UnityEngine.UI.Image cardImage;
        [SerializeField] UnityEngine.UI.Button cardButton;
        [SerializeField] UnityEngine.UI.Button[] tileButtons;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text cardTitle;
        PieceDefinition definition;
        Color tint;
        float cellSize;
        Tween moveTween;
        Tween punchTween;
        Tween glowTween;
        PieceRenderState lastRender;
        PieceRenderState desiredRender;
        Tween rotationTween;
        int reservedRotations;
        internal int AnimationGeneration { get; private set; }
        public const float RotationSeconds = .2f;
        public bool CanPlayRotation => isActiveAndEnabled && visual != null && visual.gameObject.activeSelf;

        public string Id => pieceId;
        public void Configure(string id, RectTransform root, RectTransform[] cellRects, UnityEngine.UI.Image[] images,
            UnityEngine.UI.Image card, UnityEngine.UI.Button stockButton, UnityEngine.UI.Button[] buttons,
            TMP_Text statusLabel, TMP_Text titleLabel)
        {
            pieceId = id; visual = root; tiles = cellRects; tileImages = images; cardImage = card;
            cardButton = stockButton; tileButtons = buttons; status = statusLabel; cardTitle = titleLabel;
        }
        public void Initialize(PieceDefinitionAsset asset, float gridCellSize, Subject<PuzzleIntent> intents, CompositeDisposable subscriptions)
        {
            definition = asset.ToDomain(); tint = asset.Tint; cellSize = gridCellSize; lastRender = null;
            cardButton.OnClickAsObservable().Subscribe(_ => intents.OnNext(new PuzzleIntent.PickUp(pieceId))).AddTo(subscriptions);
            foreach (var button in tileButtons)
                button.OnClickAsObservable().Subscribe(_ => intents.OnNext(new PuzzleIntent.PickUp(pieceId))).AddTo(subscriptions);
        }

        /// <summary>Pose and visibility always come from this value, regardless of the preceding operations.</summary>
        public void Render(PieceRenderState state)
        {
            desiredRender = state;
            cardButton.interactable = state.CanPick;
            foreach (var button in tileButtons) button.interactable = state.CanPick;
            cardTitle.color = tint * new Color(.64f, .64f, .64f, 1);
            var placement = state.Placement;
            status.text = placement is PieceViewState.InStock ? "未配置" : placement is PieceViewState.Held ? "選択中" : "配置済み";
            status.color = placement is PieceViewState.Held ? Palette.Green : Palette.Muted;
            cardImage.color = placement is PieceViewState.Held ? Color.Lerp(Palette.Paper, tint, .19f) : Palette.Paper;
            // Update logical UI immediately; queued poses must remain visible in input order.
            if (placement is PieceViewState.InStock)
            {
                visual.gameObject.SetActive(false);
                StopAnimations(); desiredRender = state; lastRender = state;
                return;
            }
            if (reservedRotations > 0) return;
            if (lastRender == state) return;
            ApplyPose(state, true);
        }
        void ApplyPose(PieceRenderState state, bool animateMove)
        {
            var placement = state.Placement;
            var wasVisible = visual.gameObject.activeSelf;
            if (placement is PieceViewState.InStock)
            {
                moveTween?.Kill(); punchTween?.Kill(); glowTween?.Kill();
                visual.gameObject.SetActive(false); visual.localScale = Vector3.one; lastRender = state; return;
            }
            Cell origin; QuarterTurn rotation;
            if (placement is PieceViewState.Held held) { origin = held.Origin; rotation = held.Rotation; visual.SetAsLastSibling(); }
            else { var placed = (PieceViewState.Placed)placement; origin = placed.Origin; rotation = placed.Rotation; }
            var shape = GameRules.Shape(definition, rotation);
            var width = shape.Max(c => c.X) + 1; var height = shape.Max(c => c.Y) + 1;
            var target = new Vector2((origin.X + width * .5f) * cellSize, (origin.Y + height * .5f) * cellSize);
            visual.localRotation = Quaternion.identity;
            visual.sizeDelta = new Vector2(width, height) * cellSize;
            moveTween?.Kill();
            if (!animateMove || !wasVisible || !UnityEngine.Application.isPlaying) visual.anchoredPosition = target;
            else moveTween = DOTween.To(() => visual.anchoredPosition, p => visual.anchoredPosition = p, target, .12f)
                .SetEase(Ease.OutCubic).SetLink(gameObject);
            visual.gameObject.SetActive(true);
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].anchoredPosition = new Vector2(shape[i].X + .5f, shape[i].Y + .5f) * cellSize;
                var conflict = state.Overlaps.Contains(shape[i] + origin);
                tileImages[i].color = conflict ? Color.Lerp(tint, Palette.Coral, .55f) : tint;
                tileImages[i].GetComponent<UnityEngine.UI.Outline>().effectColor = placement is PieceViewState.Held
                    ? new Color(1, 1, 1, .95f) : new Color(0, 0, 0, .08f);
            }
            lastRender = state;
        }

        internal void ReserveRotation() => reservedRotations++;
        internal Tween StartRotation(RotationDirection direction, PieceRenderState target)
        {
            moveTween?.Complete(); punchTween?.Kill(); visual.localScale = Vector3.one;
            var held = (PieceViewState.Held)target.Placement;
            var shape = GameRules.Shape(definition, held.Rotation);
            var center = new Vector2(held.Origin.X + (shape.Max(c => c.X) + 1) * .5f,
                held.Origin.Y + (shape.Max(c => c.Y) + 1) * .5f) * cellSize;
            rotationTween = DOTween.Sequence()
                .Join(visual.DOLocalRotate(new Vector3(0, 0, -90 * (int)direction), RotationSeconds, RotateMode.FastBeyond360).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => visual.anchoredPosition, p => visual.anchoredPosition = p, center, RotationSeconds).SetEase(Ease.OutCubic))
                .SetUpdate(true).SetLink(gameObject);
            return rotationTween;
        }
        internal void CompleteRotation(PieceRenderState target, bool completed)
        {
            rotationTween = null;
            reservedRotations = Math.Max(0, reservedRotations - 1);
            if (!completed || !CanPlayRotation) return;
            ApplyPose(target, false); // Bake the quarter turn into canonical cell geometry.
            if (reservedRotations == 0 && desiredRender != null) ApplyPose(desiredRender, false);
        }

        public void Punch(float strength = .1f)
        {
            if (reservedRotations > 0) return;
            if (!visual.gameObject.activeSelf) return;
            punchTween?.Kill(); visual.localScale = Vector3.one;
            punchTween = visual.DOPunchScale(new Vector3(strength, -strength * .6f, 0), .22f, 5, .45f)
                .OnComplete(() => visual.localScale = Vector3.one).SetLink(gameObject);
        }
        public async Task GlowAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            moveTween?.Complete(); punchTween?.Kill(); visual.localScale = Vector3.one;
            glowTween?.Kill();
            var sequence = DOTween.Sequence().SetLink(gameObject);
            var start = tint;
            var light = Color.Lerp(tint, Color.white, .78f);
            foreach (var image in tileImages)
                sequence.Join(DOTween.To(() => image.color, c => image.color = c, light, .14f));
            sequence.Join(visual.DOScale(1.075f, .14f).SetEase(Ease.OutBack));
            sequence.AppendInterval(.06f);
            foreach (var image in tileImages)
                sequence.Insert(.2f, DOTween.To(() => image.color, c => image.color = c, start, .2f));
            sequence.Insert(.2f, visual.DOScale(1, .2f).SetEase(Ease.OutCubic));
            glowTween = sequence;
            await TweenAwaiter.CompleteAsync(sequence, cancellationToken);
        }
        public void StopAnimations()
        {
            AnimationGeneration++;
            reservedRotations = 0; desiredRender = null;
            rotationTween?.Kill(false); rotationTween = null;
            moveTween?.Kill(); punchTween?.Kill(); glowTween?.Kill();
            if (visual != null) { visual.localScale = Vector3.one; visual.localRotation = Quaternion.identity; }
            lastRender = null;
        }
        void OnDisable() => StopAnimations();
        void OnDestroy() => StopAnimations();
    }

    static class Palette
    {
        public static readonly Color Ink = Hex("213D34");
        public static readonly Color Muted = Hex("6D7B71");
        public static readonly Color Green = Hex("247C59");
        public static readonly Color Paper = Hex("FFFDFA");
        public static readonly Color Background = Hex("F4F1E9");
        public static readonly Color Line = Hex("D9DFD2");
        public static readonly Color Coral = Hex("EE7464");
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    }
}
