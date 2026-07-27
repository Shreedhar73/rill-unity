using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.Meta
{
    /// <summary>
    /// The one line on the end card about what is *almost* about to happen. "One more run" is fed
    /// by unfinished business, and the card only ever reported the past; the world is full of
    /// numbers that are nearly something — a basin at 91%, a secret half a metre from daylight —
    /// and none of them were ever said.
    ///
    /// Reads the world and awards nothing, like every other surface in this game. Every line here
    /// must be recomputable from the heightfield: if the teaser says 40 m³, pouring 40 m³ in must
    /// actually do the thing. No fabricated urgency, no invented countdowns.
    /// </summary>
    public static class NextTeaser
    {
        // A teaser that fires on every run is wallpaper; one that never fires is a system that
        // silently does nothing. The windows below aim between the two: close enough that the
        // promise is genuinely near, wide enough that a mid-game mountain usually has one.
        const float BasinNoticeFill = 0.55f;   // below this a basin is progress, not a promise
        const float BasinDoneFill = 0.995f;
        const float SecretNoticeBurial = 1.5f; // metres of rock left; matches the shimmer hint radius

        /// <summary>Best "next" line for this world, or null when nothing is genuinely close.</summary>
        public static string For(RillWorld world)
        {
            // Basin promise and secret promise are collected separately and alternated by run
            // number when both exist. Ranked purely by urgency, the shallowest secret won every
            // run of a 24-run session and the card read as a secrets ticker — variety between two
            // true lines beats strict priority between them.
            string basinLine = null, secretLine = null;
            float basinUrgency = 0f, secretUrgency = 0f;

            // A basin near its brim. The remaining volume is the exact number the card's own
            // basin rows already use, so the promise and the progress bar can never disagree.
            var basins = world.Basins.Basins;
            for (int i = 0; i < basins.Count; i++)
            {
                var b = basins[i];
                if (b.Capacity < 5f) continue;   // a puddle filling is not a promise
                float fill = b.FillFraction;
                if (fill < BasinNoticeFill || fill > BasinDoneFill) continue;
                float u = (fill - BasinNoticeFill) / (1f - BasinNoticeFill);
                if (u <= basinUrgency) continue;
                basinUrgency = u;
                string name = string.IsNullOrEmpty(b.Name) ? "A tarn" : b.Name;
                basinLine = string.Format("{0} wants {1:n0} m³ more", name, Mathf.Max(1f, b.Capacity - b.Volume));
            }

            // A secret close under the rock. Deliberately vague about where — the shimmer already
            // marks the spot in the world, and naming the place on a card would turn a discovery
            // into an errand.
            for (int i = 0; i < world.Secrets.Count; i++)
            {
                var s = world.Secrets[i];
                if (s.Revealed) continue;
                // Only sites the player's water has actually cut toward. Generation places some
                // secrets shallow, and an untouched one produced the same "0.3 m under the rock"
                // line on 23 of 24 runs — a promise that never moves is wallpaper, and worse, it
                // is a promise about a place the player has no channel to.
                if (world.Field.Virgin[s.Cell] - world.Field.Height[s.Cell] < 0.05f) continue;
                float burial = world.Field.Height[s.Cell] - s.RevealElevation;
                if (burial <= 0f || burial > SecretNoticeBurial) continue;
                float u = 1f - burial / SecretNoticeBurial;
                if (u <= secretUrgency) continue;
                secretUrgency = u;
                secretLine = string.Format("Something lies {0:0.0} m under the rock", burial);
            }

            if (basinLine != null && secretLine != null)
                return (world.RunNumber & 1) == 0 ? basinLine : secretLine;
            if (basinLine != null) return basinLine;
            if (secretLine != null) return secretLine;

            // A basin the water has never favoured, said only once the mountain is wet enough for
            // the contrast to mean something. Weakest of the three on purpose: it is a direction,
            // not a near-miss, so it only speaks when nothing is genuinely close.
            if (world.Basins.TotalWater() > 50f)
            {
                for (int i = 0; i < basins.Count; i++)
                {
                    var b = basins[i];
                    if (b.Capacity < 5f || b.Volume > 1f) continue;
                    string name = string.IsNullOrEmpty(b.Name) ? "One tarn" : b.Name;
                    return string.Format("{0} sits empty", name);
                }
            }

            return null;
        }
    }
}
