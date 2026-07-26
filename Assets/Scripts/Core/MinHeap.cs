namespace Rill.Core
{
    /// <summary>Allocation-light binary min-heap of (float key, int payload). Used by the priority-flood.</summary>
    public sealed class MinHeap
    {
        float[] _keys;
        int[] _values;
        int _count;

        public MinHeap(int capacity)
        {
            capacity = capacity < 16 ? 16 : capacity;
            _keys = new float[capacity];
            _values = new int[capacity];
        }

        public int Count => _count;
        public void Clear() => _count = 0;

        public void Push(float key, int value)
        {
            if (_count == _keys.Length) Grow();
            int i = _count++;
            _keys[i] = key;
            _values[i] = value;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_keys[p] <= _keys[i]) break;
                Swap(p, i);
                i = p;
            }
        }

        public bool Pop(out float key, out int value)
        {
            if (_count == 0) { key = 0f; value = 0; return false; }
            key = _keys[0];
            value = _values[0];
            _count--;
            if (_count > 0)
            {
                _keys[0] = _keys[_count];
                _values[0] = _values[_count];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < _count && _keys[l] < _keys[m]) m = l;
                    if (r < _count && _keys[r] < _keys[m]) m = r;
                    if (m == i) break;
                    Swap(m, i);
                    i = m;
                }
            }
            return true;
        }

        void Swap(int a, int b)
        {
            float k = _keys[a]; _keys[a] = _keys[b]; _keys[b] = k;
            int v = _values[a]; _values[a] = _values[b]; _values[b] = v;
        }

        void Grow()
        {
            int cap = _keys.Length * 2;
            var nk = new float[cap];
            var nv = new int[cap];
            System.Array.Copy(_keys, nk, _count);
            System.Array.Copy(_values, nv, _count);
            _keys = nk;
            _values = nv;
        }
    }
}
