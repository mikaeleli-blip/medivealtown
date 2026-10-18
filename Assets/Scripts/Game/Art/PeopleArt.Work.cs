using System.Collections.Generic;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>
    /// Work poses: settlers swinging axes and picks, hoeing, scything, sowing, pitching hay, digging, splitting and sawing,
    /// fishing, picking, roping cattle, scraping hides, stirring, kneading, cleaving, sewing, shaving, serving, writing,
    /// preaching and sweeping. Each pose is a short cycle of frames with the arms and the tool drawn in.
    /// </summary>
    public static partial class PeopleArt
    {
        public enum Act
        {
            None, Chop, Pick, Hoe, Scythe, Sow, Fork, Shovel, Maul, Saw, Fish, Pluck, Lasso, Scrape, Stir,
            Knead, Cleaver, Sew, Shears, Shave, Wipe, Serve, Write, Preach, Point, Sweep, Pull, Peel, Tend
        }

        const int PadX = 10, PadT = 9, PadB = 9;

        // ---------------------------------------------------------------- timing
        sealed class Cycle { public float Step; public int[] Seq; public int Hit; public Cycle(float s, int hit, params int[] q) { Step = s; Hit = hit; Seq = q; } }
        static readonly Dictionary<Act, Cycle> cycles = new Dictionary<Act, Cycle>
        {
            { Act.Chop,    new Cycle(0.16f, 2, 0, 0, 0, 1, 2, 2, 2, 2) },
            { Act.Pick,    new Cycle(0.16f, 2, 0, 0, 0, 1, 2, 2, 2, 2) },
            { Act.Hoe,     new Cycle(0.20f, 1, 0, 0, 1, 1, 1) },
            { Act.Scythe,  new Cycle(0.18f, 2, 0, 0, 1, 2, 2, 1) },
            { Act.Sow,     new Cycle(0.22f, 1, 0, 0, 1, 1, 2) },
            { Act.Fork,    new Cycle(0.22f, 2, 0, 0, 1, 1, 2, 2, 1) },
            { Act.Shovel,  new Cycle(0.22f, 2, 0, 0, 0, 1, 1, 2, 2) },
            { Act.Maul,    new Cycle(0.18f, 2, 0, 0, 0, 1, 2, 2, 2, 2, 2) },
            { Act.Saw,     new Cycle(0.16f, 1, 0, 1) },
            { Act.Fish,    new Cycle(0.30f, 4, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 4, 4, 2, 2, 3, 0) },
            { Act.Pluck,   new Cycle(0.30f, 1, 0, 0, 1, 2, 2, 1) },
            { Act.Lasso,   new Cycle(0.12f, -1, 0, 1, 2, 3) },
            { Act.Scrape,  new Cycle(0.28f, -1, 0, 1) },
            { Act.Stir,    new Cycle(0.25f, -1, 0, 1, 2, 3) },
            { Act.Knead,   new Cycle(0.25f, 1, 0, 1) },
            { Act.Cleaver, new Cycle(0.20f, 1, 0, 0, 0, 1, 1) },
            { Act.Sew,     new Cycle(0.30f, -1, 0, 0, 1) },
            { Act.Shears,  new Cycle(0.25f, -1, 0, 1) },
            { Act.Shave,   new Cycle(0.30f, -1, 0, 1) },
            { Act.Wipe,    new Cycle(0.30f, -1, 0, 1) },
            { Act.Serve,   new Cycle(0.80f, -1, 0, 1) },
            { Act.Write,   new Cycle(0.25f, -1, 0, 1, 0, 0, 1, 1, 0, 1) },
            { Act.Preach,  new Cycle(0.60f, -1, 0, 1, 2, 1) },
            { Act.Point,   new Cycle(0.80f, -1, 0, 1) },
            { Act.Sweep,   new Cycle(0.30f, 1, 0, 1) },
            { Act.Pull,    new Cycle(0.35f, 1, 0, 1) },
            { Act.Peel,    new Cycle(0.70f, -1, 0, 1) },
            { Act.Tend,    new Cycle(0.60f, -1, 0, 1) },
        };

        /// <summary>Which frame of a work cycle to show at time t (seconds), and whether this is the frame where the tool lands.</summary>
        public static int PhaseAt(Act a, float t, out bool landing)
        {
            landing = false;
            Cycle c;
            if (!cycles.TryGetValue(a, out c)) return 0;
            int i = Mathf.FloorToInt(t / c.Step);
            int n = c.Seq.Length;
            int k = ((i % n) + n) % n, prev = (k + n - 1) % n;
            landing = c.Hit >= 0 && c.Seq[k] == c.Hit && c.Seq[prev] != c.Hit;
            return c.Seq[k];
        }

        /// <summary>How many different frames a work cycle has.</summary>
        public static int Phases(Act a)
        {
            Cycle c;
            if (!cycles.TryGetValue(a, out c)) return 1;
            int m = 0; foreach (int q in c.Seq) m = Mathf.Max(m, q);
            return m + 1;
        }

        /// <summary>Legs planted apart for the heavy swings.</summary>
        static bool Stance(Act a)
        {
            return a == Act.Chop || a == Act.Pick || a == Act.Hoe || a == Act.Scythe || a == Act.Fork || a == Act.Shovel || a == Act.Maul || a == Act.Saw;
        }

        /// <summary>A settler at work: pivot at the feet like the walking sprites, on a wider canvas with room for the tool.</summary>
        public static Sprite Working(Citizen p, int dir, Act act, int phase)
        {
            if (act == Act.None) return Get(p, dir, 0);
            phase = Mathf.Clamp(phase, 0, Phases(act) - 1);
            var L = LookOf(p);
            string key = L.Key + "|w" + dir + "_" + (int)act + "_" + phase;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            bool side = dir == Left || dir == Right;
            var g = Draw(L, dir, side && Stance(act) ? 1 : 0, act, phase);
            s = g.ToSprite(key, new Vector2((5.5f + PadX) / g.W, (1.5f + PadB) / g.H));
            cache[key] = s;
            return s;
        }

        // ---------------------------------------------------------------- drawing kit
        const string Wood = "#a8734a", WoodD = "#8a5a34", Steel = "#9a9aa2", SteelL = "#d0d0d8", SteelD = "#6a6a72",
                     Rope = "#c8a878", Straw = "#d8b860", Hay = "#e0c070", Soil = "#5e3e24", Line = "#e4e0d6";

        sealed class Pen
        {
            readonly PixelCanvas g; readonly int ox, oy;
            public readonly Look L; public readonly int Dir, T, F, Bx, Bw; readonly string arm, armShade;
            readonly List<KeyValuePair<Vector2Int, Color32>> late = new List<KeyValuePair<Vector2Int, Color32>>();
            public Pen(PixelCanvas g, int ox, int oy, Look L, int dir, int ty, int fy, int bx, int bw, string arm, string armShade)
            { this.g = g; this.ox = ox; this.oy = oy; this.L = L; Dir = dir; T = ty; F = fy; Bx = bx; Bw = bw; this.arm = arm; this.armShade = armShade; }

            public bool Side { get { return Dir == Left; } }
            public bool Back { get { return Dir == Up; } }
            /// <summary>Shoulders in the front and back views: screen-left and screen-right.</summary>
            public int SL { get { return Bx - 1; } }
            public int SR { get { return Bx + Bw; } }

            public void Px(int x, int y, string c, bool behind = false)
            {
                int X = x + ox, Y = y + oy;
                if ((behind || Back) && g.Get(X, Y).a > 0) return;
                g.D(X, Y, c);
            }
            public void Rect(int x, int y, int w, int h, string c, bool behind = false) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) Px(xx, yy, c, behind); }

            public static List<Vector2Int> Pts(int x0, int y0, int x1, int y1)
            {
                var r = new List<Vector2Int>();
                int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
                if (n == 0) { r.Add(new Vector2Int(x0, y0)); return r; }
                for (int i = 0; i <= n; i++) r.Add(new Vector2Int(Mathf.RoundToInt(x0 + (x1 - x0) * (float)i / n), Mathf.RoundToInt(y0 + (y1 - y0) * (float)i / n)));
                return r;
            }
            public void Seg(int x0, int y0, int x1, int y1, string c, bool behind = false) { foreach (var q in Pts(x0, y0, x1, y1)) Px(q.x, q.y, c, behind); }

            /// <summary>An arm from the shoulder to the hand. The near arm passes in front of the body but behind the head;
            /// the far arm stays behind the body.</summary>
            public void Arm(int sx, int sy, int hx, int hy, bool near = true)
            {
                string c = near ? arm : armShade;
                var pts = Pts(sx, sy, hx, hy);
                for (int i = 0; i < pts.Count - 1; i++) Px(pts[i].x, pts[i].y, c, !near || pts[i].y < T);
                Px(hx, hy, near ? L.Skin : L.SkinShade, !near || hy < T);
            }
            /// <summary>A bent arm: shoulder to elbow (kept outside the body so it reads), then the forearm a shade darker.</summary>
            public void Arm2(int sx, int sy, int ex, int ey, int hx, int hy)
            {
                foreach (var q in Pts(sx, sy, ex, ey)) Px(q.x, q.y, arm, q.y < T);
                var b = Pts(ex, ey, hx, hy);
                for (int i = 1; i < b.Count - 1; i++) Px(b[i].x, b[i].y, armShade, b[i].y < T);
                Px(hx, hy, L.Skin, hy < T);
            }
            /// <summary>A small box with a shaded bottom and right edge: a parcel, a ledger, a lump of dough.</summary>
            public void Box(int x, int y, int w, int h, string fill, string rim)
            {
                Rect(x, y, w, h, fill); Rect(x, y + h - 1, w, 1, rim); Rect(x + w - 1, y, 1, h, rim);
            }
            /// <summary>An arm hanging at the side as usual (front and back views).</summary>
            public void Rest(int sx) { Rect(sx, T + 1, 1, 2, sx == SR ? armShade : arm); Px(sx, T + 3, sx == SR ? L.SkinShade : L.Skin); }

            /// <summary>Stamps a tool head at the end of a handle: a along the handle, b across it.</summary>
            public void Head(int hx, int hy, int tx, int ty, int a, int b, string c, bool behind = false)
            {
                float dx = tx - hx, dy = ty - hy, m = Mathf.Sqrt(dx * dx + dy * dy);
                if (m < 0.01f) { dx = 0; dy = -1; } else { dx /= m; dy /= m; }
                float nx = -dy, ny = dx;
                Px(Mathf.RoundToInt(tx + dx * a + nx * b), Mathf.RoundToInt(ty + dy * a + ny * b), c, behind);
            }

            /// <summary>Drawn after the outline: thin lines like fishing line and thread.</summary>
            public void Late(int x, int y, string c, byte alpha = 255) { late.Add(new KeyValuePair<Vector2Int, Color32>(new Vector2Int(x + ox, y + oy), PixelCanvas.C(c, alpha))); }
            public void LateSeg(int x0, int y0, int x1, int y1, string c, byte alpha = 255) { foreach (var q in Pts(x0, y0, x1, y1)) Late(q.x, q.y, c, alpha); }
            public void Finish()
            {
                foreach (var kv in late)
                {
                    if (kv.Value.a == 255) g.Set(kv.Key.x, kv.Key.y, kv.Value); else g.Blend(kv.Key.x, kv.Key.y, kv.Value);
                }
            }

            // ---------------- tools
            public void Axe(int hx, int hy, int tx, int ty, bool behind = false)
            {
                Seg(hx, hy, tx, ty, Wood, behind);
                for (int a = -1; a <= 0; a++) for (int b = -1; b <= 1; b++) Head(hx, hy, tx, ty, a, b, Steel, behind);
                Head(hx, hy, tx, ty, 0, -2, SteelL, behind); Head(hx, hy, tx, ty, -1, -2, SteelL, behind);
            }
            public void PickAxe(int hx, int hy, int tx, int ty, bool behind = false)
            {
                Seg(hx, hy, tx, ty, Wood, behind);
                for (int b = -3; b <= 3; b++) Head(hx, hy, tx, ty, 0, b, b == 0 ? SteelD : Steel, behind);
                Head(hx, hy, tx, ty, -1, -3, Steel, behind); Head(hx, hy, tx, ty, -1, 3, Steel, behind);
            }
            public void Hoe(int bx, int by, int tx, int ty, bool behind = false)
            {
                Seg(bx, by, tx, ty, Wood, behind);
                for (int b = 1; b <= 3; b++) Head(bx, by, tx, ty, 0, -b, Steel, behind);
            }
            public void Shovel(int bx, int by, int tx, int ty, bool loaded, bool behind = false)
            {
                Seg(bx, by, tx, ty, Wood, behind);
                for (int a = 0; a <= 2; a++) for (int b = -1; b <= 1; b++) Head(bx, by, tx, ty, a, b, a == 2 ? SteelD : Steel, behind);
                if (loaded) for (int a = 0; a <= 2; a++) { Head(bx, by, tx, ty, a, -2, Soil, behind); if (a == 1) Head(bx, by, tx, ty, a, -3, Soil, behind); }
            }
            public void Fork(int bx, int by, int tx, int ty, bool hay, bool behind = false)
            {
                Seg(bx, by, tx, ty, Wood, behind);
                for (int b = -1; b <= 1; b++) Head(bx, by, tx, ty, 0, b, SteelD, behind);
                for (int a = 1; a <= 3; a++) { Head(bx, by, tx, ty, a, -1, SteelD, behind); Head(bx, by, tx, ty, a, 1, SteelD, behind); }
                if (hay) for (int a = 1; a <= 3; a++) for (int b = -2; b <= 2; b++) if ((a + b) % 2 == 0 || b == 0) Head(bx, by, tx, ty, a, b - 1, (a + b) % 3 == 0 ? Straw : Hay, behind);
            }
            public void Maul(int hx, int hy, int tx, int ty, bool behind = false)
            {
                Seg(hx, hy, tx, ty, Wood, behind);
                for (int a = 0; a <= 1; a++) for (int b = -2; b <= 1; b++) Head(hx, hy, tx, ty, a, b, a == 1 ? SteelD : "#7a7a82", behind);
            }
            public void Scythe(int bx, int by, int tx, int ty, int dirX, bool behind = false)
            {
                Seg(bx, by, tx, ty, Wood, behind);
                for (int k = 1; k <= 5; k++) Px(tx + dirX * k, ty + (k == 5 ? -1 : 0), SteelL, behind);
            }
            public void Broom(int bx, int by, int tx, int ty, bool behind = false)
            {
                Seg(bx, by, tx, ty, Wood, behind);
                for (int a = 0; a <= 2; a++) for (int b = -1 - a / 2; b <= 1 + a / 2; b++) Head(bx, by, tx, ty, a, b, a == 2 ? "#b89a48" : Straw, behind);
            }
            public void Pole(int bx, int by, int tx, int ty, bool behind = false) { Seg(bx, by, tx, ty, WoodD, behind); }
        }

        // ---------------------------------------------------------------- the poses
        static void Pose(Pen k, Act act, int ph)
        {
            int T = k.T, F = k.F;
            if (k.Side)
            {
                switch (act)
                {
                    case Act.Chop:
                    case Act.Pick:
                    case Act.Maul:
                    {
                        // wind up over the back shoulder, overhead, then down
                        int hx, hy, tx, ty; bool behind = false;
                        if (ph == 0) { hx = 7; hy = T - 3; tx = 10; ty = T - 10; behind = true; }
                        else if (ph == 1) { hx = 3; hy = T - 12; tx = -1; ty = T - 18; }
                        else if (act == Act.Chop) { hx = -1; hy = T + 2; tx = -7; ty = T + 1; }
                        else if (act == Act.Pick) { hx = -1; hy = T + 3; tx = -6; ty = F - 1; }
                        else { hx = -1; hy = T + 2; tx = -4; ty = F - 3; }
                        k.Arm(3, T + 1, hx + (ph == 2 ? 1 : 0), hy, false);
                        if (act == Act.Chop) k.Axe(hx, hy, tx, ty, behind);
                        else if (act == Act.Pick) k.PickAxe(hx, hy, tx, ty, behind);
                        else k.Maul(hx, hy, tx, ty, behind);
                        k.Arm(4, T + 1, hx, hy);
                        break;
                    }
                    case Act.Hoe:
                        if (ph == 0) { k.Arm(3, T + 1, 1, T + 1, false); k.Hoe(3, T + 3, -4, T - 8); k.Arm(4, T + 1, 0, T - 1); }
                        else { k.Arm(3, T + 1, 0, T + 3, false); k.Hoe(2, T + 1, -6, F - 2); k.Arm(4, T + 1, -1, T + 2); }
                        break;
                    case Act.Scythe:
                        if (ph == 0) { k.Arm(3, T + 1, 6, T + 3, false); k.Scythe(4, T - 1, 9, F, -1, true); k.Arm(4, T + 1, 5, T + 1); }
                        else if (ph == 1) { k.Arm(3, T + 1, 3, T + 3, false); k.Scythe(2, T, 2, F + 1, -1); k.Arm(4, T + 1, 2, T + 2); }
                        else { k.Arm(3, T + 1, 0, T + 3, false); k.Scythe(1, T, -4, F, -1); k.Arm(4, T + 1, -1, T + 2); }
                        break;
                    case Act.Sow:
                        k.Arm(3, T + 1, 3, T + 3, false);
                        k.Seg(2, T, 5, T + 2, "#8a6a4a");
                        k.Rect(4, T + 2, 3, 3, "#d8c49a"); k.Px(6, T + 3, "#b8a478"); k.Px(5, T + 2, "#b8a478");
                        if (ph == 0) k.Arm(4, T + 1, 5, T + 2);
                        else if (ph == 1) { k.Arm(4, T + 1, -2, T - 1); k.Px(-3, T - 2, "#e8d890"); }
                        else k.Arm(4, T + 1, 0, T + 2);
                        break;
                    case Act.Fork:
                        if (ph == 0) { k.Arm(3, T + 1, 2, T, false); k.Fork(3, T - 2, -5, F - 1, false); k.Arm(4, T + 1, 0, T + 1); }
                        else if (ph == 1) { k.Arm(3, T + 1, 2, T - 1, false); k.Fork(3, T + 1, -4, T - 9, true); k.Arm(4, T + 1, 0, T - 3); }
                        else { k.Arm(3, T + 1, 1, T - 4, false); k.Fork(2, T, -5, T - 13, false); k.Arm(4, T + 1, -1, T - 6); }
                        break;
                    case Act.Shovel:
                        if (ph == 0) { k.Arm(3, T + 1, 2, T, false); k.Shovel(3, T - 2, -3, F - 2, false); k.Arm(4, T + 1, 0, T + 2); }
                        else if (ph == 1) { k.Arm(3, T + 1, 2, T + 2, false); k.Shovel(3, T + 1, -5, T + 3, true); k.Arm(4, T + 1, -1, T + 2); }
                        else { k.Arm(3, T + 1, 1, T + 1, false); k.Shovel(2, T + 2, -5, T - 5, false); k.Arm(4, T + 1, -1, T - 1); }
                        break;
                    case Act.Saw:
                    {
                        int s = ph == 0 ? 0 : -3;
                        k.Arm(3, T + 1, 1 + s, T + 3, false);
                        // a bucksaw: blade below, a wooden frame above
                        k.Seg(s, T + 3, s - 8, T + 3, Steel); for (int x = s - 7; x <= s; x += 2) k.Px(x, T + 4, SteelD);
                        k.Seg(s, T, s - 8, T, Wood); k.Seg(s, T, s, T + 3, Wood); k.Seg(s - 8, T, s - 8, T + 3, Wood);
                        k.Arm(4, T + 1, s, T + 2);
                        break;
                    }
                    case Act.Fish:
                        FishSide(k, ph);
                        break;
                    case Act.Lasso:
                        LassoLoop(k, 6, T - 10, ph);
                        k.Arm(3, T + 1, 2, T + 3, false);
                        k.Arm(4, T + 1, 5 + (ph == 1 ? -1 : ph == 3 ? 1 : 0), T - 8);
                        break;
                    case Act.Scrape:
                    {
                        int hx = ph == 0 ? -1 : -3, hy = ph == 0 ? T + 1 : T + 3;
                        k.Arm(3, T + 1, hx + 1, hy, false);
                        k.Rect(hx - 1, hy + 1, 2, 1, SteelL);
                        k.Arm(4, T + 1, hx, hy);
                        break;
                    }
                    case Act.Stir:
                    {
                        int[] hx = { 0, -1, 0, 1 }, px = { 0, -2, 0, 2 };
                        k.Arm(3, T + 1, hx[ph] + 1, T + 2, false);
                        k.Pole(hx[ph] + 2, T - 3, px[ph] - 4, F - 1);
                        k.Arm(4, T + 1, hx[ph], T + 1);
                        break;
                    }
                    case Act.Sew:
                        k.Rect(-3, T + 3, 4, 1, "#8a6a9a"); k.Px(-3, T + 4, "#6a4a7a");
                        k.Arm(3, T + 1, -2, T + 3, false);
                        if (ph == 0) k.Arm(4, T + 1, -1, T + 3);
                        else { k.Arm(4, T + 1, 0, T - 1); k.LateSeg(-1, T + 2, 0, T - 2, Line); }
                        break;
                    case Act.Shave:
                    {
                        int y = ph == 0 ? T - 2 : T;
                        k.Arm(3, T + 1, -2, T - 3, false);
                        k.Arm(4, T + 1, -2, y);
                        k.Px(-3, y - 1, SteelL); k.Px(-3, y, SteelL);
                        break;
                    }
                    case Act.Sweep:
                    {
                        int bx = ph == 0 ? -4 : -1;
                        k.Arm(3, T + 1, 1, T + 3, false);
                        k.Broom(2, T - 2, bx, F - 1);
                        k.Arm(4, T + 1, 0, T + 1);
                        break;
                    }
                    case Act.Peel:
                    {
                        int tx = ph == 0 ? -10 : -6;
                        k.Arm(3, T + 1, 1, T + 2, false);
                        k.Pole(3, T + 3, tx, T);
                        for (int b = -1; b <= 1; b++) for (int a = 0; a <= 1; a++) k.Px(tx - a - 1, T + b, Wood);
                        if (ph == 1) { k.Rect(tx - 3, T - 2, 3, 1, "#c8863a"); }
                        k.Arm(4, T + 1, -1, T + 2);
                        break;
                    }
                    case Act.Pull:
                    {
                        int y = ph == 0 ? T - 8 : T - 3;
                        k.Seg(-1, T - 14, -1, y - 1, Rope);
                        k.Arm(3, T + 1, -1, y + 2, false);
                        k.Arm(4, T + 1, -1, y);
                        break;
                    }
                    default:   // hands busy at a bench, a counter or a sickbed
                    {
                        int y = ph == 0 ? T + 2 : T + 3;
                        k.Arm(3, T + 1, -1, T + 3, false);
                        k.Arm(4, T + 1, -2, y);
                        break;
                    }
                }
                return;
            }

            // front (Down) and back (Up) views
            int SL = k.SL, SR = k.SR;
            switch (act)
            {
                case Act.Fish:
                    if (k.Back) FishBack(k, ph); else FishFront(k, ph);
                    break;
                case Act.Pluck:
                {
                    // a basket on the hip, reaching up into the branches
                    k.Box(SL - 4, T + 3, 4, 3, "#b8844a", "#8a5a34"); k.Px(SL - 3, T + 2, WoodD); k.Px(SL - 2, T + 2, WoodD);
                    if (ph == 0) { k.Arm(SL, T + 1, SL - 1, T - 7); k.Px(SL - 2, T - 8, "#d84a3a"); }
                    else if (ph == 1) k.Arm2(SL, T + 1, SL - 2, T, SL - 2, T - 3);
                    else { k.Arm2(SL, T + 1, SL - 1, T + 2, SL - 2, T + 3); k.Px(SL - 3, T + 3, "#d84a3a"); }
                    k.Rest(SR);
                    break;
                }
                case Act.Lasso:
                    LassoLoop(k, SR + 3, T - 10, ph);
                    k.Arm(SR, T + 1, SR + 1 + (ph == 1 ? -1 : ph == 3 ? 1 : 0), T - 8);
                    k.Rest(SL); k.Rect(SL - 1, T + 3, 2, 2, Rope); k.Px(SL - 2, T + 4, Rope);
                    break;
                case Act.Knead:
                    if (ph == 0)
                    {
                        k.Box(2, T + 4, 5, 2, "#f0e0c0", "#c8b088"); k.Px(3, T + 4, "#fff4dc");
                        k.Arm2(SL, T + 1, SL - 1, T + 3, 2, T + 4); k.Arm2(SR, T + 1, SR + 1, T + 3, 6, T + 4);
                    }
                    else
                    {
                        k.Box(1, T + 5, 7, 1, "#f0e0c0", "#c8b088"); k.Rect(2, T + 4, 5, 1, "#f0e0c0");
                        k.Arm2(SL, T + 1, SL, T + 3, 3, T + 4); k.Arm2(SR, T + 1, SR, T + 3, 5, T + 4);
                    }
                    break;
                case Act.Cleaver:
                    k.Box(1, T + 4, 4, 2, "#a0503a", "#7a3a2a"); k.Px(1, T + 4, "#e8d8c0");
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 2, T + 4);
                    if (ph == 0) { k.Arm(SR, T + 1, SR + 1, T - 3); k.Px(SR + 1, T - 4, WoodD); k.Box(SR, T - 7, 3, 3, Steel, SteelD); k.Rect(SR, T - 7, 1, 3, SteelL); }
                    else { k.Arm2(SR, T + 1, SR + 1, T + 3, 5, T + 4); k.Box(4, T + 5, 3, 2, Steel, SteelD); k.Rect(4, T + 6, 3, 1, SteelL); }
                    break;
                case Act.Shears:
                    k.Box(0, T + 4, 9, 2, "#5a7aaa", "#46628a"); k.Px(2, T + 4, "#7a9aca");
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 2, T + 4);
                    k.Arm2(SR, T + 1, SR + 1, T + 3, 6, T + 4);
                    if (ph == 0) { k.Px(5, T + 5, SteelL); k.Px(7, T + 5, SteelL); k.Px(4, T + 6, SteelL); k.Px(8, T + 6, SteelL); }
                    else { k.Px(6, T + 5, SteelL); k.Px(6, T + 6, SteelL); k.Px(6, T + 7, SteelL); }
                    break;
                case Act.Wipe:
                    k.Arm2(SL, T + 1, SL - 1, T + 3, SL, T + 4);
                    if (ph == 0) { k.Arm2(SR, T + 1, SR + 1, T + 3, SR + 2, T + 4); k.Rect(SR + 1, T + 5, 3, 1, "#f4f0e6"); }
                    else { k.Arm2(SR, T + 1, SR, T + 3, 3, T + 4); k.Rect(2, T + 5, 3, 1, "#f4f0e6"); }
                    break;
                case Act.Serve:
                {
                    int y = ph == 0 ? T + 1 : T + 3;
                    k.Box(2, y, 5, 3, "#c8a878", "#a08458"); k.Rect(4, y, 1, 3, WoodD); k.Rect(2, y + 1, 5, 1, WoodD);
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 1, y + 1); k.Arm2(SR, T + 1, SR + 1, T + 3, 7, y + 1);
                    break;
                }
                case Act.Write:
                {
                    int x = ph == 0 ? 6 : 4;
                    k.Box(0, T + 4, 9, 2, "#f4ecd8", "#d8ccb4"); k.Rect(1, T + 4, 2, 1, "#8a8070"); k.Px(6, T + 4, "#8a8070");
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 1, T + 4);
                    k.Arm2(SR, T + 1, SR + 1, T + 3, x, T + 4);
                    k.Px(x + 1, T + 3, "#f4f0e6"); k.Px(x + 2, T + 2, "#f4f0e6"); k.Px(x + 2, T + 1, "#d8d4c8");
                    break;
                }
                case Act.Preach:
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 2, T + 3);
                    k.Box(1, T + 1, 3, 3, "#2a2420", "#1a1418"); k.Rect(3, T + 1, 1, 3, "#c8a050");
                    if (ph == 0) k.Arm(SR, T + 1, SR + 2, T - 6);
                    else if (ph == 1) k.Arm(SR, T + 1, SR + 3, T + 1);
                    else k.Arm(SR, T + 1, SR + 2, T - 3);
                    break;
                case Act.Point:
                    k.Rest(SL);
                    if (ph == 0) { k.Seg(SR + 2, T - 3, SR + 4, T - 9, WoodD); k.Arm(SR, T + 1, SR + 1, T - 2); }
                    else { k.Seg(SR + 2, T - 1, SR + 5, T - 6, WoodD); k.Arm(SR, T + 1, SR + 1, T); }
                    break;
                case Act.Sweep:
                {
                    int bx = ph == 0 ? 1 : 7;
                    k.Broom(SR + 1, T - 1, bx, F);
                    k.Arm2(SL, T + 1, SL - 1, T + 3, SR - 1, T + 3); k.Arm(SR, T + 1, SR + 1, T + 1);
                    break;
                }
                case Act.Stir:
                {
                    int[] dx = { -2, 0, 2, 0 };
                    if (k.Back) k.Pole(4, T + 2, 4 + dx[ph], T - 10);
                    else k.Pole(4 + dx[ph], T - 2, 4, F + 2);
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 3 + dx[ph] / 2, T + 3); k.Arm2(SR, T + 1, SR + 1, T + 3, 5 + dx[ph] / 2, T + 3);
                    break;
                }
                case Act.Peel:
                {
                    // the long baker's peel into the oven
                    int top = ph == 0 ? T - 13 : T - 9;
                    k.Pole(4, T + 2, 4, top + 2);
                    k.Rect(3, top, 3, 2, Wood);
                    if (ph == 1) k.Rect(3, top - 1, 3, 1, "#c8863a");
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 3, T + 3); k.Arm2(SR, T + 1, SR + 1, T + 3, 5, T + 2);
                    break;
                }
                case Act.Pull:
                {
                    int y = ph == 0 ? T - 7 : T - 2;
                    k.Seg(SR + 1, T - 13, SR + 1, y - 1, Rope);
                    k.Arm(SR, T + 1, SR + 1, y);
                    k.Arm2(SL, T + 1, SL - 1, T + 2, SL, T + 3);
                    break;
                }
                default:
                {
                    int d = ph % 2;
                    k.Arm2(SL, T + 1, SL - 1, T + 3, 3, T + 4 - d); k.Arm2(SR, T + 1, SR + 1, T + 3, 5, T + 3 + d);
                    break;
                }
            }
        }

        static void LassoLoop(Pen k, int cx, int cy, int ph)
        {
            // the loop spins: it swings round the hand, a thin rope with no outline
            int[] ox = { -2, 0, 2, 0 }, oy = { 0, -1, 0, 1 };
            float x0 = cx + ox[ph], y0 = cy - 2 + oy[ph];
            var seen = new HashSet<int>();
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2;
                int x = Mathf.RoundToInt(x0 + Mathf.Cos(a) * 5f), y = Mathf.RoundToInt(y0 + Mathf.Sin(a) * 2f);
                if (seen.Add(x * 100 + y)) k.Late(x, y, Mathf.Sin(a) > 0 ? "#a8885a" : Rope);
            }
            k.LateSeg(cx - 2, cy + 2, Mathf.RoundToInt(x0 - 3), Mathf.RoundToInt(y0 + 1), Rope);
        }

        static void FishSide(Pen k, int ph)
        {
            int T = k.T, F = k.F;
            switch (ph)
            {
                case 0: case 1:
                {
                    int tip = ph == 0 ? T - 5 : T - 4;
                    k.Arm(3, T + 1, 0, T + 2, false);
                    k.Seg(1, T + 3, -11, tip, WoodD);
                    k.Arm(4, T + 1, -1, T + 1);
                    k.LateSeg(-12, tip + 1, -12, F + (ph == 0 ? 0 : 1), Line, 200);
                    k.Late(-12, F + (ph == 0 ? 1 : 2), "#d84a3a"); if (ph == 0) k.Late(-12, F, "#f4f0e6");
                    break;
                }
                case 2:   // drawn back to cast
                    k.Arm(3, T + 1, 2, T, false);
                    k.Seg(0, T + 2, 6, T - 12, WoodD, true);
                    k.Arm(4, T + 1, 1, T);
                    break;
                case 3:   // the cast
                    k.Arm(3, T + 1, 0, T, false);
                    k.Seg(1, T + 1, -10, T - 9, WoodD);
                    k.Arm(4, T + 1, -1, T);
                    k.LateSeg(-11, T - 9, -14, T - 4, Line, 200);
                    break;
                default:  // a bite: the rod bends up and the line goes taut
                    k.Arm(3, T + 1, 1, T, false);
                    k.Seg(1, T + 2, -9, T - 9, WoodD);
                    k.Arm(4, T + 1, 0, T);
                    k.LateSeg(-10, T - 8, -13, F + 1, Line, 220);
                    break;
            }
        }

        static void FishBack(Pen k, int ph)
        {
            // facing out over the water, the rod held up and out
            int T = k.T, SR = k.SR, SL = k.SL;
            k.Arm2(SL, T + 1, SL - 1, T + 3, 4, T + 3);
            switch (ph)
            {
                case 0: case 1:
                    k.Seg(SR, T + 1, SR + 3, T - 13, WoodD);
                    k.LateSeg(SR + 3, T - 12, SR + 3, T - (ph == 0 ? 9 : 8), Line, 200);
                    k.Late(SR + 3, T - (ph == 0 ? 8 : 7), "#d84a3a"); if (ph == 0) k.Late(SR + 3, T - 9, "#f4f0e6");
                    break;
                case 2: k.Seg(SR, T + 1, SR + 1, T - 14, WoodD); break;
                case 3: k.Seg(SR, T + 1, SR + 5, T - 11, WoodD); break;
                default:
                    k.Seg(SR, T + 1, SR + 2, T - 14, WoodD);
                    k.LateSeg(SR + 2, T - 13, SR + 4, T - 7, Line, 220);
                    break;
            }
            k.Arm(SR, T + 1, SR, T + 2);
        }

        static void FishFront(Pen k, int ph)
        {
            int T = k.T, F = k.F, SR = k.SR, SL = k.SL;
            switch (ph)
            {
                case 0: case 1:
                    k.Arm(SL, T + 1, 4, T + 3);
                    k.Seg(5, T + 2, 11, F + 3, WoodD);
                    k.Arm(SR, T + 1, 6, T + 3);
                    k.LateSeg(11, F + 4, 11, F + (ph == 0 ? 7 : 8), Line, 200);
                    k.Late(11, F + (ph == 0 ? 8 : 9), "#d84a3a"); if (ph == 0) k.Late(11, F + 7, "#f4f0e6");
                    break;
                case 2:
                    k.Arm(SL, T + 1, SL, T - 2);
                    k.Seg(SR + 1, T, SR + 3, T - 12, WoodD, true);
                    k.Arm(SR, T + 1, SR + 1, T - 1);
                    break;
                case 3:
                    k.Arm(SL, T + 1, 4, T + 2);
                    k.Seg(5, T + 1, 12, F + 1, WoodD);
                    k.Arm(SR, T + 1, 6, T + 2);
                    break;
                default:
                    k.Arm(SL, T + 1, 4, T + 1);
                    k.Seg(5, T + 1, 11, T - 4, WoodD);
                    k.Arm(SR, T + 1, 6, T + 1);
                    k.LateSeg(11, T - 3, 12, F + 8, Line, 220);
                    break;
            }
        }
    }
}
