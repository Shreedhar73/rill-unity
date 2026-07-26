using UnityEngine;

namespace Rill.Core
{
    /// <summary>
    /// Droplet-based hydraulic erosion, run once when a mountain is generated.
    ///
    /// Why a virgin mountain needs erosion applied before the player ever sees it: noise alone
    /// produces lumps. Every real landscape the eye recognises as a mountain got its silhouette
    /// from water — dendritic valleys, sharpened ridgelines between them, alluvial fans where the
    /// slope breaks. Generating those with more noise never works, because they are not a texture,
    /// they are the *record of a process*. So we run the process.
    ///
    /// This also does the game a favour the player never sees: it leaves the terrain covered in
    /// shallow natural drainage, so the first run already has lines worth following, and the
    /// player's own carving deepens an existing hydrology rather than scribbling on a blank hill.
    /// </summary>
    public static class HydraulicErosion
    {
        public struct Settings
        {
            public int Droplets;
            public int MaxSteps;
            public float Inertia;          // 0 = water follows the gradient exactly, 1 = it never turns
            public float CapacityFactor;   // how much sediment a fast, full droplet can carry
            public float MinCapacity;      // stops slow water depositing everything at once
            public float ErodeSpeed;
            public float DepositSpeed;
            public float Evaporation;
            public float Gravity;
            public int ErosionRadius;      // cells; wider = smoother valley floors

            public static Settings Default
            {
                get
                {
                    return new Settings
                    {
                        Droplets = 60000,
                        MaxSteps = 42,
                        Inertia = 0.045f,
                        CapacityFactor = 3.6f,
                        MinCapacity = 0.012f,
                        ErodeSpeed = 0.32f,
                        DepositSpeed = 0.28f,
                        Evaporation = 0.022f,
                        Gravity = 4.0f,
                        ErosionRadius = 3
                    };
                }
            }
        }

        /// <summary>Erodes the field in place. Deterministic for a given seed.</summary>
        public static void Run(HeightField f, Settings s, uint seed)
        {
            int n = f.Size;
            var rng = new Rng(Noise.Hash(seed ^ 0x9d0e1a3fu));

            // Precomputed falloff weights for the erosion brush, so the inner loop stays cheap.
            int r = Mathf.Max(1, s.ErosionRadius);
            int span = r * 2 + 1;
            var brushDx = new int[span * span];
            var brushDz = new int[span * span];
            var brushW = new float[span * span];
            int brushCount = 0;
            float weightSum = 0f;
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > r) continue;
                    float w = 1f - d / r;
                    brushDx[brushCount] = dx;
                    brushDz[brushCount] = dz;
                    brushW[brushCount] = w;
                    weightSum += w;
                    brushCount++;
                }
            }
            for (int i = 0; i < brushCount; i++) brushW[i] /= weightSum;

            for (int d = 0; d < s.Droplets; d++)
            {
                float px = rng.Range(1f, n - 2f);
                float pz = rng.Range(1f, n - 2f);
                float dirX = 0f, dirZ = 0f;
                float speed = 1f, water = 1f, sediment = 0f;

                for (int step = 0; step < s.MaxSteps; step++)
                {
                    int cx = (int)px, cz = (int)pz;
                    if (cx < 1 || cz < 1 || cx >= n - 2 || cz >= n - 2) break;
                    float fx = px - cx, fz = pz - cz;

                    float gx, gz, height;
                    GradientAndHeight(f, n, cx, cz, fx, fz, out gx, out gz, out height);

                    // Momentum: a little inertia stops droplets zig-zagging one cell at a time,
                    // which is what turns scattered pits into continuous valleys.
                    dirX = dirX * s.Inertia - gx * (1f - s.Inertia);
                    dirZ = dirZ * s.Inertia - gz * (1f - s.Inertia);
                    float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                    if (len < 1e-6f) break;
                    dirX /= len;
                    dirZ /= len;

                    float npx = px + dirX;
                    float npz = pz + dirZ;
                    int ncx = (int)npx, ncz = (int)npz;
                    if (ncx < 1 || ncz < 1 || ncx >= n - 2 || ncz >= n - 2) break;

                    float newHeight = SampleBilinear(f.Height, n, npx, npz);
                    float deltaHeight = newHeight - height;

                    // Capacity falls to nothing as the slope flattens: that is what builds fans
                    // and floodplains at the foot of the mountain instead of one endless trench.
                    float capacity = Mathf.Max(-deltaHeight * speed * water * s.CapacityFactor, s.MinCapacity);

                    if (sediment > capacity || deltaHeight > 0f)
                    {
                        // Uphill step: fill the pit we just walked into, never more than its depth.
                        float deposit = deltaHeight > 0f
                            ? Mathf.Min(deltaHeight, sediment)
                            : (sediment - capacity) * s.DepositSpeed;
                        sediment -= deposit;
                        DepositBilinear(f.Height, n, cx, cz, fx, fz, deposit);
                    }
                    else
                    {
                        float erode = Mathf.Min((capacity - sediment) * s.ErodeSpeed, -deltaHeight);
                        for (int i = 0; i < brushCount; i++)
                        {
                            int ex = cx + brushDx[i], ez = cz + brushDz[i];
                            if (ex < 0 || ez < 0 || ex >= n || ez >= n) continue;
                            int idx = ez * n + ex;
                            float amount = erode * brushW[i];
                            f.Height[idx] -= amount;
                            sediment += amount;
                        }
                    }

                    speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + -deltaHeight * s.Gravity));
                    water *= 1f - s.Evaporation;
                    if (water < 0.01f) break;

                    px = npx;
                    pz = npz;
                }
            }

            f.MarkAllDirty();
        }

        static void GradientAndHeight(HeightField f, int n, int cx, int cz, float fx, float fz,
                                      out float gx, out float gz, out float height)
        {
            int i = cz * n + cx;
            float h00 = f.Height[i];
            float h10 = f.Height[i + 1];
            float h01 = f.Height[i + n];
            float h11 = f.Height[i + n + 1];

            gx = (h10 - h00) * (1f - fz) + (h11 - h01) * fz;
            gz = (h01 - h00) * (1f - fx) + (h11 - h10) * fx;
            height = h00 * (1f - fx) * (1f - fz) + h10 * fx * (1f - fz) + h01 * (1f - fx) * fz + h11 * fx * fz;
        }

        static float SampleBilinear(float[] a, int n, float px, float pz)
        {
            int cx = (int)px, cz = (int)pz;
            float fx = px - cx, fz = pz - cz;
            int i = cz * n + cx;
            return a[i] * (1f - fx) * (1f - fz) + a[i + 1] * fx * (1f - fz)
                 + a[i + n] * (1f - fx) * fz + a[i + n + 1] * fx * fz;
        }

        static void DepositBilinear(float[] a, int n, int cx, int cz, float fx, float fz, float amount)
        {
            int i = cz * n + cx;
            a[i] += amount * (1f - fx) * (1f - fz);
            a[i + 1] += amount * fx * (1f - fz);
            a[i + n] += amount * (1f - fx) * fz;
            a[i + n + 1] += amount * fx * fz;
        }
    }
}
