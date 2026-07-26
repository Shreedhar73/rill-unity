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
        const int DefaultRuns = 24;

        [MenuItem("RILL/Run Headless Smoke Test", false, 60)]
        public static void RunHeadless() { Play(DefaultRuns); }

        /// <summary>
        /// The same test over a session's worth of play. 24 runs move 1.8k m³ of rock against
        /// 16.7k m³ of basin capacity, so a 24-run result cannot distinguish "basins never spill"
        /// from "basins have not filled yet" — and those need opposite fixes.
        /// </summary>
        [MenuItem("RILL/Run Headless Smoke Test (long)", false, 61)]
        public static void RunHeadlessLong() { Play(150); }

        static void Play(int Runs)
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

            // Where runs stop, not just that they stopped. "24/24 Pooled" is the same log line
            // whether the water drowned in a lake or seized up on open rock 40 m from the summit,
            // and those two need opposite fixes.
            int stoppedInBasin = 0, stoppedOnOpenGround = 0, inWater = 0, throughFlow = 0;
            // Can a player who wants to fill a particular basin actually fill it? The whole basin
            // lattice as a progression track assumes yes, and nothing has ever tested it.
            int aimedRuns = 0, aimedHits = 0, seaRuns = 0, seaHits = 0;
            float aimedMissDistance = 0f;
            float strandedVolume = 0f, totalDescent = 0f, totalStopSlope = 0f;
            var stopBasinHits = new Dictionary<int, int>();

            for (int run = 1; run <= Runs; run++)
            {
                world.BeginRun();
                var rng = new Rng(Noise.Hash((uint)run * 2654435761u ^ world.Seed));
                Vector3 spawn = world.SpawnPoint(ref rng);
                sim.Begin(spawn, config.StartVolume);

                // Two thirds of runs commit to a destination for their whole length; the rest are
                // hands off. The original bot re-rolled a random ±25 m target every second, which
                // averages to no steering at all — it cannot route water anywhere, so it cannot
                // test the one claim the basin lattice rests on: that a player who *wants* to fill
                // the west basin can. With that bot, 105 of 150 runs ended in the same lake.
                // Three player intents in equal measure: fill a chosen basin, run for the sea, or
                // hands off. An earlier version aimed 2 runs in 3 at a basin — i.e. deliberately
                // away from the sea — which depressed sea arrivals on its own and made "water
                // reaches the sea sometimes" unmeasurable. The mix has to be neutral between the
                // two endings the loop is judged on.
                float intent = rng.Next01();
                bool aimBasin = world.Basins.Basins.Count > 0 && intent < 0.34f;
                bool aimSea = !aimBasin && intent < 0.67f;
                Vector2 destination = Vector2.zero;
                int intendedBasin = -1;
                if (aimBasin)
                {
                    intendedBasin = rng.Range(0, world.Basins.Basins.Count);
                    int c = world.Basins.Basins[intendedBasin].Cells[0];
                    destination = world.Field.GridToWorldXZ(c % config.Size, c / config.Size);
                }
                else if (aimSea)
                {
                    // Straight at the nearest map edge, which is where the sea is.
                    float half = world.Field.WorldExtent * 0.5f;
                    destination = Mathf.Abs(spawn.x) > Mathf.Abs(spawn.z)
                        ? new Vector2(Mathf.Sign(spawn.x) * half, spawn.z)
                        : new Vector2(spawn.x, Mathf.Sign(spawn.z) * half);
                }
                bool intentional = aimBasin || aimSea;

                int steps = 0;
                while (sim.Running && steps++ < 20000)
                {
                    if (steps % 30 == 0)
                    {
                        if (intentional) sim.SetSteer(true, destination);
                        else sim.SetSteer(rng.Next01() < 0.45f,
                                          sim.Head.Pos + new Vector2(rng.Range(-25f, 25f), rng.Range(-25f, 25f)));
                    }
                    sim.Advance(config.SimStep);
                }

                // Read the stop point before EndRun/Rebuild relabels the mountain under it.
                int stopCell = world.Field.NearestIndex(sim.Head.Pos.x, sim.Head.Pos.y);
                var stopBasin = world.Basins.BasinAt(stopCell);
                float stopSlope;
                world.Field.DownhillWorld(sim.Head.Pos.x, sim.Head.Pos.y, out stopSlope);
                totalStopSlope += stopSlope;
                totalDescent += spawn.y - sim.Head.Height;
                strandedVolume += sim.VolumeAtEnd;
                inWater += sim.InWaterSteps;
                throughFlow += sim.ThroughFlowSteps;
                if (stopBasin != null)
                {
                    stoppedInBasin++;
                    if (!stopBasinHits.ContainsKey(stopBasin.Id)) stopBasinHits[stopBasin.Id] = 0;
                    stopBasinHits[stopBasin.Id]++;
                }
                else stoppedOnOpenGround++;

                if (aimBasin)
                {
                    aimedRuns++;
                    if (stopBasin != null && stopBasin.Id == intendedBasin) aimedHits++;
                    else aimedMissDistance += (sim.Head.Pos - destination).magnitude;
                }
                if (aimSea)
                {
                    seaRuns++;
                    if (sim.Ending == RunEnding.ReachedSea) seaHits++;
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

                if (run <= 3 || run == Runs || run % 25 == 0)
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
            log.AppendFormat("  stopped in basin {0}, on open ground {1}\n", stoppedInBasin, stoppedOnOpenGround);
            log.AppendFormat("  stranded volume  {0:n0} m³ total (avg {1:0.0}/run of {2:0} started)\n",
                strandedVolume, strandedVolume / Runs, config.StartVolume);
            log.AppendFormat("  descent          avg {0:0.0} m of {1:0.0} m available\n",
                totalDescent / Runs, world.SummitWorld.y - world.Field.SeaLevel);
            log.AppendFormat("  slope at stop    avg {0:0.000} (tan)\n", totalStopSlope / Runs);
            log.AppendFormat("  steps in water   {0:n0}, of which through-flow {1:n0}\n", inWater, throughFlow);
            log.AppendFormat("  aimed at a basin {0} runs, reached it {1} ({2:0}%), avg miss {3:0} m\n",
                aimedRuns, aimedHits, aimedRuns > 0 ? aimedHits * 100f / aimedRuns : 0f,
                aimedRuns > aimedHits ? aimedMissDistance / (aimedRuns - aimedHits) : 0f);
            log.AppendFormat("  aimed at the sea {0} runs, reached it {1} ({2:0}%)\n",
                seaRuns, seaHits, seaRuns > 0 ? seaHits * 100f / seaRuns : 0f);
            log.Append("  basin sites:    ");
            for (int i = 0; i < world.Basins.Basins.Count; i++)
            {
                var b = world.Basins.Basins[i];
                int c = b.Cells[0];
                Vector2 at = world.Field.GridToWorldXZ(c % config.Size, c / config.Size);
                Vector2 summitXZ = new Vector2(world.SummitWorld.x, world.SummitWorld.z);
                // A basin whose floor sits above the summit spring, or that lies on another
                // drainage, cannot be filled by any amount of steering. That is a generation
                // problem, not a tuning one, and it is invisible in a fill percentage.
                log.AppendFormat("#{0} {1:0}m/{2:0}m  ", i, world.Field.Height[c], (at - summitXZ).magnitude);
            }
            log.AppendLine("  (floor elevation / distance from summit)");

            // Is an unfilled basin unreachable, or merely unvisited? Opposite fixes.
            foreach (float climb in new[] { 0f, 3f })
            {
                var probe = new Rng(world.Seed);
                var reach = DownhillReachable(world, world.SpawnPoint(ref probe), climb);
                int reachable = 0;
                log.AppendFormat("  reach (climb {0:0} m): ", climb);
                for (int i = 0; i < world.Basins.Basins.Count; i++)
                {
                    var b = world.Basins.Basins[i];
                    bool ok = false;
                    for (int k = 0; k < b.Cells.Length && !ok; k++) ok = reach[b.Cells[k]];
                    if (ok) reachable++;
                    log.AppendFormat("#{0} {1}  ", i, ok ? "yes" : "NO");
                }
                log.AppendFormat(" ({0} of {1})\n", reachable, world.Basins.Basins.Count);
            }
            if (stopBasinHits.Count > 0)
            {
                log.Append("  stop basins:    ");
                foreach (var kv in stopBasinHits)
                {
                    var b = world.Basins.Basins[kv.Key];
                    log.AppendFormat("#{0} \"{1}\" x{2} ({3:0}% full)  ", kv.Key, b.Name, kv.Value, b.FillFraction * 100f);
                }
                log.AppendLine();
            }
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

        /// <summary>
        /// Cells water could reach from the spawn, allowing it to climb up to <paramref name="climb"/>
        /// metres at a time on momentum.
        ///
        /// A strictly non-ascending version of this was wrong and said so loudly: it reported basin
        /// #2 unreachable while 25 of 150 runs were visibly ending in it. Water on this mountain
        /// tops 25 m/s, and v²/2g at that speed is tens of metres of climb, so "downhill only"
        /// does not describe the simulation at all. Report a couple of tolerances rather than one
        /// number that looks authoritative and is not.
        /// </summary>
        static bool[] DownhillReachable(RillWorld w, Vector3 spawn, float climb)
        {
            int n = w.Field.Size;
            var h = w.Field.Height;
            var reach = new bool[w.Field.Count];
            int seed = w.Field.NearestIndex(spawn.x, spawn.z);
            reach[seed] = true;

            // A worklist, not a sorted sweep: once climb > 0 reachability can propagate uphill, so
            // "settle every cell before anything below it" is no longer a valid ordering.
            var work = new Queue<int>();
            work.Enqueue(seed);

            while (work.Count > 0)
            {
                int c = work.Dequeue();
                int cx = c % n, cz = c / n;
                for (int q = 0; q < 8; q++)
                {
                    int nx = cx + (q == 0 || q == 4 || q == 5 ? 1 : q == 1 || q == 6 || q == 7 ? -1 : 0);
                    int nz = cz + (q == 2 || q == 4 || q == 6 ? 1 : q == 3 || q == 5 || q == 7 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int ni = nz * n + nx;
                    if (reach[ni]) continue;
                    if (h[ni] > h[c] + climb) continue;
                    reach[ni] = true;
                    work.Enqueue(ni);
                }
            }
            return reach;
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
