using System.Collections.Generic;
using UnityEngine;

namespace Rill.Core
{
    /// <summary>
    /// Builds a virgin mountain from a seed. Deterministic: the same seed produces the same
    /// mountain on every device, which is what Daily Rill and shared seeds rely on.
    /// </summary>
    public static class MountainGenerator
    {
        public struct Settings
        {
            public int Size;
            public float CellSize;
            public uint Seed;
            public Biome Biome;
            public float PeakHeight;      // metres above sea level at the summit
            public float ShoreRadius01;   // fraction of half-extent where land meets sea
            public int SecretCount;

            public static Settings Default(uint seed, Biome biome = Biome.Sandstone)
            {
                return new Settings
                {
                    Size = 256,
                    CellSize = 2.0f,
                    Seed = seed,
                    Biome = biome,
                    PeakHeight = 120f,
                    ShoreRadius01 = 0.86f,
                    SecretCount = 60
                };
            }
        }

        public static HeightField Generate(Settings s, out Vector2Int summitCell, out List<SecretSite> secrets)
        {
            var field = new HeightField(s.Size, s.CellSize);
            var rng = new Rng(Noise.Hash(s.Seed ^ 0x5bf03635u));
            var bands = StrataPalette.For(s.Biome);

            // Summit is offset from dead centre so the mountain is never symmetric and the
            // player's first instinct ("go down the near face") is already a real choice.
            float summitAngle = rng.Range(0f, Mathf.PI * 2f);
            float summitOff = rng.Range(0.04f, 0.16f) * s.Size;
            Vector2 summit = new Vector2(s.Size * 0.5f + Mathf.Cos(summitAngle) * summitOff,
                                         s.Size * 0.5f + Mathf.Sin(summitAngle) * summitOff);

            float half = s.Size * 0.5f;
            float freq = 3.2f / s.Size;
            float detailFreq = 11f / s.Size;
            uint ns = Noise.Hash(s.Seed);

            for (int z = 0; z < s.Size; z++)
            {
                for (int x = 0; x < s.Size; x++)
                {
                    int i = z * s.Size + x;

                    // Radial island mask measured from the summit, not the centre.
                    float dx = (x - summit.x) / half;
                    float dz = (z - summit.y) / half;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);

                    // Warp the coastline so it is never a circle.
                    float coastWarp = Noise.FBM(x * freq * 0.7f, z * freq * 0.7f, ns + 991u, 3) * 0.18f;
                    float shore = s.ShoreRadius01 + coastWarp;
                    float mask = Mathf.Clamp01(1f - Mathf.SmoothStep(shore * 0.35f, shore, d));


                    // Cone + ridged spines + fbm detail. Spines are what give first-time players
                    // a reason to steer: the obvious line down a spine is fast but goes nowhere.
                    //
                    // The ridge field is domain-warped before it is sampled. Un-warped ridged
                    // noise produces a radially symmetric starburst of spines; warping bends them
                    // into the long, curving, interconnected ridgelines real massifs have.
                    float warpX = Noise.FBM(x * freq * 0.5f, z * freq * 0.5f, ns + 4211u, 3) * 24f;
                    float warpZ = Noise.FBM(x * freq * 0.5f, z * freq * 0.5f, ns + 8677u, 3) * 24f;

                    float cone = Mathf.Pow(mask, 1.15f);
                    float ridges = Noise.Ridged((x + warpX) * freq, (z + warpZ) * freq, ns, 6);
                    float detail = Noise.FBM(x * detailFreq, z * detailFreq, ns + 77u, 4);

                    // Ridges carry most of the height rather than sitting on top of a smooth cone,
                    // so the silhouette is made of edges instead of a dome with bumps.
                    float h = s.PeakHeight * cone * (0.24f + 0.86f * ridges);
                    h += detail * 9f * mask;

                    // Sea floor: keep dropping past the shoreline so the sea reads as a basin.
                    h -= (1f - mask) * 16f;

                    field.Height[i] = h;
                    field.Hardness[i] = 0.82f + 0.36f * (Noise.FBM(x * detailFreq * 1.7f, z * detailFreq * 1.7f, ns + 313u, 3) * 0.5f + 0.5f);
                }
            }

