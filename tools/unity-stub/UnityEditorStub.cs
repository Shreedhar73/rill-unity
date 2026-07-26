// Signature-only stub of the UnityEditor surface the RILL editor tools use.
using System;
using UnityEngine;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
        public string menuItem;
        public bool validate;
        public int priority;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InitializeOnLoadMethod : Attribute { }

    public static class EditorApplication
    {
        public static event Action update;
        public static bool isPlaying { get { return false; } }
        public static void EnterPlaymode() { }
        public static void Exit(int returnValue) { }
        // Referenced so the compiler believes the event is used; the stub never runs.
        internal static void Never() { if (update != null) update(); }
    }

    public static class SessionState
    {
        public static bool GetBool(string key, bool defaultValue) { return defaultValue; }
        public static void SetBool(string key, bool value) { }
        public static int GetInt(string key, int defaultValue) { return defaultValue; }
        public static void SetInt(string key, int value) { }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public static class EditorUtility
    {
        public static void RevealInFinder(string path) { }
        public static bool DisplayDialog(string title, string message, string ok, string cancel) { return false; }
        public static bool DisplayDialog(string title, string message, string ok) { return false; }
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static void SaveAssets() { }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get { return ""; } }
        public string path { get { return ""; } }
        public bool IsValid() { return false; }
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new Scene(); }
        public static bool SaveScene(Scene scene, string path) { return false; }
        public static bool SaveScene(Scene scene) { return false; }
        public static Scene OpenScene(string scenePath) { return new Scene(); }
    }
}
