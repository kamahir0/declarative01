using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using DeclarativePuzzle.Unity;

namespace DeclarativePuzzle.Editor
{
    public static class SampleProjectBuilder
    {
        public const string ScenePath = "Assets/Puzzle/Scenes/PolyGrid.unity";
        public const string MasterPath = "Assets/Puzzle/MasterData/SamplePuzzle.asset";
        public const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";

        [MenuItem("Tools/Declarative Puzzle/Create Sample Scene")]
        public static void CreateSampleScene()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before building a scene.");
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            CreateMaster();
            CreateFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unused assets. Resolve persistent objects after that boundary.
            var master = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionAsset>(MasterPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.957f, .945f, .914f);
            var entry = PuzzleSceneLayout.Build(master, font);
            EnsureFolder("Assets/Puzzle/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Shape Study"; PlayerSettings.productName = "POLY GRID";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            Selection.activeGameObject = entry.gameObject;
            AssetDatabase.SaveAssets();
            Debug.Log("[Declarative Puzzle] Sample scene and ScriptableObject masters created.");
        }

        public static PuzzleDefinitionAsset CreateMaster()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionAsset>(MasterPath);
            if (existing != null) return existing;
            EnsureFolder("Assets/Puzzle/MasterData/Pieces");
            var shapes = new[]
            {
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(0,2) },
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(1,2) },
                new[] { new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(1,0) },
                new[] { new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(1,0) },
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(2,1) },
                new[] { new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1) },
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) },
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0) },
                new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(0,2) }
            };
            var colors = new[] { "EBA275", "EBC467", "85BB9A", "7FAFCD", "B19BC6", "DF989C", "91BDAF", "A9B77C", "CCA27E" };
            var labels = new[] { "L", "J", "T", "T", "S", "Z", "O", "I", "L" };
            var pieces = new PieceDefinitionAsset[shapes.Length];
            for (var i = 0; i < pieces.Length; i++)
            {
                var id = ((char)('A' + i)).ToString();
                var piece = ScriptableObject.CreateInstance<PieceDefinitionAsset>();
                ColorUtility.TryParseHtmlString("#" + colors[i], out var color);
                piece.Configure(id, labels[i], color, shapes[i]);
                AssetDatabase.CreateAsset(piece, $"Assets/Puzzle/MasterData/Pieces/Piece_{id}.asset"); pieces[i] = piece;
            }
            var master = ScriptableObject.CreateInstance<PuzzleDefinitionAsset>(); master.Configure(6, 6, pieces);
            master.ToDomain(); // Validate authored data before persisting it.
            AssetDatabase.CreateAsset(master, MasterPath); return master;
        }

        public static TMP_FontAsset CreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKjp-Regular.otf");
            if (source == null) throw new FileNotFoundException("Noto Sans Japanese source font is missing.");
            var font = TMP_FontAsset.CreateFontAsset(source, 54, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "NotoSansJP SDF";
            // Prewarm every character used by the sample, plus ordinary ASCII.
            var characters = "";
            for (var i = 32; i < 127; i++) characters += (char)i;
            characters += "×↑↓←→ぴったりはまる。つの形をひと正方にクリックで持ち上げ重なっていてもここから選べます確定したマス未配置済み中戻回転提出完成立選択してください移動調整しましょう過不足確認しています…すべてピースがひとつもう一度新組合わせみ答え試SpaceEnterWASDSetSubmitR失敗できませんやり直";
            if (!font.TryAddCharacters(characters, out var missing)) Debug.LogWarning("Missing sample glyphs: " + missing);
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) if (texture != null) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.SaveAssets(); return font;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
