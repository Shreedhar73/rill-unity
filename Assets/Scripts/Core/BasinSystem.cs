using System.Collections.Generic;
using UnityEngine;

namespace Rill.Core
{
    /// <summary>
    /// A depression in the terrain that can hold water, with a real capacity and a real spill
    /// point. Basins are the Zeigarnik engine of RILL: "east basin 87% full" is an open loop the
    /// brain refuses to put down, and it is resolved by ordinary play.
    /// </summary>
    public sealed class Basin
    {
        public int Id;
        public int[] Cells;            // grid indices, sorted by ascending terrain height
        public float[] SortedHeights;  // parallel to Cells
        public float[] PrefixVolume;   // volume needed to reach SortedHeights[k], m^3
        public float SpillLevel;       // elevation at which the basin overflows
        public int SpillCell;          // where the water leaves when it does
        public float Capacity;         // m^3 to reach SpillLevel
        public float Volume;           // m^3 currently held
        public float SurfaceLevel;     // current water surface elevation
        public string Name;

        public float FillFraction => Capacity <= 1e-3f ? 0f : Mathf.Clamp01(Volume / Capacity);
        public Vector2Int SpillXZ(int size) => new Vector2Int(SpillCell % size, SpillCell / size);
    }

    /// <summary>
    /// Priority-flood depression analysis over the heightfield. Rebuilt whenever the terrain has
    /// changed enough to matter (end of a run), never during flow.
    /// </summary>
    public sealed class BasinSystem
    {
        readonly HeightField _f;
        readonly int _n;
        readonly float _cellArea;

        float[] _filled;       // depression-filled surface
        bool[] _closed;
        int[] _basinOf;        // per-cell basin id, -1 = none
        MinHeap _heap;
        readonly List<Basin> _basins = new List<Basin>();
        readonly Queue<int> _bfs = new Queue<int>();

        public IReadOnlyList<Basin> Basins => _basins;
        public float[] FilledSurface => _filled;

        /// <summary>Raised when a basin's water passes its spill level. The dam break.</summary>
        public event System.Action<Basin, float> Overflowed;

        public BasinSystem(HeightField field)
        {
            _f = field;
            _n = field.Size;
            _cellArea = field.CellSize * field.CellSize;
            _filled = new float[field.Count];
            _closed = new bool[field.Count];
            _basinOf = new int[field.Count];
            _heap = new MinHeap(field.Size * 8);
        }

        public int BasinIdAt(int cell) => (cell >= 0 && cell < _basinOf.Length) ? _basinOf[cell] : -1;

        public Basin BasinAt(int cell)
        {
            int id = BasinIdAt(cell);
            return id >= 0 ? _basins[id] : null;
        }

        // ------------------------------------------------------------------ analysis

        public void Rebuild()
        {
            PriorityFlood();
            LabelBasins();
            GatherExistingWater();
            SolveLevels(raiseOverflow: false);
        }

