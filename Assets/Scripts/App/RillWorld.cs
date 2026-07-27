using System;
using System.Collections.Generic;
using UnityEngine;
using Rill.Core;
using Rill.Flow;

namespace Rill.App
{
    /// <summary>
    /// One mountain, and everything that has ever happened to it. This object is the save file:
    /// there is no XP, no level, no currency — progression is the state of these arrays.
    /// </summary>
    public sealed class RillWorld
    {
        public GameConfig Config { get; private set; }
        public HeightField Field { get; private set; }
        public BasinSystem Basins { get; private set; }
        public List<SecretSite> Secrets { get; private set; }
        public StrataBand[] Bands { get; private set; }
        public Biome Biome { get; private set; }
        public uint Seed { get; private set; }

        public Vector2Int SummitCell { get; private set; }
        public Vector3 SummitWorld => Field.GridToWorld(SummitCell.x, SummitCell.y);

        // Lifetime record. These are read off the world, never awarded.
        public int RunNumber;
        public float LifetimeSediment;      // m^3 moved
        public float LifetimeWaterToSea;    // m^3 delivered
        public float LifetimePlaySeconds;
        public long FirstPlayedUtcTicks;

        public event Action<SecretSite> SecretRevealed;
        public event Action<Basin, float> BasinOverflowed;

        float[] _preRunHeight;
        float[] _preRunBasinFill;
        readonly List<string> _pendingHeadlines = new List<string>();
        string _pendingLatticeChange;

        public static RillWorld Create(GameConfig config, uint seed, Biome biome)
        {
            var w = new RillWorld();
            w.Config = config;
            w.Seed = seed;
            w.Biome = biome;
            w.Bands = StrataPalette.For(biome);

            var s = MountainGenerator.Settings.Default(seed, biome);
            s.Size = config.Size;
            s.CellSize = config.CellSize;
            s.PeakHeight = config.PeakHeight;

            Vector2Int summit;
            List<SecretSite> secrets;
            w.Field = MountainGenerator.Generate(s, out summit, out secrets);
            w.SummitCell = summit;
            w.Secrets = secrets;
            w.FirstPlayedUtcTicks = DateTime.UtcNow.Ticks;

            w.FinishSetup();
            return w;
        }

        /// <summary>Used by the loader once the arrays have been restored from disk.</summary>
        public static RillWorld FromRestored(GameConfig config, uint seed, Biome biome, HeightField field,
                                             Vector2Int summit, List<SecretSite> secrets)
        {
            var w = new RillWorld
            {
                Config = config,
                Seed = seed,
                Biome = biome,
                Bands = StrataPalette.For(biome),
                Field = field,
                SummitCell = summit,
                Secrets = secrets
            };
            w.FinishSetup();
            return w;
        }

        void FinishSetup()
        {
            _preRunHeight = new float[Field.Count];
            Basins = new BasinSystem(Field);
            Basins.Overflowed += (b, excess) =>
            {
                _pendingHeadlines.Add(b.Name + " broke its banks");
                if (BasinOverflowed != null) BasinOverflowed(b, excess);
            };
            Basins.Lost += (name, volume) =>
            {
                // The water is not gone — GatherExistingWater routes anything left outside a
                // depression downhill until it finds one — so say where it went, not just that
                // something vanished. A silted-up tarn is the mountain finishing a thing the
                // player spent a hundred runs on, and it deserves to be told as an ending.
                _pendingLatticeChange = volume >= 1f
                    ? string.Format("{0} silted up for good — its {1:n0} m³ moved on downhill", name, volume)
                    : string.Format("{0} silted up for good", name);
                _pendingHeadlines.Add(_pendingLatticeChange);
            };
            Basins.Merged += (oldNames, survivor) =>
            {
                // The lattice shrinking is the mountain finishing something, and the player spent
                // runs on it. Two tarns whose dividing ground your silt raised until they share a
                // surface are now one lake, and that is worth a sentence.
                _pendingLatticeChange = string.Format("{0} are one lake now", oldNames);
                _pendingHeadlines.Add(_pendingLatticeChange);
            };
            Basins.Rebuild();
            Field.MarkAllDirty();
        }

        // ------------------------------------------------------------------ queries

        /// <summary>
        /// Effective rock hardness where the water is right now: the strata band exposed at this
        /// elevation, modulated by per-cell variation. Carving down into a hard band slows you,
        /// which is what makes a deep channel an achievement rather than an inevitability.
        /// </summary>
        public float HardnessAt(float worldX, float worldZ)
        {
            float h = Field.SampleHeightWorld(worldX, worldZ);
            float band = StrataPalette.HardnessAt(Bands, h);
            float varMul = Field.SampleHardnessWorld(worldX, worldZ);
            float biomeMul = Rill.World.BiomeRules.HardnessMultiplier(Field, worldX, worldZ);
            return Mathf.Clamp01(band * varMul * biomeMul);
        }

        public bool IsSea(float worldX, float worldZ)
        {
            return Field.SampleHeightWorld(worldX, worldZ) <= Field.SeaLevel + Config.SeaMargin;
        }

