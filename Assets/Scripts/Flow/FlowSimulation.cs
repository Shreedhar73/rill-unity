using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.Flow
{
    /// <summary>
    /// The stream head: one droplet of water with momentum, volume and a load of sediment.
    /// The player never moves it directly. They lean on it.
    /// </summary>
    public struct StreamHead
    {
        public Vector2 Pos;        // world XZ
        public Vector2 Vel;        // world XZ, metres/second
        public float Height;       // terrain elevation under the head
        public float Volume;       // m^3 of water remaining
        public float Sediment;     // m^3 of rock being carried
        public float Speed => Vel.magnitude;
        public Vector3 World => new Vector3(Pos.x, Height, Pos.y);
    }

    /// <summary>
    /// Rule 1: water flows downhill. Rule 2: flowing water carves, and carved paths attract
    /// future water. Rule 3 (persistence) is enforced by never resetting the heightfield.
    ///
    /// Everything the player experiences as depth — momentum economics, oxbows, river capture,
    /// deltas, waterfalls — falls out of these two rules plus the fact that the field remembers.
    /// </summary>
    public sealed class FlowSimulation
    {
        readonly RillWorld _world;
        readonly GameConfig _cfg;
        readonly HeightField _f;

        public StreamHead Head;
        public bool Running { get; private set; }
        public RunEnding Ending { get; private set; }

        public float Elapsed { get; private set; }
        public float Distance { get; private set; }
        public float TopSpeed { get; private set; }
        public float WaterToSea { get; private set; }
        public float SedimentMoved { get; private set; }

        /// <summary>
        /// Volume still in the head when the run ended. Head.Volume is zeroed by Finish, so without
        /// this there is no way to tell a run that ran dry from a run that stopped moving with 50 m³
        /// still in it — two failures that look identical in every other statistic.
        /// </summary>
        public float VolumeAtEnd { get; private set; }

        /// <summary>Sub-steps spent standing in open water, and of those, how many took the
        /// through-flow branch. A near-full basin that still swallows runs shows up here as
        /// InWaterSteps high and ThroughFlowSteps zero — invisible in any end-of-run statistic.</summary>
        public int InWaterSteps { get; private set; }
        public int ThroughFlowSteps { get; private set; }

        /// <summary>
        /// Conditions at the exact cell the run died on, and how long it had been crawling before
        /// it did. A "Pooled" ending is the same log line whether the water sat down in a lake, sank
        /// into a pit it dug, or ground to a halt on a slope that the terminal-speed identity says
        /// should still have carried it at 5 m/s — three failures with three different fixes.
        /// </summary>
        public float SlopeAtEnd { get; private set; }
        public float WaterAtEnd { get; private set; }
        public float PolishAtEnd { get; private set; }
        /// <summary>Seconds between the last moment the head was moving usefully and the ending.</summary>
        public float CrawlSeconds { get; private set; }

        const float CrawlSpeed = 3f;
        float _lastSlope, _lastWater, _lastPolish, _lastFastElapsed;

        /// <summary>Hollows this run filled to their lip and left over, and what that cost in water.</summary>
        public int HollowsFilled { get; private set; }
        public float HollowVolume { get; private set; }

        /// <summary>Water this run left in basins on its way past them, m³.</summary>
        public float WaterToBasins { get; private set; }
        Basin _soakBasin;
        float _soakPending;

        // A run that stops is a run that ended. A run that neither moves nor ends is the worst
        // thing this game can do with seventy-five seconds, and it was happening to 4 runs in 24.
        // Traced: run 3 spent 68 of its 75 s oscillating between 69 m and 84 m of elevation at
        // 0.1-3 m/s, polishing the same two metres of ground from 0.10 to 0.99, and never once
        // spent a continuous 0.9 s under PoolSpeedThreshold — so the Pooled ending never fired
        // either. Run 10 was worse: its stall elevation *rose* every second, because a slow head
        // is over its sediment capacity and deposits, which buries it deeper.
        //
        // The cause is that 675 cells of this mountain sit in closed depressions too small for the
        // basin lattice to name (it ignores anything under 24 cells, on purpose), plus whatever
        // potholes a run digs mid-flight that no Rebuild has seen yet. Real water in a hollow does
        // not oscillate: it fills the hollow and leaves over the lowest point of the rim. The basin
        // lattice already models exactly that; this is the same thing for the hollows it declines
        // to name.
        // Stuck is measured as net displacement, not as speed. The first version of this tested
        // instantaneous speed and missed the case it was written for: a head oscillating across a
        // hollow reads 0.5-2.7 m/s the whole time and never spends a continuous second slow, while
        // covering four metres of ground in forty seconds. Path length is not progress.
        const float StuckWindow = 2.5f;
        const float StuckRadius = 5f;    // less than 2 m/s of net progress over the window
        const int MaxEscapesPerRun = 24;
        const int EscapeSearchCells = 900;
        const float EscapeMaxRise = 6f;      // above this it is a real basin, and a lake should swallow the run
        const float EscapeMinDrop = 0.25f;   // the lip has to actually lead somewhere downhill
        // Overtopping a lip cuts it. This is rule 2 at the scale of a pothole, and it is what stops
        // a trap being a trap forever: the hollow that ate three runs is breached by the fourth.
        const float LipBreachDepth = 0.22f;

        Vector2 _stuckAnchor;
        float _stuckAnchorTime;
        int _escapes;
        MinHeap _escapeHeap;
        readonly List<int> _escapeVisited = new List<int>(1024);
        readonly HashSet<int> _escapeSeen = new HashSet<int>();

        /// <summary>
        /// Distance travelled after the run's last basin crossing. A crossing count only proves the
        /// branch ran; this proves it *carried the run somewhere*, which is the distinction that
        /// cost this project the most time (see docs/VERIFICATION.md).
        /// </summary>
        public float DistanceAfterCrossing { get; private set; }
        public bool CrossedAnyBasin { get; private set; }
        float _distanceAtLastCrossing;

        /// <summary>Recent head positions, used by the ribbon mesh and the run's signature glyph.</summary>
        public readonly List<Vector3> Path = new List<Vector3>(512);

        /// <summary>Fires on splashy moments (waterfall entry, pool entry) for audio and particles.</summary>
        public event System.Action<Vector3, float> Splash;

        /// <summary>
        /// Optional hook for whatever is scattered along the run: given the head's position, its
        /// elevation and its speed, it returns any speed the stream just earned (threading a gate).
        /// Kept as a delegate so the simulation never has to know what a pickup is.
        /// </summary>
        public System.Func<Vector2, float, float, float> PickupCheck;

        // A run may spill through several lakes on its way down, but not the same one twice — two
        // adjacent basins whose spill cells point at each other would trade the head forever.
        // Frozen ground: slick underfoot, stubborn under the chisel.
        const float IceDragFactor = 0.55f;
        const float IceCarveFactor = 0.25f;

        const int MaxCrossingsPerRun = 4;
        readonly HashSet<int> _crossed = new HashSet<int>();

        // Mid-traverse of a lake: the head is swimming to the outlet rather than obeying terrain.
        bool _crossing;
        Vector2 _crossingTo;
        float _crossingExitSpeed;

        // Drift speed while swimming a lake, m/s. Fast enough not to eat the run clock — an
        // earlier version decayed toward StartSpeed and took ~27 s to cross a 40 m lake, which
        // halved what a run could reach afterwards — slow enough to read as water, not a shortcut.
        const float CrossingDriftSpeed = 6f;

        float _accum;
        float _poolTimer;
        float _fallTimer;
        bool _steerActive;
        Vector2 _steerTarget;

        public FlowSimulation(RillWorld world)
        {
            _world = world;
            _cfg = world.Config;
            _f = world.Field;
        }

        public void Begin(Vector3 spawn, float volume)
        {
            Head = new StreamHead
            {
                Pos = new Vector2(spawn.x, spawn.z),
                Vel = Vector2.zero,
                Height = _f.SampleHeightWorld(spawn.x, spawn.z),
                Volume = volume,
                Sediment = 0f
            };

            // A nudge downhill so the run starts moving without the player having to poke it.
            float slope;
            Vector2 down = _f.DownhillWorld(Head.Pos.x, Head.Pos.y, out slope);
            Head.Vel = down * _cfg.StartSpeed;

            Running = true;
            Ending = RunEnding.Abandoned;
            Elapsed = Distance = TopSpeed = WaterToSea = SedimentMoved = VolumeAtEnd = 0f;
            InWaterSteps = ThroughFlowSteps = 0;
            SlopeAtEnd = WaterAtEnd = PolishAtEnd = CrawlSeconds = 0f;
            _lastSlope = _lastWater = _lastPolish = _lastFastElapsed = 0f;
            HollowsFilled = 0;
            HollowVolume = 0f;
            WaterToBasins = 0f;
            _soakBasin = null;
            _soakPending = 0f;
            _stuckAnchor = Head.Pos;
            _stuckAnchorTime = 0f;
            _escapes = 0;
            DistanceAfterCrossing = _distanceAtLastCrossing = 0f;
            CrossedAnyBasin = false;
            _crossed.Clear();
            _crossing = false;
            _accum = _poolTimer = _fallTimer = 0f;
            Path.Clear();
            Path.Add(Head.World);
        }

        public void SetSteer(bool active, Vector2 worldTargetXZ)
        {
            _steerActive = active;
            _steerTarget = worldTargetXZ;
        }

        /// <summary>Advances the run by a frame, in fixed sub-steps so runs stay reproducible.</summary>
        public void Advance(float deltaTime)
        {
            if (!Running) return;
            _accum += Mathf.Min(deltaTime, 0.25f);
            int guard = 0;
            while (_accum >= _cfg.SimStep && Running && guard++ < 16)
            {
                _accum -= _cfg.SimStep;
                Step(_cfg.SimStep);
            }
        }

        void Step(float dt)
        {
            Elapsed += dt;

            if (_crossing) { StepCrossing(dt); return; }

            float slope;
            Vector2 down = _f.DownhillWorld(Head.Pos.x, Head.Pos.y, out slope);
            float polish = _f.SamplePolishWorld(Head.Pos.x, Head.Pos.y);
            float waterHere = _f.SampleWaterWorld(Head.Pos.x, Head.Pos.y);
            float hardness = _world.HardnessAt(Head.Pos.x, Head.Pos.y);
            float ice = _f.SampleIceWorld(Head.Pos.x, Head.Pos.y);
            _lastSlope = slope; _lastWater = waterHere; _lastPolish = polish;

            // --- gravity. sin(theta) form so a 100% slope is not twice as fast as a 50% one.
            float slopeAccel = _cfg.Gravity * slope / Mathf.Sqrt(1f + slope * slope);
            Head.Vel += down * slopeAccel * dt;

            // --- the entire control scheme: lateral pull, scaled by thumb distance, paid for in speed.
            if (_steerActive && Head.Vel.sqrMagnitude > 0.01f)
            {
                Vector2 fwd = Head.Vel.normalized;
                Vector2 right = new Vector2(fwd.y, -fwd.x);
                Vector2 offset = _steerTarget - Head.Pos;
                float lateral = Mathf.Clamp(Vector2.Dot(offset, right) / _cfg.SteerRange, -1f, 1f);
                // Authority is bought with momentum. Slow water is stubborn; fast water is
                // responsive. Without this the thumb outmuscles the mountain — SteerAccel is 20
                // against 15 m/s² of downhill pull on a 30° face — and a held lean spirals the
                // stream in place instead of steering it, which is not a punishment the player can
                // read, only a run that stops happening.
                float authority = Mathf.Clamp01(Head.Vel.magnitude / Mathf.Max(_cfg.SteerFullSpeed, 0.01f));
                Vector2 push = right * (lateral * _cfg.SteerAccel * authority * dt);

                // Rule 1 is "water flows downhill", and the thumb does not get a vote on it. Strip
                // whatever part of the lean points up the fall line, leaving the part that steers
                // across it. Leaning decides *where* on the mountain the water goes; the mountain
                // decides whether it goes up.
                //
                // This is what makes the deadlock impossible by construction rather than by tuning.
                // Without it the two things the player needs are the same number and pull opposite
                // ways: enough authority to carve a route to a basin off the channel (basin #0 goes
                // 0% -> 100% over one campaign at SteerAccel 42, and stays at 0% at 20) is also
                // enough to hold the stream against the hill until the clock runs out (9-18 runs
                // per 150 timed out at those values). Separating them costs nothing the player
                // wanted: nobody is trying to push water uphill on purpose.
                if (slope > 1e-6f)
                {
                    float uphill = Vector2.Dot(push, -down);
                    if (uphill > 0f) push += down * uphill;
                }
                Head.Vel += push;
                // Fighting gravity bleeds momentum. Expert play is knowing when NOT to touch.
                float bleed = 1f - _cfg.SteerSpeedCost * Mathf.Abs(lateral) * dt;
                Head.Vel *= Mathf.Max(0f, bleed);
            }

            // --- drag: fresh rock is slow, your own polished channel is fast. The whole economy.
            float drag = Mathf.Lerp(_cfg.DragFresh, _cfg.DragPolished, polish);

            // Ice was written by the glacier rules and read only as a colour tint, so a frozen
            // channel behaved exactly like an open one and the whole biome was a palette swap.
            // Ice is slick and ice is armour: you travel faster over it and you cut almost nothing
            // while it holds. That is what makes a glacier a different game rather than a filter —
            // fast, and grudging about being carved.
            if (ice > 0.01f) drag *= Mathf.Lerp(1f, IceDragFactor, ice);

            // A lake with room left in it absorbs the run — that is what a basin is for. A lake
            // without room does not: it fills, spills, and the stream leaves by the outlet.
            //
            // Two earlier versions of this failed silently. Steering the head toward the spill cell
            // with a 0.35g nudge loses to terrain gravity on the rim it has to climb: 1,187
            // sub-steps in that branch across 24 runs, zero runs carried out. Gating the crossing
            // on standing water (depth > 5 cm) then missed almost every case, because a head
            // entering a lake decelerates in the shallow margin and pools before it ever reaches
            // water that deep: 105 of 150 runs died inside a basin that was 100% full.
            //
            // So the test is basin membership, not water depth.
            var basin = _world.Basins.BasinAt(_f.NearestIndex(Head.Pos.x, Head.Pos.y));
            if (basin != null && CanCross(basin))
            {
                ThroughFlowSteps++;
                CrossBasin(basin);
                return;
            }
            if (basin != null) SoakIntoBasin(basin, dt);

            if (waterHere > 0.05f)
            {
                InWaterSteps++;
                drag += 2.5f;
            }

            Head.Vel *= Mathf.Exp(-drag * dt);

            float speed = Head.Vel.magnitude;
            if (speed > _cfg.MaxSpeed)
            {
                Head.Vel *= _cfg.MaxSpeed / speed;
                speed = _cfg.MaxSpeed;
            }
            if (speed > TopSpeed) TopSpeed = speed;
            if (speed > CrawlSpeed) _lastFastElapsed = Elapsed;

            // --- move
            Vector2 next = Head.Pos + Head.Vel * dt;
            float half = _f.WorldExtent * 0.5f - _f.CellSize * 2f;
            if (Mathf.Abs(next.x) > half || Mathf.Abs(next.y) > half)
            {
                Finish(RunEnding.ReachedSea, deliverVolume: true);
                return;
            }

            float prevHeight = Head.Height;
            Head.Pos = next;
            float ground = _f.SampleHeightWorld(next.x, next.y);
            Head.Height = ground;
            Distance += speed * dt;

            // Waterfall detection: a sharp drop is worth a sound and a bigger carve at the plunge pool.
            float drop = prevHeight - ground;
            if (drop > 0.55f * _cfg.CellSize)
            {
                _fallTimer += dt;
                if (_fallTimer > 0.12f)
                {
                    _fallTimer = 0f;
                    if (Splash != null) Splash(Head.World, Mathf.Clamp01(drop));
                }
            }
            else _fallTimer = 0f;

            // --- anything scattered on the line the player chose
            if (PickupCheck != null)
            {
                float gain = PickupCheck(Head.Pos, Head.Height, speed);
                if (gain > 0f && Head.Vel.sqrMagnitude > 1e-6f)
                {
                    Head.Vel += Head.Vel.normalized * gain;
                    speed = Head.Vel.magnitude;
                    if (speed > TopSpeed) TopSpeed = speed;
                }
            }

            // --- carve / deposit
            float volumeNorm = Mathf.Clamp01(Head.Volume / Mathf.Max(_cfg.StartVolume, 1f));
            float radius = _cfg.BrushRadiusCells + _cfg.BrushRadiusPerVolume * volumeNorm;
            float area = EffectiveBrushArea(radius);
            float capacity = _cfg.SedimentCapacity * speed * volumeNorm * Mathf.Max(slope, 0.08f) * area;

            if (Head.Sediment < capacity)
            {
                float depth = _cfg.CarveRate * (speed / _cfg.CarveReferenceSpeed) * volumeNorm * (1.15f - hardness) * dt;
                if (ice > 0.01f) depth *= Mathf.Lerp(1f, IceCarveFactor, ice);
                depth += drop > 0f ? drop * 0.02f : 0f;                 // plunge pools deepen fast

                // Rock gets harder to move the further below the original surface it sits, so the
                // channel approaches a graded profile instead of drilling a shaft. Without this the
                // convergence point reached 23.7 m below virgin in 150 runs — the "boring local
                // minimum" the design document names as a top-three risk.
                int here = _f.NearestIndex(Head.Pos.x, Head.Pos.y);
                float incision = _f.Virgin[here] - _f.Height[here];
                if (incision > 0f)
                {
                    float grade = Mathf.Clamp01(1f - incision / Mathf.Max(_cfg.GradeDepth, 0.01f));
                    depth *= grade * grade;
                }

                depth = Mathf.Min(depth, _cfg.MaxCarvePerStep);
                if (depth > 1e-5f)
                {
                    float moved = _f.AddBrush(_f.Height, Head.Pos.x, Head.Pos.y, radius, -depth);
                    Head.Sediment += moved;
                    SedimentMoved += moved;
                }
            }
            else
            {
                // Over capacity: drop the load. This is how deltas, silt bars and oxbows form.
                float depth = Mathf.Min((Head.Sediment - capacity) / Mathf.Max(area, 1e-4f), _cfg.DepositRate * dt);
                if (depth > 1e-5f)
                {
                    float moved = _f.AddBrush(_f.Height, Head.Pos.x, Head.Pos.y, radius * 1.25f, depth);
                    Head.Sediment = Mathf.Max(0f, Head.Sediment - moved);
                    SedimentMoved += moved;
                }
            }

            // --- the bed remembers: polish (speed) and wetness (life)
            _f.AddBrush(_f.Polish, Head.Pos.x, Head.Pos.y, radius, _cfg.PolishRate * (speed / _cfg.CarveReferenceSpeed) * dt, clamp01: true, markDirty: false);
            _f.AddBrush(_f.Wet, Head.Pos.x, Head.Pos.y, radius * 1.6f, 1.2f * dt, clamp01: true, markDirty: false);

            // --- volume: dry ground drinks, wet ground does not, and open water drinks nothing
            float wet = _f.SampleWetWorld(Head.Pos.x, Head.Pos.y);
            float soak = waterHere > 0.05f ? 0.12f : 1f;
            Head.Volume -= _cfg.InfiltrationRate * (1f - 0.85f * wet) * soak * dt;

            // --- path history for the ribbon
            if ((Head.World - Path[Path.Count - 1]).sqrMagnitude > 0.36f)
            {
                Path.Add(Head.World);
                if (Path.Count > 4096) Path.RemoveAt(0);
            }

            // --- endings
            if (ground <= _f.SeaLevel + _cfg.SeaMargin)
            {
                Finish(RunEnding.ReachedSea, deliverVolume: true);
                return;
            }
            if (Head.Volume <= _cfg.MinVolume)
            {
                Finish(RunEnding.SoakedAway, deliverVolume: false);
                return;
            }
            // Gone nowhere for two and a half seconds: the head is in a hollow, not on a journey.
            // Give it the fill-and-spill that real water gets, before the 75-second clock is the
            // thing that resolves it. Checked before the pool ending because a hollow the run can
            // afford to fill is not an ending at all.
            if (Elapsed - _stuckAnchorTime >= StuckWindow)
            {
                bool stuck = (Head.Pos - _stuckAnchor).sqrMagnitude < StuckRadius * StuckRadius;
                _stuckAnchor = Head.Pos;
                _stuckAnchorTime = Elapsed;
                if (stuck && _escapes < MaxEscapesPerRun && EscapeHollow()) return;
            }

            if (speed < _cfg.PoolSpeedThreshold)
            {
                _poolTimer += dt;
                if (_poolTimer >= _cfg.PoolDwellTime)
                {
                    if (Splash != null) Splash(Head.World, 0.4f);
                    Finish(RunEnding.Pooled, deliverVolume: false);
                    return;
                }
            }
            else _poolTimer = 0f;

            if (Elapsed >= _cfg.MaxRunSeconds)
            {
                Finish(RunEnding.TimedOut, deliverVolume: false);
            }
        }

        /// <summary>
        /// A basin with headroom drinks from the stream crossing it. Deliberately a drain and not a
        /// capture: stopping the run dead the moment it touches a bowl would make every lake on the
        /// route a wall, whereas draining lets the player watch their water go into the thing they
        /// aimed at and carry on with what is left.
        ///
        /// Batched rather than applied per sub-step, because AddWater re-solves the basin's whole
        /// water surface and the largest basin here is 2,900 m³ of cells — doing that ninety times
        /// a second would cost more than the rest of the simulation put together.
        /// </summary>
        void SoakIntoBasin(Basin b, float dt)
        {
            float headroom = b.Capacity - b.Volume - _soakPending;
            if (headroom <= 0f) return;

            float give = Mathf.Min(Mathf.Min(_cfg.BasinSoakRate * dt, headroom), Head.Volume - _cfg.MinVolume);
            if (give <= 0f) return;

            // Flush before switching basins, or the batch owed to the last one is credited to this.
            if (_soakBasin != null && _soakBasin.Id != b.Id) FlushSoak();
            _soakBasin = b;

            Head.Volume -= give;
            WaterToBasins += give;
            _soakPending += give;
            if (_soakPending >= 2f) FlushSoak();
        }

        void FlushSoak()
        {
            if (_soakBasin == null || _soakPending <= 0f) { _soakPending = 0f; return; }
            _world.Basins.AddWater(_soakBasin.Cells[0], _soakPending);
            _soakPending = 0f;
        }

        /// <summary>
        /// True when this lake cannot swallow the run: the water left in the head is more than the
        /// headroom below the spill level. Deliberately a volume comparison and not a fill
        /// percentage — a 4,967 m³ tarn at 0% and a 396 m³ tarn at 0% look identical as fractions
        /// and behave completely differently, and it is the small ones that should become river
        /// first.
        /// </summary>
        bool CanCross(Basin b)
        {
            if (_crossed.Count >= MaxCrossingsPerRun) return false;
            if (_crossed.Contains(b.Id)) return false;   // already spilled this one; do not ring-route
            return b.Capacity - b.Volume < Head.Volume;
        }

        /// <summary>
        /// Fills the basin to its spill level with as much of the run as fits, then continues the
        /// stream from the outlet with what is left. Water is conserved across the crossing: the
        /// part that fits is in the basin, the rest is still in the head.
        /// </summary>
        void CrossBasin(Basin b)
        {
            FlushSoak();
            _crossed.Add(b.Id);
            CrossedAnyBasin = true;
            _distanceAtLastCrossing = Distance;

            float headroom = Mathf.Max(0f, b.Capacity - b.Volume);
            if (headroom > 0f)
            {
                float given = Mathf.Min(headroom, Head.Volume);
                Head.Volume -= given;
                _world.Basins.AddWater(b.Cells[0], given);
            }

            // Aim the head at the outlet and let it swim there under its own steam. Setting
            // Head.Pos to the outlet directly was correct as physics and wrong as a picture: it is
            // a hard jump across open water, drawn by the ribbon as a straight line, and it reads
            // to a player as the stream teleporting. Observed and reported as exactly that.
            // Aim past the lip. The spill cell is the saddle itself — flat, and at water level
            // when the basin is full — so arriving there left the head with no slope to work with
            // and it pooled on the rim instead of leaving.
            int outlet = _world.Basins.OutletCell(b);
            _crossingTo = _f.GridToWorldXZ(outlet % _f.Size, outlet / _f.Size);
            _crossing = true;

            // Exit speed is banked here from the arrival speed, not carried through the traverse.
            // Momentum across a lake is not what decides the run's reach on the far side; letting
            // the drift decide it cost 40% of the distance travelled after a crossing.
            float speedIn = Head.Vel.magnitude;
            _crossingExitSpeed = Mathf.Max(_cfg.StartSpeed, speedIn * 0.35f);

            Vector2 toOutlet = _crossingTo - Head.Pos;
            if (toOutlet.sqrMagnitude < 1e-6f) toOutlet = new Vector2(0f, -1f);
            Head.Vel = toOutlet.normalized * CrossingDriftSpeed;

            _poolTimer = 0f;
            if (Splash != null) Splash(Head.World, 0.6f);
        }

        /// <summary>
        /// Swimming across a full lake to its outlet. Terrain is ignored deliberately — the head is
        /// on water, and the bed beneath it slopes back toward the middle of the basin, which is
        /// what made every earlier attempt at "steer toward the spill" fail. No carving happens
        /// here either: a lake bed is not being cut by water drifting over it.
        /// </summary>
        void StepCrossing(float dt)
        {
            Vector2 toOutlet = _crossingTo - Head.Pos;
            float remaining = toOutlet.magnitude;

            // Arrived: hand the head back to gravity, pointing downhill off the lip.
            if (remaining <= _f.CellSize)
            {
                _crossing = false;
                Head.Pos = _crossingTo;
                Head.Height = _f.SampleHeightWorld(Head.Pos.x, Head.Pos.y);

                float slope;
                Vector2 down = _f.DownhillWorld(Head.Pos.x, Head.Pos.y, out slope);
                if (down.sqrMagnitude < 1e-6f) down = new Vector2(0f, -1f);
                Head.Vel = down.normalized * _crossingExitSpeed;

                _distanceAtLastCrossing = Distance;   // measure the run's reach from the outlet
                Path.Add(Head.World);
                return;
            }

            // Steady drift toward the outlet, no acceleration and no decay.
            float speed = CrossingDriftSpeed;
            Head.Vel = toOutlet / remaining * speed;
            Head.Pos += Head.Vel * dt;
            Distance += speed * dt;

            // The surface it is travelling on, not the bed below it.
            float bed = _f.SampleHeightWorld(Head.Pos.x, Head.Pos.y);
            Head.Height = bed + Mathf.Max(0f, _f.SampleWaterWorld(Head.Pos.x, Head.Pos.y));

            Head.Volume -= _cfg.InfiltrationRate * 0.05f * dt;   // open water barely drinks
            if (Head.Volume <= _cfg.MinVolume) { _crossing = false; Finish(RunEnding.SoakedAway, false); return; }

            if ((Head.World - Path[Path.Count - 1]).sqrMagnitude > 0.36f) Path.Add(Head.World);
        }

        /// <summary>
        /// The head has stalled in a hollow. Find the lowest point of the rim by a minimax flood
        /// outward — the classic "least maximum elevation" path, which is what a rising water
        /// surface finds — fill the hollow to that level out of the run's own volume, cut the lip,
        /// and let the stream swim across the new pond and leave over it.
        ///
        /// Returns true when it has taken responsibility for the step, including the case where the
        /// run cannot afford to fill the hollow, which is a genuine ending rather than a stall.
        /// </summary>
        bool EscapeHollow()
        {
            int n = _f.Size;

            // Walk down to the floor of the hollow before flooding out of it. Seeding the flood at
            // wherever the head happened to be caught — usually part-way up one side, since it is
            // oscillating — makes the search find the hollow's *own* floor as an outlet and
            // conclude there is no rim to cross. Measured: the escape fired 4 times in 24 runs
            // instead of the ~95 the same runs needed.
            //
            // The walk is deliberately short. A head on an open slope has no floor within a few
            // cells, and steepest descent would happily wander off to some real depression tens of
            // metres away and fill a hollow the run is not in.
            int start = _f.NearestIndex(Head.Pos.x, Head.Pos.y);
            bool atFloor = false;
            for (int s = 0; s < 6 && !atFloor; s++)
            {
                int cx = start % n, cz = start / n;
                int low = -1;
                float lowH = _f.Height[start];
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + (k == 0 || k == 4 || k == 5 ? 1 : k == 1 || k == 6 || k == 7 ? -1 : 0);
                    int nz = cz + (k == 2 || k == 4 || k == 6 ? 1 : k == 3 || k == 5 || k == 7 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int ni = nz * n + nx;
                    if (_f.Height[ni] < lowH) { lowH = _f.Height[ni]; low = ni; }
                }
                if (low < 0) atFloor = true; else start = low;
            }
            if (!atFloor) return false;   // still descending after six cells: an open slope, not a pit

            float startH = _f.Height[start];

            if (_escapeHeap == null) _escapeHeap = new MinHeap(1024);
            _escapeHeap.Clear();
            _escapeSeen.Clear();
            _escapeVisited.Clear();
            _escapeHeap.Push(startH, start);
            _escapeSeen.Add(start);

            int outlet = -1;
            float lipLevel = startH;
            float lvl;
            int c;
            while (_escapeHeap.Pop(out lvl, out c))
            {
                // Higher than a pothole rim: this is a real basin, and a lake is supposed to
                // swallow the run rather than hand it a staircase out.
                if (lvl - startH > EscapeMaxRise) break;

                // Below the level the water had to reach to get here, and below where the head
                // started: over the lip and heading down. This is where the pond would drain.
                if (c != start && _f.Height[c] <= startH - EscapeMinDrop && _f.Height[c] < lvl - 1e-3f)
                {
                    outlet = c;
                    lipLevel = lvl;
                    break;
                }

                _escapeVisited.Add(c);
                if (_escapeVisited.Count >= EscapeSearchCells) break;

                int cx = c % n, cz = c / n;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + (k == 0 || k == 4 || k == 5 ? 1 : k == 1 || k == 6 || k == 7 ? -1 : 0);
                    int nz = cz + (k == 2 || k == 4 || k == 6 ? 1 : k == 3 || k == 5 || k == 7 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int ni = nz * n + nx;
                    if (!_escapeSeen.Add(ni)) continue;
                    float h = _f.Height[ni];
                    _escapeHeap.Push(h > lvl ? h : lvl, ni);
                }
            }

            // No rim within reach: the water genuinely cannot leave here. That is an ending, and
            // saying so is the whole point — the alternative is the run grinding out its clock.
            if (outlet < 0)
            {
                Finish(RunEnding.Pooled, deliverVolume: false);
                return true;
            }

            // The water never had to climb to get out, so this is not a hollow — the head is merely
            // slow, on ground that still leads downhill. Leaving it alone matters: without this
            // test a stuck-looking head on an open slope would be handed a free hop to the nearest
            // lower cell, which is a speed boost the player did not earn and physics did not offer.
            if (lipLevel <= startH + 1e-3f) return false;

            // Water already standing in the hollow counts toward the fill. Without this, a head
            // that falls back into a pond it just paid for is charged for the same water twice,
            // which would quietly create volume from nothing.
            float cellArea = _f.CellSize * _f.CellSize;
            float fill = 0f;
            for (int i = 0; i < _escapeVisited.Count; i++)
            {
                int cell = _escapeVisited[i];
                float d = lipLevel - _f.Height[cell] - _f.Water[cell];
                if (d > 0f) fill += d * cellArea;
            }

            // Filling costs real water. A run that keeps falling into holes runs out, which is the
            // honest price and is also the lesson: the hollow you filled is shallower for next time.
            if (fill > Head.Volume - _cfg.MinVolume)
            {
                Finish(RunEnding.Pooled, deliverVolume: false);
                return true;
            }

            Head.Volume -= fill;
            HollowsFilled++;
            HollowVolume += fill;
            _escapes++;

            // The pond is real while the run lasts. At the next Rebuild, GatherExistingWater routes
            // any of it that is not inside a named basin downhill into one, so the volume is
            // conserved rather than quietly deleted.
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int i = 0; i < _escapeVisited.Count; i++)
            {
                int cell = _escapeVisited[i];
                float d = lipLevel - _f.Height[cell];
                if (d <= 0f) continue;
                if (d > _f.Water[cell]) _f.Water[cell] = d;
                int x = cell % n, z = cell / n;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }
            if (minX <= maxX) _f.MarkDirty(minX, minZ, maxX, maxZ);

            Vector2 outletXZ = _f.GridToWorldXZ(outlet % n, outlet / n);

            // Overtopping cuts the lip. Rule 2 at the scale of a pothole, and the reason a trap
            // stops being a trap: the hollow that ate three runs is breached by the fourth.
            SedimentMoved += _f.AddBrush(_f.Height, outletXZ.x, outletXZ.y, 1.3f, -LipBreachDepth);

            // Swim to the lip rather than jump to it. Setting Head.Pos directly is correct as
            // physics and reads as a teleport — the same defect that had to be fixed for basin
            // crossings, so this reuses that machinery rather than reintroducing it.
            _crossingTo = outletXZ;
            _crossing = true;
            float drop = Mathf.Max(0f, lipLevel - _f.Height[outlet]);
            _crossingExitSpeed = Mathf.Clamp(Mathf.Sqrt(2f * 9.81f * drop), _cfg.StartSpeed, _cfg.MaxSpeed * 0.5f);

            Vector2 toOutlet = _crossingTo - Head.Pos;
            if (toOutlet.sqrMagnitude < 1e-6f) toOutlet = new Vector2(0f, -1f);
            Head.Vel = toOutlet.normalized * CrossingDriftSpeed;

            _poolTimer = 0f;
            if (Splash != null) Splash(Head.World, 0.35f);
            return true;
        }

        void Finish(RunEnding ending, bool deliverVolume)
        {
            FlushSoak();
            Running = false;
            Ending = ending;
            VolumeAtEnd = Mathf.Max(0f, Head.Volume);
            DistanceAfterCrossing = CrossedAnyBasin ? Distance - _distanceAtLastCrossing : 0f;
            SlopeAtEnd = _lastSlope;
            WaterAtEnd = _lastWater;
            PolishAtEnd = _lastPolish;
            CrawlSeconds = Mathf.Max(0f, Elapsed - _lastFastElapsed);

            // Whatever rock is still in suspension settles where the water stopped.
            if (Head.Sediment > 1e-4f)
            {
                float radius = _cfg.BrushRadiusCells * 2f;
                float depth = Head.Sediment / Mathf.Max(EffectiveBrushArea(radius), 1e-4f);
                _f.AddBrush(_f.Height, Head.Pos.x, Head.Pos.y, radius, Mathf.Min(depth, 0.35f));
                Head.Sediment = 0f;
            }

            if (deliverVolume)
            {
                WaterToSea = Mathf.Max(0f, Head.Volume);
            }
            else if (ending == RunEnding.Pooled || ending == RunEnding.TimedOut || ending == RunEnding.Abandoned)
            {
                // The water stays on the mountain. Basins remember it between runs.
                //
                // Abandoned belongs here and did not use to. Abort() fell through both branches and
                // zeroed Head.Volume, which destroys the run's water silently — invariant 6, the one
                // this project has already broken twice. It went unnoticed because Abort() had no
                // callers at all; a back button that can be pressed mid-run makes it live. Walking
                // away from a run does not evaporate the water you released.
                int cell = _f.NearestIndex(Head.Pos.x, Head.Pos.y);
                _world.Basins.AddWater(cell, Mathf.Max(0f, Head.Volume));
            }

            Head.Volume = 0f;
            _f.MarkAllDirty();
        }

        public void Abort()
        {
            if (!Running) return;
            Finish(RunEnding.Abandoned, deliverVolume: false);
        }

        /// <summary>Falloff-weighted area of the brush, m^2. ∫(1-d²)² over the disc = πr²/3.</summary>
        float EffectiveBrushArea(float radiusCells)
        {
            float r = radiusCells * _f.CellSize;
            return Mathf.PI * r * r / 3f;
        }
    }
}
