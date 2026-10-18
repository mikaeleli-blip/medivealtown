using System.Collections.Generic;
using System.Text;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game.Art
{
    /// <summary>
    /// Folk in a chunky, big-headed farm-sim style: 4 facing directions, a 3-frame walk.
    /// Everyone gets their own look from their id: complexion, hair, beards, build and height.
    /// Rank shows in their clothes: serfs in undyed and earthy wool with hoods and coifs, freemen in brighter dyed tunics,
    /// the priest's black cassock and tonsure, the knight's mail and surcoat, the lord's rich gown and gold circlet.
    /// </summary>
    public static partial class PeopleArt
    {
        public const int Down = 0, Up = 1, Left = 2, Right = 3;
        const string OL = "#2b1d1a";

        // complexions: pale, fair, peach, ruddy, weathered
        static readonly string[][] Skins = { new[] { "#fbe3cf", "#ebc8ae" }, new[] { "#f6d2b0", "#e3b48e" }, new[] { "#f3c9a6", "#dfad88" }, new[] { "#f0c0a0", "#d9a080" }, new[] { "#ecbe98", "#d4a27c" } };
        // black, dark brown, brown, chestnut, light brown, auburn, red, ginger, strawberry, dark blond, blond, flaxen
        static readonly string[] Hairs = { "#2e2420", "#3b2618", "#5a3a22", "#7a4a26", "#8a6a42", "#8a3a1a", "#a8461e", "#c86a2a", "#d0905a", "#b08a4a", "#d8b060", "#e8d08a" };
        // undyed and cheaply dyed wool: oatmeal, grey, brown, russet, woad blue faded, weld yellow faded, sage
        static readonly string[] SerfTunics = { "#a89a80", "#9a8e7a", "#8a7a62", "#7a6a52", "#8a5a3a", "#9a6a4a", "#6a7a8a", "#a8986a", "#7a8a6a", "#b0a088" };
        static readonly string[] SerfKirtles = { "#a89a80", "#8a7a62", "#7a5a4a", "#6a7a8a", "#8a6a5a", "#a08a6a", "#7a8a6a", "#9a7a6a" };
        static readonly string[] FreeTunics = { "#4a6fa5", "#5a8a4a", "#a8553a", "#b5432f", "#c49a3a", "#3e5a7a", "#6a8a5a", "#8a3a4a", "#5a7a8a" };
        static readonly string[] FreeGowns = { "#4a5a8a", "#6a8a5a", "#a8553a", "#8a3a4a", "#c8a060", "#5a7a7a", "#7a5a8a", "#b56a6a" };
        static readonly string[] Hose = { "#5a4a3a", "#6a5a4a", "#4a4440", "#7a6a52", "#5a5a52", "#4a3a2e" };
        static readonly string[] Hoods = { "#7a5a3a", "#5a6a4a", "#8a3a2a", "#4a5a6a", "#9a8a6a", "#6a4a3a", "#3e5a3a" };
        static readonly string[] Veils = { "#f0ece0", "#e8e0cc", "#f4f0e6", "#e0d8c4" };
        static readonly string[] NobleGowns = { "#6a2a4a", "#2e3e7a", "#7a2a2a", "#2e5a3a", "#5a2a6a" };
        static readonly string[] Mantles = { "#8a1e2a", "#1e2e6a", "#2e4a2e", "#4a1e4a" };

        sealed class Look
        {
            public bool Female, Child;
            public string Skin, SkinShade, Hair, HairShade, Shirt, ShirtShade, Pant, PantShade, Shoe = "#4a2f1e";
            public string Hat;           // null, straw, cowboy, top, bowler, cap, parson, slouch, bonnet, kerchief
            public string HatColor;
            public string HairStyle;     // men: short, parted, shaggy, curly; women and girls: long, bun, braids, updo
            public bool Bald, Freckles, Blush;
            public string Beard, BeardColor;   // full, mustache, chops, goatee, long
            public string Pattern;       // plain, check, stripe, print
            public string Vest, Jacket, Neck, Shawl;
            public bool Coat, Badge, Suspenders, Collar, Stout, Tall, Short, Mail, Tonsure;
            public string Apron;         // white for the butcher, baker, barber and many a housewife; leather for smith and tanner
            public string Key;
        }

        static string Darker(string hex, float k = 0.78f)
        {
            var c = PixelCanvas.C(hex);
            return "#" + ((int)(c.r * k)).ToString("x2") + ((int)(c.g * k)).ToString("x2") + ((int)(c.b * k)).ToString("x2");
        }

        /// <summary>Where someone works, so their clothes can show their trade. Set by the map view.</summary>
        public static System.Func<Citizen, BuildingType?> WorkplaceOf;
        /// <summary>Whether someone is the reigning lord or regent (the circlet and mantle). Set by the map view.</summary>
        public static System.Func<Citizen, bool> Rules;
        /// <summary>The lord's colours, for the knight's surcoat. Set by the map view.</summary>
        public static string HouseColour = "#b5432f";

        static int Stem(int id) { return id * 7919 + 13; }
        static float H(int stem, int k) { return Noise.Hash(stem, k, 4111); }
        static T Pick<T>(T[] a, float r) { return a[Mathf.Clamp((int)(r * a.Length), 0, a.Length - 1)]; }

        sealed class Cached { public Look L; public long Print; }
        static readonly Dictionary<int, Cached> looks = new Dictionary<int, Cached>();

        /// <summary>A look changes only with rank, trade, coming of age and greying; reuse it otherwise.</summary>
        static Look LookOf(Citizen p)
        {
            if (p.Id <= 0) return Build(p, null);
            var w = WorkplaceOf != null && p.Rank == Rank.Freeman ? WorkplaceOf(p) : null;
            bool rules = Rules != null && Rules(p);
            long print = ((((long)p.Rank * 64 + (w.HasValue ? (int)w.Value + 1 : 0)) * 2 + (rules ? 1 : 0)) * 64 + (int)(p.Age / 3f)) * 4 + (p.Female ? 1 : 0);
            Cached c;
            if (looks.TryGetValue(p.Id, out c) && c.Print == print) return c.L;
            c = new Cached { L = Build(p, w), Print = print };
            looks[p.Id] = c;
            return c.L;
        }

        static Look Build(Citizen p, BuildingType? work)
        {
            int s = p.Id > 0 ? Stem(p.Id) : (int)(p.Seed * 1000003f);
            System.Func<int, float> R = k => H(s, k);
            var L = new Look { Female = p.Female, Child = p.IsChild };

            // complexion and hair run in families: a child takes after its mother or its father
            int skinFrom = s, hairFrom = s;
            if (p.IsChild)
            {
                int a = R(1) < 0.5f ? p.MotherId : p.FatherId, b = R(2) < 0.5f ? p.MotherId : p.FatherId;
                if (a <= 0) a = p.MotherId > 0 ? p.MotherId : p.FatherId;
                if (b <= 0) b = p.MotherId > 0 ? p.MotherId : p.FatherId;
                if (a > 0) hairFrom = Stem(a);
                if (b > 0) skinFrom = Stem(b);
            }
            int si = Mathf.Clamp((int)(H(skinFrom, 4) * Skins.Length), 0, Skins.Length - 1);
            L.Skin = Skins[si][0]; L.SkinShade = Skins[si][1];
            L.Hair = Pick(Hairs, H(hairFrom, 3));
            if (p.IsChild && p.Age < 8 && R(5) < 0.35f) L.Hair = Lighter(L.Hair);
            if (p.Age > 62) L.Hair = "#e0dcd6";
            else if (p.Age > 55) L.Hair = "#b8b0a8";
            else if (p.Age > 47 && R(6) < 0.5f) L.Hair = "#8a8078";
            L.HairShade = Darker(L.Hair, 0.75f);
            L.Freckles = si <= 2 && R(11) < 0.25f;
            L.Blush = L.Female || L.Child || si == 3 || R(10) < 0.3f;
            if (!L.Child) { L.Stout = R(12) < 0.15f; float h = R(13); L.Tall = h < 0.25f; L.Short = h > 0.85f; }

            if (L.Female)
            {
                float hs = R(7);
                L.HairStyle = L.Child ? (hs < 0.45f ? "long" : hs < 0.9f ? "braids" : "bun") : (hs < 0.4f ? "long" : hs < 0.75f ? "braids" : "bun");
            }
            else
            {
                L.HairStyle = Pick(new[] { "short", "shaggy", "curly", "short" }, R(7));
                L.Bald = !L.Child && p.Age > 40 && R(8) < (p.Age - 40) / 40f;
                if (!L.Child && p.Age >= 19)
                {
                    float r = R(9);
                    L.Beard = r < 0.35f ? null : r < 0.65f ? "full" : r < 0.75f ? "mustache" : r < 0.88f ? "goatee" : "long";
                    if (L.Beard == "long" && p.Age < 35) L.Beard = "full";
                    L.BeardColor = L.Hair;
                    if ((L.Beard == "full" || L.Beard == "long") && L.HairStyle == "shaggy") L.HairStyle = "short";
                }
            }
            L.Pattern = "plain";
            L.Pant = Pick(Hose, R(29));
            L.Shoe = R(32) < 0.6f ? "#4a2f1e" : "#3a2a1e";
            bool free = p.Rank == Rank.Freeman;

            // everyday clothes by station
            if (L.Female)
            {
                L.Shirt = free ? Pick(FreeGowns, R(17)) : Pick(SerfKirtles, R(17));
                if (L.Child) { if (R(18) < 0.35f) L.Apron = "#e8e0cc"; if (R(19) < 0.3f) { L.Hat = "coif"; L.HatColor = "#f0ece0"; } }
                else
                {
                    if (R(18) < 0.4f) L.Apron = "#e8e0cc";
                    // a married woman covers her hair
                    if (p.PartnerId >= 0 || p.Age > 30 || R(19) < 0.4f) { L.Hat = R(20) < 0.6f ? "veil" : "coif"; L.HatColor = Pick(Veils, R(21)); }
                    if (p.Age > 40 && R(22) < 0.3f) L.Shawl = Pick(Hoods, R(23));
                }
            }
            else
            {
                L.Shirt = L.Child ? Pick(SerfTunics, R(17)) : free ? Pick(FreeTunics, R(17)) : Pick(SerfTunics, R(17));
                if (L.Child) { if (R(19) < 0.35f) { L.Hat = "coif"; L.HatColor = "#e8e0cc"; } }
                else
                {
                    float hr = R(19);
                    if (hr < 0.4f) { L.Hat = "hood"; L.HatColor = Pick(Hoods, R(20)); }
                    else if (hr < 0.55f) { L.Hat = "coif"; L.HatColor = "#ece6d8"; }
                    else if (hr < 0.75f) { L.Hat = "straw"; L.HatColor = Pick(new[] { "#e0c070", "#d8b860", "#e8d088" }, R(20)); }
                    else if (free && hr < 0.9f) { L.Hat = "felt"; L.HatColor = Pick(Hoods, R(20)); }
                    if (R(25) < 0.3f) L.Neck = Pick(Hoods, R(26));   // a hood down round the shoulders
                }
            }

            if (!L.Child)
                switch (p.Rank)
                {
                    case Rank.Reeve:
                        L.Shirt = "#3e6a3a"; L.Hat = "hood"; L.HatColor = "#2e4a2e"; L.Neck = "#2e4a2e"; break;
                    case Rank.Priest:
                        L.Shirt = "#24202a"; L.Pant = "#24202a"; L.Coat = true; L.Collar = true; L.Hat = null; L.Neck = null; L.Apron = null; L.Shawl = null;
                        L.Tonsure = true; L.Bald = false; L.Beard = null; L.HairStyle = "short";
                        break;
                    case Rank.Knight:
                        L.Mail = true; L.Shirt = "#efe6d2"; L.Jacket = "#8a8e96"; L.Pant = "#7a7e86"; L.Hat = "helm"; L.HatColor = "#9a9ea6";
                        L.Neck = null; L.Apron = null; L.Badge = true; L.Shoe = "#3a3a3a";
                        break;
                    case Rank.Lord: case Rank.Noble:
                    {
                        bool rules = Rules != null && Rules(p);
                        L.Apron = null; L.Shawl = null; L.Neck = null;
                        if (L.Female)
                        {
                            L.Shirt = Pick(NobleGowns, R(35)); L.Hat = "veil"; L.HatColor = "#fffaf0";
                            if (rules || p.Rank == Rank.Lord) L.Hat = "crownveil";
                        }
                        else
                        {
                            L.Shirt = Pick(NobleGowns, R(35)); L.Pant = "#3a2a3a";
                            L.Hat = rules || p.Rank == Rank.Lord ? "circlet" : (R(36) < 0.5f ? "felt" : null); L.HatColor = rules ? "#e0b04a" : "#4a2a3a";
                        }
                        if (rules || p.Rank == Rank.Lord) { L.Jacket = Pick(Mantles, R(37)); L.Coat = true; }
                        L.Shoe = "#2a1e1a";
                        break;
                    }
                }
            else if (p.IsNoble) { L.Shirt = Pick(NobleGowns, R(35)); L.Apron = null; if (L.Hat == "coif") L.Hat = null; }
            L.ShirtShade = Darker(L.Shirt); L.PantShade = Darker(L.Pant);

            // the trade shows: a floury apron for the miller, leather for the woodward, the alewife's white apron
            if (!L.Child && work.HasValue)
            {
                var wt = work.Value;
                if (wt == BuildingType.Mill || wt == BuildingType.Alehouse) L.Apron = "#f4f0e6";
                else if (wt == BuildingType.Woodcutter || wt == BuildingType.Quarry) L.Apron = "#6e4428";
                if (wt == BuildingType.Mill && !L.Female) { L.Hat = "coif"; L.HatColor = "#f4f0e6"; }
            }

            var sb = new StringBuilder(96);
            sb.Append(L.Female ? 'f' : 'm').Append(L.Child ? 'c' : 'a').Append(L.Skin).Append(L.Hair).Append(L.HairStyle).Append(L.Bald ? 'B' : '-')
             .Append(L.Freckles ? 'F' : '-').Append(L.Blush ? 'R' : '-').Append(L.Beard ?? "-").Append(L.BeardColor ?? "-")
             .Append(L.Shirt).Append(L.Pattern).Append(L.Pant).Append(L.Shoe).Append(L.Hat ?? "-").Append(L.HatColor ?? "-")
             .Append(L.Vest ?? "-").Append(L.Jacket ?? "-").Append(L.Neck ?? "-").Append(L.Shawl ?? "-").Append(L.Apron ?? "-")
             .Append(L.Coat ? 'c' : '-').Append(L.Badge ? 'b' : '-').Append(L.Suspenders ? 's' : '-').Append(L.Collar ? 'k' : '-')
             .Append(L.Stout ? 'S' : '-').Append(L.Tall ? 'T' : L.Short ? 't' : '-').Append(L.Mail ? 'M' : '-').Append(L.Tonsure ? 'O' : '-');
            L.Key = sb.ToString();
            return L;
        }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Settler sprite, pivot at the feet. frame: 0 standing, 1 and 2 mid-stride.</summary>
        public static Sprite Get(Citizen p, int dir, int frame)
        {
            var L = LookOf(p);
            string key = L.Key + dir + frame;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var g = Draw(L, dir, frame);
            s = g.ToSprite(key, new Vector2(5.5f / 11f, 1.5f / g.H));
            cache[key] = s;
            return s;
        }

        /// <summary>A settler asleep (or ill) in bed: just the head on the pillow. Pivot at the top left.</summary>
        public static Sprite Sleeper(Citizen p)
        {
            var L = LookOf(p);
            string top = L.Bald ? L.Skin : L.Hair;
            string key = "sleep|" + L.Skin + "|" + L.Hair + (L.Bald ? "B" : "") + (L.Beard != null ? L.BeardColor : "") + (L.Child ? "c" : "a");
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var g = new PixelCanvas(7, 6);
            g.R(2, 1, 3, 1, top); g.R(1, 2, 5, 1, top);
            g.R(2, 3, 3, 2, L.Skin); g.D(1, 3, L.Hair); g.D(5, 3, L.Hair);
            g.D(2, 3, L.SkinShade); g.D(4, 3, L.SkinShade);
            if (L.Beard != null) g.R(2, 4, 3, 1, L.BeardColor);
            g.Outline(OL);
            s = g.ToSprite(key, new Vector2(0, 1));
            cache[key] = s;
            return s;
        }

        /// <summary>Canvas 11 wide; adults stand about 17 px tall (taller or shorter by a pixel), children 14 (big-headed).
        /// Feet sit two rows above the bottom.</summary>
        static PixelCanvas Draw(Look L, int dir, int frame, Act act = Act.None, int phase = 0)
        {
            if (dir == Right) return Draw(L, Left, frame, act, phase).Mirrored();
            int torso = L.Child ? 2 : 4, legs = L.Child ? 2 : L.Tall ? 4 : L.Short ? 2 : 3;
            const int hy = 3;                         // top of the head
            int ty = hy + 7, ly = ty + torso, fy = ly + legs;
            bool posed = act != Act.None;
            int padX = posed ? PadX : 0, padT = posed ? PadT : 0, padB = posed ? PadB : 0;   // room for tools held out
            int H = fy + 3 + padT + padB;
            var g = new PixelCanvas(11 + padX * 2, H);
            int ox = 1 + padX, oy = padT;
            System.Action<int, int, string> P = (x, y, c) => g.D(x + ox, y + oy, c);
            System.Action<int, int, int, int, string> RR = (x, y, w, h, c) => g.R(x + ox, y + oy, w, h, c);
            bool side = dir == Left;
            bool skirt = L.Female && !L.Child;
            int l1 = frame == 1 ? 1 : 0, l2 = frame == 2 ? 1 : 0;
            int bx = L.Stout ? 1 : 2, bw = L.Stout ? 7 : 5;   // the body, front on
            string arms = L.Jacket ?? L.Shirt, armShade = L.Jacket != null ? Darker(L.Jacket) : L.ShirtShade;

            // ---------- legs / skirt ----------
            if (!side)
            {
                if (skirt)
                {
                    int sway = frame == 1 ? -1 : frame == 2 ? 1 : 0, sx = L.Stout ? 0 : 1, sw = L.Stout ? 9 : 7;
                    RR(sx, ly - 1, sw, 1, L.Shirt);
                    for (int r = 0; r < legs; r++) RR(sx + (r == legs - 1 ? sway : 0), ly + r, sw, 1, r == legs - 1 ? L.ShirtShade : L.Shirt);
                    RR(sx + sw - 2, ly - 1, 1, legs + 1, L.ShirtShade);
                    Pattern(L, RR, sx, ly - 1, sw - 2, legs);
                    RR(2, fy - l1, 2, 1, L.Shoe); RR(5, fy - l2, 2, 1, L.Shoe);
                }
                else
                {
                    RR(2, ly, 2, legs - l1, L.Pant); RR(2, fy - l1, 2, 1, L.Shoe);
                    RR(5, ly, 2, legs - l2, L.PantShade); RR(5, fy - l2, 2, 1, L.Shoe);
                    RR(4, ly, 1, 1, L.Pant);
                    if (L.Stout) { P(1, ly, L.Pant); P(7, ly, L.PantShade); }
                }
            }
            else
            {
                if (skirt)
                {
                    RR(2, ly - 1, 5, 1, L.Shirt);
                    for (int r = 0; r < legs; r++) RR(1 + (frame == 0 ? 1 : 0), ly + r, frame == 0 ? 5 : 6, 1, r == legs - 1 ? L.ShirtShade : L.Shirt);
                    Pattern(L, RR, 2, ly - 1, 4, legs);
                    if (frame == 0) RR(2, fy, 3, 1, L.Shoe); else { RR(1, fy, 2, 1, L.Shoe); RR(5, fy, 2, 1, L.Shoe); }
                }
                else if (frame == 0) { RR(3, ly, 2, legs, L.Pant); RR(4, ly, 1, legs, L.PantShade); RR(2, fy, 3, 1, L.Shoe); }
                else
                {
                    RR(2, ly, 2, 1, L.Pant); RR(1, ly + 1, 2, legs - 1, L.Pant); RR(0, fy, 3, 1, L.Shoe);
                    RR(4, ly, 2, 1, L.PantShade); RR(5, ly + 1, 2, legs - 1, L.PantShade); RR(5, fy, 2, 1, L.Shoe);
                }
            }

            // a long robe for priests and lords: the hem falls over the legs
            if (L.Coat && !skirt)
            {
                if (!side) { RR(bx, ly, bw, legs - 1, L.Shirt); RR(bx + bw - 1, ly, 1, legs - 1, L.ShirtShade); }
                else RR(2, ly, 4, legs - 1, L.Shirt);
            }
            // ---------- torso and arms ----------
            if (!side)
            {
                RR(bx, ty, bw, torso, L.Shirt); RR(bx - 1, ty, bw + 2, 1, L.Shirt);
                RR(bx + bw - 1, ty + 1, 1, torso - 1, L.ShirtShade);
                Pattern(L, RR, bx, ty + 1, bw - 1, torso - 1);
                if (!skirt) RR(bx, ty + torso - 1, bw, 1, Darker(L.Pant, 0.85f));
                if (L.Jacket != null)
                {
                    if (dir == Down) { RR(bx - 1, ty, 2, torso, L.Jacket); RR(bx + bw - 1, ty, 2, torso, armShade); }
                    else { RR(bx - 1, ty, bw + 2, torso, L.Jacket); RR(bx + bw - 1, ty, 1, torso, armShade); }
                }
                if (L.Vest != null && dir == Down) { RR(bx, ty, 1, torso - 1, L.Vest); RR(bx + bw - 1, ty, 1, torso - 1, Darker(L.Vest)); }
                if (L.Vest != null && dir == Up) RR(bx, ty, bw, torso - 1, L.Vest);
                if (L.Shawl != null) { RR(bx - 1, ty, bw + 2, 1, L.Shawl); RR(bx, ty + 1, bw, 1, L.Shawl); if (dir == Down) P(4, ty + 2, L.Shawl); else RR(bx + 1, ty + 2, bw - 2, 1, L.Shawl); }
                if (L.Coat && dir == Down) { RR(4, ty, 1, 2, "#ece4d2"); if (!L.Collar) P(4, ty + 1, "#b5432f"); }
                if (L.Suspenders) { P(3, ty, "#5a3a2a"); P(5, ty, "#5a3a2a"); RR(3, ty + 1, 1, torso - 2, "#5a3a2a"); RR(5, ty + 1, 1, torso - 2, "#5a3a2a"); if (L.Child && dir == Down) RR(3, ty + 1, 3, torso - 1, L.Pant); }
                if (L.Neck != null) { RR(3, ty, 3, 1, L.Neck); if (dir == Down) P(4, ty + 1, L.Neck); }
                if (L.Badge && dir == Down) { RR(4, ty, 1, torso, HouseColour); RR(bx, ty + 1, bw, 1, HouseColour); }   // the cross on the surcoat
                if (L.Badge && dir == Up) RR(4, ty, 1, torso, HouseColour);
                if (L.Collar && dir == Down) { P(4, ty, "#f8f8f4"); }
                if (L.Apron != null && dir == Down) { RR(3, ty + 1, 3, torso - 1, L.Apron); RR(3, ly, 3, Mathf.Min(legs, skirt ? 2 : 1), L.Apron); }
                int arm = L.Child ? 1 : 2, la = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                int axl = bx - 1, axr = bx + bw;
                if (!posed)
                {
                    RR(axl, ty + 1, 1, arm + la, arms); P(axl, ty + 1 + arm + la, L.Skin);
                    RR(axr, ty + 1, 1, arm - la, armShade); P(axr, ty + 1 + arm - la, L.SkinShade);
                }
            }
            else
            {
                int sx = L.Stout ? 1 : 2, sw = L.Stout ? 5 : 4;
                RR(sx, ty, sw, torso, L.Shirt); RR(sx + sw - 1, ty, 1, torso, L.ShirtShade);
                Pattern(L, RR, sx, ty + 1, sw - 1, torso - 1);
                if (!skirt) RR(sx, ty + torso - 1, sw, 1, Darker(L.Pant, 0.85f));
                if (L.Jacket != null) RR(sx + 1, ty, sw - 1, torso, L.Jacket);
                if (L.Vest != null) RR(sx + 2, ty, sw - 2, torso - 1, L.Vest);
                if (L.Shawl != null) RR(sx, ty, sw, 2, L.Shawl);
                if (L.Apron != null) { RR(sx, ty + 1, 1, torso - 1, L.Apron); }
                if (L.Neck != null) P(sx, ty, L.Neck);
                if (L.Badge) P(2, ty + 1, HouseColour);
                if (L.Coat || L.Collar) P(2, ty, L.Collar ? "#f8f8f4" : "#ece4d2");
                int arm = L.Child ? 1 : 2, swg = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                if (!posed) { RR(3 + swg, ty + 1, 2, arm, armShade); RR(3 + swg, ty + 1 + arm, 2, 1, L.Skin); }
            }

            // ---------- head ----------
            string top = L.Bald ? L.Skin : L.Hair, topShade = L.Bald ? L.SkinShade : L.HairShade;
            if (!side)
            {
                RR(2, hy, 5, 1, L.Skin); RR(1, hy + 1, 7, 5, L.Skin); RR(2, hy + 6, 5, 1, L.Skin);
                RR(6, hy + 2, 2, 4, L.SkinShade);
                if (dir == Down)
                {
                    // hair
                    RR(2, hy, 5, 1, top); RR(1, hy + 1, 7, L.Bald ? 1 : 2, top);
                    if (L.Bald) { P(3, hy, Lighter(L.Skin)); P(1, hy + 2, L.Hair); P(7, hy + 2, L.HairShade); }
                    else RR(2, hy + 1, 3, 1, Lighter(L.Hair));
                    P(1, hy + 3, L.Hair); P(7, hy + 3, L.HairShade);
                    switch (L.HairStyle)
                    {
                        case "parted": P(5, hy + 1, L.HairShade); RR(2, hy + 1, 3, 1, Lighter(L.Hair)); break;
                        case "shaggy": if (!L.Bald) { P(1, hy + 4, L.Hair); P(7, hy + 4, L.HairShade); P(4, hy + 2, L.HairShade); } break;
                        case "curly": if (!L.Bald) { RR(1, hy, 7, 1, L.Hair); P(1, hy - 1 + 1, L.HairShade); P(3, hy + 2, Lighter(L.Hair)); P(6, hy + 1, L.HairShade); } break;
                        case "long": RR(0, hy + 2, 1, 6, L.Hair); RR(8, hy + 2, 1, 6, L.HairShade); RR(1, hy + 3, 1, 4, L.Hair); RR(7, hy + 3, 1, 4, L.HairShade); break;
                        case "bun": RR(3, hy - 1, 3, 1, L.Hair); break;
                        case "updo": RR(2, hy - 1, 5, 1, L.Hair); RR(3, hy - 2, 3, 1, L.HairShade); break;
                        case "braids":
                            for (int y = hy + 3; y < hy + 8; y++) { P(0, y, (y % 2 == 0) ? L.Hair : L.HairShade); P(8, y, (y % 2 == 1) ? L.Hair : L.HairShade); }
                            P(0, hy + 8, "#b5432f"); P(8, hy + 8, "#b5432f");
                            break;
                    }
                    // face
                    RR(3, hy + 3, 1, 2, OL); RR(5, hy + 3, 1, 2, OL);
                    if (L.Freckles) { P(2, hy + 4, L.SkinShade); P(6, hy + 4, L.SkinShade); }
                    if (L.Blush) { P(2, hy + 5, "#e89a8a"); P(6, hy + 5, "#d88a7a"); }
                    if (L.Beard != null)
                    {
                        string b = L.BeardColor, bs = Darker(b, 0.8f);
                        switch (L.Beard)
                        {
                            // beards keep to the jaw and chin so the face stays clear
                            case "full": RR(3, hy + 5, 3, 1, b); RR(2, hy + 6, 5, 1, b); RR(3, ty, 3, 1, bs); break;
                            case "long": RR(3, hy + 5, 3, 1, b); RR(2, hy + 6, 5, 1, b); RR(3, ty, 3, 2, b); P(4, ty + 2, bs); break;
                            case "mustache": RR(3, hy + 5, 3, 1, b); P(2, hy + 5, bs); P(6, hy + 5, bs); break;
                            case "chops": RR(1, hy + 3, 1, 3, b); RR(7, hy + 3, 1, 3, bs); P(2, hy + 5, b); P(6, hy + 5, bs); break;
                            case "goatee": RR(3, hy + 6, 3, 1, b); P(4, hy + 5, b); break;
                        }
                    }
                }
                else
                {
                    RR(2, hy, 5, 1, top); RR(1, hy + 1, 7, 5, L.Hair); RR(2, hy + 6, 5, 1, L.HairShade); RR(6, hy + 2, 2, 4, L.HairShade);
                    if (L.Bald) { RR(2, hy, 5, 1, L.Skin); RR(1, hy + 1, 7, 2, L.Skin); RR(6, hy + 1, 2, 2, L.SkinShade); }
                    bool longish = L.HairStyle == "long";
                    if (!longish) { P(1, hy + 3, L.Skin); P(7, hy + 3, L.SkinShade); }
                    else RR(1, hy + 6, 7, 3, L.Hair);
                    if (L.HairStyle == "bun") RR(3, hy - 1, 3, 2, L.HairShade);
                    if (L.HairStyle == "updo") { RR(2, hy - 1, 5, 2, L.HairShade); }
                    if (L.HairStyle == "braids") { RR(2, hy + 6, 1, 3, L.Hair); RR(6, hy + 6, 1, 3, L.HairShade); P(2, hy + 9, "#b5432f"); P(6, hy + 9, "#b5432f"); }
                    if (L.Beard != null) { P(1, hy + 5, L.BeardColor); P(7, hy + 5, Darker(L.BeardColor, 0.8f)); }
                }
            }
            else
            {
                RR(2, hy, 4, 1, L.Skin); RR(1, hy + 1, 6, 5, L.Skin); RR(2, hy + 6, 4, 1, L.Skin);
                P(0, hy + 3, L.Skin);
                RR(2, hy, 4, 1, top); RR(1, hy + 1, 6, L.Bald ? 1 : 2, top); RR(4, hy + 3, 3, 3, L.Hair); RR(6, hy + 3, 1, 3, L.HairShade);
                if (L.Bald) { RR(4, hy + 2, 3, 1, L.Hair); P(3, hy, Lighter(L.Skin)); }
                switch (L.HairStyle)
                {
                    case "long": RR(4, hy + 3, 3, 6, L.Hair); break;
                    case "bun": RR(5, hy - 1, 2, 2, L.HairShade); break;
                    case "updo": RR(3, hy - 1, 4, 1, L.Hair); RR(4, hy - 2, 2, 1, L.HairShade); break;
                    case "braids": for (int y = hy + 6; y < hy + 9; y++) P(6, y, y % 2 == 0 ? L.Hair : L.HairShade); P(6, hy + 9, "#b5432f"); break;
                    case "shaggy": if (!L.Bald) P(3, hy + 3, L.Hair); break;
                    case "curly": if (!L.Bald) { P(1, hy, L.Hair); P(5, hy + 2, Lighter(L.Hair)); } break;
                }
                P(4, hy + 3, L.SkinShade);
                RR(2, hy + 3, 1, 2, OL);
                if (L.Freckles) P(2, hy + 5, L.SkinShade);
                if (L.Blush) P(1, hy + 5, "#e89a8a");
                if (L.Beard != null)
                {
                    string b = L.BeardColor;
                    switch (L.Beard)
                    {
                        case "full": P(1, hy + 5, b); RR(3, hy + 5, 2, 1, b); RR(2, hy + 6, 3, 1, b); P(2, ty, b); break;
                        case "long": P(1, hy + 5, b); RR(3, hy + 5, 2, 1, b); RR(2, hy + 6, 3, 1, b); RR(2, ty, 2, 2, b); break;
                        case "mustache": RR(1, hy + 5, 2, 1, b); break;
                        case "chops": RR(3, hy + 3, 1, 3, b); P(2, hy + 5, b); break;
                        case "goatee": RR(1, hy + 6, 2, 1, b); break;
                    }
                }
            }

            if (L.Tonsure)
            {
                // a shaven crown ringed with hair
                if (dir == Up) { RR(2, hy, 5, 2, L.Skin); P(3, hy, Lighter(L.Skin)); }
                else if (dir == Down) { RR(3, hy, 3, 1, L.Skin); }
                else RR(2, hy, 3, 1, L.Skin);
            }
            // ---------- hat ----------
            if (L.Hat != null)
            {
                string hc = L.HatColor, hs = Darker(hc, 0.72f);
                int by = hy + 1;
                switch (L.Hat)
                {
                    case "straw": RR(2, by - 2, 5, 2, hc); RR(2, by - 1, 5, 1, "#8a5a2a"); RR(-1, by, 11, 1, hc); break;
                    case "cowboy": RR(2, by - 2, 5, 2, hc); P(4, by - 2, hs); RR(0, by, 9, 1, hc); P(-1, by - 1, hc); P(9, by - 1, hc); break;
                    case "top": RR(2, by - 4, 5, 4, hc); RR(2, by - 1, 5, 1, "#b5432f"); RR(1, by, 7, 1, hc); RR(6, by - 4, 1, 4, hs); break;
                    case "bowler": RR(3, by - 2, 3, 1, hc); RR(2, by - 1, 5, 1, hc); RR(1, by, 7, 1, hs); break;
                    case "parson": RR(2, by - 2, 5, 2, hc); RR(-1, by, 11, 1, hc); P(4, by - 2, hs); break;   // a low crown and a wide flat brim
                    case "slouch": RR(2, by - 2, 5, 2, hc); P(3, by - 2, Lighter(hc)); RR(0, by, 9, 1, hc); P(-1, by + 1, hs); P(9, by + 1, hs); RR(2, by - 1, 5, 1, hs); break;   // a soft brim that droops
                    case "cap":
                        RR(1, by - 1, 7, 2, hc); RR(2, by - 2, 5, 1, hc); RR(6, by - 1, 2, 2, hs);
                        if (dir == Down) RR(1, by + 1, 7, 1, hs); else if (dir == Left) RR(-1, by + 1, 3, 1, hs);
                        break;
                    case "bonnet":   // a sunbonnet: deep brim round the face, a curtain over the neck
                        if (dir == Down) { RR(1, by - 2, 7, 2, hc); RR(0, by, 9, 1, hs); RR(0, by + 1, 1, 3, hc); RR(8, by + 1, 1, 3, hs); P(2, hy + 6, hs); P(6, hy + 6, hs); }
                        else if (dir == Up) { RR(1, by - 2, 7, 4, hc); RR(1, by + 2, 7, 3, hc); RR(6, by - 1, 2, 6, hs); RR(1, by + 5, 7, 1, hs); }
                        else { RR(1, by - 2, 6, 3, hc); RR(-1, by - 1, 2, 3, hs); RR(4, by + 1, 3, 4, hc); RR(6, by + 1, 1, 4, hs); }
                        break;
                    case "hood":   // a peasant's hood with a short shoulder cape and a liripipe tail
                        if (dir == Down) { RR(1, by - 2, 7, 2, hc); RR(0, by, 1, 5, hc); RR(8, by, 1, 5, hs); RR(1, hy + 6, 7, 1, hc); RR(0, ty, 9, 1, hc); P(2, by - 1, Lighter(hc)); }
                        else if (dir == Up) { RR(1, by - 2, 7, 7, hc); RR(6, by - 1, 2, 6, hs); RR(0, ty, 9, 1, hc); RR(4, ty + 1, 1, 2, hs); }
                        else { RR(1, by - 2, 6, 3, hc); RR(4, by + 1, 3, 5, hc); RR(6, by + 1, 1, 5, hs); RR(1, ty, 6, 1, hc); P(7, by, hs); P(8, by + 1, hs); }
                        break;
                    case "coif":   // a close linen cap tied under the chin
                        if (dir == Down) { RR(2, by - 2, 5, 1, hc); RR(1, by - 1, 7, 2, hc); P(1, by + 1, hc); P(7, by + 1, hc); P(1, by + 2, hc); P(7, by + 2, hs); }
                        else if (dir == Up) { RR(1, by - 2, 7, 4, hc); RR(6, by - 1, 2, 3, hs); }
                        else { RR(1, by - 2, 6, 3, hc); RR(4, by + 1, 3, 2, hc); P(6, by + 1, hs); }
                        break;
                    case "felt":   // a soft felt cap with a rolled brim
                        RR(2, by - 3, 5, 2, hc); P(3, by - 3, Lighter(hc)); RR(1, by - 1, 7, 1, hs); break;
                    case "veil": case "crownveil":   // a linen veil over the hair, falling to the shoulders, with a wimple at the chin for ladies
                        if (dir == Down) { RR(1, by - 2, 7, 2, hc); RR(0, by, 1, 6, hc); RR(8, by, 1, 6, hs); if (L.Hat == "crownveil") { RR(2, hy + 6, 5, 1, hc); } }
                        else if (dir == Up) { RR(1, by - 2, 7, 7, hc); RR(6, by - 1, 2, 6, hs); RR(1, by + 5, 7, 1, hs); }
                        else { RR(1, by - 2, 6, 3, hc); RR(4, by + 1, 3, 6, hc); RR(6, by + 1, 1, 6, hs); }
                        if (L.Hat == "crownveil") { RR(1, by - 2, 7, 1, "#e0b04a"); if (dir == Down) P(4, by - 3, "#c84a3a"); }
                        break;
                    case "circlet":   // a gold circlet on the brow
                        RR(1, by - 1, 7, 1, hc); P(2, by - 2, hc); P(4, by - 2, hc); P(6, by - 2, hc); if (dir == Down) P(4, by - 1, "#c84a3a"); break;
                    case "helm":   // a round steel helm with a nasal guard
                        RR(2, by - 3, 5, 1, hc); RR(1, by - 2, 7, 3, hc); P(2, by - 2, "#c8ccd4"); RR(6, by - 2, 2, 3, hs);
                        if (dir == Down) RR(4, by + 1, 1, 3, hs);
                        else if (dir == Left) RR(0, by + 1, 1, 2, hs);
                        break;
                    case "kerchief":   // a headscarf knotted at the back
                        if (dir == Down) { RR(2, by - 2, 5, 1, hc); RR(1, by - 1, 7, 2, hc); P(2, by - 1, Lighter(hc)); }
                        else if (dir == Up) { RR(1, by - 2, 7, 3, hc); P(4, by + 1, hs); P(3, by + 2, hs); P(5, by + 2, hs); }
                        else { RR(1, by - 2, 6, 3, hc); P(6, by + 1, hs); P(7, by + 1, hs); }
                        break;
                }
            }

            Pen pen = null;
            if (posed) { pen = new Pen(g, ox, oy, L, dir, ty, fy, bx, bw, arms, armShade); Pose(pen, act, phase); }
            g.Outline(OL);
            if (pen != null) pen.Finish();   // fishing lines and thread go on after the outline so they stay thin
            g.RBlend(ox + 1, fy + 2 + oy, 7, 1, new Color32(0, 0, 0, 70));
            return g;
        }

        /// <summary>Gingham checks, pinstripes or a calico print over a patch of clothing.</summary>
        static void Pattern(Look L, System.Action<int, int, int, int, string> RR, int x, int y, int w, int h)
        {
            if (L.Pattern == null || L.Pattern == "plain" || w <= 0 || h <= 0) return;
            string dark = Darker(L.Shirt, 0.72f), light = Lighter(L.Shirt);
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    if (L.Pattern == "check" && (xx % 2 == 0) && ((yy - y) % 2 == 0)) RR(xx, yy, 1, 1, dark);
                    else if (L.Pattern == "stripe" && xx % 2 == 0) RR(xx, yy, 1, 1, dark);
                    else if (L.Pattern == "print" && (xx + yy * 2) % 4 == 0) RR(xx, yy, 1, 1, light);
                }
        }

        static string Lighter(string hex)
        {
            var c = PixelCanvas.C(hex);
            return "#" + Mathf.Min(255, c.r + 30).ToString("x2") + Mathf.Min(255, c.g + 26).ToString("x2") + Mathf.Min(255, c.b + 20).ToString("x2");
        }
    }
}
