using System.Collections.Generic;
using UnityEngine;

namespace MedievalTown.Game.UI
{
    /// <summary>
    /// A hand-drawn 5x7 pixel font (with descenders) rendered through IMGUI. Text is drawn glyph by glyph
    /// from a small atlas so it stays crisp at any whole-number UI scale.
    /// </summary>
    public static class PixelFont
    {
        public const int CellH = 9, Baseline = 7, LineH = 11;
        static readonly Dictionary<char, string> src = new Dictionary<char, string>
        {
            {'A', ".###.|#...#|#...#|#####|#...#|#...#|#...#"}, {'B', "####.|#...#|#...#|####.|#...#|#...#|####."},
            {'C', ".###.|#...#|#....|#....|#....|#...#|.###."}, {'D', "####.|#...#|#...#|#...#|#...#|#...#|####."},
            {'E', "#####|#....|#....|####.|#....|#....|#####"}, {'F', "#####|#....|#....|####.|#....|#....|#...."},
            {'G', ".###.|#...#|#....|#.###|#...#|#...#|.####"}, {'H', "#...#|#...#|#...#|#####|#...#|#...#|#...#"},
            {'I', "###|.#.|.#.|.#.|.#.|.#.|###"}, {'J', "..###|...#.|...#.|...#.|#..#.|#..#.|.##.."},
            {'K', "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#"}, {'L', "#....|#....|#....|#....|#....|#....|#####"},
            {'M', "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#"}, {'N', "#...#|##..#|#.#.#|#..##|#...#|#...#|#...#"},
            {'O', ".###.|#...#|#...#|#...#|#...#|#...#|.###."}, {'P', "####.|#...#|#...#|####.|#....|#....|#...."},
            {'Q', ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#"}, {'R', "####.|#...#|#...#|####.|#.#..|#..#.|#...#"},
            {'S', ".####|#....|#....|.###.|....#|....#|####."}, {'T', "#####|..#..|..#..|..#..|..#..|..#..|..#.."},
            {'U', "#...#|#...#|#...#|#...#|#...#|#...#|.###."}, {'V', "#...#|#...#|#...#|#...#|#...#|.#.#.|..#.."},
            {'W', "#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#."}, {'X', "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#"},
            {'Y', "#...#|#...#|.#.#.|..#..|..#..|..#..|..#.."}, {'Z', "#####|....#|...#.|..#..|.#...|#....|#####"},
            {'a', "....|....|.##.|...#|.###|#..#|.###"}, {'b', "#...|#...|###.|#..#|#..#|#..#|###."},
            {'c', "....|....|.###|#...|#...|#...|.###"}, {'d', "...#|...#|.###|#..#|#..#|#..#|.###"},
            {'e', "....|....|.##.|#..#|####|#...|.###"}, {'f', "..##|.#..|####|.#..|.#..|.#..|.#.."},
            {'g', "....|....|.###|#..#|#..#|#..#|.###|...#|.##."}, {'h', "#...|#...|###.|#..#|#..#|#..#|#..#"},
            {'i', ".#.|...|##.|.#.|.#.|.#.|###"}, {'j', "..#|...|..#|..#|..#|..#|..#|#.#|.#."},
            {'k', "#...|#...|#..#|#.#.|##..|#.#.|#..#"}, {'l', "##.|.#.|.#.|.#.|.#.|.#.|###"},
            {'m', ".....|.....|##.#.|#.#.#|#.#.#|#.#.#|#.#.#"}, {'n', "....|....|###.|#..#|#..#|#..#|#..#"},
            {'o', "....|....|.##.|#..#|#..#|#..#|.##."}, {'p', "....|....|###.|#..#|#..#|#..#|###.|#...|#..."},
            {'q', "....|....|.###|#..#|#..#|#..#|.###|...#|...#"}, {'r', "....|....|#.##|##..|#...|#...|#..."},
            {'s', "....|....|.###|#...|.##.|...#|###."}, {'t', ".#..|.#..|####|.#..|.#..|.#..|..##"},
            {'u', "....|....|#..#|#..#|#..#|#..#|.###"}, {'v', ".....|.....|#...#|#...#|.#.#.|.#.#.|..#.."},
            {'w', ".....|.....|#...#|#.#.#|#.#.#|#.#.#|.#.#."}, {'x', "....|....|#..#|#..#|.##.|#..#|#..#"},
            {'y', "....|....|#..#|#..#|#..#|#..#|.###|...#|.##."}, {'z', "....|....|####|...#|.##.|#...|####"},
            {'0', ".##.|#..#|#.##|##.#|#..#|#..#|.##."}, {'1', ".#.|##.|.#.|.#.|.#.|.#.|###"},
            {'2', ".##.|#..#|...#|..#.|.#..|#...|####"}, {'3', "###.|...#|...#|.##.|...#|...#|###."},
            {'4', "#..#|#..#|#..#|####|...#|...#|...#"}, {'5', "####|#...|#...|###.|...#|...#|###."},
            {'6', ".##.|#...|#...|###.|#..#|#..#|.##."}, {'7', "####|...#|..#.|.#..|.#..|.#..|.#.."},
            {'8', ".##.|#..#|#..#|.##.|#..#|#..#|.##."}, {'9', ".##.|#..#|#..#|.###|...#|...#|.##."},
            {'.', ".|.|.|.|.|.|#"}, {',', "..|..|..|..|..|.#|.#|#."}, {':', ".|.|#|.|.|#|."}, {';', "..|..|.#|..|..|.#|.#|#."},
            {'!', "#|#|#|#|#|.|#"}, {'?', ".##.|#..#|...#|..#.|.#..|....|.#.."}, {'\'', "#|#|.|.|.|.|."}, {'"', "#.#|#.#|...|...|...|...|..."},
            {'-', "....|....|....|####|....|....|...."}, {'+', ".....|..#..|..#..|#####|..#..|..#..|....."},
            {'/', "...#|...#|..#.|.##.|.#..|#...|#..."}, {'(', ".#|#.|#.|#.|#.|#.|.#"}, {')', "#.|.#|.#|.#|.#|.#|#."},
            {'$', "..#..|.####|#.#..|.###.|..#.#|####.|..#.."}, {'%', "##..#|##.#.|...#.|..#..|.#...|.#.##|#..##"},
            {'&', ".##..|#..#.|#.#..|.#...|#.#.#|#..#.|.##.#"}, {'#', ".#.#.|#####|.#.#.|.#.#.|#####|.#.#.|....."},
            {'=', "....|....|####|....|####|....|...."}, {'_', "....|....|....|....|....|....|####"},
            {'<', "...#|..#.|.#..|#...|.#..|..#.|...#"}, {'>', "#...|.#..|..#.|...#|..#.|.#..|#..."},
            {'*', ".....|#.#.#|.###.|#####|.###.|#.#.#|....."}, {'[', "##|#.|#.|#.|#.|#.|##"}, {']', "##|.#|.#|.#|.#|.#|##"},
            {'·', ".|.|.|#|.|.|."}, {'×', ".....|#...#|.#.#.|..#..|.#.#.|#...#|....."}, {'↑', "..#..|.###.|#.#.#|..#..|..#..|..#..|..#.."},
            {'↓', "..#..|..#..|..#..|..#..|#.#.#|.###.|..#.."}, {'♥', ".....|.#.#.|#####|#####|.###.|..#..|....."},
        };

        sealed class Glyph { public Rect Uv; public int W, H; }
        static readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
        static Texture2D atlas;

        static void Build()
        {
            if (atlas != null) return;
            int cols = 16, cell = 6;
            var keys = new List<char>(src.Keys);
            int rows = (keys.Count + cols - 1) / cols;
            int W = cols * cell, H = rows * CellH;
            atlas = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[W * H];
            for (int i = 0; i < keys.Count; i++)
            {
                var rowsTxt = src[keys[i]].Split('|');
                int gw = rowsTxt[0].Length, gx = (i % cols) * cell, gy = (i / cols) * CellH;
                for (int r = 0; r < rowsTxt.Length; r++)
                    for (int c = 0; c < rowsTxt[r].Length; c++)
                        if (rowsTxt[r][c] == '#') px[(H - 1 - (gy + r)) * W + gx + c] = new Color32(255, 255, 255, 255);
                glyphs[keys[i]] = new Glyph { W = gw, H = CellH, Uv = new Rect(gx / (float)W, 1f - (gy + CellH) / (float)H, gw / (float)W, CellH / (float)H) };
            }
            atlas.SetPixels32(px); atlas.Apply(false);
        }

        static char Map(char c)
        {
            switch (c)
            {
                case '—': case '–': return '-';
                case '’': case '‘': return '\'';
                case '“': case '”': return '"';
                case '★': return '*';
                case 'é': return 'e';
                case '…': return '.';
            }
            return c;
        }

        public static int CharWidth(char c)
        {
            Build();
            if (c == ' ') return 3;
            Glyph g; return glyphs.TryGetValue(Map(c), out g) ? g.W + 1 : 4;
        }

        public static int Width(string s, int scale = 1)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int w = 0; foreach (char c in s) w += CharWidth(c);
            return (w - 1) * scale;
        }

