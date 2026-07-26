using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Rill.App;
using Rill.Core;
using Rill.Meta;

namespace Rill.EditorTools
{
    /// <summary>
    /// Editor conveniences. The game builds itself at runtime, so these exist only to create the
    /// one-object scene, to look at a player's mountain, and to wipe a development save.
    /// </summary>
    public static class RillEditorTools
    {
        const string ScenePath = "Assets/Scenes/Rill.unity";

        [MenuItem("RILL/Build Scene", false, 0)]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("RILL");
            go.AddComponent<GameBootstrap>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            Debug.Log("[RILL] Scene written to " + ScenePath);
        }

        [MenuItem("RILL/Add Scene To Build Settings", false, 1)]
        public static void AddSceneToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == ScenePath) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("RILL/Open Save Folder", false, 20)]
        public static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(SaveSystem.RootDir + "/");
        }

        [MenuItem("RILL/Delete Saved Mountain (slot 0)", false, 21)]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("Delete saved mountain?",
                    "This erases a world that only exists because someone played it. There is no undo.",
                    "Delete", "Keep it"))
                return;

            SaveSystem.DeleteSlot(0);
            foreach (var f in Directory.GetFiles(SaveSystem.RootDir))
            {
                string name = Path.GetFileName(f);
                if (name.StartsWith("almanac_") || name.StartsWith("timelapse_") ||
                    name.StartsWith("confluence_queue_") || name == "daily.json")
                    File.Delete(f);
            }
            Debug.Log("[RILL] Save slot 0 cleared.");
        }

        [MenuItem("RILL/Preview Mountain Seed", false, 40)]
        public static void PreviewSeed()
        {
            uint seed = (uint)Random.Range(1, int.MaxValue);
            var settings = MountainGenerator.Settings.Default(seed);
            Vector2Int summit;
            System.Collections.Generic.List<SecretSite> secrets;
            var field = MountainGenerator.Generate(settings, out summit, out secrets);

            // Writes a quick top-down PNG so a designer can flick through seeds without playing.
            int n = field.Size;
            var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
            var bands = StrataPalette.For(settings.Biome);
            var pixels = new Color[n * n];
            for (int i = 0; i < n * n; i++)
            {
                float h = field.Height[i];
                Color c = h <= 0f ? StrataPalette.SeaColor : StrataPalette.ColorAt(bands, h);
                pixels[i] = c * Mathf.Clamp01(0.55f + h / settings.PeakHeight * 0.65f);
            }
            tex.SetPixels(pixels);
            tex.Apply();

            string path = Path.Combine(Application.dataPath, "..", "seed_" + seed + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[RILL] Seed " + seed + " preview written to " + Path.GetFullPath(path));
        }
    }
}