        /// <summary>Where this run's rain gathers. Slight jitter so no two runs start identically.</summary>
        public Vector3 SpawnPoint(ref Rng rng)
        {
            Vector2 xz = Field.GridToWorldXZ(SummitCell.x, SummitCell.y);
            xz += new Vector2(rng.Range(-2.5f, 2.5f), rng.Range(-2.5f, 2.5f));
            return new Vector3(xz.x, Field.SampleHeightWorld(xz.x, xz.y), xz.y);
        }

        // ------------------------------------------------------------------ run bookkeeping

        public void BeginRun()
        {
            RunNumber++;
            Snapshot();
        }

        /// <summary>
        /// Same snapshot, no run number. A dam break is something the mountain does, not something
        /// the player did: counting it as a run made the report card read "run 12" while the world
        /// had moved to 13, and wrote automatic events into the player's own history.
        /// </summary>
        public void BeginAutomaticEvent()
        {
            Snapshot();
        }

        void Snapshot()
        {
            Field.CopyHeightTo(_preRunHeight);
            _pendingHeadlines.Clear();
            _pendingLatticeChange = null;

            int n = Basins.Basins.Count;
            if (_preRunBasinFill == null || _preRunBasinFill.Length < n) _preRunBasinFill = new float[Mathf.Max(n, 8)];
            for (int i = 0; i < n; i++) _preRunBasinFill[i] = Basins.Basins[i].FillFraction;
        }

        /// <summary>
        /// Diffs the mountain against its state at the start of the run and produces the carve
        /// report. This is the only place a player's effort is turned into words, and it can
        /// never come back empty-handed.
        /// </summary>
        public CarveReport EndRun(RunEnding ending, float duration, float distance, float topSpeed, float waterToSea)
        {
            var rep = new CarveReport
            {
                RunNumber = RunNumber,
                Ending = ending,
                Duration = duration,
                DistanceTravelled = distance,
                TopSpeed = topSpeed,
                WaterToSea = waterToSea
            };

            float cellArea = Field.CellSize * Field.CellSize;
            float deepest = 0f;
            int deepestCell = -1;
            float moved = 0f;
            int changed = 0;
            float channelMetres = 0f;

            for (int i = 0; i < Field.Count; i++)
            {
                float d = Field.Height[i] - _preRunHeight[i];
                if (d > -1e-4f && d < 1e-4f) continue;
                changed++;
                moved += Mathf.Abs(d) * cellArea;
                float cut = -d;
                if (cut > deepest) { deepest = cut; deepestCell = i; }
                if (cut > 0.02f && Field.Polish[i] > 0.5f) channelMetres += Field.CellSize;
            }

            rep.CellsChanged = changed;
            rep.SedimentMoved = moved;
            rep.DeepestCarve = deepest;
            rep.NewChannelMetres = channelMetres;
            if (deepestCell >= 0)
                rep.DeepestCarveWorld = Field.GridToWorld(deepestCell % Field.Size, deepestCell / Field.Size);

            // Basin deltas — the open loops the player came back for.
            var basins = Basins.Basins;
            for (int i = 0; i < basins.Count; i++)
            {
                float before = i < _preRunBasinFill.Length ? _preRunBasinFill[i] : 0f;
                float after = basins[i].FillFraction;
                if (after - before > 0.002f)
                {
                    rep.BasinChanges.Add(new CarveReport.BasinDelta
                    {
                        Name = basins[i].Name,
                        Before01 = before,
                        After01 = after,
                        AddedVolume = (after - before) * basins[i].Capacity
                    });
                }
            }
            rep.BasinChanges.Sort((a, b) => (b.After01 - b.Before01).CompareTo(a.After01 - a.Before01));

            CheckRevelations(rep);

            for (int i = 0; i < _pendingHeadlines.Count; i++)
            {
                rep.Headlines.Add(_pendingHeadlines[i]);
                if (_pendingHeadlines[i].EndsWith("broke its banks"))
                {
                    rep.Overflowed = true;
                    rep.OverflowBasin = _pendingHeadlines[i].Replace(" broke its banks", "");
                }
            }
            rep.LatticeChange = _pendingLatticeChange;
            _pendingHeadlines.Clear();
            _pendingLatticeChange = null;

            LifetimeSediment += moved;
            LifetimeWaterToSea += waterToSea;
            LifetimePlaySeconds += duration;

            return rep;
        }

        void CheckRevelations(CarveReport rep)
        {
            for (int i = 0; i < Secrets.Count; i++)
            {
                var s = Secrets[i];
                if (s.Revealed) continue;
                if (!ErodedNear(s.Cell, Field.Virgin[s.Cell] - s.RevealElevation)) continue;
                s.Revealed = true;
                s.RevealedOnRun = RunNumber;
                rep.Revealed.Add(s);
                rep.Headlines.Add(s.DisplayName + " uncovered");
                if (SecretRevealed != null) SecretRevealed(s);
                if (s.Kind == SecretKind.Spring) OpenSpring(s);
                if (s.Kind == SecretKind.CaveMouth) OpenCave(s);
            }
        }

