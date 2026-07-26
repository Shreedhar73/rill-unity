using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;
using Rill.World;

namespace Rill.Meta
{
    public struct RillProject
    {
        public string Id;
        public string Text;
        public float Progress01;
        public bool Done;
    }

    /// <summary>
    /// Projects are self-directed engineering goals the game *surfaces* and never assigns. They
    /// carry no reward, no timer and no failure — they exist because a player standing in front of
    /// a basin at 84% will finish it, and all the game has to do is mention that it is at 84%.
    ///
    /// Every project is read off the world. There is no project database to author or balance.
    /// </summary>
    public sealed class ProjectSystem
    {
        readonly HashSet<string> _announced = new HashSet<string>();
        readonly List<RillProject> _current = new List<RillProject>(4);

        public IReadOnlyList<RillProject> Current => _current;

        /// <summary>Recomputes the shortlist. Called after runs, never per frame.</summary>
        public void Refresh(RillWorld world, EcosystemSystem ecosystem, RevelationSystem revelation, Almanac almanac)
        {
            _current.Clear();
            if (world == null) return;

            // 1. The nearest basin to full. The Zeigarnik hook, stated plainly.
            Basin best = null;
            var basins = world.Basins.Basins;
            for (int i = 0; i < basins.Count; i++)
            {
                var b = basins[i];
                if (b.Capacity < 50f) continue;
                if (b.FillFraction >= 0.999f) continue;
                if (best == null || b.FillFraction > best.FillFraction) best = b;
            }
            if (best != null && best.FillFraction > 0.05f)
            {
                Add(new RillProject
                {
                    Id = "fill:" + best.Name,
                    Text = string.Format("Fill the {0} — {1:0}%", best.Name.ToLower(), best.FillFraction * 100f),
                    Progress01 = best.FillFraction
                });
            }

            // 2. The secret closest to daylight, named only by direction. Never by what it is.
            SecretSite nearest = null;
            float nearestRemaining = float.MaxValue;
            for (int i = 0; i < world.Secrets.Count; i++)
            {
                var s = world.Secrets[i];
                if (s.Revealed) continue;
                float remaining = world.Field.Height[s.Cell] - s.RevealElevation;
                if (remaining <= 0f || remaining > 4f) continue;
                if (remaining < nearestRemaining) { nearestRemaining = remaining; nearest = s; }
            }
            if (nearest != null)
            {
                Add(new RillProject
                {
                    Id = "dig:" + nearest.Cell,
                    Text = string.Format("Something is {0:0.0} m under the {1} slope", nearestRemaining, Compass(world, nearest.Cell)),
                    Progress01 = Mathf.Clamp01(1f - nearestRemaining / 4f)
                });
            }

            // 3. The sea. The only goal the game ever states outright, and only when it is close.
            if (world.LifetimeWaterToSea < 1f)
            {
                Add(new RillProject { Id = "sea:first", Text = "Get water all the way to the sea", Progress01 = 0f });
            }

            // 4. The ecosystem's next rung, once the mountain holds enough water to support it.
            if (ecosystem != null)
            {
                var tier = ecosystem.HighestTier;
                if (tier < LifeTier.Village && ecosystem.LivingCells > 40)
                {
                    var next = tier + 1;
                    Add(new RillProject
                    {
                        Id = "life:" + next,
                        Text = "Keep the valley wet long enough for " + NextLifeWord(next),
                        Progress01 = Mathf.Clamp01(ecosystem.LivingCells / 900f)
                    });
                }
            }

            // Announce anything that has just come true, once, quietly.
            AnnounceCompletions(world, ecosystem, almanac);
        }

        void Add(RillProject p)
        {
            if (_current.Count >= 3) return;
            _current.Add(p);
        }

        void AnnounceCompletions(RillWorld world, EcosystemSystem ecosystem, Almanac almanac)
        {
            var basins = world.Basins.Basins;
            for (int i = 0; i < basins.Count; i++)
            {
                var b = basins[i];
                if (b.FillFraction < 0.999f || b.Capacity < 50f) continue;
                string id = "done:fill:" + b.Name;
                if (_announced.Contains(id)) continue;
                _announced.Add(id);
                if (almanac != null) almanac.Note(world.RunNumber, "milestone", b.Name + " filled to the brim");
            }

            if (world.LifetimeWaterToSea > 0f && _announced.Add("done:sea"))
            {
                if (almanac != null) almanac.Note(world.RunNumber, "milestone", "Water reached the sea");
            }
        }

        static string NextLifeWord(LifeTier t)
        {
            switch (t)
            {
                case LifeTier.Reeds: return "reeds";
                case LifeTier.Fish: return "fish";
                case LifeTier.Birds: return "birds";
                case LifeTier.Deer: return "deer";
                case LifeTier.Village: return "someone to settle here";
                default: return "moss";
            }
        }

        static readonly string[] CompassNames = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        static string Compass(RillWorld world, int cell)
        {
            int n = world.Field.Size;
            int x = cell % n, z = cell / n;
            float dx = x - n * 0.5f, dz = z - n * 0.5f;
            float ang = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            int oct = Mathf.RoundToInt(((ang + 360f) % 360f) / 45f) % 8;
            return CompassNames[oct];
        }

        /// <summary>One line for the idle HUD: the project the player is closest to finishing.</summary>
        public string HeadlineLine()
        {
            if (_current.Count == 0) return "";
            var best = _current[0];
            for (int i = 1; i < _current.Count; i++)
                if (_current[i].Progress01 > best.Progress01) best = _current[i];
            return best.Text;
        }

        public string PanelBlock()
        {
            if (_current.Count == 0) return "Nothing is close to finishing. Go and make something close.";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _current.Count; i++)
                sb.AppendFormat("• {0}\n", _current[i].Text);
            return sb.ToString();
        }
    }
}
