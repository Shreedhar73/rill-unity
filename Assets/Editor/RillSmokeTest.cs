using System;
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

        /// <summary>
        /// Every biome, 24 runs each. Glacier, Volcanic and Granite have never been run at all —
        /// they are implemented, compiled, and completely unobserved, which in this project has
        /// meant "silently does nothing" more often than not. Sandstone is the tuned one; the
        /// others are read against it.
        /// </summary>
        [MenuItem("RILL/Run Headless Smoke Test (all biomes)", false, 62)]
        public static void RunHeadlessBiomes()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RILL biome comparison, 24 runs each ===");
            foreach (Biome b in System.Enum.GetValues(typeof(Biome)))
                log.Append(PlayBiome(24, b));
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Glacier freeze *and* thaw. Weather is derived from the date, and the default seed lands
        /// on Drought every run, so the thaw branch — meltwater as real volume, the entire point of
        /// a glacier — had never executed anywhere. This drives both halves explicitly.
        /// </summary>
        [MenuItem("RILL/Run Headless Glacier Thaw", false, 63)]
        public static void RunHeadlessGlacierThaw()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RILL glacier freeze/thaw ===");

            var config = new GameConfig { Biome = Biome.Glacier };
            var world = RillWorld.Create(config, 20260726u, Biome.Glacier);
            var sim = new FlowSimulation(world);
            var weather = new Rill.World.WeatherSystem(world.Seed);
            var headlines = new List<string>();

            DateTime freezeDay = FindWeather(weather, Rill.World.WeatherKind.Drought);
            DateTime thawDay = FindWeather(weather, Rill.World.WeatherKind.Snowmelt);
            log.AppendFormat("freeze weather {0}, thaw weather {1}\n", freezeDay.ToString("yyyy-MM-dd HH"), thawDay.ToString("yyyy-MM-dd HH"));

            for (int phase = 0; phase < 2; phase++)
            {
                weather.Evaluate(phase == 0 ? freezeDay : thawDay);
                headlines.Clear();

                for (int run = 1; run <= 12; run++)
                {
                    world.BeginRun();
                    var rng = new Rng(Noise.Hash((uint)(run + phase * 100) * 2654435761u ^ world.Seed));
                    Vector3 spawn = world.SpawnPoint(ref rng);
                    sim.Begin(spawn, config.StartVolume);
                    int steps = 0;
                    while (sim.Running && steps++ < 20000)
                    {
                        if (steps % 30 == 0)
                            sim.SetSteer(rng.Next01() < 0.45f, sim.Head.Pos + new Vector2(rng.Range(-25f, 25f), rng.Range(-25f, 25f)));
                        sim.Advance(config.SimStep);
                    }
                    world.EndRun(sim.Ending, sim.Elapsed, sim.Distance, sim.TopSpeed, sim.WaterToSea);
                    world.Basins.Rebuild();
                    world.ApplyBetweenRunDrift();
                    Rill.World.BiomeRules.BetweenRuns(world, weather, headlines);
                }

                log.AppendFormat("after 12 runs of {0,-9} ice cells {1,5}  basin water {2,7:n0} m³   headlines: {3}\n",
                    weather.Kind, IceCells(world), world.Basins.TotalWater(),
                    headlines.Count == 0 ? "NONE" : string.Join(" | ", headlines.ToArray()));
            }

            Debug.Log(log.ToString());
        }

        static DateTime FindWeather(Rill.World.WeatherSystem w, Rill.World.WeatherKind want)
        {
            var d = new DateTime(2026, 3, 1, 6, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 900; i++)
            {
                w.Evaluate(d);
                if (w.Kind == want) return d;
                d = d.AddHours(12);
            }
            return d;
        }

        static void Play(int Runs) { Debug.Log(PlayBiome(Runs, Biome.Sandstone).ToString()); }

        /// <summary>
        /// The headline numbers of a session, so one tuning constant can be swept without reading
        /// forty lines of prose per arm.
        /// </summary>
        public class Summary
        {
            public int Sea, TimedOut, Pooled, AimedRuns, AimedDelivered, StoppedInBasin, AimedEntered;
            public float DistancePerRun, Descent, SedimentPerRun, ToSea, AimedMiss, AimedClosest, HollowVolume, HeldWater;
            public float TargetFill, TargetVolume;
        }

        /// <summary>
        /// Steering authority against the momentum that buys it. SteerAccel is 20 m/s² and downhill
        /// acceleration on a 30° face is 30·sin30° = 15, so an unscaled thumb outmuscles the
        /// mountain and a held lean spirals the stream in place — traced doing that for 70 of one
        /// run's 75 seconds. Scaling authority by speed fixes it, but it also costs the player the
        /// ability to route water deliberately, which is the other half of the game. This measures
        /// both halves against each other instead of trading one away by eye.
        ///
        /// SteerFullSpeed = 0 is the unscaled arm, i.e. the behaviour before any of this.
        /// </summary>
        [MenuItem("RILL/Run Headless Steering Sweep", false, 64)]
        public static void RunHeadlessSteerSweep() { SweepSteering(24, new[] { 0f, 3f, 5f, 7f, 9f, 12f }); }

        /// <summary>
        /// The same sweep at session length. A 24-run arm contains about 5 aimed runs, so every
        /// aiming number in the short sweep is noise — and aiming is exactly the half of the game
        /// that scaling steering authority is suspected of costing. 150 runs gives ~36.
        /// </summary>
        [MenuItem("RILL/Run Headless Steering Sweep (long)", false, 65)]
        public static void RunHeadlessSteerSweepLong() { SweepSteering(150, new[] { 0f, 7f, 11f }); }

        /// <summary>
        /// How fast a basin with headroom should drink from a stream passing over it. 8 of the 15
        /// aimed runs that reached their target basin sailed across its dry floor and climbed out
        /// the far side, which is why aimed delivery sat at 19%. Too slow a drain leaves that
        /// unfixed; too fast turns every lake on the route into a wall and starves the sea. Both
        /// halves are in the table. 0 is the arm with no drain at all.
        /// </summary>
        [MenuItem("RILL/Run Headless Basin Soak Sweep", false, 66)]
        public static void RunHeadlessSoakSweep()
        {
            var log = new StringBuilder();
            log.AppendLine("=== basin soak sweep — 150 runs per arm, same seed, same bot ===");
            log.AppendLine("  m3/s    sea  timeout  pooled   dist/run  descent   toSea    held   aimedIn  delivered");
            foreach (float r in new[] { 0f, 4f, 8f, 14f, 22f })
            {
                var cfg = new GameConfig { BasinSoakRate = r };
                var sum = new Summary();
                PlayBiome(150, Biome.Sandstone, cfg, sum);
                log.AppendFormat("  {0,4:0}  {1,5}  {2,7}  {3,6}   {4,7:0} m  {5,6:0} m  {6,6:0} m3  {7,6:0} m3  {8,6}/{9}  {10,7}/{11}\n",
                    r, sum.Sea, sum.TimedOut, sum.Pooled, sum.DistancePerRun, sum.Descent, sum.ToSea,
                    sum.HeldWater, sum.AimedEntered, sum.AimedRuns, sum.AimedDelivered, sum.AimedRuns);
            }
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// The claim the entire progression track rests on, re-tested under the new flow dynamics:
        /// a player who commits to one basin off the incised channel can fill it. L-027 measured
        /// 0% -> 85% for basin #0 over one sustained campaign; if that no longer holds, the basin
        /// lattice is scenery and "north basin 87% full" is not an open loop the player can close.
        ///
        /// Basin #0 is chosen because it is NOT reachable downhill from the spring without carving
        /// a route to it, which is the hard case and the interesting one.
        /// </summary>
        [MenuItem("RILL/Run Headless Basin Campaign", false, 67)]
        public static void RunHeadlessCampaign()
        {
            var log = new StringBuilder();
            log.AppendLine("=== one sustained campaign against basin #0, 150 runs ===");
            log.Append(PlayBiome(150, Biome.Sandstone, null, null, 0));
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Lateral authority against the one thing that needs it: carving a route to a basin that
        /// is not downhill from the spring. Scaling authority by speed (L-038) fixed a deadlock but
        /// took basin #0 from L-027's 0% -> 85% under one sustained campaign to 0 of 36 aimed runs
        /// even getting inside it. The two are separable — the deadlock was authority at REST, and
        /// route-carving is authority at SPEED — so SteerAccel is the knob to test, not the scaling.
        /// </summary>
        [MenuItem("RILL/Run Headless Campaign Sweep", false, 68)]
        public static void RunHeadlessCampaignSweep()
        {
            var log = new StringBuilder();
            log.AppendLine("=== steer authority vs one sustained campaign on off-channel basin #0, 150 runs ===");
            log.AppendLine("  accel   basin#0     entered  delivered   sea  timeout   dist/run   toSea   aimedNear");
            foreach (float a in new[] { 20f, 42f, 56f, 70f })
            {
                var cfg = new GameConfig { SteerAccel = a };
                var sum = new Summary();
                PlayBiome(150, Biome.Sandstone, cfg, sum, 0);
                log.AppendFormat("  {0,5:0}  {1,5:0.0}% {2,7:n0} m3  {3,5}/{4}  {5,5}/{6}  {7,4}  {8,7}  {9,7:0} m  {10,6:0} m3  {11,7:0} m\n",
                    a, sum.TargetFill * 100f, sum.TargetVolume, sum.AimedEntered, sum.AimedRuns,
                    sum.AimedDelivered, sum.AimedRuns, sum.Sea, sum.TimedOut, sum.DistancePerRun,
                    sum.ToSea, sum.AimedClosest);
            }
            Debug.Log(log.ToString());
        }

        static void SweepSteering(int runs, float[] arms)
        {
            var log = new StringBuilder();
            log.AppendFormat("=== steering authority sweep — {0} runs per arm, same seed, same bot ===\n", runs);
            log.AppendLine("  full@   sea  timeout  pooled  inBasin   dist/run  descent  sed/run   toSea   aimedMiss  aimedNear  delivered");
            foreach (float s in arms)
            {
                var cfg = new GameConfig { SteerFullSpeed = s };
                var sum = new Summary();
                PlayBiome(runs, Biome.Sandstone, cfg, sum);
                log.AppendFormat("  {0,5:0.0}  {1,4}  {2,7}  {3,6}  {4,7}   {5,7:0} m  {6,6:0} m  {7,6:0} m³  {8,6:0} m³  {9,8:0} m  {10,8:0} m  {11,7}/{12}\n",
                    s, sum.Sea, sum.TimedOut, sum.Pooled, sum.StoppedInBasin, sum.DistancePerRun, sum.Descent,
                    sum.SedimentPerRun, sum.ToSea, sum.AimedMiss, sum.AimedClosest, sum.AimedDelivered, sum.AimedRuns);
            }
            Debug.Log(log.ToString());
        }

        /// <summary>Glyph cells showing water rather than background. "Reads as empty" is a count.</summary>
        static int RunCells(string glyph)
        {
            int n = 0;
            var water = new[] { "⬜", "🟪", "🟩", "🟧" };
            foreach (var w in water)
            {
                int at = 0;
                while ((at = glyph.IndexOf(w, at, System.StringComparison.Ordinal)) >= 0) { n++; at += w.Length; }
            }
            return n;
        }

        static float SlopeAt(RillWorld w, Vector2 xz)
        {
            float slope;
            w.Field.DownhillWorld(xz.x, xz.y, out slope);
            return slope;
        }

        class StopStats
        {
            public int Count, InBasin;
            public float Slope, Water, Polish, Crawl, Volume;
        }

        static StringBuilder PlayBiome(int Runs, Biome biome, GameConfig config = null, Summary summary = null,
                                       int forcedCampaignBasin = -1)
        {
            var log = new StringBuilder();
            if (config == null) config = new GameConfig();
            config.Biome = biome;
            var world = RillWorld.Create(config, 20260726u, biome);

            log.AppendFormat("=== RILL headless smoke test — {0} ===\n", biome);
            log.AppendFormat("field {0}² at {1} m/cell = {2} m across\n", config.Size, config.CellSize, config.WorldExtent);
            log.AppendFormat("summit {0}  height {1:0.0} m\n", world.SummitCell, world.SummitWorld.y);
            log.AppendFormat("secrets placed {0}\n", world.Secrets.Count);
            log.AppendFormat("basins found {0}, capacity {1:n0} m³\n", world.Basins.Basins.Count, TotalCapacity(world));

            // Biome rules live in RunController.FinishRun, so no headless test had ever executed
            // them: glacier freeze/thaw, volcanic vents and granite spalling had run exactly zero
            // times in this project's entire history. A biome comparison without them measures
            // only generation, which is how four biomes could look "implemented" while three of
            // their behaviours had never been invoked once.
            var weather = new Rill.World.WeatherSystem(world.Seed);
            weather.Evaluate(System.DateTime.UtcNow);
            var biomeHeadlines = new List<string>();

            var sim = new FlowSimulation(world);
            // The dam break has never been counted anywhere. L-029 closed with the caveat that a
            // basin needs ~50 runs to fill, so nobody in a first session would ever see one; the
            // basins fill far faster now, so whether the cascade actually fires — and how often —
            // is a question this test can answer and never has. (L-019)
            int overflows = 0; float overflowVolume = 0f;
            world.BasinOverflowed += (b, excess) => { overflows++; overflowVolume += excess; };
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
            int crossingRuns = 0, crossingToSea = 0, aimedDelivered = 0, aimedEntered = 0, aimedPassedThrough = 0;
            int aimedWithRoom = 0, aimedEnteredWithRoom = 0;
            int campaignBasin = -1;
            float waterToBasins = 0f;
            float aimedClosest = 0f;
            float distanceAfterCrossing = 0f;
            float aimedMissDistance = 0f;
            float strandedVolume = 0f, totalDescent = 0f, totalStopSlope = 0f;
            int hollowsFilled = 0, runsThatFilled = 0;
            float hollowVolume = 0f;
            var stopBasinHits = new Dictionary<int, int>();
            var biomeHeadlineCounts = new Dictionary<string, int>();
            // A "Pooled" ending covers three different failures — sat down in a lake, sank into a
            // pit it dug, or seized up on a slope the terminal-speed identity says should still
            // carry it at ~6 m/s — and they need opposite fixes. Split the ending by what the
            // ground under the stop point was actually doing.
            var stopDetail = new Dictionary<RunEnding, StopStats>();
            var trace = new List<string>(128);
            int tracesPrinted = 0;

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
                    // A campaign, not a lottery. Picking a fresh random basin every run means no
                    // basin is ever worked at twice running, so no new route can ever be carved to
                    // one — and carving a route is the only way to reach a basin off the incised
                    // channel. A real player picks a target and keeps at it.
                    //
                    // Block size must divide the run count into at least one campaign per basin.
                    // At 50 runs over a 150-run test only basins 0-2 were ever aimed at, so the 0%
                    // sitting against 3 and 4 measured nothing but the test's own blind spot.
                    // Blocks of 50, i.e. three sustained campaigns across a 150-run test. Sized
                    // from what was measured, not by taste: ~36 aimed runs spread over all five
                    // basins gives ~7 each and fills none (25% hit, 2 of 5 basins wet), while one
                    // campaign of ~36 aimed runs took a basin from 0% to 85%. Filling an
                    // off-channel basin is a campaign, so the bot has to run campaigns or it is
                    // testing something nobody does. A 150-run test therefore only visits three of
                    // the five basins; the untargeted ones sitting at 0% mean nothing.
                    // Pick the campaign target by headroom, not by index. Cycling the index aimed
                    // 15 of 36 aimed runs at a basin that was already 100% full — basin #2 fills by
                    // run 24 on this seed — and a run aimed at a full basin cannot deliver anything
                    // however well it is flown. That made "aimed delivered 19%" partly a measure of
                    // the harness choosing impossible targets, and it silently got worse over a
                    // session precisely *because* the game was working. A player picks somewhere
                    // that still has room. (L-039)
                    if (forcedCampaignBasin >= 0) campaignBasin = forcedCampaignBasin;
                    else if (campaignBasin < 0 || (run - 1) % 50 == 0)
                    {
                        campaignBasin = 0;
                        float bestRoom = -1f;
                        for (int b = 0; b < world.Basins.Basins.Count; b++)
                        {
                            float room = world.Basins.Basins[b].Capacity - world.Basins.Basins[b].Volume;
                            if (room > bestRoom) { bestRoom = room; campaignBasin = b; }
                        }
                    }
                    intendedBasin = campaignBasin;
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

                // Volume in the target basin before the run. The existing "hit" test asks whether
                // the run STOPPED in the basin it was aimed at, which scores a run that delivers
                // water and flows onward as a miss — so it undercounts arrival by construction.
                float targetVolumeBefore = intendedBasin >= 0 && intendedBasin < world.Basins.Basins.Count
                    ? world.Basins.Basins[intendedBasin].Volume : 0f;
                // A campaign aimed at a basin that is already full cannot deliver anything, however
                // well it is flown. Basin #2 reaches 100% by run 24 on this seed, and the bot aims a
                // whole 50-run block at it, so a delivery rate quoted over all aimed runs is partly
                // measuring the harness picking impossible targets. (L-039)
                bool targetHadRoom = intendedBasin >= 0 && intendedBasin < world.Basins.Basins.Count &&
                    world.Basins.Basins[intendedBasin].Capacity - targetVolumeBefore > 1f;

                int steps = 0;
                float closestApproach = float.MaxValue;
                // Did the run ever get *inside* the basin it was aimed at? "Delivered" and "stopped
                // in it" both answer a different question, and neither can tell a run that never
                // arrived from one that arrived, sailed across the dry floor and left out the far
                // side. Those need opposite fixes, so the distinction has to be measured. (L-039)
                bool enteredTarget = false;
                // A once-a-second trace of the head, kept for every run and printed only for the
                // first couple that end badly. End-of-run aggregates said two contradictory things
                // about a TimedOut run — terminal speed 23 m/s where it stopped, 2.1 m/s averaged
                // over its 75 s — and no summary statistic can tell you which second went wrong.
                trace.Clear();
                while (sim.Running && steps++ < 20000)
                {
                    if (steps % 90 == 1)
                        trace.Add(string.Format("{0,5:0.0}s {1,6:0.0} m/s  vol {2,4:0} m³  h {3,6:0.0} m  at ({4,7:0.0},{5,7:0.0})  slope {6:0.00}  polish {7:0.00}  water {8:0.00}",
                            sim.Elapsed, sim.Head.Speed, sim.Head.Volume, sim.Head.Height,
                            sim.Head.Pos.x, sim.Head.Pos.y,
                            SlopeAt(world, sim.Head.Pos), world.Field.SamplePolishWorld(sim.Head.Pos.x, sim.Head.Pos.y),
                            world.Field.SampleWaterWorld(sim.Head.Pos.x, sim.Head.Pos.y)));
                    if (intentional)
                    {
                        float d = (sim.Head.Pos - destination).magnitude;
                        if (d < closestApproach) closestApproach = d;
                        if (intendedBasin >= 0 && !enteredTarget &&
                            world.Basins.BasinIdAt(world.Field.NearestIndex(sim.Head.Pos.x, sim.Head.Pos.y)) == intendedBasin)
                            enteredTarget = true;
                    }
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
                hollowsFilled += sim.HollowsFilled;
                hollowVolume += sim.HollowVolume;
                waterToBasins += sim.WaterToBasins;
                if (sim.HollowsFilled > 0) runsThatFilled++;
                if (sim.CrossedAnyBasin)
                {
                    crossingRuns++;
                    distanceAfterCrossing += sim.DistanceAfterCrossing;
                    if (sim.Ending == RunEnding.ReachedSea) crossingToSea++;
                }
                if (sim.Ending == RunEnding.TimedOut && tracesPrinted < 2)
                {
                    tracesPrinted++;
                    log.AppendFormat("--- trace of run {0} ({1}, {2:0} m travelled) ---\n", run, sim.Ending, sim.Distance);
                    for (int t = 0; t < trace.Count; t++) log.Append("    ").AppendLine(trace[t]);
                    log.AppendLine("--- end trace ---");
                }

                if (!stopDetail.ContainsKey(sim.Ending)) stopDetail[sim.Ending] = new StopStats();
                {
                    var ss = stopDetail[sim.Ending];
                    ss.Count++;
                    ss.Slope += sim.SlopeAtEnd;
                    ss.Water += sim.WaterAtEnd;
                    ss.Polish += sim.PolishAtEnd;
                    ss.Crawl += sim.CrawlSeconds;
                    ss.Volume += sim.VolumeAtEnd;
                    if (stopBasin != null) ss.InBasin++;
                }

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
                    // Basin ids come from a rescan each run. The count has been stable at 5 on this
                    // seed, but if it ever moves this comparison silently changes meaning.
                    if (intendedBasin < world.Basins.Basins.Count)
                    {
                        float after = world.Basins.Basins[intendedBasin].Volume;
                        if (after - targetVolumeBefore > 0.5f) aimedDelivered++;
                    }
                    // Closest approach separates "never got near it" from "passed it and carried
                    // on". A final-distance miss cannot tell those apart, and they need opposite
                    // fixes: more steering authority vs a reason to stop once you arrive.
                    aimedClosest += closestApproach;
                    if (enteredTarget) aimedEntered++;
                    if (targetHadRoom) aimedWithRoom++;
                    if (targetHadRoom && enteredTarget) aimedEnteredWithRoom++;
                    if (enteredTarget && (stopBasin == null || stopBasin.Id != intendedBasin)) aimedPassedThrough++;
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
                biomeHeadlines.Clear();
                Rill.World.BiomeRules.BetweenRuns(world, weather, biomeHeadlines);
                for (int h = 0; h < biomeHeadlines.Count; h++)
                    if (!biomeHeadlineCounts.ContainsKey(biomeHeadlines[h])) biomeHeadlineCounts[biomeHeadlines[h]] = 1;
                    else biomeHeadlineCounts[biomeHeadlines[h]]++;

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

            if (summary != null)
            {
                foreach (var kv in endings)
                {
                    if (kv.Key == RunEnding.ReachedSea) summary.Sea = kv.Value;
                    else if (kv.Key == RunEnding.TimedOut) summary.TimedOut = kv.Value;
                    else if (kv.Key == RunEnding.Pooled) summary.Pooled = kv.Value;
                }
                summary.StoppedInBasin = stoppedInBasin;
                summary.DistancePerRun = totalDistance / Runs;
                summary.Descent = totalDescent / Runs;
                summary.SedimentPerRun = totalSediment / Runs;
                summary.ToSea = toSea;
                summary.AimedRuns = aimedRuns;
                summary.AimedDelivered = aimedDelivered;
                summary.AimedMiss = aimedRuns > aimedHits ? aimedMissDistance / (aimedRuns - aimedHits) : 0f;
                summary.AimedClosest = aimedRuns > 0 ? aimedClosest / aimedRuns : 0f;
                summary.HollowVolume = hollowVolume;
                summary.AimedEntered = aimedEntered;
                summary.HeldWater = world.Basins.TotalWater();
                if (forcedCampaignBasin >= 0 && forcedCampaignBasin < world.Basins.Basins.Count)
                {
                    summary.TargetFill = world.Basins.Basins[forcedCampaignBasin].FillFraction;
                    summary.TargetVolume = world.Basins.Basins[forcedCampaignBasin].Volume;
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
            foreach (var kv in stopDetail)
            {
                var s = kv.Value;
                // Terminal speed on this ground, from the identity the flow constants were chosen
                // from: g*sin(theta)/drag. If a run stopped where this number is several m/s, the
                // stop is not physics — it is something eating the momentum that is not in the model.
                float sl = s.Slope / s.Count;
                float drag = Mathf.Lerp(config.DragFresh, config.DragPolished, s.Polish / s.Count)
                             + (s.Water / s.Count > 0.05f ? 2.5f : 0f);
                float terminal = config.Gravity * sl / Mathf.Sqrt(1f + sl * sl) / Mathf.Max(drag, 1e-3f);
                log.AppendFormat("    {0,-11} x{1,-3} in basin {2,-3} slope {3:0.000}  water {4:0.00} m  polish {5:0.00}  " +
                                 "terminal {6:0.0} m/s  crawled {7:0.0}s  left {8:0} m³\n",
                    kv.Key, s.Count, s.InBasin, sl, s.Water / s.Count, s.Polish / s.Count,
                    terminal, s.Crawl / s.Count, s.Volume / s.Count);
            }
            log.AppendFormat("  steps in water   {0:n0}, of which through-flow {1:n0}\n", inWater, throughFlow);
            log.AppendFormat("  hollows filled   {0} across {1} of {2} runs, {3:n0} m³ of the runs' own water spent ({4:0.0}/run)\n",
                hollowsFilled, runsThatFilled, Runs, hollowVolume, hollowVolume / Runs);
            log.AppendFormat("  basin crossings  {0} runs crossed a full lake; {1} of those reached the sea; avg {2:0} m travelled after crossing\n",
                crossingRuns, crossingToSea, crossingRuns > 0 ? distanceAfterCrossing / crossingRuns : 0f);
            log.AppendFormat("  aimed at a basin {0} runs, reached it {1} ({2:0}%), avg miss {3:0} m\n",
                aimedRuns, aimedHits, aimedRuns > 0 ? aimedHits * 100f / aimedRuns : 0f,
                aimedRuns > aimedHits ? aimedMissDistance / (aimedRuns - aimedHits) : 0f);
            log.AppendFormat("  aimed delivered  {0} of {1} aimed runs put water in the target ({2:0}%)\n",
                aimedDelivered, aimedRuns, aimedRuns > 0 ? aimedDelivered * 100f / aimedRuns : 0f);
            log.AppendFormat("  aimed entered    {0} of {1} got inside the target basin; {2} of those sailed across and left\n",
                aimedEntered, aimedRuns, aimedPassedThrough);
            log.AppendFormat("  aimed answerable {0} of {1} were aimed at a basin that had room; {2} of those got inside it\n",
                aimedWithRoom, aimedRuns, aimedEnteredWithRoom);
            log.AppendFormat("  fed in passing   {0:n0} m³ left in basins the runs flowed over ({1:0.0}/run)\n",
                waterToBasins, waterToBasins / Runs);
            log.AppendFormat("  aimed closest    avg {0:0} m (vs avg final miss above)\n",
                aimedRuns > 0 ? aimedClosest / aimedRuns : 0f);
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
                // The load-bearing structural question, and it has never been asked: is the sea
                // reachable from the spring at all without climbing? "Water reaches the sea 2 times
                // in 24" is a tuning result if the answer is yes and a generation bug if it is no,
                // and every flow constant in the game would be tuned against the wrong problem.
                int seaCells = 0, reachedSeaCells = 0;
                for (int i = 0; i < world.Field.Count; i++)
                {
                    if (world.Field.Height[i] > world.Field.SeaLevel + config.SeaMargin) continue;
                    seaCells++;
                    if (reach[i]) reachedSeaCells++;
                }
                log.AppendFormat("  sea (climb {0:0} m): {1} of {2:n0} shore cells reachable from the spring\n",
                    climb, reachedSeaCells, seaCells);
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
            log.AppendFormat("  dam breaks       {0} overflows, {1:n0} m³ over the lip\n", overflows, overflowVolume);
            log.AppendFormat("  fullest basin    {0:0.0}%\n", FullestBasin(world) * 100f);
            log.AppendFormat("  polished cells   {0} ({1:0.0}% of field)\n", PolishedCells(world), PolishedCells(world) * 100f / world.Field.Count);
            {
                // Polish decays between runs; the cut does not. If old channels are to read as
                // channels with no water in them (L-015), the permanent record is this, not polish,
                // and the cue is worth nothing if there is nothing to draw.
                int half = 0, deep = 0;
                for (int i = 0; i < world.Field.Count; i++)
                {
                    float cut = world.Field.Virgin[i] - world.Field.Height[i];
                    if (cut > 0.5f) half++;
                    if (cut > 1.5f) deep++;
                }
                log.AppendFormat("  incised cells    {0:n0} cut over 0.5 m, {1:n0} over 1.5 m ({2:0.0}% / {3:0.0}% of field)\n",
                    half, deep, half * 100f / world.Field.Count, deep * 100f / world.Field.Count);
            }
            log.AppendFormat("  secrets revealed {0} of {1}\n", RevealedCount(world), world.Secrets.Count);
            {
                // "0 revealed" has two opposite causes: sites the water never crosses (a placement
                // problem) and sites it crosses but does not cut deeply enough (a pricing problem).
                int touched = 0;
                float sumNeed = 0f, sumBest = 0f;
                for (int i = 0; i < world.Secrets.Count; i++)
                {
                    var s = world.Secrets[i];
                    float best = 0f;
                    int n = world.Field.Size, cx = s.Cell % n, cz = s.Cell / n;
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int x = cx + dx, z = cz + dz;
                            if (x < 0 || z < 0 || x >= n || z >= n) continue;
                            int c = z * n + x;
                            float e = world.Field.Virgin[c] - world.Field.Height[c];
                            if (e > best) best = e;
                        }
                    if (best > 0.05f) touched++;
                    sumBest += best;
                    sumNeed += world.Field.Virgin[s.Cell] - s.RevealElevation;
                }
                log.AppendFormat("  secret sites     {0} of {1} touched by any erosion; avg best cut {2:0.00} m vs avg needed {3:0.00} m\n",
                    touched, world.Secrets.Count, sumBest / Mathf.Max(1, world.Secrets.Count), sumNeed / Mathf.Max(1, world.Secrets.Count));
            }
            log.Append("  basin lattice:  ");
            for (int i = 0; i < world.Basins.Basins.Count; i++)
            {
                var b = world.Basins.Basins[i];
                if (b.Capacity < 80f) continue;
                log.AppendFormat("{0:0}%/{1:n0}m³  ", b.FillFraction * 100f, b.Capacity);
            }
            log.AppendLine();
            log.AppendFormat("  terrain delta    min {0:0.00} m, max {1:0.00} m vs virgin\n", MinDelta(world), MaxDelta(world));
            {
                // A single "max +8.87 m" is the same number for a delta and for a silt wall across
                // the runout, and those are a feature and a defect respectively. Footprint tells
                // them apart: a delta is one connected mass low down near the water; a wall is a
                // ridge, and scattered lumps are neither. (L-041)
                var f = world.Field;
                var seen = new bool[f.Count];
                int cells = 0, masses = 0, biggest = 0;
                float lowest = float.MaxValue, highest = float.MinValue, biggestElev = 0f;
                var q = new Queue<int>();
                for (int i = 0; i < f.Count; i++)
                {
                    if (seen[i] || f.Height[i] - f.Virgin[i] < 2f) continue;
                    int size = 0; float elevSum = 0f;
                    seen[i] = true; q.Clear(); q.Enqueue(i);
                    while (q.Count > 0)
                    {
                        int c = q.Dequeue();
                        size++; cells++;
                        elevSum += f.Height[c];
                        if (f.Height[c] < lowest) lowest = f.Height[c];
                        if (f.Height[c] > highest) highest = f.Height[c];
                        int cx = c % f.Size, cz = c / f.Size;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                            int nz = cz + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            if (!f.InBounds(nx, nz)) continue;
                            int ni = nz * f.Size + nx;
                            if (seen[ni] || f.Height[ni] - f.Virgin[ni] < 2f) continue;
                            seen[ni] = true; q.Enqueue(ni);
                        }
                    }
                    masses++;
                    if (size > biggest) { biggest = size; biggestElev = elevSum / size; }
                }
                if (cells == 0) log.AppendLine("  deposits         nothing stands 2 m above virgin rock");
                else
                    log.AppendFormat("  deposits         {0:n0} cells over 2 m above virgin in {1} masses; largest {2:n0} cells ({3:n0} m²) at {4:0} m elevation; spread {5:0}-{6:0} m\n",
                        cells, masses, biggest, biggest * f.CellSize * f.CellSize, biggestElev, lowest, highest);
            }
            {
                // Closed depressions too small for the basin lattice to name (under 24 cells). The
                // lattice ignores them on purpose — 47 nameless puddles is not a progression track —
                // but the flow simulation does not, and a head that falls into one has no fill-and-
                // spill to get it out again. They are the traps, and nothing has ever counted them.
                var filled = world.Basins.FilledSurface;
                int cells = 0; float deepest = 0f;
                for (int i = 0; i < world.Field.Count; i++)
                {
                    if (world.Field.Height[i] <= world.Field.SeaLevel) continue;
                    if (world.Basins.BasinIdAt(i) >= 0) continue;
                    float d = filled[i] - world.Field.Height[i];
                    if (d <= 0.15f) continue;
                    cells++;
                    if (d > deepest) deepest = d;
                }
                log.AppendFormat("  unnamed sinks    {0:n0} cells outside every named basin hold water, deepest {1:0.00} m\n",
                    cells, deepest);
            }
            log.AppendFormat("  weather          {0}\n", weather.Kind);
            if (biomeHeadlineCounts.Count == 0) log.AppendLine("  biome events     NONE — biome rules produced nothing");
            else
            {
                log.Append("  biome events    ");
                foreach (var kv in biomeHeadlineCounts) log.AppendFormat("\"{0}\" x{1}  ", kv.Key, kv.Value);
                log.AppendLine();
            }
            log.AppendFormat("  ice cells        {0}\n", IceCells(world));

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

            // A Daily is three runs, not the whole session, so the glyph rendered from every run
            // of a 150-run test is not the share unit anybody will ever see. Print both: the real
            // case first, and the density of each, because "reads as empty" is a count.
            var lastThree = new List<List<Vector3>>();
            var lastThreeSea = new List<bool>();
            for (int i = Mathf.Max(0, dailyPaths.Count - DailyRill.RunsPerDay); i < dailyPaths.Count; i++)
            {
                lastThree.Add(dailyPaths[i]);
                lastThreeSea.Add(dailySea[i]);
            }
            string daily = GlyphGenerator.Render(lastThree, lastThreeSea, world.Field.WorldExtent, world.Field);
            string glyph = GlyphGenerator.Render(dailyPaths, dailySea, world.Field.WorldExtent, world.Field);
            log.AppendFormat("  daily glyph ({0} runs, the real share unit) — {1} of {2} cells carry a run:\n",
                lastThree.Count, RunCells(daily), GlyphGenerator.Grid * GlyphGenerator.Grid);
            log.AppendLine(daily);
            log.AppendFormat("  whole session ({0} runs) — {1} of {2}:\n",
                dailyPaths.Count, RunCells(glyph), GlyphGenerator.Grid * GlyphGenerator.Grid);
            log.AppendLine(glyph);

            return log;
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

        static int IceCells(RillWorld w)
        {
            int n = 0;
            for (int i = 0; i < w.Field.Count; i++) if (w.Field.Ice[i] > 0.5f) n++;
            return n;
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
