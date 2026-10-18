using System.Collections.Generic;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game
{
    /// <summary>Tile pathfinding for settlers: roads are fast, open ground is fine, woods are slow,
    /// water and cliffs block unless bridged. Most buildings block except the one being visited.</summary>
    public sealed class Nav
    {
        readonly Town town;
        float[] cost;
        int version = -1;
        const int C = Town.Cols, R = Town.Rows;

        public Nav(Town t) { town = t; }

        void Rebuild()
        {
            if (cost == null) cost = new float[C * R];
            for (int y = 0; y < R; y++)
                for (int x = 0; x < C; x++)
                {
                    int i = y * C + x;
                    byte t = town.TerrainAt(x, y);
                    var b = town.OccupantAt(x, y);
                    float c;
                    if (b != null && b.Type == BuildingType.Road) c = 1f;
                    else if (b != null && (b.Def.IsField || !b.Built)) c = 2.5f;
                    else if (b != null) c = -1f;
                    else if (t == Tile.Water) c = AdjacentFishery(x, y) ? 2f : -1f;
                    else if (t == Tile.Hill || t == Tile.Rock) c = -1f;
                    else if (t == Tile.Tree) c = 4f;
                    else c = 2f;
                    cost[i] = c;
                }
            version = town.MapVersion;
        }

        bool AdjacentFishery(int x, int y)
        {
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            for (int k = 0; k < 4; k++) { var b = town.OccupantAt(x + dx[k], y + dy[k]); if (b != null && false) return true; }
            return false;
        }

        public bool IsRoad(int x, int y) { return town.IsRoad(x, y); }

        public float CostAt(int x, int y)
        {
            if (version != town.MapVersion || cost == null) Rebuild();
            if (x < 0 || y < 0 || x >= C || y >= R) return -1;
            return cost[y * C + x];
        }

        // ---- A* on a 4-connected grid ----
        readonly float[] g = new float[C * R];
        readonly int[] from = new int[C * R];
        readonly int[] stamp = new int[C * R];
        int run;

        /// <summary>Tile path from start to goal (inclusive). The building being visited counts as walkable.
        /// Returns null when there's no way through.</summary>
        public List<Vector2Int> Find(Vector2Int s, Vector2Int t, Building allow)
        {
            if (version != town.MapVersion || cost == null) Rebuild();
            if (s.x < 0 || s.y < 0 || s.x >= C || s.y >= R || t.x < 0 || t.y < 0 || t.x >= C || t.y >= R) return null;
            run++;
            var open = new SortedSet<long>();
            int si = s.y * C + s.x, ti = t.y * C + t.x;
            g[si] = 0; from[si] = -1; stamp[si] = run;
            open.Add(Key(Heur(s, t), si));
            int expanded = 0;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (open.Count > 0 && expanded < 24000)
            {
                long top = open.Min; open.Remove(top);
                int cur = (int)(top & 0xFFFFF);
                if (cur == ti) break;
                expanded++;
                int cx = cur % C, cy = cur / C;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + dx[k], ny = cy + dy[k];
                    if (nx < 0 || ny < 0 || nx >= C || ny >= R) continue;
                    int ni = ny * C + nx;
                    float c = cost[ni];
                    if (c < 0)
                    {
                        bool ok = ni == ti || ni == si;
                        if (!ok && allow != null && allow.Contains(nx, ny)) ok = true;
                        if (!ok) continue;
                        c = 2f;
                    }
                    float ng = g[cur] + c;
                    if (stamp[ni] == run && ng >= g[ni]) continue;
                    stamp[ni] = run; g[ni] = ng; from[ni] = cur;
                    open.Add(Key(ng + Heur(new Vector2Int(nx, ny), t), ni));
                }
            }
            if (stamp[ti] != run) return null;
            var path = new List<Vector2Int>();
            for (int c = ti; c != -1; c = from[c]) { path.Add(new Vector2Int(c % C, c / C)); if (path.Count > 1500) break; }
            path.Reverse();
            return path;
        }

        static float Heur(Vector2Int a, Vector2Int b) { return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)) * 1f; }
        static long Key(float f, int i) { return ((long)(f * 64) << 20) | (long)(uint)i; }
    }
}
