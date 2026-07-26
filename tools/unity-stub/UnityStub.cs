// Signature-only stub of the Unity API surface RILL uses.
// Purpose: type-check the game code with a real C# compiler on a machine with no Unity install.
// Nothing here has behaviour — only shapes that must match Unity's.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return 0f; } }
        public float sqrMagnitude { get { return 0f; } }
        public Vector2 normalized { get { return this; } }
        public void Normalize() { }
        public static Vector2 zero { get { return new Vector2(); } }
        public static Vector2 one { get { return new Vector2(); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return a; }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return a; }
        public static Vector2 operator -(Vector2 a) { return a; }
        public static Vector2 operator *(Vector2 a, float b) { return a; }
        public static Vector2 operator *(float b, Vector2 a) { return a; }
        public static Vector2 operator /(Vector2 a, float b) { return a; }
        public static bool operator ==(Vector2 a, Vector2 b) { return false; }
        public static bool operator !=(Vector2 a, Vector2 b) { return false; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
        public static float Dot(Vector2 a, Vector2 b) { return 0f; }
        public static float Distance(Vector2 a, Vector2 b) { return 0f; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return a; }
        public static Vector2 ClampMagnitude(Vector2 a, float m) { return a; }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude { get { return 0f; } }
        public float sqrMagnitude { get { return 0f; } }
        public Vector3 normalized { get { return this; } }
        public void Normalize() { }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 one { get { return new Vector3(); } }
        public static Vector3 up { get { return new Vector3(); } }
        public static Vector3 right { get { return new Vector3(); } }
        public static Vector3 forward { get { return new Vector3(); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator -(Vector3 a) { return a; }
        public static Vector3 operator *(Vector3 a, float b) { return a; }
        public static Vector3 operator *(float b, Vector3 a) { return a; }
        public static Vector3 operator /(Vector3 a, float b) { return a; }
        public static bool operator ==(Vector3 a, Vector3 b) { return false; }
        public static bool operator !=(Vector3 a, Vector3 b) { return false; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a; }
        public static Vector3 ClampMagnitude(Vector3 a, float m) { return a; }
        public static float Dot(Vector3 a, Vector3 b) { return 0f; }
        public static implicit operator Vector3(Vector4 v) { return new Vector3(); }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(); }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static implicit operator Vector4(Vector3 v) { return new Vector4(); }
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public static Color white { get { return new Color(); } }
        public static Color black { get { return new Color(); } }
        public static Color clear { get { return new Color(); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
        public static Color operator *(Color a, float b) { return a; }
        public static Color operator *(Color a, Color b) { return a; }
        public static Color operator +(Color a, Color b) { return a; }
        public static implicit operator Color(Color32 c) { return new Color(); }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) { return new Color32(); }
    }

    public struct Quaternion
    {
        public static Quaternion identity { get { return new Quaternion(); } }
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
        public static Quaternion Euler(Vector3 v) { return new Quaternion(); }
        public static Quaternion LookRotation(Vector3 f, Vector3 up) { return new Quaternion(); }
        public static Quaternion LookRotation(Vector3 f) { return new Quaternion(); }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
    }

    public struct Matrix4x4
    {
        public static Matrix4x4 TRS(Vector3 pos, Quaternion rot, Vector3 scale) { return new Matrix4x4(); }
        public Vector4 GetColumn(int i) { return new Vector4(); }
        public static Matrix4x4 identity { get { return new Matrix4x4(); } }
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; }
    }

    public struct RectInt
    {
        public int x, y, width, height;
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Rad2Deg = 57.29578f;
        public const float Deg2Rad = 0.0174533f;
        public const float Infinity = float.PositiveInfinity;
        public static float Abs(float v) { return 0f; }
        public static int Abs(int v) { return 0; }
        public static float Sqrt(float v) { return 0f; }
        public static float Sin(float v) { return 0f; }
        public static float Cos(float v) { return 0f; }
        public static float Atan2(float a, float b) { return 0f; }
        public static float Pow(float a, float b) { return 0f; }
        public static float Exp(float a) { return 0f; }
        public static float Log(float a) { return 0f; }
        public static float Min(float a, float b) { return 0f; }
        public static int Min(int a, int b) { return 0; }
        public static float Max(float a, float b) { return 0f; }
        public static int Max(int a, int b) { return 0; }
        public static float Clamp(float v, float a, float b) { return 0f; }
        public static int Clamp(int v, int a, int b) { return 0; }
        public static float Clamp01(float v) { return 0f; }
        public static float Lerp(float a, float b, float t) { return 0f; }
        public static float SmoothStep(float a, float b, float t) { return 0f; }
        public static float MoveTowards(float a, float b, float d) { return 0f; }
        public static int RoundToInt(float v) { return 0; }
        public static int FloorToInt(float v) { return 0; }
        public static int CeilToInt(float v) { return 0; }
        public static float Repeat(float t, float len) { return 0f; }
        public static float Floor(float v) { return 0f; }
        public static float Ceil(float v) { return 0f; }
        public static float Sign(float v) { return 0f; }
    }

    public class Object
    {
        public string name;
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public static bool operator ==(Object a, object b) { return false; }
        public static bool operator !=(Object a, object b) { return false; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
    }

    public class Component : Object
    {
        public Transform transform { get { return null; } }
        public GameObject gameObject { get { return null; } }
        public T GetComponent<T>() where T : Component { return null; }
        public T AddComponent<T>() where T : Component, new() { return null; }
    }

    public class Behaviour : Component { public bool enabled; }

    public class MonoBehaviour : Behaviour
    {
        public void StartCoroutine(System.Collections.IEnumerator r) { }
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 localPosition { get; set; }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetParent(Transform p) { }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform { get { return null; } }
        public string tag;
        public int layer;
        public T AddComponent<T>() where T : Component { return null; }
        public Component AddComponent(Type t) { return null; }
        public T GetComponent<T>() where T : Component { return null; }
        public void SetActive(bool v) { }
        public bool activeSelf { get { return false; } }
    }

    public enum LightType { Directional, Point, Spot, Area }
    public enum LightShadows { None, Hard, Soft }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public LightShadows shadows { get; set; }
    }

    public enum CameraClearFlags { Skybox, Color, SolidColor, Depth, Nothing }

    public class Camera : Behaviour
    {
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public bool allowHDR { get; set; }
        public bool allowMSAA { get; set; }
        public Ray ScreenPointToRay(Vector3 p) { return new Ray(); }
        public Ray ScreenPointToRay(Vector2 p) { return new Ray(); }
        public static Camera main { get { return null; } }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) { return null; }
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public Material(Material m) { }
        public Color color { get; set; }
        public bool enableInstancing { get; set; }
        public void SetFloat(string n, float v) { }
        public void SetColor(string n, Color c) { }
    }

    public class Texture : Object { }

    public enum TextureFormat { RGB24, RGBA32, ARGB32 }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { }
        public Texture2D(int w, int h, TextureFormat f, bool mips) { }
        public void SetPixels(Color[] px) { }
        public void Apply() { }
        public byte[] EncodeToPNG() { return null; }
    }

    public class MaterialPropertyBlock { }

    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public Color32[] colors32 { get; set; }
        public Color[] colors { get; set; }
        public int[] triangles { get; set; }
        public Rendering.IndexFormat indexFormat { get; set; }
        public void MarkDynamic() { }
        public void Clear() { }
        public void SetVertices(List<Vector3> v) { }
        public void SetColors(List<Color32> c) { }
        public void SetColors(List<Color> c) { }
        public void SetTriangles(List<int> t, int sub) { }
        public void SetUVs(int ch, List<Vector2> uv) { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
    }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh { get; set; }
        public Mesh mesh { get; set; }
    }

    public class MeshRenderer : Renderer { }

    public static class Graphics
    {
        public static void DrawMeshInstanced(Mesh mesh, int submesh, Material mat, Matrix4x4[] matrices,
            int count, MaterialPropertyBlock props, Rendering.ShadowCastingMode shadows, bool receiveShadows) { }
        public static void DrawMeshInstanced(Mesh mesh, int submesh, Material mat, Matrix4x4[] matrices, int count) { }
    }

    public static class Time
    {
        public static float deltaTime { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0f; } }
        public static float time { get { return 0f; } }
        public static float unscaledTime { get { return 0f; } }
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch
    {
        public Vector2 position { get { return new Vector2(); } }
        public TouchPhase phase { get { return TouchPhase.Began; } }
        public int fingerId { get { return 0; } }
    }

    public static class Input
    {
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
        public static Vector3 mousePosition { get { return new Vector3(); } }
        public static int touchCount { get { return 0; } }
        public static Touch GetTouch(int i) { return new Touch(); }
    }

    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
    }

    public enum SleepTimeout2 { }
    public static class SleepTimeout
    {
        public static int SystemSetting { get { return 0; } }
        public static int NeverSleep { get { return 0; } }
    }

    public static class Screen
    {
        public static int sleepTimeout { get; set; }
        public static int width { get { return 0; } }
        public static int height { get { return 0; } }
    }

    public static class Application
    {
        public static string persistentDataPath { get { return ""; } }
        public static string dataPath { get { return ""; } }
        public static int targetFrameRate { get; set; }
        public static bool isEditor { get { return false; } }
    }

    public static class QualitySettings
    {
        public static int vSyncCount { get; set; }
    }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientLight { get; set; }
        public static bool fog { get; set; }
    }

    public static class ScreenCapture
    {
        public static void CaptureScreenshot(string path) { }
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; }
    }

    public static class Handheld
    {
        public static void Vibrate() { }
    }

    public static class JsonUtility
    {
        public static string ToJson(object o) { return ""; }
        public static T FromJson<T>(string s) { return default(T); }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static T GetBuiltinResource<T>(string name) where T : Object { return null; }
    }

    public static class Random
    {
        public static int Range(int a, int b) { return 0; }
        public static float Range(float a, float b) { return 0f; }
        public static float value { get { return 0f; } }
    }

    public static class AudioSettings
    {
        public static int outputSampleRate { get { return 48000; } }
    }

    public class AudioListener : Behaviour { }
    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public void Play() { }
        public void Stop() { }
    }
    public class AudioClip : Object { }

    public class AnimationCurve
    {
        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1) { return null; }
        public static AnimationCurve Linear(float t0, float v0, float t1, float v1) { return null; }
    }

    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemShapeType { Sphere, Hemisphere, Cone, Box, Circle }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh }

    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public MinMaxCurve(float constant) { }
            public MinMaxCurve(float multiplier, AnimationCurve curve) { }
            public static implicit operator MinMaxCurve(float f) { return new MinMaxCurve(f); }
        }

        public struct EmitParams
        {
            public Vector3 position { get; set; }
            public Vector3 velocity { get; set; }
            public Color32 startColor { get; set; }
            public float startSize { get; set; }
            public float startLifetime { get; set; }
        }

        public class MainModule
        {
            public float duration { get; set; }
            public bool loop { get; set; }
            public bool playOnAwake { get; set; }
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public float gravityModifier { get; set; }
            public int maxParticles { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
            public Color startColor { get; set; }
        }

        public class EmissionModule { public bool enabled { get; set; } }

        public class ShapeModule
        {
            public bool enabled { get; set; }
            public ParticleSystemShapeType shapeType { get; set; }
            public float angle { get; set; }
            public float radius { get; set; }
            public Vector3 rotation { get; set; }
        }

        public class SizeOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve size { get; set; }
        }

        public MainModule main { get { return null; } }
        public EmissionModule emission { get { return null; } }
        public ShapeModule shape { get { return null; } }
        public SizeOverLifetimeModule sizeOverLifetime { get { return null; } }
        public void Emit(EmitParams p, int count) { }
        public void Emit(int count) { }
        public void Play() { }
        public void Stop() { }
    }

    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode { get; set; }
    }

    // ---- attributes
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum IndexFormat { UInt16, UInt32 }
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
}

namespace UnityEngine.Events
{
    public class UnityEventBase { }
    public class UnityEvent : UnityEventBase
    {
        public void AddListener(Action call) { }
        public void RemoveAllListeners() { }
        public void RemoveListener(Action call) { }
        public void Invoke() { }
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour { public static EventSystem current { get { return null; } } }
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
}

namespace UnityEngine.UI
{
    using UnityEngine.Events;

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public enum TextAnchor2 { }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
    }

    public class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : Behaviour { }

    public class Graphic : Behaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
    }

    public class Image : Graphic { }

    public class Text : Graphic
    {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; }
        public float fadeDuration { get; set; }
    }

    public class Selectable : Behaviour
    {
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get { return null; } }
    }
}

namespace UnityEngine
{
    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public class Font : Object { }

    public class CanvasGroup : Behaviour
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }
}
