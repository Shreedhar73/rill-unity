using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rill.Flow;

namespace Rill.Meta
{
    [Serializable]
    public class AlmanacEntry
    {
        public int Run;
        public long UtcTicks;
        public string Kind;      // "secret" | "life" | "overflow" | "milestone"
        public string Text;

        public DateTime Utc => new DateTime(UtcTicks, DateTimeKind.Utc);
        public string DateLabel => Utc.ToLocalTime().ToString("d MMM yyyy");
    }

    [Serializable]
    public class RunSummary
    {
        public int Run;
        public long UtcTicks;
        public float Seconds;
        public float Distance;
        public float TopSpeed;
        public float WaterToSea;
        public float Sediment;
        public float DeepestCarve;
        public string Ending;
        public string Headline;
    }

    [Serializable]
    class AlmanacFile
    {
        public List<AlmanacEntry> Entries = new List<AlmanacEntry>();
        public List<RunSummary> Runs = new List<RunSummary>();
        public int TotalRuns;
        public long LastPlayedUtcTicks;
        public int DayStreak;
        public long StreakDayUtcTicks;
    }

    /// <summary>
    /// An automatic illustrated journal: every fossil found, species arrived, overflow triggered,
    /// with the date and the run number. Nobody has to write it and nobody has to read it — but
    /// it is the artefact a 70-year-old shows their grandchildren, and it is the biography of a
    /// place that only exists because someone played.
    /// </summary>
    public sealed class Almanac
    {
        const int MaxRunsKept = 2000;

        readonly AlmanacFile _file;
        readonly string _path;

        public IReadOnlyList<AlmanacEntry> Entries => _file.Entries;
        public IReadOnlyList<RunSummary> Runs => _file.Runs;
        public int DayStreak => _file.DayStreak;
        public long LastPlayedUtcTicks => _file.LastPlayedUtcTicks;

        Almanac(AlmanacFile file, string path)
        {
            _file = file;
            _path = path;
        }

        public static Almanac Load(int slot = 0)
        {
            string path = Path.Combine(SaveSystem.RootDir, "almanac_" + slot + ".json");
            if (File.Exists(path))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<AlmanacFile>(File.ReadAllText(path));
                    if (loaded != null) return new Almanac(loaded, path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[RILL] Almanac unreadable, starting a new one: " + e.Message);
                }
            }
            return new Almanac(new AlmanacFile(), path);
        }

        public void Save()
        {
            try { File.WriteAllText(_path, JsonUtility.ToJson(_file)); }
            catch (Exception e) { Debug.LogWarning("[RILL] Almanac save failed: " + e.Message); }
        }

        public void Note(int run, string kind, string text)
        {
            _file.Entries.Add(new AlmanacEntry { Run = run, UtcTicks = DateTime.UtcNow.Ticks, Kind = kind, Text = text });
        }

        /// <summary>
        /// Records the run and folds in everything the carve report discovered. Streaks are
        /// counted but never spent, never lost loudly, and never gate anything.
        /// </summary>
        public void RecordRun(CarveReport rep)
        {
            var now = DateTime.UtcNow;

            _file.Runs.Add(new RunSummary
            {
                Run = rep.RunNumber,
                UtcTicks = now.Ticks,
                Seconds = rep.Duration,
                Distance = rep.DistanceTravelled,
                TopSpeed = rep.TopSpeed,
                WaterToSea = rep.WaterToSea,
                Sediment = rep.SedimentMoved,
                DeepestCarve = rep.DeepestCarve,
                Ending = rep.Ending.ToString(),
                Headline = rep.Summary()
            });
            if (_file.Runs.Count > MaxRunsKept) _file.Runs.RemoveRange(0, _file.Runs.Count - MaxRunsKept);

            for (int i = 0; i < rep.Revealed.Count; i++)
                Note(rep.RunNumber, "secret", rep.Revealed[i].DisplayName + " uncovered");
            for (int i = 0; i < rep.LifeArrivals.Count; i++)
                Note(rep.RunNumber, "life", rep.LifeArrivals[i]);
            if (rep.Overflowed)
                Note(rep.RunNumber, "overflow", rep.OverflowBasin + " overflowed and opened new ground");

            _file.TotalRuns = rep.RunNumber;
            UpdateStreak(now);
            _file.LastPlayedUtcTicks = now.Ticks;
        }

        void UpdateStreak(DateTime now)
        {
            DateTime today = now.Date;
            DateTime last = _file.StreakDayUtcTicks == 0 ? DateTime.MinValue : new DateTime(_file.StreakDayUtcTicks, DateTimeKind.Utc).Date;
            if (last == today) return;
            _file.DayStreak = (today - last).TotalDays <= 1.5 ? _file.DayStreak + 1 : 1;
            _file.StreakDayUtcTicks = today.Ticks;
        }

        public void NoteMilestones(int runNumber, float lifetimeSediment, int secretsFound)
        {
            // Milestones are observations, not rewards. They cost nothing and unlock nothing.
            int[] runMarks = { 10, 50, 100, 250, 500, 1000, 2500 };
            for (int i = 0; i < runMarks.Length; i++)
                if (runNumber == runMarks[i]) Note(runNumber, "milestone", "Run " + runNumber);

            int[] sedimentMarks = { 1000, 10000, 100000, 1000000 };
            for (int i = 0; i < sedimentMarks.Length; i++)
            {
                float m = sedimentMarks[i];
                if (lifetimeSediment >= m && lifetimeSediment - m < 250f)
                    Note(runNumber, "milestone", string.Format("{0:n0} m³ of mountain moved", m));
            }
        }
    }
}
