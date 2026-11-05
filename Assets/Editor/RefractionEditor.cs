using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Refraction.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Refraction.EditorTools
{
    /// <summary>Imports the original PNGs as 100-pixels-per-unit sprites (1 board cell = 1 world unit).</summary>
    public sealed class RefractionTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Sprites/") && !p.Contains("/Resources/Tutorials/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
        }
    }

    /// <summary>One-time project setup: main scene, build settings, player settings.</summary>
    [InitializeOnLoad]
    public static class RefractionProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static RefractionProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) || PlayerSettings.productName != "Refraction") Setup(false);
            };
        }

        [MenuItem("Refraction/Setup Project (scene + player settings)")]
        public static void SetupMenu() => Setup(true);

        static void Setup(bool verbose)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 6.4f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camGo.transform.position = new Vector3(0, 0, -10);
                camGo.AddComponent<AudioListener>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            PlayerSettings.productName = "Refraction";
            PlayerSettings.companyName = "Personal";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.personal.refraction");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.personal.refraction");
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Icon/icon.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            AssetDatabase.SaveAssets();
            if (EditorSceneManager.GetActiveScene().path != ScenePath && File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath);
            if (verbose) Debug.Log("Refraction: project setup done. Press Play.");
        }
    }

    public static class RefractionLevelTools
    {
        static IEnumerable<string> LevelFiles(string folder) =>
            Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json").OrderBy(f => f) : Enumerable.Empty<string>();

        [MenuItem("Refraction/Validate Custom Levels (with solver)")]
        public static void ValidateCustom() => Validate(LevelFiles("Assets/Resources/CustomLevels")
            .Concat(LevelFiles(Path.Combine(Application.persistentDataPath, "CustomLevels"))), true);

        [MenuItem("Refraction/Validate Original Levels (fast)")]
        public static void ValidateMain() => Validate(LevelFiles("Assets/Resources/Levels"), false);

        static void Validate(IEnumerable<string> files, bool solve)
        {
            var sb = new StringBuilder();
            int ok = 0, n = 0;
            foreach (var f in files)
            {
                n++;
                var errors = new List<string>();
                var lvl = Level.FromJson(File.ReadAllText(f), errors);
                LevelValidator.Report rep = null;
                if (lvl != null && errors.Count == 0) rep = LevelValidator.Validate(lvl, solve, new Solver.Options { MaxPieces = 4, NodeBudget = 150000 });
                bool good = errors.Count == 0 && rep != null && rep.Ok;
                if (good) ok++;
                sb.AppendLine($"{(good ? "OK  " : "FAIL")} {f}");
                foreach (var e in errors) sb.AppendLine("   ERROR: " + e);
                if (rep != null) foreach (var line in rep.ToString().Split('\n')) if (line.Length > 0) sb.AppendLine("   " + line);
            }
            string msg = $"Refraction: {ok}/{n} level(s) valid\n{sb}";
            if (ok == n) Debug.Log(msg); else Debug.LogWarning(msg);
        }

        [MenuItem("Refraction/Validate + Solve Selected Level")]
        public static void SolveSelected()
        {
            var ta = Selection.activeObject as TextAsset;
            if (ta == null) { Debug.LogWarning("Select a level .json file in the Project window first."); return; }
            var errors = new List<string>();
            var lvl = Level.FromJson(ta.text, errors);
            if (lvl == null) { Debug.LogWarning(string.Join("\n", errors)); return; }
            var rep = LevelValidator.Validate(lvl, false);
            var o = Solver.Solve(lvl, new Solver.Options { MaxPieces = 5, NodeBudget = 600000 });
            string sol = o.Solution == null ? $"no solution found ({o.Nodes} positions{(o.BudgetExhausted ? ", budget exhausted" : "")})"
                : $"solution ({o.Solution.Count} pieces): {Board.PiecesToJson(o.Solution)}\n{lvl.ToAscii(o.Solution)}";
            Debug.Log($"{ta.name}\n{rep}\n{sol}");
        }

        [MenuItem("Refraction/Open Device Custom Levels Folder")]
        public static void OpenFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, "CustomLevels");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("Refraction/Import Original-Format Level...")]
        public static void ImportLegacy()
        {
            string path = EditorUtility.OpenFilePanel("Original level file (e.g. level1 from the APK)", "", "");
            if (string.IsNullOrEmpty(path)) return;
            var lvl = Level.FromLegacy(File.ReadAllText(path), 0);
            lvl.Id = Path.GetFileNameWithoutExtension(path);
            lvl.Name = lvl.Id;
            Directory.CreateDirectory("Assets/Resources/CustomLevels");
            string outPath = $"Assets/Resources/CustomLevels/{lvl.Id}.json";
            File.WriteAllText(outPath, lvl.ToJson());
            AssetDatabase.Refresh();
            Debug.Log("Refraction: imported to " + outPath);
        }
    }
}
