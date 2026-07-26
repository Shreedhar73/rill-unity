using UnityEngine;

namespace Rill.Core
{
    public enum Biome
    {
        Sandstone = 0,
        Granite = 1,
        Glacier = 2,
        Volcanic = 3
    }

    /// <summary>
    /// The strata stack. Art direction and simulation share one table: every band is both a
    /// colour the player sees and a hardness the water feels, so "erosion reveals prettier
    /// layers" and "erosion gets harder here" are the same fact.
    /// </summary>
    public struct StrataBand
    {
        public float TopElevation;  // band occupies (previous top .. TopElevation]
        public Color Color;
        public float Hardness;      // 0 soft .. 1 hard

        public StrataBand(float top, Color color, float hardness)
        {
            TopElevation = top;
            Color = color;
            Hardness = hardness;
        }
    }

    public static class StrataPalette
    {
        public static StrataBand[] For(Biome biome)
        {
            switch (biome)
            {
                case Biome.Granite:
                    return new[]
                    {
                        new StrataBand(6f,   C(0xE9DCC7), 0.30f),
                        new StrataBand(18f,  C(0xC9C3BA), 0.55f),
                        new StrataBand(34f,  C(0xA9A6A6), 0.72f),
                        new StrataBand(52f,  C(0x8E8C97), 0.84f),
                        new StrataBand(74f,  C(0x76788A), 0.90f),
                        new StrataBand(96f,  C(0x9AA0B4), 0.80f),
                        new StrataBand(130f, C(0xE4E9F2), 0.60f),
                        new StrataBand(999f, C(0xFFFFFF), 0.50f),
                    };
                case Biome.Glacier:
                    return new[]
                    {
                        new StrataBand(6f,   C(0x8FA9B8), 0.25f),
                        new StrataBand(18f,  C(0xA9C4CE), 0.35f),
                        new StrataBand(34f,  C(0xC3DAE0), 0.45f),
                        new StrataBand(52f,  C(0xDCEAEE), 0.55f),
                        new StrataBand(74f,  C(0xEFF7F9), 0.62f),
                        new StrataBand(999f, C(0xFFFFFF), 0.70f),
                    };
                case Biome.Volcanic:
                    return new[]
                    {
                        new StrataBand(6f,   C(0x4A403C), 0.35f),
                        new StrataBand(18f,  C(0x5C4A44), 0.50f),
                        new StrataBand(34f,  C(0x6E5148), 0.62f),
                        new StrataBand(52f,  C(0x7C4A3C), 0.74f),
                        new StrataBand(74f,  C(0x8E4634), 0.86f),
                        new StrataBand(999f, C(0x2E2724), 0.92f),
                    };
                default: // Sandstone — the starting mountain: soft, fast to carve, legible bands
                    return new[]
                    {
                        new StrataBand(4f,   C(0xE8D9B5), 0.18f),
                        new StrataBand(12f,  C(0xE2C393), 0.26f),
                        new StrataBand(22f,  C(0xD9A874), 0.34f),
                        new StrataBand(34f,  C(0xC98B5E), 0.46f),
                        new StrataBand(48f,  C(0xB97455), 0.55f),
                        new StrataBand(64f,  C(0xA8604F), 0.64f),
                        new StrataBand(82f,  C(0x8E5350), 0.72f),
                        new StrataBand(102f, C(0xA97A6B), 0.60f),
                        new StrataBand(999f, C(0xD8B79B), 0.45f),
                    };
            }
        }

        static Color C(int hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);
        }

        public static int BandIndex(StrataBand[] bands, float elevation)
        {
            for (int i = 0; i < bands.Length; i++)
                if (elevation <= bands[i].TopElevation) return i;
            return bands.Length - 1;
        }

        public static Color ColorAt(StrataBand[] bands, float elevation)
        {
            return bands[BandIndex(bands, elevation)].Color;
        }

        public static float HardnessAt(StrataBand[] bands, float elevation)
        {
            return bands[BandIndex(bands, elevation)].Hardness;
        }

        /// <summary>Sea, wet sand and the shallow-water tint the shoreline fades into.</summary>
        public static readonly Color SeaColor = new Color(0.18f, 0.42f, 0.52f, 1f);
        public static readonly Color WetColor = new Color(0.46f, 0.38f, 0.30f, 1f);
    }
}
