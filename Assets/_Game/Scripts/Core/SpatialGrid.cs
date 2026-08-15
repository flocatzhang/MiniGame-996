using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>
    /// Uniform grid over enemy indices. Every range query in the game (weapon targeting,
    /// aoe, contact damage, magnet pickup, push) goes through here, which is what keeps the
    /// no-physics rule affordable once a few hundred enemies are alive.
    /// Indices are positions inside the caller's list and stay valid until the next Rebuild.
    /// </summary>
    public sealed class SpatialGrid
    {
        const float CellSize = 2f;
        const float InvCellSize = 1f / CellSize;

        readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>(512);
        readonly Stack<List<int>> _spare = new Stack<List<int>>(128);
        readonly List<long> _usedKeys = new List<long>(512);

        public void Clear()
        {
            for (int i = 0; i < _usedKeys.Count; i++)
            {
                List<int> bucket;
                if (_cells.TryGetValue(_usedKeys[i], out bucket))
                {
                    bucket.Clear();
                    _spare.Push(bucket);
                }
            }

            _usedKeys.Clear();
            _cells.Clear();
        }

        public void Insert(int index, Vector2 pos)
        {
            long key = KeyOf(pos);
            List<int> bucket;
            if (!_cells.TryGetValue(key, out bucket))
            {
                bucket = _spare.Count > 0 ? _spare.Pop() : new List<int>(8);
                _cells[key] = bucket;
                _usedKeys.Add(key);
            }

            bucket.Add(index);
        }

        /// <summary>Appends every index whose cell overlaps the circle. Callers still do the precise test.</summary>
        public void QueryCircle(Vector2 center, float radius, List<int> results)
        {
            results.Clear();

            int minX = Mathf.FloorToInt((center.x - radius) * InvCellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) * InvCellSize);
            int minY = Mathf.FloorToInt((center.y - radius) * InvCellSize);
            int maxY = Mathf.FloorToInt((center.y + radius) * InvCellSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    List<int> bucket;
                    if (!_cells.TryGetValue(Key(x, y), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        results.Add(bucket[i]);
                    }
                }
            }
        }

        static long KeyOf(Vector2 pos)
        {
            return Key(Mathf.FloorToInt(pos.x * InvCellSize), Mathf.FloorToInt(pos.y * InvCellSize));
        }

        static long Key(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }
    }
}
