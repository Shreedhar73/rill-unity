using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Rill.App;
using Rill.Core;
using Rill.Flow;
using Rill.Meta;

namespace Rill.EditorTools
{
    /// <summary>
    /// Headless exercise of the parts of RILL that are plain C#: generation, the flow simulation,
    /// basins, revelation, save/load and the Daily glyph. It renders nothing, so it can be run in
    /// batchmode to answer the only question a compiler cannot: does a run actually do anything?
    ///
    /// Run from a terminal:
    ///   Unity -batchmode -quit -nographics -projectPath . -executeMethod Rill.EditorTools.RillSmokeTest.RunHeadless
    /// </summary>
    public static class RillSmokeTest
    {
        const int Runs = 24;

        [MenuItem("RILL/Run Headless Smoke Test", false, 60)]
        public static void RunHeadless()
        {
            var log = new StringBuilder();
            var config = new GameConfig();
            var world = RillWorld.Create(config, 20260726u, Biome.Sandstone);

            log.AppendLine("=== RILL headless smoke test ===");
            log.AppendFormat("field {0}² at {1} m/cell = {2} m across\n", config.Size, config.CellSize, config.WorldExtent);
            log.AppendFormat("summit {0}  height {1:0.0} m\n", world.SummitCell, world.SummitWorld.y);
            log.AppendFormat("secrets placed {0}\n", world.Secrets.Count);
            log.AppendFormat("basins found {0}, capacity {1:n0} m³\n", world.Basins.Basins.Count, TotalCapacity(world));

            var sim = new FlowSimulation(world);
            var endings = new Dictionary<RunEnding, int>();
            var dailyPaths = new List<List<Vector3>>();
            var dailySea = new List<bool>();
            float totalSediment = 0f, totalDistance = 0f, bestSpeed = 0f, toSea = 0f;

            for (int run = 1; run <= Runs; run++)
            {
                world.BeginRun();
                var rng = new Rng(Noise.Hash((uint)run * 2654435761u ^ world.Seed));
                Vector3 spawn = world.SpawnPoint(ref rng);
                sim.Begin(spawn, config.StartVolume);

                // Steer like a distracted player: occasional lateral lean, mostly hands off.
                int steps = 0;
                while (sim.Running && steps++ < 20000)
                {
                    if (steps % 90 == 0)
                    {
                        bool steer = rng.Next01() < 0.45f;
                        Vector2 target = sim.Head.Pos + new Vector2(rng.Range(-25f, 25f), rng.Range(-25f, 25f));
                        sim.SetSteer(steer, target);
                    }
                    sim.Advance(config.SimStep);
                }

                var rep = world.EndRun(sim.Ending, sim.Elapsed, sim.Distance, sim.TopSpeed, sim.WaterToSea);
                world.Basins.Rebuild();
                world.ApplyBetweenRunDrift();

                if (!endings.ContainsKey(sim.Ending)) endings[sim.Ending] = 0;
                endings[sim.Ending]++;
                totalSediment += rep.SedimentMoved;
                totalDistance += rep.DistanceTravelled;
                toSea += rep.WaterToSea;
                if (rep.TopSpeed > bestSpeed) bestSpeed = rep.TopSpeed;

                dailyPaths.Add(new List<Vector3>(sim.Path));
                dailySea.Add(sim.Ending == RunEnding.ReachedSea);

                if (run <= 3 || run == Runs)
                {
                    log.AppendFormat("run {0,3}  {1,-12} {2,5:0.0}s  {3,6:0} m  top {4,5:0.0} m/s  moved {5,8:n0} m³  deepest {6:0.00} m  \"{7}\"\n",
                        run, sim.Ending, rep.Duration, rep.DistanceTravelled, rep.TopSpeed,
                        rep.SedimentMoved, rep.DeepestCarve, rep.Summary());
                }
            }

            log.AppendLine("--- after " + Runs + " runs ---");
            foreach (var kv in endings) log.AppendFormat("  {0,-12} {1}\n", kv.Key, kv.Value);
            log.AppendFormat("  sediment moved   {0:n0} m³ (avg {1:n0}/run)\n", totalSediment, totalSediment / Runs);
            log.AppendFormat("  distance         {0:n0} m (avg {1:n0}/run)\n", totalDistance, totalDistance / Runs);
            log.AppendFormat("  top speed        {0:0.0} m/s\n", bestSpeed);
            log.AppendFormat("  delivered to sea {0:n0} m³\n", toSea);
            log.AppendFormat("  water held       {0:n0} m³ across {1} basins\n", world.Basins.TotalWater(), world.Basins.Basins.Count);
            log.AppendFormat("  fullest basin    {0:0.0}%\n", FullestBasin(world) * 100f);
            log.AppendFormat("  polished cells   {0} ({1:0.0}% of field)\n", PolishedCells(world), PolishedCells(world) * 100f / world.Field.Count);
            log.AppendFormat("  secrets revealed {0} of {1}\n", RevealedCount(world), world.Secrets.Count);
            log.Append("  basin lattice:  ");
            for (int i = 0; i < world.Basins.Basins.Count; i++)
            {
                var b = world.Basins.Basins[i];
                if (b.Capacity < 80f) continue;
                log.AppendFormat("{0:0}%/{1:n0}m³  ", b.FillFraction * 100f, b.Capacity);
            }
            log.AppendLine();
            log.AppendFormat("  terrain delta    min {0:0.00} m, max {1:0.00} m vs virgin\n", MinDelta(world), MaxDelta(world));

            // Save / load round-trip: the world is the save file, so this is the load-bearing test.
            var life = new float[world.Field.Count];
            SaveSystem.Save(world, life, 99);
            float[] loadedLife;
            var reloaded = SaveSystem.Load(new GameConfig(), out loadedLife, 99);
            bool identical = reloaded != null && SameHeights(world, reloaded);
            log.AppendFormat("  save/load        {0} (run {1}, {2:n0} m³ lifetime)\n",
                identical ? "round-trips exactly" : "MISMATCH", reloaded != null ? reloaded.RunNumber : -1,
                reloaded != null ? reloaded.LifetimeSediment : 0f);
            SaveSystem.DeleteSlot(99);

            string glyph = GlyphGenerator.Render(dailyPaths, dailySea, world.Field.WorldExtent);
            log.AppendLine("  daily glyph:");
            log.AppendLine(glyph);

            Debug.Log(log.ToString());
        }

