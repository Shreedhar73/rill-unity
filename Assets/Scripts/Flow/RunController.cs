using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Rill.App;
using Rill.Audio;
using Rill.Core;
using Rill.InputSystem;
using Rill.Meta;
using Rill.Render;
using Rill.UI;
using Rill.World;

namespace Rill.Flow
{
    /// <summary>
    /// The run loop, end to end: rain gathers, the player releases it, the water carves, the
    /// mountain keeps the change, and the carve report proves it. Nothing here resets anything.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        public enum State { Idle, Flowing, Report, Panel, TimeLapse }

        [Header("Wiring (filled in by GameBootstrap)")]
        public GameConfig Config;
        public ThumbInput Thumb;
        public RillCamera Cam;
        public TerrainMeshBuilder Terrain;
        public WaterRibbon Ribbon;
        public PooledWaterMesh Pooled;
        public EcosystemSystem Ecosystem;
        public RevelationSystem Revelation;
        public Collectibles Pickups;
        public SplashFX Fx;
        public HudController Hud;
        public FlowAudio Audio;

        [Header("Cadence")]
        public int TimeLapseEveryRuns = 3;
        public int AutosaveEveryRuns = 1;

        /// <summary>Rain that gathered while the player was away. Spent on the next run, then gone.</summary>
        [HideInInspector] public float PendingBonusVolume;

        public State Current { get; private set; } = State.Idle;
        public RillWorld Home { get; private set; }
        public RillWorld Active { get; private set; }
        public bool InDaily { get; private set; }

        Almanac _almanac;
        DailyRill _daily;
        TimeLapseArchive _archive;
        TimeLapsePlayer _player;
        ConfluenceQueue _confluence;
        WeatherSystem _weather;

        readonly ProjectSystem _projects = new ProjectSystem();
        FlowSimulation _sim;
        float[] _beforeHeights;
        CarveReport _lastReport;
        readonly List<Vector3> _lastPath = new List<Vector3>();
        readonly Queue<Cascade> _cascades = new Queue<Cascade>();
        // The player's report, held back while their dam break plays out first.
        CarveReport _heldReport;
        bool _autoRun;
        Vector2 _lastThumbPos;

        struct Cascade
        {
            public Vector3 Origin;
            public float Volume;
            public string BasinName;
        }

        public void Initialise(RillWorld home, Almanac almanac, DailyRill daily, TimeLapseArchive archive,
                               TimeLapsePlayer player, ConfluenceQueue confluence, WeatherSystem weather)
        {
            Home = home;
            Active = home;
            _almanac = almanac;
            _daily = daily;
            _archive = archive;
            _player = player;
            _confluence = confluence;
            _weather = weather;

            BindWorld(home);

            Hud.AlmanacRequested += OnAlmanac;
            Hud.TimeLapseRequested += OnTimeLapse;
            Hud.DailyRequested += OnDailyToggle;
            Hud.ShareRequested += OnShare;
            Hud.ReportDismissed += OnReportDismissed;
            Hud.PanelClosed += () => { if (Current == State.Panel) Current = State.Idle; };

            // Surface projects before the first idle frame, so a returning player is greeted by
            // the thing they were in the middle of rather than by an instruction.
            _projects.Refresh(home, Ecosystem, Revelation, almanac);

            EnterIdle();
        }

        /// <summary>Points every renderer and system at a world. Used to swap in the Daily mountain.</summary>
        public void BindWorld(RillWorld world)
        {
            Active = world;
            _sim = new FlowSimulation(world);
            _sim.Splash += OnSplash;
            _sim.PickupCheck = OnPickupCheck;
            _beforeHeights = new float[world.Field.Count];

            if (Pickups != null)
            {
                Pickups.Collected -= OnCollected;
                Pickups.Initialise(world, PropMaterial);
                Pickups.Collected += OnCollected;
            }

            world.BasinOverflowed -= OnOverflow;
            world.BasinOverflowed += OnOverflow;

            Cam.SetOverview(world.SummitWorld);
            Pooled.SetDirty();
            Revelation.Refresh();
            Terrain.MarkAll();
            Ribbon.Clear();
        }

