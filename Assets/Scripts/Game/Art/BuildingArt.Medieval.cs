using System.Collections.Generic;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>The manor's buildings: thatched cottages, timber-framed houses, the stone keep and church, the post mill.
    /// Same chunky 3/4 farm-sim look as the rest of the art.</summary>
    public static partial class BuildingArt
    {
        // ================================================================== medieval kit
        static readonly string[] ThatchC = { "#c8a050", "#a07a2a", "#e0c070", "#8a6424" };
        static readonly string[] ThatchOld = { "#b0904a", "#8a6a2a", "#c8a860", "#74561e" };
        static readonly string[] StoneC = { "#a39c90", "#8d877d", "#bdb6aa", "#6e6860" };
        static readonly string[] StoneWarm = { "#b8aa92", "#9a8e78", "#cfc2aa", "#7a705e" };
        static readonly string[] Slate = { "#5a5e66", "#43464d", "#767a82" };
        static readonly string[] Tiles = { "#a8553a", "#84402c", "#c87452" };
        const string TIMBERDARK = "#4a3020", TIMBERMID = "#6e4428";

        /// <summary>A thatched roof: thick golden straw in rows, a rounded hip at each end and a heavy eave.</summary>
        static void Thatch(PixelCanvas g, int x, int y, int w, int h, bool winter, string[] c = null, int hip = 4)
        {
            c = c ?? ThatchC;
            for (int r = 0; r < h; r++)
            {
                int inset = Mathf.Max(0, hip - r);
                int xa = x + inset, xb = x + w - inset;
                g.R(xa, y + r, xb - xa, 1, c[0]);
                for (int xx = xa + ((r * 3) % 4); xx < xb; xx += 4) g.D(xx, y + r, (r % 3 == 0) ? c[2] : c[1]);
                if (r % 4 == 3) for (int xx = xa; xx < xb; xx++) if (Hs(xx, y + r, 3) < 0.55f) g.D(xx, y + r, c[1]);
                g.D(xa, y + r, c[1]); g.D(xb - 1, y + r, c[3]);
            }
            // the ridge, and the deep eave at the bottom
            g.R(x + hip, y, w - hip * 2, 1, c[3]);
            g.R(x, y + h - 2, w, 1, c[1]); g.R(x, y + h - 1, w, 1, c[3]);
            for (int xx = x; xx < x + w; xx += 2) g.D(xx, y + h - 1, c[1]);
            if (winter)
            {
                for (int r = 0; r < Mathf.Min(h - 3, 6); r++)
                {
                    int inset = Mathf.Max(0, hip - r);
                    for (int xx = x + inset; xx < x + w - inset; xx++) if (r < 4 || Hs(xx, r, 9) < 0.5f) g.D(xx, y + r, r < 3 ? SNOW : SNOW2);
                }
            }
        }

        /// <summary>Wattle and daub between dark timbers.</summary>
        static void Daub(PixelCanvas g, int x, int y, int w, int h, string[] c, bool frame = true, bool braces = true)
        {
            g.R(x, y, w, h, c[0]);
            for (int i = 0; i < w * h / 7; i++) g.D(x + (int)(Hs(i, x + y, 5) * w), y + (int)(Hs(x - y, i, 6) * h), Hs(i, i, 2) < 0.5f ? c[1] : c[2]);
            if (!frame) return;
            g.R(x, y, w, 1, TIMBERDARK); g.R(x, y + h - 1, w, 1, TIMBERDARK);
            g.R(x, y, 2, h, TIMBERDARK); g.R(x + w - 2, y, 2, h, TIMBERDARK);
            if (braces) { g.Line(x + 2, y + h - 2, x + 6, y + 1, TIMBERMID); g.Line(x + w - 3, y + h - 2, x + w - 7, y + 1, TIMBERMID); }
        }

        /// <summary>Coursed stone: rows of blocks with offset joints and a few lighter faces.</summary>
        static void Stone(PixelCanvas g, int x, int y, int w, int h, string[] c)
        {
            g.R(x, y, w, h, c[0]);
            for (int r = 0; r < h; r += 4)
            {
                int yy = y + r;
                if (r > 0) g.R(x, yy, w, 1, c[1]);
                int off = (r / 4) % 2 == 0 ? 2 : 6;
                for (int xx = x + off; xx < x + w; xx += 8) for (int j = 1; j < 4 && r + j < h; j++) g.D(xx, yy + j, c[1]);
                for (int xx = x; xx < x + w; xx += 8)
                {
                    if (Hs(xx, yy, 11) < 0.3f) g.R(xx + off - 1 < x ? x : Mathf.Min(x + w - 3, xx + 1), yy + 1, 3, Mathf.Min(2, h - r - 1), c[2]);
                    if (Hs(yy, xx, 12) < 0.15f) g.D(Mathf.Min(x + w - 1, xx + 4), yy + 2, c[3]);
                }
            }
            g.R(x + w - 1, y, 1, h, c[3]);
        }

        /// <summary>Battlements along the top of a wall: merlons with gaps between.</summary>
        static void Merlons(PixelCanvas g, int x, int y, int w, string[] c, bool winter)
        {
            for (int xx = x; xx < x + w; xx += 6)
            {
                int mw = Mathf.Min(4, x + w - xx);
                g.R(xx, y - 4, mw, 4, c[0]); g.R(xx, y - 4, mw, 1, c[2]); g.D(xx + mw - 1, y - 3, c[3]);
                if (winter) g.R(xx, y - 5, mw, 1, SNOW);
            }
            g.R(x, y, w, 2, c[1]);
            if (winter) for (int xx = x; xx < x + w; xx += 6) if (xx + 4 < x + w) g.R(xx + 4, y - 1, 2, 1, SNOW);
        }

        /// <summary>A small unglazed window with a wooden shutter swung open; firelight shows through at night.</summary>
        static void Shutter(Ctx k, int x, int y, int w, int h, bool left = true)
        {
            var g = k.Tg;
            g.R(x - 1, y - 1, w + 2, h + 2, TIMBERDARK);
            g.R(x, y, w, h, "#2a1a10");
            g.D(x, y, "#4a3020");
            int sx = left ? x - 1 - Mathf.Max(2, w / 2) : x + w + 1;
            g.R(sx, y - 1, Mathf.Max(2, w / 2), h + 2, "#8a5a34");
            g.R(sx, y - 1, 1, h + 2, "#6e4428");
            k.Wins.Add(new RectInt(x, y, w, h));
        }

        /// <summary>A tall narrow lancet window with a pointed head, glazed in coloured glass.</summary>
        static void Lancet(Ctx k, int x, int y, int w, int h, bool stained = true, string frame = "#cfc2aa")
        {
            var g = k.Tg;
            g.R(x - 1, y + 1, w + 2, h, frame);
            for (int i = 0; i < (w + 1) / 2 + 1; i++) g.R(x - 1 + i, y - (w + 1) / 2 + 1 + i, w + 2 - i * 2, 1, frame);
            g.R(x, y + 1, w, h - 1, "#3a5a88");
            for (int i = 0; i < (w + 1) / 2; i++) g.R(x + i, y - (w + 1) / 2 + 2 + i, w - i * 2, 1, "#3a5a88");
            if (stained)
                for (int yy = y + 1; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                    {
                        float hh = Hs(xx, yy, 21);
                        if (hh < 0.18f) g.D(xx, yy, "#c84a3a"); else if (hh < 0.3f) g.D(xx, yy, "#e0b04a"); else if (hh < 0.38f) g.D(xx, yy, "#5aa04a");
                    }
            g.D(x, y + 1, "#a8c8e0");
            k.Wins.Add(new RectInt(x, y + 1, w, h - 1));
        }

        /// <summary>A round-headed or pointed doorway in stone, with a plank door.</summary>
        static void ArchDoor(Ctx k, int x, int y, int w, int h, string wood = "#6e4428", string stone = "#cfc2aa")
        {
            var g = k.Tg;
            k.M.HasDoor = true; k.M.Door = new RectInt(x, y, w, h);
            g.R(x - 1, y, w + 2, h, stone);
            for (int i = 0; i <= w / 2; i++) g.R(x - 1 + i, y - w / 2 - 1 + i, w + 2 - i * 2, 1, stone);
            g.R(x, y, w, h, wood);
            for (int i = 0; i < w / 2; i++) g.R(x + i, y - w / 2 + i, w - i * 2, 1, wood);
            for (int xx = x + 1; xx < x + w; xx += 2) g.R(xx, y - 1, 1, h, "#5a3420");
            g.R(x, y + 2, w, 1, "#3a3a3a"); g.R(x, y + h - 3, w, 1, "#3a3a3a");   // iron straps
            g.D(x + w - 2, y + h / 2, "#c8a050");
            g.R(x - 1, y + h, w + 2, 1, StoneC[1]);
        }

        static void Sack(PixelCanvas g, int x, int y, string c = "#d8c8a0")
        {
            g.R(x, y + 1, 5, 4, c); g.R(x + 1, y, 3, 1, c); g.R(x + 4, y + 1, 1, 4, Darker(c));
            g.D(x + 2, y, "#a07a48");
        }
        static string Darker(string hex)
        {
            var c = PixelCanvas.C(hex);
            return "#" + ((int)(c.r * 0.78f)).ToString("x2") + ((int)(c.g * 0.78f)).ToString("x2") + ((int)(c.b * 0.78f)).ToString("x2");
        }

        static readonly string[][] Daubs = { new[] { "#e0d0a8", "#cbb98e", "#ece0c0" }, new[] { "#d8c49a", "#c0aa7e", "#e8d8b4" }, new[] { "#e8dcc4", "#d0c2a4", "#f4ecd8" } };

        // ================================================================== homes
        /// <summary>A serf's cottage: cruck-framed wattle and daub under a deep thatch, a smoke hole in the ridge and a toft garden behind.</summary>
        static void Cottage(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int v = k.V; int y0 = k.Y(0);
            // the toft: a garden plot behind a woven wattle fence
            bg.R(2, y0 + 2, 28, 15, W ? SNOW : "#7a5232");
            if (!W) for (int fy = y0 + 4; fy < y0 + 16; fy += 3) bg.R(3, fy, 26, 1, "#5e3e24");
            else for (int i = 0; i < 26; i++) bg.D(3 + (int)(Hs(i, v, 1) * 26), y0 + 2 + (int)(Hs(v, i, 2) * 14), "#a89478");
            for (int x = 1; x < 31; x++) { bg.D(x, y0 + 1, (x % 2 == 0) ? "#8a6a42" : "#a8845a"); bg.D(x, y0 + 2, (x % 2 == 1) ? "#8a6a42" : "#6e5232"); }
            for (int y = y0 + 1; y < y0 + 18; y++) { bg.D(1, y, (y % 2 == 0) ? "#8a6a42" : "#6e5232"); bg.D(30, y, (y % 2 == 1) ? "#8a6a42" : "#6e5232"); }
            for (int x = 1; x < 31; x += 5) bg.R(x, y0, 1, 3, "#5a3a22");
            if (W) for (int x = 1; x < 31; x += 2) bg.D(x, y0 + 1, SNOW);
            k.Crops.Add(new CropRow { X = 4, Y = y0 + 6, W = 24 });
            k.Crops.Add(new CropRow { X = 4, Y = y0 + 9, W = 24 });
            k.Crops.Add(new CropRow { X = 4, Y = y0 + 12, W = 24 });
            // a beaten path to the door
            bg.R(12, y0 + 46, 8, 2, W ? "#d8d2c6" : "#b8996a");
            // the cottage
            Shadow(tg, 3, y0 + 45, 26);
            Thatch(tg, 1, y0 + 17, 30, 16, W, v == 1 ? ThatchOld : ThatchC, 5);
            tg.R(14, y0 + 17, 4, 2, "#5a4020"); tg.D(15, y0 + 17, "#2a1a10"); tg.D(16, y0 + 17, "#2a1a10");   // smoke hole in the ridge
            k.M.Smoke.Add(new Vector2(16, y0 + 16));
            Daub(tg, 3, y0 + 32, 26, 13, Daubs[v]);
            tg.R(3, y0 + 32, 26, 1, ThatchC[3]);
            Shutter(k, 7, y0 + 35, 3, 3, true);
            Shutter(k, 23, y0 + 35, 3, 3, false);
            Door(k, 13, y0 + 35, 6, 10, "#7a4e2c");
            tg.R(3, y0 + 44, 26, 1, "#6e5a44");
            // a water butt or a woodpile by the wall
            if (v == 2) { Barrel(tg, 26, y0 + 40); }
            else { for (int i = 0; i < 3; i++) { tg.R(0, y0 + 42 - i * 2, 4, 2, i % 2 == 0 ? "#a8784a" : "#8a5a34"); tg.D(0, y0 + 42 - i * 2, "#e0b878"); } }
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>A freeman's house: two storeys of close-studded timber framing, the upper floor jettied out, a tiled roof and a stone chimney.</summary>
        static void FreeHouse(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int v = k.V; int y0 = k.Y(0);
            string[][] roofs = { Tiles, Slate, new[] { "#8a6a4a", "#6e5038", "#a8845e" } };
            // a yard behind with a vegetable bed
            bg.R(3, y0 + 2, 26, 11, W ? SNOW : "#7a5232");
            if (!W) for (int fy = y0 + 4; fy < y0 + 12; fy += 3) bg.R(4, fy, 24, 1, "#5e3e24");
            for (int x = 2; x < 30; x += 3) { bg.R(x, y0, 2, 3, "#8a6a42"); }
            bg.R(2, y0 + 1, 28, 1, "#6e5232");
            k.Crops.Add(new CropRow { X = 5, Y = y0 + 6, W = 22 });
            k.Crops.Add(new CropRow { X = 5, Y = y0 + 9, W = 22 });
            bg.R(12, y0 + 46, 8, 2, W ? "#d8d2c6" : "#b8996a");
            Shadow(tg, 3, y0 + 45, 26);
            Shingles(tg, 1, y0 + 1, 30, 15, roofs[v], W);
            Chimney(k, 23, y0 - 7, 10);
            // upper storey, jettied out over the lower
            Daub(tg, 1, y0 + 16, 30, 13, Daubs[2], true, false);
            for (int x = 5; x < 29; x += 4) tg.R(x, y0 + 17, 1, 11, TIMBERDARK);
            tg.R(1, y0 + 28, 30, 2, TIMBERDARK); tg.R(1, y0 + 29, 30, 1, "#2e1e14");
            Window(k, 7, y0 + 19, 4, 4, false, TIMBERMID); Window(k, 21, y0 + 19, 4, 4, false, TIMBERMID);
            // lower storey
            Daub(tg, 3, y0 + 30, 26, 15, Daubs[v == 2 ? 0 : 2], true, true);
            tg.R(15, y0 + 31, 1, 13, TIMBERDARK);
            Window(k, 6, y0 + 34, 4, 4, true, TIMBERMID);
            Door(k, 19, y0 + 34, 6, 11, "#6e4428");
            Foundation(tg, 3, y0 + 44, 26);
        }

        /// <summary>The lord's seat: a square stone keep with battlements and the lord's banner, and the great hall beside it.</summary>
        static void Keep(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            // the bailey: a packed-earth yard, a stone path to the door
            bg.R(0, y0 + 4, 64, 60, W ? "#e4e0d8" : "#b8a07a");
            for (int i = 0; i < 90; i++) bg.D((int)(Hs(i, 31, 3) * 64), y0 + 4 + (int)(Hs(31, i, 4) * 60), W ? "#d0ccc2" : (i % 3 == 0 ? "#a08a64" : "#c8b088"));
            for (int y = y0 + 52; y < y0 + 64; y += 3) for (int x = 15; x < 25; x += 4) { bg.R(x + (y % 2) * 2, y, 3, 2, "#a39c90"); bg.D(x + (y % 2) * 2, y, "#bdb6aa"); }
            // grass round the edges
            string[] lawn = { "#7fbe55", "#6fae48", "#a6a052", SNOW };
            bg.R(0, y0 + 60, 64, 4, lawn[si]);

            // the great hall, behind and to the right
            Shadow(tg, 30, y0 + 51, 32);
            Shingles(tg, 28, y0 + 6, 35, 18, Slate, W);
            Chimney(k, 52, y0 - 1, 9);
            Stone(tg, 30, y0 + 24, 31, 28, StoneWarm);
            tg.R(30, y0 + 24, 31, 2, StoneWarm[1]);
            Lancet(k, 34, y0 + 30, 3, 9, false, StoneWarm[2]); Lancet(k, 41, y0 + 30, 3, 9, false, StoneWarm[2]);
            Lancet(k, 48, y0 + 30, 3, 9, false, StoneWarm[2]); Lancet(k, 55, y0 + 30, 3, 9, false, StoneWarm[2]);
            tg.R(30, y0 + 50, 31, 2, StoneWarm[3]);

            // the keep tower
            int tx = 4, tw = 28, ttop = y0 - 40;
            Shadow(tg, tx, y0 + 55, tw);
            Stone(tg, tx, ttop, tw, y0 + 55 - ttop, StoneC);
            tg.R(tx + tw - 4, ttop, 4, y0 + 55 - ttop, StoneC[1]);       // the shaded east face
            for (int y = ttop + 2; y < y0 + 55; y += 4) tg.D(tx + tw - 3, y, StoneC[3]);
            Merlons(tg, tx - 1, ttop, tw + 2, StoneC, W);
            // corner turret corbels
            tg.R(tx - 2, ttop + 2, 3, 3, StoneC[1]); tg.R(tx + tw - 1, ttop + 2, 3, 3, StoneC[3]);
            // string courses
            tg.R(tx, ttop + 22, tw, 1, StoneC[2]); tg.R(tx, ttop + 46, tw, 1, StoneC[2]);
            // arrow slits and windows
            foreach (int sy in new[] { ttop + 8, ttop + 30 })
                foreach (int sx in new[] { tx + 6, tx + 19 })
                {
                    tg.R(sx, sy, 1, 7, "#2a1a10"); tg.R(sx - 1, sy + 3, 3, 1, "#2a1a10");
                }
            Lancet(k, tx + 11, ttop + 26, 5, 8, false, StoneC[2]);
            k.Glow.Add(new RectInt(tx + 6, ttop + 8, 1, 7)); k.Glow.Add(new RectInt(tx + 19, ttop + 8, 1, 7));
            // the forebuilding: steps up to the door
            Stone(tg, tx + 7, y0 + 40, 14, 15, StoneC);
            ArchDoor(k, tx + 11, y0 + 44, 6, 10, "#5a3420", StoneC[2]);
            for (int i = 0; i < 3; i++) tg.R(tx + 9 - i, y0 + 55 + i, 10 + i * 2, 1, i % 2 == 0 ? StoneC[2] : StoneC[1]);
            // the banner pole on the roof
            tg.R(tx + tw / 2, ttop - 16, 1, 12, "#5a3a22");
            tg.D(tx + tw / 2, ttop - 17, "#e0b04a");
            k.M.BannerAt = new Vector2(tx + tw / 2 + 1, ttop - 15);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>The parish church: a stone nave, a west tower with battlements and a bell, lancet windows of coloured glass, and the churchyard.</summary>
        static void Church(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            string[] lawns = { "#86c05a", "#74b04c", "#a8a052", SNOW };
            bg.R(0, y0, 48, 64, lawns[si]);
            for (int i = 0; i < 50; i++) bg.D((int)(Hs(i, 41, 3) * 48), y0 + (int)(Hs(41, i, 4) * 64), W ? SNOW2 : "#5a9a3e");
            // the churchyard wall, a lychgate gap and the path
            for (int x = 0; x < 48; x++) { if (x >= 28 && x < 38) continue; bg.R(x, y0 + 60, 1, 3, x % 4 == 0 ? "#6e6860" : "#8d877d"); }
            bg.R(0, y0 + 60, 28, 1, "#a39c90"); bg.R(38, y0 + 60, 10, 1, "#a39c90");
            bg.R(29, y0 + 52, 8, 12, W ? "#e4e0d8" : "#d8c8a0");
            // a few old graves in the yard
            foreach (var gp in new[] { new Vector2Int(3, 54), new Vector2Int(9, 56), new Vector2Int(42, 54), new Vector2Int(15, 53) })
            {
                bg.R(gp.x, y0 + gp.y, 3, 4, "#a39c90"); bg.R(gp.x, y0 + gp.y, 3, 1, "#bdb6aa"); bg.D(gp.x + 1, y0 + gp.y + 1, "#6e6860");
                if (!W) bg.R(gp.x - 1, y0 + gp.y + 4, 5, 1, "#5a8a3e");
            }

            // the nave: its roof runs back, the gable end faces the path
            Shadow(tg, 17, y0 + 51, 29);
            Shingles(tg, 15, y0 - 4, 32, 18, Slate, W);
            Gable(tg, 31, y0 + 2, 15, 14, StoneC[0], StoneC[2], Slate, W);
            Stone(tg, 17, y0 + 15, 28, 36, StoneC);
            tg.R(17, y0 + 15, 28, 1, StoneC[1]);
            Lancet(k, 20, y0 + 22, 3, 13); Lancet(k, 40, y0 + 22, 3, 13);
            // a round rose window over the doors
            tg.Disc(31, y0 + 10, 3f, StoneC[2]); tg.Disc(31, y0 + 10, 2f, "#3a5a88"); tg.D(31, y0 + 10, "#c84a3a"); tg.D(30, y0 + 9, "#e0b04a");
            k.Wins.Add(new RectInt(30, y0 + 9, 2, 2));
            ArchDoor(k, 28, y0 + 38, 7, 12, "#6e3a2a", StoneC[2]);
            tg.R(17, y0 + 49, 28, 2, StoneC[3]);

            // the west tower
            int tx = 2, tw = 15, ttop = y0 - 32;
            Shadow(tg, tx, y0 + 51, tw);
            Stone(tg, tx, ttop, tw, y0 + 51 - ttop, StoneC);
            tg.R(tx + tw - 3, ttop, 3, y0 + 51 - ttop, StoneC[1]);
            Merlons(tg, tx - 1, ttop, tw + 2, StoneC, W);
            // pinnacles at the corners
            tg.R(tx - 1, ttop - 8, 2, 5, StoneC[2]); tg.D(tx - 1, ttop - 9, StoneC[2]);
            tg.R(tx + tw - 1, ttop - 8, 2, 5, StoneC[1]); tg.D(tx + tw, ttop - 9, StoneC[1]);
            // the belfry: louvred openings and a bell
            tg.R(tx + 4, ttop + 4, 6, 9, "#2a1a10");
            for (int i = 0; i < 4; i++) tg.R(tx + 4 + i, ttop + 4 - 2 + (i > 1 ? 3 - i : i), 1, 1, StoneC[0]);
            tg.R(tx + 5, ttop + 7, 4, 4, "#c89a3a"); tg.R(tx + 4, ttop + 10, 6, 1, "#a87a2a"); tg.D(tx + 7, ttop + 11, "#7a5a1e");
            Lancet(k, tx + 6, ttop + 24, 2, 9);
            tg.R(tx, ttop + 18, tw, 1, StoneC[2]); tg.R(tx, ttop + 40, tw, 1, StoneC[2]);
            // a cross on the gable
            tg.R(31, y0 - 9, 1, 6, "#c89a3a"); tg.R(29, y0 - 7, 5, 1, "#c89a3a");
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        // ================================================================== stores and fields
        /// <summary>The lord's granary: a big timber barn raised on mushroom-shaped staddle stones to keep the rats out, under a heavy thatch.</summary>
        static void Granary(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 24, 48, 8, W ? "#d8d2c6" : "#b8996a");
            for (int i = 0; i < 24; i++) bg.D((int)(Hs(i, 2, 3) * 48), y0 + 24 + (int)(Hs(2, i, 4) * 8), W ? "#c4beb2" : (i % 3 == 0 ? "#e0c070" : "#a08452"));
            Shadow(tg, 3, y0 + 29, 42);
            Thatch(tg, 1, y0 - 14, 46, 22, W, ThatchC, 7);
            // weatherboarded walls
            Planks(tg, 4, y0 + 8, 40, 15, "#7a4e2c", "#5a3820", "#9a6a3a");
            tg.R(4, y0 + 8, 40, 1, "#4a3020");
            for (int x = 4; x < 44; x += 13) tg.R(x, y0 + 8, 2, 15, TIMBERDARK);
            // the wide doors
            tg.R(18, y0 + 10, 12, 13, TIMBERDARK);
            tg.R(19, y0 + 11, 10, 12, "#8a5a34"); tg.R(23, y0 + 11, 2, 12, TIMBERDARK);
            tg.Line(19, y0 + 11, 22, y0 + 22, "#6e4428"); tg.Line(25, y0 + 11, 28, y0 + 22, "#6e4428");
            k.M.HasDoor = true; k.M.Door = new RectInt(19, y0 + 11, 10, 12);
            // staddle stones underneath
            tg.R(4, y0 + 23, 40, 2, "#4a3020");
            foreach (int sx in new[] { 5, 15, 30, 40 })
            {
                tg.R(sx - 1, y0 + 25, 5, 1, StoneC[2]); tg.R(sx, y0 + 26, 3, 3, StoneC[0]); tg.D(sx + 2, y0 + 27, StoneC[1]); tg.R(sx - 1, y0 + 29, 5, 1, StoneC[1]);
            }
            // steps up to the door, sacks waiting
            tg.R(20, y0 + 25, 8, 2, StoneC[0]); tg.R(19, y0 + 27, 10, 2, StoneC[1]);
            Sack(tg, 9, y0 + 25); Sack(tg, 33, y0 + 26, "#c8b890");
            k.M.PileAt = new Vector2Int(36, y0 + 31);
        }

        /// <summary>A family's strips in the open field: long ploughed ridges with grass balks between and a boundary stone. No fences: it's common land.</summary>
        static void Strip(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            int w = k.W, h = k.H;
            bg.R(1, 1, w - 2, h - 2, W ? SNOW : "#7a5232");
            // ridge and furrow, running up and down the field in three strips
            for (int x = 2; x < w - 2; x++)
            {
                int inStrip = (x - 2) % 15;
                if (inStrip == 14) { for (int y = 1; y < h - 1; y++) bg.D(x, y, W ? SNOW2 : (y % 3 == 0 ? "#6a9a48" : "#5a8a3e")); continue; }   // the grass balk
                string c = W ? (inStrip % 4 == 0 ? "#dfe8ec" : SNOW) : (inStrip % 4 == 0 ? "#5e3e24" : inStrip % 4 == 2 ? "#8a6240" : "#7a5232");
                for (int y = 2; y < h - 2; y++) bg.D(x, y, c);
            }
            // headlands top and bottom where the plough turns
            bg.R(1, 1, w - 2, 1, W ? SNOW : "#8a6a42"); bg.R(1, h - 2, w - 2, 1, W ? SNOW : "#8a6a42");
            // a meer stone marking the family's strips
            tg.R(2, h - 7, 3, 4, StoneC[0]); tg.R(2, h - 7, 3, 1, StoneC[2]); tg.D(4, h - 5, StoneC[3]);
            if (W) tg.D(3, h - 8, SNOW);
            k.M.PileAt = new Vector2Int(w - 12, h - 2);
        }

        /// <summary>The lord's demesne: a great field behind a hedge and a hurdle gate, with the lord's pennant on a pole.</summary>
        static void Demesne(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si;
            int w = k.W, h = k.H;
            bg.R(2, 2, w - 4, h - 4, W ? SNOW : "#7a5232");
            for (int y = 4; y < h - 3; y += 4) { bg.R(3, y, w - 6, 1, W ? "#dfe8ec" : "#5e3e24"); bg.R(3, y + 1, w - 6, 1, W ? SNOW : "#8a6240"); }
            // a hawthorn hedge all round, with a gap at the bottom for the gate
            string hedge = si == 3 ? "#8a7a62" : si == 2 ? "#7a8a3a" : "#3f7a32", hedgeL = si == 3 ? SNOW : si == 2 ? "#a8a04a" : "#5a9a3e";
            for (int x = 0; x < w; x++)
            {
                if (x >= w / 2 - 5 && x < w / 2 + 5) continue;
                bg.R(x, h - 3, 1, 3, hedge); if (Hs(x, 1, 3) < 0.5f) bg.D(x, h - 3, hedgeL);
                bg.R(x, 0, 1, 2, hedge); if (Hs(x, 2, 3) < 0.5f) bg.D(x, 0, hedgeL);
            }
            for (int y = 0; y < h; y++)
            {
                bg.R(0, y, 2, 1, hedge); bg.R(w - 2, y, 2, 1, hedge);
                if (Hs(y, 5, 3) < 0.4f) bg.D(0, y, hedgeL);
                if (Hs(y, 6, 3) < 0.4f) bg.D(w - 1, y, hedgeL);
                if (si < 2 && Hs(y, 7, 3) < 0.06f) bg.D(1, y, "#f4f1e6");   // may blossom
            }
            // the hurdle gate, swung open
            tg.R(w / 2 - 6, h - 6, 1, 6, "#8a5a34"); tg.R(w / 2 + 5, h - 6, 1, 6, "#8a5a34");
            for (int y = h - 9; y < h - 1; y += 2) tg.R(w / 2 + 6, y, 5, 1, "#a8784a");
            tg.R(w / 2 + 6, h - 9, 1, 8, "#8a5a34"); tg.R(w / 2 + 10, h - 9, 1, 8, "#8a5a34");
            // the lord's pennant pole in the corner
            tg.R(4, h - 30, 1, 27, "#5a3a22"); tg.D(4, h - 31, "#e0b04a");
            k.M.BannerAt = new Vector2(5, h - 29);
            k.M.PileAt = new Vector2Int(w - 18, h - 3);
        }

        // ================================================================== works
        /// <summary>The woodward's lodge: a small timber hut, a chopping block with an axe in it, and stacked logs.</summary>
        static void Woodward(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 18, 32, 14, W ? "#d8d2c6" : "#a08452");
            for (int i = 0; i < 36; i++) bg.D((int)(Hs(i, 51, 3) * 32), y0 + 18 + (int)(Hs(51, i, 4) * 14), W ? "#c4beb2" : (i % 2 == 0 ? "#e8d0a0" : "#8a6a42"));
            Shadow(tg, 2, y0 + 19, 18);
            Shingles(tg, 0, y0 - 2, 21, 11, new[] { "#6e5038", "#54392a", "#8a6a4a" }, W);
            Planks(tg, 2, y0 + 8, 17, 11, "#8a5a34", "#6e4428", "#a8784a");
            tg.R(2, y0 + 8, 17, 1, TIMBERDARK);
            Door(k, 11, y0 + 10, 5, 9, "#5a3a22");
            Shutter(k, 5, y0 + 11, 3, 3, false);
            // log stack on the right
            for (int r = 0; r < 4; r++)
                for (int i = 0; i < 4 - (r + 1) / 2; i++)
                {
                    int lx = 21 + i * 3 + (r % 2), ly = y0 + 16 - r * 3;
                    tg.Disc(lx + 1, ly + 1, 1.6f, "#a8784a"); tg.D(lx + 1, ly + 1, "#e0b878");
                }
            if (W) tg.R(21, y0 + 6, 11, 1, SNOW);
            // chopping block with an axe
            tg.R(5, y0 + 23, 6, 4, "#8a5a34"); tg.R(5, y0 + 23, 6, 1, "#e0b878");
            tg.Line(9, y0 + 18, 7, y0 + 23, "#7a4e2c"); tg.R(9, y0 + 18, 3, 2, "#9a9aa2");
            k.M.PileAt = new Vector2Int(14, y0 + 31);
        }

        /// <summary>A quarry: a cut face of grey stone, dressed blocks stacked ready, and a little lean-to for the tools.</summary>
        static void Quarry(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 14, 32, 18, W ? "#d8d2c6" : "#a8a090");
            for (int i = 0; i < 40; i++) bg.D((int)(Hs(i, 61, 3) * 32), y0 + 14 + (int)(Hs(61, i, 4) * 18), i % 2 == 0 ? "#8d877d" : "#c4beb2");
            // the cut face behind
            Stone(tg, 1, y0 - 6, 30, 20, StoneC);
            for (int x = 1; x < 31; x++) { int d = (int)(Hs(x, 7, 3) * 3); tg.Clear(x, y0 - 6, 1, d); }
            tg.R(1, y0 + 12, 30, 2, StoneC[3]);
            if (W) for (int x = 2; x < 30; x++) tg.D(x, y0 - 6 + (int)(Hs(x, 7, 3) * 3), SNOW);
            // a lean-to
            tg.Line(2, y0 + 14, 2, y0 + 24, "#7a4e2c"); tg.Line(10, y0 + 14, 10, y0 + 24, "#7a4e2c");
            tg.R(1, y0 + 12, 11, 2, "#8a6a4a"); tg.R(1, y0 + 12, 11, 1, "#a8845e");
            // dressed blocks stacked, and a pick
            foreach (var bl in new[] { new Vector2Int(15, 22), new Vector2Int(21, 22), new Vector2Int(18, 18), new Vector2Int(26, 23) })
            {
                tg.R(bl.x, y0 + bl.y, 6, 4, StoneC[0]); tg.R(bl.x, y0 + bl.y, 6, 1, StoneC[2]); tg.R(bl.x + 5, y0 + bl.y, 1, 4, StoneC[1]);
            }
            tg.Line(6, y0 + 26, 9, y0 + 20, "#7a4e2c"); tg.Line(7, y0 + 20, 11, y0 + 21, "#9a9aa2");
            k.M.PileAt = new Vector2Int(2, y0 + 31);
        }

        /// <summary>A post mill: the whole timber body turns on a great post to face the wind. The sails are drawn separately so they can turn.</summary>
        static void Mill(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            string[] lawn = { "#7fbe55", "#6fae48", "#a6a052", SNOW };
            bg.Ellipse(16, y0 + 24, 15, 7, PixelCanvas.C(W ? "#d8d2c6" : "#b8996a"));
            // the trestle: crossed quarter-bars on brick piers
            int px = 15;
            foreach (int bx in new[] { 5, 25 }) { tg.R(bx - 1, y0 + 22, 5, 4, "#a8553a"); tg.R(bx - 1, y0 + 22, 5, 1, "#c87452"); }
            tg.R(4, y0 + 21, 25, 2, TIMBERMID);
            tg.Line(6, y0 + 21, px, y0 + 6, TIMBERMID); tg.Line(26, y0 + 21, px + 1, y0 + 6, TIMBERMID);
            tg.R(px, y0 - 2, 2, 24, TIMBERDARK);
            // the buck (body): weatherboarded, a curved roof
            int bx0 = 6, bw = 20, btop = y0 - 30, bh = 28;
            Planks(tg, bx0, btop + 4, bw, bh - 4, "#e8dcc4", "#c8b89a", "#f6eedc");
            tg.R(bx0 + bw - 3, btop + 4, 3, bh - 4, "#c8b89a");
            for (int r = 0; r < 5; r++) { int inset = r == 0 ? 3 : r == 1 ? 1 : 0; tg.R(bx0 - 1 + inset, btop + r, bw + 2 - inset * 2, 1, r < 2 ? "#7a6a5a" : "#5a4a3a"); }
            if (W) tg.R(bx0 + 2, btop - 1, bw - 4, 2, SNOW);
            Window(k, bx0 + 4, btop + 9, 3, 3, false, TIMBERMID);
            Door(k, bx0 + 12, btop + 16, 5, 9, "#8a5a34");
            // the ladder down to the ground, and the tail pole for turning her
            tg.Line(bx0 + 15, btop + 26, bx0 + 21, y0 + 22, TIMBERMID); tg.Line(bx0 + 18, btop + 26, bx0 + 24, y0 + 22, TIMBERMID);
            for (int i = 0; i < 6; i++) { int ly = btop + 28 + i * 3; int lx = bx0 + 16 + i; tg.R(lx, ly, 3, 1, "#a8784a"); }
            Sack(tg, 1, y0 + 23); Sack(tg, 26, y0 + 24, "#f0ece0");
            // the hub, where the sails meet
            k.M.SailHub = new Vector2(bx0 + bw / 2f, btop + 8);
            tg.Disc(bx0 + bw / 2f, btop + 8, 2.2f, TIMBERDARK);
            k.Sails = MillSails(k.Si);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        static readonly Dictionary<int, Sprite[]> sailCache = new Dictionary<int, Sprite[]>();
        /// <summary>Four common sails (canvas spread on lattice frames), in four frames through a quarter turn. Pivot at the hub.</summary>
        static Sprite[] MillSails(int si)
        {
            Sprite[] f;
            if (sailCache.TryGetValue(si, out f)) return f;
            f = new Sprite[4];
            const int R = 21, S = R * 2 + 3;
            string cloth = si == 3 ? "#dcd8d0" : "#efe6d2", lat = "#8a6a42", stock = "#5a3a22";
            for (int i = 0; i < 4; i++)
            {
                var g = new PixelCanvas(S, S);
                float c = S / 2f;
                float a0 = i * Mathf.PI / 8f;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        for (int s = 0; s < 4; s++)
                        {
                            float a = a0 + s * Mathf.PI / 2f;
                            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                            float along = dx * ca + dy * sa, across = -dx * sa + dy * ca;
                            if (along < 0 || along > R) continue;
                            if (Mathf.Abs(across) < 0.75f) { g.D(x, y, stock); break; }
                            if (along > 5 && across > 0.75f && across < 5.5f)
                            {
                                bool grid = Mathf.Abs(Mathf.Repeat(along, 4f) - 2f) < 0.5f || across > 4.6f;
                                g.D(x, y, grid ? lat : cloth); break;
                            }
                        }
                    }
                g.Disc(c - 0.5f, c - 0.5f, 1.6f, stock);
                g.Outline(OL);
                f[i] = g.ToSprite("sails_" + si + "_" + i, new Vector2(0.5f, 0.5f));
            }
            sailCache[si] = f;
            return f;
        }

        /// <summary>The alehouse: a thatched house with an ale-stake, a pole with a green bush on the end, to say there's fresh ale.</summary>
        static void Alehouse(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 24, 32, 8, W ? "#d8d2c6" : "#b8996a");
            Shadow(tg, 2, y0 + 29, 28);
            Thatch(tg, 0, y0 - 4, 32, 15, W, ThatchOld, 4);
            k.M.Smoke.Add(new Vector2(22, y0 - 5));
            tg.R(21, y0 - 4, 3, 2, "#5a4020");
            Daub(tg, 2, y0 + 11, 28, 18, Daubs[1]);
            tg.R(15, y0 + 12, 2, 16, TIMBERDARK);
            Shutter(k, 6, y0 + 15, 4, 4, true);
            Door(k, 19, y0 + 17, 6, 11, "#6e4428");
            // the ale-stake jutting out over the door, with its bush
            tg.Line(24, y0 + 12, 31, y0 + 8, TIMBERDARK);
            string bush = W ? "#6a7a5a" : "#3f7a32";
            tg.Disc(30, y0 + 11, 2.6f, bush); tg.D(29, y0 + 10, W ? "#8a9a7a" : "#5aa83e"); tg.D(31, y0 + 12, "#2e5a24");
            // a bench and a barrel outside
            tg.R(4, y0 + 26, 9, 1, "#a8784a"); tg.R(5, y0 + 27, 1, 2, "#7a4e2c"); tg.R(11, y0 + 27, 1, 2, "#7a4e2c");
            Barrel(tg, 26, y0 + 23);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>The lord's pavilion: a big round tent in red and cream stripes with a pennant on top, where the household lives until the keep is built.</summary>
        static void Pavilion(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.Ellipse(16, y0 + 25, 15, 6, PixelCanvas.C(W ? "#d8d2c6" : "#a08452"));
            Shadow(tg, 3, y0 + 28, 26);
            // the walls: vertical stripes
            for (int x = 3; x < 29; x++)
            {
                bool red = ((x - 3) / 3) % 2 == 0;
                tg.R(x, y0 + 10, 1, 18, red ? "#b5432f" : "#efe6d2");
                if (x == 28 || x == 27) tg.R(x, y0 + 10, 1, 18, red ? "#8a2e22" : "#d6ccb6");
            }
            // scalloped valance
            for (int x = 2; x < 30; x += 4) { tg.R(x, y0 + 10, 4, 2, "#e0b04a"); tg.D(x + 1, y0 + 12, "#e0b04a"); tg.D(x + 2, y0 + 12, "#e0b04a"); }
            // the cone of the roof
            for (int r = 0; r < 18; r++)
            {
                int hw = 1 + Mathf.RoundToInt(r * 14f / 17f);
                for (int x = 16 - hw; x < 16 + hw; x++)
                {
                    int band = ((x - 16) * 18 / Mathf.Max(1, hw) + 40) / 6;
                    tg.D(x, y0 - 8 + r, band % 2 == 0 ? "#b5432f" : "#efe6d2");
                }
                tg.D(16 + hw - 1, y0 - 8 + r, "#8a2e22");
            }
            if (W) for (int r = 0; r < 6; r++) tg.R(16 - r, y0 - 8 + r, r * 2, 1, SNOW);
            // the door flap tied back
            tg.R(13, y0 + 16, 7, 12, "#3a2618"); tg.Line(13, y0 + 16, 11, y0 + 27, "#efe6d2"); tg.Line(19, y0 + 16, 21, y0 + 27, "#b5432f");
            k.M.HasDoor = true; k.M.Door = new RectInt(14, y0 + 17, 5, 11);
            k.Glow.Add(new RectInt(15, y0 + 20, 3, 6));
            // guy ropes and the pole
            tg.Line(3, y0 + 12, 0, y0 + 27, "#d6ccb6"); tg.Line(28, y0 + 12, 31, y0 + 27, "#d6ccb6");
            tg.R(16, y0 - 14, 1, 6, "#5a3a22");
            k.M.BannerAt = new Vector2(17, y0 - 14);
            k.M.PileAt = new Vector2Int(-1, -1);
        }
    }
}
