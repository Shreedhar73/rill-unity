using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.World
{
    /// <summary>
    /// What makes each mountain a different game rather than a different colour.
    ///
    ///   Sandstone — soft and fast. The mountain that teaches the rules by obeying them.
    ///   Granite   — slow, precise, prestige. Nothing here is changed cheaply.
    ///   Glacier   — freeze/melt. Your channels lock shut in the cold and hand it all back in a thaw.
    ///   Volcanic  — the inverted verb. Vents *create* land, and water quenching lava makes
    ///               obsidian: rock your stream will never cut again.
    ///
    /// All of it runs between runs, never during flow, so the simulation stays one thing.
    /// </summary>
    public static class BiomeRules
    {
        public const float FreezeRate = 0.16f;
        public const float MeltRate = 0.34f;
        public const float VentGrowth = 0.55f;    // metres of new land per run per vent

        public static void BetweenRuns(RillWorld world, WeatherSystem weather, List<string> headlines)
        {
            switch (world.Biome)
            {
                case Biome.Glacier: Glacier(world, weather, headlines); break;
                case Biome.Volcanic: Volcanic(world, headlines); break;
                case Biome.Granite: Granite(world); break;
            }
        }

        // ------------------------------------------------------------------ glacier

        static void Glacier(RillWorld world, WeatherSystem weather, List<string> headlines)
        {
            var f = world.Field;
            bool thawing = weather != null &&
                           (weather.Kind == WeatherKind.Snowmelt || weather.Kind == WeatherKind.Storm);

            float frozenCells = 0f, thawedVolume = 0f;
            float cellArea = f.CellSize * f.CellSize;

            for (int i = 0; i < f.Count; i++)
            {
                if (f.Height[i] <= f.SeaLevel) { f.Ice[i] = 0f; continue; }

                if (thawing)
                {
                    if (f.Ice[i] <= 0f) continue;
                    float melted = Mathf.Min(f.Ice[i], MeltRate);
                    f.Ice[i] -= melted;
                    // Meltwater is real water: a thaw is a free run's worth of volume, spread out.
                    f.Wet[i] = Mathf.Min(1f, f.Wet[i] + melted * 0.5f);
                    thawedVolume += melted * 0.12f * cellArea;
                }
                else
                {
                    float moisture = Mathf.Max(f.Wet[i], Mathf.Clamp01(f.Water[i]));
                    if (moisture < 0.15f) continue;
                    // High ground freezes first, exactly as anyone would guess.
                    float altitude = Mathf.Clamp01(f.Height[i] / Mathf.Max(world.Config.PeakHeight, 1f));
                    f.Ice[i] = Mathf.Min(1f, f.Ice[i] + FreezeRate * moisture * (0.35f + altitude));
                    if (f.Ice[i] > 0.5f) frozenCells++;
                }
            }

            f.MarkAllDirty();

            if (thawing && thawedVolume > 1f)
                headlines.Add(string.Format("The thaw released {0:n0} m³", thawedVolume));
            else if (frozenCells > f.Count * 0.02f)
                headlines.Add("Channels froze overnight");
        }

        // ------------------------------------------------------------------ volcanic

        /// <summary>
        /// Vents are derived from the world seed rather than saved: the mountain's plumbing is a
        /// property of the mountain, so it never needs to be written to disk.
        /// </summary>
        public static List<int> Vents(RillWorld world, int count = 5)
        {
            var list = new List<int>(count);
            var f = world.Field;
            var rng = new Rng(Noise.Hash(world.Seed ^ 0x1EAF1EAFu));
            int guard = 0;
            while (list.Count < count && guard++ < count * 200)
            {
                int x = rng.Range(12, f.Size - 12);
                int z = rng.Range(12, f.Size - 12);
                int i = z * f.Size + x;
                // Vents sit high: land creation should push the summit around, not the shoreline.
                if (f.Virgin[i] < world.Config.PeakHeight * 0.45f) continue;
                list.Add(i);
            }
            return list;
        }

        static void Volcanic(RillWorld world, List<string> headlines)
        {
            var f = world.Field;
            var vents = Vents(world);
            bool madeObsidian = false;
            float grown = 0f;

            for (int v = 0; v < vents.Count; v++)
            {
                int cell = vents[v];
                int x = cell % f.Size, z = cell / f.Size;
                Vector2 xz = f.GridToWorldXZ(x, z);

                // Lava piles up: the only place in RILL where the mountain grows on its own.
                grown += f.AddBrush(f.Height, xz.x, xz.y, 3.2f, VentGrowth);

                // Water that met lava leaves obsidian: permanently the hardest rock on the mountain.
                if (f.SampleWetWorld(xz.x, xz.y) > 0.25f || f.SampleWaterWorld(xz.x, xz.y) > 0.02f)
                {
                    f.AddBrush(f.Hardness, xz.x, xz.y, 4.5f, 0.22f, clamp01: false);
                    f.AddDye(xz.x, xz.y, 4.5f, new Color(0.10f, 0.09f, 0.12f), 0.5f);
                    madeObsidian = true;
                }
            }

            // Keep the variation multiplier sane no matter how many runs quench the same vent.
            for (int i = 0; i < f.Count; i++)
                if (f.Hardness[i] > 1.35f) f.Hardness[i] = 1.35f;

            f.MarkAllDirty();
            if (madeObsidian) headlines.Add("Water met lava — obsidian formed");
            // The vents build 13 m of new rock over 24 runs — an order of magnitude more terrain
            // change than any other biome causes — and used to say nothing at all unless water
            // happened to quench one. A mountain that grows in silence breaks the same rule as a
            // system that silently does nothing: if it creates something, report the amount.
            else if (grown > 1f) headlines.Add(string.Format("The vents added {0:n0} m³ of new rock", grown));
        }

        // ------------------------------------------------------------------ granite

        static void Granite(RillWorld world)
        {
            // Granite barely heals: what you cut here stays cut, which is the whole appeal of it.
            var f = world.Field;
            for (int i = 0; i < f.Count; i++)
                if (f.Polish[i] > 0.001f) f.Polish[i] = Mathf.Min(1f, f.Polish[i] + 0.002f);
        }

        // ------------------------------------------------------------------ shared

        /// <summary>Multiplier applied on top of strata hardness. Ice is the big one.</summary>
        public static float HardnessMultiplier(HeightField f, float worldX, float worldZ)
        {
            float ice = f.SampleIceWorld(worldX, worldZ);
            return 1f + ice * 2.6f;
        }
    }
}