            // Talus is expressed per cell, so it must scale with cell size or the mountain comes
            // out a pillow: 1.4 m over a 2 m cell is a 35° face, which is what rock actually does.
            ThermalRelax(field, 2, 0.9f * s.CellSize);

            // The step that turns a noise field into a landscape. Sixty thousand droplets cut
            // dendritic valleys and sharpen the ridgelines between them — the shapes the eye
            // reads as "mountain" are a record of water, so the only honest way to get them is
            // to run water over it. It also leaves natural drainage for the player to find.
            HydraulicErosion.Run(field, HydraulicErosion.Settings.Default, s.Seed);

            // Hard bands stand out as cliffs, soft bands weather back into ledges. This is where
            // the strata stop being a colour scheme and start being terrain the player must read.
            Terrace(field, bands, 3.2f, 0.55f);

            // Erosion and terracing both push material around; put the summit back where the
            // design asked for it so PeakHeight means what it says.
            NormaliseTo(field, s.PeakHeight);

            // Droplet erosion is a pit-filling process: it leaves a beautifully drained mountain
            // with nowhere for water to collect. But the basin lattice IS the retention design —
            // "east basin 87% full" is the open loop the player comes back to close — so the
            // tarns have to be cut deliberately after erosion, at a range of sizes so there are
            // always several part-finished at once.
            // The spring has to be known before the tarns are cut, because where they can be cut
            // depends on where water from it can actually get to. HighestCell is a pure query and
            // the summit does not move materially over the two smoothing passes that follow.
            Vector2 preSummit = HighestCell(field);
            CarveBasins(field, 5, field.Index(Mathf.RoundToInt(preSummit.x), Mathf.RoundToInt(preSummit.y)), ref rng);

            ThermalRelax(field, 1, 1.1f * s.CellSize);

            // No summit bowl. An earlier version dished the peak "so rain gathers" and it trapped
            // every run within a few metres of the spawn — the water pooled before it ever flowed.
            // A summit is a place with slopes in every direction; that is the whole point of one.
            summit = HighestCell(field);
            CarveSpawnNotch(field, new Vector2(summit.x, summit.y), s.CellSize);

            field.CopyHeightTo(field.Virgin);
            field.MarkAllDirty();

            summitCell = new Vector2Int(Mathf.RoundToInt(summit.x), Mathf.RoundToInt(summit.y));
            secrets = PlaceSecrets(field, bands, s, summitCell, ref rng);
            return field;
        }

        /// <summary>
        /// Steps the terrain toward the strata so that hard bands finish as cliffs and soft bands
        /// as ledges. Treads and risers, not a staircase: the effect is scaled by the hardness of
        /// the band each cell sits in, so a soft mountain barely terraces and granite reads as
        /// stacked shelves. This is what makes the per-pixel strata in the shader line up with
        /// terrain the player can actually feel under the water.
        /// </summary>
        static void Terrace(HeightField f, StrataBand[] bands, float stepMetres, float strength)
        {
            for (int i = 0; i < f.Count; i++)
            {
                float h = f.Height[i];
                if (h <= f.SeaLevel + 1f) continue;

                float hard = StrataPalette.HardnessAt(bands, h) * f.Hardness[i];
                float amount = Mathf.Clamp01(strength * hard);
                if (amount <= 0.001f) continue;

                float t = h / stepMetres;
                float band = Mathf.Floor(t);
                float frac = t - band;
                // Smoothstep flattens the tread and steepens the riser.
                float shaped = frac * frac * (3f - 2f * frac);
                f.Height[i] = (band + Mathf.Lerp(frac, shaped, amount)) * stepMetres;
            }
            f.MarkAllDirty();
        }

