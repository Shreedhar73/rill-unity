using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.World
{
    public enum LifeTier
    {
        None = 0,
        Moss = 1,
        Reeds = 2,
        Fish = 3,
        Birds = 4,
        Deer = 5,
        Village = 6
    }

    /// <summary>
    /// Where water persists, life arrives uninvited. The ecosystem never asks for anything — it
    /// cannot starve, it cannot nag, and skipping a week costs nothing. It is a garden, not a pet.
    /// It exists so the world can visibly thank the player for playing.
    /// </summary>
    public sealed class EcosystemSystem : MonoBehaviour
    {
        public float GrowthPerRun = 0.16f;
        public float DecayPerRun = 0.012f;
        public int MaxInstancesPerType = 2000;

        RillWorld _world;
        HeightField _f;
        float[] _life;               // 0..6 continuous; floor() is the tier

        Mesh _mossMesh, _reedMesh, _bushMesh, _hutMesh;
        Material _mossMat, _reedMat, _bushMat, _hutMat;

        readonly List<Matrix4x4> _moss = new List<Matrix4x4>();
        readonly List<Matrix4x4> _reeds = new List<Matrix4x4>();
        readonly List<Matrix4x4> _bushes = new List<Matrix4x4>();
        readonly List<Matrix4x4> _huts = new List<Matrix4x4>();
        readonly Matrix4x4[] _batch = new Matrix4x4[1023];

        LifeTier _highestSeen = LifeTier.None;
        public LifeTier HighestTier => _highestSeen;
        public int LivingCells { get; private set; }

        /// <summary>Fires the first time a tier appears anywhere. Goes straight into the Almanac.</summary>
        public event System.Action<LifeTier> TierArrived;

        public void Initialise(RillWorld world, Material propMaterialTemplate)
        {
            _world = world;
            _f = world.Field;
            if (_life == null || _life.Length != _f.Count) _life = new float[_f.Count];

            // Was Disc + Blade: a green decal and a single crossed quad. Both read as exactly
            // what they were once the capture tool finally put them on screen.
            _mossMesh = PropMeshes.Cushion(1.0f, 7);
            _reedMesh = PropMeshes.ReedClump(0.26f, 1.7f);
            // Was a single 6-sided open cone, which reads as a flat paper triangle.
            _bushMesh = PropMeshes.Conifer(1.05f, 3.1f, 7);
            // Bigger than the 2.4 x 2.3 x 2.6 it was. A conifer here is 3.1 m, so at the old
            // size a dwelling was smaller than the trees around it and vanished into them — a
            // village has to be the thing you notice in a wood, not the thing hidden by it.
            _hutMesh = PropMeshes.Hut(new Vector3(3.8f, 3.4f, 4.2f));

            _mossMat = Tinted(propMaterialTemplate, new Color(0.35f, 0.55f, 0.32f));
            _reedMat = Tinted(propMaterialTemplate, new Color(0.52f, 0.72f, 0.38f));
            // Lifted from (0.28, 0.48, 0.30). Rendered against the mountain, conifers at the old
            // value read as near-black cutouts: the base of a tree is most of its visible mass and
            // the baked shading darkens exactly there, so the two compounded.
            _bushMat = Tinted(propMaterialTemplate, new Color(0.34f, 0.58f, 0.34f));
            _hutMat = Tinted(propMaterialTemplate, new Color(0.78f, 0.66f, 0.50f));

            RebuildInstances();
        }

        static Material Tinted(Material template, Color c)
        {
            var m = new Material(template);
            m.enableInstancing = true;
            m.color = c;
            return m;
        }

        public float[] LifeField => _life;

        /// <summary>Swaps in another world's life field (the Daily mountain borrows the renderers).</summary>
        public void UseLifeField(float[] life)
        {
            if (life == null) return;
            _life = life;
            RebuildInstances();
        }

        public void RestoreLife(float[] life)
        {
            if (life == null || _life == null || life.Length != _life.Length) return;
            System.Array.Copy(life, _life, life.Length);
            RebuildInstances();
        }

        /// <summary>
        /// Called once per run, never per frame. Life is a week-to-week track: it should move
        /// slowly enough that noticing it feels like noticing a plant has grown.
        /// </summary>
        public void AdvanceAfterRun(List<string> arrivalsOut)
        {
            if (_world == null) return;
            var cfg = _world.Config;
            int n = _f.Size;
            LifeTier before = _highestSeen;

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float h = _f.Height[i];
                    if (h <= _f.SeaLevel) { _life[i] = 0f; continue; }

                    // A filled basin drowns what grew in it. The old rule was the opposite — cells
                    // under a lake counted as maximally moist and grew to tier 6 — but RebuildInstances
                    // rightly refuses to draw props underwater, so a basin campaign ended with the
                    // mountain's entire visible forest sunk out of sight: observed on a real save,
                    // 128 living cells, every one of them under the brim-full North basin, and not a
                    // tree on screen anywhere. Reported as "the trees are gone", which it was.
                    float standing = _f.Water[i];
                    if (standing > 0.25f)
                    {
                        _life[i] = Mathf.Max(0f, _life[i] - GrowthPerRun);
                        continue;
                    }

                    float moisture = Mathf.Max(_f.Wet[i], Mathf.Clamp01(standing * 2f));

                    // The shore drinks from the lake. This is where the drowned forest's growth
                    // goes instead: a ring of reeds and trees at the waterline, which is the thing
                    // a player who filled a basin can actually see.
                    bool shore = (x > 0 && _f.Water[i - 1] > 0.1f)
                              || (x < n - 1 && _f.Water[i + 1] > 0.1f)
                              || (z > 0 && _f.Water[i - n] > 0.1f)
                              || (z < n - 1 && _f.Water[i + n] > 0.1f);
                    if (shore) moisture = 1f;

                    if (moisture >= cfg.LifeMoistureThreshold)
                    {
                        // Growth accelerates near standing water and slows at altitude: the valley
                        // greens first, exactly as the player expects, without being told.
                        float altitudeFactor = Mathf.Clamp01(1f - h / Mathf.Max(cfg.PeakHeight, 1f) * 0.85f);
                        float lakeBonus = (shore || standing > 0.05f) ? 1.7f : 1f;
                        _life[i] = Mathf.Min(6f, _life[i] + GrowthPerRun * moisture * altitudeFactor * lakeBonus);
                    }
                    else
                    {
                        _life[i] = Mathf.Max(0f, _life[i] - DecayPerRun);
                    }
                }
            }

            RebuildInstances();

            LifeTier now = _highestSeen;
            if (now > before && arrivalsOut != null)
            {
                for (LifeTier t = before + 1; t <= now; t++)
                {
                    arrivalsOut.Add(Describe(t));
                    if (TierArrived != null) TierArrived(t);
                }
            }
        }

        /// <summary>
        /// Drops life directly at a spot — how caught seeds pay out. It is the only way life
        /// appears anywhere the water has not already been, and the player has to have carried
        /// the seed there in the stream to earn it.
        /// </summary>
        public void PlantAt(Vector3 world, float radiusMetres, float amount)
        {
            if (_f == null || _life == null) return;
            Vector2 g = _f.WorldToGrid(world.x, world.z);
            float radiusCells = Mathf.Max(1f, radiusMetres / _f.CellSize);
            int r = Mathf.CeilToInt(radiusCells);
            int cx = Mathf.RoundToInt(g.x), cz = Mathf.RoundToInt(g.y);

            for (int z = cz - r; z <= cz + r; z++)
            {
                if (z < 0 || z >= _f.Size) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= _f.Size) continue;
                    float dx = x - g.x, dz = z - g.y;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) / radiusCells;
                    if (d >= 1f) continue;
                    int i = z * _f.Size + x;
                    if (_f.Height[i] <= _f.SeaLevel) continue;
                    float w = 1f - d * d;
                    _life[i] = Mathf.Min(6f, _life[i] + amount * w);
                    _f.Wet[i] = Mathf.Min(1f, _f.Wet[i] + 0.4f * w);
                }
            }
            RebuildInstances();
        }

        public static string Describe(LifeTier t)
        {
            switch (t)
            {
                case LifeTier.Moss: return "Moss took hold";
                case LifeTier.Reeds: return "Reeds arrived";
                case LifeTier.Fish: return "Fish arrived";
                case LifeTier.Birds: return "Birds arrived";
                case LifeTier.Deer: return "Deer came down to drink";
                case LifeTier.Village: return "A village settled at the delta";
                default: return "The mountain is bare";
            }
        }

        void RebuildInstances()
        {
            _moss.Clear(); _reeds.Clear(); _bushes.Clear(); _huts.Clear();
            LivingCells = 0;
            LifeTier highest = LifeTier.None;
            int n = _f.Size;

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float l = _life[i];
                    if (l < 0.5f) continue;
                    LivingCells++;

                    var tier = (LifeTier)Mathf.Clamp(Mathf.FloorToInt(l), 0, 6);
                    if (tier > highest) highest = tier;
                    if (_f.Water[i] > 0.1f) continue; // props sit on land, not in the lake

                    // Deterministic jitter so the meadow never shimmers between rebuilds.
                    uint h = Noise.Hash2(x, z, 4242u);
                    float jx = ((h & 0xFF) / 255f - 0.5f) * _f.CellSize * 0.8f;
                    float jz = (((h >> 8) & 0xFF) / 255f - 0.5f) * _f.CellSize * 0.8f;
                    float rot = ((h >> 16) & 0xFF) / 255f * 360f;
                    float scale = 0.75f + ((h >> 24) & 0xFF) / 255f * 0.5f;

                    Vector3 p = new Vector3((x - n * 0.5f) * _f.CellSize + jx, _f.Height[i], (z - n * 0.5f) * _f.CellSize + jz);

                    // Density rises with life rather than every living cell sprouting something.
                    // Placing a prop on all of them carpets the valley in identical dots; the eye
                    // reads that as noise, not as a meadow getting thicker.
                    uint roll = h % 100u;
                    float density = Mathf.Clamp01((l - 0.5f) / 4f);      // 0 at first moss, 1 by deer
                    if (roll > 12u + density * 26u) continue;

                    var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, rot, 0f), Vector3.one * scale);

                    // A hut needs somewhere to stand. Props are placed at a single sampled height
                    // with no slope adaptation, which is invisible for a tree — a buried trunk
                    // still reads as a tree — and wrong for a building: rendered on a 35° face, a
                    // 2.4 m hut is half-buried uphill and floating downhill, and reads as a pale
                    // card stuck to the rock. People build on flats, so this is what it should
                    // always have been rather than a workaround.
                    if (l >= 5.5f && (h % 37u) == 0u && _huts.Count < 60 && _f.NormalAt(x, z).y > 0.91f)
                    {
                        _huts.Add(m);
                    }
                    else if (l >= 3.0f && _bushes.Count < MaxInstancesPerType)
                    {
                        // Bushes get real size variance so a stand of them has a silhouette.
                        _bushes.Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, rot, 0f), new Vector3(scale, scale * (0.8f + ((h >> 4) & 0x3F) / 63f * 0.9f), scale)));
                    }
                    else if (l >= 1.5f && _reeds.Count < MaxInstancesPerType)
                    {
                        _reeds.Add(m);
                    }
                    else if (_moss.Count < MaxInstancesPerType)
                    {
                        _moss.Add(m);
                    }
                }
            }

            if (highest > _highestSeen) _highestSeen = highest;
        }

        /// <summary>
        /// Bakes the current instance lists into real MeshRenderers under <paramref name="parent"/>.
        ///
        /// Life is normally issued with Graphics.DrawMesh from Update, which never runs outside
        /// play mode — so the offscreen capture tool could render the terrain and the lakes and
        /// never the life on them, and "no trees in the picture" meant nothing at all. This is the
        /// seam that makes the whole look checkable from a terminal instead of only the rock.
        ///
        /// One combined mesh per type rather than thousands of GameObjects: the props are a handful
        /// of triangles each, so 2,000 of them still fits comfortably in a 32-bit index buffer.
        /// </summary>
        /// <summary>
        /// World position of a settlement, if any has appeared. Used to frame a capture on one:
        /// twelve huts on a 512 m mountain will not turn up in a general framing by luck, and a
        /// prop nobody has seen is a prop nobody has checked.
        /// </summary>
        public bool TryGetVillage(out Vector3 position)
        {
            position = Vector3.zero;
            if (_huts.Count == 0) return false;
            // Middle of the list rather than the first: huts are emitted in scan order, so the
            // first is on the edge of whatever cluster exists and frames badly.
            position = _huts[_huts.Count / 2].GetColumn(3);
            return true;
        }

        public void BakeStaticRenderers(Transform parent)
        {
            Bake(parent, "Moss", _mossMesh, _mossMat, _moss);
            Bake(parent, "Reeds", _reedMesh, _reedMat, _reeds);
            Bake(parent, "Bushes", _bushMesh, _bushMat, _bushes);
            Bake(parent, "Huts", _hutMesh, _hutMat, _huts);
        }

        static void Bake(Transform parent, string name, Mesh mesh, Material mat, List<Matrix4x4> xforms)
        {
            if (mesh == null || xforms.Count == 0) return;

            var combines = new CombineInstance[xforms.Count];
            for (int i = 0; i < xforms.Count; i++)
            {
                combines[i].mesh = mesh;
                combines[i].transform = xforms[i];
            }

            var combined = new Mesh { name = name };
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.CombineMeshes(combines, true, true);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Debug.Log(string.Format("[RILL] baked {0} x{1}", name, xforms.Count));
        }

        void Update()
        {
            if (_mossMesh == null) return;
            DrawAll(_mossMesh, _mossMat, _moss);
            DrawAll(_reedMesh, _reedMat, _reeds);
            DrawAll(_bushMesh, _bushMat, _bushes);
            DrawAll(_hutMesh, _hutMat, _huts);
        }

        void DrawAll(Mesh mesh, Material mat, List<Matrix4x4> list)
        {
            if (list.Count == 0) return;
            int i = 0;
            while (i < list.Count)
            {
                int count = Mathf.Min(1023, list.Count - i);
                list.CopyTo(i, _batch, 0, count);
                Graphics.DrawMeshInstanced(mesh, 0, mat, _batch, count, null,
                    UnityEngine.Rendering.ShadowCastingMode.Off, false);
                i += count;
            }
        }
    }
}
