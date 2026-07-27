using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rill.Core;

namespace Rill.Meta
{
    [Serializable]
    class DailyFile
    {
        public string DateKey;
        public uint Seed;
        public int RunsUsed;
        public float WaterToSea;
        public List<string> Glyphs = new List<string>();
        public string FinalGlyph;
        public bool Complete;
    }

    /// <summary>
    /// Every player gets the same fresh procedurally generated mountain and seven runs. Score is
    /// water delivered to the sea. It shares as a river-signature glyph, which is the part that
    /// travels — the mountain is identical for everybody, so the line you chose is the whole
    /// conversation.
    /// </summary>
    public sealed class DailyRill
    {
        public const int RunsPerDay = 7;

        readonly string _path;
        DailyFile _file;

        /// <summary>Every glyph ever made, kept past the daily rollover that used to discard them.</summary>
        public readonly GlyphJournal Journal;

        public readonly List<List<Vector3>> Paths = new List<List<Vector3>>();
        public readonly List<bool> ReachedSea = new List<bool>();

        public uint Seed => _file.Seed;
        public int RunsUsed => _file.RunsUsed;
        public int RunsLeft => Mathf.Max(0, RunsPerDay - _file.RunsUsed);
        public float WaterToSea => _file.WaterToSea;
        public bool Complete => _file.Complete;
        public string DateKey => _file.DateKey;

        public DailyRill()
        {
            _path = Path.Combine(SaveSystem.RootDir, "daily.json");
            Journal = new GlyphJournal();
            Load();
        }

        public static uint SeedForDate(DateTime utcDate)
        {
            // Date only, UTC. Everyone on earth carves the same rock on the same day.
            uint d = (uint)(utcDate.Year * 10000 + utcDate.Month * 100 + utcDate.Day);
            return Noise.Hash(d ^ 0x52494C4Cu);
        }

        public static string KeyForDate(DateTime utcDate) => utcDate.ToString("yyyy-MM-dd");

        void Load()
        {
            string todayKey = KeyForDate(DateTime.UtcNow);
            if (File.Exists(_path))
            {
                try
                {
                    var f = JsonUtility.FromJson<DailyFile>(File.ReadAllText(_path));
                    if (f != null && f.DateKey == todayKey) { _file = f; return; }
                    // The rollover used to be where yesterday's glyph died: the stale file was
                    // simply replaced. The journal keeps it — a collection you cannot look at is
                    // not a collection. (Normally a no-op: RecordRun journals as it goes; this
                    // catches a day played on a build from before the journal existed.)
                    if (f != null && !string.IsNullOrEmpty(f.FinalGlyph))
                        Journal.Record(f.DateKey, f.FinalGlyph, f.RunsUsed, f.WaterToSea, f.Complete);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[RILL] Daily state unreadable: " + e.Message);
                }
            }
            _file = new DailyFile
            {
                DateKey = todayKey,
                Seed = SeedForDate(DateTime.UtcNow),
                RunsUsed = 0,
                WaterToSea = 0f
            };
            Save();
        }

        public void Save()
        {
            try { File.WriteAllText(_path, JsonUtility.ToJson(_file)); }
            catch (Exception e) { Debug.LogWarning("[RILL] Daily save failed: " + e.Message); }
        }

        /// <summary>Call once per completed daily run.</summary>
        public void RecordRun(List<Vector3> path, bool reachedSea, float waterDelivered, float worldExtent,
                              Rill.Core.HeightField field = null)
        {
            if (_file.Complete) return;
            _file.RunsUsed++;
            _file.WaterToSea += Mathf.Max(0f, waterDelivered);

            Paths.Add(new List<Vector3>(path));
            ReachedSea.Add(reachedSea);

            _file.FinalGlyph = GlyphGenerator.Render(Paths, ReachedSea, worldExtent, field);
            if (_file.RunsUsed >= RunsPerDay) _file.Complete = true;
            Save();
            Journal.Record(_file.DateKey, _file.FinalGlyph, _file.RunsUsed, _file.WaterToSea, _file.Complete);
        }

        public string Glyph => string.IsNullOrEmpty(_file.FinalGlyph) ? "" : _file.FinalGlyph;

        public string ShareText(float worldExtent, Rill.Core.HeightField field = null)
        {
            string glyph = string.IsNullOrEmpty(_file.FinalGlyph)
                ? GlyphGenerator.Render(Paths, ReachedSea, worldExtent, field)
                : _file.FinalGlyph;
            return GlyphGenerator.ShareText(_file.DateKey, _file.RunsUsed, RunsPerDay, _file.WaterToSea, glyph);
        }

        /// <summary>Copies the share block to the system clipboard. One tap, no dialog, no account.</summary>
        public void CopyShareToClipboard(float worldExtent, Rill.Core.HeightField field = null)
        {
            GUIUtility.systemCopyBuffer = ShareText(worldExtent, field);
        }
    }
}