        /// <summary>
        /// Cuts tarns into the eroded mountain: shallow bowls on the gentler ground, sized so a
        /// small one fills in a handful of runs and a big one is a project for a fortnight.
        /// Each keeps a low lip on its downhill side, so when it finally overflows the water has
        /// somewhere obvious to go and the dam break reads as a place rather than an accident.
        /// </summary>
        static void CarveBasins(HeightField f, int count, int springCell, ref Rng rng)
        {
            // Rank every candidate site and take the best, rather than sampling at random and
            // hoping one passes a threshold. An earlier version rejection-sampled on slope and
            // silently placed zero basins on every seed — the whole retention loop was missing
            // and nothing in the game said so. Scoring cannot fail to produce N sites.
            var sites = new System.Collections.Generic.List<int>(2048);
            var scores = new System.Collections.Generic.List<float>(2048);

            // Only ground the spring's water can actually reach without climbing. Scoring by
            // concavity alone describes where a lake *could* sit on this mountain, which is not the
            // same question as where the player can put water — and the answer differed badly:
            // 4 of the 5 tarns on the default seed sat off the summit's drainage entirely and could
            // never be filled by any amount of steering, while reading as 0% forever in a lattice
            // the whole retention design leans on.
            //
            // This is the same mistake, and the same fix, as L-010: flow accumulation describes the
            // mountain, descent from the spring describes the game.
            bool[] reachable = DownhillFrom(f, springCell);
            int onDrainage = 0;

            for (int z = 20; z < f.Size - 20; z += 3)
            {
                for (int x = 20; x < f.Size - 20; x += 3)
                {
                    int i = f.Index(x, z);
                    float h = f.Height[i];
                    if (h < 10f || h > 110f) continue;
                    if (!reachable[i]) continue;
                    onDrainage++;

                    // Flat ground scores well; ground that already collects water scores better.
                    float relief = Relief(f, x, z, 9);
                    float concavity = Concavity(f, x, z);
                    sites.Add(i);
                    scores.Add(concavity * 2.2f - relief / 18f);
                }
            }
            if (sites.Count == 0)
            {
                Debug.Log("[RILL] No basin site on the spring's drainage — carved none.");
                return;
            }

            var order = new int[sites.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => scores[b].CompareTo(scores[a]));

            var used = new System.Collections.Generic.List<Vector2Int>(count);
            for (int oi = 0; oi < order.Length && used.Count < count; oi++)
            {
                int cell = sites[order[oi]];
                int x = cell % f.Size, z = cell / f.Size;

                bool tooClose = false;
                for (int k = 0; k < used.Count; k++)
                    if ((used[k].x - x) * (used[k].x - x) + (used[k].y - z) * (used[k].y - z) < 38 * 38) { tooClose = true; break; }
                if (tooClose) continue;

                float h = f.Height[cell];

                // A spread of sizes so there are always several basins at different fill levels:
                // one nearly done, one halfway, one that is a fortnight's project.
                float radius = rng.Range(7f, 14f);
                float depth = rng.Range(1.8f, 5.5f);

                // Find the downhill direction so the lip can be left open there.
                float bestDrop = 0f;
                float exitX = 1f, exitZ = 0f;
                for (int k = 0; k < 8; k++)
                {
                    int dx = (k == 0 || k == 4 || k == 5) ? 1 : (k == 1 || k == 6 || k == 7) ? -1 : 0;
                    int dz = (k == 2 || k == 4 || k == 6) ? 1 : (k == 3 || k == 5 || k == 7) ? -1 : 0;
                    int nx = x + dx * 12, nz = z + dz * 12;
                    if (!f.InBounds(nx, nz)) continue;
                    float drop = h - f.Height[f.Index(nx, nz)];
                    if (drop > bestDrop) { bestDrop = drop; exitX = dx; exitZ = dz; }
                }

                int r = Mathf.CeilToInt(radius);

                // Excavate to an absolute level rather than subtracting a bump from whatever was
                // there. Cutting a dish out of a hillside does not make a lake — the downhill lip
                // is still downhill and the water runs straight out. A tarn needs a floor and a
                // rim that closes ALL the way round, with one low point that becomes the spill.
                float minH = float.MaxValue;
                for (int dz = -r; dz <= r; dz++)
                {
                    int cz = z + dz;
                    if (cz < 0 || cz >= f.Size) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int cx = x + dx;
                        if (cx < 0 || cx >= f.Size) continue;
                        if (dx * dx + dz * dz > radius * radius) continue;
                        float hh = f.Height[cz * f.Size + cx];
                        if (hh < minH) minH = hh;
                    }
                }
                if (minH == float.MaxValue) continue;

                float floorLevel = minH - depth * 0.2f;

                for (int dz = -r; dz <= r; dz++)
                {
                    int cz = z + dz;
                    if (cz < 0 || cz >= f.Size) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int cx = x + dx;
                        if (cx < 0 || cx >= f.Size) continue;
                        float d = Mathf.Sqrt(dx * dx + dz * dz) / radius;
                        if (d >= 1f) continue;

                        int ci = cz * f.Size + cx;

                        if (d < 0.86f)
                        {
                            // Parabolic floor, rising toward the rim. Never fills anything in.
                            float dish = floorLevel + depth * d * d * 1.2f;
                            if (f.Height[ci] > dish) f.Height[ci] = dish;
                        }
                        else
                        {
                            // The rim. Lower on the downhill side: that low point is the spill,
                            // and it is where the dam break will happen when the basin finally
                            // overflows — a place the player can learn, not a random edge.
                            float towardExit = (dx * exitX + dz * exitZ) / radius;
                            float exitness = Mathf.Clamp01(Mathf.Max(0f, towardExit) * 1.8f);
                            float lip = floorLevel + depth * Mathf.Lerp(1.05f, 0.55f, exitness);
                            if (f.Height[ci] < lip) f.Height[ci] = lip;
                        }
                    }
                }