        void PriorityFlood()
        {
            int count = _f.Count;
            System.Array.Clear(_closed, 0, count);
            _heap.Clear();

            // Seed with the grid border: everything eventually drains off the map, i.e. to sea.
            for (int x = 0; x < _n; x++)
            {
                Seed(x);                        // z = 0
                Seed((_n - 1) * _n + x);        // z = n-1
            }
            for (int z = 1; z < _n - 1; z++)
            {
                Seed(z * _n);
                Seed(z * _n + _n - 1);
            }

            float key;
            int c;
            while (_heap.Pop(out key, out c))
            {
                int cx = c % _n, cz = c / _n;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    int nz = cz + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= _n || nz >= _n) continue;
                    int ni = nz * _n + nx;
                    if (_closed[ni]) continue;
                    _closed[ni] = true;
                    float w = Mathf.Max(_f.Height[ni], _filled[c]);
                    _filled[ni] = w;
                    _heap.Push(w, ni);
                }
            }
        }

        void Seed(int i)
        {
            if (_closed[i]) return;
            _closed[i] = true;
            _filled[i] = _f.Height[i];
            _heap.Push(_filled[i], i);
        }

        void LabelBasins()
        {
            // Ignore anything shallower than 10 cm. Below that a "basin" is just noise in the
            // heightfield, and 47 nameless puddles is not a progression track.
            //
            // Ignore anything at or below sea level too. The priority flood happily labels
            // depressions in the sea floor as basins, and they are not places the player can ever
            // route water to: 9 of the 14 "basins" on the default seed had floors 7-158 m BELOW
            // sea level, 230-330 m from the summit, including the two largest by capacity. They
            // sat at 0% forever, made "basins found 14, capacity 16,712 m³" false (the truth was
            // 5 and 5,325 m³), and poisoned every routing test that picked a target at random.
            const float eps = 0.10f;
            for (int i = 0; i < _basinOf.Length; i++) _basinOf[i] = -1;
            _basins.Clear();

            var cells = new List<int>(1024);
            for (int start = 0; start < _f.Count; start++)
            {
                if (_basinOf[start] >= 0) continue;
                if (_f.Height[start] <= _f.SeaLevel) continue;
                if (_filled[start] - _f.Height[start] <= eps) continue;

                int id = _basins.Count;
                cells.Clear();
                _bfs.Clear();
                _bfs.Enqueue(start);
                _basinOf[start] = id;

                float spill = _filled[start];
                while (_bfs.Count > 0)
                {
                    int c = _bfs.Dequeue();
                    cells.Add(c);
                    if (_filled[c] > spill) spill = _filled[c];
                    int cx = c % _n, cz = c / _n;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                        int nz = cz + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= _n || nz >= _n) continue;
                        int ni = nz * _n + nx;
                        if (_basinOf[ni] >= 0) continue;
                        if (_f.Height[ni] <= _f.SeaLevel) continue;
                        if (_filled[ni] - _f.Height[ni] <= eps) continue;
                        // Same depression only if the two share a spill elevation.
                        if (Mathf.Abs(_filled[ni] - _filled[c]) > 0.25f) continue;
                        _basinOf[ni] = id;
                        _bfs.Enqueue(ni);
                    }
                }

                if (cells.Count < 24) // too small to be a "place" the player would name; un-label it
                {
                    for (int k = 0; k < cells.Count; k++) _basinOf[cells[k]] = -1;
                    continue;
                }

                _basins.Add(BuildBasin(id, cells, spill));
            }
        }

        Basin BuildBasin(int id, List<int> cells, float spillLevel)
        {
            var arr = cells.ToArray();
            System.Array.Sort(arr, (a, b) => _f.Height[a].CompareTo(_f.Height[b]));

            var heights = new float[arr.Length];
            for (int k = 0; k < arr.Length; k++) heights[k] = _f.Height[arr[k]];

            // PrefixVolume[k] = volume required to raise the surface to heights[k].
            var prefix = new float[arr.Length];
            float vol = 0f;
            for (int k = 1; k < arr.Length; k++)
            {
                vol += k * (heights[k] - heights[k - 1]) * _cellArea;
                prefix[k] = vol;
            }

            float capacity = vol + arr.Length * Mathf.Max(0f, spillLevel - heights[heights.Length - 1]) * _cellArea;

            var b = new Basin
            {
                Id = id,
                Cells = arr,
                SortedHeights = heights,
                PrefixVolume = prefix,
                SpillLevel = spillLevel,
                Capacity = capacity,
                SpillCell = FindSpillCell(arr, spillLevel),
                Name = NameFor(arr[0])
            };
            return b;
        }

        int FindSpillCell(int[] cells, float spillLevel)
        {
            int best = cells[0];
            float bestH = float.MaxValue;
            for (int k = 0; k < cells.Length; k++)
            {
                int c = cells[k];
                int cx = c % _n, cz = c / _n;
                for (int q = 0; q < 4; q++)
                {
                    int nx = cx + (q == 0 ? 1 : q == 1 ? -1 : 0);
                    int nz = cz + (q == 2 ? 1 : q == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= _n || nz >= _n) continue;
                    int ni = nz * _n + nx;
                    if (_basinOf[ni] == _basinOf[c]) continue;
                    float h = _f.Height[ni];
                    if (h < bestH) { bestH = h; best = ni; }
                }
            }
            return best;
        }

        static readonly string[] Compass = { "North", "North-east", "East", "South-east", "South", "South-west", "West", "North-west" };

        string NameFor(int cell)
        {
            int x = cell % _n, z = cell / _n;
            float dx = x - _n * 0.5f, dz = z - _n * 0.5f;
            float ang = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            int oct = Mathf.RoundToInt(((ang + 360f) % 360f) / 45f) % 8;
            return Compass[oct] + " basin";
        }

        // ------------------------------------------------------------------ water

        void GatherExistingWater()
        {
            for (int b = 0; b < _basins.Count; b++) _basins[b].Volume = 0f;

            for (int i = 0; i < _f.Count; i++)
            {
                float w = _f.Water[i];
                if (w <= 0f) continue;
                int id = _basinOf[i];
                if (id >= 0)
                {
                    _basins[id].Volume += w * _cellArea;
                    continue;
                }

                // Orphaned water: the run reshaped the ground under it and this cell is no longer
                // inside a depression. Deleting it here is what silently emptied every lake on
                // the mountain between runs — water the player had spent runs collecting simply
                // vanished. It has to go somewhere, so send it downhill until it finds a basin.
                _f.Water[i] = 0f;
                int landed = RouteDownhill(i);
                if (landed >= 0) _basins[_basinOf[landed]].Volume += w * _cellArea;
            }
        }

        /// <summary>
        /// Walks steepest descent from a cell until it reaches a basin, leaves the map, or gives
        /// up. Returns the cell it settled in, or -1 if the water drained to sea.
        /// </summary>
        int RouteDownhill(int start)
        {
            int c = start;
            for (int step = 0; step < 512; step++)
            {
                if (_basinOf[c] >= 0) return c;
                if (_f.Height[c] <= _f.SeaLevel) return -1;

                int cx = c % _n, cz = c / _n;
                int best = -1;
                float bestH = _f.Height[c];
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + (k == 0 || k == 4 || k == 5 ? 1 : k == 1 || k == 6 || k == 7 ? -1 : 0);
                    int nz = cz + (k == 2 || k == 4 || k == 6 ? 1 : k == 3 || k == 5 || k == 7 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= _n || nz >= _n) continue;
                    int ni = nz * _n + nx;
                    if (_f.Height[ni] < bestH) { bestH = _f.Height[ni]; best = ni; }
                }
                if (best < 0) return -1;   // a flat with no basin label: the water is simply gone
                c = best;
            }
            return -1;
        }

        /// <summary>Adds water at a cell. Returns the basin it landed in, or null if it drained off.</summary>
        public Basin AddWater(int cell, float volume)
        {
            if (volume <= 0f) return null;

            int id = BasinIdAt(cell);
            if (id < 0)
            {
                // The run stopped on open ground, which is the common case — most of a mountain
                // is not a depression. Returning here threw the run's whole remaining volume
                // away, so basins never filled and the game's main retention loop never ran.
                // Water that stops on a slope does not evaporate; it seeps to the low ground.
                int landed = RouteDownhill(cell);
                if (landed < 0) return null;
                id = _basinOf[landed];
                if (id < 0) return null;
            }
            var b = _basins[id];
            b.Volume += volume;
            SolveLevel(b, raiseOverflow: true);
            return b;
        }

        public void SolveLevels(bool raiseOverflow)
        {
            for (int i = 0; i < _basins.Count; i++) SolveLevel(_basins[i], raiseOverflow);
        }

        /// <summary>
        /// Converts a basin's stored volume into a real water surface, writes per-cell depths,
        /// and fires the overflow event when the surface reaches the spill level.
        /// </summary>
        public void SolveLevel(Basin b, bool raiseOverflow)
        {
            float overflow = 0f;
            if (b.Volume > b.Capacity)
            {
                overflow = b.Volume - b.Capacity;
                b.Volume = b.Capacity;
            }

            float level = LevelForVolume(b, b.Volume);
            b.SurfaceLevel = level;

            for (int k = 0; k < b.Cells.Length; k++)
            {
                float d = level - b.SortedHeights[k];
                _f.Water[b.Cells[k]] = d > 0f ? d : 0f;
                if (d > 0f)
                {
                    int c = b.Cells[k];
                    _f.Wet[c] = Mathf.Min(1f, _f.Wet[c] + 0.35f);
                }
            }

            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int k = 0; k < b.Cells.Length; k++)
            {
                int c = b.Cells[k];
                int x = c % _n, z = c / _n;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }
            _f.MarkDirty(minX, minZ, maxX, maxZ);

            if (overflow > 0f && raiseOverflow && Overflowed != null) Overflowed(b, overflow);
        }

        public float LevelForVolume(Basin b, float volume)
        {
            if (volume <= 0f) return b.SortedHeights[0];
            var prefix = b.PrefixVolume;
            int lo = 0, hi = prefix.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (prefix[mid] <= volume) lo = mid; else hi = mid - 1;
            }
            // lo cells are submerged; distribute the remainder evenly over them.
            int submerged = lo + 1;
            float rest = volume - prefix[lo];
            return b.SortedHeights[lo] + rest / (submerged * _cellArea);
        }

        /// <summary>Total standing water on the mountain, m^3. A progression stat read off the world.</summary>
        public float TotalWater()
        {
            float v = 0f;
            for (int i = 0; i < _basins.Count; i++) v += _basins[i].Volume;
            return v;
        }
    }
}
