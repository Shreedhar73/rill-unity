using System;
using UnityEngine;
using Rill.Core;

namespace Rill.World
{
    public enum WeatherKind
    {
        Clear = 0,
        Storm = 1,
        Drought = 2,
        Snowmelt = 3,
        MeteorShower = 4
    }

    /// <summary>
    /// Weather is an invitation, never a punishment. A storm doubles your water for twelve hours;
    /// missing it costs nothing because another one is always coming. Live-ops here is a
    /// parameter change, not an asset drop — which is why one small team can run this forever.
    /// </summary>
    public sealed class WeatherSystem
    {
        public WeatherKind Kind { get; private set; }
        public float VolumeMultiplier { get; private set; } = 1f;
        public float CarveMultiplier { get; private set; } = 1f;
        public float BrushMultiplier { get; private set; } = 1f;
        public Color SkyTint { get; private set; } = new Color(0.72f, 0.82f, 0.92f);
        public string Headline { get; private set; } = "Clear skies";
        public DateTime WindowEndUtc { get; private set; }

        readonly uint _worldSeed;

        public WeatherSystem(uint worldSeed)
        {
            _worldSeed = worldSeed;
            Evaluate(DateTime.UtcNow);
        }

        /// <summary>
        /// Deterministic from the date, so "storm tonight" is the same event for every player and
        /// can be talked about, without any server telling anyone anything.
        /// </summary>
        public void Evaluate(DateTime utcNow)
        {
            Set(KindFor(utcNow));
            WindowEndUtc = utcNow.Date.AddHours(utcNow.Hour < 12 ? 12 : 24);
        }

        /// <summary>
        /// The weather for any moment, past or future. Static and pure because the whole point of
        /// date-derived weather is that tomorrow is already knowable — the forecast is this
        /// function called on tomorrow, so it CANNOT disagree with what Evaluate will do when
        /// tomorrow arrives.
        /// </summary>
        public static WeatherKind KindFor(DateTime utc)
        {
            int dayIndex = (int)(utc.Date - new DateTime(2024, 1, 1)).TotalDays;
            int half = utc.Hour < 12 ? 0 : 1;
            uint h = Noise.Hash((uint)dayIndex * 2654435761u ^ (uint)half * 40503u ^ 0x1234u);
            float roll = (h & 0xffff) / 65536f;

            // Seasons bias the roll: spring is meltwater, late summer is dry.
            int month = utc.Month;
            bool spring = month >= 3 && month <= 5;
            bool summer = month >= 6 && month <= 8;

            if (spring && roll < 0.22f) return WeatherKind.Snowmelt;
            if (summer && roll < 0.16f) return WeatherKind.Drought;
            if (roll < 0.30f) return WeatherKind.Storm;
            if (roll < 0.34f) return WeatherKind.MeteorShower;
            if (roll < 0.42f) return WeatherKind.Drought;
            return WeatherKind.Clear;
        }

        /// <summary>
        /// One line about the next change in the weather, or null while nothing changes. An
        /// appointment rather than an ambush: "snowmelt tomorrow" is a reason to open the app
        /// tomorrow specifically, and here it is true rather than manufactured. Looks at most two
        /// windows (24 h) ahead — a forecast past tomorrow is trivia, not an appointment.
        /// </summary>
        public string ForecastLine(DateTime utcNow)
        {
            WeatherKind current = KindFor(utcNow);
            DateTime next = utcNow.Date.AddHours(utcNow.Hour < 12 ? 12 : 24);
            for (int i = 0; i < 2; i++)
            {
                WeatherKind k = KindFor(next);
                if (k != current)
                {
                    string when = next.Date == utcNow.Date ? "This evening" : "Tomorrow";
                    return when + ": " + Describe(k);
                }
                next = next.AddHours(12);
            }
            return null;
        }

        static string Describe(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Storm: return "a storm — double water";
                case WeatherKind.Drought: return "drought — narrow, deeper cuts";
                case WeatherKind.Snowmelt: return "snowmelt — high, wide water";
                case WeatherKind.MeteorShower: return "a meteor shower";
                default: return "clear skies";
            }
        }

        void Set(WeatherKind k)
        {
            Kind = k;
            switch (k)
            {
                case WeatherKind.Storm:
                    VolumeMultiplier = 2f; CarveMultiplier = 1.1f; BrushMultiplier = 1.25f;
                    SkyTint = new Color(0.42f, 0.48f, 0.58f);
                    Headline = "Storm — double water";
                    break;
                case WeatherKind.Drought:
                    // Less water, but it runs concentrated: the precision-carving window.
                    VolumeMultiplier = 0.65f; CarveMultiplier = 1.55f; BrushMultiplier = 0.7f;
                    SkyTint = new Color(0.92f, 0.84f, 0.66f);
                    Headline = "Drought — narrow, deeper cuts";
                    break;
                case WeatherKind.Snowmelt:
                    VolumeMultiplier = 1.6f; CarveMultiplier = 0.9f; BrushMultiplier = 1.4f;
                    SkyTint = new Color(0.80f, 0.90f, 0.96f);
                    Headline = "Snowmelt — high, wide water";
                    break;
                case WeatherKind.MeteorShower:
                    VolumeMultiplier = 1f; CarveMultiplier = 1f; BrushMultiplier = 1f;
                    SkyTint = new Color(0.20f, 0.22f, 0.34f);
                    Headline = "Meteor shower — glowing minerals seeded";
                    break;
                default:
                    VolumeMultiplier = 1f; CarveMultiplier = 1f; BrushMultiplier = 1f;
                    SkyTint = new Color(0.72f, 0.82f, 0.92f);
                    Headline = "Clear skies";
                    break;
            }
        }
    }
}
