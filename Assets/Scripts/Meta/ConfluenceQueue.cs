using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rill.Core;

namespace Rill.Meta
{
    /// <summary>
    /// The Confluence: one colossal shared mountain per season, eroded by everybody at once.
    /// Clients never talk to each other — each run's terrain delta is queued locally as a sparse
    /// packet and uploaded opportunistically. The merge is eventually consistent and trivially
    /// cheap, which is why ten million people can share a landmass without a game server.
    ///
    /// The upload endpoint is deliberately not wired here: the game is fully playable offline and
    /// this queue simply accumulates until a backend exists.
    /// </summary>
    public sealed class ConfluenceQueue
    {
        public const float MinDelta = 0.02f;   // ignore changes under 2 cm
        public const int MaxQueuedPackets = 240;

        readonly string _path;

        public ConfluenceQueue(int slot = 0)
        {
            _path = Path.Combine(SaveSystem.RootDir, "confluence_queue_" + slot + ".bin");
        }

        public bool HasPending => File.Exists(_path) && new FileInfo(_path).Length > 8;

        /// <summary>Queues one run's contribution as (cell, quantised delta) pairs.</summary>
        public void Enqueue(HeightField field, float[] beforeHeights, int runNumber, uint worldSeed)
        {
            if (beforeHeights == null) return;
            var cells = new List<int>(512);
            var deltas = new List<short>(512);

            for (int i = 0; i < field.Count; i++)
            {
                float d = field.Height[i] - beforeHeights[i];
                if (d > -MinDelta && d < MinDelta) continue;
                cells.Add(i);
                // Centimetres in a short: ±327 m of change per run, far beyond anything possible.
                deltas.Add((short)Mathf.Clamp(Mathf.RoundToInt(d * 100f), short.MinValue, short.MaxValue));
            }
            if (cells.Count == 0) return;

            try
            {
                using (var fs = new FileStream(_path, FileMode.Append, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(worldSeed);
                    w.Write(runNumber);
                    w.Write(DateTime.UtcNow.Ticks);
                    w.Write(field.Size);
                    w.Write(cells.Count);
                    for (int i = 0; i < cells.Count; i++)
                    {
                        w.Write(cells[i]);
                        w.Write(deltas[i]);
                    }
                }
                TrimIfHuge();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RILL] Confluence enqueue failed: " + e.Message);
            }
        }

        void TrimIfHuge()
        {
            try
            {
                var info = new FileInfo(_path);
                // Cap at ~8 MB; the Confluence is a nicety, never a reason to fill a phone.
                if (info.Length > 8L * 1024L * 1024L) File.Delete(_path);
            }
            catch { /* nothing here is worth interrupting play for */ }
        }

        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        }
    }
}
