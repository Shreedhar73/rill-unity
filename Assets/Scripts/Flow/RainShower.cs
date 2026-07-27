using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.Flow
{
    /// <summary>
    /// A held finger summons a brief shower: the petting interaction. Between runs the mountain
    /// was look-but-don't-touch; rain is zero-stakes water that finds the player's own channels
    /// and shows them working, every time. It carves nothing — a free interaction must not be a
    /// free run — but it is real water under invariant 6: every drop reaches the sea, seeps to a
    /// basin, or infiltrates, and the ledger is returned so a test can hold the mass balance.
    ///
    /// The volumes are deliberately tiny (a shower is ~2% of one run) so rain reads as affection,
    /// not as an alternative economy.
    /// </summary>
    public sealed class RainShower
    {
        public enum Fate { Infiltrated, Basin, Sea }

        public sealed class Drop
        {
            public readonly List<Vector3> Trace = new List<Vector3>();
            public float Volume;
            public Fate Fate;
            public int EndCell;
        }

        public readonly List<Drop> Drops = new List<Drop>();
        public float ToBasins, ToSea, Infiltrated;

        const float StepSeconds = 0.1f;
        const int MaxSteps = 300;

        /// <summary>
        /// Traces the shower. Pure computation — nothing in the world changes until Apply — so
        /// the ledger can be inspected headlessly and the visuals can play back at their own pace.
        /// </summary>
        public static RainShower Compute(RillWorld world, Vector2 centreXZ, float totalVolume, int dropCount, float radius, uint salt)
        {
            var shower = new RainShower();
            var field = world.Field;
            var rng = new Rng(Noise.Hash(salt ^ world.Seed));
            float perDrop = totalVolume / Mathf.Max(1, dropCount);

            for (int d = 0; d < dropCount; d++)
            {
                var drop = new Drop { Volume = perDrop };
                Vector2 pos = centreXZ + new Vector2(rng.Range(-radius, radius), rng.Range(-radius, radius));
                Vector2 vel = Vector2.zero;

                for (int s = 0; s < MaxSteps; s++)
                {
                    drop.Trace.Add(new Vector3(pos.x, field.SampleHeightWorld(pos.x, pos.y) + 0.25f, pos.y));

                    if (world.IsSea(pos.x, pos.y)) { drop.Fate = Fate.Sea; break; }
                    if (field.SampleWaterWorld(pos.x, pos.y) > 0.25f)
                    {
                        // Standing water is only a delivery if a real basin is under it. Unnamed
                        // sinks hold water too, and AddWater on one silently discards — the first
                        // run of the mass-balance test caught exactly half a shower vanishing
                        // that way. A drop into an unnamed hollow soaks in, honestly.
                        var basin = world.Basins.BasinAt(field.NearestIndex(pos.x, pos.y));
                        drop.Fate = basin != null ? Fate.Basin : Fate.Infiltrated;
                        break;
                    }

                    float slope;
                    Vector2 downhill = field.DownhillWorld(pos.x, pos.y, out slope);
                    float polish = field.SamplePolishWorld(pos.x, pos.y);

                    // Rain is light: on rough rock it soaks in quickly, in a carved channel it
                    // runs. This bias is what makes a shower a demonstration of the network.
                    if (slope < 0.02f && polish < 0.1f) { drop.Fate = Fate.Infiltrated; break; }

                    float speed = (1.2f + 6f * slope) * (0.6f + 1.4f * Mathf.Clamp01(polish * 2f));
                    vel = Vector2.Lerp(vel, downhill * speed, 0.5f);
                    pos += vel * StepSeconds;

                    if (s == MaxSteps - 1) drop.Fate = Fate.Infiltrated;
                }

                drop.EndCell = field.NearestIndex(pos.x, pos.y);
                switch (drop.Fate)
                {
                    case Fate.Sea: shower.ToSea += perDrop; break;
                    case Fate.Basin: shower.ToBasins += perDrop; break;
                    default: shower.Infiltrated += perDrop; break;
                }
                shower.Drops.Add(drop);
            }
            return shower;
        }

        /// <summary>
        /// Lands the shower on the world: basin drops become held water, every trace dampens the
        /// rock it crossed (visibly, and briefly — wetness decays between runs as it always has).
        /// Infiltrated and sea water leave nothing behind but the damp, which is invariant 6's
        /// "infiltrates" honestly rendered.
        /// </summary>
        public void Apply(RillWorld world)
        {
            var field = world.Field;
            ToBasins = 0f;
            for (int d = 0; d < Drops.Count; d++)
            {
                var drop = Drops[d];
                if (drop.Fate == Fate.Basin)
                {
                    // Only into headroom, never through AddWater's overflow path: a petting
                    // gesture must not trigger a dam break, and a brim-full tarn clamping the
                    // volume away silently is exactly the mass-balance hole the headless test
                    // caught (0.75 of 1.50 m³ vanished). Rain on a full lake soaks away.
                    var basin = world.Basins.BasinAt(drop.EndCell);
                    float headroom = basin != null ? basin.Capacity - basin.Volume : 0f;
                    if (headroom >= drop.Volume)
                    {
                        basin.Volume += drop.Volume;
                        ToBasins += drop.Volume;
                    }
                    else
                    {
                        drop.Fate = Fate.Infiltrated;
                    }
                }
                for (int i = 0; i < drop.Trace.Count; i++)
                {
                    int cell = field.NearestIndex(drop.Trace[i].x, drop.Trace[i].z);
                    field.Wet[cell] = Mathf.Min(1f, field.Wet[cell] + 0.30f);
                }
            }
            // Settle the ledger to what actually landed, so it always sums to the shower.
            float total = 0f;
            for (int d = 0; d < Drops.Count; d++) total += Drops[d].Volume;
            Infiltrated = total - ToBasins - ToSea;

            world.Basins.SolveLevels(false);
            field.MarkAllDirty();
        }
    }
}
