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
            int dayIndex = (int)(utcNow.Date - new DateTime(2024, 1, 1)).TotalDays;
            int half = utcNow.Hour < 12 ? 0 : 1;
            uint h = Noise.Hash((uint)dayIndex * 2654435761u ^ (uint)half * 40503u ^ 0x1234u);
            float roll = (h & 0xffff) / 65536f;

            // Seasons bias the roll: spring is meltwater, late summer is dry.
            int month = utcNow.Month;
            bool spring = month >= 3 && month <= 5;
            bool summer = month >= 6 && month <= 8;

            if (spring && roll < 0.22f) Set(WeatherKind.Snowmelt);
            else if (summer && roll < 0.16f) Set(WeatherKind.Drought);
            else if (roll < 0.30f) Set(WeatherKind.Storm);
            else if (roll < 0.34f) Set(WeatherKind.MeteorShower);
            else if (roll < 0.42f) Set(WeatherKind.Drought);
            else Set(WeatherKind.Clear);

            WindowEndUtc = utcNow.Date.AddHours(half == 0 ? 12 : 24);
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
