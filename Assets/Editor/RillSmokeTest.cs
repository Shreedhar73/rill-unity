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
        /// A season, not a session. The lattice loses a basin to the river's own deposits somewhere
        /// inside 150 runs, and the design expects the same mountain to be played for months — so
        /// the question "does reachability stabilise or keep falling" cannot be answered at session
        /// length. (L-042)
        /// </summary>
        [MenuItem("RILL/Run Headless Smoke Test (season)", false, 62)]
        public static void RunHeadlessSeason() { Play(500); }

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
                    world.Basins.Rebuild();
                    world.EndRun(sim.Ending, sim.Elapsed, sim.Distance, sim.TopSpeed, sim.WaterToSea);
                    world.ApplyBetweenRunDrift();
                    Rill.World.BiomeRules.BetweenRuns(world, weather, headlines);
                }

                log.AppendFormat("after 12 runs of {0,-9} ice cells {1,5}  basin water {2,7:n0} m³   headlines: {3}\n",
                    weather.Kind, IceCells(world), world.Basins.TotalWater(),
                    headlines.Count == 0 ? "NONE" : string.Join(" | ", headlines.ToArray()));
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// The camera against every biome's topology. RillCamera's framing was pure distance-and-
        /// height arithmetic with no idea terrain existed; on the Sandstone slot it happened to
        /// work, and on Glacier and Volcanic it walked inside ridges — reported as "the camera
        /// enters the mountains" on the second and third slots. This drives the real follow, title
        /// and report framings over real runs on all three slot biomes and counts, per biome, how
        /// many frames the naive framing was inside rock or looking through a hill, then asserts
        /// the clamped framing (RillCamera.RequiredCameraY) never is.
        /// </summary>
        [MenuItem("RILL/Run Headless Camera Test", false, 72)]
        public static void RunHeadlessCamera()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RILL camera vs terrain ===");
            int pass = 0, fail = 0;
            System.Action<bool, string> check = (ok, what) =>
            {
                if (ok) { pass++; log.AppendFormat("  ok    {0}\n", what); }
                else { fail++; log.AppendFormat("  FAIL  {0}\n", what); }
            };

            // Defaults mirrored from RillCamera. If these drift the test framings go stale, but
            // reading them off a MonoBehaviour would need a scene, which is the thing this test
            // exists to avoid.
            const float Yaw = 30f, FollowDist = 62f, FollowHeight = 46f;
            const float CloseIn = 0.22f, SpeedRef = 24f, Lookahead = 0.65f;
            const float Clearance = 10f;
            const float TitleDist = 190f, TitleHeight = 78f, ReportDist = 115f;

            var slots = new[]
            {
                new { Biome = Biome.Sandstone, Seed = 12345u },
                new { Biome = Biome.Glacier,   Seed = 777001u },
                new { Biome = Biome.Volcanic,  Seed = 424243u },
            };

            int naiveTotal = 0;
            foreach (var slot in slots)
            {
                var config = new GameConfig { Biome = slot.Biome };
                var world = RillWorld.Create(config, slot.Seed, slot.Biome);
                System.Func<float, float, float> ground = world.Field.SampleHeightWorld;

                int frames = 0, naiveBad = 0, clampedBad = 0;
                float maxLift = 0f;
                var sim = new FlowSimulation(world);

                for (int run = 1; run <= 8; run++)
                {
                    world.BeginRun();
                    var rng = new Rng(Noise.Hash((uint)run * 2654435761u ^ world.Seed));
                    sim.Begin(world.SpawnPoint(ref rng), config.StartVolume);
                    int steps = 0;
                    while (sim.Running && steps++ < 20000)
                    {
                        if (steps % 30 == 0)
                            sim.SetSteer(rng.Next01() < 0.45f,
                                sim.Head.Pos + new Vector2(rng.Range(-25f, 25f), rng.Range(-25f, 25f)));
                        sim.Advance(config.SimStep);

                        // Every 5th step, frame the head exactly as RillCamera.Follow would.
                        if (steps % 5 != 0) continue;
                        frames++;
                        Vector2 vel = sim.Head.Vel;
                        float speed01 = Mathf.Clamp01(vel.magnitude / SpeedRef);
                        Vector3 target = sim.Head.World
                                       + new Vector3(vel.x, 0f, vel.y) * Lookahead;
                        Vector3 back = Quaternion.Euler(0f, Yaw, 0f) * new Vector3(0f, 0f, -1f);
                        Vector3 pos = target + back * (FollowDist * (1f - CloseIn * speed01))
                                             + Vector3.up * (FollowHeight * (1f - CloseIn * speed01));
                        if (FramingBad(pos, target, ground, Clearance)) naiveBad++;
                        float need = Rill.Render.RillCamera.RequiredCameraY(pos, target, ground, Clearance);
                        maxLift = Mathf.Max(maxLift, need - pos.y);
                        if (need > pos.y) pos.y = need;
                        if (FramingBad(pos, target, ground, Clearance)) clampedBad++;
                    }
                    world.Basins.Rebuild();
                    world.EndRun(sim.Ending, sim.Elapsed, sim.Distance, sim.TopSpeed, sim.WaterToSea);
                }

                // The title orbit sweeps the full circle, so every yaw must clear the ridges.
                int titleNaiveBad = 0, titleClampedBad = 0;
                for (int yaw = 0; yaw < 360; yaw += 4)
                {
                    Vector3 target = world.SummitWorld;
                    Vector3 back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -1f);
                    Vector3 pos = target + back * TitleDist + Vector3.up * TitleHeight;
                    if (FramingBad(pos, target, ground, Clearance)) titleNaiveBad++;
                    float need = Rill.Render.RillCamera.RequiredCameraY(pos, target, ground, Clearance);
                    if (need > pos.y) pos.y = need;
                    if (FramingBad(pos, target, ground, Clearance)) titleClampedBad++;

                    // Report framing from the same angles, aimed at a low point rather than the
                    // summit — reports frame the deepest carve, which is never at the top.
                    Vector3 low = target * 0.5f;
                    low.y = ground(low.x, low.z);
                    Vector3 rpos = low + back * ReportDist + Vector3.up * (ReportDist * 0.7f);
                    if (FramingBad(rpos, low, ground, Clearance)) titleNaiveBad++;
                    float rneed = Rill.Render.RillCamera.RequiredCameraY(rpos, low, ground, Clearance);
                    if (rneed > rpos.y) rpos.y = rneed;
                    if (FramingBad(rpos, low, ground, Clearance)) titleClampedBad++;
                }

                naiveTotal += naiveBad + titleNaiveBad;
                log.AppendFormat("  {0,-9} follow frames {1,5}  naive bad {2,4}  clamped bad {3}  max lift {4:0.0} m   title/report naive bad {5}  clamped bad {6}\n",
                    slot.Biome, frames, naiveBad, clampedBad, maxLift, titleNaiveBad, titleClampedBad);
                check(clampedBad == 0, slot.Biome + " follow camera never enters terrain once clamped");
                check(titleClampedBad == 0, slot.Biome + " title and report framings clear the ridges once clamped");
            }

            // If the naive framing was never bad anywhere, this test is measuring nothing and a
            // regression in the clamp would be invisible — the count is the proof the clamp is
            // load-bearing.
            log.AppendFormat("  naive framing violations across all biomes: {0}\n", naiveTotal);
            check(naiveTotal > 0, "the unclamped framing does hit terrain somewhere, so the clamp is doing real work");

            log.AppendFormat("--- {0} passed, {1} failed ---\n", pass, fail);
            if (fail > 0) Debug.LogError(log.ToString()); else Debug.Log(log.ToString());
        }

        /// <summary>
        /// Independent check that a framing is acceptable: camera above ground, and the sight line
        /// to the subject clear of terrain (with the same taper RequiredCameraY solves against).
        /// Deliberately re-derived here rather than calling back into RillCamera, so a sign error
        /// in the solve cannot certify itself.
        /// </summary>
        static bool FramingBad(Vector3 pos, Vector3 target, System.Func<float, float, float> ground, float clearance)
        {
            if (pos.y < ground(pos.x, pos.z) + clearance - 0.05f) return true;
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 7f;
                if (t < 0.25f) continue;
                float x = Mathf.Lerp(target.x, pos.x, t);
                float z = Mathf.Lerp(target.z, pos.z, t);
                float rayY = target.y + (pos.y - target.y) * t;
                if (rayY < ground(x, z) + clearance * t - 0.05f) return true;
            }
            return false;
        }

        /// <summary>
        /// The time-lapse archive, round-tripped. The playback UI existed for weeks wired to an
        /// archive nobody had ever read back — the exact "built, never observed" shape — and the
        /// archive is append-only binary, where a silent format break costs the player their whole
        /// recorded history. (L-061)
        /// </summary>
        [MenuItem("RILL/Run Headless TimeLapse Test", false, 73)]
        public static void RunHeadlessTimeLapse()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RILL time-lapse archive ===");
            int pass = 0, fail = 0;
            System.Action<bool, string> check = (ok, what) =>
            {
                if (ok) { pass++; log.AppendFormat("  ok    {0}\n", what); }
                else { fail++; log.AppendFormat("  FAIL  {0}\n", what); }
            };

            // Scratch slot far outside anything a player can reach, wiped before and after.
            const int Scratch = 93;
            string path = System.IO.Path.Combine(SaveSystem.RootDir, "timelapse_" + Scratch + ".bin");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

            var world = RillWorld.Create(new GameConfig(), 555u, Biome.Sandstone);
            var archive = new TimeLapseArchive(Scratch);
            check(!archive.Exists, "a fresh slot has no archive");

            // Three keyframes with real, distinct terrain between them — a genuine gorge's worth
            // of change, so "the frames differ" is a claim about recording and not about noise.
            archive.Append(world.Field, 1);
            for (int i = 0; i < world.Field.Count; i++)
                if (world.Field.Height[i] > 20f) world.Field.Height[i] -= 2.5f;
            archive.Append(world.Field, 4);
            for (int i = 0; i < world.Field.Count; i++)
                if (world.Field.Height[i] > 60f) world.Field.Height[i] -= 4f;
            archive.Append(world.Field, 7);

            var frames = archive.LoadAll();
            check(frames.Count == 3, "three appends read back as three frames, got " + frames.Count);
            if (frames.Count == 3)
            {
                check(frames[0].Run == 1 && frames[1].Run == 4 && frames[2].Run == 7,
                      "run numbers survive: " + frames[0].Run + ", " + frames[1].Run + ", " + frames[2].Run);

                int differ01 = 0, differ12 = 0;
                for (int i = 0; i < frames[0].Data.Length; i++)
                {
                    if (System.Math.Abs(frames[0].HeightAt(i) - frames[1].HeightAt(i)) > 0.5f) differ01++;
                    if (System.Math.Abs(frames[1].HeightAt(i) - frames[2].HeightAt(i)) > 0.5f) differ12++;
                }
                check(differ01 > 100 && differ12 > 100,
                      "the frames record the terrain actually changing: " + differ01 + " and " + differ12 + " cells moved");

                // The last frame must reconstruct the current terrain to quantisation accuracy —
                // 16 bits over the height range, ~2 mm here. 0.1 m of slack covers downsampling.
                float worst = 0f;
                int res = TimeLapseArchive.Resolution;
                int step = world.Field.Size / res;
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                    {
                        float have = frames[2].HeightAt(z * res + x);
                        float sum = 0f; int n = 0;
                        for (int dz = 0; dz < step; dz++)
                            for (int dx = 0; dx < step; dx++)
                            {
                                int gz = z * step + dz, gx = x * step + dx;
                                if (gz >= world.Field.Size || gx >= world.Field.Size) continue;
                                sum += world.Field.Height[gz * world.Field.Size + gx]; n++;
                            }
                        float want = n > 0 ? sum / n : 0f;
                        worst = Mathf.Max(worst, Mathf.Abs(have - want));
                    }
                check(worst < 0.1f, string.Format("the last frame reconstructs the live terrain, worst error {0:0.000} m", worst));
            }

            // A truncated tail — the app killed mid-append — must not poison the whole history.
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Append))
                fs.Write(new byte[100], 0, 100);
            var withTail = archive.LoadAll();
            check(withTail.Count == 3, "a truncated tail write is dropped cleanly, " + withTail.Count + " frames survive");

            System.IO.File.Delete(path);
            log.AppendFormat("--- {0} passed, {1} failed ---\n", pass, fail);
            if (fail > 0) Debug.LogError(log.ToString()); else Debug.Log(log.ToString());
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
        /// Three mountains, and the guards that stop a slot picker becoming a new-game button
        /// standing next to three save files. Invariant 1 is absolute — "no new game that touches
        /// an existing slot" — so every way to lose a mountain is asserted against here rather
        /// than trusted to the UI.
        ///
        /// Uses high slot numbers so a real player's mountains can never be touched by the test.
        /// </summary>
        [MenuItem("RILL/Run Headless Mountains Test", false, 71)]
        public static void RunHeadlessMountains()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RILL three mountains ===");
            int pass = 0, fail = 0;
            System.Action<bool, string> check = (ok, what) =>
            {
                if (ok) { pass++; log.AppendFormat("  ok    {0}\n", what); }
                else { fail++; log.AppendFormat("  FAIL  {0}\n", what); }
            };

            // The roster addresses slots 0..2, which are a player's real saves. Redirect it by
            // clearing those slots only inside a scratch area would need plumbing; instead the test
            // asserts on behaviour it can reach without destroying anything, and does its
            // destructive work through SaveSystem on slots well out of range.
            const int ScratchA = 90, ScratchB = 91;
            SaveSystem.DeleteSlot(ScratchA);
            SaveSystem.DeleteSlot(ScratchB);

            var cfg = new GameConfig();

            SaveSystem.MountainSummary none;
            check(!SaveSystem.ReadSummary(ScratchA, out none), "an empty slot reports no summary");
            check(!none.Occupied, "and is not marked occupied");

            var made = RillWorld.Create(cfg, 4242u, Biome.Glacier);
            made.RunNumber = 17;
            made.LifetimeSediment = 1234f;
            made.LifetimeWaterToSea = 567f;
            SaveSystem.Save(made, new float[made.Field.Count], ScratchA);

            SaveSystem.MountainSummary got;
            check(SaveSystem.ReadSummary(ScratchA, out got), "a saved mountain reports a summary");
            check(got.Occupied && got.Seed == 4242u, "with the right seed");
            check(got.Biome == Biome.Glacier, "and the right biome, so the picker can name it");
            check(got.RunNumber == 17 && Mathf.Abs(got.LifetimeSediment - 1234f) < 0.5f,
                  "and the lifetime record, without deserialising the terrain");

            // The header read must not depend on the arrays that follow it.
            var big = RillWorld.Create(cfg, 99u, Biome.Volcanic);
            SaveSystem.Save(big, new float[big.Field.Count], ScratchB);
            SaveSystem.MountainSummary b;
            check(SaveSystem.ReadSummary(ScratchB, out b) && b.Seed == 99u && b.Biome == Biome.Volcanic,
                  "two slots hold two different mountains at once");

            SaveSystem.MountainSummary a2;
            SaveSystem.ReadSummary(ScratchA, out a2);
            check(a2.Seed == 4242u, "and writing the second did not touch the first");

            // The roster's guards, exercised on a roster pointed at real slots but only through
            // methods that cannot mutate: Create refuses, Delete refuses on a wrong seed.
            var roster = new MountainRoster();
            check(MountainRoster.Slots == 3, "the roster is three slots");
            for (int i = 0; i < MountainRoster.Slots; i++)
            {
                if (!roster.Occupied(i)) continue;
                uint realSeed = roster[i].Seed;
                check(roster.Create(i, Biome.Sandstone, 1u, cfg) == null,
                      "Create refuses an occupied slot outright — there is no overwrite path");
                check(!roster.Delete(i, realSeed ^ 1u),
                      "Delete refuses a seed that does not match the mountain in the slot");
                check(roster.Occupied(i), "and the mountain is still there afterwards");
                break;
            }
            if (roster.OccupiedCount == 0)
                log.AppendLine("  note  no real mountain saved, so the refuse-to-overwrite guards were not exercised");

            check(roster.FirstEmpty() >= 0 || roster.OccupiedCount == MountainRoster.Slots,
                  "FirstEmpty is a slot index or -1 when all three are taken");

            // Adopt is how an Expedition becomes one of the three. It must obey the same rule as
            // Create: no overwrite, anywhere, ever.
            {
                var visited = RillWorld.Create(cfg, 777u, Biome.Granite);
                visited.RunNumber = 7;
                var r2 = new MountainRoster();
                bool refusedAll = true;
                for (int i = 0; i < MountainRoster.Slots; i++)
                {
                    if (!r2.Occupied(i)) continue;
                    if (r2.Adopt(i, visited, null)) refusedAll = false;
                }
                check(refusedAll, "Adopt refuses every occupied slot — an expedition cannot replace a mountain");
                check(!r2.Adopt(0, null, null), "Adopt refuses a null world rather than writing a corrupt slot");
            }

            SaveSystem.DeleteSlot(ScratchA);
            SaveSystem.DeleteSlot(ScratchB);
            check(!SaveSystem.Exists(ScratchA) && !SaveSystem.Exists(ScratchB), "scratch slots cleaned up");

            log.AppendFormat("--- {0} passed, {1} failed ---\n", pass, fail);
            if (fail > 0) Debug.LogError(log.ToString()); else Debug.Log(log.ToString());
        }

        /// <summary>
        /// Every navigation transition, driven directly. The shell's state machine is deliberately
        /// MonoBehaviour-free so this can exist: UI cannot be checked from a terminal, and L-018 is
        /// what happens when the only thing that could catch a gate is a person pressing Play.
        ///
        /// Each case below is a way to strand the player, not a formality.
        /// </summary>
        [MenuItem("RILL/Run Headless Navigation Test", false, 70)]
        public static void RunHeadlessNavigation()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RILL navigation ===");
            int pass = 0, fail = 0;

            System.Action<bool, string> check = (ok, what) =>
            {
                if (ok) { pass++; log.AppendFormat("  ok    {0}\n", what); }
                else { fail++; log.AppendFormat("  FAIL  {0}\n", what); }
            };

            var nav = new Navigator();
            check(nav.Current == AppScreen.Launch, "boots into Launch");
            check(nav.Back() == NavAction.None, "Back during the launch does nothing");

            nav.FinishLaunch();
            check(nav.Current == AppScreen.Home, "launch hands off to Home");
            check(nav.Depth == 1, "Home is the root, not stacked on the launch");

            // The launch must not be re-enterable: an opening beat you can go back into is a menu,
            // and the second time you see it, it is an obstacle.
            nav.Push(AppScreen.Launch);
            check(nav.Current == AppScreen.Home, "Launch cannot be pushed again");

            nav.Push(AppScreen.Mountain);
            nav.Push(AppScreen.Almanac);
            check(nav.Depth == 3, "panels stack on the mountain");
            check(nav.Back() == NavAction.Changed && nav.Current == AppScreen.Mountain,
                  "Back from a panel returns to the mountain, not to Home");
            check(nav.Back() == NavAction.Changed && nav.Current == AppScreen.Home,
                  "Back from the mountain returns Home");

            // The case that eats a run. Back must not unwind the screen out from under a live
            // simulation: the water in the head has to be put somewhere first.
            nav.Push(AppScreen.Mountain);
            nav.RunInProgress = true;
            check(nav.Back() == NavAction.AbandonRun, "Back mid-run asks for the run to be abandoned");
            check(nav.Current == AppScreen.Mountain, "and does not move the player while it is in flight");
            nav.RunInProgress = false;
            check(nav.Back() == NavAction.Changed && nav.Current == AppScreen.Home,
                  "and works normally once the run has ended");

            // Depth is not a proxy for "have I been here before".
            nav.Push(AppScreen.Mountain);
            nav.Push(AppScreen.Almanac);
            nav.Push(AppScreen.Almanac);
            check(nav.Depth == 3, "pushing the screen you are already on is not two screens deep");

            // "End game" is GoHome from anywhere, however deep, however mid-run. It is not a quit:
            // the application keeps running and the mountain is still there when the player returns,
            // which is the entire premise.
            nav.Push(AppScreen.Mountain);
            nav.Push(AppScreen.Almanac);
            nav.RunInProgress = true;
            nav.GoHome();
            check(nav.Current == AppScreen.Home && nav.Depth == 1,
                  "End game returns to the main screen from any depth, even mid-run");
            nav.RunInProgress = false;

            check(nav.Back() == NavAction.Quit, "Back at the root asks to quit where that is allowed");
            nav.CanQuit = false;
            check(nav.Back() == NavAction.None, "and does nothing on a platform that must not quit");
            check(!nav.CanGoBack, "Back is not offered at the root when quitting is not allowed");

            // The other half of "Back mid-run": the water. Abort() fell through both branches of
            // Finish and zeroed the head's volume, destroying it — invariant 6, and unnoticed only
            // because Abort() had no callers at all until a back button needed one.
            {
                var cfg = new GameConfig();
                var world = RillWorld.Create(cfg, cfg.Seed, cfg.Biome);
                var sim = new FlowSimulation(world);

                world.BeginRun();
                var rng = new Rng(world.Seed);
                sim.Begin(world.SpawnPoint(ref rng), cfg.StartVolume);
                for (int i = 0; i < 300 && sim.Running; i++) sim.Advance(cfg.SimStep);

                float held = world.Basins.TotalWater();
                float inHead = sim.Head.Volume;
                sim.Abort();
                world.Basins.Rebuild();
                float after = world.Basins.TotalWater();

                check(inHead > 1f, string.Format("the aborted run still had water in it ({0:0.0} m³)", inHead));
                check(after > held + 1f,
                      string.Format("abandoning a run leaves its water on the mountain ({0:0} -> {1:0} m³ held)", held, after));
            }

            log.AppendFormat("--- {0} passed, {1} failed ---\n", pass, fail);
            if (fail > 0) Debug.LogError(log.ToString()); else Debug.Log(log.ToString());
        }

        /// <summary>
        /// The headline numbers of a session, so one tuning constant can be swept without reading
        /// forty lines of prose per arm.
        /// </summary>
        public class Summary
        {
            public int Sea, TimedOut, Pooled, AimedRuns, AimedDelivered, StoppedInBasin, AimedEntered;
            public int BasinCountMin, BasinCountMax, BasinCountEnd;
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
            log.AppendLine("=== one sustained campaign against basin #3, 500 runs ===");
            log.Append(PlayBiome(500, Biome.Sandstone, null, null, 3));
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

        /// <summary>
        /// One sustained campaign per basin, over a season. At run 500 four basins sit at 100% and
        /// one at 0%, which reads as a hole in the progression track — but the bot picks its target
        /// by largest headroom, so it spends almost the whole session on the 2,038 m³ basin and only
        /// turns to the 363 m³ one at the very end. That is exactly the shape of the three previous
        /// occasions where the harness was the answer, so it gets tested before anything is
        /// designed. (L-043)
        /// </summary>
        [MenuItem("RILL/Run Headless Campaign — every basin", false, 69)]
        public static void RunHeadlessCampaignEachBasin()
        {
            var log = new StringBuilder();
            log.AppendLine("=== one sustained 500-run campaign per basin, fresh mountain each time ===");
            log.AppendLine("  target   final fill   held        entered  delivered   sea   dist/run   basins min-max (end)");
            for (int b = 0; b < 5; b++)
            {
                var sum = new Summary();
                PlayBiome(500, Biome.Sandstone, null, sum, b);
                log.AppendFormat("  #{0}     {1,7:0.0}%  {2,7:n0} m3  {3,6}/{4}  {5,6}/{4}  {6,4}  {7,7:0} m   {8}-{9} ({10})\n",
                    b, sum.TargetFill * 100f, sum.TargetVolume, sum.AimedEntered, sum.AimedRuns,
                    sum.AimedDelivered, sum.Sea, sum.DistancePerRun,
                    sum.BasinCountMin, sum.BasinCountMax, sum.BasinCountEnd);
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
            // Counted off the event as well as off the headline, because those are two different
            // claims: one says the world raised it, the other says the player would have been told.
            int lostEvents = 0, mergeEvents = 0, latticeShown = 0;
            world.Basins.Lost += (name, vol) => lostEvents++;
            world.Basins.Merged += (oldNames, survivor) => mergeEvents++;
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
            int basinCountMin = int.MaxValue, basinCountMax = 0;
            float waterToBasins = 0f;
            float aimedClosest = 0f;
            float distanceAfterCrossing = 0f;
            float aimedMissDistance = 0f;
            float strandedVolume = 0f, totalDescent = 0f, totalStopSlope = 0f;
            int hollowsFilled = 0, runsThatFilled = 0;
            float hollowVolume = 0f;
            var stopBasinHits = new Dictionary<int, int>();
            var biomeHeadlineCounts = new Dictionary<string, int>();
            int teaserRuns = 0;
            var teaserLines = new Dictionary<string, int>();
            var worldHeadlines = new Dictionary<string, int>();
            // A "Pooled" ending covers three different failures — sat down in a lake, sank into a
            // pit it dug, or seized up on a slope the terminal-speed identity says should still
            // carry it at ~6 m/s — and they need opposite fixes. Split the ending by what the
            // ground under the stop point was actually doing.
            var stopDetail = new Dictionary<RunEnding, StopStats>();
            var trace = new List<string>(128);
            int tracesPrinted = 0;
            var reachHistory = new StringBuilder();

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
                if (world.Basins.Basins.Count < basinCountMin) basinCountMin = world.Basins.Basins.Count;
                if (world.Basins.Basins.Count > basinCountMax) basinCountMax = world.Basins.Basins.Count;

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
                    // Basin ids come from a rescan every run, and the count is NOT stable: terrain
                    // moves, depressions merge and split, and a 500-run season can end with fewer
                    // basins than it started with. An index-based campaign target therefore has to
                    // be range-checked every run — it threw ArgumentOutOfRangeException the first
                    // time this was run over a season — and any index-based comparison in this file
                    // is only meaningful while basinCountMin == basinCountMax.
                    if (forcedCampaignBasin >= 0)
                        campaignBasin = Mathf.Min(forcedCampaignBasin, world.Basins.Basins.Count - 1);
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

                // Rebuild BEFORE EndRun, which is the order RunController.FinishRun uses. The test
                // had them the other way round, and it silently threw away every headline the
                // rebuild raises: they land in the world's pending list and the next BeginRun
                // clears it before anything reads it. That is why "basins silted up 0" was printed
                // for a season in which the lattice demonstrably went from 5 basins to 3 — the
                // detection was fine and the harness was reading it a run too early.
                //
                // Terrain moved during the run, so the depression map is stale until this runs;
                // rebuilding first is also what makes the report's basin fill percentages true.
                world.Basins.Rebuild();
                var rep = world.EndRun(sim.Ending, sim.Elapsed, sim.Distance, sim.TopSpeed, sim.WaterToSea);
                world.ApplyBetweenRunDrift();
                biomeHeadlines.Clear();
                Rill.World.BiomeRules.BetweenRuns(world, weather, biomeHeadlines);
                for (int h = 0; h < biomeHeadlines.Count; h++)
                    if (!biomeHeadlineCounts.ContainsKey(biomeHeadlines[h])) biomeHeadlineCounts[biomeHeadlines[h]] = 1;
                    else biomeHeadlineCounts[biomeHeadlines[h]]++;

                // Headlines the world itself raised, as opposed to the biome rules. Overflows and
                // basins silting out of existence come through here, and neither had ever been
                // counted — a basin can vanish under a campaign and nothing said so. (L-044)
                // The headline existing is not the same claim as the player being told. Summary()
                // is the card's title — the one sentence they cannot miss — and it did not surface
                // a lattice change at all until this was checked. HudController also lists every
                // entry in rep.Headlines in the card body, so a change that loses the title to a
                // dam break is still on screen; this counts the stronger claim.
                if (!string.IsNullOrEmpty(rep.LatticeChange) && rep.Summary() == rep.LatticeChange) latticeShown++;

                for (int h = 0; h < rep.Headlines.Count; h++)
                {
                    string key = rep.Headlines[h];
                    if (!worldHeadlines.ContainsKey(key)) worldHeadlines[key] = 1;
                    else worldHeadlines[key]++;
                }

                // The end-card teaser, computed at the same point in the run lifecycle as
                // RunController does it (after drift and biome rules). Counted because a teaser
                // that never fires is a system that silently does nothing — the exact failure
                // mode this project keeps hitting. (L-060)
                string tease = Rill.Meta.NextTeaser.For(world);
                if (tease != null)
                {
                    teaserRuns++;
                    if (!teaserLines.ContainsKey(tease)) teaserLines[tease] = 1;
                    else teaserLines[tease]++;
                }

                if (!endings.ContainsKey(sim.Ending)) endings[sim.Ending] = 0;
                endings[sim.Ending]++;
                totalSediment += rep.SedimentMoved;
                totalDistance += rep.DistanceTravelled;
                toSea += rep.WaterToSea;
                if (rep.TopSpeed > bestSpeed) bestSpeed = rep.TopSpeed;

                dailyPaths.Add(new List<Vector3>(sim.Path));
                dailySea.Add(sim.Ending == RunEnding.ReachedSea);

                // Reachability of the lattice, sampled as the session goes rather than only at the
                // end. It was 5 of 5 at generation and 4 of 5 after 150 runs — the river silts up
                // its own approaches — and a single end-of-test number cannot say when, how fast,
                // or whether anything ever reopens. (L-042)
                if (run == 1 || run % 25 == 0 || run == Runs)
                {
                    var probeRng = new Rng(world.Seed);
                    Vector3 probeSpawn = world.SpawnPoint(ref probeRng);
                    var now = DownhillReachable(world, probeSpawn, 0f);
                    var virgin = DownhillReachable(world, probeSpawn, 0f, world.Field.Virgin);
                    // Strictly-downhill is a lower bound the simulation never obeys: water here
                    // tops 25 m/s and v²/2g at that speed is tens of metres of climb. Reporting
                    // only the strict number would make a lattice the player can still reach on
                    // momentum look like one they have lost.
                    var withRun = DownhillReachable(world, probeSpawn, 3f);
                    int okNow = 0, okVirgin = 0, okMomentum = 0, lostAndWanted = 0;
                    var lost = new StringBuilder();
                    for (int i = 0; i < world.Basins.Basins.Count; i++)
                    {
                        var b = world.Basins.Basins[i];
                        bool a = false, v = false;
                        for (int k = 0; k < b.Cells.Length && !(a && v); k++)
                        {
                            if (now[b.Cells[k]]) a = true;
                            if (virgin[b.Cells[k]]) v = true;
                        }
                        if (a) okNow++;
                        if (v) okVirgin++;
                        for (int k = 0; k < b.Cells.Length; k++)
                            if (withRun[b.Cells[k]]) { okMomentum++; break; }
                        // A basin the water can no longer get to only costs the player something if
                        // it still had room. Losing the approach to a lake that is already full is
                        // the mountain finishing with it, not the progression track dying.
                        if (!a)
                        {
                            lost.AppendFormat("#{0} {1:0}%  ", i, b.FillFraction * 100f);
                            if (b.FillFraction < 0.99f) lostAndWanted++;
                        }
                    }
                    reachHistory.AppendFormat("    run {0,4}   downhill {1}/{2}, on momentum {6}/{2}, on virgin rock {3}/{2}   no downhill route: {4}{5}\n",
                        run, okNow, world.Basins.Basins.Count, okVirgin,
                        lost.Length == 0 ? "none" : lost.ToString(),
                        lostAndWanted > 0 ? "  <- " + lostAndWanted + " still had room" : "", okMomentum);
                }

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
                summary.BasinCountMin = basinCountMin;
                summary.BasinCountMax = basinCountMax;
                summary.BasinCountEnd = world.Basins.Basins.Count;
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
            log.Append("  lattice reachability over the session (downhill only / allowing a 3 m climb on momentum):\n").Append(reachHistory);
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
                    // Ids were collected across the whole session against a list that is rebuilt
                    // every run, and the count is not stable — it threw here on the first 500-run
                    // season. An id that no longer exists is reported as such rather than dropped,
                    // because a basin disappearing mid-session is a fact worth seeing.
                    if (kv.Key >= world.Basins.Basins.Count)
                    {
                        log.AppendFormat("#{0} (gone) x{1}  ", kv.Key, kv.Value);
                        continue;
                    }
                    var b = world.Basins.Basins[kv.Key];
                    log.AppendFormat("#{0} \"{1}\" x{2} ({3:0}% full)  ", kv.Key, b.Name, kv.Value, b.FillFraction * 100f);
                }
                log.AppendLine();
            }
            log.AppendFormat("  water held       {0:n0} m³ across {1} basins\n", world.Basins.TotalWater(), world.Basins.Basins.Count);
            log.AppendFormat("  basin count      {0}{1}\n", basinCountMin,
                basinCountMin == basinCountMax ? " throughout — index-based comparisons are sound"
                                               : "-" + basinCountMax + " — IT MOVED, every index-based number above is suspect");
            log.AppendFormat("  dam breaks       {0} overflows, {1:n0} m³ over the lip\n", overflows, overflowVolume);
            {
                int silted = 0, merged = 0;
                foreach (var kv in worldHeadlines)
                {
                    if (kv.Key.Contains("silted up")) silted += kv.Value;
                    if (kv.Key.Contains("one lake now")) merged += kv.Value;
                }
                log.AppendFormat("  lattice changes  {0} silted out of existence, {1} merges — raised by the world: {2} and {3}; as the card's title {4} (all appear in its body)\n",
                    silted, merged, lostEvents, mergeEvents, latticeShown);
                foreach (var kv in worldHeadlines)
                    if (kv.Key.Contains("silted up") || kv.Key.Contains("one lake now"))
                        log.AppendFormat("      \"{0}\" x{1}\n", kv.Key, kv.Value);
            }
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

            // The end-card teaser (L-060). Zero firings over a whole session would mean the card
            // never has a "next" to offer, which is the silent-nothing failure; every run firing
            // would mean it is wallpaper. Both are visible here.
            if (teaserRuns == 0) log.AppendFormat("  next teaser      NONE in {0} runs — the card never had a next to offer\n", Runs);
            else
            {
                log.AppendFormat("  next teaser      on {0} of {1} runs: ", teaserRuns, Runs);
                foreach (var kv in teaserLines) log.AppendFormat("\"{0}\" x{1}  ", kv.Key, kv.Value);
                log.AppendLine();
            }

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
            return DownhillReachable(w, spawn, climb, w.Field.Height);
        }

        static bool[] DownhillReachable(RillWorld w, Vector3 spawn, float climb, float[] h)
        {
            int n = w.Field.Size;
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
