using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Rill.Core;

namespace Rill.Render
{
    /// <summary>
    /// Geology as papercraft. The terrain is drawn as flat strata bands so that every metre of
    /// depth is legible as colour: erosion literally reveals prettier layers, which makes the
    /// core verb and the reward pipeline the same thing.
    ///
    /// Chunked, with a per-frame rebuild budget so carving never costs a frame drop.
    /// </summary>
    public sealed class TerrainMeshBuilder : MonoBehaviour
    {
        public int ChunkCells = 64;
        public int ChunkRebuildBudgetPerFrame = 3;

        HeightField _f;
        StrataBand[] _bands;
        Material _material;

        class Chunk
        {
            public GameObject Go;
            public Mesh Mesh;
            public Vector3[] Verts;
            public Vector3[] Normals;
            public Color32[] Colors;
            // uv.x = ambient occlusion from local concavity, uv.y = wetness.
            // Vertex colour is fully spent (rgb = strata, a = carve glow), so form and damp
            // ground ride in the UV channel instead.
            public Vector2[] Uvs;
            public int X0, Z0, W, H;   // W,H = vertex counts
            public bool Dirty;
        }

        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly Queue<Chunk> _rebuildQueue = new Queue<Chunk>();

        /// <summary>Per-cell glow used by the carve report overlay. Decays to zero.</summary>
        float[] _overlay;
        bool _overlayDirty;

        public void Initialise(HeightField field, StrataBand[] bands, Material material)
        {
            _f = field;
            _bands = bands;
            _material = material;
            _overlay = new float[field.Count];

            foreach (var c in _chunks) if (c.Go != null) Destroy(c.Go);
            _chunks.Clear();
            _rebuildQueue.Clear();

            int step = ChunkCells;
            for (int z0 = 0; z0 < _f.Size - 1; z0 += step)
            {
                for (int x0 = 0; x0 < _f.Size - 1; x0 += step)
                {
                    int w = Mathf.Min(step + 1, _f.Size - x0);
                    int h = Mathf.Min(step + 1, _f.Size - z0);
                    _chunks.Add(CreateChunk(x0, z0, w, h));
                }
            }
            MarkAll();
        }

        Chunk CreateChunk(int x0, int z0, int w, int h)
        {
            var go = new GameObject(string.Format("Chunk_{0}_{1}", x0, z0));
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var mesh = new Mesh { name = go.name };
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.MarkDynamic();

            var chunk = new Chunk
            {
                Go = go,
                Mesh = mesh,
                Verts = new Vector3[w * h],
                Normals = new Vector3[w * h],
                Colors = new Color32[w * h],
                Uvs = new Vector2[w * h],
                X0 = x0, Z0 = z0, W = w, H = h
            };

            var tris = new int[(w - 1) * (h - 1) * 6];
            int t = 0;
            for (int z = 0; z < h - 1; z++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    int i = z * w + x;
                    tris[t++] = i;
                    tris[t++] = i + w;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + w;
                    tris[t++] = i + w + 1;
                }
            }

            Fill(chunk);
            mesh.vertices = chunk.Verts;
            mesh.normals = chunk.Normals;
            mesh.colors32 = chunk.Colors;
            mesh.uv = chunk.Uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return chunk;
        }

