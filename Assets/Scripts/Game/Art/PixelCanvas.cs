using System.Collections.Generic;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>
    /// A pixel surface you draw on with top-left coordinates (like the HTML prototype's canvas),
    /// stored bottom-left so it uploads straight into a Texture2D.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public PixelCanvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }

        static readonly Dictionary<string, Color32> cache = new Dictionary<string, Color32>();
        /// <summary>Parses "#rrggbb" (cached).</summary>
        public static Color32 C(string hex)
        {
            Color32 c;
            if (cache.TryGetValue(hex, out c)) return c;
            Color parsed;
            ColorUtility.TryParseHtmlString(hex, out parsed);
            c = parsed; cache[hex] = c;
            return c;
        }
        public static Color32 C(string hex, byte alpha) { var c = C(hex); c.a = alpha; return c; }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[(H - 1 - y) * W + x] = c;
        }
        public Color32 Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return default(Color32);
            return Px[(H - 1 - y) * W + x];
        }
        /// <summary>Alpha-blends c over the existing pixel.</summary>
        public void Blend(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = (H - 1 - y) * W + x;
            var d = Px[i];
            float a = c.a / 255f, ia = 1 - a, da = d.a / 255f;
            float oa = a + da * ia;
            if (oa <= 0) { Px[i] = default(Color32); return; }
            Px[i] = new Color32(
                (byte)((c.r * a + d.r * da * ia) / oa),
                (byte)((c.g * a + d.g * da * ia) / oa),
                (byte)((c.b * a + d.b * da * ia) / oa),
                (byte)(oa * 255));
        }

        public void D(int x, int y, string hex) { Set(x, y, C(hex)); }
        public void R(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++) Set(xx, yy, c);
        }
        public void R(int x, int y, int w, int h, string hex) { R(x, y, w, h, C(hex)); }
        public void RBlend(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++) Blend(xx, yy, c);
        }
        public void Clear(int x, int y, int w, int h) { R(x, y, w, h, default(Color32)); }
        public void Line(int x0, int y0, int x1, int y1, string hex)
        {
            int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (n == 0) n = 1;
            for (int i = 0; i <= n; i++)
                D(Mathf.RoundToInt(x0 + (x1 - x0) * (float)i / n), Mathf.RoundToInt(y0 + (y1 - y0) * (float)i / n), hex);
        }
        public void Draw(PixelCanvas src, int dx, int dy)
        {
            for (int y = 0; y < src.H; y++)
                for (int x = 0; x < src.W; x++)
                {
                    var c = src.Get(x, y);
                    if (c.a == 255) Set(dx + x, dy + y, c);
                    else if (c.a > 0) Blend(dx + x, dy + y, c);
                }
        }
        public bool IsOpaque(int x, int y) { return Get(x, y).a > 0; }
        bool Solid(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H && Get(x, y).a == 255; }

        /// <summary>Stardew-style outline: every empty pixel touching a solid pixel becomes the outline color.
        /// Semi-transparent pixels (shadows) don't count as solid.</summary>
        public void Outline(Color32 col, bool corners = false)
        {
            var mark = new List<int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (Get(x, y).a == 255) continue;
                    bool edge = Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1);
                    if (!edge && corners) edge = Solid(x - 1, y - 1) || Solid(x + 1, y - 1) || Solid(x - 1, y + 1) || Solid(x + 1, y + 1);
                    if (edge) mark.Add(y * W + x);
                }
            foreach (int i in mark) Set(i % W, i / W, col);
        }
        public void Outline(string hex, bool corners = false) { Outline(C(hex), corners); }

        /// <summary>Darkens solid pixels on the inside edge of a shape (gives a soft rim).</summary>
        public void Recolor(string fromHex, string toHex)
        {
            var f = C(fromHex); var t = C(toHex);
            for (int i = 0; i < Px.Length; i++) if (Px[i].r == f.r && Px[i].g == f.g && Px[i].b == f.b && Px[i].a == f.a) Px[i] = t;
        }

        public PixelCanvas Mirrored()
        {
            var m = new PixelCanvas(W, H);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m.Set(W - 1 - x, y, Get(x, y));
            return m;
        }

        public void Disc(float cx, float cy, float r, string hex)
        {
            var c = C(hex);
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) Set(x, y, c);
                }
        }
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c, bool blend = false)
        {
            for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1) { if (blend) Blend(x, y, c); else Set(x, y, c); }
                }
        }

        public Sprite ToSprite(string name, Vector2 pivot)
        {
            var t = ToTexture(name);
            var s = Sprite.Create(t, new Rect(0, 0, W, H), pivot, Pixels.PerUnit, 0, SpriteMeshType.FullRect);
            if (name != null) s.name = name;
            return s;
        }

        public Texture2D ToTexture(string name = null)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            if (name != null) t.name = name;
            t.SetPixels32(Px);
            t.Apply(false, false);
            return t;
        }

        /// <summary>Sprite with its pivot at the top-left corner, 16 pixels per world unit (one map tile).</summary>
        public Sprite ToSprite(string name = null)
        {
            var t = ToTexture(name);
            var s = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0f, 1f), Pixels.PerUnit, 0, SpriteMeshType.FullRect);
            if (name != null) s.name = name;
            return s;
        }
    }

    public static class Pixels
    {
        public const int PerUnit = 16;
        /// <summary>Converts prototype pixel coordinates (y down) into world units (y up).</summary>
        public static Vector3 ToWorld(float px, float py, float z = 0f) { return new Vector3(px / PerUnit, -py / PerUnit, z); }
        public static Vector2 ToPixel(Vector3 world) { return new Vector2(world.x * PerUnit, -world.y * PerUnit); }
    }
}
