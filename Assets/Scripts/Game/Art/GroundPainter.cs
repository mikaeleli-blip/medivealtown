using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>Paints grass, water with sandy banks, hill plateaus with cliffs, roads and bridges, a chunk of the map at a time
    /// into that chunk's own canvas (the map is too big to hold painted all at once).</summary>
    public sealed class GroundPainter
    {
        const string SNOW = "#f2f6f8", SNOW2 = "#cfdde6";
        static readonly string[][] Grass =
        {
            new[] { "#6aab45", "#559a38", "#88c45c", "#77b54e" },
            new[] { "#5a9e3c", "#468a30", "#74b84e", "#66aa44" },
            new[] { "#a29a48", "#8a823a", "#bcb05a", "#b0a24a" },
            new[] { "#e6ecef", "#ccd8df", "#ffffff", "#dbe4e9" },
        };
        public static readonly string[][] HillColors =
        {
            new[] { "#8cc25a", "#74a84a", "#a8d870" },
            new[] { "#7cb450", "#64983f", "#98cc64" },
            new[] { "#bcae5a", "#a0924a", "#d0c470" },
            new[] { "#f4f7f9", "#d6e1e8", "#ffffff" },
        };
        static readonly string[] Cliff = { "#8a6d4c", "#a3855f", "#74593e" };
        public static string GrassBase(int si) { return Grass[si][0]; }

        /// <summary>The chunk being painted, and where it sits on the map in pixels.</summary>
        PixelCanvas Canvas; int ox, oy;
        readonly Town town;
        Noise.Value2D gn;
        int gnSeed = int.MinValue;

        public GroundPainter(Town town)
        {
            this.town = town;
        }

        void Target(PixelCanvas c, int tx0, int ty0) { Canvas = c; ox = tx0 * 16; oy = ty0 * 16; }

        bool Land(byte v) { return v != Tile.Water && v != Tile.OutOfBounds; }

        /// <summary>Paints the tiles of a chunk (tw x th tiles from tx0, ty0) into its canvas.</summary>
        public void PaintChunk(PixelCanvas c, int tx0, int ty0, int tw, int th, int si)
        {
            Target(c, tx0, ty0);
            for (int ty = ty0; ty < ty0 + th; ty++) for (int tx = tx0; tx < tx0 + tw; tx++) PaintBase(tx, ty, si);
            for (int ty = ty0; ty < ty0 + th; ty++) for (int tx = tx0; tx < tx0 + tw; tx++) PaintOverlay(tx, ty, si);
        }

        /// <summary>A quick flat coat for a chunk that's just come into view: each tile its plain colour, until it's painted properly.</summary>
        public void QuickChunk(PixelCanvas c, int tx0, int ty0, int tw, int th, int si)
        {
            var grass = PixelCanvas.C(Grass[si][0]); var hill = PixelCanvas.C(HillColors[si][0]);
            var water = PixelCanvas.C(si == 3 ? "#bcd8e6" : "#3b7cc0"); var road = PixelCanvas.C(si == 3 ? "#cbbfa9" : "#b08a5a");
            for (int ty = 0; ty < th; ty++)
                for (int tx = 0; tx < tw; tx++)
                {
                    int x = tx0 + tx, y = ty0 + ty; byte t = town.TerrainAt(x, y);
                    var col = town.IsRoad(x, y) ? road : t == Tile.Water ? water : t == Tile.Hill ? hill : grass;
                    for (int py = 0; py < 16; py++)
                    {
                        int row = (c.H - 1 - (ty * 16 + py)) * c.W + tx * 16;
                        for (int px = 0; px < 16; px++) c.Px[row + px] = col;
                    }
                }
        }

        /// <summary>Repaints a tile and its neighbours (banks and road joins depend on neighbours) in a chunk's canvas.</summary>
        public void PaintAround(PixelCanvas c, int tx0, int ty0, int x, int y, int si)
        {
            Target(c, tx0, ty0);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) PaintBase(x + dx, y + dy, si);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) PaintOverlay(x + dx, y + dy, si);
        }

        void PaintBase(int tx, int ty, int si)
        {
            if (tx < 0 || ty < 0 || tx >= Town.Cols || ty >= Town.Rows) return;
            if (gnSeed != town.Seed) { gn = new Noise.Value2D(town.Seed + 9); gnSeed = town.Seed; }
            byte t = town.TerrainAt(tx, ty);
            var G = Grass[si]; var H = HillColors[si];
            byte n = town.TerrainAt(tx, ty - 1), s = town.TerrainAt(tx, ty + 1), w = town.TerrainAt(tx - 1, ty), e = town.TerrainAt(tx + 1, ty);
            bool lN = Land(n), lS = Land(s), lW = Land(w), lE = Land(e);
            bool lNW = Land(town.TerrainAt(tx - 1, ty - 1)), lNE = Land(town.TerrainAt(tx + 1, ty - 1));
            bool lSW = Land(town.TerrainAt(tx - 1, ty + 1)), lSE = Land(town.TerrainAt(tx + 1, ty + 1));
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    int wx = tx * 16 + px, wy = ty * 16 + py;
                    float r = Noise.Hash(wx, wy, 1);
                    string c;
                    if (t == Tile.Water)
                    {
                        if (si == 3)
                        {
                            c = r < 0.1f ? "#a8c8d8" : "#bcd8e6";
                            if ((wx + wy * 2) % 13 == 0 && Noise.Hash(wx, wy, 2) < 0.6f) c = "#e8f3f8";
                        }
                        else
                        {
                            c = gn.Sample(wx / 10f, wy / 10f) > 0.55f ? "#336eae" : "#3b7cc0";
                            if (wy % 5 == 0 && ((wx + (wy / 5) * 3) % 9) < 3) c = "#64a4dc";
                        }
                        int dd = 99;
                        if (lN) dd = Mathf.Min(dd, py);
                        if (lS) dd = Mathf.Min(dd, 15 - py);
                        if (lW) dd = Mathf.Min(dd, px);
                        if (lE) dd = Mathf.Min(dd, 15 - px);
                        if (lNW && px + py <= 2) dd = Mathf.Min(dd, Mathf.Max(px, py));
                        if (lNE && (15 - px) + py <= 2) dd = Mathf.Min(dd, Mathf.Max(15 - px, py));
                        if (lSW && px + (15 - py) <= 2) dd = Mathf.Min(dd, Mathf.Max(px, 15 - py));
                        if (lSE && (15 - px) + (15 - py) <= 2) dd = Mathf.Min(dd, Mathf.Max(15 - px, 15 - py));
                        if (dd == 0) c = si == 3 ? "#e6e6de" : "#e0c488";
                        else if (dd == 1) c = si == 3 ? "#d2dde2" : "#c4a06a";
                        else if (dd == 2 && si != 3) c = "#2f6aa4";
                        else if (dd == 3 && si != 3) c = "#8cc0e8";
                    }
                    else if (t == Tile.Hill)
                    {
                        c = r < 0.09f ? H[1] : r > 0.95f ? H[2] : H[0];
                        bool cut = (n != Tile.Hill && w != Tile.Hill && px + py < 3) || (n != Tile.Hill && e != Tile.Hill && (15 - px) + py < 3);
                        if (cut) c = G[0];
                        else if (s != Tile.Hill && py >= 7)
                        {
                            if (py == 7) c = si == 3 ? "#ffffff" : H[2];
                            else if (py == 15) c = "#3a2c20";
                            else if (py == 14) c = "#5a4532";
                            else
                            {
                                c = Cliff[(py + ((wx >> 1) & 1)) % 3];
                                if (((wx + py * 3) % 7 == 0) || Noise.Hash(wx, wy, 5) < 0.08f) c = "#57422e";
                                if ((px == 0 && w != Tile.Hill) || (px == 15 && e != Tile.Hill)) c = "#57422e";
                                if (si == 3 && py == 8) c = SNOW2;
                            }
                        }
                        else
                        {
                            if (w != Tile.Hill && px < 2) c = px > 0 ? H[0] : H[1];
                            if (e != Tile.Hill && px > 13) c = px == 15 ? "#6f6a3a" : H[1];
                            if (n != Tile.Hill && py == 0) c = H[2];
                            if (s != Tile.Hill && py == 6) c = H[1];
                        }
                    }
                    else
                    {
                        c = gn.Sample(wx / 13f, wy / 13f) > 0.6f ? G[3] : G[0];
                        if (r < 0.03f) c = G[1]; else if (r > 0.988f) c = G[2];
                        else if (Noise.Hash(wx >> 1, wy, 3) < 0.035f && py < 15) c = G[1];
                        else if (Noise.Hash(wx, wy >> 1, 9) < 0.02f) c = G[2];
                        if (n == Tile.Hill && (py < 2 || (py < 4 && (wx + wy) % 2 == 0))) c = si == 3 ? "#c3cfd7" : G[1];
                        if (s == Tile.Hill && py == 15) c = G[1];
                    }
                    Canvas.D(wx - ox, wy - oy, c);
                }
        }

        void PaintOverlay(int tx, int ty, int si)
        {
            if (tx < 0 || ty < 0 || tx >= Town.Cols || ty >= Town.Rows) return;
            var g = Canvas; int X = tx * 16 - ox, Y = ty * 16 - oy; byte t = town.TerrainAt(tx, ty);
            if (town.IsRoad(tx, ty)) { if (t == Tile.Water) Bridge(X, Y, tx, ty, si); else Road(X, Y, tx, ty, si); return; }
            if (t == Tile.Water && NextToFishery(tx, ty)) { Pier(X, Y, si); return; }
            if (t == Tile.Hill)
            {
                bool sOpen = town.TerrainAt(tx, ty + 1) != Tile.Hill; int lim = sOpen ? 5 : 14;
                for (int i = 0; i < 3; i++)
                {
                    if (Noise.Hash(tx, ty, i + 90) > 0.45f) continue;
                    int px = 2 + (int)(Noise.Hash(tx, ty, i + 91) * 10), py = 1 + (int)(Noise.Hash(tx, ty, i + 92) * (lim - 1));
                    if (si == 3) { g.D(X + px, Y + py, "#9fb0a0"); g.D(X + px + 2, Y + py, "#9fb0a0"); }
                    else { g.R(X + px, Y + py, 3, 2, HillColors[si][1]); g.D(X + px + 1, Y + py, HillColors[si][2]); g.Blend(X + px, Y + py + 2, new Color32(0, 0, 0, 46)); }
                }
                if (Noise.Hash(tx, ty, 95) < 0.2f)
                {
                    int px = 3 + (int)(Noise.Hash(tx, ty, 96) * 9), py = 1 + (int)(Noise.Hash(tx, ty, 97) * (lim - 1));
                    g.R(X + px, Y + py, 2, 1, "#8d877d"); g.D(X + px, Y + py, "#b2aca1");
                }
                return;
            }
            if (t != Tile.Grass && t != Tile.Tree && t != Tile.Rock && t != Tile.Sapling && t != Tile.Stump) return;
            if (town.OccupantAt(tx, ty) != null) return;
            float r = Noise.Hash(tx, ty, 11);
            string tuft = si == 3 ? "#a9b8ae" : Grass[si][1];
            int tufts = r < 0.4f ? 1 + ((int)(r * 10)) % 3 : 0;
            for (int i = 0; i < tufts; i++)
            {
                int px = 2 + (int)(Noise.Hash(tx, ty, i + 20) * 11), py = 3 + (int)(Noise.Hash(tx, ty, i + 30) * 10);
                g.D(X + px, Y + py, tuft); g.D(X + px + 2, Y + py, tuft); g.D(X + px + 1, Y + py + 1, tuft);
            }
            if (si != 3 && Noise.Hash(tx, ty, 15) < 0.14f)
            {
                // a leafy weed tuft
                int px = 3 + (int)(Noise.Hash(tx, ty, 16) * 9), py = 4 + (int)(Noise.Hash(tx, ty, 17) * 8);
                string dk = Grass[si][1], lt = Grass[si][2];
                g.D(X + px, Y + py, dk); g.D(X + px + 1, Y + py + 1, dk); g.D(X + px + 2, Y + py + 1, lt); g.D(X + px + 3, Y + py, lt);
                g.D(X + px + 4, Y + py - 1, dk); g.D(X + px + 2, Y + py - 1, lt); g.D(X + px + 2, Y + py, dk);
                g.R(X + px + 1, Y + py + 2, 3, 1, dk);
            }
            if (si < 2 && Noise.Hash(tx, ty, 12) < 0.2f)
            {
                string[] fc = { "#f4f1e6", "#f2cf4a", "#a77bc9", "#d9534a" };
                int count = 1 + (int)(Noise.Hash(tx, ty, 13) * 3);
                for (int i = 0; i < count; i++)
                {
                    int px = 1 + (int)(Noise.Hash(tx, ty, i + 50) * 14), py = 1 + (int)(Noise.Hash(tx, ty, i + 60) * 13);
                    g.D(X + px, Y + py, fc[(i + (int)(r * 40)) % 4]); g.D(X + px, Y + py + 1, Grass[si][1]);
                }
            }
            if (si == 2 && Noise.Hash(tx, ty, 14) < 0.45f)
                for (int i = 0; i < 3; i++)
                    g.D(X + (int)(Noise.Hash(tx, ty, i + 70) * 16), Y + (int)(Noise.Hash(tx, ty, i + 80) * 16), i % 2 == 1 ? "#c8642a" : "#e0a03a");
        }

        bool NextToFishery(int tx, int ty)
        {
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                var b = town.OccupantAt(tx + dx[i], ty + dy[i]);
                if (b != null && false) return true;
            }
            return false;
        }

        void Pier(int X, int Y, int si)
        {
            var g = Canvas;
            g.R(X + 1, Y + 1, 14, 14, "#9a6a3a");
            for (int y = Y + 1; y < Y + 15; y += 3) { g.R(X + 1, y, 14, 1, "#b8844a"); g.R(X + 1, y + 2, 14, 1, "#7a4e2c"); }
            g.R(X, Y, 2, 16, "#5a3a22"); g.R(X + 14, Y, 2, 16, "#5a3a22");
            g.RBlend(X + 1, Y + 15, 14, 1, new Color32(0, 0, 0, 70));
            if (si == 3) g.R(X + 2, Y + 1, 12, 1, SNOW);
        }

        void Road(int X, int Y, int tx, int ty, int si)
        {
            var g = Canvas; bool W = si == 3;
            string bas = W ? "#cbbfa9" : "#b08a5a", dk = W ? "#b3a68e" : "#9a774c", lt = W ? "#e2dccf" : "#c29c6a", edge = W ? "#9d917a" : "#7d6240";
            bool n = town.IsRoad(tx, ty - 1), s = town.IsRoad(tx, ty + 1), w = town.IsRoad(tx - 1, ty), e = town.IsRoad(tx + 1, ty);
            g.R(X, Y, 16, 16, bas);
            for (int i = 0; i < 14; i++) g.D(X + (int)(Noise.Hash(tx, ty, i) * 16), Y + (int)(Noise.Hash(ty, tx, i + 40) * 16), i % 2 == 1 ? dk : lt);
            bool hz = w || e, vt = n || s;
            if (hz && !vt) { for (int x = 0; x < 16; x++) if (Noise.Hash(tx * 16 + x, ty, 7) < 0.85f) { g.D(X + x, Y + 5, dk); g.D(X + x, Y + 10, dk); } }
            else if (vt && !hz) { for (int y = 0; y < 16; y++) if (Noise.Hash(tx, ty * 16 + y, 8) < 0.85f) { g.D(X + 5, Y + y, dk); g.D(X + 10, Y + y, dk); } }
            string gc = Grass[si][1];
            if (!n) { g.R(X, Y, 16, 1, edge); for (int i = 0; i < 16; i += 3) if (Noise.Hash(tx, ty, i + 7) < 0.5f) g.D(X + i, Y + 1, gc); }
            if (!s) { g.R(X, Y + 15, 16, 1, edge); for (int i = 1; i < 16; i += 3) if (Noise.Hash(tx, ty, i + 17) < 0.5f) g.D(X + i, Y + 14, gc); }
            if (!w) g.R(X, Y, 1, 16, edge);
            if (!e) g.R(X + 15, Y, 1, 16, edge);
        }

        void Bridge(int X, int Y, int tx, int ty, int si)
        {
            var g = Canvas; bool W = si == 3;
            bool hz = town.IsRoad(tx - 1, ty) || town.IsRoad(tx + 1, ty) || !(town.IsRoad(tx, ty - 1) || town.IsRoad(tx, ty + 1));
            const string pl = "#8a5a34", pd = "#6b4228", rail = "#4a2f1e", post = "#a8734a";
            var shade = new Color32(0, 0, 0, 72);
            if (hz)
            {
                g.R(X, Y + 2, 16, 12, pl);
                for (int x = X; x < X + 16; x += 3) g.R(x, Y + 2, 1, 12, pd);
                g.R(X, Y + 1, 16, 2, rail); g.R(X, Y + 13, 16, 2, rail);
                for (int x = X + 1; x < X + 16; x += 5) { g.R(x, Y, 1, 3, post); g.R(x, Y + 12, 1, 3, post); }
                if (W) g.R(X, Y + 3, 16, 1, SNOW);
                g.RBlend(X, Y + 15, 16, 1, shade);
            }
            else
            {
                g.R(X + 2, Y, 12, 16, pl);
                for (int y = Y; y < Y + 16; y += 3) g.R(X + 2, y, 12, 1, pd);
                g.R(X + 1, Y, 2, 16, rail); g.R(X + 13, Y, 2, 16, rail);
                for (int y = Y + 1; y < Y + 16; y += 5) { g.R(X, y, 3, 1, post); g.R(X + 13, y, 3, 1, post); }
                if (W) g.R(X + 3, Y, 1, 16, SNOW);
                g.RBlend(X + 15, Y, 1, 16, shade);
            }
        }
    }
}
