using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.Meta
{
    /// <summary>
    /// The world IS the save file, so this is the most important non-gameplay class in RILL.
    /// Format is a gzipped binary blob of the terrain arrays plus the secrets and the lifetime
    /// record. A mature mountain is a couple of megabytes — cheap enough to keep forever, which
    /// is the whole point: a six-month-old mountain is six months of switching cost made of stone.
    /// </summary>
    public static class SaveSystem
    {
        const uint Magic = 0x4C4C4952; // "RILL"
        const int Version = 4;         // 4 added permanent dye and ice

        public static string RootDir
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "rill");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string WorldPath(int slot) => Path.Combine(RootDir, "world_" + slot + ".rill");
        public static bool Exists(int slot) => File.Exists(WorldPath(slot));

        /// <summary>
        /// Everything a slot picker needs about a mountain, without paying for the mountain.
        /// A mature world is a couple of megabytes of float arrays; drawing a three-slot menu must
        /// not deserialise three of them.
        /// </summary>
        public struct MountainSummary
        {
            public int Slot;
            public bool Occupied;
            public uint Seed;
            public Biome Biome;
            public int RunNumber;
            public float LifetimeSediment;
            public float LifetimeWaterToSea;
            public float LifetimePlaySeconds;
            public long FirstPlayedUtcTicks;
        }

        /// <summary>
        /// Reads just the header. It sits ahead of the terrain arrays in the format, so the gzip
        /// stream is only pulled far enough to get it — about sixty bytes rather than several
        /// megabytes.
        /// </summary>
        public static bool ReadSummary(int slot, out MountainSummary summary)
        {
            summary = new MountainSummary { Slot = slot };
            string path = WorldPath(slot);
            if (!File.Exists(path)) return false;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var r = new BinaryReader(gz))
                {
                    if (r.ReadUInt32() != Magic) return false;
                    int version = r.ReadInt32();
                    if (version < 3) return false;

                    summary.Seed = r.ReadUInt32();
                    summary.Biome = (Biome)r.ReadInt32();
                    r.ReadInt32();      // size
                    r.ReadSingle();     // cell size
                    r.ReadInt32();      // summit x
                    r.ReadInt32();      // summit z
                    summary.RunNumber = r.ReadInt32();
                    summary.LifetimeSediment = r.ReadSingle();
                    summary.LifetimeWaterToSea = r.ReadSingle();
                    summary.LifetimePlaySeconds = r.ReadSingle();
                    summary.FirstPlayedUtcTicks = r.ReadInt64();
                    summary.Occupied = true;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RILL] could not read slot " + slot + " header: " + e.Message);
                return false;
            }
        }

        // No default on `slot`. It used to be 0, and a defaulted slot silently means "the first
        // mountain" — RunController had two save calls that had quietly kept it, so ending a
        // session on mountain 3 would have written mountain 3 over mountain 1. Making the argument
        // mandatory turns that entire class of bug into a compile error instead of a lost world.
        public static void Save(RillWorld world, float[] lifeField, int slot)
        {
            string path = WorldPath(slot);
            string tmp = path + ".tmp";

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            // Fully qualified: UnityEngine declares a CompressionLevel of its own.
            using (var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal))
            using (var w = new BinaryWriter(gz))
            {
                var f = world.Field;
                w.Write(Magic);
                w.Write(Version);
                w.Write(world.Seed);
                w.Write((int)world.Biome);
                w.Write(f.Size);
                w.Write(f.CellSize);
                w.Write(world.SummitCell.x);
                w.Write(world.SummitCell.y);
                w.Write(world.RunNumber);
                w.Write(world.LifetimeSediment);
                w.Write(world.LifetimeWaterToSea);
                w.Write(world.LifetimePlaySeconds);
                w.Write(world.FirstPlayedUtcTicks);

                WriteFloats(w, f.Height);
                WriteFloats(w, f.Polish);
                WriteFloats(w, f.Water);
                WriteFloats(w, f.Wet);
                WriteFloats(w, f.Hardness);
                WriteFloats(w, f.Virgin);
                WriteFloats(w, lifeField ?? new float[f.Count]);
                WriteColors(w, f.Dye);
                WriteFloats(w, f.Ice);

                w.Write(world.Secrets.Count);
                for (int i = 0; i < world.Secrets.Count; i++)
                {
                    var s = world.Secrets[i];
                    w.Write(s.Cell);
                    w.Write(s.RevealElevation);
                    w.Write((int)s.Kind);
                    w.Write(s.Revealed);
                    w.Write(s.RevealedOnRun);
                }
            }

            // Atomic-ish replace so a kill mid-save never eats a player's mountain.
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static RillWorld Load(GameConfig config, out float[] lifeField, int slot)
        {
            lifeField = null;
            string path = WorldPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var r = new BinaryReader(gz))
                {
                    if (r.ReadUInt32() != Magic) return null;
                    int version = r.ReadInt32();
                    if (version < 3) return null;

                    uint seed = r.ReadUInt32();
                    var biome = (Biome)r.ReadInt32();
                    int size = r.ReadInt32();
                    float cellSize = r.ReadSingle();
                    var summit = new Vector2Int(r.ReadInt32(), r.ReadInt32());

                    var field = new HeightField(size, cellSize);

                    int runNumber = r.ReadInt32();
                    float sediment = r.ReadSingle();
                    float toSea = r.ReadSingle();
                    float playSeconds = r.ReadSingle();
                    long firstPlayed = r.ReadInt64();

                    ReadFloats(r, field.Height);
                    ReadFloats(r, field.Polish);
                    ReadFloats(r, field.Water);
                    ReadFloats(r, field.Wet);
                    ReadFloats(r, field.Hardness);
                    ReadFloats(r, field.Virgin);
                    lifeField = new float[field.Count];
                    ReadFloats(r, lifeField);
                    if (version >= 4)
                    {
                        ReadColors(r, field.Dye);
                        ReadFloats(r, field.Ice);
                    }

                    int secretCount = r.ReadInt32();
                    var secrets = new List<SecretSite>(secretCount);
                    for (int i = 0; i < secretCount; i++)
                    {
                        secrets.Add(new SecretSite
                        {
                            Cell = r.ReadInt32(),
                            RevealElevation = r.ReadSingle(),
                            Kind = (SecretKind)r.ReadInt32(),
                            Revealed = r.ReadBoolean(),
                            RevealedOnRun = r.ReadInt32()
                        });
                    }

                    config.Size = size;
                    config.CellSize = cellSize;
                    config.Biome = biome;
                    config.Seed = seed;

                    var restored = RillWorld.FromRestored(config, seed, biome, field, summit, secrets);
                    restored.RunNumber = runNumber;
                    restored.LifetimeSediment = sediment;
                    restored.LifetimeWaterToSea = toSea;
                    restored.LifetimePlaySeconds = playSeconds;
                    restored.FirstPlayedUtcTicks = firstPlayed;
                    return restored;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RILL] Save load failed, starting a fresh mountain: " + e.Message);
                return null;
            }
        }

        public static void DeleteSlot(int slot)
        {
            string p = WorldPath(slot);
            if (File.Exists(p)) File.Delete(p);
        }

        static void WriteFloats(BinaryWriter w, float[] a)
        {
            w.Write(a.Length);
            var bytes = new byte[a.Length * 4];
            Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length);
            w.Write(bytes);
        }

        static void WriteColors(BinaryWriter w, Color32[] a)
        {
            w.Write(a.Length);
            var bytes = new byte[a.Length * 4];
            for (int i = 0; i < a.Length; i++)
            {
                int o = i * 4;
                bytes[o] = a[i].r; bytes[o + 1] = a[i].g; bytes[o + 2] = a[i].b; bytes[o + 3] = a[i].a;
            }
            w.Write(bytes);
        }

        static void ReadColors(BinaryReader r, Color32[] into)
        {
            int len = r.ReadInt32();
            var bytes = r.ReadBytes(len * 4);
            int count = Mathf.Min(len, into.Length);
            for (int i = 0; i < count; i++)
            {
                int o = i * 4;
                into[i] = new Color32(bytes[o], bytes[o + 1], bytes[o + 2], bytes[o + 3]);
            }
        }

        static void ReadFloats(BinaryReader r, float[] into)
        {
            int len = r.ReadInt32();
            var bytes = r.ReadBytes(len * 4);
            int copy = Mathf.Min(len, into.Length) * 4;
            Buffer.BlockCopy(bytes, 0, into, 0, copy);
        }
    }
}
