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
        public enum State { Title, Idle, Flowing, Settling, Report, Panel, TimeLapse }

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
        public SkyDriver Sky;

        [Header("Cadence")]
        public int TimeLapseEveryRuns = 3;
        public int AutosaveEveryRuns = 1;

        /// <summary>Rain that gathered while the player was away. Spent on the next run, then gone.</summary>
        [HideInInspector] public float PendingBonusVolume;

        public State Current { get; private set; } = State.Idle;

        /// <summary>
        /// Where the player is in the app, above the run loop. Owned here for now because
        /// RunController is still the only thing with a Update(); it moves up to a shell when
        /// there is more than one mountain to choose between (L-047).
        /// </summary>
        public readonly Navigator Nav = new Navigator();

        /// <summary>
        /// The three mountains, and the only sanctioned way to make or destroy one.
        ///
        /// Created in Initialise, NEVER in a field initializer: the constructor reads the save
        /// headers from disk, and Unity forbids persistentDataPath inside a MonoBehaviour field
        /// initializer. As one, it threw during AddComponent — which killed every field
        /// initializer after this line, so _projects, _lastPath and _cascades were null, Initialise
        /// died before EnterTitle, and the game booted straight onto the mountain with no title,
        /// no report and no working run end. That was the entire "everything is broken" evening,
        /// and no headless test could see it because none of them construct the MonoBehaviour.
        /// Found by RILL/Run Play-Mode Probe, which does.
        /// </summary>
        public MountainRoster Roster { get; private set; }

        /// <summary>Which of the three the player is on. Every per-slot system keys off this.</summary>
        public int CurrentSlot { get; private set; }
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
        // Session-scoped: an existing save has no record of whether its owner ever learned to steer.
        bool _hasSteered;

        // Speed at which the stream starts throwing spray. Near the terminal speed of fresh rock
        // on a steep face, so spray means "faster than un-carved ground allows" — the reward for
        // having carved, made visible without a meter.
        const float SpraySpeed = 12f;
        float _sprayTimer;

        // The pause between the run ending and the report card.
        const float SettleSeconds = 1.1f;
        CarveReport _settleReport;
        float _settleTimer;
        bool _autoRun;
        Vector2 _lastThumbPos;

        struct Cascade
        {
            public Vector3 Origin;
            public float Volume;
            public string BasinName;
        }

        public void Initialise(RillWorld home, Almanac almanac, DailyRill daily, TimeLapseArchive archive,
                               TimeLapsePlayer player, ConfluenceQueue confluence, WeatherSystem weather,
                               int slot = 0)
        {
            Roster = new MountainRoster();
            CurrentSlot = slot;
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
            Hud.BackRequested += GoBack;
            Hud.EndGameRequested += EndGame;
            Hud.MountainPicked += OnMountainPicked;

            // The hardware back key at the root closes the app on Android, which is that platform's
            // convention and the only place an app should ever close itself. Apple's guidelines are
            // explicit that an iOS app must not, so there the key does not exist and neither does
            // the action.
#if UNITY_IOS && !UNITY_EDITOR
            Nav.CanQuit = false;
#endif

            // Surface projects before the first idle frame, so a returning player is greeted by
            // the thing they were in the middle of rather than by an instruction.
            _projects.Refresh(home, Ecosystem, Revelation, almanac);

            // Boot into the title, not into a playable mountain. Opening straight into the run
            // state gave the app no front door at all — it simply appeared, mid-game.
            EnterTitle(arriving: true);
        }

        /// <summary>
        /// Moves the player to another of their mountains.
        ///
        /// The order here is the whole job. The mountain being left is written to disk BEFORE
        /// anything is rebound, because every later step overwrites the live world; a switch that
        /// saves afterwards saves the wrong one. Every per-slot system — almanac, time-lapse
        /// archive, confluence queue — is rebuilt for the new slot, because they are keyed by slot
        /// on disk and would otherwise keep writing the previous mountain's history into the new
        /// one's files.
        ///
        /// Returns false if the slot is empty; making a mountain is MountainRoster.Create's job and
        /// deliberately not something a "switch to" call can do by accident.
        /// </summary>
        public bool SwitchToMountain(int slot)
        {
            // Step off the Daily's borrowed world before anything is saved. The previous version
            // cleared the flag first and saved Active second — and if a caller arrived here while
            // still in the Daily, Active WAS the daily world, so the player's slot was overwritten
            // with a throwaway mountain. That is the exact loss invariant 1 exists to prevent.
            LeaveDaily();

            if (slot == CurrentSlot) return true;
            if (!Roster.Occupied(slot)) return false;

            // Save first. Everything below replaces the live world.
            if (Active != null) SaveSystem.Save(Active, Ecosystem.LifeField, CurrentSlot);
            if (_almanac != null) _almanac.Save();

            float[] life;
            var loaded = SaveSystem.Load(Config, out life, slot);
            if (loaded == null) return false;

            CurrentSlot = slot;
            Home = loaded;
            _almanac = Almanac.Load(slot);
            _archive = new TimeLapseArchive(slot);
            _confluence = new ConfluenceQueue(slot);
            _homeLife = life;

            RebindRenderers(loaded, life ?? new float[loaded.Field.Count]);
            _projects.Refresh(loaded, Ecosystem, Revelation, _almanac);

            if (!_archive.Exists) _archive.Append(loaded.Field, loaded.RunNumber);
            if (Audio != null) Audio.SetAmbientWater(loaded.Basins.TotalWater());

            Roster.Refresh();
            Nav.GoHome();
            EnterTitle();
            return true;
        }

        /// <summary>
        /// Starts a mountain in an empty slot and moves to it. Refuses on an occupied slot, because
        /// MountainRoster.Create refuses — there is no overwrite anywhere in this path.
        /// </summary>
        public bool StartMountain(int slot, Biome biome)
        {
            if (Roster.Occupied(slot)) return false;

            uint seed = (uint)System.DateTime.UtcNow.Ticks ^ (uint)(slot * 2654435761u);
            var made = Roster.Create(slot, biome, seed, Config);
            if (made == null) return false;
            return SwitchToMountain(slot);
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

        /// <summary>
        /// The game opens here, not in a run. The mountain drifts behind the title — the world is
        /// the save file, so the honest splash screen for this game is the river system the player
        /// built last time, and a returning player sees their own work before they see a button.
        /// </summary>
        public void EnterTitle() { EnterTitle(false); }

        /// <summary>
        /// <paramref name="arriving"/> plays the opening camera move. Only true once, on launch:
        /// an arrival you sit through every time you press Back is an obstacle, not an arrival.
        /// </summary>
        public void EnterTitle(bool arriving)
        {
            // The main screen never shows the Daily's mountain. Back and End game could both reach
            // here with InDaily still set and Active still pointing at the borrowed daily world —
            // and SwitchToMountain would then have saved THAT world over the player's slot, because
            // it cleared the flag before the save guard read it. Leaving the Daily at the door
            // makes the whole class impossible: anything at the title is the player's own mountain.
            LeaveDaily();

            Current = State.Title;
            if (arriving) Cam.SetTitleArriving(Active.SummitWorld);
            else Cam.SetTitle(Active.SummitWorld);
            Ribbon.Clear();
            Hud.SetIdleUI(false);
            Hud.SetHint("");
            Hud.HideAllPanels();
            Hud.SetTopLine("", "");

            string record = Active.RunNumber > 0
                ? string.Format("{0:n0} runs · {1:n0} m³ moved · {2:n0} m³ to the sea",
                                Active.RunNumber, Active.LifetimeSediment, Active.LifetimeWaterToSea)
                : "A new mountain, untouched";
            Hud.SetTitle(true, record, StartFromTitle);

            // The three mountains, each saying what has been done to it. The one being stood on is
            // marked rather than hidden, so the list is always the same shape and the row a player
            // reaches for does not move.
            Roster.Refresh();
            var rows = new string[MountainRoster.Slots];
            for (int i = 0; i < MountainRoster.Slots; i++)
                rows[i] = (i == CurrentSlot && Roster.Occupied(i) ? "▸  " : "    ") + Roster.Describe(i);
            Hud.SetMountains(rows);
        }

        /// <summary>
        /// A mountain row was chosen. An occupied slot is entered; an empty one is started with the
        /// biome that slot does not have yet, so a player who takes all three ends up with three
        /// different games rather than three copies of the same one.
        /// </summary>
        void OnMountainPicked(int slot)
        {
            if (Current != State.Title) return;

            if (Roster.Occupied(slot))
            {
                if (slot != CurrentSlot) SwitchToMountain(slot);
                else StartFromTitle();
                return;
            }

            StartMountain(slot, BiomeForNewSlot(slot));
        }

        /// <summary>
        /// Which rock a new mountain gets. The biomes are genuinely different games — glacier is
        /// fast and grudging, volcanic builds terrain an order of magnitude faster than anything
        /// else, granite keeps what you cut — and a player who fills all three slots should meet
        /// three of them rather than the same one three times. Picked by what is missing, so the
        /// choice needs no menu.
        /// </summary>
        Biome BiomeForNewSlot(int slot)
        {
            var wanted = new[] { Biome.Sandstone, Biome.Glacier, Biome.Volcanic };
            for (int i = 0; i < wanted.Length; i++)
            {
                bool taken = false;
                for (int k = 0; k < MountainRoster.Slots && !taken; k++)
                    if (Roster.Occupied(k) && Roster[k].Biome == wanted[i]) taken = true;
                if (!taken) return wanted[i];
            }
            return wanted[slot % wanted.Length];
        }

        void StartFromTitle()
        {
            Hud.SetTitle(false, "", null);
            Nav.Push(AppScreen.Mountain);
            EnterIdle();
        }

        /// <summary>
        /// One back action, whatever raised it — the on-screen button or the hardware key.
        ///
        /// A run in flight is not interrupted silently: the Navigator refuses to move and asks for
        /// the run to be abandoned first, so the water in the head gets left on the mountain
        /// instead of deleted. Abandoning then falls through to the same Back again.
        /// </summary>
        public void GoBack()
        {
            switch (Nav.Back())
            {
                case NavAction.AbandonRun:
                    if (_sim != null && _sim.Running) _sim.Abort();
                    _settleReport = null;
                    FinishRun();
                    Hud.HideAllPanels();
                    ShowScreen();
                    break;

                case NavAction.Changed:
                    ShowScreen();
                    break;

                case NavAction.Quit:
                    QuitApplication();
                    break;
            }
        }

        /// <summary>Puts the game where the Navigator now says it is.</summary>
        void ShowScreen()
        {
            Hud.HideAllPanels();
            switch (Nav.Current)
            {
                case AppScreen.Home:
                    EnterTitle();
                    break;
                case AppScreen.Mountain:
                    if (Current != State.Idle) EnterIdle();
                    break;
                default:
                    // Panels are their own screens; the run loop just stops driving the mountain.
                    Current = State.Panel;
                    break;
            }
        }

        /// <summary>
        /// Ends the session and goes back to the main screen. This is what "close game" means here:
        /// the run stops, the mountain is written to disk, and the player is home. The application
        /// keeps running — nothing in this game is worth closing an app over, and the mountain is
        /// still there when they come back, which is the entire premise.
        /// </summary>
        public void EndGame()
        {
            if (_sim != null && _sim.Running) _sim.Abort();
            if (Current == State.Flowing || Current == State.Settling)
            {
                _settleReport = null;
                FinishRun();
            }

            // Leave the Daily before saving, so what gets written is the player's mountain and not
            // the borrowed daily world. EnterTitle would do this anyway; doing it before the save
            // is the part that matters.
            LeaveDaily();
            if (Active != null) SaveSystem.Save(Active, Ecosystem.LifeField, CurrentSlot);

            _cascades.Clear();
            _heldReport = null;
            Hud.HideAllPanels();
            Nav.GoHome();
            EnterTitle();
        }

        /// <summary>Closes the application. Only ever reached by Android's hardware back at the root.</summary>
        void QuitApplication()
        {
            if (!Nav.CanQuit) return;
            // Save first. Quitting is the one exit that does not go through OnApplicationQuit on
            // every platform, and this game's entire premise is that the world remembers.
            if (!InDaily && Active != null) SaveSystem.Save(Active, Ecosystem.LifeField, CurrentSlot);

            // No UnityEditor branch here on purpose: a runtime assembly that references UnityEditor
            // compiles in the editor and breaks the player build, guarded or not. Application.Quit
            // is simply a no-op in play mode, which is the correct amount of nothing.
            Application.Quit();
        }

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

            // The onboarding teaches the two verbs and then gets out of the way. Nothing here
            // explains that the mountain remembers — that is the discovery the whole game is built
            // on, and saying it out loud replaces it with a chore. It names only what the thumb does,
            // because a player who never finds out they can steer concludes the game is boring for
            // a reason that has nothing to do with the game.
            //
            // Gated on whether the player has actually steered, NOT on run number. The first
            // version keyed off RunNumber < 6 and could therefore never appear on an existing
            // mountain — which is every mountain except a brand new one, so in practice it showed
            // to nobody. Reported as "onboarding is not here", correctly.
            if (!InDaily && !_hasSteered)
                idleLine = Active.RunNumber < 1
                    ? "Tap to let the water go"
                    : "Hold and drag while it runs to lean the water";

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

            // Android's hardware back must do what the on-screen back does, or the OS wins and the
            // app closes out from under a run in progress.
            if (Input.GetKeyDown(KeyCode.Escape)) GoBack();

            // The main screen is visible when, and only when, the game is on it. This used to be
            // hidden by the Begin handler alone, so any other route into play left it on screen over
            // a live run — reported exactly that way.
            Hud.SetTitleShown(Current == State.Title);

            Nav.RunInProgress = Current == State.Flowing || Current == State.Settling;
            Hud.SetBackVisible(Nav.Current != AppScreen.Launch && Nav.Current != AppScreen.Home
                               && Current != State.Flowing);
            // End game rides in the idle button row, which SetIdleUI already shows and hides, so
            // there is nothing to drive per frame any more.

            switch (Current)
            {
                case State.Title: UpdateTitle(); break;
                case State.Idle: UpdateIdle(); break;
                case State.Flowing: UpdateFlowing(); break;
                case State.Settling: UpdateSettling(); break;
                case State.Report: break;
                case State.Panel: break;
                case State.TimeLapse: UpdateTimeLapse(); break;
            }

            if (Audio != null && Current != State.Flowing)
                Audio.SetFlowState(false, 0f, Config.MaxSpeed, 0f, Config.StartVolume, 0f);

            Hud.SetSpeed(Current == State.Flowing ? _sim.Head.Speed / Config.MaxSpeed : 0f, Current == State.Flowing);
        }

        /// <summary>
        /// The main screen. Nothing here does anything except let the player cut the opening short
        /// — the launch is skippable from the first frame, because the second time somebody sees it
        /// they have already arrived.
        /// </summary>
        void UpdateTitle()
        {
            if (Cam.Arriving && Thumb.WasTap()) Cam.SkipArrival();
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
            // Until they have steered even once: if two seconds pass with no touch, say the one
            // thing that unblocks them. Steering is invisible otherwise — there is no button, and
            // nothing moves on its own to suggest it exists.
            if (!InDaily && !_hasSteered && !Thumb.Held && _sim.Elapsed > 2f)
                Hud.SetHint("Hold and drag to lean the water");

            // The whole control scheme: a lateral pull toward the thumb, and the cost of using it.
            Vector2 target = _sim.Head.Pos;
            bool steering = Thumb.Held && Thumb.WorldTargetOnPlane(Cam.Cam, _sim.Head.Height, out target);
            if (steering && !_hasSteered)
            {
                // Learned. The hint never appears again this session.
                _hasSteered = true;
                Hud.SetHint("");
            }
            if (!steering) target = _sim.Head.Pos;
            _sim.SetSteer(steering, target);

            _sim.Advance(Time.deltaTime);

            Ribbon.SetPath(_sim.Path, _sim.Head.World, _sim.Head.Speed);
            Cam.Follow(_sim.Head.World, _sim.Head.Vel);
            EmitSpray(Time.deltaTime);

            if (Audio != null)
            {
                float polish = Active.Field.SamplePolishWorld(_sim.Head.Pos.x, _sim.Head.Pos.y);
                Audio.SetFlowState(true, _sim.Head.Speed, Config.MaxSpeed, _sim.Head.Volume, Config.StartVolume, polish);
            }

            if (!_sim.Running) FinishRun();
        }

        /// <summary>
        /// Spray, which is the cue the momentum economy was missing. The camera already widens and
        /// drops toward the bed with speed, but nothing in the world itself changed: 24 m/s looked
        /// the same as 9 m/s, so the player could not see the thing they were optimising without
        /// reading the HUD meter.
        ///
        /// Deliberately gated rather than proportional from zero. Spray that is always present is
        /// weather; spray that starts when the stream gets fast is information, and the threshold is
        /// roughly the terminal speed of fresh rock on a steep face — i.e. it appears exactly when
        /// the water is going faster than un-carved ground would allow, which is the whole reward
        /// for having carved.
        /// </summary>
        void EmitSpray(float dt)
        {
            if (Fx == null) return;
            float over = (_sim.Head.Speed - SpraySpeed) / Mathf.Max(1f, Config.MaxSpeed - SpraySpeed);
            if (over <= 0f) { _sprayTimer = 0f; return; }

            _sprayTimer -= dt * (1f + over * 5f);
            if (_sprayTimer > 0f) return;
            _sprayTimer = 0.1f;
            Fx.Burst(_sim.Head.World, 0.10f + over * 0.30f, new Color(0.86f, 0.95f, 1f, 0.5f));
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
            Active.BeginAutomaticEvent();
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

            // A cascade is the mountain acting, not the player. It must not enter the daily, the
            // almanac, the confluence queue or the time-lapse, or an automatic event ends up in the
            // player's own history and consumes one of their daily runs.
            if (_autoRun)
            {
                // terrain changes persist; the bookkeeping does not
            }
            else if (InDaily)
            {
                _daily.RecordRun(_sim.Path, _sim.Ending == RunEnding.ReachedSea, _sim.WaterToSea, field.WorldExtent, field);
            }
            else
            {
                _almanac.RecordRun(report);
                _almanac.NoteMilestones(Active.RunNumber, Active.LifetimeSediment, Revelation.RevealedCount());
                _almanac.Save();

                _confluence.Enqueue(field, _beforeHeights, Active.RunNumber, Active.Seed);
                if (Active.RunNumber % TimeLapseEveryRuns == 0) _archive.Append(field, Active.RunNumber);
                if (Active.RunNumber % AutosaveEveryRuns == 0) SaveSystem.Save(Active, Ecosystem.LifeField, CurrentSlot);
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

                    // Give the settle beat back to the PLAYER'S run, not to the dam break that just
                    // played over it. The cascade re-aimed the camera at its own deepest cut and
                    // overwrote the ribbon with its own path, so on a mountain that overflows often
                    // — which a mature one does after every second run — the player never saw the
                    // path their own water took before the card arrived. Reported exactly that way.
                    if (_lastPath.Count > 1)
                        Ribbon.SetPath(_lastPath, _lastPath[_lastPath.Count - 1], 0f);
                    Cam.FrameReport(held.DeepestCarve > 0.01f
                        ? held.DeepestCarveWorld
                        : (_lastPath.Count > 0 ? _lastPath[_lastPath.Count - 1] : Active.SummitWorld));

                    BeginSettle(held);
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

            BeginSettle(report);
        }

        /// <summary>
        /// A beat between the water stopping and the card arriving. The report used to appear on the
        /// same frame the run ended, which gave the player no moment to look at what they had just
        /// carved — reported as the ending being too sudden. The camera has already framed the
        /// deepest cut and the overlay is already up; this simply lets them be seen.
        /// </summary>
        void BeginSettle(CarveReport report)
        {
            _settleReport = report;
            _settleTimer = SettleSeconds;
            Current = State.Settling;
        }

        void UpdateSettling()
        {
            Ribbon.FadeOut(Time.deltaTime * 0.6f);   // the stream thins out rather than vanishing
            _settleTimer -= Time.deltaTime;

            // A tap skips the wait: never make a returning player sit through it.
            if (_settleTimer > 0f && !Thumb.WasTap()) return;

            var report = _settleReport;
            _settleReport = null;
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

            // Start the dam break below the lip, not on it. SpillCell is the saddle: zero slope,
            // at water level once the basin is full, so a cascade launched there stalled on the rim
            // and the overflow had, in the player's words, "no way out".
            var f = Active.Field;
            int outlet = Active.Basins.OutletCell(basin);
            Vector3 origin = f.GridToWorld(outlet % f.Size, outlet / f.Size);
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

        /// <summary>
        /// Steps off the Daily's borrowed world and back onto the player's own mountain: flag,
        /// lighting, renderers, life field. Safe to call when not in the Daily — it does nothing.
        /// Every route that can leave the Daily goes through here, because the one that did not
        /// (Back, then picking a mountain) saved the daily world over the player's slot.
        /// </summary>
        void LeaveDaily()
        {
            if (!InDaily) return;
            InDaily = false;
            if (Sky != null) Sky.UseFixedHour = false;
            RebindRenderers(Home, _homeLife ?? new float[Home.Field.Count]);
        }

        void OnDailyToggle()
        {
            if (Current != State.Idle) return;

            if (InDaily)
            {
                LeaveDaily();
                EnterIdle();
                return;
            }

            // Remember the home mountain's ecosystem before the Daily borrows the renderers.
            _homeLife = Ecosystem.LifeField;
            var dailyWorld = RillWorld.Create(Config, _daily.Seed, Config.Biome);
            InDaily = true;
            // Everyone competing on the same seed has to be looking at the same mountain, so the
            // Daily is lit at a fixed hour rather than by the player's clock.
            if (Sky != null) { Sky.UseFixedHour = true; Sky.FixedHour = DayCycle.DailyHour; }
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
                text = _daily.ShareText(Active.Field.WorldExtent, Active.Field);
            }
            else
            {
                var paths = new List<List<Vector3>> { new List<Vector3>(_lastPath) };
                var seas = new List<bool> { _lastReport != null && _lastReport.Ending == RunEnding.ReachedSea };
                string glyph = GlyphGenerator.Render(paths, seas, Active.Field.WorldExtent, Active.Field);
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
            if (paused && !InDaily && Active != null)
            {
                SaveSystem.Save(Active, Ecosystem.LifeField, CurrentSlot);
                // The world and the almanac go together. Quitting mid-run saved a RunNumber the
                // almanac had never heard of — five phantom runs in one evening of testing.
                if (_almanac != null) _almanac.Save();
            }
        }

        void OnApplicationQuit()
        {
            if (!InDaily && Active != null)
            {
                SaveSystem.Save(Active, Ecosystem.LifeField, CurrentSlot);
                if (_almanac != null) _almanac.Save();
            }
        }
    }
}
