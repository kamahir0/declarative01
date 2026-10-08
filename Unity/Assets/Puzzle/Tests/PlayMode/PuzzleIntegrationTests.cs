using System.Collections;
using System.Linq;
using DeclarativePuzzle.Domain;
using DeclarativePuzzle.Presentation;
using DeclarativePuzzle.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DeclarativePuzzle.PlayModeTests
{
    public class PuzzleIntegrationTests
    {
        [UnitySetUp] public IEnumerator LoadSample()
        {
            yield return SceneManager.LoadSceneAsync("PolyGrid");
            yield return null;
        }
        static UnityEngine.UI.Button Button(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>();
        static TMP_Text Label(string name) => GameObject.Find(name).GetComponent<TMP_Text>();
        static void Click(string name) => Button(name).onClick.Invoke();
        static PuzzleDefinition Definition() => Resources.FindObjectsOfTypeAll<PuzzleDefinitionAsset>()
            .Single(a => a.name == "SamplePuzzle").ToDomain();

        [UnityTest] public IEnumerator RealInputEventsAllowOverlapsAndReturnPlacedPiecesToStock()
        {
            Assert.That(Button("Set").interactable, Is.False);
            Click("PieceCard_A"); Assert.That(Button("Set").interactable, Is.True);
            Assert.That(Label("Holding").text, Does.Contain("A"));
            Click("Set"); Assert.That(Label("Coverage").text, Does.StartWith("4"));
            Click("PieceCard_G"); Assert.That(Label("Message").text, Does.Contain("重な"));
            Click("Set");
            Assert.That(Label("Coverage").text, Does.StartWith("6"));
            Assert.That(Label("OverlapCount").text, Is.EqualTo("重なり 2 マス"));
            Assert.That(Button("Submit").interactable, Is.False);
            Click("PieceCard_A"); Click("ReturnToStock");
            Assert.That(Label("Coverage").text, Does.StartWith("4"));
            Assert.That(Label("OverlapCount").text, Is.EqualTo("重なりなし"));
            Assert.That(Button("Set").interactable, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator RenderRecoversTheSamePoseFromDifferentDisplayHistories()
        {
            var definition = Definition(); var initial = GameState.Create(definition);
            var held = GameRules.Move(GameRules.PickUp(initial, "A"), new Cell(-1,0));
            var view = Object.FindAnyObjectByType<PuzzleView>();
            view.Render(ViewStateProjection.From(held)); yield return new WaitForSeconds(.25f);
            var visual = GameObject.Find("Piece_A").GetComponent<RectTransform>();
            var expected = visual.anchoredPosition;
            view.Render(ViewStateProjection.From(GameRules.Spin(held))); yield return new WaitForSeconds(.25f);
            view.Render(ViewStateProjection.From(initial));
            view.Render(ViewStateProjection.From(held)); yield return new WaitForSeconds(.25f);
            Assert.That(Vector2.Distance(visual.anchoredPosition, expected), Is.LessThan(.01f));
            Assert.That(visual.gameObject.activeSelf, Is.True);
            var image = visual.GetComponentInChildren<UnityEngine.UI.Image>();
            Assert.That(image.canvasRenderer.GetColor(), Is.EqualTo(Color.white), "Holding must keep the piece's full color.");
        }

        [UnityTest] public IEnumerator FullSubmissionDisablesInputThenCompletesCelebrationAndCanRestart()
        {
            SolveUsingButtons();
            Assert.That(Button("Submit").interactable, Is.True);
            Click("Submit"); Click("Submit");
            Assert.That(Label("Phase").text, Is.EqualTo("CHECKING…"));
            Assert.That(Button("Submit").interactable, Is.False); Assert.That(Button("Restart").interactable, Is.False);
            var deadline = Time.realtimeSinceStartup + 8;
            while (Label("Phase").text != "COMPLETE" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Label("Phase").text, Is.EqualTo("COMPLETE"));
            Assert.That(Label("Coverage").text, Does.StartWith("36"));
            Assert.That(Button("Submit").interactable, Is.False); Assert.That(Button("Restart").interactable, Is.True);
            Click("Restart");
            Assert.That(Label("Coverage").text, Does.StartWith("0")); Assert.That(Label("Phase").text, Is.EqualTo("MAKE IT FIT."));
            Assert.That(Button("PieceCard_A").interactable, Is.True);
        }

        [UnityTest] public IEnumerator DisablingTheEntryPointDuringSubmissionCancelsAndReinitializesCleanly()
        {
            SolveUsingButtons(); Click("Submit");
            var root = GameObject.Find("Puzzle"); root.SetActive(false);
            yield return new WaitForSeconds(.8f);
            root.SetActive(true); yield return null;
            Assert.That(Label("Coverage").text, Does.StartWith("0")); Assert.That(Label("Phase").text, Is.EqualTo("MAKE IT FIT."));
            Click("PieceCard_A"); Assert.That(Button("Set").interactable, Is.True);
        }

        [UnityTest] public IEnumerator MasterChangesRebuildRectangularBoardAndDifferentPieceCellCounts()
        {
            var original = Resources.FindObjectsOfTypeAll<PuzzleDefinitionAsset>().Single(a => a.name == "SamplePuzzle");
            var assets = original.Pieces;
            var triomino = ScriptableObject.CreateInstance<PieceDefinitionAsset>();
            triomino.Configure("A", "Triomino", assets[0].Tint,
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1) });
            var pentomino = ScriptableObject.CreateInstance<PieceDefinitionAsset>();
            pentomino.Configure("B", "Pentomino", assets[1].Tint,
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(0,3) });
            assets[0] = triomino; assets[1] = pentomino;
            var alternate = ScriptableObject.CreateInstance<PuzzleDefinitionAsset>();
            alternate.Configure(4, 9, assets);
            var root = GameObject.Find("Puzzle");
            var entry = root.GetComponent<PuzzleEntryPoint>();
            var view = root.GetComponentInChildren<PuzzleView>();
            try
            {
                root.SetActive(false); entry.Configure(alternate, view); root.SetActive(true);
                yield return null;
                var board = GameObject.Find("Board").GetComponent<RectTransform>();
                Assert.That(board.rect.height, Is.EqualTo(492).Within(.01f));
                Assert.That(board.rect.width, Is.EqualTo(492f * 4 / 9).Within(.01f));
                Assert.That(board.Find("Grid").childCount, Is.EqualTo(36));
                Assert.That(Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None).Length, Is.EqualTo(9));
                Click("PieceCard_A");
                Assert.That(GameObject.Find("Piece_A").GetComponentsInChildren<UnityEngine.UI.Button>().Length, Is.EqualTo(3));
                Click("Set"); Assert.That(Label("Coverage").text, Does.StartWith("3"));
                Click("PieceCard_B"); Click("Spin");
                var visual = GameObject.Find("Piece_B").GetComponent<RectTransform>();
                Assert.That(visual.GetComponentsInChildren<UnityEngine.UI.Button>().Length, Is.EqualTo(5));
                for (var i = 0; i < 12; i++) Click("Move3");
                yield return new WaitForSeconds(.3f);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(board, visual);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(board.rect.xMax + .01f));
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(board.rect.xMin - .01f));
            }
            finally
            {
                root.SetActive(false); entry.Configure(original, view); root.SetActive(true);
                Object.Destroy(alternate); Object.Destroy(triomino); Object.Destroy(pentomino);
            }
            yield return null;
        }

        static void SolveUsingButtons()
        {
            var model = GameState.Create(Definition());
            var poses = new[]
            {
                new PieceLocation.Placed("A",new Cell(0,0),QuarterTurn.North),
                new PieceLocation.Placed("B",new Cell(2,0),QuarterTurn.East),
                new PieceLocation.Placed("C",new Cell(4,0),QuarterTurn.East),
                new PieceLocation.Placed("D",new Cell(2,3),QuarterTurn.North),
                new PieceLocation.Placed("E",new Cell(2,1),QuarterTurn.East),
                new PieceLocation.Placed("F",new Cell(4,2),QuarterTurn.East),
                new PieceLocation.Placed("G",new Cell(0,4),QuarterTurn.North),
                new PieceLocation.Placed("H",new Cell(2,5),QuarterTurn.North),
                new PieceLocation.Placed("I",new Cell(0,1),QuarterTurn.South)
            };
            foreach (var pose in poses)
            {
                Click("PieceCard_" + pose.PieceId); model = GameRules.PickUp(model, pose.PieceId);
                for (var i = 0; i < (int)pose.Rotation; i++) { Click("Spin"); model = GameRules.Spin(model); }
                var held = (InteractionState.Holding)model.Interaction;
                for (var i = 0; held.Origin.X != pose.Origin.X && i < 16; i++)
                {
                    var right = held.Origin.X < pose.Origin.X; Click(right ? "Move3" : "Move0");
                    model = GameRules.Move(model, new Cell(right ? 1 : -1,0)); held = (InteractionState.Holding)model.Interaction;
                }
                for (var i = 0; held.Origin.Y != pose.Origin.Y && i < 16; i++)
                {
                    var up = held.Origin.Y < pose.Origin.Y; Click(up ? "Move1" : "Move2");
                    model = GameRules.Move(model, new Cell(0,up ? 1 : -1)); held = (InteractionState.Holding)model.Interaction;
                }
                Assert.That(held.Origin, Is.EqualTo(pose.Origin), pose.PieceId);
                Click("Set"); model = GameRules.Set(model);
            }
        }
    }
}
