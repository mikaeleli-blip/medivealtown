using System.Collections.Generic;
using UnityEngine;

namespace MedievalTown.Game.UI
{
    public enum Icon { Food, Firewood, Logs, Stone, Tools, People, Child, House, Heart, Health, Coin, Warning, Grave, Book, Scales, Ballot, News, Hammer, Pointer, Axe, Sun, Moon, Snow, Leaf, Close, Plus, Minus, Play, Pause, Star, Chair, Wagon,
                       Meat, Bread, Iron, Wool, Hide, Leather, Clothes, Beer, Steer, Cross, Bath, Crown, Shield, Sheaf }

    /// <summary>
    /// A tiny immediate-mode UI kit on top of IMGUI: wooden frames, parchment panels, pixel text,
    /// buttons, sliders and icons. Everything is drawn in "UI pixels"; the HUD sets a whole-number scale.
    /// </summary>
    public static class Ui
    {
        // Palette: lamplit wood and parchment
        public static readonly Color Ink = Hex("#3b2618"), InkSoft = Hex("#7a5534"), Title = Hex("#5a2a10"),
            Good = Hex("#3a7a2a"), Bad = Hex("#b0301e"), Warn = Hex("#a86a10"), Cream = Hex("#fff4dc"), Gold = Hex("#f2cf4a"),
            Parch = Hex("#f6dcae"), ParchDark = Hex("#e6c48c"), Wood = Hex("#b8692c"), WoodDark = Hex("#7a3f18"), Sky = Hex("#5a86a8");

        public static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

        static Texture2D frame, inset, button, buttonHover, buttonOn, slot, slotOn, white, tooltip;
        static readonly Dictionary<Icon, Texture2D> icons = new Dictionary<Icon, Texture2D>();

        // ------------------------------------------------------------------ textures
        static Texture2D Tex(int w, int h, System.Func<int, int, Color32> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[(h - 1 - y) * w + x] = f(x, y);
            t.SetPixels32(px); t.Apply(false); return t;
        }
        static Color32 C(string h) { return Hex(h); }

