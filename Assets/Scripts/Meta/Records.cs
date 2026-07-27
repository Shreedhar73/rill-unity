using System;
using UnityEngine;
using Rill.App;
using Rill.World;

namespace Rill.Meta
{
    /// <summary>
    /// What this mountain has had done to it — a record, not a score, and the difference is the
    /// whole design. Every figure here reads off the world or the almanac and can be recomputed
    /// from them; nothing is awarded, nothing ranks, nothing goes up for playing rather than for
    /// doing. If a number cannot be traced to the heightfield or the almanac it does not belong
    /// on this screen, which is why this class takes the world and nothing else.
    ///
    /// Plain C# so the headless test can hold every line against the arrays it claims to read.
    /// </summary>
    public static class Records
    {
        public static string Text(RillWorld world, Almanac almanac, float[] life, int secretsRevealed)
        {
            var sb = new System.Text.StringBuilder();
            var field = world.Field;

            // The header: which rock, how long, since when.
            sb.AppendFormat("{0} · seed {1}\n", world.Biome, world.Seed);
            if (world.FirstPlayedUtcTicks > 0)
                sb.AppendFormat("First rain {0:d MMM yyyy}\n",
                    new DateTime(world.FirstPlayedUtcTicks, DateTimeKind.Utc).ToLocalTime());
            sb.AppendFormat("{0:n0} runs · {1} of play\n", world.RunNumber, PlaySpan(world.LifetimePlaySeconds));
            sb.Append('\n');

            // The ledger: what moved, where it went.
            sb.AppendFormat("{0:n0} m³ of rock moved\n", world.LifetimeSediment);
            sb.AppendFormat("{0:n0} m³ of water delivered to the sea\n", world.LifetimeWaterToSea);

            // The relief: how far this mountain now is from the one that was generated. Both
            // numbers recomputed from Height vs Virgin right here, every time.
            float deepestCut = 0f, tallestBuild = 0f;
            for (int i = 0; i < field.Count; i++)
            {
                float d = field.Virgin[i] - field.Height[i];
                if (d > deepestCut) deepestCut = d;
                if (-d > tallestBuild) tallestBuild = -d;
            }
            sb.AppendFormat("Deepest cut {0:0.0} m below the virgin rock · tallest build +{1:0.0} m\n", deepestCut, tallestBuild);
            sb.Append('\n');

            // The lattice, by name, with what it holds.
            var basins = world.Basins.Basins;
            if (basins.Count > 0)
            {
                sb.AppendFormat("Water held {0:n0} m³ across {1} basin{2}\n",
                    world.Basins.TotalWater(), basins.Count, basins.Count == 1 ? "" : "s");
                for (int i = 0; i < basins.Count; i++)
                {
                    var b = basins[i];
                    if (b.Capacity < 5f) continue;
                    sb.AppendFormat("  {0}   {1:0}%\n",
                        string.IsNullOrEmpty(b.Name) ? "A tarn" : b.Name, b.FillFraction * 100f);
                }
            }

            var marks = Landmarks.Find(world);
            if (marks.Count > 0)
            {
                sb.Append('\n');
                sb.Append(Landmarks.PanelBlock(marks, field.CellSize * field.CellSize));
            }

            sb.Append('\n');
            sb.AppendFormat("Uncovered {0} of {1} secrets\n", secretsRevealed, world.Secrets.Count);

            if (life != null)
            {
                int living = 0;
                float highest = 0f;
                for (int i = 0; i < life.Length; i++)
                {
                    if (life[i] > 0.05f) living++;
                    if (life[i] > highest) highest = life[i];
                }
                if (living > 0)
                    sb.AppendFormat("Life: {0} · {1:n0} living cells\n",
                        EcosystemSystem.Describe((LifeTier)Mathf.Clamp(Mathf.FloorToInt(highest), 0, 6)), living);
            }

            if (almanac != null && almanac.DayStreak > 1)
                sb.AppendFormat("Played {0} days running\n", almanac.DayStreak);

            return sb.ToString();
        }

        static string PlaySpan(float seconds)
        {
            if (seconds >= 5400f) return string.Format("{0:0.#} hours", seconds / 3600f);
            if (seconds >= 90f) return string.Format("{0:0} minutes", seconds / 60f);
            return string.Format("{0:0} seconds", seconds);
        }
    }
}