        void Fill(Chunk c)
        {
            int n = _f.Size;
            for (int z = 0; z < c.H; z++)
            {
                int gz = c.Z0 + z;
                for (int x = 0; x < c.W; x++)
                {
                    int gx = c.X0 + x;
                    int gi = gz * n + gx;
                    int vi = z * c.W + x;

                    float hgt = _f.Height[gi];
                    c.Verts[vi] = new Vector3((gx - n * 0.5f) * _f.CellSize, hgt, (gz - n * 0.5f) * _f.CellSize);
                    c.Normals[vi] = _f.NormalAt(gx, gz);

                    // Concavity occlusion: a cell that sits below its neighbours is inside
                    // something — a channel, a basin, a plunge pool — and must read as inside it.
                    // Without this the terrain is a flat-lit bedsheet and carving is invisible.
                    float ao = Occlusion(gx, gz, hgt);
                    float wet = _f.Wet[gi];
                    c.Uvs[vi] = new Vector2(ao, wet);

                    Color col = StrataPalette.ColorAt(_bands, hgt);

                    // A polished bed is cooler and a little darker: your channels read as channels
                    // from orbit. sqrt() lifts modest polish (a real channel sits near 0.3-0.5)
                    // into visible range.
                    //
                    // Mostly a TINT, not a darkener, and that distinction was learned the hard way.
                    // Four separate things darken a channel cell — this, the wet blend below, the
                    // shader's occlusion term and the shader's own wet darkening — and they
                    // multiply. At the old values a channel came out at roughly 0.25 of the
                    // surrounding rock before seam and cliff darkening, which rendered as a black
                    // stripe down the mountain and hid the deeper strata band the cut had exposed.
                    // Revealing strata is the reward; nothing may paint over it.
                    float polish = _f.Polish[gi];
                    if (polish > 0.001f)
                    {
                        float k = Mathf.Sqrt(Mathf.Clamp01(polish)) * 0.85f;
                        var dampRock = col * 0.78f + new Color(0.02f, 0.045f, 0.075f);
                        col = Color.Lerp(col, dampRock, k);
                    }

                    // NO third darkening term for the cut itself. There was one, and the first
                    // rendered image of this mountain showed why it was wrong: stacked on top of
                    // the polish tint and the concavity shading it turned the main channel into a
                    // near-black stripe, and — worse — it overrode the deeper strata band that the
                    // cut had just revealed. "Every metre of depth is legible as colour" is the
                    // design's central visual promise, and painting depth as *darkness* defeats it
                    // exactly where the player has done the most work.
                    //
                    // The cut is already drawn twice over, correctly: the cell sits lower so it
                    // takes a lower band's colour, and Occlusion() shades it because it is inside
                    // something. Both are geometry, so both survive polish decaying to nothing,
                    // which is what L-015 actually needed.

                    // Light: the shader darkens by wetness too, and doing it twice was
                    // half the reason a channel went black.
                    if (wet > 0.001f) col = Color.Lerp(col, StrataPalette.WetColor, wet * 0.12f);

                    // Dye is a mineral stain, not paint: it tints the rock it soaked into and
                    // never replaces it. An earlier pass blended at 0.85 and looked like a bruise.
                    Color32 dye = _f.Dye[gi];
                    if (dye.a > 0) col = Color.Lerp(col, new Color32(dye.r, dye.g, dye.b, 255), dye.a / 255f * 0.38f);

                    // Ice pales and cools the rock it has locked up.
                    float ice = _f.Ice[gi];
                    if (ice > 0.001f) col = Color.Lerp(col, new Color(0.86f, 0.93f, 0.97f), ice * 0.75f);

                    if (hgt <= _f.SeaLevel) col = Color.Lerp(col, StrataPalette.SeaColor, 0.55f);

                    float glow = _overlay != null ? Mathf.Clamp01(_overlay[gi]) : 0f;
                    c.Colors[vi] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(glow * 255f));
                }
            }
        }

        /// <summary>Darkest a hollow may be shaded. Shading says "inside something"; it must never
        /// take the strata colour away, because revealing strata IS the reward.</summary>
        const float MinOcclusion = 0.55f;

        /// <summary>
        /// 1 = open ground, 0.45 = deep inside a hollow. Compares the cell against a ring of
        /// neighbours a few cells out, so it catches channel walls rather than pixel noise.
        /// </summary>
        float Occlusion(int x, int z, float h)
        {
            int n = _f.Size;
            const int R = 3;
            float above = 0f;
            int samples = 0;

            for (int k = 0; k < 8; k++)
            {
                int dx = (k == 0 || k == 4 || k == 5) ? R : (k == 1 || k == 6 || k == 7) ? -R : 0;
                int dz = (k == 2 || k == 4 || k == 6) ? R : (k == 3 || k == 5 || k == 7) ? -R : 0;
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                float d = _f.Height[nz * n + nx] - h;
                if (d > 0f) above += d;
                samples++;
            }
            if (samples == 0) return 1f;

            // Metres of surrounding rock standing above this point, normalised to the depth of
            // channel worth shading.
            //
            // 4 m was a guess calibrated for channels this game does not make: measured over 150
            // runs, 613 cells are cut more than 0.5 m below virgin and only 191 more than 1.5 m, so
            // a real channel produced an occlusion of about 0.93 — a 7% darkening before
            // _AOStrength scaled it down further, which is why a dry channel could not be made out
            // at all.
            //
            // 1.6 m then overcorrected badly, and it took rendering the mountain to see it: a
            // 2 m-deep channel drove the term to zero and the whole feature went black. Shading is
            // supposed to say "this is inside something", not delete it.
            //
            // 2.5 m with a floor. The floor is the important half — occlusion may darken a hollow
            // by at most 55%, so no amount of depth can crush the strata colour that the cut
            // exposed, which is the thing the player is actually being shown.
            float mean = above / samples;
            return Mathf.Clamp01(Mathf.Max(MinOcclusion, 1f - mean / 2.5f));
        }

        public void MarkAll()
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i].Dirty) continue;
                _chunks[i].Dirty = true;
                _rebuildQueue.Enqueue(_chunks[i]);
            }
        }

        void MarkRect(int minX, int minZ, int maxX, int maxZ)
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                var c = _chunks[i];
                if (c.Dirty) continue;
                if (c.X0 > maxX || c.X0 + c.W - 1 < minX) continue;
                if (c.Z0 > maxZ || c.Z0 + c.H - 1 < minZ) continue;
                c.Dirty = true;
                _rebuildQueue.Enqueue(c);
            }
        }

        void LateUpdate()
        {
            if (_f == null) return;

            if (_f.Dirty)
            {
                MarkRect(_f.DirtyMinX - 1, _f.DirtyMinZ - 1, _f.DirtyMaxX + 1, _f.DirtyMaxZ + 1);
                _f.ClearDirty();
            }

            if (_overlayDirty) DecayOverlay(Time.deltaTime);

            int budget = ChunkRebuildBudgetPerFrame;
            while (budget-- > 0 && _rebuildQueue.Count > 0)
            {
                var c = _rebuildQueue.Dequeue();
                c.Dirty = false;
                Fill(c);
                c.Mesh.vertices = c.Verts;
                c.Mesh.normals = c.Normals;
                c.Mesh.colors32 = c.Colors;
                c.Mesh.uv = c.Uvs;
                c.Mesh.RecalculateBounds();
            }
        }

        // ------------------------------------------------------------- carve overlay

        /// <summary>
        /// Paints the glowing "here is what this run changed" overlay from a height diff.
        /// This is the carve report made visible on the mountain itself.
        /// </summary>
        public void ShowCarveOverlay(float[] beforeHeights, float scale = 12f)
        {
            if (_overlay == null || beforeHeights == null) return;
            for (int i = 0; i < _overlay.Length; i++)
            {
                float d = Mathf.Abs(_f.Height[i] - beforeHeights[i]);
                _overlay[i] = Mathf.Clamp01(d * scale);
            }
            _overlayDirty = true;
            MarkAll();
        }

        public void ClearOverlay()
        {
            if (_overlay == null) return;
            System.Array.Clear(_overlay, 0, _overlay.Length);
            _overlayDirty = false;
            MarkAll();
        }

        void DecayOverlay(float dt)
        {
            float k = Mathf.Exp(-dt * 0.55f);
            bool any = false;
            for (int i = 0; i < _overlay.Length; i++)
            {
                if (_overlay[i] <= 0.002f) { _overlay[i] = 0f; continue; }
                _overlay[i] *= k;
                any = true;
            }
            _overlayDirty = any;
            MarkAll();
        }
    }
}
