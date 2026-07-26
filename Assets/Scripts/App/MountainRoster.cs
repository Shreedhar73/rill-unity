using Rill.Core;
using Rill.Meta;

namespace Rill.App
{
    /// <summary>
    /// The three mountains a player owns, and the only sanctioned way to create or destroy one.
    ///
    /// This class exists because a slot picker is a new-game button standing next to three save
    /// files, and invariant 1 is absolute: "Nothing clears HeightField.Height. No reset, no level
    /// load, no new game that touches an existing slot." A six-month-old mountain is six months of
    /// switching cost made of stone, and the whole design rests on the player believing it is safe.
    ///
    /// So the rules are enforced here rather than trusted to the UI:
    ///   - Create refuses outright on an occupied slot. There is no overwrite path and no force
    ///     flag, because a force flag is a thing a future caller passes true to.
    ///   - Delete requires the seed of the mountain being deleted. A caller can only know it by
    ///     having read that slot's summary, so it is impossible to destroy a mountain you have not
    ///     looked at, and impossible to destroy the wrong one by passing a stale index.
    ///
    /// Plain C# and no Unity types, so the smoke test can drive all of it.
    /// </summary>
    public sealed class MountainRoster
    {
        /// <summary>Three, as designed. Slots are addressed by index and never renumbered.</summary>
        public const int Slots = 3;

        readonly SaveSystem.MountainSummary[] _summaries = new SaveSystem.MountainSummary[Slots];

        public MountainRoster() { Refresh(); }

        /// <summary>Re-reads every slot header from disk. Cheap; call it whenever the picker opens.</summary>
        public void Refresh()
        {
            for (int i = 0; i < Slots; i++)
            {
                SaveSystem.MountainSummary s;
                if (!SaveSystem.ReadSummary(i, out s)) s = new SaveSystem.MountainSummary { Slot = i };
                _summaries[i] = s;
            }
        }

        public SaveSystem.MountainSummary this[int slot] => _summaries[Clamp(slot)];
        public bool Occupied(int slot) => _summaries[Clamp(slot)].Occupied;

        public int OccupiedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Slots; i++) if (_summaries[i].Occupied) n++;
                return n;
            }
        }

        /// <summary>First slot with no mountain in it, or -1 when all three are taken.</summary>
        public int FirstEmpty()
        {
            for (int i = 0; i < Slots; i++) if (!_summaries[i].Occupied) return i;
            return -1;
        }

        /// <summary>
        /// One line for the picker, read off the world and never awarded. A slot that has been
        /// played says what has been done to it; an empty one says what it is.
        /// </summary>
        public string Describe(int slot)
        {
            var s = _summaries[Clamp(slot)];
            if (!s.Occupied) return "Empty · start a mountain here";
            if (s.RunNumber <= 0) return s.Biome + " · untouched";
            return string.Format("{0} · {1:n0} runs · {2:n0} m³ moved · {3:n0} m³ to the sea",
                                 s.Biome, s.RunNumber, s.LifetimeSediment, s.LifetimeWaterToSea);
        }

        /// <summary>
        /// Makes a new mountain in an empty slot and writes it to disk immediately, so that a slot
        /// shown as occupied is occupied even if the app dies in the next second.
        ///
        /// Returns null if the slot already holds a mountain. There is deliberately no overwrite.
        /// </summary>
        public RillWorld Create(int slot, Biome biome, uint seed, GameConfig config)
        {
            slot = Clamp(slot);
            if (_summaries[slot].Occupied) return null;

            var world = RillWorld.Create(config, seed, biome);
            SaveSystem.Save(world, new float[world.Field.Count], slot);
            Refresh();
            return world;
        }

        /// <summary>
        /// Destroys a mountain. <paramref name="confirmSeed"/> must match the seed of the mountain
        /// actually in that slot, which a caller can only obtain by reading its summary first.
        ///
        /// This is the single most dangerous method in the project. It is not reachable by passing
        /// an index alone on purpose.
        /// </summary>
        public bool Delete(int slot, uint confirmSeed)
        {
            slot = Clamp(slot);
            var s = _summaries[slot];
            if (!s.Occupied) return false;
            if (s.Seed != confirmSeed) return false;

            SaveSystem.DeleteSlot(slot);
            Refresh();
            return true;
        }

        static int Clamp(int slot) => slot < 0 ? 0 : (slot >= Slots ? Slots - 1 : slot);
    }
}
