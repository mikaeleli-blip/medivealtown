using System.Collections.Generic;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>Everything the renderer needs to show one building in one season.</summary>
    public sealed class BuildingSprites
    {
        public Sprite[] Base = new Sprite[2];  // ground layer (gardens, yards, lawns); two sway frames
        public Sprite Top;                     // the building itself, y-sorted with people and trees
        public Sprite Lights;                  // lit windows, faded in at night
        public PixelCanvas TopCanvas;          // kept for pixel-accurate click picking and UI icons
        public Texture2D TopTexture;
        public int Lift;                       // pixels the sprite rises above its footprint
        public int W, H;
        public readonly List<Vector2> Smoke = new List<Vector2>();       // chimney tops, sprite pixels
        public readonly List<Vector2Int> Chickens = new List<Vector2Int>();
        public RectInt Forge;
        public bool HasForge;
        public Vector2Int PileAt = new Vector2Int(-1, -1);              // where the goods pile sits (sprite pixels, bottom-left)
        public PixelCanvas BaseCanvas;         // ground layer, for construction sites of flat buildings
        public bool HasDoor;
        public RectInt Door;                   // the doorway, sprite pixels
        public Sprite DoorOpen, DoorOpenLit;   // overlays: the door swung open on a dark or lamplit room
        public readonly List<Vector2Int> Herd = new List<Vector2Int>();   // where cattle and sheep graze (footprint pixels)
        public Sprite[] Sails;                 // a windmill's sails, four frames through a quarter turn (pivot at the hub)
        public Vector2 SailHub;                // the hub, sprite pixels
        public Vector2 BannerAt = new Vector2(-1, -1);   // where a pennant flies (sprite pixels), drawn by the renderer
    }

    /// <summary>
    /// Procedural 3/4-view buildings in a warm, chunky farm-sim style: dark outlines, plank walls,
    /// scalloped shingles, window boxes. Generated once per (type, variant, season) and cached.
    /// </summary>
    public static partial class BuildingArt
    {
        public const string OL = "#3b2618";
        const string SNOW = "#f4f8fa", SNOW2 = "#d4e2ea";

        public static int LiftOf(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.Cottage: return 8;
                case BuildingType.FreeHouse: return 18;
                case BuildingType.Keep: return 46;
                case BuildingType.Church: return 38;
                case BuildingType.Granary: return 16;
                case BuildingType.Woodcutter: return 10;
                case BuildingType.Quarry: return 10;
                case BuildingType.Mill: return 40;
                case BuildingType.Alehouse: return 16;
                case BuildingType.Pavilion: return 18;
                case BuildingType.Cart: return 8;
                default: return 0;
            }
        }

        public static int VariantOf(Building b) { return b.Type == BuildingType.Cottage || b.Type == BuildingType.FreeHouse ? b.Id % 3 : 0; }

        static readonly Dictionary<int, BuildingSprites> cache = new Dictionary<int, BuildingSprites>();
        public static BuildingSprites Get(BuildingType type, int variant, int season)
        {
            int key = ((int)type * 16 + variant) * 4 + season;
            BuildingSprites s;
            if (!cache.TryGetValue(key, out s)) { s = Make(type, variant, season); cache[key] = s; }
            return s;
        }

        // ================================================================== kit
        sealed class Ctx
        {
            public PixelCanvas Bg, Tg;
            public BuildingSprites M;
            public int Si, V, L, W, H;
            public List<RectInt> Wins = new List<RectInt>();
            public List<RectInt> Glow = new List<RectInt>();
            public List<CropRow> Crops = new List<CropRow>();
            public Sprite[] Sails;
            public bool Winter { get { return Si == 3; } }
            public int Y(int f) { return f + L; }
        }
        struct CropRow { public int X, Y, W; }

        static float Hs(int x, int y, int s = 99) { return Noise.Hash(x, y, s); }

        /// <summary>Horizontal plank wall.</summary>
        static void Planks(PixelCanvas g, int x, int y, int w, int h, string b, string dark, string light)
        {
            g.R(x, y, w, h, b);
            for (int r = y; r < y + h; r += 3)
            {
                g.R(x, r, w, 1, light);
                if (r + 2 < y + h) g.R(x, r + 2, w, 1, dark);
                int off = ((r - y) / 3) % 2 == 0 ? 3 : 8;
                for (int xx = x + off; xx < x + w; xx += 10) { g.D(xx, r, dark); g.D(xx, r + 1, dark); }
            }
        }

        /// <summary>Scalloped shingle roof seen from the front slope.</summary>
        static void Shingles(PixelCanvas g, int x, int y, int w, int h, string[] c, bool winter)
        {
            g.R(x, y, w, h, c[0]);
            for (int r = 0; r < h; r += 3)
            {
                int yy = y + r, off = (r / 3) % 2 == 0 ? 0 : 2;
                for (int xx = x + off; xx < x + w; xx += 4)
                {
                    g.D(xx, yy + 2, c[1]); g.D(xx + 3, yy + 2, c[1]);
                    g.D(xx + 1, yy, c[2]);
                }
                g.R(x, yy + 2, w, 1, c[1]);
                for (int xx = x + off + 1; xx < x + w; xx += 4) g.R(xx, yy + 2, 2, 1, c[0]);
            }
            g.R(x, y, w, 1, c[2]);
            g.R(x, y + h - 1, w, 1, c[1]);
            if (winter)
            {
                g.R(x, y, w, Mathf.Min(h - 2, 4), SNOW);
                for (int xx = x; xx < x + w; xx++) if (Hs(xx, y) < 0.5f) g.D(xx, y + Mathf.Min(h - 2, 4), SNOW2);
                for (int xx = x; xx < x + w; xx++) if (Hs(xx, y, 7) < 0.25f) g.D(xx, y + Mathf.Min(h - 2, 4) + 1, SNOW2);
            }
        }

        /// <summary>Triangular gable end (barns, warehouses).</summary>
        static void Gable(PixelCanvas g, int cx, int top, int halfW, int h, string wall, string trim, string[] roof, bool winter)
        {
            for (int r = 0; r < h; r++)
            {
                int hw = Mathf.RoundToInt((r + 1f) / h * halfW);
                g.R(cx - hw, top + r, hw * 2, 1, wall);
                g.D(cx - hw - 1, top + r, roof[1]); g.D(cx - hw - 2, top + r, roof[0]);
                g.D(cx + hw, top + r, roof[1]); g.D(cx + hw + 1, top + r, roof[0]);
                if (winter) { g.D(cx - hw - 2, top + r, SNOW); g.D(cx + hw + 1, top + r, SNOW); }
            }
            g.R(cx - 1, top - 1, 2, 2, roof[0]);
        }

        static void Window(Ctx k, int x, int y, int w, int h, bool box = true, string frame = "#efe2c4")
        {
            var g = k.Tg;
            g.R(x - 1, y - 1, w + 2, h + 2, frame);
            g.R(x, y, w, h, "#5a86a8");
            g.R(x, y + h / 2, w, (h + 1) / 2, "#4a7090");
            g.D(x, y, "#c8e4f0"); if (w > 2) g.D(x + 1, y, "#9ac4dc");
            if (w >= 4) g.R(x + w / 2, y, 1, h, frame);
            if (h >= 4) g.R(x, y + h / 2, w, 1, frame);
            k.Wins.Add(new RectInt(x, y, w, h));
            if (box)
            {
                g.R(x - 1, y + h + 1, w + 2, 2, "#8a5a34");
                if (!k.Winter)
                    for (int i = 0; i < w + 2; i += 2)
                    {
                        g.D(x - 1 + i, y + h, "#5a9a3a");
                        if (k.Si < 2) g.D(x - 1 + i + (i % 4 == 0 ? 0 : 1), y + h, i % 4 == 0 ? "#e85a6a" : "#f2cf4a");
                    }
                else g.R(x - 1, y + h, w + 2, 1, SNOW);
            }
        }

        static void Door(Ctx k, int x, int y, int w, int h, string wood = "#8a5a34")
        {
            var g = k.Tg;
            k.M.HasDoor = true; k.M.Door = new RectInt(x, y, w, h);
            g.R(x - 1, y - 1, w + 2, h + 1, "#5a3a22");
            g.R(x, y, w, h, wood);
            g.R(x + 1, y + 1, w - 2, h / 2 - 1, "#6e4428");
            g.R(x + 1, y + h / 2 + 1, w - 2, h / 2 - 2, "#6e4428");
            g.D(x + w - 2, y + h / 2, "#f2cf4a");
            g.R(x - 1, y + h, w + 2, 1, "#a39c90");
        }

        static void Foundation(PixelCanvas g, int x, int y, int w)
        {
            g.R(x, y, w, 2, "#8d877d");
            for (int xx = x; xx < x + w; xx += 4) { g.D(xx, y, "#6e6860"); g.D(xx + 2, y + 1, "#6e6860"); }
            g.R(x, y, w, 1, "#a39c90");
        }

        static void Chimney(Ctx k, int x, int y, int h)
        {
            var g = k.Tg;
            g.R(x, y, 5, h, "#8a7e72");
            for (int r = y + 1; r < y + h; r += 2) { g.D(x + 1 + (r % 4 == 1 ? 0 : 2), r, "#6e645a"); }
            g.R(x + 3, y, 2, h, "#6e645a");
            g.R(x - 1, y, 7, 2, "#5e554c");
            if (k.Winter) g.R(x - 1, y - 1, 7, 1, SNOW);
            k.M.Smoke.Add(new Vector2(x + 2.5f, y - 1));
        }

        static void Letters(PixelCanvas g, int x, int y, int w, string c)
        {
            for (int i = 0; i + 3 <= w; i += 4)
                for (int yy = 0; yy < 3; yy++)
                    for (int xx = 0; xx < 3; xx++)
                        if (Noise.Hash(x + i + xx, y + yy, i) < 0.55f) g.D(x + i + xx, y + yy, c);
        }

        static void Star5(PixelCanvas g, int x, int y, string c)
        {
            string[] rows = { "..x..", "xxxxx", ".xxx.", ".x.x.", "x...x" };
            for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++) if (rows[j][i] == 'x') g.D(x + i, y + j, c);
        }

        static void Barrel(PixelCanvas g, int x, int y)
        {
            g.R(x, y, 5, 6, "#8a5a34"); g.R(x + 3, y, 2, 6, "#6e4428");
            g.R(x, y + 1, 5, 1, "#5a5a5a"); g.R(x, y + 4, 5, 1, "#5a5a5a");
            g.R(x + 1, y - 1, 3, 1, "#a8734a");
        }
        static void Crate(PixelCanvas g, int x, int y, int s = 6)
        {
            g.R(x, y, s, s, "#c49a62"); g.R(x, y, s, 1, "#d8b07a"); g.R(x + s - 1, y, 1, s, "#a07a48");
            g.Line(x, y, x + s - 1, y + s - 1, "#a07a48");
        }
        static void Shadow(PixelCanvas g, int x, int y, int w) { g.RBlend(x, y, w, 2, new Color32(0, 0, 0, 55)); }

        // ================================================================== build
        static BuildingSprites Make(BuildingType type, int v, int si)
        {
            var d = Defs.Get(type);
            int L = LiftOf(type), W = d.W * 16, H = d.H * 16 + L;
            var m = new BuildingSprites { Lift = L, W = W, H = H };
            var k = new Ctx { Bg = new PixelCanvas(W, H), Tg = new PixelCanvas(W, H), M = m, Si = si, V = v, L = L, W = W, H = H };
            switch (type)
            {
                case BuildingType.Cottage: Cottage(k); break;
                case BuildingType.FreeHouse: FreeHouse(k); break;
                case BuildingType.Keep: Keep(k); break;
                case BuildingType.Church: Church(k); break;
                case BuildingType.Granary: Granary(k); break;
                case BuildingType.Strip: Strip(k); break;
                case BuildingType.Demesne: Demesne(k); break;
                case BuildingType.Woodcutter: Woodward(k); break;
                case BuildingType.Quarry: Quarry(k); break;
                case BuildingType.Mill: Mill(k); break;
                case BuildingType.Alehouse: Alehouse(k); break;
                case BuildingType.Pavilion: Pavilion(k); break;
                case BuildingType.Cart: Wagon(k); break;
            }
            k.Tg.Outline(OL);
            string n = type + "_" + v + "_" + si;
            if (k.Sails != null) m.Sails = k.Sails;
            for (int f = 0; f < 2; f++)
            {
                var b = new PixelCanvas(W, H);
                b.Draw(k.Bg, 0, 0);
                foreach (var row in k.Crops) GardenRow(b, row, si, f);
                m.Base[f] = b.ToSprite(n + "_base" + f);
                if (f == 0) m.BaseCanvas = b;
            }
            if (m.HasDoor) { m.DoorOpen = OpenDoor(m, false, n); m.DoorOpenLit = OpenDoor(m, true, n); }
            m.Top = k.Tg.ToSprite(n + "_top");
            m.TopTexture = m.Top.texture;
            m.TopCanvas = k.Tg;
            var lights = new PixelCanvas(W, H);
            var halo = PixelCanvas.C("#ffbe5a", 60);
            var rects = new List<RectInt>(k.Wins); rects.AddRange(k.Glow);
            foreach (var r in rects) lights.RBlend(r.x - 2, r.y - 2, r.width + 4, r.height + 4, halo);
            foreach (var r in rects) { lights.R(r.x, r.y, r.width, r.height, "#ffd27a"); lights.D(r.x, r.y, "#fff0c0"); }
            if (m.HasForge) lights.RBlend(m.Forge.x - 4, m.Forge.y - 4, m.Forge.width + 8, m.Forge.height + 8, PixelCanvas.C("#ff8c32", 80));
            m.Lights = lights.ToSprite(n + "_lights");
            return m;
        }

        /// <summary>The door swung in against the left jamb, showing the room behind.</summary>
        static Sprite OpenDoor(BuildingSprites m, bool lit, string n)
        {
            var g = new PixelCanvas(m.W, m.H); var r = m.Door;
            g.R(r.x, r.y, r.width, r.height, lit ? "#f0a848" : "#24160e");
            g.R(r.x, r.y, r.width, Mathf.Max(1, r.height / 3), lit ? "#d88838" : "#1a0f0a");
            g.R(r.x, r.y + r.height - 2, r.width, 2, lit ? "#ffd27a" : "#3a2418");     // floorboards catch the light
            if (lit) g.R(r.x + r.width / 2, r.y + r.height / 3, Mathf.Max(1, r.width / 3), r.height / 3, "#ffe2a0");
            g.R(r.x, r.y, Mathf.Min(2, r.width - 1), r.height, "#6e4428");                 // the door leaf, edge-on
            g.R(r.x, r.y, 1, r.height, "#8a5a34");
            return g.ToSprite(n + (lit ? "_doorlit" : "_door"));
        }

        /// <summary>Vegetable rows in a house garden.</summary>
        static void GardenRow(PixelCanvas g, CropRow r, int si, int frame)
        {
            for (int i = 0; i < r.W; i += 3)
            {
                int x = r.X + i, y = r.Y;
                float h = Noise.Hash(x, y, 4);
                int sw = (x / 3 + frame) % 2;
                if (si == 3) { if (h < 0.3f) g.D(x, y, "#9a8466"); continue; }
                if (si == 0) { g.D(x, y, "#4a8a2e"); if (h < 0.6f) g.D(x + sw, y - 1, "#7cc04a"); continue; }
                if (si == 1)
                {
                    g.R(x, y - 1, 2, 2, "#3f7a32"); g.D(x + sw, y - 2, "#5aa83e");
                    if (h < 0.3f) g.D(x + 1, y, "#e84a3a"); else if (h < 0.5f) g.D(x, y, "#f2cf4a");
                }
                else
                {
                    g.R(x, y - 1, 2, 2, "#7a7a32");
                    if (h < 0.45f) { g.R(x, y, 2, 1, "#e07a2a"); g.D(x, y - 1, "#f09a3a"); }
                }
            }
        }

        static void Wagon(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.Ellipse(16, y0 + 26, 15, 5, PixelCanvas.C(W ? "#d8d2c6" : "#a08452"));
            Shadow(tg, 3, y0 + 24, 26);
            // canvas cover: three hoops
            for (int r = 0; r < 12; r++)
            {
                int hw = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((11 - r) / 12f, 2))) * 12);
                tg.R(16 - hw, y0 + 2 + r, hw * 2, 1, r < 2 ? "#fffaf0" : "#efe6d2");
            }
            tg.R(4, y0 + 13, 24, 1, "#d6ccb6");
            for (int x = 8; x < 26; x += 6) tg.R(x, y0 + 3, 1, 11, "#d6ccb6");
            if (W) tg.R(8, y0 + 2, 16, 2, SNOW);
            // wooden bed
            tg.R(3, y0 + 14, 26, 6, "#8a5a34"); tg.R(3, y0 + 14, 26, 1, "#b8844a"); tg.R(3, y0 + 17, 26, 1, "#6e4428");
            // wheels
            foreach (int wx in new[] { 8, 24 })
            {
                tg.Disc(wx, y0 + 22, 4.5f, "#5a3a22"); tg.Disc(wx, y0 + 22, 3.2f, "#b8844a"); tg.Disc(wx, y0 + 22, 1.2f, "#5a3a22");
                tg.Line(wx - 3, y0 + 22, wx + 3, y0 + 22, "#5a3a22"); tg.Line(wx, y0 + 19, wx, y0 + 25, "#5a3a22");
            }
            // tongue
            tg.Line(28, y0 + 18, 31, y0 + 21, "#6e4428");
            Crate(tg, 0, y0 + 24, 5); Barrel(tg, 27, y0 + 24);
            k.M.PileAt = new Vector2Int(1, y0 + 31);
        }

        // ================================================================== construction sites
        /// <summary>A construction site at one step of progress.</summary>
        public sealed class SiteSprites
        {
            public Sprite Base, Top;
            public int PlankY = -1, PlankX0, PlankX1;   // scaffold plank the builders stand on (sprite pixels), -1 if none
            public int WorkY;                           // where the work is happening (top of the finished part)
        }

        public const int SiteSteps = 24;
        static readonly Dictionary<int, SiteSprites> siteCache = new Dictionary<int, SiteSprites>();
        const string TIMBER = "#d8b07a", TIMBER_D = "#a07a48", ROPE = "#efe6d2";

        /// <summary>Progress (0-1) to a cached step.</summary>
        public static int SiteStep(float progress) { return Mathf.Clamp(Mathf.FloorToInt(progress * SiteSteps), 0, SiteSteps); }

        /// <summary>
        /// A building going up: stakes and string on cleared ground, then stone footings laid left to right,
        /// a timber frame rising, and finally walls and roof filling in from the ground up behind scaffolding.
        /// </summary>
        public static SiteSprites Site(BuildingType type, int variant, int season, int step)
        {
            int key = (((int)type * 16 + variant) * 4 + season) * 32 + step;
            SiteSprites s;
            if (siteCache.TryGetValue(key, out s)) return s;
            var full = Get(type, variant, season);
            var d = Defs.Get(type);
            int W = full.W, H = full.H, L = full.Lift, fh = d.H * 16;
            float f = step / (float)SiteSteps;
            bool winter = season == 3;
            s = new SiteSprites();

            // ---- ground: trampled dirt, corner stakes and string lines
            var bg = new PixelCanvas(W, H);
            bool flat = type == BuildingType.Strip || type == BuildingType.Demesne;
            if (!flat) bg.R(1, L + 1, W - 2, fh - 2, winter ? "#d8d2c6" : "#9e7a4d");
            if (!flat) for (int i = 0; i < W * d.H / 2; i++) bg.D(1 + (int)(Hs(i, (int)type, 3) * (W - 2)), L + 1 + (int)(Hs((int)type, i, 4) * (fh - 2)), winter ? "#c4beb2" : "#8a6a42");
            if (flat)
            {
                // fields and graveyards are laid out from the far side toward the road
                int upto = L + Mathf.RoundToInt(fh * Mathf.Clamp01(f / 0.9f));
                for (int y = L; y < upto; y++) for (int x = 0; x < W; x++) { var c = full.BaseCanvas.Get(x, y); if (c.a > 0) bg.Set(x, y, c); }
            }
            for (int x = 2; x < W - 2; x += 2) { bg.D(x, L + 1, ROPE); bg.D(x, L + fh - 2, ROPE); }
            for (int y = L + 2; y < L + fh - 2; y += 2) { bg.D(1, y, ROPE); bg.D(W - 2, y, ROPE); }
            foreach (var c in new[] { new Vector2Int(0, L), new Vector2Int(W - 2, L), new Vector2Int(0, L + fh - 3), new Vector2Int(W - 2, L + fh - 3) })
            { bg.R(c.x, c.y, 2, 3, "#8a5a34"); bg.D(c.x, c.y, TIMBER); }

            var tg = new PixelCanvas(W, H);
            if (flat)
            {
                if (f >= 0.9f) tg.Draw(full.TopCanvas, 0, 0);
                s.Base = bg.ToSprite("site_base" + key); s.Top = tg.ToSprite("site_top" + key); s.WorkY = L + fh / 2;
                siteCache[key] = s; return s;
            }

            // ---- the finished building's silhouette, column by column
            var top = new int[W]; var bot = new int[W];
            int x0 = W, x1 = -1, yTop = H, yBot = 0;
            for (int x = 0; x < W; x++)
            {
                top[x] = -1; bot[x] = -1;
                for (int y = 0; y < H; y++) if (full.TopCanvas.Get(x, y).a == 255) { if (top[x] < 0) top[x] = y; bot[x] = y; }
                if (top[x] < 0) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                yTop = Mathf.Min(yTop, top[x]); yBot = Mathf.Max(yBot, bot[x]);
            }
            if (x1 < 0) { s.Base = bg.ToSprite("site_base" + key); s.Top = tg.ToSprite("site_top" + key); siteCache[key] = s; return s; }

            const float footEnd = 0.12f, frameEnd = 0.4f;
            var fr = new PixelCanvas(W, H);   // footings, frame and scaffold, outlined on their own
            // footings: grey stones laid left to right
            float lay = Mathf.Clamp01(f / footEnd);
            int laid = x0 + Mathf.RoundToInt((x1 - x0 + 1) * lay);
            for (int x = x0; x < laid; x++)
            {
                if (bot[x] < 0) continue;
                bool joint = ((x - x0) % 5) == 4;
                fr.Set(x, bot[x] - 1, PixelCanvas.C(joint ? "#6e6860" : "#a39c90"));
                fr.Set(x, bot[x], PixelCanvas.C(joint ? "#5e5850" : "#8d877d"));
            }
            // timber frame: posts rising, then cross beams and the line of the roof
            if (f > footEnd)
            {
                float rise = Mathf.Clamp01((f - footEnd) / (frameEnd - footEnd - 0.04f));
                var posts = new List<int>();
                for (int x = x0 + 1; x < x1 - 3; x += 9) posts.Add(x);
                posts.Add(x1 - 2);
                var reach = new Dictionary<int, int>();
                foreach (int px in posts)
                {
                    int b = Mathf.Max(bot[px], bot[Mathf.Min(px + 1, W - 1)]) - 2, t = Mathf.Min(top[px], top[Mathf.Min(px + 1, W - 1)]) + 1;
                    int y = b - Mathf.RoundToInt((b - t) * rise);
                    reach[px] = y;
                    for (int yy = y; yy <= b; yy++) { fr.Set(px, yy, PixelCanvas.C(TIMBER)); fr.Set(px + 1, yy, PixelCanvas.C(TIMBER_D)); }
                }
                for (int i = 0; i + 1 < posts.Count; i++)
                {
                    int a = posts[i], b2 = posts[i + 1];
                    for (int by = yBot - 10; by > yTop + 2; by -= 10)
                        if (reach[a] <= by && reach[b2] <= by)
                            for (int x = a + 2; x < b2; x++) if (by >= top[x] && by <= bot[x]) fr.Set(x, by, PixelCanvas.C(TIMBER_D));
                }
                if (rise >= 1f)
                    for (int x = x0 + 1; x < x1; x++) if (top[x] >= 0) fr.Set(x, top[x] + 1, PixelCanvas.C(TIMBER));
            }
            // walls and roof fill in from the ground up
            int reveal = H;
            if (f >= frameEnd)
            {
                float k = Mathf.Clamp01((f - frameEnd) / (1f - frameEnd));
                reveal = yBot + 1 - Mathf.RoundToInt((yBot + 1 - yTop) * k);
            }
            s.WorkY = Mathf.Min(reveal, yBot - 2);
            // scaffolding once the frame is up: poles, a ladder and a plank just under the work
            bool scaffold = f >= frameEnd - 0.06f && f < 1f && yBot - yTop > 14;
            var sc = new PixelCanvas(W, H);
            if (scaffold)
            {
                int plank = Mathf.Clamp(reveal + 6, yTop + 6, yBot - 4);
                int pa = x0 + 1, pb = x1 - 1;
                for (int y = plank - 8; y <= yBot; y++) { sc.Set(pa, y, PixelCanvas.C("#a8734a")); sc.Set(pb, y, PixelCanvas.C("#a8734a")); }
                for (int x = pa; x <= pb; x++) { sc.Set(x, plank, PixelCanvas.C("#c49364")); sc.Set(x, plank + 1, PixelCanvas.C("#7a4e2c")); }
                int lx = pa + 3;
                for (int y = plank; y <= yBot; y++) { sc.Set(lx, y, PixelCanvas.C("#8a5a34")); sc.Set(lx + 3, y, PixelCanvas.C("#8a5a34")); if ((y - plank) % 3 == 2) { sc.Set(lx + 1, y, PixelCanvas.C(TIMBER)); sc.Set(lx + 2, y, PixelCanvas.C(TIMBER)); } }
                s.PlankY = plank; s.PlankX0 = pa + 6; s.PlankX1 = pb - 2;
            }
            fr.Outline(OL);
            tg.Draw(fr, 0, 0);
            // the finished part
            for (int y = Mathf.Max(0, reveal); y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = full.TopCanvas.Get(x, y);
                    if (c.a == 0) continue;
                    if (y < reveal + 2 && c.a == 255 && f < 1f) c = PixelCanvas.C((x + y) % 4 == 0 ? TIMBER_D : TIMBER);   // fresh boards on the top course
                    tg.Set(x, y, c);
                }
            // scaffolding stands in front of the new walls
            if (scaffold) { sc.Outline(OL); tg.Draw(sc, 0, 0); }
            s.Base = bg.ToSprite("site_base" + key); s.Top = tg.ToSprite("site_top" + key);
            siteCache[key] = s;
            return s;
        }
    }
}

