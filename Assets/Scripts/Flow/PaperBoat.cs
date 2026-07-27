using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.Flow
{
    /// <summary>
    /// A paper boat released from the spring, riding whatever the mountain has become. No
    /// steering, no carving, no water spent — a pure reading of the network. How far it gets is
    /// the carved system's grade, measured rather than awarded: on virgin rock the boat grinds to
    /// a halt in metres; on a mountain with real channels it runs them all the way down. It is
    /// the design's own claim — "the mountain remembers" — made into a toy the player can release
    /// whenever they want to see what they have built.
    ///
    /// Plain C# and deterministic for a given world state, so the virgin-vs-mature contrast is a
    /// number a headless test can hold.
    /// </summary>
    public static class PaperBoat
    {
        public sealed class Result
        {
            public readonly List<Vector3> Path = new List<Vector3>();
            public readonly List<float> Speeds = new List<float>();
            public float Distance;
            public float Duration;
            public bool ReachedSea;
            public bool RestedInWater;
            public string RestingPlace;   // basin name when the voyage ends on a lake

            /// <summary>
            /// The network's grade as one number: how fast the mountain moved the boat over its
            /// whole voyage. Distance alone cannot be the reading — a mature mountain's own lakes
            /// legitimately end voyages early, and resting on a lake you carved is not a worse
            /// result than grinding to a halt on open rock.
            /// </summary>
            public float AverageSpeed => Duration > 0.1f ? Distance / Duration : 0f;
        }

        const float Dt = 1f / 30f;
        const float MaxSeconds = 150f;
        const float SurfaceOffset = 0.3f;

        /// <param name="spawnSalt">
        /// 0 (the game): spawn varies with RunNumber like a run's would. Non-zero (measurement):
        /// a fixed spawn per salt, so two readings of the same mountain at different run counts
        /// compare the network rather than two different launch points — the L-071 confound.
        /// </param>
        public static Result Sail(RillWorld world, uint spawnSalt = 0)
        {
            var r = new Result();
            var field = world.Field;

            // Same spring the runs use, same seed derivation, so the boat answers "what would my
            // water find" rather than sailing a course no run could take.
            var rng = new Rng(spawnSalt == 0
                ? Noise.Hash((uint)world.RunNumber * 2654435761u ^ world.Seed)
                : Noise.Hash(spawnSalt * 2654435761u ^ world.Seed));
            Vector3 spawn = world.SpawnPoint(ref rng);
            Vector2 pos = new Vector2(spawn.x, spawn.z);
            Vector2 vel = Vector2.zero;

            float slowTime = 0f;
            float t = 0f;
            Vector2 last = pos;

            while (t < MaxSeconds)
            {
                t += Dt;

                float slope;
                Vector2 downhill = field.DownhillWorld(pos.x, pos.y, out slope);
                float polish = field.SamplePolishWorld(pos.x, pos.y);
                float wet = field.SampleWetWorld(pos.x, pos.y);

                // The channel term is the whole toy: polished, damp rock carries the boat and
                // rough virgin rock eats its momentum, so the boat's range IS the network's
                // maturity. The constants are not tuned to feel — they are tuned so a virgin
                // mountain strands the boat and a played one does not, which the headless test
                // holds as a measured gap.
                float channel = Mathf.Clamp01(polish * 1.5f + wet * 0.5f);
                Vector2 accel = downhill * (9.81f * slope) * (0.55f + 0.45f * channel);
                float drag = Mathf.Lerp(1.8f, 0.35f, channel);

                vel += (accel - vel * drag) * Dt;
                pos += vel * Dt;

                float speed = vel.magnitude;
                r.Distance += Vector2.Distance(pos, last);
                last = pos;

                if ((r.Path.Count == 0) || t * 30f % 2f < 1f)
                {
                    r.Path.Add(new Vector3(pos.x, field.SampleHeightWorld(pos.x, pos.y) + SurfaceOffset, pos.y));
                    r.Speeds.Add(speed);
                }

                if (world.IsSea(pos.x, pos.y)) { r.ReachedSea = true; break; }

                if (field.SampleWaterWorld(pos.x, pos.y) > 0.3f)
                {
                    var basin = world.Basins.BasinAt(field.NearestIndex(pos.x, pos.y));

                    // A brim-full tarn is part of the network: the surface sits at the lip, so
                    // the boat drifts across toward the spill and sails on. Anything less full
                    // has shoreline above the waterline on every side that matters, and the
                    // voyage honestly ends there — on a lake the player carved, which the
                    // reading says by name.
                    if (basin != null && basin.FillFraction > 0.95f)
                    {
                        var spill = basin.SpillXZ(field.Size);
                        Vector2 spillW = field.GridToWorldXZ(spill.x, spill.y);
                        Vector2 to = spillW - pos;
                        if (to.magnitude > 1.5f)
                        {
                            vel = Vector2.Lerp(vel, to.normalized * 2.4f, 0.12f);
                            continue;
                        }
                        // At the lip: fall through and let the far slope take it.
                    }
                    else
                    {
                        r.RestedInWater = true;
                        r.RestingPlace = basin != null ? basin.Name : null;
                        break;
                    }
                }

                // Becalmed: a boat is allowed a slow bend, not a permanent rest.
                slowTime = speed < 0.15f ? slowTime + Dt : 0f;
                if (slowTime > 1.2f) break;
            }

            r.Duration = t;
            return r;
        }

        /// <summary>The reading, as one sentence. Distances only — nothing is awarded for them.</summary>
        public static string Describe(Result r)
        {
            if (r.ReachedSea) return string.Format("The boat sailed {0:0} m and reached the sea", r.Distance);
            if (r.RestedInWater)
                return string.IsNullOrEmpty(r.RestingPlace)
                    ? string.Format("The boat sailed {0:0} m and came to rest on still water", r.Distance)
                    : string.Format("The boat sailed {0:0} m and came to rest on {1}", r.Distance, r.RestingPlace);
            return string.Format("The boat ran aground after {0:0} m", r.Distance);
        }
    }
}
