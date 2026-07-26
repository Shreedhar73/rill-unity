using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Rill.Core;

namespace Rill.Meta
{
    /// <summary>
    /// The Wordle grid of RILL. A run's line down the mountain is compressed into a tiny square
    /// glyph: instantly recognisable, spoiler-free, and copy-pasteable into a group chat. It is
    /// the viral spine, and it costs nothing to run.
    /// </summary>
    public static class GlyphGenerator
    {
        public const int Grid = 7;

        // The background is the day's mountain, not blank space.
        //
        // A Wordle grid reads at a glance because every cell carries information. This one did not.
        // Measured: a day's seven runs all leave the same summit and converge on the same corridor,
        // so they touch 8 of the 49 cells and the other 41 were drawn as "⬛ nothing happened here".
        // The share unit was a scatter of marks on a void.
        //
        // Land and sea cost nothing to draw, are identical for everyone playing the same Daily seed
        // — so glyphs stay comparable, which is the entire point of a shared grid — and turn the
        // picture into a river crossing a coastline instead of a few dots in the dark.
        const string Land = "🟫";
        const string Ocean = "🟦";
        const string Empty = "⬛";   // only when no terrain was supplied

        // The water is the brightest thing in the frame, the same as it is in the game.
        const string Trace = "⬜";
        const string Deep = "🟪";
        const string Sea = "🟩";
        const string Pool = "🟧";

        /// <summary>
        /// Rasterises the paths of a day's runs onto one grid. Cells the water crossed often read
        /// as "deep", a run that made the sea marks its arrival green, one that stopped marks it
        /// amber. Pass <paramref name="field"/> to draw the mountain underneath; without it the
        /// background is blank, which is what the glyph used to be everywhere.
        /// </summary>
        public static string Render(List<List<Vector3>> runPaths, List<bool> reachedSea, float worldExtent,
                                    HeightField field = null)
        {
            var counts = new int[Grid * Grid];
            var flags = new byte[Grid * Grid]; // 1 = sea arrival, 2 = pooled end

            for (int r = 0; r < runPaths.Count; r++)
            {
                var path = runPaths[r];
                if (path == null || path.Count == 0) continue;
                for (int i = 0; i < path.Count; i++)
                {
                    int c = CellOf(path[i], worldExtent);
                    if (c >= 0) counts[c]++;
                }
                int last = CellOf(path[path.Count - 1], worldExtent);
                if (last >= 0)
                {
                    bool sea = r < reachedSea.Count && reachedSea[r];
                    flags[last] = (byte)(sea ? 1 : 2);
                }
            }

            string[] background = Background(field);

            int max = 1;
            for (int i = 0; i < counts.Length; i++) if (counts[i] > max) max = counts[i];

            var sb = new StringBuilder(Grid * Grid * 2 + Grid);
            for (int z = Grid - 1; z >= 0; z--) // north at the top
            {
                for (int x = 0; x < Grid; x++)
                {
                    int i = z * Grid + x;
                    if (flags[i] == 1) sb.Append(Sea);
                    else if (flags[i] == 2) sb.Append(Pool);
                    else if (counts[i] == 0) sb.Append(background[i]);
                    else if (counts[i] > max * 0.5f) sb.Append(Deep);
                    else sb.Append(Trace);
                }
                if (z > 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Land or ocean per glyph cell, by majority of the terrain samples inside it. A majority
        /// rather than a mean: a cell that is half a 100 m peak and half open sea has an average
        /// elevation that describes neither, and the glyph is asking a yes/no question.
        /// </summary>
        static string[] Background(HeightField f)
        {
            var bg = new string[Grid * Grid];
            if (f == null)
            {
                for (int i = 0; i < bg.Length; i++) bg[i] = Empty;
                return bg;
            }

            int step = Mathf.Max(1, f.Size / (Grid * 8));   // ~8 samples per glyph cell per axis
            var land = new int[Grid * Grid];
            var total = new int[Grid * Grid];

            for (int z = 0; z < f.Size; z += step)
            {
                int gz = z * Grid / f.Size;
                for (int x = 0; x < f.Size; x += step)
                {
                    int gx = x * Grid / f.Size;
                    int gi = gz * Grid + gx;
                    total[gi]++;
                    if (f.Height[z * f.Size + x] > f.SeaLevel) land[gi]++;
                }
            }

            for (int i = 0; i < bg.Length; i++)
                bg[i] = total[i] > 0 && land[i] * 2 >= total[i] ? Land : Ocean;
            return bg;
        }

        static int CellOf(Vector3 world, float worldExtent)
        {
            float half = worldExtent * 0.5f;
            int x = Mathf.FloorToInt((world.x + half) / worldExtent * Grid);
            int z = Mathf.FloorToInt((world.z + half) / worldExtent * Grid);
            if (x < 0 || z < 0 || x >= Grid || z >= Grid) return -1;
            return z * Grid + x;
        }

        /// <summary>The full shareable block: one header line, the glyph, one link line.</summary>
        public static string ShareText(string dateLabel, int runsUsed, int runsTotal, float litresToSea, string glyph)
        {
            var sb = new StringBuilder();
            sb.Append("RILL ").Append(dateLabel).Append("  ")
              .Append(runsUsed).Append('/').Append(runsTotal).Append("  ")
              .Append(Mathf.RoundToInt(litresToSea)).Append(" m³ to sea\n");
            sb.Append(glyph);
            sb.Append("\n#RILL");
            return sb.ToString();
        }
    }
}