        /// <summary>
        /// Has the player worn the ground down by <paramref name="need"/> metres anywhere within a
        /// couple of cells of this site?
        ///
        /// Requiring the exact cell made secrets practically unfindable: a channel is a few metres
        /// wide and wanders, so cutting one specific 2 m cell deeply enough is a coincidence rather
        /// than a skill. With exact-cell matching, 24 runs revealed 0 of 51.
        ///
        /// But the widened test must compare *erosion*, not elevation. Asking whether any nearby
        /// cell sits below the target elevation is satisfied by slope alone — 4 m downhill on a
        /// 30° face is already 2 m lower than here, before anyone has played. That version revealed
        /// 37 of 51, and gave itself away by reporting the same 37 after 24 runs and after 150:
        /// a count that does not move with play was never driven by play.
        /// </summary>
        bool ErodedNear(int cell, float need)
        {
            if (need <= 0f) return true;
            const int r = 2;                 // 2 cells = 4 m at the default cell size
            int n = Field.Size;
            int cx = cell % n, cz = cell / n;
            for (int dz = -r; dz <= r; dz++)
            {
                int z = cz + dz;
                if (z < 0 || z >= n) continue;
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= n) continue;
                    int c = z * n + x;
                    if (Field.Virgin[c] - Field.Height[c] >= need) return true;
                }
            }
            return false;
        }

        /// <summary>A revealed spring becomes a permanent second source: the mountain's plumbing changed.</summary>
        void OpenSpring(SecretSite s)
        {
            int x = s.Cell % Field.Size, z = s.Cell / Field.Size;
            Vector2 xz = Field.GridToWorldXZ(x, z);
            Field.AddBrush(Field.Wet, xz.x, xz.y, 4f, 1f, clamp01: true);
            Field.AddBrush(Field.Polish, xz.x, xz.y, 3f, 0.4f, clamp01: true);
        }

        /// <summary>A cave mouth swallows water: a sink that can drain a lake you spent weeks filling.</summary>
        void OpenCave(SecretSite s)
        {
            int x = s.Cell % Field.Size, z = s.Cell / Field.Size;
            Vector2 xz = Field.GridToWorldXZ(x, z);
            Field.AddBrush(Field.Height, xz.x, xz.y, 3.5f, -4.5f);
        }

        /// <summary>
        /// Between runs the mountain keeps living. Abandoned channels silt closed a little,
        /// ground dries, polish dulls. Slow enough to never feel like punishment, fast enough
        /// that a six-week-old topology cannot ossify into a boring local minimum.
        /// </summary>
        public void ApplyBetweenRunDrift()
        {
            float heal = Config.HealingPerRun;
            for (int i = 0; i < Field.Count; i++)
            {
                float polish = Field.Polish[i];
                if (polish > 0.001f)
                {
                    // Only channels that were NOT used this run silt up: usage is tracked by wetness.
                    float unused = 1f - Field.Wet[i];
                    Field.Height[i] += heal * unused * polish;
                    Field.Polish[i] = Mathf.Max(0f, polish - Config.PolishDecayPerRun * unused);
                }
                Field.Wet[i] = Mathf.Max(0f, Field.Wet[i] - Config.WetDecayPerRun);
            }
            Field.MarkAllDirty();
        }

        /// <summary>
        /// The mountain breathing while the app was closed: the same silt-and-dry drift that runs
        /// between runs, applied once per stretch of absence and *measured*, so the title can say
        /// truthfully what changed. Capped by the caller — a month away must read as "the mountain
        /// settled", not "your channels are gone"; the design forbids absence ever being a
        /// punishment.
        ///
        /// Returns the diffs rather than a string because what is worth saying is a UI decision,
        /// and a measured zero (nothing changed) must be distinguishable from not having looked.
        /// </summary>
        public void ApplyAwayDrift(int ticks, out float siltVolume, out int driedCells)
        {
            siltVolume = 0f;
            driedCells = 0;
            float cellArea = Field.CellSize * Field.CellSize;
            for (int t = 0; t < ticks; t++)
            {
                float heal = Config.HealingPerRun;
                for (int i = 0; i < Field.Count; i++)
                {
                    float polish = Field.Polish[i];
                    if (polish > 0.001f)
                    {
                        float unused = 1f - Field.Wet[i];
                        float dh = heal * unused * polish;
                        Field.Height[i] += dh;
                        siltVolume += dh * cellArea;
                        Field.Polish[i] = Mathf.Max(0f, polish - Config.PolishDecayPerRun * unused);
                    }
                    float wet = Field.Wet[i];
                    float after = Mathf.Max(0f, wet - Config.WetDecayPerRun);
                    if (wet > 0.05f && after <= 0.05f) driedCells++;
                    Field.Wet[i] = after;
                }
            }
            if (ticks > 0) Field.MarkAllDirty();
        }

        public void AddHeadline(string h) => _pendingHeadlines.Add(h);
    }
}
