using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.World
{
    /// <summary>
    /// Names for the places the player has made. Six months of carving used to produce "the deep
    /// bit near the left"; a gorge past a real depth or a fan past a real size gets a name, and a
    /// named place is how a space becomes somewhere. Emotional attachment needs handles.
    ///
    /// Recomputed from the terrain every time rather than stored: the save format stays untouched,
    /// and a name can never survive the destruction of the thing it named — if the gorge silts
    /// back up, the name goes with it, which is this game's honesty applied to sentiment. Names
    /// are deterministic from the seed and the feature's deepest point, so the same mountain
    /// always calls its places the same thing.
    /// </summary>
    public static class Landmarks
    {
        public enum Kind { Gorge, Fan }

        public struct Landmark
        {
            public string Name;
            public Kind Kind;
            public int Cell;        // the feature's defining cell: deepest cut or tallest deposit
            public float Measure;   // metres of relief for a gorge, of build for a fan
            public int Cells;       // footprint
        }

        // First names for the land. Short, place-ish, none of them twee.
        static readonly string[] GorgeNames =
        {
            "Raven", "Thorn", "Slate", "Ember", "Cold", "Hollow", "Iron", "Bright",
            "Fern", "Ash", "Heron", "Otter", "Flint", "Bracken", "Shale", "Tarn"
        };
        static readonly string[] FanNames =
        {
            "Gull", "Sand", "Reed", "Pebble", "Willow", "Grey", "Salt", "Moss",
            "Drift", "Shell", "Marl", "Rush", "Plover", "Dune", "Loam", "Wren"
        };

        // Thresholds are the difference between a landmark and a scratch. A gorge must be cut
        // deeper than any single run manages (deepest single-run cut measured ~1.5 m), so a name
        // marks sustained work; the footprint floor keeps a single deep pothole nameless.
        const float GorgeRelief = 2.5f;
        const float FanBuild = 1.5f;
        const int MinCells = 10;

        /// <summary>Every named place on the mountain, recomputed from what the terrain is now.</summary>
        public static List<Landmark> Find(RillWorld world)
        {
            var found = new List<Landmark>();
            var field = world.Field;

            // Two passes over the same clustering: cut below virgin, build above it.
            FindKind(world, Kind.Gorge, i => field.Virgin[i] - field.Height[i], GorgeRelief, found);
            FindKind(world, Kind.Fan, i => field.Height[i] - field.Virgin[i], FanBuild, found);

            // Sixteen first names and a mature mountain's worth of gorges collide — the 1,000-run
            // survey listed two Shale Gorges. Same rule as basins (L-070): every named place on
            // one mountain is nameable apart. Deterministic because the walk order and the probe
            // sequence both are.
            var used = new HashSet<string>();
            for (int i = 0; i < found.Count; i++)
            {
                var m = found[i];
                if (used.Add(m.Name)) continue;
                for (uint step = 1; step < 64; step++)
                {
                    string retry = NameFor(world.Seed, m.Kind, m.Cell + (int)(step * 7919));
                    if (used.Add(retry)) { m.Name = retry; found[i] = m; break; }
                }
            }
            return found;
        }

        static void FindKind(RillWorld world, Kind kind, System.Func<int, float> relief, float threshold,
                             List<Landmark> found)
        {
            var field = world.Field;
            int size = field.Size;
            var visited = new bool[field.Count];
            var stack = new Stack<int>();

            for (int start = 0; start < field.Count; start++)
            {
                if (visited[start] || relief(start) < threshold) continue;
                // Underwater features are the sea floor's business, not the mountain's.
                if (field.Height[start] <= field.SeaLevel) { visited[start] = true; continue; }

                int cells = 0, keyCell = start;
                float deepest = 0f;
                stack.Push(start);
                visited[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    cells++;
                    float r = relief(i);
                    if (r > deepest) { deepest = r; keyCell = i; }
                    int x = i % size, z = i / size;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int nz = z + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (!field.InBounds(nx, nz)) continue;
                        int k = field.Index(nx, nz);
                        if (visited[k] || relief(k) < threshold) continue;
                        visited[k] = true;
                        stack.Push(k);
                    }
                }

                if (cells < MinCells) continue;
                found.Add(new Landmark
                {
                    Kind = kind,
                    Cell = keyCell,
                    Measure = deepest,
                    Cells = cells,
                    Name = NameFor(world.Seed, kind, keyCell)
                });
            }
        }

        /// <summary>
        /// Deterministic from seed and the feature's deepest cell. The deepest point of a gorge
        /// wanders in its first runs and settles as the trench establishes, so a young feature can
        /// be renamed once or twice before its name sticks — the almanac keeps every christening,
        /// which reads as the place earning its final name rather than as a bug.
        /// </summary>
        static string NameFor(uint seed, Kind kind, int keyCell)
        {
            uint h = Noise.Hash(seed ^ (uint)(keyCell * 2654435761u) ^ (uint)kind * 97u);
            string[] pool = kind == Kind.Gorge ? GorgeNames : FanNames;
            string first = pool[h % (uint)pool.Length];
            return kind == Kind.Gorge ? first + " Gorge" : first + " Fan";
        }

        /// <summary>The named places as panel lines, deepest first.</summary>
        public static string PanelBlock(List<Landmark> marks, float cellArea)
        {
            if (marks.Count == 0) return "";
            marks.Sort((a, b) => b.Measure.CompareTo(a.Measure));
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Named places:");
            for (int i = 0; i < marks.Count && i < 8; i++)
            {
                var m = marks[i];
                sb.AppendFormat("  {0} — {1} {2:0.0} m over {3:n0} m²\n", m.Name,
                    m.Kind == Kind.Gorge ? "cut" : "built", m.Measure, m.Cells * cellArea);
            }
            return sb.ToString();
        }
    }
}
