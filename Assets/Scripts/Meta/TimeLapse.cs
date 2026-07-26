using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rill.Core;

namespace Rill.Meta
{
    /// <summary>
    /// The game silently keyframes the mountain forever. One tap renders five hundred runs in
    /// thirty seconds — the meta-reward of RILL is watching your own play become geology.
    ///
    /// Frames are stored downsampled and quantised to 16 bits: about 32 KB each, so a decade of
    /// play still fits in a folder you would not notice.
    /// </summary>
    public sealed class TimeLapseArchive
    {
        public const int Resolution = 128;
        const uint Magic = 0x504C544Cu; // "LTLP"

        public struct Frame
        {
            public int Run;
            public long UtcTicks;
            public float Min, Max;
            public ushort[] Data;

            public float HeightAt(int i) => Min + (Max - Min) * (Data[i] / 65535f);
        }

        readonly string _path;
        public TimeLapseArchive(int slot = 0)
        {
            _path = Path.Combine(SaveSystem.RootDir, "timelapse_" + slot + ".bin");
        }

        public bool Exists => File.Exists(_path);

        /// <summary>Appends a keyframe. Called every few runs — cheap enough to never think about.</summary>
        public void Append(HeightField f, int runNumber)
        {
            try
            {
                var data = Downsample(f, out float min, out float max);
                bool fresh = !File.Exists(_path);
                using (var fs = new FileStream(_path, FileMode.Append, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    if (fresh)
                    {
                        w.Write(Magic);
                        w.Write(Resolution);
                    }
                    w.Write(runNumber);
                    w.Write(DateTime.UtcNow.Ticks);
                    w.Write(min);
                    w.Write(max);
                    var bytes = new byte[data.Length * 2];
                    Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
                    w.Write(bytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RILL] Time-lapse append failed: " + e.Message);
            }
        }

        public List<Frame> LoadAll()
        {
            var frames = new List<Frame>();
            if (!File.Exists(_path)) return frames;
            try
            {
                using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read))
                using (var r = new BinaryReader(fs))
                {
                    if (r.ReadUInt32() != Magic) return frames;
                    int res = r.ReadInt32();
                    int cells = res * res;
                    while (fs.Position < fs.Length)
                    {
                        var frame = new Frame
                        {
                            Run = r.ReadInt32(),
                            UtcTicks = r.ReadInt64(),
                            Min = r.ReadSingle(),
                            Max = r.ReadSingle(),
                            Data = new ushort[cells]
                        };
                        var bytes = r.ReadBytes(cells * 2);
                        if (bytes.Length < cells * 2) break; // truncated tail: stop cleanly
                        Buffer.BlockCopy(bytes, 0, frame.Data, 0, bytes.Length);
                        frames.Add(frame);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RILL] Time-lapse read failed: " + e.Message);
            }
            return frames;
        }

        static ushort[] Downsample(HeightField f, out float min, out float max)
        {
            int res = Resolution;
            var acc = new float[res * res];
            int step = Mathf.Max(1, f.Size / res);
            min = float.MaxValue;
            max = float.MinValue;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int sx = Mathf.Min(x * step, f.Size - 1);
                    int sz = Mathf.Min(z * step, f.Size - 1);
                    float sum = 0f;
                    int n = 0;
                    for (int dz = 0; dz < step; dz++)
                    {
                        int gz = sz + dz;
                        if (gz >= f.Size) break;
                        for (int dx = 0; dx < step; dx++)
                        {
                            int gx = sx + dx;
                            if (gx >= f.Size) break;
                            sum += f.Height[gz * f.Size + gx];
                            n++;
                        }
                    }
                    float h = n > 0 ? sum / n : 0f;
                    acc[z * res + x] = h;
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }

            if (max - min < 1e-3f) max = min + 1e-3f;
            var outData = new ushort[res * res];
            float inv = 1f / (max - min);
            for (int i = 0; i < acc.Length; i++)
                outData[i] = (ushort)Mathf.Clamp(Mathf.RoundToInt((acc[i] - min) * inv * 65535f), 0, 65535);
            return outData;
        }
    }

    /// <summary>
    /// Plays an archive back on its own low-resolution field so the live mountain is never
    /// touched. "Run 1 → Run 847" in one tap: the share unit TikTok already loves.
    /// </summary>
    public sealed class TimeLapsePlayer : MonoBehaviour
    {
        public float FramesPerSecond = 18f;

        HeightField _field;
        Render.TerrainMeshBuilder _builder;
        List<TimeLapseArchive.Frame> _frames;
        float _t;
        int _index;

        public bool Playing { get; private set; }
        public int CurrentRun => (_frames != null && _index < _frames.Count) ? _frames[_index].Run : 0;
        public float Progress01 => (_frames == null || _frames.Count < 2) ? 1f : _index / (float)(_frames.Count - 1);

        public void Initialise(float cellSizeOfSource, int sourceSize, StrataBand[] bands, Material material)
        {
            int res = TimeLapseArchive.Resolution;
            float cell = cellSizeOfSource * (sourceSize / (float)res);
            _field = new HeightField(res, cell);

            var go = new GameObject("TimeLapseTerrain");
            go.transform.SetParent(transform, false);
            _builder = go.AddComponent<Render.TerrainMeshBuilder>();
            _builder.ChunkCells = 64;
            _builder.ChunkRebuildBudgetPerFrame = 16;
            _builder.Initialise(_field, bands, material);
            go.SetActive(false);
        }

        public bool Play(TimeLapseArchive archive)
        {
            _frames = archive.LoadAll();
            if (_frames.Count < 2) return false;
            _index = 0;
            _t = 0f;
            Playing = true;
            _builder.gameObject.SetActive(true);
            ApplyFrame(0);
            return true;
        }

        public void Stop()
        {
            Playing = false;
            if (_builder != null) _builder.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Playing || _frames == null) return;
            _t += Time.deltaTime * FramesPerSecond;
            while (_t >= 1f)
            {
                _t -= 1f;
                _index++;
                if (_index >= _frames.Count) { _index = _frames.Count - 1; Stop(); return; }
                ApplyFrame(_index);
            }
        }

        void ApplyFrame(int i)
        {
            var f = _frames[i];
            for (int k = 0; k < _field.Count && k < f.Data.Length; k++)
                _field.Height[k] = f.HeightAt(k);
            _field.MarkAllDirty();
        }
    }
}
