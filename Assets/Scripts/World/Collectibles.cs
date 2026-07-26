using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.World
{
    public enum PickupKind
    {
        Seed = 0,       // catch it, and life takes hold where the water carries it
        Dye = 1,        // splash it, and the rock keeps the colour forever
        Gate = 2        // thread it at speed, and the channel comes out polished
    }

    public struct Pickup
    {
        public PickupKind Kind;
        public Vector3 World;
        public float Radius;
        public float SpeedRequired;   // gates only
        public Color Color;
        public bool Taken;
    }

    /// <summary>
    /// The things scattered along a run. None of them are currency and none of them gate anything:
    /// a seed plants life, a flower stains the rock, a gate rewards a line you took at speed. They
    /// exist to give the eye something to want on the way down, and to make two runs down the same
    /// channel feel different.
    ///
    /// Placement is deterministic from (world seed, run number), so a run is reproducible and the
    /// Daily Rill is fair — but it is regenerated every run, so nothing has to be saved.
    /// </summary>
    public sealed class Collectibles : MonoBehaviour
    {
        public int SeedsPerRun = 7;
        public int FlowersPerRun = 4;
        public int GatesPerRun = 5;
        public float CatchRadius = 3.2f;
        public float GateRadius = 5.0f;
        public float GatePolishBonus = 0.55f;
        public float GateSpeedReward = 2.4f;

        RillWorld _world;
        Material _propTemplate;
        Mesh _seedMesh, _flowerMesh, _gateMesh;
        Material _seedMat, _flowerMat, _gateMatOpen;

        readonly List<Pickup> _pickups = new List<Pickup>();
        readonly List<Matrix4x4> _seedDraw = new List<Matrix4x4>();
        readonly List<Matrix4x4> _flowerDraw = new List<Matrix4x4>();
        readonly List<Matrix4x4> _gateDraw = new List<Matrix4x4>();
        readonly Matrix4x4[] _batch = new Matrix4x4[256];

        public int SeedsCaught { get; private set; }
        public int FlowersSplashed { get; private set; }
        public int GatesThreaded { get; private set; }

        /// <summary>Fired when the stream takes something. Carries a world position for juice.</summary>
        public event System.Action<PickupKind, Vector3, Color> Collected;

        static readonly Color[] DyePalette =
        {
            new Color(0.78f, 0.42f, 0.44f),
            new Color(0.44f, 0.55f, 0.74f),
            new Color(0.82f, 0.71f, 0.42f),
            new Color(0.52f, 0.70f, 0.50f),
            new Color(0.64f, 0.50f, 0.72f)
        };

        public void Initialise(RillWorld world, Material propTemplate)
        {
            _world = world;
            _propTemplate = propTemplate;

            _seedMesh = PropMeshes.Disc(0.7f, 6);
            _flowerMesh = PropMeshes.Blade(0.5f, 1.1f);
            _gateMesh = PropMeshes.Cone(GateRadius * 0.5f, 0.35f, 10);

            _seedMat = Tint(new Color(0.95f, 0.90f, 0.62f));
            _flowerMat = Tint(new Color(0.92f, 0.52f, 0.72f));
            _gateMatOpen = Tint(new Color(0.55f, 0.85f, 0.95f, 0.85f));

            _pickups.Clear();
            RebuildDrawLists();
        }

        Material Tint(Color c)
        {
            var m = new Material(_propTemplate) { color = c };
            m.enableInstancing = true;
            return m;
        }

        /// <summary>
        /// Lays out this run's pickups. Seeds and flowers sit on likely water, gates sit on the
        /// fast lines the player has already carved — so the reward for a deep channel is that
        /// the game starts putting things worth catching along it.
        /// </summary>
        public void PlaceForRun(int runNumber)
        {
            _pickups.Clear();
            if (_world == null) return;

            var f = _world.Field;
            var rng = new Rng(Noise.Hash((uint)runNumber * 2246822519u ^ _world.Seed));

            // Gates prefer polished, steep cells: the existing river, at its fastest.
            var fastCells = new List<int>(256);
            var dampCells = new List<int>(512);
            int stride = 3; // sampling the field rather than scanning it keeps this sub-millisecond
            for (int z = 2; z < f.Size - 2; z += stride)
            {
                for (int x = 2; x < f.Size - 2; x += stride)
                {
                    int i = z * f.Size + x;
                    if (f.Height[i] <= f.SeaLevel + 1f) continue;
                    if (f.Polish[i] > 0.35f) fastCells.Add(i);
                    else if (f.Wet[i] > 0.08f || f.Polish[i] > 0.08f) dampCells.Add(i);
                }
            }

            for (int i = 0; i < GatesPerRun && fastCells.Count > 0; i++)
                AddAt(fastCells[rng.Range(0, fastCells.Count)], PickupKind.Gate, ref rng);

            for (int i = 0; i < SeedsPerRun; i++)
                AddAt(PickCell(dampCells, fastCells, ref rng, f), PickupKind.Seed, ref rng);

            for (int i = 0; i < FlowersPerRun; i++)
                AddAt(PickCell(dampCells, fastCells, ref rng, f), PickupKind.Dye, ref rng);

            RebuildDrawLists();
        }

        int PickCell(List<int> damp, List<int> fast, ref Rng rng, HeightField f)
        {
            if (damp.Count > 0 && rng.Next01() < 0.75f) return damp[rng.Range(0, damp.Count)];
            if (fast.Count > 0) return fast[rng.Range(0, fast.Count)];
            // Virgin mountain: scatter anywhere above the waterline so run one still has things in it.
            int guard = 0;
            while (guard++ < 64)
            {
                int c = rng.Range(0, f.Count);
                if (f.Height[c] > f.SeaLevel + 3f) return c;
            }
            return f.Count / 2;
        }

        void AddAt(int cell, PickupKind kind, ref Rng rng)
        {
            var f = _world.Field;
            int x = cell % f.Size, z = cell / f.Size;
            Vector3 p = f.GridToWorld(x, z);

            _pickups.Add(new Pickup
            {
                Kind = kind,
                World = p,
                Radius = kind == PickupKind.Gate ? GateRadius : CatchRadius,
                // Gates ask for a speed the player can only have if they used their own channel.
                SpeedRequired = kind == PickupKind.Gate ? rng.Range(7f, 13f) : 0f,
                Color = kind == PickupKind.Dye ? DyePalette[rng.Range(0, DyePalette.Length)] : Color.white,
                Taken = false
            });
        }

        /// <summary>Called every sim frame during a run. Returns any speed the stream just earned.</summary>
        public float Check(Vector2 headXZ, float headY, float speed, out bool tookSomething)
        {
            tookSomething = false;
            float speedGain = 0f;
            if (_world == null) return 0f;

            for (int i = 0; i < _pickups.Count; i++)
            {
                var p = _pickups[i];
                if (p.Taken) continue;

                float dx = p.World.x - headXZ.x;
                float dz = p.World.z - headXZ.y;
                if (dx * dx + dz * dz > p.Radius * p.Radius) continue;
                if (Mathf.Abs(p.World.y - headY) > 6f) continue;   // do not catch things on the cliff above

                if (p.Kind == PickupKind.Gate)
                {
                    if (speed < p.SpeedRequired) continue;   // too slow: the gate simply stays open
                    // Threading a gate at speed polishes the bed under it: the line you found is
                    // now permanently faster, which is the only "upgrade" in the game.
                    _world.Field.AddBrush(_world.Field.Polish, p.World.x, p.World.z, 3.5f, GatePolishBonus, clamp01: true);
                    speedGain += GateSpeedReward;
                    GatesThreaded++;
                }
                else if (p.Kind == PickupKind.Dye)
                {
                    _world.Field.AddDye(p.World.x, p.World.z, 3.0f, p.Color, 0.30f);
                    FlowersSplashed++;
                }
                else
                {
                    SeedsCaught++;   // planted where the run ends, by RunController
                }

                p.Taken = true;
                _pickups[i] = p;
                tookSomething = true;
                if (Collected != null) Collected(p.Kind, p.World, p.Color);
            }

            if (tookSomething) RebuildDrawLists();
            return speedGain;
        }

        public void ResetCounters()
        {
            SeedsCaught = FlowersSplashed = GatesThreaded = 0;
        }

        void RebuildDrawLists()
        {
            _seedDraw.Clear();
            _flowerDraw.Clear();
            _gateDraw.Clear();

            for (int i = 0; i < _pickups.Count; i++)
            {
                var p = _pickups[i];
                if (p.Taken) continue;
                var m = Matrix4x4.TRS(p.World + Vector3.up * 0.35f, Quaternion.identity, Vector3.one);
                switch (p.Kind)
                {
                    case PickupKind.Seed: _seedDraw.Add(m); break;
                    case PickupKind.Dye: _flowerDraw.Add(m); break;
                    default: _gateDraw.Add(Matrix4x4.TRS(p.World + Vector3.up * 0.1f, Quaternion.identity, Vector3.one)); break;
                }
            }
        }

        void Update()
        {
            if (_seedMesh == null) return;
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.18f;
            DrawList(_seedMesh, _seedMat, _seedDraw, bob);
            DrawList(_flowerMesh, _flowerMat, _flowerDraw, bob * 0.4f);
            DrawList(_gateMesh, _gateMatOpen, _gateDraw, 0f);
        }

        void DrawList(Mesh mesh, Material mat, List<Matrix4x4> list, float bobY)
        {
            if (list.Count == 0) return;
            int i = 0;
            while (i < list.Count)
            {
                int count = Mathf.Min(_batch.Length, list.Count - i);
                for (int k = 0; k < count; k++)
                {
                    var m = list[i + k];
                    if (bobY != 0f)
                    {
                        Vector3 pos = m.GetColumn(3);
                        pos.y += bobY;
                        m = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
                    }
                    _batch[k] = m;
                }
                Graphics.DrawMeshInstanced(mesh, 0, mat, _batch, count, null,
                    UnityEngine.Rendering.ShadowCastingMode.Off, false);
                i += count;
            }
        }

        /// <summary>Seeds are not points: they become life where the water finally stopped.</summary>
        public void PlantCaughtSeeds(EcosystemSystem ecosystem, Vector3 whereTheRunEnded)
        {
            if (SeedsCaught <= 0 || ecosystem == null || _world == null) return;
            ecosystem.PlantAt(whereTheRunEnded, 2.5f + SeedsCaught * 0.9f, 1.4f + SeedsCaught * 0.35f);
        }
    }
}
