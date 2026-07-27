using UnityEngine;
using Rill.Core;

namespace Rill.Audio
{
    /// <summary>
    /// What the mountain should sound like when nothing is running, read off the world like every
    /// other surface in this game. A mature river system murmurs, its greened slopes have birds,
    /// and bare young rock has only wind — so the difference between a virgin mountain and six
    /// months of play is audible before it is visible, and the difference between the three slots
    /// is three different rooms.
    ///
    /// Plain C# and pure, separate from the synth, so a headless test can hold the claim "virgin
    /// and mature sound different" as numbers rather than as an opinion about a mix.
    /// </summary>
    public static class AmbienceParams
    {
        /// <param name="stream01">Distant water murmur: how much carved, damp channel exists.</param>
        /// <param name="birds01">Birdsong density: how much of the mountain is alive.</param>
        /// <param name="wind01">Wind on bare rock: the complement of life, never fully absent.</param>
        public static void From(HeightField field, float[] life, out float stream01, out float birds01, out float wind01)
        {
            int n = field.Count;
            int polished = 0;
            float lifeSum = 0f;
            int land = 0;

            for (int i = 0; i < n; i++)
            {
                if (field.Height[i] <= field.SeaLevel) continue;
                land++;
                if (field.Polish[i] > 0.15f) polished++;
                if (life != null && i < life.Length) lifeSum += Mathf.Min(life[i], 6f);
            }
            if (land == 0) { stream01 = 0f; birds01 = 0f; wind01 = 1f; return; }

            // The scale factors turn "fraction of a whole mountain" into a usable 0..1: a healthy
            // network polishes ~1-2% of the field (smoke test: 949 cells of 65k at 24 runs), and
            // that should already read as a real stream, so 1.5% maps to full murmur.
            stream01 = Mathf.Clamp01(polished / (float)land / 0.015f);

            // Life tiers run 0..6; an average of 0.09 over the land (mixed moss and stands of
            // trees on a played mountain) is a living hillside, so that maps to full song.
            birds01 = Mathf.Clamp01(lifeSum / land / 0.09f);

            // Wind is the sound of what is still bare. Floored at 0.25: a mountain with no wind
            // at all sounds like a room, not a summit.
            wind01 = Mathf.Clamp01(1f - birds01 * 0.6f - stream01 * 0.15f);
            if (wind01 < 0.25f) wind01 = 0.25f;
        }
    }
}
