using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rill.Render
{
    /// <summary>
    /// The stream, drawn as a luminous ribbon that is deliberately a little wider than physical
    /// truth: at phone scale a hairline of water reads as nothing, and this is the object the
    /// player's eye is locked to for the entire run.
    /// </summary>
    public sealed class WaterRibbon : MonoBehaviour
    {
        public int MaxPoints = 320;
        public float BaseWidth = 2.4f;
        public float WidthPerSpeed = 0.10f;
        public float TailFade = 0.75f;
        public float SurfaceOffset = 0.38f;

        Mesh _mesh;
        MeshRenderer _renderer;
        Vector3[] _verts;
        Vector2[] _uvs;
        Color32[] _colors;
        int[] _tris;
        int _capacity;

        readonly List<Vector3> _points = new List<Vector3>(512);
        float _fade = 1f;

        public void Initialise(Material material)
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _mesh = new Mesh { name = "WaterRibbon" };
            _mesh.indexFormat = IndexFormat.UInt16;
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;
            Allocate(MaxPoints);
        }

        void Allocate(int points)
        {
            _capacity = points;
            _verts = new Vector3[points * 2];
            _uvs = new Vector2[points * 2];
            _colors = new Color32[points * 2];
            _tris = new int[(points - 1) * 6];
            for (int i = 0; i < points - 1; i++)
            {
                int v = i * 2, t = i * 6;
                _tris[t] = v; _tris[t + 1] = v + 2; _tris[t + 2] = v + 1;
                _tris[t + 3] = v + 1; _tris[t + 4] = v + 2; _tris[t + 5] = v + 3;
            }
        }

        public void Clear()
        {
            _points.Clear();
            if (_mesh != null) _mesh.Clear();
            _fade = 1f;
        }

        /// <summary>Fades the ribbon out after a run without deleting it — the water lingers, then goes.</summary>
        public void FadeOut(float dt, float rate = 1.4f)
        {
            _fade = Mathf.Max(0f, _fade - dt * rate);
            if (_points.Count >= 2) Rebuild(0f);
            if (_fade <= 0.001f && _mesh != null) _mesh.Clear();
        }

        public void SetPath(IList<Vector3> path, Vector3 head, float speed)
        {
            _fade = 1f;
            _points.Clear();
            int start = Mathf.Max(0, path.Count - _capacity + 1);
            for (int i = start; i < path.Count; i++) _points.Add(path[i]);
            if (_points.Count == 0 || (head - _points[_points.Count - 1]).sqrMagnitude > 0.01f) _points.Add(head);
            if (_points.Count < 2) return;
            Rebuild(speed);
        }

        void Rebuild(float speed)
        {
            int n = Mathf.Min(_points.Count, _capacity);
            if (n < 2) return;
            int offset = _points.Count - n;

            float width = BaseWidth + WidthPerSpeed * speed;

            for (int i = 0; i < n; i++)
            {
                Vector3 p = _points[offset + i];
                Vector3 dir;
                if (i == 0) dir = _points[offset + 1] - p;
                else if (i == n - 1) dir = p - _points[offset + i - 1];
                else dir = _points[offset + i + 1] - _points[offset + i - 1];
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                dir.Normalize();
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);

                float t = (float)i / (n - 1);
                // Taper: thin at the tail, full width at the head, tiny bulge just behind the head.
                float w = width * Mathf.Lerp(0.35f, 1f, Mathf.Pow(t, 0.6f));
                float alpha = Mathf.Lerp(1f - TailFade, 1f, t) * _fade;

                int v = i * 2;
                Vector3 up = Vector3.up * SurfaceOffset;
                _verts[v] = p - side * w + up;
                _verts[v + 1] = p + side * w + up;
                _uvs[v] = new Vector2(0f, t * 4f);
                _uvs[v + 1] = new Vector2(1f, t * 4f);
                byte a = (byte)(Mathf.Clamp01(alpha) * 255f);
                // Green channel carries "energy" so the shader can brighten fast water.
                byte e = (byte)(Mathf.Clamp01(speed / 20f) * 255f);
                _colors[v] = new Color32(255, e, 255, a);
                _colors[v + 1] = _colors[v];
            }

            // Collapse unused vertices onto the tail so the shared triangle list stays valid.
            for (int i = n; i < _capacity; i++)
            {
                int v = i * 2;
                _verts[v] = _verts[(n - 1) * 2];
                _verts[v + 1] = _verts[(n - 1) * 2 + 1];
                _colors[v] = _colors[v + 1] = new Color32(255, 0, 255, 0);
                _uvs[v] = _uvs[v + 1] = Vector2.zero;
            }

            _mesh.Clear();
            _mesh.vertices = _verts;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = _tris;
            _mesh.RecalculateBounds();
        }
    }
}