        /// <summary>Draws a single line; returns its width. Call from OnGUI.</summary>
        public static float Draw(float x, float y, string s, Color color, int scale = 1, bool shadow = false)
        {
            if (string.IsNullOrEmpty(s) || Event.current.type != EventType.Repaint) return Width(s, scale);
            Build();
            var old = GUI.color;
            if (shadow) { GUI.color = new Color(0, 0, 0, color.a * 0.35f); DrawRaw(x + scale, y + scale, s, scale); }
            GUI.color = color;
            float w = DrawRaw(x, y, s, scale);
            GUI.color = old;
            return w;
        }

        static float DrawRaw(float x, float y, string s, int scale)
        {
            float cx = x;
            foreach (char ch in s)
            {
                if (ch == ' ') { cx += 3 * scale; continue; }
                Glyph g;
                if (!glyphs.TryGetValue(Map(ch), out g)) { glyphs.TryGetValue('?', out g); }
                GUI.DrawTextureWithTexCoords(new Rect(cx, y, g.W * scale, g.H * scale), atlas, g.Uv);
                cx += (g.W + 1) * scale;
            }
            return cx - x;
        }

        /// <summary>Breaks text into lines no wider than maxW (in font pixels × scale).</summary>
        public static List<string> Wrap(string s, float maxW, int scale = 1)
        {
            var lines = new List<string>();
            foreach (var para in s.Split('\n'))
            {
                var words = para.Split(' ');
                string line = "";
                foreach (var w in words)
                {
                    string t = line.Length == 0 ? w : line + " " + w;
                    if (Width(t, scale) > maxW && line.Length > 0) { lines.Add(line); line = w; }
                    else line = t;
                }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Draws wrapped text; returns the height used.</summary>
        public static float DrawWrapped(Rect r, string s, Color color, int scale = 1)
        {
            var lines = Wrap(s, r.width, scale);
            float y = r.y;
            foreach (var l in lines) { Draw(r.x, y, l, color, scale); y += LineH * scale; }
            return y - r.y;
        }

        public static float WrappedHeight(string s, float width, int scale = 1) { return Wrap(s, width, scale).Count * LineH * scale; }
    }
}
