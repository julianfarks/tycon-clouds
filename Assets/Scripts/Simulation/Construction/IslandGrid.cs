using System.Collections.Generic;

namespace CloudGarden.Construction
{
    /// <summary>
    /// Square tile grid of one island. Coordinates are centered on the island origin, so expanding
    /// the island never moves existing buildings. Valid tiles: [-Half, Half-1].
    /// </summary>
    public sealed class IslandGrid
    {
        public const int BaseHalf = 4;
        public const int MaxExpansion = 4;

        readonly Dictionary<long, int> occupancy = new Dictionary<long, int>();

        public int Expansion { get; private set; }
        public int Half { get { return BaseHalf + Expansion; } }
        public int Min { get { return -Half; } }
        public int Max { get { return Half - 1; } }
        public int Size { get { return Half * 2; } }

        public void SetExpansion(int level)
        {
            if (level < 0) level = 0;
            if (level > MaxExpansion) level = MaxExpansion;
            Expansion = level;
        }

        static long Key(int x, int y)
        {
            return ((long)(x + 32768) << 20) | (long)(y + 32768);
        }

        public bool InBounds(int x, int y)
        {
            return x >= Min && x <= Max && y >= Min && y <= Max;
        }

        public int OccupantAt(int x, int y)
        {
            int uid;
            return occupancy.TryGetValue(Key(x, y), out uid) ? uid : 0;
        }

        /// <summary>True if the rectangle is fully inside the island and free (ignoring ignoreUid).</summary>
        public bool CanPlace(int x, int y, int w, int d, int ignoreUid)
        {
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < d; j++)
                {
                    int tx = x + i;
                    int ty = y + j;
                    if (!InBounds(tx, ty)) return false;
                    int occ;
                    if (occupancy.TryGetValue(Key(tx, ty), out occ) && occ != ignoreUid) return false;
                }
            }
            return true;
        }

        public void Place(int uid, int x, int y, int w, int d)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < d; j++)
                    occupancy[Key(x + i, y + j)] = uid;
        }

        public void Remove(int uid, int x, int y, int w, int d)
        {
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < d; j++)
                {
                    long k = Key(x + i, y + j);
                    int occ;
                    if (occupancy.TryGetValue(k, out occ) && occ == uid) occupancy.Remove(k);
                }
            }
        }

        public void Clear()
        {
            occupancy.Clear();
        }

        public int OccupiedTiles { get { return occupancy.Count; } }
    }
}