        static void Build()
        {
            if (frame != null) return;
            white = Tex(1, 1, (x, y) => new Color32(255, 255, 255, 255));
            // 16x16, 6px border: dark outline, two-tone wood bevel, inner line, parchment
            frame = Tex(16, 16, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(15 - x, 15 - y));
                if (d == 0) return C("#3b2618");
                if (d <= 3) { bool lit = x < 15 - y; return d == 1 ? C(lit ? "#e08a48" : "#9a4e20") : C(lit ? "#c8743a" : "#8a4420"); }
                if (d == 4) return C("#5a2e12");
                if (d == 5) return C("#e6c48c");
                return C("#f6dcae");
            });
            inset = Tex(8, 8, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                if (d == 0) return C(x < 7 - y ? "#b8844a" : "#f8e6c4");
                return C("#ecc890");
            });
            System.Func<string, string, string, Texture2D> btn = (fill, lit, dark) => Tex(8, 8, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                if (d == 0) return C("#3b2618");
                if (d == 1) return C(x < 7 - y ? lit : dark);
                return C(fill);
            });
            button = btn("#e0a868", "#f4c88a", "#a8642c");
            buttonHover = btn("#f0bc7c", "#ffdca0", "#b8743a");
            buttonOn = btn("#7a9a4a", "#9aba62", "#4a6a2a");
            slot = Tex(8, 8, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                if (d == 0) return C("#3b2618");
                if (d == 1) return C(x < 7 - y ? "#8a4420" : "#e6a868");
                return C("#f0d098");
            });
            slotOn = Tex(8, 8, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                if (d == 0) return C("#3b2618");
                if (d == 1) return C("#f2cf4a");
                return C("#fff0c8");
            });
            tooltip = Tex(8, 8, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                if (d == 0) return C("#3b2618");
                if (d == 1) return C("#c8743a");
                return C("#fff0d0");
            });
            BuildIcons();
        }

        static void BuildIcons()
        {
            // 9x9 icons; '.' = clear, letters pick palette colors
            var pal = new Dictionary<char, string>
            {
                {'o', "#3b2618"}, {'b', "#a86a2a"}, {'B', "#d89a4a"}, {'y', "#f2cf4a"}, {'w', "#fff4dc"}, {'g', "#a39c90"}, {'G', "#6e6860"},
                {'r', "#d8443a"}, {'R', "#9a2a22"}, {'l', "#7cc04a"}, {'L', "#3f8a2e"}, {'s', "#f6d2b0"}, {'h', "#6b4428"}, {'k', "#5a86a8"}, {'K', "#c8e4f0"}, {'c', "#e8dcc4"}, {'p', "#b05a82"},
            };
            var art = new Dictionary<Icon, string>
            {
                {Icon.Food, "..ooooo..|.oBBBBBo.|oBBbBbBBo|oBbBBBbBo|obbbbbbbo|obbbbbbbo|.ooooooo.|.........|........."},
                {Icon.Firewood, ".........|..oo.oo..|.oBBoBBo.|.oBboBbo.|oooo.oooo|oBBoBBoBo|oBboBboBo|.oo.oo.o.|........."},
                {Icon.Logs, ".........|.ooooooo.|oBbbbbbo.|oyBbbbbbo|oByBbbbbo|oBBbbbbbo|.ooooooo.|.........|........."},
                {Icon.Stone, ".........|...ooo...|..ogggo..|.oggggGo.|ogggggGGo|oggGGGGGo|.ooooooo.|.........|........."},
                {Icon.Tools, "..ooo....|.oggGo...|.oGgGo...|..ooboo..|....obo..|.....obo.|......obo|.......oo|........."},
                {Icon.People, "...ooo...|..osssо..|..osssо..|...ooo...|..okkko..|.okkkkko.|.okkkkko.|.ooooooo.|........."},
                {Icon.Child, ".........|.........|...ooo...|..osso...|...oo....|..orrro..|..orrro..|..ooooo..|........."},
                {Icon.House, "....o....|...oRo...|..oRRRo..|.oRRRRRo.|ooooooooo|.oBBBBBo.|.oBohoBo.|.oBohoBo.|.ooooooo."},
                {Icon.Heart, ".........|.oo...oo.|orro.orro|orrrorrro|orrrrrrro|.orrrrro.|..orrro..|...oro...|....o...."},
                {Icon.Health, ".........|...ooo...|...olo...|.ooolooo.|.olllllo.|.ooolooo.|...olo...|...ooo...|........."},
                {Icon.Coin, "..ooooo..|.oyyyyyo.|oyyBBByyo|oyBByyyyo|oyyBByyyo|oyyyBByyo|oyBBByyyo|.oyyyyyo.|..ooooo.."},
                {Icon.Warning, "....o....|...oyo...|...oyo...|..oyoyo..|..oyoyo..|.oyyyyyo.|.oyyoyyo.|ooooooooo|........."},
                {Icon.Grave, "..ooooo..|.oggggGo.|.ogGGgGo.|.oggggGo.|.ogGGGGo.|.oggggGo.|.oggggGo.|ooooooooo|olllllllo"},
                {Icon.Book, ".........|.ooo.ooo.|obbbobbbo|obwbobwbo|obwbobwbo|obbbobbbo|.ooo.ooo.|....o....|........."},
                {Icon.Scales, "....o....|.ooooooo.|.o..o..o.|oyo.o.oyo|ooo.o.ooo|....o....|...ooo...|..ooooo..|........."},
                {Icon.Ballot, ".........|.ooooooo.|.owwwwwo.|.owoooWo.|ooooooooo|obbbbbbbo|obbbbbbbo|ooooooooo|........."},
                {Icon.News, ".ooooooo.|.owwwwwo.|.owooowo.|.owwwwwo.|.owoooo..|.owwwwwo.|.owoooo..|.owwwwwo.|.ooooooo."},
                {Icon.Hammer, ".ooooo...|oGgggGo..|.ooobo...|...obo...|...obo...|...obo...|...obo...|...ooo...|........."},
                {Icon.Pointer, "o........|oo.......|owo......|owwo.....|owwwo....|owwwwo...|owwoo....|oo.oo....|....o...."},
                {Icon.Axe, "....ooo..|...oggGo.|...ogGGo.|..oboooo.|..obo....|.obo.....|.obo.....|obo......|oo......."},
                {Icon.Sun, "....y....|.y.....y.|...yyy...|..yyyyy..|y.yyyyy.y|..yyyyy..|...yyy...|.y.....y.|....y...."},
                {Icon.Moon, ".........|...oooo..|..okkko..|.okkoo...|.okko....|.okkoo...|..okkko..|...oooo..|........."},
                {Icon.Snow, "....w....|.w..w..w.|..w.w.w..|...www...|wwwwwwwww|...www...|..w.w.w..|.w..w..w.|....w...."},
                {Icon.Leaf, ".......oo|.....ooLo|...oolLLo|..olllLo.|.olllLo..|.ollLo...|.oLoo....|ooo......|o........"},
                {Icon.Close, ".........|.oo...oo.|.ooo.ooo.|..ooooo..|...ooo...|..ooooo..|.ooo.ooo.|.oo...oo.|........."},
                {Icon.Plus, ".........|...ooo...|...ooo...|.ooooooo.|.ooooooo.|.ooooooo.|...ooo...|...ooo...|........."},
                {Icon.Minus, ".........|.........|.........|.ooooooo.|.ooooooo.|.ooooooo.|.........|.........|........."},
                {Icon.Play, ".........|..oo.....|..ooo....|..oooo...|..ooooo..|..oooo...|..ooo....|..oo.....|........."},
                {Icon.Pause, ".........|..oo.oo..|..oo.oo..|..oo.oo..|..oo.oo..|..oo.oo..|..oo.oo..|..oo.oo..|........."},
                {Icon.Star, "....o....|...oyo...|ooooyoooo|oyyyyyyyo|.oyyyyyo.|..oyyyo..|.oyyoyyo.|.oo...oo.|........."},
                {Icon.Chair, ".oo......|.oBo.....|.oBo.....|.oBo.....|.oBoooooo|.oBBBBBBo|.oBooooBo|.oBo..oBo|.ooo..ooo"},
                {Icon.Wagon, "..ooooo..|.owwwwwo.|owwwwwwwo|owwcwcwwo|ooooooooo|obbbbbbbo|ooooooooo|.ogo.ogo.|..o...o.."},
                {Icon.Meat, ".........|..ooooo..|.orrrrro.|orrRrrrro|orRRrrrro|orrrrrro.|.orrrrooo|..ooo.owo|......oo."},
                {Icon.Bread, ".........|.........|..ooooo..|.oBBBBBo.|oBbBbBbBo|oBBBBBBBo|obbbbbbbo|.ooooooo.|........."},
                {Icon.Iron, ".........|.........|..ooooooo|.oGgggGo.|oGgggGoo.|oooooooGo|oGgggGooo|ooooooo..|........."},
                {Icon.Wool, ".........|..ooooo..|.owwcwwo.|owcwwwcwo|owwwcwwwo|owcwwwcwo|.owwwwwo.|..ooooo..|........."},
                {Icon.Hide, ".o.....o.|oBo...oBo|.oBBBBBo.|..oBBBo..|..oBbBo..|.oBBBBBo.|oBo...oBo|.o.....o.|........."},
                {Icon.Leather, ".........|.oooooo..|obbbbbbo.|obhhhhbo.|obbbbbboo|.oooooobo|....obbbo|.....ooo.|........."},
                {Icon.Clothes, ".oo...oo.|okkoookko|okkkkkkko|.ookkkoo.|..okkko..|..okkko..|..okkko..|..ooooo..|........."},
                {Icon.Beer, ".........|.ooooo...|.owwwwoo.|.oyyyyoyo|.oyyyyoyo|.oyyyyoo.|.oyyyyo..|.oooooo..|........."},
                {Icon.Steer, "w.......w|ow.ooo.wo|.oobbboo.|..obbbo..|..obobo..|..obbbo..|...ohho..|....oo...|........."},
                {Icon.Cross, "...ooo...|...oyo...|.oooyooo.|.oyyyyyo.|.oooyooo.|...oyo...|...oyo...|...ooo...|........."},
 {Icon.Crown, ".........|o.o.o.o.o|oyoyoyoyo|oyyyyyyyo|oyryyyryo|oyyyyyyyo|ooooooooo|.........|........."},
                {Icon.Shield, "ooooooooo|orrrwrrro|orrrwrrro|owwwwwwwo|orrrwrrro|.orrwrro.|..orwro..|...ooo...|........."},
                {Icon.Sheaf, "..o.o.o..|.oyoyoyo.|..oyyyo..|...oyo...|...obo...|..ooooo..|..oyyyo..|.oyoyoyo.|.oo.o.oo."},
                {Icon.Bath, ".......o.|......oko|.......o.|ooooooooo|okkkkkkko|ogggggggo|.ooooooo.|.o.....o.|........."},
            };
            foreach (var kv in art)
            {
                var rows = kv.Value.Split('|');
                icons[kv.Key] = Tex(9, 9, (x, y) =>
                {
                    char ch = y < rows.Length && x < rows[y].Length ? rows[y][x] : '.';
                    if (ch == 'о') ch = 'o';
                    string col; return pal.TryGetValue(ch, out col) ? C(col) : new Color32(0, 0, 0, 0);
                });
            }
        }

        // ------------------------------------------------------------------ drawing
        public static bool Repaint { get { return Event.current.type == EventType.Repaint; } }

        public static void Slice(Rect r, Texture2D t, int border)
        {
            if (!Repaint) return;
            float w = t.width, h = t.height, b = border;
            float[] xs = { r.x, r.x + b, r.xMax - b, r.xMax };
            float[] ys = { r.y, r.y + b, r.yMax - b, r.yMax };
            float[] us = { 0, b / w, 1 - b / w, 1 };
            float[] vs = { 1, 1 - b / h, b / h, 0 };
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    var dst = Rect.MinMaxRect(xs[i], ys[j], xs[i + 1], ys[j + 1]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    var uv = Rect.MinMaxRect(us[i], vs[j + 1], us[i + 1], vs[j]);
                    GUI.DrawTextureWithTexCoords(dst, t, uv);
                }
        }

        public static void Panel(Rect r) { Build(); Slice(r, frame, 6); }
        public static void Inset(Rect r) { Build(); Slice(r, inset, 2); }
        public static void Tip(Rect r) { Build(); Slice(r, tooltip, 2); }
        public static void SlotBox(Rect r, bool on) { Build(); Slice(r, on ? slotOn : slot, 2); }

        public static void Fill(Rect r, Color c)
        {
            if (!Repaint) return; Build();
            var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o;
        }

        public static void Line(float x, float y, float w, Color c) { Fill(new Rect(x, y, w, 1), c); }

        public static void IconAt(float x, float y, Icon i, float alpha = 1f)
        {
            if (!Repaint) return; Build();
            var o = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(x, y, 9, 9), icons[i]);
            GUI.color = o;
        }

        public static void Texture(Rect r, Texture t) { if (Repaint && t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit); }

        public enum Align { Left, Center, Right }

        public static float Text(float x, float y, string s, Color c, Align a = Align.Left, bool shadow = false)
        {
            float w = PixelFont.Width(s);
            if (a == Align.Center) x -= w / 2f; else if (a == Align.Right) x -= w;
            PixelFont.Draw(Mathf.Round(x), Mathf.Round(y), s, c, 1, shadow);
            return w;
        }
        public static float Text(float x, float y, string s) { return Text(x, y, s, Ink); }

        public static float Wrapped(Rect r, string s, Color c) { return PixelFont.DrawWrapped(r, s, c); }
        public static float WrappedHeight(string s, float w) { return PixelFont.WrappedHeight(s, w); }

        public static bool Hover(Rect r) { return r.Contains(Event.current.mousePosition); }

        /// <summary>A wooden button. Returns true when clicked.</summary>
        public static bool Button(Rect r, string label, bool on = false, bool enabled = true, Icon? icon = null)
        {
            Build();
            bool hover = enabled && Hover(r);
            Slice(r, on ? buttonOn : hover ? buttonHover : button, 2);
            float cx = r.x + r.width / 2, cy = r.y + (r.height - 7) / 2f;
            Color tc = on ? Cream : enabled ? Ink : InkSoft;
            if (icon.HasValue && string.IsNullOrEmpty(label)) IconAt(Mathf.Round(cx - 4.5f), Mathf.Round(r.y + (r.height - 9) / 2f), icon.Value, enabled ? 1 : 0.5f);
            else if (icon.HasValue)
            {
                float w = 11 + PixelFont.Width(label);
                IconAt(Mathf.Round(cx - w / 2), Mathf.Round(r.y + (r.height - 9) / 2f), icon.Value, enabled ? 1 : 0.5f);
                Text(cx - w / 2 + 11, cy, label, tc);
            }
            else Text(cx, cy, label, tc, Align.Center);
            if (!enabled) return false;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static bool Clickable(Rect r) { return GUI.Button(r, GUIContent.none, GUIStyle.none); }

        static GUIStyle sliderBg, sliderThumb;
        /// <summary>A brass slider. Value snaps to step.</summary>
        public static float Slider(Rect r, float v, float min, float max, float step)
        {
            Build();
            if (sliderBg == null)
            {
                sliderBg = new GUIStyle { fixedHeight = 0 };
                sliderThumb = new GUIStyle { fixedWidth = 8, fixedHeight = 0 };
            }
            sliderThumb.fixedHeight = r.height;
            float t = Mathf.InverseLerp(min, max, v);
            Fill(new Rect(r.x, r.y + r.height / 2 - 2, r.width, 4), Hex("#3b2618"));
            Fill(new Rect(r.x + 1, r.y + r.height / 2 - 1, r.width - 2, 2), Hex("#a8642c"));
            Fill(new Rect(r.x + 1, r.y + r.height / 2 - 1, (r.width - 2) * t, 2), Hex("#f2cf4a"));
            float tx = r.x + (r.width - 8) * t;
            Fill(new Rect(tx, r.y, 8, r.height), Hex("#3b2618"));
            Fill(new Rect(tx + 1, r.y + 1, 6, r.height - 2), Hover(new Rect(tx, r.y, 8, r.height)) ? Hex("#ffe08a") : Hex("#e0a868"));
            Fill(new Rect(tx + 1, r.y + 1, 6, 1), Hex("#fff0c8"));
            float nv = GUI.HorizontalSlider(r, v, min, max, sliderBg, sliderThumb);
            return Mathf.Clamp(Mathf.Round(nv / step) * step, min, max);
        }

        public static bool Toggle(Rect r, bool v, string label)
        {
            Build();
            var box = new Rect(r.x, r.y + (r.height - 9) / 2, 9, 9);
            Fill(box, Hex("#3b2618")); Fill(new Rect(box.x + 1, box.y + 1, 7, 7), Hex("#fff0c8"));
            if (v) { Fill(new Rect(box.x + 2, box.y + 4, 2, 2), Good); Fill(new Rect(box.x + 3, box.y + 5, 2, 2), Good); Fill(new Rect(box.x + 4, box.y + 3, 2, 2), Good); Fill(new Rect(box.x + 5, box.y + 2, 2, 2), Good); }
            Text(r.x + 13, r.y + (r.height - 7) / 2, label, Ink);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) return !v;
            return v;
        }

        public static void Bar(Rect r, float frac, Color c)
        {
            Fill(r, Hex("#3b2618"));
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), Hex("#c8a070"));
            Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(frac), r.height - 2), c);
        }

        public static Color Mood(float v) { return v < 35 ? Bad : v < 55 ? Warn : Good; }
    }
}
