using UnityEngine;

namespace Rill.Core
{
    /// <summary>
    /// Deterministic, table-free gradient noise. Identical output on every platform for a
    /// given seed, which is what makes Daily Rill seeds comparable between players.
    /// </summary>
    public static class Noise
    {
        public static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        public static uint Hash2(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x9e3779b1u ^ (uint)y * 0x85ebca77u ^ seed * 0xc2b2ae3du;
                return Hash(h);
            }
        }

        /// <summary>Uniform float in [0,1) from an integer lattice point.</summary>
        public static float Value(int x, int y, uint seed)
        {
            return (Hash2(x, y, seed) & 0xffffff) / 16777216f;
        }

        static void Gradient(int x, int y, uint seed, out float gx, out float gy)
        {
            // 8-way gradient set: cheap, well distributed, no directional artefacts at our scale.
            uint h = Hash2(x, y, seed) & 7u;
            const float s = 0.70710678f;
            switch (h)
            {
                case 0: gx = 1f; gy = 0f; break;
                case 1: gx = -1f; gy = 0f; break;
                case 2: gx = 0f; gy = 1f; break;
                case 3: gx = 0f; gy = -1f; break;
                case 4: gx = s; gy = s; break;
                case 5: gx = -s; gy = s; break;
                case 6: gx = s; gy = -s; break;
                default: gx = -s; gy = -s; break;
            }
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Perlin-style gradient noise in roughly [-1,1].</summary>
        public static float Perlin(float x, float y, uint seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            float g00x, g00y, g10x, g10y, g01x, g01y, g11x, g11y;
            Gradient(x0, y0, seed, out g00x, out g00y);
            Gradient(x0 + 1, y0, seed, out g10x, out g10y);
            Gradient(x0, y0 + 1, seed, out g01x, out g01y);
            Gradient(x0 + 1, y0 + 1, seed, out g11x, out g11y);

            float d00 = g00x * fx + g00y * fy;
            float d10 = g10x * (fx - 1f) + g10y * fy;
            float d01 = g01x * fx + g01y * (fy - 1f);
            float d11 = g11x * (fx - 1f) + g11y * (fy - 1f);

            float u = Fade(fx);
            float v = Fade(fy);
            float a = Mathf.Lerp(d00, d10, u);
            float b = Mathf.Lerp(d01, d11, u);
            return Mathf.Lerp(a, b, v) * 1.4142f;
        }

        /// <summary>Fractal Brownian motion. Output roughly [-1,1].</summary>
        public static float FBM(float x, float y, uint seed, int octaves = 5, float lacunarity = 2.03f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(x * freq, y * freq, seed + (uint)i * 7919u) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / Mathf.Max(norm, 1e-5f);
        }

        /// <summary>Ridged multifractal. Output [0,1]. Produces the spines a mountain needs.</summary>
        public static float Ridged(float x, float y, uint seed, int octaves = 5, float lacunarity = 2.07f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f, prev = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(x * freq, y * freq, seed + (uint)i * 104729u));
                n *= n;
                sum += n * amp * prev;
                prev = n;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return Mathf.Clamp01(sum / Mathf.Max(norm, 1e-5f));
        }
    }

    /// <summary>Deterministic PRNG (xorshift). Never use UnityEngine.Random for seeded content.</summary>
    public struct Rng
    {
        uint _s;

        public Rng(uint seed)
        {
            _s = seed == 0u ? 0x9e3779b9u : seed;
        }

        public uint NextUInt()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        public float Next01() => (NextUInt() & 0xffffff) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Next01();
        public int Range(int minInclusive, int maxExclusive)
        {
            int span = maxExclusive - minInclusive;
            if (span <= 0) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)span);
        }
    }
}