        // ------------------------------------------------------------------ state machine

        void EnterIdle()
        {
            Current = State.Idle;
            Cam.SetOverview(Active.SummitWorld);
            Hud.SetIdleUI(true);
            // The idle line is where projects surface: never assigned, never rewarded, just said
            // out loud once so the player can decide it was their idea.
            string idleLine = "Tap to release the water";
            if (!InDaily)
            {
                string project = _projects.HeadlineLine();
                if (!string.IsNullOrEmpty(project)) idleLine = project;
            }

            // The first runs teach the two verbs and then get out of the way. Nothing here explains
            // that the mountain remembers — that is the discovery the whole game is built on, and
            // saying it out loud replaces it with a chore. It only names what the thumb does,
            // because a player who never finds out they can steer concludes the game is boring for
            // a reason that has nothing to do with the game.
            if (!InDaily && Active.RunNumber < 3) idleLine = "Tap to let the water go";
            else if (!InDaily && Active.RunNumber < 6) idleLine = "Hold and drag while it runs to lean the water";

            Hud.SetHint(InDaily
                ? (_daily.RunsLeft > 0 ? "Daily Rill — " + _daily.RunsLeft + " runs left. Tap to release." : "Daily complete. Share your glyph.")
                : idleLine);
            RefreshTopLine();
        }

        void RefreshTopLine()
        {
            string left = InDaily
                ? "Daily Rill · " + _daily.DateKey
                : "Run " + Active.RunNumber + " · " + _weather.Headline;
            string right;
            if (InDaily)
            {
                right = string.Format("{0} m³ to sea · {1}/{2}", Mathf.RoundToInt(_daily.WaterToSea), _daily.RunsUsed, DailyRill.RunsPerDay);
            }
            else
            {
                float water = Active.Basins.TotalWater();
                right = string.Format("{0:n0} m³ moved · {1:n0} m³ held", Active.LifetimeSediment, water);
            }
            Hud.SetTopLine(left, right);
        }

        void Update()
        {
            if (Thumb == null || Active == null) return;

            switch (Current)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Flowing: UpdateFlowing(); break;
                case State.Report: break;
                case State.Panel: break;
                case State.TimeLapse: UpdateTimeLapse(); break;
            }

            if (Audio != null && Current != State.Flowing)
                Audio.SetFlowState(false, 0f, Config.MaxSpeed, 0f, Config.StartVolume, 0f);