                used.Add(new Vector2Int(x, z));
            }

            Debug.Log("[RILL] Carved " + used.Count + " basins into the eroded mountain, from "
                      + onDrainage + " candidate cells on the spring's drainage.");
            f.MarkAllDirty();
        }

        /// <summary>
        /// Cells water leaving <paramref name="start"/> can reach without ever going uphill.
        ///
        /// A worklist rather than a height-sorted sweep, because eight-way descent is not a total
        /// order — two cells at the same elevation can each be reachable through the other's
        /// neighbours. Deliberately strict about climbing: a run has momentum and can top a small
        /// rise, but a basin that can only be filled by spending that momentum is a basin the
        /// player has to fight rule 1 to reach, and rule 1 is the game.
        /// </summary>
        static bool[] DownhillFrom(HeightField f, int start)
        {
            int n = f.Size;
            var reach = new bool[f.Count];
            var work = new System.Collections.Generic.Queue<int>();
            reach[start] = true;
            work.Enqueue(start);

            while (work.Count > 0)
            {
                int c = work.Dequeue();
                int cx = c % n, cz = c / n;
                float h = f.Height[c];
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + (k == 0 || k == 4 || k == 5 ? 1 : k == 1 || k == 6 || k == 7 ? -1 : 0);
                    int nz = cz + (k == 2 || k == 4 || k == 6 ? 1 : k == 3 || k == 5 || k == 7 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int ni = nz * n + nx;
                    if (reach[ni] || f.Height[ni] > h) continue;
                    reach[ni] = true;
                    work.Enqueue(ni);
                }
            }
            return reach;
        }

        /// <summary>Rescales the land so the summit lands exactly on the requested peak height.</summary>
        static void NormaliseTo(HeightField f, float peakHeight)
        {
            float max = float.MinValue;
            for (int i = 0; i < f.Count; i++) if (f.Height[i] > max) max = f.Height[i];
            if (max <= 1f) return;

            float scale = peakHeight / max;
            for (int i = 0; i < f.Count; i++)
            {
                float h = f.Height[i];
                // Only scale what is above sea level; the sea floor keeps its depth.
                f.Height[i] = h > 0f ? h * scale : h;
            }
            f.MarkAllDirty();
        }

        /// <summary>Height range within a radius, in metres. Flat ground scores low.</summary>
        static float Relief(HeightField f, int x, int z, int radius)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int dz = -radius; dz <= radius; dz += 2)
            {
                int cz = z + dz;
                if (cz < 0 || cz >= f.Size) continue;
                for (int dx = -radius; dx <= radius; dx += 2)
                {
                    int cx = x + dx;
                    if (cx < 0 || cx >= f.Size) continue;
                    float h = f.Height[cz * f.Size + cx];
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }
            return max - min;
        }

        /// <summary>0 = a ridge or a plane, 1 = the floor of a channel. Where water collects.</summary>
        static float Concavity(HeightField f, int x, int z)
        {
            const int R = 4;
            float above = 0f;
            int samples = 0;
            float h = f.Height[f.Index(x, z)];
            for (int k = 0; k < 8; k++)
            {
                int dx = (k == 0 || k == 4 || k == 5) ? R : (k == 1 || k == 6 || k == 7) ? -R : 0;
                int dz = (k == 2 || k == 4 || k == 6) ? R : (k == 3 || k == 5 || k == 7) ? -R : 0;
                int nx = x + dx, nz = z + dz;
                if (!f.InBounds(nx, nz)) continue;
                above += Mathf.Max(0f, f.Height[f.Index(nx, nz)] - h);
                samples++;
            }
            if (samples == 0) return 0f;
            return Mathf.Clamp01(above / samples / 3f);
        }

        static Vector2 HighestCell(HeightField f)
        {
            int best = 0;
            float max = float.MinValue;
            for (int i = 0; i < f.Count; i++)
            {
                if (f.Height[i] <= max) continue;
                max = f.Height[i];
                best = i;
            }
            return new Vector2(best % f.Size, best / f.Size);
        }

        /// <summary>
        /// Talus relaxation. Removes vertical walls the droplet solver cannot descend sanely,
        /// and gives the virgin mountain the settled look of rock that has already had weather.
        /// </summary>
        public static void ThermalRelax(HeightField f, int iterations, float talusPerCell)
        {
            int n = f.Size;
            var delta = new float[f.Count];
            for (int it = 0; it < iterations; it++)
            {
                System.Array.Clear(delta, 0, delta.Length);
                for (int z = 1; z < n - 1; z++)
                {
                    for (int x = 1; x < n - 1; x++)
                    {
                        int i = z * n + x;
                        float h = f.Height[i];
                        float total = 0f;
                        float d0 = 0f, d1 = 0f, d2 = 0f, d3 = 0f;

                        d0 = h - f.Height[i - 1]; if (d0 > talusPerCell) total += d0; else d0 = 0f;
                        d1 = h - f.Height[i + 1]; if (d1 > talusPerCell) total += d1; else d1 = 0f;
                        d2 = h - f.Height[i - n]; if (d2 > talusPerCell) total += d2; else d2 = 0f;
                        d3 = h - f.Height[i + n]; if (d3 > talusPerCell) total += d3; else d3 = 0f;

                        if (total <= 0f) continue;
                        float move = total * 0.25f;
                        delta[i] -= move;
                        if (d0 > 0f) delta[i - 1] += move * (d0 / total);
                        if (d1 > 0f) delta[i + 1] += move * (d1 / total);
                        if (d2 > 0f) delta[i - n] += move * (d2 / total);
                        if (d3 > 0f) delta[i + n] += move * (d3 / total);
                    }
                }
                for (int i = 0; i < f.Count; i++) f.Height[i] += delta[i];
            }
            f.MarkAllDirty();
        }

        /// <summary>
        /// A shallow notch just below the summit: enough of a hollow to read as "the water starts
        /// here", open downhill so it never becomes a trap. The difference between a spring and a
        /// puddle is one open side.
        /// </summary>
        static void CarveSpawnNotch(HeightField f, Vector2 summit, float cellSize)
        {
            int cx = Mathf.RoundToInt(summit.x), cz = Mathf.RoundToInt(summit.y);
            if (!f.InBounds(cx, cz)) return;

            // Find the steepest way down from the summit and open the notch along it.
            float bestDrop = 0f;
            int bx = 1, bz = 0;
            float h0 = f.Height[f.Index(cx, cz)];
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx * 3, nz = cz + dz * 3;
                    if (!f.InBounds(nx, nz)) continue;
                    float drop = h0 - f.Height[f.Index(nx, nz)];
                    if (drop > bestDrop) { bestDrop = drop; bx = dx; bz = dz; }
                }
            }

            const int r = 4;
            for (int z = cz - r; z <= cz + r; z++)
            {
                if (z < 0 || z >= f.Size) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= f.Size) continue;
                    float dx = x - summit.x, dz = z - summit.y;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) / r;
                    if (d >= 1f) continue;

                    // Cut nothing on the downhill side: that is the notch's mouth.
                    float alongExit = (dx * bx + dz * bz) / Mathf.Max(r, 1);
                    float openness = Mathf.Clamp01(1f - Mathf.Max(0f, alongExit) * 2.2f);
                    float w = (1f - d * d) * openness;
                    f.Height[z * f.Size + x] -= 2.2f * w * w;
                }
            }
        }

        /// <summary>
        /// D8 flow accumulation: how many cells drain through each cell. This is the mountain's
        /// drainage network, and it is where runs actually go.
        /// </summary>
        static float[] FlowAccumulation(HeightField f)
        {
            int n = f.Size;
            var h = f.Height;
            var acc = new float[f.Count];
            for (int i = 0; i < acc.Length; i++) acc[i] = 1f;

            // Highest first, so a cell's own total is final before it donates downstream.
            var order = new int[f.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => h[b].CompareTo(h[a]));

            for (int k = 0; k < order.Length; k++)
            {
                int c = order[k];
                int cx = c % n, cz = c / n;
                int best = -1;
                float bestDrop = 0f;
                for (int q = 0; q < 8; q++)
                {
                    int nx = cx + (q == 0 || q == 4 || q == 5 ? 1 : q == 1 || q == 6 || q == 7 ? -1 : 0);
                    int nz = cz + (q == 2 || q == 4 || q == 6 ? 1 : q == 3 || q == 5 || q == 7 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int ni = nz * n + nx;
                    float drop = h[c] - h[ni];
                    if (drop > bestDrop) { bestDrop = drop; best = ni; }
                }
                if (best >= 0) acc[best] += acc[c];
            }
            return acc;
        }

        /// <summary>
        /// Where runs actually go: descent paths traced from the summit, the way a real run starts.
        /// Returns visit counts per cell.
        ///
        /// Flow accumulation alone was not enough. It describes drainage over the whole mountain,
        /// but every run begins at one summit spring and converges into a single corridor — only
        /// 2.4% of the field is polished after 150 runs. Sites spread across the drainage network
        /// therefore sat untouched: measured, 45 of 51 had received *no* erosion at all after 150
        /// runs, with an average best cut of 0.15 m against 1.74 m needed.
        /// </summary>
        static int[] SummitCorridor(HeightField f, Vector2Int summit, ref Rng rng)
        {
            int n = f.Size;
            var visits = new int[f.Count];

            for (int walk = 0; walk < 240; walk++)
            {
                int x = Mathf.Clamp(summit.x + rng.Range(-2, 3), 1, n - 2);
                int z = Mathf.Clamp(summit.y + rng.Range(-2, 3), 1, n - 2);

                for (int step = 0; step < 4096; step++)
                {
                    // Mark a small disc, because a channel is wider than one cell.
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int px = x + dx, pz = z + dz;
                            if (px < 0 || pz < 0 || px >= n || pz >= n) continue;
                            visits[pz * n + px]++;
                        }

                    int c = z * n + x;
                    if (f.Height[c] <= f.SeaLevel) break;

                    // Steepest descent, with an occasional lateral wobble so the corridor has the
                    // width that steering gives a real run rather than being one hairline.
                    int best = -1;
                    float bestDrop = 0f;
                    for (int q = 0; q < 8; q++)
                    {
                        int nx = x + (q == 0 || q == 4 || q == 5 ? 1 : q == 1 || q == 6 || q == 7 ? -1 : 0);
                        int nz = z + (q == 2 || q == 4 || q == 6 ? 1 : q == 3 || q == 5 || q == 7 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                        float drop = f.Height[c] - f.Height[nz * n + nx];
                        if (rng.Next01() < 0.15f) drop *= rng.Range(0.5f, 1.5f);
                        if (drop > bestDrop) { bestDrop = drop; best = nz * n + nx; }
                    }
                    if (best < 0) break;      // a pit; a real run would pool here
                    x = best % n; z = best / n;
                }
            }
            return visits;
        }

        static List<SecretSite> PlaceSecrets(HeightField f, StrataBand[] bands, Settings s, Vector2Int summit, ref Rng rng)
        {
            // Placement is locked to the drainage network, not biased toward it. The old rule
            // accepted any concave cell, and accepted non-route cells 20% of the time anyway, so
            // most sites sat where no run would ever pass: 0 of 60 were found in 24 runs, and the
            // revelation track — one of the design's four — was invisible over any real session.
            var acc = FlowAccumulation(f);
            var sorted = new float[acc.Length];
            System.Array.Copy(acc, sorted, acc.Length);
            System.Array.Sort(sorted);
            // Top 2% of cells by drainage area. On a 256² field that is ~1,300 cells of channel.
            float channelThreshold = sorted[(int)(sorted.Length * 0.98f)];

            var corridor = SummitCorridor(f, summit, ref rng);

            // Half the sites go on the corridor runs actually take, so the revelation track is
            // felt inside a session; the rest go on the wider drainage network, so there is
            // something left for a player who deliberately routes water somewhere new. All of them
            // on the corridor would be found at once and the track would be over in a week.
            int corridorQuota = s.SecretCount / 2;

            // Sample from the eligible cells directly rather than throwing darts at the whole grid
            // and hoping. The corridor is a couple of cells wide over a few hundred cells of path,
            // so rejection sampling exhausted its guard and quietly placed 20 sites where 60 were
            // asked for — the kind of silent shortfall that looks exactly like a working system.
            var corridorCells = new List<int>(4096);
            var networkCells = new List<int>(4096);
            for (int c = 0; c < f.Count; c++)
            {
                int cx = c % f.Size, cz = c / f.Size;
                if (cx < 8 || cz < 8 || cx >= f.Size - 8 || cz >= f.Size - 8) continue;
                if (f.Height[c] < 6f) continue;          // not under the sea
                if (corridor[c] >= 4) corridorCells.Add(c);
                else if (acc[c] >= channelThreshold) networkCells.Add(c);
            }

            var list = new List<SecretSite>(s.SecretCount);
            int guard = 0;
            while (list.Count < s.SecretCount && guard++ < s.SecretCount * 400)
            {
                bool wantCorridor = list.Count < corridorQuota && corridorCells.Count > 0;
                var pool = wantCorridor ? corridorCells : networkCells;
                if (pool.Count == 0) break;

                int i = pool[rng.Range(0, pool.Count)];
                int x = i % f.Size, z = i / f.Size;
                float h = f.Height[i];

                SecretKind kind;
                float roll = rng.Next01();
                if (roll < 0.44f) kind = SecretKind.Fossil;
                else if (roll < 0.66f) kind = SecretKind.Geode;
                else if (roll < 0.82f) kind = SecretKind.Ruin;
                else if (roll < 0.93f) kind = SecretKind.Spring;
                else kind = SecretKind.CaveMouth;

                // Burial depth is the price in play, and it has to be payable. A run carves
                // ~0.3-0.5 m at a cell it crosses, so a 0.8-6.0 m spread priced even the commonest
                // find at several perfect repeats of the same line. Common kinds are now findable
                // in a session; the plumbing-changing ones stay month-scale on purpose.
                float depth;
                switch (kind)
                {
                    case SecretKind.Fossil:
                    case SecretKind.Geode: depth = rng.Range(0.3f, 1.2f); break;
                    case SecretKind.Ruin: depth = rng.Range(1.2f, 3.0f); break;
                    default: depth = rng.Range(3.5f, 6.5f); break;
                }

                // 4 cells apart, not 6: along a corridor two cells wide, 6-cell spacing alone
                // capped how many sites could physically fit.
                bool tooClose = false;
                for (int k = 0; k < list.Count; k++)
                {
                    int c = list[k].Cell;
                    int ox = c % f.Size, oz = c / f.Size;
                    if ((ox - x) * (ox - x) + (oz - z) * (oz - z) < 16) { tooClose = true; break; }
                }
                if (tooClose) continue;

                list.Add(new SecretSite { Cell = i, RevealElevation = h - depth, Kind = kind });
            }
            return list;
        }
    }
}
