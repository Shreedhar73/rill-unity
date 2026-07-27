using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rill.Meta
{
    [Serializable]
    public class GlyphJournalEntry
    {
        public string DateKey;
        public string Glyph;
        public int RunsUsed;
        public float WaterToSea;
        public bool Complete;
    }

    [Serializable]
    class GlyphJournalFile
    {
        public List<GlyphJournalEntry> Entries = new List<GlyphJournalEntry>();
    }

    /// <summary>
    /// Every daily glyph the player has ever made. The Daily produced one shareable glyph and then
    /// `DailyRill.Load` threw it away at the next day's rollover — a collection you cannot look at
    /// is not a collection. Kept glyphs are a calendar of played days: the streak made visible
    /// without inventing a streak counter, and nothing here is awarded — every entry is just what
    /// the day's seven runs actually drew.
    ///
    /// Global rather than per-slot, like daily.json itself: the Daily belongs to the player, not
    /// to a mountain.
    /// </summary>
    public sealed class GlyphJournal
    {
        readonly string _path;
        GlyphJournalFile _file;

        public GlyphJournal(string path = null)
        {
            _path = path ?? Path.Combine(SaveSystem.RootDir, "glyph_journal.json");
            Load();
        }

        public IReadOnlyList<GlyphJournalEntry> Entries => _file.Entries;
        public int PlayedDays => _file.Entries.Count;

        void Load()
        {
            _file = null;
            if (File.Exists(_path))
            {
                try { _file = JsonUtility.FromJson<GlyphJournalFile>(File.ReadAllText(_path)); }
                catch (Exception e) { Debug.LogWarning("[RILL] Glyph journal unreadable: " + e.Message); }
            }
            if (_file == null) _file = new GlyphJournalFile();
            if (_file.Entries == null) _file.Entries = new List<GlyphJournalEntry>();
        }

        /// <summary>
        /// Upserts a day. Called after every daily run — not only at rollover — so the journal is
        /// always current and a crash between the last run and midnight loses nothing.
        /// </summary>
        public void Record(string dateKey, string glyph, int runsUsed, float waterToSea, bool complete)
        {
            if (string.IsNullOrEmpty(dateKey) || string.IsNullOrEmpty(glyph)) return;
            for (int i = 0; i < _file.Entries.Count; i++)
            {
                if (_file.Entries[i].DateKey != dateKey) continue;
                _file.Entries[i].Glyph = glyph;
                _file.Entries[i].RunsUsed = runsUsed;
                _file.Entries[i].WaterToSea = waterToSea;
                _file.Entries[i].Complete = complete;
                Save();
                return;
            }
            _file.Entries.Add(new GlyphJournalEntry
            {
                DateKey = dateKey, Glyph = glyph, RunsUsed = runsUsed,
                WaterToSea = waterToSea, Complete = complete
            });
            // Kept in date order so "the last N" is a plain slice. DateKeys are yyyy-MM-dd, so
            // ordinal string order IS date order.
            _file.Entries.Sort((a, b) => string.CompareOrdinal(a.DateKey, b.DateKey));
            Save();
        }

        void Save()
        {
            try { File.WriteAllText(_path, JsonUtility.ToJson(_file)); }
            catch (Exception e) { Debug.LogWarning("[RILL] Glyph journal save failed: " + e.Message); }
        }

        /// <summary>
        /// Consecutive played days ending at the given date (inclusive if played). Computed from
        /// the entries rather than stored, so it can never drift from the record and never punishes
        /// — a missed day just starts the count again.
        /// </summary>
        public int StreakEndingAt(DateTime utcToday)
        {
            var have = new HashSet<string>();
            for (int i = 0; i < _file.Entries.Count; i++) have.Add(_file.Entries[i].DateKey);
            int streak = 0;
            var d = utcToday;
            // Today not yet played does not break the streak built through yesterday.
            if (!have.Contains(DailyRill.KeyForDate(d))) d = d.AddDays(-1);
            while (have.Contains(DailyRill.KeyForDate(d))) { streak++; d = d.AddDays(-1); }
            return streak;
        }

        /// <summary>The journal as panel text: recent glyphs in full, older days as one line each.</summary>
        public string PanelBlock(int fullGlyphs, DateTime utcToday)
        {
            if (_file.Entries.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("Daily glyphs — {0} day{1} played, streak {2}\n",
                PlayedDays, PlayedDays == 1 ? "" : "s", StreakEndingAt(utcToday));
            int from = Mathf.Max(0, _file.Entries.Count - fullGlyphs);
            for (int i = 0; i < from; i++)
            {
                var e = _file.Entries[i];
                sb.AppendFormat("{0}   {1} runs · {2:n0} m³{3}\n", e.DateKey, e.RunsUsed, e.WaterToSea,
                    e.Complete ? "" : " · unfinished");
            }
            for (int i = from; i < _file.Entries.Count; i++)
            {
                var e = _file.Entries[i];
                sb.AppendFormat("{0}   {1} runs · {2:n0} m³{3}\n{4}\n", e.DateKey, e.RunsUsed, e.WaterToSea,
                    e.Complete ? "" : " · unfinished", e.Glyph);
            }
            return sb.ToString();
        }
    }
}
