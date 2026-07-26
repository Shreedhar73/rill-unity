using System;
using UnityEngine;

namespace Rill.World
{
    /// <summary>
    /// Where the sun is, and what colour the light and the sky are, at a given hour.
    ///
    /// The light was one fixed directional at Euler(46, 35, 0), set once at boot and never touched,
    /// so every session of every day looked identical — in a game whose entire premise is a world
    /// that carries time in it.
    ///
    /// Time of day comes from the player's LOCAL clock rather than from the seed, so playing in the
    /// evening looks like evening. That costs nothing, needs no content, and is the kind of detail
    /// that makes a world feel like a place rather than a level. The Daily is the exception: it has
    /// to look identical for everyone competing on it, so it asks for a fixed hour.
    ///
    /// Plain C# with no MonoBehaviour, so the capture tool can ask for any hour it likes and the
    /// smoke test can assert on the numbers.
    /// </summary>
    public struct SkyState
    {
        public Quaternion SunRotation;
        public Color SunColor;
        public float SunIntensity;
        public Color Ambient;
        public Color Sky;
        /// <summary>0 at solar noon, 1 in the deepest part of the night. Drives anything nocturnal.</summary>
        public float Night01;

        /// <summary>
        /// Multiplier for surfaces that do their own lighting and would otherwise ignore the sun.
        /// The water is unlit by design — its colour comes from depth, not from a normal — so at
        /// night the sea stayed full daytime blue against a mountain that had gone dark. Rendered
        /// and corrected; this is what the water shader multiplies by.
        /// </summary>
        public Color SurfaceTint;
    }

    public static class DayCycle
    {
        /// <summary>The hour the Daily is always lit at, so every player sees the same mountain.</summary>
        public const float DailyHour = 9.5f;

        public static float HourOf(DateTime local) => local.Hour + local.Minute / 60f;

        public static float LocalHour() => HourOf(DateTime.Now);

        /// <summary>
        /// The sky at a given hour, 0..24.
        ///
        /// Elevation is a cosine peaking at 13:00 rather than 12:00 — an afternoon sun reads better
        /// than a noon one, because a sun directly overhead flattens exactly the strata relief this
        /// game spends all its shading on. It never quite reaches vertical for the same reason.
        /// </summary>
        public static SkyState At(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);

            // -1 at midnight, +1 at 13:00.
            float t = Mathf.Cos((hour - 13f) / 24f * Mathf.PI * 2f);

            // Height of the sun above the horizon, in degrees. Peaks at 62 and not 90: an overhead
            // sun would light every face equally and erase the terracing.
            float elevation = t * 62f;

            // The sun swings through the sky rather than sitting at one compass bearing, or the
            // shadows would only ever get longer and never move.
            float azimuth = 35f + (hour - 13f) / 24f * 360f * 0.55f;

            // Day 1, night 0. The band is deliberately wide — it used to run from -8° to +12°,
            // which is about half an hour of real time, so 19:30 rendered identically to midnight
            // and dusk did not exist as a state at all. Rendered both and they were the same
            // picture. Twilight is the best-looking part of a day and it needs room.
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-16f, 10f, elevation));

            var noonSun = new Color(1f, 0.96f, 0.89f);
            var lowSun = new Color(1f, 0.72f, 0.45f);     // warm and horizontal, at either end
            var nightSun = new Color(0.42f, 0.52f, 0.78f); // moonlight, cold and weak

            // How close the sun is to the horizon, which is what makes light go orange.
            float low = 1f - Mathf.Clamp01(Mathf.Abs(elevation) / 40f);
            Color sunColor = Color.Lerp(noonSun, lowSun, low * 0.85f);
            sunColor = Color.Lerp(nightSun, sunColor, day);

            // The sky is a flat clear colour, not a gradient, and warm orange does not survive
            // that: at dawn the whole top of the screen came out one muddy brown. The warmth of a
            // low sun belongs on the LIGHT, where it falls across the terracing and reads as
            // morning; the flat fill stays a plausible sky at every hour. Rendered and corrected.
            var noonSky = new Color(0.72f, 0.82f, 0.92f);
            var duskSky = new Color(0.63f, 0.62f, 0.74f);   // washed violet, not orange
            var nightSky = new Color(0.05f, 0.08f, 0.15f);
            Color sky = Color.Lerp(noonSky, duskSky, low * 0.75f);
            sky = Color.Lerp(nightSky, sky, day);

            var noonAmbient = new Color(0.42f, 0.46f, 0.55f);
            var nightAmbient = new Color(0.10f, 0.13f, 0.22f);
            Color ambient = Color.Lerp(nightAmbient, noonAmbient, day);

            return new SkyState
            {
                // Negative elevation keeps the light below the horizon at night, so the mountain is
                // lit from beneath by almost nothing rather than from above by a blue sun.
                SunRotation = Quaternion.Euler(Mathf.Max(elevation, 2f), azimuth, 0f),
                SunColor = sunColor,
                SunIntensity = Mathf.Lerp(0.18f, 1.05f, day),
                Ambient = ambient,
                Sky = sky,
                SurfaceTint = Color.Lerp(new Color(0.26f, 0.32f, 0.46f), Color.white, day),
                Night01 = 1f - day
            };
        }
    }
}
