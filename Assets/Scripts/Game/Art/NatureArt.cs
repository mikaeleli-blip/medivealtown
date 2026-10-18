using System.Collections.Generic;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>Big leafy trees, pines, saplings, stumps, boulders, chickens and the 1-pixel white sprite.</summary>
    public static class NatureArt
    {
        const string SNOW = "#f4f8fa", SNOW2 = "#d4e2ea";
        const string TREE_OL = "#1f2a14", ROCK_OL = "#2e2a26";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Cached(string key, System.Func<PixelCanvas> make, Vector2 pivot)
        {
            Sprite s;
            if (!cache.TryGetValue(key, out s)) { s = make().ToSprite(key, pivot); cache[key] = s; }
            return s;
        }

        // ------------------------------------------------------------------ placement
        public struct Placement { public Sprite Sprite; public float Px, Py; }

        /// <summary>Sprite and anchor (bottom-centre of the trunk, in map pixels) for whatever grows on a tile.</summary>
        public static Placement At(int tx, int ty, byte tile, int season)
        {
            float ox = Mathf.Round((Noise.Hash(tx, ty, 21) - 0.5f) * 6), oy = Mathf.Round((Noise.Hash(tx, ty, 22) - 0.3f) * 4);
            float px = tx * 16 + 8 + ox, py = ty * 16 + 13 + oy;
            switch (tile)
            {
                case Tile.Rock: return new Placement { Sprite = Rock(season, Noise.Hash(tx, ty, 23) < 0.5f ? 1 : 0), Px = px, Py = py };
                case Tile.Sapling: return new Placement { Sprite = Sapling(season), Px = px, Py = py };
                case Tile.Stump: return new Placement { Sprite = Stump(season), Px = px, Py = py };
            }
            bool pine = Noise.Hash(tx, ty, 5) < (ty < Balance.Rows / 2 ? 0.5f : 0.28f);
            if (pine) return new Placement { Sprite = Pine(season, Noise.Hash(tx, ty, 7) < 0.5f ? 0 : 1), Px = px, Py = py };
            int kind = Noise.Hash(tx, ty, 6) < 0.6f ? 0 : 1;   // oak or maple
            return new Placement { Sprite = Leafy(season, kind, Noise.Hash(tx, ty, 8) < 0.5f ? 0 : 1), Px = px, Py = py };
        }

        // ------------------------------------------------------------------ leafy trees
        static readonly string[][][] Canopy =
        {
            // oak: spring, summer, autumn  (outline-adjacent dark, mid, light, highlight)
            new[] { new[] { "#3d7a2a", "#56a03a", "#78c24a", "#a8de6a" }, new[] { "#2e6424", "#428a32", "#5aa83e", "#86c85a" }, new[] { "#8a5a1a", "#c47a24", "#e0a032", "#f4c860" } },
            // maple
            new[] { new[] { "#4a8a2e", "#68ae3e", "#8ccc52", "#b8e47a" }, new[] { "#356e28", "#4c9436", "#66b246", "#92d066" }, new[] { "#8a2a1a", "#c43a24", "#e0602e", "#f49a4a" } },
        };

        public static Sprite Leafy(int si, int kind, int v)
        {
            return Cached("leafy" + si + kind + v, () =>
            {
                var g = new PixelCanvas(34, 46);
                const int cx = 17, baseY = 43;
                // ground shadow
                g.Ellipse(cx, baseY + 0.5f, 9, 2.2f, new Color32(0, 0, 0, 60), true);
                // trunk with roots
                g.R(cx - 3, 26, 6, baseY - 26 + 1, "#7a4e2c");
                g.R(cx + 1, 26, 2, baseY - 26 + 1, "#5e3a20");
                g.R(cx - 3, 26, 1, baseY - 26 + 1, "#94603a");
                g.R(cx - 5, baseY - 1, 3, 2, "#7a4e2c"); g.R(cx + 3, baseY - 1, 3, 2, "#5e3a20");
                for (int y = 30; y < baseY - 1; y += 4) g.D(cx - 1, y, "#5e3a20");
                if (si == 3)
                {
                    // bare branches with a dusting of snow
                    const string br = "#6a4428";
                    g.R(cx - 2, 12, 4, 15, "#7a4e2c");
                    g.Line(cx - 1, 24, cx - 9, 12, br); g.Line(cx - 1, 23, cx - 10, 13, br);
                    g.Line(cx + 1, 22, cx + 9, 10, br); g.Line(cx + 2, 22, cx + 10, 11, br);
                    g.Line(cx, 16, cx - 5, 4, br); g.Line(cx, 15, cx + 4, 3, br); g.Line(cx, 14, cx, 2, br);
                    g.Line(cx - 6, 16, cx - 12, 14, br); g.Line(cx + 6, 15, cx + 13, 13, br);
                    g.Line(cx - 4, 8, cx - 8, 6, br); g.Line(cx + 3, 7, cx + 7, 4, br);
                    foreach (var p in new[] { new[] { cx - 9, 11 }, new[] { cx - 10, 12 }, new[] { cx + 9, 9 }, new[] { cx + 10, 10 }, new[] { cx - 5, 3 }, new[] { cx + 4, 2 }, new[] { cx, 1 }, new[] { cx - 12, 13 }, new[] { cx + 13, 12 } }) g.D(p[0], p[1], SNOW);
                    g.Outline("#3a2618");
                    return g;
                }
                var pal = Canopy[kind][si == 0 ? 0 : si == 1 ? 1 : 2];
                // canopy: a cluster of blobs
                var blobs = v == 0
                    ? new[] { new[] { 17f, 15, 11 }, new[] { 9f, 19, 7 }, new[] { 25f, 19, 7 }, new[] { 12f, 9, 7 }, new[] { 22f, 9, 7 }, new[] { 17f, 22, 8 } }
                    : new[] { new[] { 17f, 14, 10 }, new[] { 10f, 18, 7.5f }, new[] { 24f, 17, 7.5f }, new[] { 17f, 7, 7.5f }, new[] { 17f, 22, 8 } };
                var inside = new bool[34, 46];
                var shade = new float[34, 46];
                foreach (var b in blobs)
                    for (int y = 0; y < 46; y++)
                        for (int x = 0; x < 34; x++)
                        {
                            float dx = x + 0.5f - b[0], dy = y + 0.5f - b[1];
                            float d = Mathf.Sqrt(dx * dx + dy * dy) + (Noise.Hash(x, y, (int)b[0] * 7 + v) - 0.5f) * 1.4f;
                            if (d >= b[2]) continue;
                            // light from the upper left, per blob
                            float l = (-dx * 0.55f - dy * 0.8f) / b[2] + 0.15f;
                            if (!inside[x, y] || l > shade[x, y]) shade[x, y] = l;
                            inside[x, y] = true;
                        }
                for (int y = 0; y < 46; y++)
                    for (int x = 0; x < 34; x++)
                    {
                        if (!inside[x, y]) continue;
                        float l = shade[x, y] + (Noise.Hash(x, y, 70 + v) - 0.5f) * 0.35f;
                        string col = l > 0.62f ? pal[3] : l > 0.2f ? pal[2] : l > -0.25f ? pal[1] : pal[0];
                        g.D(x, y, col);
                    }
                // leaf texture: little dark notches
                for (int i = 0; i < 40; i++)
                {
                    int x = (int)(Noise.Hash(i, v, 81 + kind) * 34), y = (int)(Noise.Hash(v, i, 82 + kind) * 30);
                    if (inside[x, y] && y + 1 < 46 && inside[x, y + 1]) { g.D(x, y, pal[0]); g.D(x + 1, y + 1, pal[0]); }
                }
                if (si == 0 && kind == 0)
                    for (int i = 0; i < 24; i++)
                    {
                        int x = (int)(Noise.Hash(i, 3, 91 + v) * 34), y = (int)(Noise.Hash(3, i, 92 + v) * 30);
                        if (inside[x, y]) { g.D(x, y, "#f6c8d6"); if (Noise.Hash(i, 4, 93) < 0.5f) g.D(x + 1, y, "#ffe2ea"); }
                    }
                if (si == 1 && kind == 1)
                    for (int i = 0; i < 10; i++)
                    {
                        int x = (int)(Noise.Hash(i, 5, 94 + v) * 34), y = (int)(Noise.Hash(5, i, 95 + v) * 30);
                        if (inside[x, y]) g.D(x, y, "#d84a3a");   // a few red fruit
                    }
                g.Outline(TREE_OL);
                return g;
            }, new Vector2(17f / 34f, 3f / 46f));
        }

        // ------------------------------------------------------------------ pines
        public static Sprite Pine(int si, int v)
        {
            return Cached("pine" + si + v, () =>
            {
                var g = new PixelCanvas(26, 44);
                const int cx = 13, baseY = 41;
                g.Ellipse(cx, baseY + 0.5f, 7, 2, new Color32(0, 0, 0, 60), true);
                g.R(cx - 2, 32, 4, baseY - 32 + 1, "#6a4428"); g.R(cx + 1, 32, 1, baseY - 32 + 1, "#4e3020");
                string[] P = { "#173a26", "#22523a", "#2e6a46", "#3f8a5a" };
                int tiers = 4;
                for (int i = 0; i < tiers; i++)
                {
                    int top = 2 + i * 7 + (v == 1 ? i : 0), h = 11;
                    float maxW = 4.5f + i * 2.6f;
                    for (int j = 0; j < h; j++)
                    {
                        int y = top + j; if (y >= 36) break;
                        int hw = Mathf.Max(1, Mathf.RoundToInt((j + 1f) / h * maxW));
                        for (int x = cx - hw; x < cx + hw; x++)
                        {
                            string col = x < cx - hw / 3 ? P[3] : x < cx + hw / 3 ? P[2] : P[1];
                            if (j >= h - 2) col = P[0];
                            else if (Noise.Hash(x, y, 31 + v) < 0.12f) col = P[0];
                            if (si == 3 && (j < 3 || (j < h - 3 && Noise.Hash(x, y, 33) < 0.16f)) && x < cx + hw / 2) col = j < 2 ? SNOW : SNOW2;
                            g.D(x, y, col);
                        }
                    }
                }
                g.Outline(TREE_OL);
                return g;
            }, new Vector2(13f / 26f, 3f / 44f));
        }

        public static Sprite Sapling(int si)
        {
            return Cached("sapling" + si, () =>
            {
                var g = new PixelCanvas(12, 16);
                g.Ellipse(6, 13.5f, 3, 1, new Color32(0, 0, 0, 50), true);
                g.R(5, 7, 2, 7, "#7a4e2c");
                if (si == 3) { g.D(4, 6, "#7a4e2c"); g.D(7, 5, "#7a4e2c"); g.D(6, 4, SNOW); }
                else
                {
                    string a = si == 2 ? "#e0a032" : "#5aa83e", b = si == 2 ? "#c47a24" : "#3d7a2a", c = si == 2 ? "#f4c860" : "#86c85a";
                    g.Disc(6, 5, 3.6f, a); g.R(3, 6, 6, 2, b); g.D(4, 3, c); g.D(5, 3, c); g.D(3, 4, c);
                }
                g.Outline("#2a2216");
                return g;
            }, new Vector2(0.5f, 2f / 16f));
        }

        public static Sprite Stump(int si)
        {
            return Cached("stump" + si, () =>
            {
                var g = new PixelCanvas(14, 10);
                g.Ellipse(7, 8, 6, 1.5f, new Color32(0, 0, 0, 55), true);
                g.R(3, 3, 8, 5, "#7a4e2c"); g.R(8, 3, 3, 5, "#5e3a20");
                g.R(2, 6, 1, 2, "#7a4e2c"); g.R(11, 6, 1, 2, "#5e3a20");
                g.Ellipse(7, 3, 4, 1.6f, PixelCanvas.C(si == 3 ? SNOW : "#d8b07a"));
                g.D(7, 3, "#a8804a"); g.D(6, 3, "#c49a62");
                g.Outline("#3a2618");
                return g;
            }, new Vector2(0.5f, 2f / 10f));
        }

        public static Sprite Rock(int si, int v)
        {
            return Cached("rock" + si + v, () =>
            {
                var g = new PixelCanvas(18, 14);
                g.Ellipse(9, 11.5f, 7.5f, 1.6f, new Color32(0, 0, 0, 60), true);
                float rx = v == 1 ? 5.6f : 7f, ry = v == 1 ? 4f : 4.8f, cx = 9, cy = v == 1 ? 8f : 7.2f;
                string[] P = { "#5e5852", "#7e776e", "#a39c90", "#c8c1b4" };
                for (int y = 0; y < 14; y++)
                    for (int x = 0; x < 18; x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                        if (dx * dx + dy * dy + (Noise.Hash(x, y, v + 90) - 0.5f) * 0.2f >= 1) continue;
                        float s = -dx * 0.55f - dy * 0.9f + (Noise.Hash(x, y, 91) - 0.5f) * 0.3f;
                        string col = s > 0.55f ? P[3] : s > 0.05f ? P[2] : s > -0.45f ? P[1] : P[0];
                        if (si == 3 && dy < -0.25f) col = SNOW;
                        else if (si < 2 && dy < -0.3f && Noise.Hash(x, y, 93) < 0.22f) col = "#6a9a3a";
                        g.D(x, y, col);
                    }
                g.D((int)cx + 1, (int)cy, P[0]); g.D((int)cx + 2, (int)cy + 1, P[0]); g.D((int)cx - 2, (int)cy - 1, P[1]);
                g.Outline(ROCK_OL);
                return g;
            }, new Vector2(0.5f, 2.5f / 14f));
        }

        /// <summary>A chicken, pivot at the feet. Frame 1 is pecking.</summary>
        public static Sprite Chicken(int frame)
        {
            return Cached("chick" + frame, () =>
            {
                var g = new PixelCanvas(9, 8);
                g.RBlend(1, 7, 6, 1, new Color32(0, 0, 0, 60));
                g.R(1, 3, 5, 3, "#f4f1e6"); g.R(2, 2, 3, 1, "#f4f1e6"); g.D(1, 2, "#e4ded0"); g.R(2, 4, 3, 1, "#e0d8c8");
                g.D(3, 6, "#e8a23a"); g.D(5, 6, "#e8a23a");
                if (frame == 1) { g.R(6, 4, 2, 2, "#f4f1e6"); g.D(7, 3, "#d9534a"); g.D(8, 5, "#e8a23a"); g.D(7, 4, "#2b1d1a"); }
                else { g.R(5, 1, 2, 3, "#f4f1e6"); g.D(6, 0, "#d9534a"); g.D(7, 2, "#e8a23a"); g.D(6, 1, "#2b1d1a"); }
                g.Outline("#4a3a2a");
                return g;
            }, new Vector2(0.5f, 1f / 8f));
        }

        static Sprite white;
        /// <summary>A single white pixel, used for overlays, particles and outlines.</summary>
        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var g = new PixelCanvas(1, 1); g.Set(0, 0, new Color32(255, 255, 255, 255));
                    white = g.ToSprite("white", new Vector2(0f, 1f));
                }
                return white;
            }
        }

        /// <summary>Minimap and list dot colours by rank.</summary>
        public static readonly Dictionary<Rank, string> RankColor = new Dictionary<Rank, string>
        {
            { Rank.Serf, "#a89a86" }, { Rank.Freeman, "#e8dcc0" }, { Rank.Reeve, "#5a9a4a" }, { Rank.Priest, "#2e2a33" },
            { Rank.Knight, "#9a9ea6" }, { Rank.Noble, "#b05a82" }, { Rank.Lord, "#e0b04a" }
        };
    }
}
