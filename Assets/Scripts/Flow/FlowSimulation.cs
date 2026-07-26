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
            Elapsed = Distance = TopSpeed = WaterToSea = SedimentMoved = 0f;
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

            if (waterHere > 0.05f)
            {
                // A lake with room left in it absorbs the run — that is what a basin is for.
                // A lake that is already near its spill level is not a sink but a piece of river:
                // the water crosses it and leaves by the outlet. Without this every run in the
                // game ends in the first depression it finds and nothing ever reaches the sea.
                var basin = _world.Basins.BasinAt(_f.NearestIndex(Head.Pos.x, Head.Pos.y));
                if (basin != null && basin.FillFraction > 0.5f)
                {
                    Vector2Int sp = basin.SpillXZ(_f.Size);
                    Vector2 toSpill = _f.GridToWorldXZ(sp.x, sp.y) - Head.Pos;
                    if (toSpill.sqrMagnitude > 1e-4f)
                        Head.Vel += toSpill.normalized * (_cfg.Gravity * 0.35f * dt);
                    drag += 0.6f;
                }
                else
                {
                    drag += 2.5f;
                }
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

        void Finish(RunEnding ending, bool deliverVolume)
        {
            Running = false;
            Ending = ending;

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