            Hud.SetSpeed(Current == State.Flowing ? _sim.Head.Speed / Config.MaxSpeed : 0f, Current == State.Flowing);
        }

        void UpdateIdle()
        {
            Ribbon.FadeOut(Time.deltaTime);

            // Drag pans the mountain; a tap releases the water. No other verbs exist.
            if (Thumb.Held && Thumb.DragDistance > 18f)
            {
                Vector2 delta = Thumb.ScreenPos - _lastThumbPos;
                Cam.Pan(delta);
            }
            _lastThumbPos = Thumb.ScreenPos;

            if (Thumb.WasTap() && !Hud.PanelVisible && !Hud.ReportVisible)
            {
                if (InDaily && _daily.RunsLeft <= 0)
                {
                    Hud.SetHint("Daily complete. Share your glyph.");
                    return;
                }
                StartRun();
            }
        }

        void UpdateFlowing()
        {
            // First run only: if two seconds pass with no touch, say the one thing that unblocks
            // them. Steering is invisible otherwise — there is no button and nothing moves on its
            // own to suggest it.
            if (!InDaily && Active.RunNumber <= 1 && !Thumb.Held && _sim.Elapsed > 2f && _sim.Elapsed < 6f)
                Hud.SetHint("Hold and drag to lean the water");

            // The whole control scheme: a lateral pull toward the thumb, and the cost of using it.
            Vector2 target = _sim.Head.Pos;
            bool steering = Thumb.Held && Thumb.WorldTargetOnPlane(Cam.Cam, _sim.Head.Height, out target);
            if (!steering) target = _sim.Head.Pos;
            _sim.SetSteer(steering, target);

            _sim.Advance(Time.deltaTime);

            Ribbon.SetPath(_sim.Path, _sim.Head.World, _sim.Head.Speed);
            Cam.Follow(_sim.Head.World, _sim.Head.Vel);

            if (Audio != null)
            {
                float polish = Active.Field.SamplePolishWorld(_sim.Head.Pos.x, _sim.Head.Pos.y);
                Audio.SetFlowState(true, _sim.Head.Speed, Config.MaxSpeed, _sim.Head.Volume, Config.StartVolume, polish);
            }

            if (!_sim.Running) FinishRun();
        }

        void UpdateTimeLapse()
        {
            if (_player != null && !_player.Playing)
            {
                Terrain.gameObject.SetActive(true);
                Pooled.gameObject.SetActive(true);
                EnterIdle();
            }
        }

        // ------------------------------------------------------------------ the run

        void StartRun()
        {
            Active.BeginRun();
            Active.Field.CopyHeightTo(_beforeHeights);
            Terrain.ClearOverlay();

            var rng = new Rng(Noise.Hash((uint)(Active.RunNumber * 2654435761u) ^ Active.Seed));
            Vector3 spawn = Active.SpawnPoint(ref rng);

            float volume = Config.StartVolume * (InDaily ? 1f : _weather.VolumeMultiplier);
            if (!InDaily && PendingBonusVolume > 0f)
            {
                volume += PendingBonusVolume;
                PendingBonusVolume = 0f;
            }
            if (Pickups != null)
            {
                Pickups.ResetCounters();
                Pickups.PlaceForRun(Active.RunNumber);
            }

            _sim.Begin(spawn, volume);
            _autoRun = false;

            Current = State.Flowing;
            Hud.SetIdleUI(false);
            Hud.SetHint("");
            Hud.HideAllPanels();
        }

        /// <summary>An overflow or a cascade runs itself: the player watches their own dam break.</summary>
        void StartCascade(Cascade c)
        {
            Active.BeginRun();
            // Do not reset the carve baseline while the player's report is waiting behind this
            // cascade, or the overlay they finally see would show the dam break only, with their
            // own run's carving already subtracted out.
            if (_heldReport == null) Active.Field.CopyHeightTo(_beforeHeights);
            _sim.Begin(c.Origin, c.Volume);
            _sim.SetSteer(false, Vector2.zero);
            _autoRun = true;
            Current = State.Flowing;
            Hud.SetIdleUI(false);
            Hud.SetHint(c.BasinName + " is overflowing");
        }

        void FinishRun()
        {
            var field = Active.Field;

            // Terrain moved, so the depression map is stale. Rebuilding also re-derives every
            // basin's held volume from the per-cell water that persisted through the run.
            Active.Basins.Rebuild();
            Pooled.SetDirty();

            var report = Active.EndRun(_sim.Ending, _sim.Elapsed, _sim.Distance, _sim.TopSpeed, _sim.WaterToSea);

            if (Pickups != null)
            {
                report.SeedsCaught = Pickups.SeedsCaught;
                report.FlowersSplashed = Pickups.FlowersSplashed;
                report.GatesThreaded = Pickups.GatesThreaded;
                // Seeds become life where the water actually came to rest, not where they were caught.
                Pickups.PlantCaughtSeeds(Ecosystem, _sim.Head.World);
            }

            Ecosystem.AdvanceAfterRun(report.LifeArrivals);
            Revelation.Refresh();
            Active.ApplyBetweenRunDrift();
            BiomeRules.BetweenRuns(Active, _weather, report.Headlines);
            _projects.Refresh(Active, Ecosystem, Revelation, InDaily ? null : _almanac);

            if (report.Revealed.Count > 0 || report.Overflowed) Haptics.Event();

            _lastReport = report;
            _lastPath.Clear();
            _lastPath.AddRange(_sim.Path);

            if (Config.ShowCarveOverlay) Terrain.ShowCarveOverlay(_beforeHeights);

            Vector3 focus = report.DeepestCarve > 0.01f ? report.DeepestCarveWorld : _sim.Head.World;
            Cam.FrameReport(focus);

            if (Audio != null)
            {
                Audio.SetAmbientWater(Active.Basins.TotalWater());
                if (report.DeepestCarve > 0.15f) Audio.DepthNote(Mathf.RoundToInt(report.DeepestCarve * 10f));
            }

            if (InDaily)
            {
                _daily.RecordRun(_sim.Path, _sim.Ending == RunEnding.ReachedSea, _sim.WaterToSea, field.WorldExtent);
            }
            else
            {
                _almanac.RecordRun(report);
                _almanac.NoteMilestones(Active.RunNumber, Active.LifetimeSediment, Revelation.RevealedCount());
                _almanac.Save();

                _confluence.Enqueue(field, _beforeHeights, Active.RunNumber, Active.Seed);
                if (Active.RunNumber % TimeLapseEveryRuns == 0) _archive.Append(field, Active.RunNumber);
                if (Active.RunNumber % AutosaveEveryRuns == 0) SaveSystem.Save(Active, Ecosystem.LifeField);
            }

            RefreshTopLine();

            if (_autoRun)
            {
                // A cascade just finished. Chain into the next one, or hand back the report that
                // was held for the player's own run.
                _autoRun = false;
                if (_cascades.Count > 0) { StartCascade(_cascades.Dequeue()); return; }

                if (_heldReport != null)
                {
                    var held = _heldReport;
                    _heldReport = null;
                    if (Config.ShowCarveOverlay) Terrain.ShowCarveOverlay(_beforeHeights);
                    Current = State.Report;
                    Hud.ShowReport(held, Revelation.RevealedCount(), Active.Secrets.Count);
                    return;
                }

                EnterIdle();
                return;
            }

            // The dam break plays *before* the report, not after. It used to run off the tap that
            // dismissed the report card — a tap indistinguishable from the one that starts a run —
            // so the player believed their own run had begun inside a lake with dead controls.
            // Consequence rather than interruption: your water filled the basin, the basin broke,
            // and only then do you get told what the run did.
            if (_cascades.Count > 0)
            {
                _heldReport = report;
                StartCascade(_cascades.Dequeue());
                return;
            }

            Current = State.Report;
            Hud.ShowReport(report, Revelation.RevealedCount(), Active.Secrets.Count);
        }

        void OnReportDismissed()
        {
            // Nothing to chain into any more: cascades have already played by the time the report
            // is on screen, so dismissing it always returns to idle at the summit.
            Terrain.ClearOverlay();
            EnterIdle();
        }

        void OnOverflow(Basin basin, float excess)
        {
            // A dam break can feed the next basin, which can break too. Three in a row is a
            // spectacle; more than that is a cutscene the player did not ask for.
            if (_cascades.Count >= 3) return;

            var f = Active.Field;
            var spill = basin.SpillXZ(f.Size);
            Vector3 origin = f.GridToWorld(spill.x, spill.y);
            _cascades.Enqueue(new Cascade
            {
                Origin = origin,
                Volume = Mathf.Clamp(excess, Config.StartVolume * 0.5f, Config.StartVolume * 4f),
                BasinName = basin.Name
            });
        }

        void OnSplash(Vector3 pos, float strength)
        {
            if (Audio != null) Audio.Splash(strength);
            if (Fx != null) Fx.Burst(pos, strength, new Color(0.80f, 0.94f, 1f, 0.9f));
            Haptics.Tick(strength);
        }

        float OnPickupCheck(Vector2 headXZ, float headY, float speed)
        {
            if (Pickups == null) return 0f;
            bool took;
            return Pickups.Check(headXZ, headY, speed, out took);
        }

        void OnCollected(PickupKind kind, Vector3 world, Color color)
        {
            if (Fx != null)
                Fx.Burst(world, kind == PickupKind.Gate ? 0.8f : 0.45f,
                         kind == PickupKind.Dye ? color : new Color(0.95f, 0.95f, 0.8f, 0.9f));
            if (Audio != null) Audio.Splash(kind == PickupKind.Gate ? 0.55f : 0.3f);
            if (kind == PickupKind.Gate) Haptics.Event();
        }

        // ------------------------------------------------------------------ meta screens

        void OnAlmanac()
        {
            if (Current != State.Idle) return;
            var sb = new StringBuilder();
            sb.AppendFormat("Runs {0}   ·   {1:n0} m³ moved   ·   {2:n0} m³ delivered to sea\n",
                Active.RunNumber, Active.LifetimeSediment, Active.LifetimeWaterToSea);
            sb.AppendFormat("Life: {0}   ·   Uncovered {1} of {2}   ·   Streak {3} days\n\n",
                EcosystemSystem.Describe(Ecosystem.HighestTier), Revelation.RevealedCount(), Active.Secrets.Count, _almanac.DayStreak);
            sb.Append("Close to finishing:\n").Append(_projects.PanelBlock()).Append('\n');
            sb.Append(HudController.FormatAlmanac(_almanac));

            Current = State.Panel;
            Hud.ShowPanel("The Almanac", sb.ToString());
        }

        void OnTimeLapse()
        {
            if (Current != State.Idle || _player == null) return;
            if (!_player.Play(_archive))
            {
                Hud.SetHint("Not enough history yet — play a few more runs");
                return;
            }
            Terrain.gameObject.SetActive(false);
            Pooled.gameObject.SetActive(false);
            Hud.SetIdleUI(false);
            Current = State.TimeLapse;
        }

        void OnDailyToggle()
        {
            if (Current != State.Idle) return;

            if (InDaily)
            {
                InDaily = false;
                RebindRenderers(Home, _homeLife);
                EnterIdle();
                return;
            }

            // Remember the home mountain's ecosystem before the Daily borrows the renderers.
            _homeLife = Ecosystem.LifeField;
            var dailyWorld = RillWorld.Create(Config, _daily.Seed, Config.Biome);
            InDaily = true;
            RebindRenderers(dailyWorld, new float[dailyWorld.Field.Count]);
            EnterIdle();
        }

        float[] _homeLife;

        void RebindRenderers(RillWorld world, float[] lifeField)
        {
            BindWorld(world);
            Terrain.Initialise(world.Field, world.Bands, TerrainMaterial);
            Pooled.Initialise(world.Field, WaterMaterial);
            Ecosystem.Initialise(world, PropMaterial);
            Ecosystem.UseLifeField(lifeField);
            Revelation.Initialise(world, PropMaterial);
        }

        // Materials are handed over by the bootstrap so rebinding can rebuild the renderers.
        [HideInInspector] public Material TerrainMaterial;
        [HideInInspector] public Material WaterMaterial;
        [HideInInspector] public Material PropMaterial;

        void OnShare()
        {
            if (Current != State.Idle) return;

            string text;
            if (InDaily)
            {
                text = _daily.ShareText(Active.Field.WorldExtent);
            }
            else
            {
                var paths = new List<List<Vector3>> { new List<Vector3>(_lastPath) };
                var seas = new List<bool> { _lastReport != null && _lastReport.Ending == RunEnding.ReachedSea };
                string glyph = GlyphGenerator.Render(paths, seas, Active.Field.WorldExtent);
                text = string.Format("RILL — my mountain, run {0}\n{1}\n{2:n0} m³ moved · {3} uncovered\n#RILL",
                    Active.RunNumber, glyph, Active.LifetimeSediment, Revelation.RevealedCount());
            }

            GUIUtility.systemCopyBuffer = text;

            // Postcard: a one-tap beauty shot, saved next to the save file.
            string shot = System.IO.Path.Combine(SaveSystem.RootDir,
                string.Format("postcard_run{0}.png", Active.RunNumber));
            ScreenCapture.CaptureScreenshot(shot);

            Hud.SetHint("Copied to clipboard · postcard saved");
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && !InDaily && Active != null) SaveSystem.Save(Active, Ecosystem.LifeField);
        }

        void OnApplicationQuit()
        {
            if (!InDaily && Active != null) SaveSystem.Save(Active, Ecosystem.LifeField);
        }
    }
}