        static float TotalCapacity(RillWorld w)
        {
            float c = 0f;
            for (int i = 0; i < w.Basins.Basins.Count; i++) c += w.Basins.Basins[i].Capacity;
            return c;
        }

        static float FullestBasin(RillWorld w)
        {
            float best = 0f;
            for (int i = 0; i < w.Basins.Basins.Count; i++)
                if (w.Basins.Basins[i].FillFraction > best) best = w.Basins.Basins[i].FillFraction;
            return best;
        }

        static int PolishedCells(RillWorld w)
        {
            int n = 0;
            for (int i = 0; i < w.Field.Count; i++) if (w.Field.Polish[i] > 0.35f) n++;
            return n;
        }

        static int RevealedCount(RillWorld w)
        {
            int n = 0;
            for (int i = 0; i < w.Secrets.Count; i++) if (w.Secrets[i].Revealed) n++;
            return n;
        }

        static float MinDelta(RillWorld w)
        {
            float m = 0f;
            for (int i = 0; i < w.Field.Count; i++)
            {
                float d = w.Field.Height[i] - w.Field.Virgin[i];
                if (d < m) m = d;
            }
            return m;
        }

        static float MaxDelta(RillWorld w)
        {
            float m = 0f;
            for (int i = 0; i < w.Field.Count; i++)
            {
                float d = w.Field.Height[i] - w.Field.Virgin[i];
                if (d > m) m = d;
            }
            return m;
        }

        static bool SameHeights(RillWorld a, RillWorld b)
        {
            if (a.Field.Count != b.Field.Count) return false;
            for (int i = 0; i < a.Field.Count; i++)
                if (Mathf.Abs(a.Field.Height[i] - b.Field.Height[i]) > 1e-4f) return false;
            return true;
        }
    }
}
