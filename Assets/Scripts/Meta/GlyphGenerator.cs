using System.Collections.Generic;
using System.Text;
using UnityEngine;

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

        const string Empty = "⬛";
        const string Trace = "🟦";
        const string Deep = "🟪";
        const string Sea = "🟩";
        const string Pool = "⬜";

        /// <summary>
        /// Rasterises the paths of a day's runs onto one grid. Cells the water crossed often
        /// read as "deep", the arrival cell reads as sea, a pooled ending reads as a pale square.
        /// </summary>
        public static string Render(List<List<Vector3>> runPaths, List<bool> reachedSea, float worldExtent)
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
                    else if (counts[i] == 0) sb.Append(Empty);
                    else if (counts[i] > max * 0.5f) sb.Append(Deep);
                    else sb.Append(Trace);
                }
                if (z > 0) sb.Append('\n');
            }
            return sb.ToString();
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
