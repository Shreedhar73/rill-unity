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
                Head.Vel += right * (lateral * _cfg.SteerAccel * dt);
                // Fighting gravity bleeds momentum. Expert play is knowing when NOT to touch.
                float bleed = 1f - _cfg.SteerSpeedCost * Mathf.Abs(lateral) * dt;
                Head.Vel *= Mathf.Max(0f, bleed);
            }

            // --- drag: fresh rock is slow, your own polished channel is fast. The whole economy.
            float drag = Mathf.Lerp(_cfg.DragFresh, _cfg.DragPolished, polish);

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
            Vector2Int sp = b.SpillXZ(_f.Size);
            _crossingTo = _f.GridToWorldXZ(sp.x, sp.y);
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

        void Finish(RunEnding ending, bool deliverVolume)
        {
            Running = false;
            Ending = ending;
            VolumeAtEnd = Mathf.Max(0f, Head.Volume);
            DistanceAfterCrossing = CrossedAnyBasin ? Distance - _distanceAtLastCrossing : 0f;

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
            else if (ending == RunEnding.Pooled || ending == RunEnding.TimedOut)
            {
                // The water stays on the mountain. Basins remember it between runs.
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
