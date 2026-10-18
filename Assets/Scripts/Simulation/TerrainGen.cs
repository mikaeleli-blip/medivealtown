using System;

namespace MedievalTown.Simulation
{
    /// <summary>Deterministic hashing and value noise shared by terrain generation and the renderer.</summary>
    public static class Noise
    {
        /// <summary>Stable 0..1 hash of two ints and a salt.</summary>
        public static float Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967296f;
            }
        }

        public sealed class Value2D
        {
            readonly float[] grid = new float[4096];
            public Value2D(int seed)
            {
                var r = new Random(seed);
                for (int i = 0; i < grid.Length; i++) grid[i] = (float)r.NextDouble();
            }
            float G(int x, int y) { return grid[((y & 63) << 6) + (x & 63)]; }
            static float Smooth(float t) { return t * t * (3f - 2f * t); }
            public float Sample(float x, float y)
            {
                int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
                float xf = Smooth(x - xi), yf = Smooth(y - yi);
                float a = G(xi, yi), b = G(xi + 1, yi), c = G(xi, yi + 1), d = G(xi + 1, yi + 1);
                return a + (b - a) * xf + (c - a) * yf + (a - b - c + d) * xf * yf;
            }
        }
    }

    public static class TerrainGen
    {
        /// <summary>Builds the starting map: a winding river west of the town site, a creek to the east, lakes, plateau hills,
        /// woods and boulders, with open meadow around the town site. Everything is placed relative to the town site,
        /// so the map can be any size.</summary>
        public static byte[] Generate(int seed, int cols, int rows)
        {
            var T = new byte[cols * rows];
            var r = new Random(seed);
            var n1 = new Noise.Value2D(seed + 1);
            var n2 = new Noise.Value2D(seed + 2);
            var n3 = new Noise.Value2D(seed + 3);
            Func<int, int, bool> inb = (x, y) => x >= 0 && y >= 0 && x < cols && y < rows;
            Func<int, int, int> I = (x, y) => y * cols + x;
            int cx = Town.WagonX + 1, cy = Town.WagonY + 1;

            // The river, a good walk west of the town site, and a smaller creek to the east
            Action<double, double, int, double, int> River = (baseX, amp, width, phase, salt) =>
            {
                for (int y = 0; y < rows; y++)
                {
                    double x = baseX + amp * Math.Sin(y / 11.0 + phase) + amp * 0.4 * Math.Sin(y / 4.3 + phase * 2) + amp * 0.5 * Math.Sin(y / 23.0 + phase * 3);
                    int w = width + (n1.Sample(3.3f + salt, y / 5f) > 0.58f ? 1 : 0);
                    int x0 = (int)Math.Round(x - w / 2.0);
                    for (int xx = x0; xx < x0 + w; xx++) if (inb(xx, y)) T[I(xx, y)] = Tile.Water;
                }
            };
            River(cx - 23, 5.5, 3, r.NextDouble() * 6.28, 0);
            River(cx + 36 + r.Next(-4, 5), 4.0, 2, r.NextDouble() * 6.28, 7);

            // Lakes: one handy to the south-east of town, others further out
            Action<int, int, double, double> Lake = (lx, ly, rx, ry) =>
            {
                for (int y = ly - (int)ry - 3; y <= ly + ry + 3; y++)
                    for (int x = lx - (int)rx - 3; x <= lx + rx + 3; x++)
                    {
                        if (!inb(x, y)) continue;
                        double d = Math.Pow((x - lx) / rx, 2) + Math.Pow((y - ly) / ry, 2) + (n1.Sample(x / 3f, y / 3f) - 0.5) * 0.6;
                        if (d < 1) T[I(x, y)] = Tile.Water;
                    }
            };
            Lake(cx + 15 + r.Next(-2, 3), cy + 14, 6, 3.2);
            Lake(cx - 45 + r.Next(-5, 6), cy - 32 + r.Next(-4, 5), 9, 5);
            Lake(cx + 20 + r.Next(-6, 7), cy - 38 + r.Next(-3, 4), 7, 4);
            Lake(cx - 12 + r.Next(-6, 7), cy + 40 + r.Next(-3, 4), 8, 4.5);

            Func<int, int, double> dist = (x, y) => Math.Sqrt(Math.Pow((x - cx) / 1.35, 2) + Math.Pow(y - cy, 2));
            // Hills
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    if (T[I(x, y)] != 0) continue;
                    double f = n2.Sample(x / 9f, y / 9f) * 0.7 + n2.Sample(x / 4f, y / 4f) * 0.3;
                    if (dist(x, y) > 14 && f > 0.65) T[I(x, y)] = Tile.Hill;
                }
            for (int pass = 0; pass < 3; pass++)
            {
                var C = (byte[])T.Clone();
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dy != 0) && inb(x + dx, y + dy) && C[I(x + dx, y + dy)] == Tile.Hill) n++;
                        int i = I(x, y);
                        if (C[i] == Tile.Hill && n < 3) T[i] = Tile.Grass;
                        else if (C[i] == Tile.Grass && n >= 6) T[i] = Tile.Hill;
                    }
            }
            // A hill tile needs another hill tile above or below it so cliffs read properly
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = I(x, y);
                    if (T[i] == Tile.Hill && !(inb(x, y - 1) && T[I(x, y - 1)] == Tile.Hill) && !(inb(x, y + 1) && T[I(x, y + 1)] == Tile.Hill)) T[i] = Tile.Grass;
                }
            // Woods
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = I(x, y);
                    if (T[i] != 0) continue;
                    double d = dist(x, y), f = n3.Sample(x / 7f, y / 7f);
                    if (d > 9 && f > 0.56 && r.NextDouble() < 0.85) T[i] = Tile.Tree;
                    else if (d > 6 && r.NextDouble() < 0.035) T[i] = Tile.Tree;
                }
            // Boulders, mostly at the foot of hills
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = I(x, y);
                    if (T[i] != 0) continue;
                    bool nearHill = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (inb(x + dx, y + dy) && T[I(x + dx, y + dy)] == Tile.Hill) nearHill = true;
                    if (nearHill ? r.NextDouble() < 0.14 : (dist(x, y) > 7 && r.NextDouble() < 0.018)) T[i] = Tile.Rock;
                }
            return T;
        }
    }
}
