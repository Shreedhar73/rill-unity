using System.Collections.Generic;
using UnityEngine;
using Rill.Core;

namespace Rill.Flow
{
    public enum RunEnding
    {
        ReachedSea,
        Pooled,
        SoakedAway,
        TimedOut,
        Abandoned
    }

    /// <summary>
    /// The proof that the run mattered. Every run produces one of these, including the bad ones —
    /// a run that "fails" still carved, and this is where the player reads that.
    /// </summary>
    public class CarveReport
    {
        public int RunNumber;
        public RunEnding Ending;
        public float Duration;
        public float DistanceTravelled;
        public float TopSpeed;
        public float WaterToSea;          // m^3 delivered
        public float SedimentMoved;       // m^3
        public float DeepestCarve;        // metres at the single most-deepened cell
        public Vector3 DeepestCarveWorld;
        public int CellsChanged;
        public float NewChannelMetres;    // length of channel that crossed the "is a channel now" line

        public readonly List<string> Headlines = new List<string>();

        /// <summary>
        /// Set when the basin lattice itself changed shape this run — a tarn silted out of
        /// existence, or two became one. Held as its own field rather than fished back out of
        /// Headlines by matching prose, which is how the overflow case does it and is fragile.
        /// </summary>
        public string LatticeChange;
        public readonly List<SecretSite> Revealed = new List<SecretSite>();
        public readonly List<BasinDelta> BasinChanges = new List<BasinDelta>();
        public readonly List<string> LifeArrivals = new List<string>();
        public bool Overflowed;
        public string OverflowBasin;

        /// <summary>
        /// The one world-derived line about what is almost about to happen, or null. Set by the
        /// controller after the run's consequences have all landed, because a teaser computed
        /// before deposition could promise a basin the run just silted shut.
        /// </summary>
        public string NextLine;

        // Things the run picked up on the way down. None of them are currency.
        public int SeedsCaught;
        public int FlowersSplashed;
        public int GatesThreaded;

        public struct BasinDelta
        {
            public string Name;
            public float Before01;
            public float After01;
            public float AddedVolume;
        }

        public string EndingLine
        {
            get
            {
                switch (Ending)
                {
                    case RunEnding.ReachedSea: return "Reached the sea";
                    case RunEnding.Pooled: return "Pooled and rested";
                    case RunEnding.SoakedAway: return "Soaked into dry ground";
                    case RunEnding.TimedOut: return "Ran until the rain was gone";
                    default: return "Ended";
                }
            }
        }

        /// <summary>The one sentence the player actually reads. Never negative — the mountain always changed.</summary>
        public string Summary()
        {
            if (Overflowed) return OverflowBasin + " broke its banks";
            // A lake ceasing to exist outranks a secret or a fill percentage: it is the end of
            // something the player spent runs on, and it can only be said once.
            if (!string.IsNullOrEmpty(LatticeChange)) return LatticeChange;
            if (Revealed.Count > 0) return Revealed[0].DisplayName + " uncovered";
            if (BasinChanges.Count > 0)
            {
                var b = BasinChanges[0];
                return string.Format("{0} now {1:0}% full", b.Name, b.After01 * 100f);
            }
            if (DeepestCarve > 0.01f) return string.Format("Channel deepened {0:0.00} m", DeepestCarve);
            return "Sediment settled";
        }
    }
}
