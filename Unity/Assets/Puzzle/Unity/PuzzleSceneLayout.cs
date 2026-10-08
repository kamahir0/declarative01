using System.Collections.Generic;
using System.Linq;
using DeclarativePuzzle.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DeclarativePuzzle.Unity
{
    /// <summary>Static scene construction and master-driven display topology.</summary>
    public static class PuzzleSceneLayout
    {
        public static PuzzleEntryPoint Build(PuzzleDefinitionAsset master, TMP_FontAsset font)
        {
            var definition = master.ToDomain();
            var rootObject = new GameObject("Puzzle", typeof(PuzzleEntryPoint));
            var entry = rootObject.GetComponent<PuzzleEntryPoint>();
            var canvasObject = new GameObject("PuzzleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(rootObject.transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var background = Fill("Background", canvasObject.transform);
            Image(background, Palette.Background, 0);
            var page = new GameObject("Page", typeof(RectTransform)).GetComponent<RectTransform>();
            page.SetParent(canvasObject.transform, false); page.sizeDelta = new Vector2(1440, 900);
            var view = page.gameObject.AddComponent<PuzzleView>();
            var audio = page.gameObject.AddComponent<PuzzleAudio>();
            page.GetComponent<AudioSource>().playOnAwake = false;
            Text("Edition", page, font, "SHAPE STUDY   /   01", 14, Palette.Green, 76, 37, 440, 26);
            Text("Title", page, font, "POLY / GRID", 60, Palette.Ink, 72, 64, 620, 84, true);
            var subtitle = Text("Subtitle", page, font, $"{definition.Pieces.Count}つの形を、ひとつの盤面に。", 20, Palette.Muted, 76, 151, 620, 30);
            var badge = Panel("Badge", page, Palette.Hex("E4EBDD"), 1228, 45, 140, 35, 17);
            Text("BadgeLabel", badge, font, "POLYOMINO", 12, Palette.Green, 0, 0, 140, 35, true, TextAlignmentOptions.Center);
            var phase = Text("Phase", page, font, "MAKE IT FIT.", 22, Palette.Green, 1030, 111, 338, 38, true, TextAlignmentOptions.Right);
            Panel("Divider", page, Palette.Line, 72, 192, 1296, 1, 0);

            var boardPanel = Panel("BoardPanel", page, Palette.Paper, 72, 214, 648, 592, 24, true);
            Text("BoardHeading", boardPanel, font, "BOARD / 01", 13, Palette.Muted, 28, 13, 200, 26, true);
            Text("CommittedHeading", boardPanel, font, "確定したマス", 12, Palette.Muted, 357, 12, 130, 26, false, TextAlignmentOptions.Right);
            var coverage = Text("Coverage", boardPanel, font, "0<size=22> / 36</size>", 30, Palette.Ink, 493, 5, 128, 40, true, TextAlignmentOptions.Right);
            var cellSize = Mathf.Min(492f / definition.Width, 492f / definition.Height);
            var boardWidth = cellSize * definition.Width; var boardHeight = cellSize * definition.Height;
            var board = Panel("Board", boardPanel, Palette.Hex("DCE5D8"), (648 - boardWidth) * .5f,
                64 + (492 - boardHeight) * .5f, boardWidth, boardHeight, 14);
            var gridLayer = Fill("Grid", board);
            var pieceLayer = Fill("Pieces", board);
            var markerLayer = Fill("OverlapMarkers", board);
            var markers = BuildBoard(definition, font, gridLayer, markerLayer, cellSize);
            var progressTrack = Panel("ProgressTrack", boardPanel, Palette.Line, (648 - boardWidth) * .5f, 568, boardWidth, 5, 2);
            var progress = Panel("ProgressFill", boardPanel, Palette.Green, (648 - boardWidth) * .5f, 568, 0, 5, 2);

            Text("RackHeading", page, font, "YOUR PIECES", 13, Palette.Muted, 768, 211, 240, 28, true);
            var count = Text("PieceCount", page, font, "0<size=18> / 9 ピース</size>", 26, Palette.Ink, 1094, 202, 274, 42, true, TextAlignmentOptions.Right);
            Text("RackDescription", page, font, "クリックで持ち上げる。重なっていても、ここから選べます。", 13, Palette.Muted, 768, 243, 600, 23);
            var pieces = BuildPieces(master, font, page, pieceLayer, cellSize);
            var holding = Text("Holding", page, font, "ピースを選択してください", 19, Palette.Ink, 768, 641, 340, 34, true);
            var overlap = Text("OverlapCount", page, font, "重なりなし", 13, Palette.Muted, 1144, 646, 224, 25, false, TextAlignmentOptions.Right);
            var arrows = new[] { "←", "↑", "↓", "→" };
            var moveButtons = new List<UnityEngine.UI.Button>();
            for (var i = 0; i < arrows.Length; i++)
                moveButtons.Add(ActionButton("Move" + i, page, font, arrows[i], 768 + i * 57, 689, 49, 49, Palette.Paper, Palette.Ink, out _));
            var spinLeft = ActionButton("SpinLeft", page, font, "左 / Q", 1008, 689, 90, 49, Palette.Paper, Palette.Ink, out _);
            var spin = ActionButton("Spin", page, font, "右 / R", 1110, 689, 90, 49, Palette.Paper, Palette.Ink, out _);
            var back = ActionButton("ReturnToStock", page, font, "戻す / Esc", 1212, 689, 156, 49, Palette.Paper, Palette.Ink, out _);
            var set = ActionButton("Set", page, font, "Set  /  配置", 768, 765, 192, 61, Palette.Green, Color.white, out _);
            var submit = ActionButton("Submit", page, font, "Submit  /  提出", 978, 765, 250, 61, Palette.Ink, Color.white, out var submitLabel);
            var restart = ActionButton("Restart", page, font, "やり直す", 1246, 765, 122, 61, Palette.Paper, Palette.Muted, out _);
            foreach (var button in moveButtons.Concat(new[] { spinLeft, spin, back, set, submit })) button.interactable = false;
            Text("KeyboardHelp", page, font, "↑↓←→ / WASD  移動    Q / R  左右回転    Space  Set    Esc  戻す    Enter  Submit", 11, Palette.Muted, 74, 822, 690, 28);
            Panel("StatusDot", page, Palette.Green, 74, 870, 7, 7, 4);
            var message = Text("Message", page, font, "ピースを選んで、盤面に置いてみましょう。", 17, Palette.Ink, 96, 858, 1272, 34);
            var celebration = Fill("Celebration", page);
            view.Configure(new PuzzleUiReferences
            {
                Font = font, Page = page, GridLayer = gridLayer, PieceLayer = pieceLayer, MarkerLayer = markerLayer,
                ProgressTrack = progressTrack, Subtitle = subtitle, Board = board, CelebrationLayer = celebration, Pieces = pieces, MoveButtons = moveButtons.ToArray(),
                Spin = spin, SpinLeft = spinLeft, Set = set, Return = back, Submit = submit, Restart = restart, Coverage = coverage, PieceCount = count,
                Holding = holding, Message = message, SubmitLabel = submitLabel, PhaseLabel = phase, OverlapLabel = overlap,
                ConflictMarkers = markers, ProgressFill = progress, Audio = audio, CellSize = cellSize
            });
            entry.Configure(master, view);
            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(rootObject.transform); eventObject.GetComponent<EventSystem>().sendNavigationEvents = false;
            eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return entry;
        }

        /// <summary>Reconcile static display topology once at initialization, from master data.</summary>
        public static void RefreshMaster(PuzzleUiReferences ui, PuzzleDefinitionAsset master)
        {
            var definition = master.ToDomain();
            foreach (var piece in ui.Pieces)
            {
                piece.StopAnimations();
                Remove(piece.gameObject);
            }
            Clear(ui.GridLayer); Clear(ui.PieceLayer); Clear(ui.MarkerLayer);
            ui.CellSize = Mathf.Min(492f / definition.Width, 492f / definition.Height);
            var width = ui.CellSize * definition.Width;
            var height = ui.CellSize * definition.Height;
            ui.Board.sizeDelta = new Vector2(width, height);
            ui.Board.anchoredPosition = new Vector2((648 - width) * .5f, -64 - (492 - height) * .5f);
            ui.ProgressTrack.anchoredPosition = new Vector2((648 - width) * .5f, -568);
            ui.ProgressTrack.sizeDelta = new Vector2(width, 5);
            ui.ProgressFill.anchoredPosition = ui.ProgressTrack.anchoredPosition;
            ui.ConflictMarkers = BuildBoard(definition, ui.Font, ui.GridLayer, ui.MarkerLayer, ui.CellSize);
            ui.Pieces = BuildPieces(master, ui.Font, ui.Page, ui.PieceLayer, ui.CellSize);
            ui.Subtitle.text = $"{definition.Pieces.Count}つの形を、ひとつの盤面に。";
        }
        static void Clear(RectTransform root)
        {
            foreach (var child in root.Cast<Transform>().ToArray()) Remove(child.gameObject);
        }
        static void Remove(GameObject gameObject)
        {
            gameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying) Object.Destroy(gameObject);
            else Object.DestroyImmediate(gameObject);
        }

        static TMP_Text[] BuildBoard(PuzzleDefinition definition, TMP_FontAsset font, RectTransform gridLayer,
            RectTransform markerLayer, float cellSize)
        {
            var gap = Mathf.Min(7, cellSize * .1f);
            for (var y = 0; y < definition.Height; y++)
                for (var x = 0; x < definition.Width; x++)
                {
                    var cell = BottomRect($"Cell_{x}_{y}", gridLayer, (x + .5f) * cellSize, (y + .5f) * cellSize, cellSize - gap, cellSize - gap);
                    Image(cell, Palette.Hex("ECF0E5"), 11);
                    var dot = BottomRect("Center", cell, (cellSize - gap) * .5f, (cellSize - gap) * .5f, 3, 3);
                    Image(dot, Palette.Hex("C6D3BF"), 2);
                }
            var markers = new List<TMP_Text>();
            for (var y = 0; y < definition.Height; y++)
                for (var x = 0; x < definition.Width; x++)
                {
                    var markerRect = BottomRect($"Conflict_{x}_{y}", markerLayer, (x + .5f) * cellSize, (y + .5f) * cellSize, 40, 40);
                    var marker = Label(markerRect, font, "×", 29, Palette.Ink, true, TextAlignmentOptions.Center);
                    marker.gameObject.SetActive(false); markers.Add(marker);
                }
            return markers.ToArray();
        }

        static PieceView[] BuildPieces(PuzzleDefinitionAsset master, TMP_FontAsset font, RectTransform page,
            RectTransform pieceLayer, float cellSize)
        {
            var pieces = new List<PieceView>();
            var assets = master.Pieces;
            var columns = assets.Length <= 9 ? 3 : 4;
            var cardWidth = (600f - (columns - 1) * 12) / columns;
            var rows = Mathf.CeilToInt(assets.Length / (float)columns);
            var rowScale = Mathf.Min(1, 336f / (rows * 104 + (rows - 1) * 12));
            var gap = Mathf.Min(7, cellSize * .1f);
            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i]; var shape = asset.ToDomain().Cells;
                var card = Panel("PieceCard_" + asset.Id, page, Palette.Paper, 768 + (i % columns) * (cardWidth + 12), 279 + (i / columns) * 116 * rowScale, cardWidth, 104, 15, true);
                card.localScale = new Vector3(1, rowScale, 1);
                var outline = card.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = Palette.Line; outline.effectDistance = new Vector2(1, -1);
                var stockButton = Button(card, card.GetComponent<UnityEngine.UI.Image>());
                var cardColors = stockButton.colors; cardColors.disabledColor = Color.white; stockButton.colors = cardColors;
                var title = Text("PieceId", card, font, asset.Id, 32, asset.Tint * new Color(.6f, .6f, .6f, 1), 16, 10, 45, 47, true);
                var status = Text("Location", card, font, "未配置", 12, Palette.Muted, 16, 75, 74, 22);
                Text("Shortcut", card, font, i < 9 ? (i + 1).ToString() : "", 10, Palette.Muted, cardWidth - 26, 8, 17, 21, false, TextAlignmentOptions.Right);
                var width = shape.Max(c => c.X) + 1; var height = shape.Max(c => c.Y) + 1;
                var mini = Mathf.Min(18, 61f / height, Mathf.Min(88, cardWidth - 86) / width);
                var miniX = cardWidth * .65f - width * mini * .5f; var miniY = 52 - height * mini * .5f;
                foreach (var cell in shape)
                    Panel("ThumbnailCell", card, asset.Tint, miniX + cell.X * mini, miniY + (height - cell.Y - 1) * mini, mini - 2.5f, mini - 2.5f, 4);
                var visual = new GameObject("Piece_" + asset.Id, typeof(RectTransform)).GetComponent<RectTransform>();
                visual.SetParent(pieceLayer, false); visual.anchorMin = visual.anchorMax = Vector2.zero; visual.pivot = Vector2.one * .5f;
                visual.sizeDelta = Vector2.zero;
                var tiles = new List<RectTransform>(); var images = new List<UnityEngine.UI.Image>(); var buttons = new List<UnityEngine.UI.Button>();
                foreach (var cell in shape)
                {
                    var tile = BottomRect("Tile", visual, (cell.X + .5f) * cellSize, (cell.Y + .5f) * cellSize, cellSize - gap, cellSize - gap);
                    var image = Image(tile, asset.Tint, 11); image.raycastTarget = true;
                    var stroke = tile.gameObject.AddComponent<UnityEngine.UI.Outline>(); stroke.effectDistance = new Vector2(2, -2); stroke.effectColor = new Color(0, 0, 0, .08f);
                    var tileButton = Button(tile, image);
                    tileButton.transition = UnityEngine.UI.Selectable.Transition.None;
                    buttons.Add(tileButton); tiles.Add(tile); images.Add(image);
                    Panel("TileShine", tile, new Color(1, 1, 1, .19f), 11, 9, Mathf.Max(1, cellSize - 29), 3, 1);
                }
                Label(Fill("PieceLetter", tiles[0]), font, asset.Id, Mathf.Min(24, cellSize * .3f), new Color(.08f, .16f, .12f, .72f), true, TextAlignmentOptions.Center);
                visual.gameObject.SetActive(false);
                var piece = card.gameObject.AddComponent<PieceView>();
                piece.Configure(asset.Id, visual, tiles.ToArray(), images.ToArray(), card.GetComponent<UnityEngine.UI.Image>(),
                    stockButton, buttons.ToArray(), status, title); pieces.Add(piece);
            }
            return pieces.ToArray();
        }

        static UnityEngine.UI.Button ActionButton(string name, Transform parent, TMP_FontAsset font, string text,
            float x, float y, float width, float height, Color background, Color foreground, out TMP_Text label)
        {
            var rect = Panel(name, parent, background, x, y, width, height, 13, true);
            var button = Button(rect, rect.GetComponent<UnityEngine.UI.Image>());
            rect.gameObject.AddComponent<ButtonJuice>();
            label = Label(Fill("Label", rect), font, text, 17, foreground, true, TextAlignmentOptions.Center);
            return button;
        }
        static UnityEngine.UI.Button Button(RectTransform rect, UnityEngine.UI.Image image)
        {
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
            colors.pressedColor = new Color(.87f, .87f, .87f, 1); colors.disabledColor = new Color(.75f, .75f, .75f, .42f);
            colors.fadeDuration = .12f; button.colors = colors;
            var navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.None; button.navigation = navigation;
            return button;
        }
        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        static RectTransform BottomRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = Rect(name, parent, 0, 0, width, height); rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        static RectTransform Fill(string name, Transform parent)
        {
            var rect = Rect(name, parent, 0, 0, 0, 0); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.pivot = Vector2.one * .5f; return rect;
        }
        static RoundedImage Image(RectTransform rect, Color color, float radius)
        {
            var image = rect.gameObject.AddComponent<RoundedImage>(); image.color = color; image.Radius = radius; image.raycastTarget = false; return image;
        }
        static RectTransform Panel(string name, Transform parent, Color color, float x, float y, float width, float height, float radius, bool shadow = false)
        {
            var rect = Rect(name, parent, x, y, width, height); Image(rect, color, radius);
            if (shadow)
            {
                var effect = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>(); effect.effectColor = new Color(.08f, .16f, .1f, .075f);
                effect.effectDistance = new Vector2(0, -4);
            }
            return rect;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
            float x, float y, float width, float height, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
            => Label(Rect(name, parent, x, y, width, height), font, text, size, color, bold, alignment);
        static TMP_Text Label(RectTransform rect, TMP_FontAsset font, string text, float size, Color color, bool bold, TextAlignmentOptions alignment)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
            label.fontSize = size; label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; label.color = color;
            label.alignment = alignment; label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Overflow; return label;
        }
    }
}
