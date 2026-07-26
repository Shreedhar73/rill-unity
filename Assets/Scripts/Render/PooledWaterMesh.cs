using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Rill.Core;

namespace Rill.Render
{
    /// <summary>
    /// Lakes. Built directly from the standing-water field, so a basin at 64% looks like a basin
    /// at 64% — the progression bar is the water line on the rock.
    /// </summary>
    public sealed class PooledWaterMesh : MonoBehaviour
    {
        public float MinDepth = 0.03f;
        public float SurfaceLift = 0.02f;

        HeightField _f;
        Mesh _mesh;
        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<Color32> _colors = new List<Color32>(4096);
        readonly List<int> _tris = new List<int>(8192);
        int[] _vertexIndex;
        bool _dirty = true;

        public void Initialise(HeightField field, Material material)
        {
            _f = field;
            _vertexIndex = new int[field.Count];

            var mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _mesh = new Mesh { name = "PooledWater" };
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;
            Rebuild();
        }

        public void SetDirty() => _dirty = true;

        void LateUpdate()
        {
            if (_dirty) Rebuild();
        }

        void Rebuild()
        {
            _dirty = false;
            if (_f == null) return;

            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
            int n = _f.Size;

            for (int i = 0; i < _vertexIndex.Length; i++) _vertexIndex[i] = -1;

            // A vertex exists wherever the cell (or a neighbour) holds water, so lake edges are
            // feathered rather than stair-stepped.
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float d = _f.Water[i];
                    bool near = d > MinDepth;
                    if (!near)
                    {
                        if (x > 0 && _f.Water[i - 1] > MinDepth) near = true;
                        else if (x < n - 1 && _f.Water[i + 1] > MinDepth) near = true;
                        else if (z > 0 && _f.Water[i - n] > MinDepth) near = true;
                        else if (z < n - 1 && _f.Water[i + n] > MinDepth) near = true;
                    }
                    if (!near) continue;

                    // A water surface is LEVEL. For a submerged cell, Height + Water already gives
                    // the lake's surface elevation. For the feather ring — cells that only exist
                    // because a neighbour holds water — d is 0, so this used to place the vertex at
                    // *terrain* height, which is above the water by definition (that is why the
                    // cell is dry). Every lake was therefore a flat disc that walled upward into a
                    // collar at its rim: "flat discs with hard shorelines", exactly as reported.
                    // Ring vertices now take the neighbouring lake's surface level instead.
                    float surface;
                    if (d > MinDepth)
                    {
                        surface = _f.Height[i] + d;
                    }
                    else
                    {
                        surface = _f.Height[i];
                        if (x > 0 && _f.Water[i - 1] > MinDepth) surface = Mathf.Max(surface, _f.Height[i - 1] + _f.Water[i - 1]);
                        if (x < n - 1 && _f.Water[i + 1] > MinDepth) surface = Mathf.Max(surface, _f.Height[i + 1] + _f.Water[i + 1]);
                        if (z > 0 && _f.Water[i - n] > MinDepth) surface = Mathf.Max(surface, _f.Height[i - n] + _f.Water[i - n]);
                        if (z < n - 1 && _f.Water[i + n] > MinDepth) surface = Mathf.Max(surface, _f.Height[i + n] + _f.Water[i + n]);
                    }
                    surface += SurfaceLift;
                    _vertexIndex[i] = _verts.Count;
                    _verts.Add(new Vector3((x - n * 0.5f) * _f.CellSize, surface, (z - n * 0.5f) * _f.CellSize));

                    // Alpha carries depth: shallows are translucent, deep water is dense.
                    //
                    // The ring of vertices that only exist because a *neighbour* holds water sits
                    // at d = 0, and a flat 0.25 floor there drew the lake edge at 25% opacity —
                    // a visible hard rim, which is why lakes read as discs laid on the mountain
                    // rather than water lying in it. The first factor takes alpha to zero exactly
                    // at the waterline, so the edge dissolves instead of stopping.
                    float shore = Mathf.Clamp01(d / 0.75f);
                    byte a = (byte)(shore * Mathf.Clamp01(0.45f + d * 0.45f) * 255f);
                    // Depth gradient over 2.5 m, not 6 m. Real basins here are only a few metres
                    // deep, so a 6 m range left almost every lake pinned at the shallow colour and
                    // the gradient invisible — the lake read as one flat tone.
                    byte depthByte = (byte)(Mathf.Clamp01(d / 2.5f) * 255f);
                    _colors.Add(new Color32(255, depthByte, 255, a));
                }
            }

            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int a = _vertexIndex[z * n + x];
                    int b = _vertexIndex[z * n + x + 1];
                    int c = _vertexIndex[(z + 1) * n + x];
                    int d = _vertexIndex[(z + 1) * n + x + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    _tris.Add(a); _tris.Add(c); _tris.Add(b);
                    _tris.Add(b); _tris.Add(c); _tris.Add(d);
                }
            }

            _mesh.Clear();
            if (_verts.Count == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
