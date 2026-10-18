using System.Collections.Generic;
using System.Linq;
using MedievalTown.Game.Art;
using MedievalTown.Game.UI;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game
{
    /// <summary>
    /// The interface: a date box with speed controls, the stores bar, warnings, the lord's purse and four books
    /// (the Court, the Manor Rules, the Folk and the Chronicle), a hotbar of buildings, a minimap, a message feed
    /// and an inspector. Wooden frames, parchment and a pixel font, all drawn through the small Ui kit at a whole-number scale.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        enum Tab { None, Court, Rules, Folk, Chronicle }

        GameController gc;
        Town T { get { return gc.Town; } }
        int S = 2;
        float W, H;

        List<Rect> blockers = new List<Rect>(), building = new List<Rect>();
        Tab tab = Tab.None;
        Category cat = Category.Homes;
        string toast; float toastUntil;
        float confirmNewUntil; int confirmPullDown = -1;
        Vector2 panelScroll, inspScroll;
        float panelContentH, inspContentH;
        Texture2D minimap; int miniVersion = -1, miniSeason = -1; float miniTimer; Color32[] miniBase;
        string hoverTip;
        int folkSort;

        readonly List<System.Action> deferred = new List<System.Action>();
        void Later(System.Action a) { deferred.Add(a); }

        void Awake() { gc = GetComponent<GameController>(); }

        void Update()
        {
            if (deferred.Count == 0) return;
            var run = deferred.ToArray(); deferred.Clear();
            foreach (var a in run) a();
        }

        public void Toast(string msg) { toast = msg; toastUntil = Time.unscaledTime + 2.6f; }
        public void ResetConfirm() { confirmPullDown = -1; }
        public void CloseAll() { tab = Tab.None; }

        public bool Blocks(Vector2 mouse)
        {
            var p = new Vector2(mouse.x / S, (Screen.height - mouse.y) / S);
            foreach (var r in blockers) if (r.Contains(p)) return true;
            return false;
        }
        Rect Block(Rect r) { building.Add(r); return r; }

        static string M(float v) { return Town.Money(v); }

        // ================================================================== main
        void OnGUI()
        {
            if (gc == null || gc.Town == null) return;
            S = Mathf.Max(1, Mathf.RoundToInt(Screen.height / 560f));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1));
            W = Screen.width / (float)S; H = Screen.height / (float)S;
            if (Event.current.type == EventType.Layout) building.Clear();
            hoverTip = null;

            DateBox();
            StoresBar();
            Alerts();
            PurseAndBooks();
            Hotbar();
            Minimap();
            Messages();
            if (tab != Tab.None) BookPanel();
            if (gc.Selected.Kind != SelectionKind.None) Inspector();
            MapTooltip();
            if (hoverTip != null) UiTip(hoverTip);
            if (toast != null && Time.unscaledTime < toastUntil)
            {
                float w = PixelFont.Width(toast) + 16;
                var r = new Rect(Mathf.Round((W - w) / 2), 44, w, 17);
                Ui.Tip(r); Ui.Text(r.x + 8, r.y + 5, toast, Ui.Bad);
            }
            if (T.Crown.Stripped) Stripped();
            if (Event.current.type == EventType.Repaint) { blockers = new List<Rect>(building); }
        }

        void UiTip(string s)
        {
            var m = Event.current.mousePosition;
            float w = Mathf.Min(190, PixelFont.Width(s) + 12);
            float h = Ui.WrappedHeight(s, w - 12) + 8;
            var r = new Rect(Mathf.Min(m.x + 10, W - w - 4), Mathf.Min(m.y + 12, H - h - 4), w, h);
            Ui.Tip(r); Ui.Wrapped(new Rect(r.x + 6, r.y + 5, w - 12, h), s, Ui.Ink);
        }

        /// <summary>The village rose: the reign is over.</summary>
        void Stripped()
        {
            float w = 260, h = 92;
            var r = Block(new Rect(Mathf.Round((W - w) / 2), Mathf.Round((H - h) / 2), w, h));
            Ui.Panel(r);
            Ui.IconAt(r.x + 10, r.y + 10, Icon.Crown);
            Ui.Text(r.x + 24, r.y + 11, "The village has risen", Ui.Bad);
            Ui.Wrapped(new Rect(r.x + 10, r.y + 26, w - 20, 40), "The house of " + T.Dynasty + " has been driven from " + T.ManorName + ". It ruled until " + T.Year + ".", Ui.Ink);
            if (Ui.Button(new Rect(r.x + w / 2 - 50, r.yMax - 24, 100, 16), "A new reign")) Later(() => gc.NewTown());
        }

        // ---------------------------------------------------------------- date and speed
        void DateBox()
        {
            var r = Block(new Rect(4, 4, 150, 52));
            Ui.Panel(r);
            int si = T.Season;
            Ui.IconAt(r.x + 9, r.y + 8, si == 3 ? Icon.Snow : si == 2 ? Icon.Leaf : Icon.Sun);
            Ui.Text(r.x + 22, r.y + 9, T.DateLabel, Ui.Title);
            if (Ui.Hover(new Rect(r.x, r.y, r.width, 20))) hoverTip = T.Weekday + (T.IsSunday ? ": mass, and a day of rest." : T.IsWeekWorkDay ? ": week-work. Serfs labour on the king's land." : T.WeekdayIndex == 4 ? ": the reeve goes round collecting taxes." : T.IsMarketDay ? ": market day. The bailiff takes the taxes to the castle." : ": the serfs work their own strips.");
            var bar = new Rect(r.x + 9, r.y + 21, 132, 5);
            Ui.Fill(bar, Ui.Hex("#3b2618"));
            int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float t = (i + 0.5f) / segs;
                float n = t < 0.15f ? 1 - t / 0.15f : t > 0.82f ? Mathf.Min(1, (t - 0.82f) / 0.12f) : 0;
                Ui.Fill(new Rect(bar.x + 1 + i * (bar.width - 2) / segs, bar.y + 1, (bar.width - 2) / segs + 0.5f, 3), Color.Lerp(Ui.Hex("#9ad0f0"), Ui.Hex("#2a3a6a"), n));
            }
            Ui.Fill(new Rect(bar.x + 1 + T.TimeOfDay * (bar.width - 3), bar.y - 1, 2, 7), Ui.Gold);
            var speeds = GameController.Speeds;
            string[] labels = { "", "1x", "3x", "10x", "30x" };
            for (int i = 0; i < speeds.Length; i++)
            {
                var b = new Rect(r.x + 8 + i * 27, r.y + 31, 26, 15);
                bool on = gc.Speed == speeds[i];
                if (i == 0 ? Ui.Button(b, "", on, true, Icon.Pause) : Ui.Button(b, labels[i], on)) gc.SetSpeed(speeds[i]);
                if (Ui.Hover(b)) hoverTip = i == 0 ? "Pause (Space)" : "Speed " + labels[i] + " (" + i + ")";
            }
        }

        // ---------------------------------------------------------------- stores bar
        void StoresBar()
        {
            int people = T.People.Count, kids = T.People.Count(p => p.IsChild), homeless = T.People.Count(p => p.HomeId < 0);
            float loyal = T.AverageLoyalty, piety = T.AveragePiety;
            var items = new List<KeyValuePair<Icon, string>>
            {
                new KeyValuePair<Icon, string>(Icon.Sheaf, Mathf.FloorToInt(T.StoreStock(Good.Grain)).ToString()),
                new KeyValuePair<Icon, string>(Icon.Bread, Mathf.FloorToInt(T.StoreStock(Good.Flour)).ToString()),
                new KeyValuePair<Icon, string>(Icon.Firewood, Mathf.FloorToInt(T.StoreStock(Good.Firewood)).ToString()),
                new KeyValuePair<Icon, string>(Icon.Logs, Mathf.FloorToInt(T.StoreStock(Good.Timber)).ToString()),
                new KeyValuePair<Icon, string>(Icon.Stone, Mathf.FloorToInt(T.StoreStock(Good.Stone)).ToString()),
                new KeyValuePair<Icon, string>(Icon.People, (people - kids) + "+" + kids),
                new KeyValuePair<Icon, string>(Icon.Heart, Mathf.RoundToInt(loyal) + "%"),
                new KeyValuePair<Icon, string>(Icon.Cross, Mathf.RoundToInt(piety) + "%"),
                new KeyValuePair<Icon, string>(Icon.Crown, M(T.LastTaxCoin) + "+" + Mathf.RoundToInt(T.LastTaxGrain)),
            };
            if (homeless > 0) items.Add(new KeyValuePair<Icon, string>(Icon.Warning, homeless + " in tents"));
            string[] tips =
            {
                "Grain in the royal granary. The court eats it as pottage until there's a mill; the merchant buys what's spare above the reserve.",
                "Flour in the granary: the crown's share (multure) of every sack ground at the king's mill. Bread goes further than pottage.",
                "Firewood in the granary. The keep's great hall burns a lot of it in winter.",
                "Timber in the granary, for building.",
                "Stone in the granary, for building the keep and the church.",
                "Grown folk + children",
                "Loyalty to the crown: fed, warm, housed, not too burdened, not squeezed by the reeve, God-fearing. Below 15 families slip away by night; near nothing for two weeks and the village rises.",
                "Piety: mass every Sunday keeps it up. A devout village is a more loyal one.",
                "Taxes that reached the castle last week: coin + grain. They climb from the households to the reeve, the bailiff and the king.",
                "Folk without a roof. Tents are no use in autumn and winter.",
            };
            float w = 12; foreach (var kv in items) w += 14 + PixelFont.Width(kv.Value) + 10;
            var r = Block(new Rect(Mathf.Round((W - w) / 2), 4, w, 24));
            Ui.Panel(r);
            float x = r.x + 8;
            for (int i = 0; i < items.Count; i++)
            {
                var kv = items[i];
                var cell = new Rect(x - 2, r.y + 4, 14 + PixelFont.Width(kv.Value) + 6, 16);
                Ui.IconAt(x, r.y + 7, kv.Key);
                Color c = Ui.Ink;
                if (i == 6) c = Ui.Mood(loyal);
                if (i == 7) c = Ui.Mood(piety);
                if (i == 9) c = Ui.Bad;
                Ui.Text(x + 12, r.y + 9, kv.Value, c);
                if (Ui.Hover(cell)) hoverTip = tips[Mathf.Min(i, tips.Length - 1)];
                x += 14 + PixelFont.Width(kv.Value) + 10;
            }
        }

        // ---------------------------------------------------------------- warnings under the stores bar
        void Alerts()
        {
            var lines = new List<KeyValuePair<string, bool>>();   // text, severe
            int si = T.Season;
            int homeless = T.People.Count(p => p.HomeId < 0);
            if (homeless > 0 && si >= 1) lines.Add(new KeyValuePair<string, bool>(homeless + " in tents. Build cottages before " + (si == 1 ? "autumn." : "the cold kills them."), si >= 2));
            int noStrip = T.Buildings.Count(b => { if (!b.Built || b.Type != BuildingType.Cottage || b.Residents.Count == 0) return false; var hd = T.HeadOf(b); return hd != null && hd.Rank != Rank.Freeman && T.StripOf(hd) == null; });
            if (noStrip > 0) lines.Add(new KeyValuePair<string, bool>((noStrip == 1 ? "A serf family has" : noStrip + " serf families have") + " no strip field. They'll go hungry.", true));
            if (!T.OfType(BuildingType.Demesne).Any() && !T.Sites.Any(s => s.Type == BuildingType.Demesne)) lines.Add(new KeyValuePair<string, bool>("No royal demesne: the court has nothing to eat once the stores run out.", true));
            float need = T.HouseholdKeep;
            if (si <= 1 && need > 0 && T.StoreStock(Good.Grain) < need * 0.9f) lines.Add(new KeyValuePair<string, bool>("The granary won't feed the court until harvest.", T.StoreStock(Good.Grain) < need * 0.5f));
            if (T.Bailiff == null) lines.Add(new KeyValuePair<string, bool>("No bailiff: the reeve carries the taxes to the castle himself until a new one comes.", false));
            if (T.Reeve == null) lines.Add(new KeyValuePair<string, bool>("No reeve: nobody is collecting the taxes.", true));
            if (T.Crown.LowDays > 0) lines.Add(new KeyValuePair<string, bool>("The village is close to rising (" + (Balance.RevoltDays - T.Crown.LowDays) + " days).", true));
            if (T.AverageLoyalty < 25 && T.Crown.LowDays == 0) lines.Add(new KeyValuePair<string, bool>("Loyalty is very low. Families will start to flee.", true));
            if (T.Sites.Any(s => s.MaterialsFraction < 1 && s.Progress >= s.MaterialsFraction * s.Def.Work - 0.5f) && T.StoreStock(Good.Timber) < 1 && T.StoreStock(Good.Stone) < 1)
                lines.Add(new KeyValuePair<string, bool>("Building sites are waiting on timber and stone.", false));
            if (T.Priest == null) lines.Add(new KeyValuePair<string, bool>("The parish has no priest.", false));
            if (lines.Count == 0) return;
            float w = 0; foreach (var l in lines) w = Mathf.Max(w, PixelFont.Width(l.Key));
            w += 26;
            var r = Block(new Rect(Mathf.Round((W - w) / 2), 31, w, lines.Count * 11 + 8));
            Ui.Fill(r, Ui.Hex("#3b2618"));
            var bg = Ui.Parch; bg.a = 0.92f; Ui.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg);
            float y = r.y + 4;
            foreach (var l in lines)
            {
                Ui.IconAt(r.x + 5, y - 1, Icon.Warning);
                Ui.Text(r.x + 17, y, l.Key, l.Value ? Ui.Bad : Ui.Warn);
                y += 11;
            }
        }

        // ---------------------------------------------------------------- the lord's purse and the books
        void PurseAndBooks()
        {
            var r = Block(new Rect(W - 160, 4, 156, 24));
            Ui.Panel(r);
            Ui.IconAt(r.x + 8, r.y + 7, Icon.Coin);
            Ui.Text(r.x + 21, r.y + 9, "Royal treasury", Ui.InkSoft);
            Ui.Text(r.xMax - 8, r.y + 9, M(T.Coin), T.Coin < 0 ? Ui.Bad : Ui.Title, Ui.Align.Right);
            if (Ui.Hover(r)) hoverTip = "Coin in the royal treasury. Last week " + M(T.LastIn) + " came in and " + M(T.LastOut) + " went out.";
            Icon[] icons = { Icon.Crown, Icon.Scales, Icon.People, Icon.Book };
            string[] names = { "The Court: the royal family, the bailiff, the reeve and the tax chain", "The Laws: taxes, dues and how the crown trades", "Folk: everyone in the village", "The Chronicle: what's been happening" };
            for (int i = 0; i < 4; i++)
            {
                var b = Block(new Rect(W - 160 + i * 39, 30, 37, 18));
                var t = (Tab)(i + 1);
                if (Ui.Button(b, "", tab == t, true, icons[i])) { Later(() => { tab = tab == t ? Tab.None : t; panelScroll = Vector2.zero; }); }
                if (Ui.Hover(b)) hoverTip = names[i];
            }
        }

        // ---------------------------------------------------------------- hotbar
        void Hotbar()
        {
            var cats = Defs.PaletteOrder;
            float slotW = 30;
            var defs = Defs.InCategory(cat).ToList();
            int nSlots = 2 + 6;
            float barW = nSlots * (slotW + 2) + 14;
            var bar = Block(new Rect(Mathf.Round((W - barW) / 2), H - 44, barW, 40));
            float tabW = 48, tabsW = cats.Length * (tabW + 2);
            var tabsR = Block(new Rect(Mathf.Round((W - tabsW) / 2), bar.y - 16, tabsW, 16));
            for (int i = 0; i < cats.Length; i++)
            {
                var c = cats[i];
                var tr = new Rect(tabsR.x + i * (tabW + 2), tabsR.y, tabW, 16);
                if (Ui.Button(tr, Defs.CategoryNames[(int)c], cat == c)) { Later(() => cat = c); }
            }
            Ui.Panel(bar);
            float x = bar.x + 7, y = bar.y + 5;
            if (Slot(new Rect(x, y, slotW, slotW), null, Icon.Pointer, gc.Tool == ToolKind.Inspect, "Inspect (Q)\nClick a building or a villager to see how they're doing.")) gc.SetTool(ToolKind.Inspect);
            x += slotW + 2;
            if (Slot(new Rect(x, y, slotW, slotW), null, Icon.Axe, gc.Tool == ToolKind.Clear, "Clear land (X)\nFell trees (+2 timber), break boulders (+3 stone), pull stumps.")) gc.SetTool(ToolKind.Clear);
            x += slotW + 6;
            foreach (var d in defs.Take(6))
            {
                var r = new Rect(x, y, slotW, slotW);
                bool on = gc.Tool == ToolKind.Build && gc.BuildType == d.Type;
                bool can = !(d.Unique && T.Buildings.Any(b => b.Type == d.Type));
                Texture tex = d.Type == BuildingType.Road ? null : BuildingArt.Get(d.Type, 0, T.Season).TopTexture;
                if (Slot(r, tex, d.Type == BuildingType.Road ? Icon.Hammer : (Icon?)null, on, null, can) && can) gc.SetTool(ToolKind.Build, d.Type);
                if (Ui.Hover(r)) BuildTip(d, new Vector2(r.x, bar.y - 20));
                x += slotW + 2;
            }
        }

        bool Slot(Rect r, Texture tex, Icon? icon, bool on, string tip, bool enabled = true)
        {
            Ui.SlotBox(r, on);
            if (tex != null)
            {
                float aspect = tex.width / (float)tex.height;
                float h = r.height - 6, w = h * aspect;
                if (w > r.width - 6) { w = r.width - 6; h = w / aspect; }
                var c = GUI.color; if (!enabled) GUI.color = new Color(1, 1, 1, 0.4f);
                Ui.Texture(new Rect(r.x + (r.width - w) / 2, r.y + (r.height - h) / 2, w, h), tex);
                GUI.color = c;
            }
            else if (icon.HasValue) Ui.IconAt(Mathf.Round(r.x + (r.width - 9) / 2), Mathf.Round(r.y + (r.height - 9) / 2), icon.Value, enabled ? 1 : 0.4f);
            if (tip != null && Ui.Hover(r)) hoverTip = tip;
            return Ui.Clickable(r);
        }

        void BuildTip(BuildingDef d, Vector2 anchor)
        {
            if (Event.current.type != EventType.Repaint) return;
            float w = 196;
            string blurb = d.Blurb ?? "";
            string extra = d.Workers > 0 ? "Up to " + d.Workers + (d.WorkerRank == Rank.Freeman ? " freemen" : " workers") + " · " + M(T.Policy.FreeWage) + " a day from the crown" : null;
            if (d.Type == BuildingType.Alehouse) extra = "An alewife brews and keeps what she sells";
            float h = 30 + Ui.WrappedHeight(blurb, w - 14) + (extra != null ? 11 : 0);
            var r = new Rect(Mathf.Clamp(anchor.x - 60, 4, W - w - 4), anchor.y - h, w, h);
            Ui.Tip(r);
            Ui.Text(r.x + 7, r.y + 6, d.Name, Ui.Title);
            float x = r.x + 7, y = r.y + 17;
            if (d.Type == BuildingType.Road) Ui.Text(x, y, "Free, laid at once", Ui.InkSoft);
            else
            {
                if (d.Timber > 0) { Ui.IconAt(x, y - 1, Icon.Logs); x += 11; x += Ui.Text(x, y, d.Timber.ToString(), T.StoreStock(Good.Timber) >= d.Timber ? Ui.Ink : Ui.Bad) + 6; }
                if (d.Stone > 0) { Ui.IconAt(x, y - 1, Icon.Stone); x += 11; x += Ui.Text(x, y, d.Stone.ToString(), T.StoreStock(Good.Stone) >= d.Stone ? Ui.Ink : Ui.Bad) + 6; }
                Ui.IconAt(x, y - 1, Icon.Hammer); x += 11; Ui.Text(x, y, d.Work + " days' labour", Ui.InkSoft);
            }
            float by = r.y + 28;
            if (extra != null) { Ui.Text(r.x + 7, by, extra, Ui.InkSoft); by += 11; }
            Ui.Wrapped(new Rect(r.x + 7, by, w - 14, 200), blurb, Ui.Ink);
        }

        // ---------------------------------------------------------------- minimap
        void RebuildMinimap()
        {
            int w = Town.Cols, h = Town.Rows, si = T.Season;
            if (minimap == null) minimap = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            if (miniBase == null) miniBase = new Color32[w * h];
            var tileCol = new Color32[7];
            tileCol[0] = PixelCanvas.C(GroundPainter.GrassBase(si));
            tileCol[1] = PixelCanvas.C(si == 3 ? "#bcd8e6" : "#3b7cc0");
            tileCol[2] = PixelCanvas.C(si == 3 ? "#6f8a78" : si == 2 ? "#c47a24" : "#3d7a2a");
            tileCol[3] = PixelCanvas.C("#a39c90");
            tileCol[4] = PixelCanvas.C(si == 3 ? "#d8e2e8" : GroundPainter.HillColors[si][0]);
            tileCol[5] = tileCol[0]; tileCol[6] = tileCol[0];
            for (int ty = 0; ty < Town.Rows; ty++)
                for (int tx = 0; tx < Town.Cols; tx++)
                {
                    var b = T.OccupantAt(tx, ty);
                    byte t = T.TerrainAt(tx, ty);
                    var c = b != null ? PixelCanvas.C(b.Type == BuildingType.Road ? "#c49c69" : b.Def.Color) : tileCol[Mathf.Min(6, (int)t)];
                    miniBase[(h - 1 - ty) * w + tx] = c;
                }
            miniVersion = T.MapVersion; miniSeason = si;
        }

        void Minimap()
        {
            var r = Block(new Rect(4, H - 4 - (MiniH + 16), MiniW + 16, MiniH + 16));
            Ui.Panel(r);
            if (minimap == null || miniVersion != T.MapVersion || miniSeason != T.Season) RebuildMinimap();
            miniTimer -= Time.unscaledDeltaTime;
            if (miniTimer <= 0 && Event.current.type == EventType.Repaint)
            {
                miniTimer = 0.25f;
                var px = (Color32[])miniBase.Clone();
                int w = minimap.width, h = minimap.height;
                foreach (var p in T.People)
                {
                    Vector2 pos;
                    if (!gc.View.TryGetPersonPos(p.Id, out pos)) continue;
                    int x = Mathf.Clamp((int)(pos.x / 16), 0, w - 1), y = Mathf.Clamp((int)(pos.y / 16), 0, h - 1);
                    px[(h - 1 - y) * w + x] = PixelCanvas.C(NatureArt.RankColor[p.Rank]);
                }
                minimap.SetPixels32(px); minimap.Apply(false);
            }
            var inner = new Rect(r.x + 8, r.y + 8, MiniW, MiniH);
            if (Ui.Repaint) GUI.DrawTexture(inner, minimap, ScaleMode.StretchToFill);
            float sx = inner.width / (Town.Cols * 16f), sy = inner.height / (Town.Rows * 16f);
            var o = gc.Rig.ViewOrigin; var v = gc.Rig.ViewSize;
            var vr = Rect.MinMaxRect(Mathf.Max(inner.x, inner.x + o.x * sx), Mathf.Max(inner.y, inner.y + o.y * sy),
                                     Mathf.Min(inner.xMax, inner.x + (o.x + v.x) * sx), Mathf.Min(inner.yMax, inner.y + (o.y + v.y) * sy));
            Ui.Fill(new Rect(vr.x, vr.y, vr.width, 1), Ui.Gold); Ui.Fill(new Rect(vr.x, vr.yMax - 1, vr.width, 1), Ui.Gold);
            Ui.Fill(new Rect(vr.x, vr.y, 1, vr.height), Ui.Gold); Ui.Fill(new Rect(vr.xMax - 1, vr.y, 1, vr.height), Ui.Gold);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && inner.Contains(e.mousePosition))
            {
                gc.CenterOn(new Vector2((e.mousePosition.x - inner.x) / sx, (e.mousePosition.y - inner.y) / sy));
                e.Use();
            }
        }

        const float MiniW = 160f, MiniH = 160f * Town.Rows / Town.Cols;

        // ---------------------------------------------------------------- message feed
        static Color KindColor(LogKind k)
        {
            switch (k)
            {
                case LogKind.Death: case LogKind.Bad: return Ui.Bad;
                case LogKind.Birth: case LogKind.Good: return Ui.Good;
                case LogKind.Crown: return Ui.Hex("#7a4a10");
                case LogKind.Church: return Ui.Hex("#4a3a6a");
                default: return Ui.Ink;
            }
        }

        void Messages()
        {
            float y = H - 4 - (MiniH + 16) - 8;
            int shown = 0;
            for (int i = T.Log.Count - 1; i >= 0; i--)
            {
                var l = T.Log[i];
                if (T.Day - l.Day > 2 || shown >= 4) break;
                if (l.Kind == LogKind.Info || l.Kind == LogKind.Build) continue;
                var lines = PixelFont.Wrap(l.Message, 230);
                float h = lines.Count * PixelFont.LineH + 6;
                y -= h + 2;
                var r = new Rect(4, y, 240, h);
                var bg = Ui.Parch; bg.a = 0.88f;
                Ui.Fill(r, Ui.Hex("#3b2618")); Ui.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg);
                float ly = r.y + 4;
                foreach (var line in lines) { Ui.Text(r.x + 5, ly, line, KindColor(l.Kind)); ly += PixelFont.LineH; }
                shown++;
            }
        }

        // ================================================================== the books
        float bx, by, bw;
        void Row(string k, string v, Color? vc = null)
        {
            Ui.Text(bx, by, k, Ui.InkSoft);
            if (PixelFont.Width(k) + PixelFont.Width(v) + 6 > bw) by += 10;
            Ui.Text(bx + bw, by, v, vc ?? Ui.Ink, Ui.Align.Right);
            by += 11;
        }
        void Heading(string s) { by += 4; Ui.Text(bx, by, s.ToUpper(), Ui.Title); by += 10; Ui.Line(bx, by - 2, bw, Ui.Hex("#c8a070")); by += 2; }
        void Para(string s, Color? c = null) { by += Ui.Wrapped(new Rect(bx, by, bw, 400), s, c ?? Ui.InkSoft) + 2; }
        void Meter(string label, float v, string tip = null)
        {
            Ui.Text(bx, by, label, Ui.InkSoft);
            var r = new Rect(bx + 62, by + 1, bw - 92, 6);
            Ui.Bar(r, v / 100f, Ui.Mood(v));
            Ui.Text(bx + bw, by, Mathf.RoundToInt(v) + "%", Ui.Mood(v), Ui.Align.Right);
            if (tip != null && Ui.Hover(new Rect(bx, by - 1, bw, 10))) hoverTip = tip;
            by += 11;
        }

        void BookPanel()
        {
            float pw = 262;
            var r = Block(new Rect(W - pw - 4, 52, pw, H - 52 - 64));
            Ui.Panel(r);
            string[] titles = { "", "The Court", "The Laws", "Folk of " + T.ManorName, "The Chronicle" };
            Ui.Text(r.x + 10, r.y + 9, titles[(int)tab], Ui.Title);
            if (Ui.Button(new Rect(r.xMax - 22, r.y + 6, 15, 14), "", false, true, Icon.Close)) Later(() => tab = Tab.None);
            float top = r.y + 22;
            var view = new Rect(r.x + 8, top, r.width - 16, r.yMax - 8 - top);
            Ui.Fill(new Rect(view.x, view.y - 1, view.width, 1), Ui.Hex("#c8a070"));
            panelScroll = GUI.BeginScrollView(view, panelScroll, new Rect(0, 0, view.width - 6, Mathf.Max(view.height, panelContentH)), false, false, GUIStyle.none, GUIStyle.none);
            bx = 2; by = 4; bw = view.width - 12;
            switch (tab)
            {
                case Tab.Court: CourtTab(); break;
                case Tab.Rules: RulesTab(); break;
                case Tab.Folk: FolkTab(); break;
                case Tab.Chronicle: ChronicleTab(); break;
            }
            panelContentH = by + 6;
            GUI.EndScrollView();
            ScrollIndicator(view, panelScroll.y, panelContentH);
        }

        void ScrollIndicator(Rect view, float scroll, float contentH)
        {
            if (contentH <= view.height) return;
            float frac = view.height / contentH, pos = scroll / (contentH - view.height);
            float h = Mathf.Max(10, view.height * frac);
            Ui.Fill(new Rect(view.xMax - 3, view.y + (view.height - h) * pos, 3, h), Ui.Hex("#b8844a"));
        }

        /// <summary>A clickable name: selects that person on the map.</summary>
        void PersonLine(Citizen p, string role)
        {
            if (p == null) { Row(role, "none", Ui.Bad); return; }
            var rr = new Rect(bx, by - 1, bw, 11);
            if (Ui.Hover(rr)) Ui.Fill(rr, Ui.Hex("#ecd09a"));
            Ui.Text(bx, by, role, Ui.InkSoft);
            Ui.Text(bx + bw, by, T.Title(p) + (p.IsChild ? " (" + (int)p.Age + ")" : ""), Ui.Ink, Ui.Align.Right);
            if (Ui.Clickable(rr)) Later(() => SelectAndShow(p));
            by += 11;
        }

        void SelectAndShow(Citizen p)
        {
            gc.Select(Selection.Person(p.Id));
            Vector2 pos;
            if (gc.View.TryGetPersonPos(p.Id, out pos)) gc.CenterOn(pos);
        }

        void CourtTab()
        {
            Heading("The house of " + T.Dynasty);
            var king = T.King;
            PersonLine(king, king != null && king.Female ? "Queen" : "King");
            if (king != null)
            {
                Row("Temperament", Defs.TraitNames[(int)king.Trait]);
                Para(Defs.TraitBlurbs[(int)king.Trait]);
                if (T.Generation > 1) Row("Monarchs of this house", T.Generation.ToString());
            }
            if (T.Regent != null) { PersonLine(T.Regent, "Regent"); Row("Regent's temperament", Defs.TraitNames[(int)T.Regent.Trait]); }
            if (king != null) PersonLine(T.GetCitizen(king.PartnerId), king.Female ? "Consort" : "Queen");
            var heir = T.Heir;
            PersonLine(heir, "Heir");
            if (heir == null) Para("Without an heir, a claimant from another house will take the throne when the king dies.", Ui.Bad);
            foreach (var c in T.People.Where(p => p.Rank == Rank.Noble && p.IsChild && p != heir).OrderByDescending(p => p.Age)) PersonLine(c, "Royal child");
            foreach (var n in T.People.Where(p => p.Rank == Rank.Noble && !p.IsChild && king != null && p.Id != king.PartnerId && p.Id != T.RegentId && p != heir)) PersonLine(n, "Royal kin");
            foreach (var k in T.People.Where(p => p.Rank == Rank.Knight)) PersonLine(k, "Knight");
            Row("Seat", T.Keep != null ? "The castle" : "A pavilion (no castle yet)", T.Keep != null ? Ui.Ink : Ui.Warn);

            Heading("The tax chain");
            Para("Households owe a share of their harvest in grain and a hearth tax in coin (freemen pay rent too; serfs tallage at Martinmas). Every Friday the reeve collects door to door and carries it to the bailiff. Every Saturday the bailiff carries the chest to the castle. Each keeps a share.");
            var reeve = T.Reeve; var bailiff = T.Bailiff;
            PersonLine(reeve, "Reeve");
            if (reeve != null)
            {
                Row("Reeve's share", Mathf.RoundToInt(T.Policy.ReeveCut * 100) + "%");
                Row("Chosen by", "the villagers, each spring");
            }
            if (Ui.Button(new Rect(bx, by, bw, 14), "Have the village choose a new reeve")) Later(() => T.NewReeve());
            by += 18;
            PersonLine(bailiff, "Bailiff");
            if (bailiff != null)
            {
                Row("Bailiff's share", Mathf.RoundToInt(T.Policy.BailiffCut * 100) + "%");
                var chest = T.TaxChest;
                if (chest != null) Row("In the tax chest", M(chest.Coin) + " · " + Mathf.FloorToInt(chest.Stock[(int)Good.Grain]) + " grain");
                Row("Lives at", T.BailiffHall != null ? "the Bailiff's Hall" : "the court (no hall yet)", T.BailiffHall != null ? Ui.Ink : Ui.Warn);
                bool confirm = confirmDismissUntil > Time.unscaledTime;
                if (Ui.Button(new Rect(bx, by, bw, 14), confirm ? "Click again to dismiss " + bailiff.First : "Dismiss the bailiff"))
                {
                    if (confirm) Later(() => T.DismissBailiff()); else confirmDismissUntil = Time.unscaledTime + 3f;
                }
                by += 18;
            }
            else Row("Bailiff", T.Crown.BailiffDay >= 0 ? "a new one arrives in " + Mathf.Max(0, T.Crown.BailiffDay - T.Day) + " days" : "none", Ui.Warn);

            TaxLedger("This year", T.Taxes);
            if (T.LastTaxes.AssessedCoin + T.LastTaxes.AssessedGrain > 0) TaxLedger("Last year", T.LastTaxes);
            Para("The reeve's and bailiff's shares are set in the Laws. If an officer takes more than his share it shows here, or in the grumbling of the village.");

            Heading("The church");
            PersonLine(T.Priest, "Parish priest");
            var ch = T.Church;
            Row("Church", ch != null ? T.ChurchName : "not built", ch != null ? Ui.Ink : Ui.Warn);
            if (ch != null)
            {
                Row("At mass last Sunday", ch.Attendance.ToString());
                Row("Tithe barn", Mathf.FloorToInt(ch.Stock[(int)Good.Grain]) + " grain");
                Para("A tenth of every harvest goes to the church. The priest gives alms from it to the hungry.");
            }
            Meter("Piety", T.AveragePiety);

            Heading("The village");
            Meter("Loyalty", T.AverageLoyalty);
            Row("Serfs", T.People.Count(p => !p.IsChild && (p.Rank == Rank.Serf || p.Rank == Rank.Reeve)).ToString());
            Row("Freemen", T.People.Count(p => !p.IsChild && p.Rank == Rank.Freeman).ToString());
            Row("Children", T.People.Count(p => p.IsChild).ToString());
            Row("Born / died", T.Births + " / " + T.Deaths);
            if (T.Fled > 0) Row("Fled", T.Fled.ToString(), Ui.Bad);
            if (T.Arrived > 0) Row("Came to the village", T.Arrived.ToString(), Ui.Good);
        }

        float confirmDismissUntil;

        /// <summary>Where the taxes went, step by step up the chain. Grain is valued at the merchant's price.</summary>
        void TaxLedger(string title, TaxBook t)
        {
            Heading("Taxes, " + title.ToLower());
            Row("Owed by households", M(t.AssessedCoin) + " · " + Mathf.FloorToInt(t.AssessedGrain) + " grain");
            Row("Reeve kept", M(t.ReeveCutCoin) + " · " + Mathf.FloorToInt(t.ReeveCutGrain) + " grain");
            Row("Reeve handed on", M(t.CollectedCoin) + " · " + Mathf.FloorToInt(t.CollectedGrain) + " grain");
            float share = T.Policy.BailiffCut;
            float handed = t.CollectedCoin + t.CollectedGrain * Balance.MerchantGrain, kept = t.BailiffCutCoin + t.BailiffCutGrain * Balance.MerchantGrain;
            bool over = handed > 3 && kept > handed * (share + 0.04f);
            Row("Bailiff kept", M(t.BailiffCutCoin) + " · " + Mathf.FloorToInt(t.BailiffCutGrain) + " grain", over ? Ui.Bad : Ui.Ink);
            if (over) Row("", "about " + Mathf.RoundToInt(kept / Mathf.Max(0.01f, handed) * 100) + "% instead of " + Mathf.RoundToInt(share * 100) + "%", Ui.Bad);
            Row("Reached the castle", M(t.ReceivedCoin) + " · " + Mathf.FloorToInt(t.ReceivedGrain) + " grain", Ui.Good);
            float unpaid = t == T.Taxes ? T.Buildings.Where(b => b.Built && Town.IsPeasantHome(b.Type)).Sum(b => b.OwedCoin) : t.Unpaid;
            if (unpaid > 0.05f) Row("Still owed", M(unpaid), Ui.Warn);
        }

        float SliderRow(string label, string value, float v, float min, float max, float step, string help)
        {
            Ui.Text(bx, by, label, Ui.Ink);
            Ui.Text(bx + bw, by, value, Ui.Title, Ui.Align.Right);
            by += 11;
            float nv = Ui.Slider(new Rect(bx, by, bw, 9), v, min, max, step);
            by += 12;
            if (help != null) Para(help);
            by += 2;
            return nv;
        }

        void RulesTab()
        {
            var pol = T.Policy;
            Heading("Taxes");
            pol.HarvestTax = SliderRow("Harvest tax", Mathf.RoundToInt(pol.HarvestTax * 100) + "% of each strip", pol.HarvestTax, 0, 0.5f, 0.01f,
                "The crown's share of every family's harvest, in grain. Above 15% the village feels it.");
            pol.HearthTax = SliderRow("Hearth tax", M(pol.HearthTax) + " a house a week", pol.HearthTax, 0, 1f, 0.05f,
                "Coin from every household. Serfs have little coin; the reeve takes grain instead when they can't pay.");
            pol.Tallage = SliderRow("Tallage", M(pol.Tallage) + " a serf family", pol.Tallage, 0, 5, 0.5f,
                "Laid on every serf family at Martinmas, the start of winter. Resented.");
            Heading("The officers' shares");
            pol.ReeveCut = SliderRow("Reeve's share", Mathf.RoundToInt(pol.ReeveCut * 100) + "% of what he collects", pol.ReeveCut, 0, 0.25f, 0.01f,
                "What the reeve keeps for his trouble.");
            pol.BailiffCut = SliderRow("Bailiff's share", Mathf.RoundToInt(pol.BailiffCut * 100) + "% of what he carries", pol.BailiffCut, 0, 0.3f, 0.01f,
                "What the bailiff keeps of the chest before it reaches the castle.");
            Heading("What the serfs owe");
            pol.WeekWork = Mathf.RoundToInt(SliderRow("Week-work", pol.WeekWork + " days a week", pol.WeekWork, 0, 5, 1,
                "Days each serf labours on the king's demesne and building sites before working their own strip. Two is the custom: more feeds the crown but starves the strips and sours the village."));
            pol.Multure = SliderRow("Multure", Mathf.RoundToInt(pol.Multure * 100) + "% of each sack", pol.Multure, 0, 0.25f, 0.01f,
                "The crown's share of all grain ground at the king's mill, paid in flour.");
            Heading("Freemen");
            pol.Rent = SliderRow("Rent", M(pol.Rent) + " a week", pol.Rent, 0, 4, 0.25f, "Owed by every freeman's household each week, collected by the reeve with the hearth tax.");
            pol.FreeWage = SliderRow("Day wage", M(pol.FreeWage), pol.FreeWage, 0.1f, 2f, 0.05f, "What the crown pays freemen for a day at the mill, the woods, the quarry or a building site.");
            pol.GrainPrice = SliderRow("Grain price", M(pol.GrainPrice) + " a measure", pol.GrainPrice, 0.05f, 1f, 0.05f, "What freemen pay for the king's grain on market day.");
            Heading("The granary");
            pol.Reserve = SliderRow("Reserve", Mathf.RoundToInt(pol.Reserve) + " grain", pol.Reserve, 0, 800, 20,
                "Grain kept back above what the court needs until harvest. The merchant buys the rest on market day.");
            pol.SellSurplus = Ui.Toggle(new Rect(bx, by, bw, 11), pol.SellSurplus, "Sell spare grain to the merchant"); by += 14;
            pol.BuyMaterials = Ui.Toggle(new Rect(bx, by, bw, 11), pol.BuyMaterials, "Buy timber and stone for waiting sites"); by += 14;
            pol.Dole = Ui.Toggle(new Rect(bx, by, bw, 11), pol.Dole, "The granary feeds the hungry"); by += 14;
            Para("A gentle king feeds the hungry whatever the laws say.");

            Heading("This week");
            Row("Came in", M(T.WeekIn)); Row("Went out", M(T.WeekOut));
            Row("Last week in / out", M(T.LastIn) + " / " + M(T.LastOut));

            Heading("The game");
            bool confirm = Time.unscaledTime < confirmNewUntil;
            if (Ui.Button(new Rect(bx, by, bw, 16), confirm ? "Click again to give up this reign" : "Start a new reign"))
            {
                if (confirm) Later(() => gc.NewTown()); else confirmNewUntil = Time.unscaledTime + 3f;
            }
            by += 20;
        }

        void FolkTab()
        {
            string[] sorts = { "Rank", "Loyalty", "Name", "Age" };
            for (int i = 0; i < sorts.Length; i++)
            {
                int k = i;
                if (Ui.Button(new Rect(bx + i * (bw / 4), by, bw / 4 - 2, 13), sorts[i], folkSort == i)) Later(() => folkSort = k);
            }
            by += 17;
            IEnumerable<Citizen> list = T.People.Where(p => !p.IsChild);
            switch (folkSort)
            {
                case 0: list = list.OrderByDescending(p => p.Rank).ThenBy(p => p.Family); break;
                case 1: list = list.OrderBy(p => p.Loyalty); break;
                case 2: list = list.OrderBy(p => p.First); break;
                default: list = list.OrderByDescending(p => p.Age); break;
            }
            foreach (var p in list)
            {
                var rr = new Rect(bx, by - 1, bw, 11);
                if (Ui.Hover(rr)) Ui.Fill(rr, Ui.Hex("#ecd09a"));
                Ui.Fill(new Rect(bx, by + 2, 4, 4), Ui.Hex(NatureArt.RankColor[p.Rank]));
                Ui.Text(bx + 7, by, T.Title(p), Ui.Ink);
                Ui.Text(bx + bw - 34, by, Defs.RankNames[(int)p.Rank], Ui.InkSoft, Ui.Align.Right);
                Ui.Text(bx + bw, by, Mathf.RoundToInt(p.Loyalty).ToString(), p.IsNoble || p.Rank == Rank.Priest || p.Rank == Rank.Knight ? Ui.InkSoft : Ui.Mood(p.Loyalty), Ui.Align.Right);
                if (Ui.Hover(rr)) hoverTip = (int)p.Age + " · " + ChoreText(p) + (p.Fed < 0.6f ? " · hungry" : "") + (!p.Warm ? " · cold" : "");
                if (Ui.Clickable(rr)) Later(() => SelectAndShow(p));
                by += 11;
            }
            int kids = T.People.Count(p => p.IsChild);
            if (kids > 0) { by += 4; Para("And " + kids + " children."); }
        }

        void ChronicleTab()
        {
            int lastDay = -1;
            for (int i = T.Log.Count - 1; i >= 0 && by < 4000; i--)
            {
                var l = T.Log[i];
                if (l.Day != lastDay)
                {
                    by += 3;
                    Ui.Text(bx, by, Defs.Seasons[Town.SeasonOf(l.Day)] + " " + (((l.Day - 1) % Balance.DaysPerYear) - Balance.SeasonStart(Town.SeasonOf(l.Day)) + 1) + ", " + (Balance.StartYear + (l.Day - 1) / Balance.DaysPerYear), Ui.Title);
                    by += 11; lastDay = l.Day;
                }
                by += Ui.Wrapped(new Rect(bx + 4, by, bw - 4, 200), l.Message, KindColor(l.Kind)) + 2;
            }
        }

        // ================================================================== inspector
        void Inspector()
        {
            float iw = 196;
            var r = Block(new Rect(4, 62, iw, Mathf.Min(H - 62 - 120, Mathf.Max(90, inspContentH + 34))));
            Ui.Panel(r);
            if (Ui.Button(new Rect(r.xMax - 22, r.y + 6, 15, 14), "", false, true, Icon.Close)) Later(() => gc.Select(Selection.None));
            var view = new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 14);
            inspScroll = GUI.BeginScrollView(view, inspScroll, new Rect(0, 0, view.width - 6, Mathf.Max(view.height, inspContentH)), false, false, GUIStyle.none, GUIStyle.none);
            bx = 2; by = 1; bw = view.width - 12;
            if (gc.Selected.Kind == SelectionKind.Person) PersonPanel(); else BuildingPanel();
            inspContentH = by + 4;
            GUI.EndScrollView();
            ScrollIndicator(view, inspScroll.y, inspContentH);
        }

        string ChoreText(Citizen p)
        {
            var t = T.GetBuilding(p.TaskId);
            switch (p.Chore)
            {
                case Chore.OwnStrip: return T.Season == 2 ? "reaping the family strip" : "working the family strip";
                case Chore.WeekWork:
                    if (t == null) return "week-work: gathering wood for the king";
                    if (t.Type == BuildingType.Quarry) return "week-work: breaking stone";
                    if (t.Type == BuildingType.Woodcutter) return "week-work: felling timber";
                    return T.Season == 2 ? "week-work: reaping the demesne" : "week-work on the demesne";
                case Chore.Build: return "building the " + (t != null ? t.Def.Name : "site");
                case Chore.Gather: return "gathering firewood";
                case Chore.Trade: return t == null ? "at work" : t.Type == BuildingType.Mill ? "grinding at the mill" : t.Type == BuildingType.Alehouse ? "brewing ale" : t.Type == BuildingType.Quarry ? "quarrying stone" : "felling and splitting wood";
                case Chore.Mass: return "at mass";
                case Chore.Pray: return T.Church != null ? "keeping the church" : "saying the offices";
                case Chore.Court: return "holding court";
                case Chore.Collect: return "collecting the taxes";
                case Chore.Deliver: return "taking the taxes to the castle";
                case Chore.Accounts: return "keeping the tax rolls";
                case Chore.Patrol: return "riding the village";
                case Chore.Play: return "playing";
                case Chore.Rest: return p.Health < 30 ? "laid up ill" : T.IsSunday ? "resting (Sunday)" : "resting";
                default: return "idle";
            }
        }

        void PersonPanel()
        {
            var p = T.GetCitizen(gc.Selected.Id);
            if (p == null) { Later(() => gc.Select(Selection.None)); return; }
            Ui.Text(bx, by, T.Title(p), Ui.Title); by += 10;
            Ui.Text(bx, by, Defs.RankName(p) + ", " + (int)p.Age, Ui.InkSoft); by += 13;
            Row("Doing", ChoreText(p));
            var home = T.HomeOf(p);
            Row("Home", home != null ? home.Def.Name : "a tent", home != null ? Ui.Ink : Ui.Bad);
            var w = T.GetBuilding(p.WorkId);
            if (w != null) Row("Trade", w.Def.Name);
            var strip = p.Rank == Rank.Serf || p.Rank == Rank.Reeve ? T.StripOf(p) : null;
            if (p.Rank == Rank.Serf || p.Rank == Rank.Reeve) Row("Family strip", strip != null ? Mathf.RoundToInt(strip.CropGrowth * 100) + "% grown" : "none", strip != null ? Ui.Ink : Ui.Bad);
            if (!p.IsNoble && p.Rank != Rank.Priest && p.Rank != Rank.Knight && p.Rank != Rank.Bailiff && !p.IsChild) Meter("Loyalty", p.Loyalty);
            Meter("Piety", p.Piety);
            Meter("Health", p.Health);
            Row("Fed", p.Fed >= 0.95f ? "well" : p.Fed >= 0.6f ? "short" : "hungry", p.Fed >= 0.95f ? Ui.Good : p.Fed >= 0.6f ? Ui.Warn : Ui.Bad);
            Row("Warm", p.Warm ? "yes" : "no", p.Warm ? Ui.Good : Ui.Bad);
            if (!p.IsChild && !p.IsNoble && p.Rank != Rank.Bailiff && p.Rank != Rank.Reeve) Row("Coin", M(p.Coin));
            if (p.IsNoble || p.Rank == Rank.Knight) Row("Temperament", Defs.TraitNames[(int)p.Trait]);
            if (p.Rank == Rank.Bailiff || p.Rank == Rank.Reeve) Row("Purse", M(p.Coin), p.Coin > 20 ? Ui.Warn : Ui.Ink);
            if (p.Pregnant > 0) Row("Expecting", "in " + p.Pregnant + " days");
            Heading("Family");
            var partner = T.GetCitizen(p.PartnerId);
            if (partner != null) FamilyLine(partner, p.Female ? "Husband" : "Wife");
            var mom = T.GetCitizen(p.MotherId); var dad = T.GetCitizen(p.FatherId);
            if (mom != null) FamilyLine(mom, "Mother");
            if (dad != null) FamilyLine(dad, "Father");
            foreach (var c in T.People.Where(c => c.MotherId == p.Id || c.FatherId == p.Id).OrderByDescending(c => c.Age)) FamilyLine(c, c.Female ? "Daughter" : "Son");
            if (partner == null && mom == null && dad == null && !T.People.Any(c => c.MotherId == p.Id || c.FatherId == p.Id)) Para("No family in the manor.");
        }

        void FamilyLine(Citizen c, string rel)
        {
            var rr = new Rect(bx, by - 1, bw, 11);
            if (Ui.Hover(rr)) Ui.Fill(rr, Ui.Hex("#ecd09a"));
            Ui.Text(bx, by, rel, Ui.InkSoft);
            Ui.Text(bx + bw, by, c.First + " (" + (int)c.Age + ")", Ui.Ink, Ui.Align.Right);
            if (Ui.Clickable(rr)) Later(() => SelectAndShow(c));
            by += 11;
        }

        void BuildingPanel()
        {
            var b = T.GetBuilding(gc.Selected.Id);
            if (b == null) { Later(() => gc.Select(Selection.None)); return; }
            var d = b.Def;
            Ui.Text(bx, by, b.Type == BuildingType.Church ? T.ChurchName + " Church" : b.Type == BuildingType.Keep ? "Castle " + T.ManorName : d.Name, Ui.Title); by += 12;
            if (!b.Built)
            {
                Row("Labour", Mathf.RoundToInt(b.Progress) + " of " + d.Work + " days");
                Ui.Bar(new Rect(bx, by, bw, 5), b.BuildFraction, Ui.Good); by += 9;
                if (d.Timber > 0) Row("Timber on site", Mathf.FloorToInt(b.Delivered[(int)Good.Timber]) + " / " + d.Timber, b.Delivered[(int)Good.Timber] >= d.Timber ? Ui.Good : Ui.Ink);
                if (d.Stone > 0) Row("Stone on site", Mathf.FloorToInt(b.Delivered[(int)Good.Stone]) + " / " + d.Stone, b.Delivered[(int)Good.Stone] >= d.Stone ? Ui.Good : Ui.Ink);
                int crew = T.People.Count(p => p.Chore == Chore.Build && p.TaskId == b.Id);
                Row("Working on it today", crew.ToString());
                Para("Serfs build on their week-work days; freemen without a trade hire on for a wage.");
            }
            else
            {
                Para(d.Blurb);
                if (d.IsField)
                {
                    var holder = T.GetCitizen(b.HolderId);
                    if (b.Type == BuildingType.Strip) Row("Held by", holder != null ? "the " + holder.Family + " family" : "nobody", holder != null ? Ui.Ink : Ui.Warn);
                    Row("Crop", Mathf.RoundToInt(b.CropGrowth * 100) + "% grown");
                    if (T.Season == 2 || b.Harvested > 0) Row("Reaped / standing", Mathf.FloorToInt(b.Harvested) + " / " + Mathf.FloorToInt(b.CropLeft));
                    int hands = T.People.Count(p => p.TaskId == b.Id && (p.Chore == Chore.OwnStrip || p.Chore == Chore.WeekWork));
                    Row("Hands today", hands.ToString());
                }
                if (b.Residents.Count > 0)
                {
                    Heading(b.Type == BuildingType.Keep || b.Type == BuildingType.Pavilion ? "The court" : "Living here");
                    foreach (var id in b.Residents) { var p = T.GetCitizen(id); if (p != null) FamilyLine(p, Defs.RankName(p)); }
                }
                if (d.Workers > 0)
                {
                    Heading("Working here");
                    foreach (var id in b.Employees) { var p = T.GetCitizen(id); if (p != null) FamilyLine(p, p.Female ? "Freewoman" : "Freeman"); }
                    for (int i = b.Employees.Count; i < d.Workers; i++) Row("", "a place for a freeman", Ui.Warn);
                    if (b.Type == BuildingType.Woodcutter) Row("Trees in reach", T.TreesNear(b).ToString(), T.TreesNear(b) > 0 ? Ui.Ink : Ui.Bad);
                    if (b.Type == BuildingType.Quarry) Row("Stone", T.QuarryOnHill(b) ? "a hillside face" : T.RocksNear(b) + " boulders in reach");
                    if (b.Employees.Count > 0 && !T.TradeNeeded(b)) Para("Idle: the stores are full and nothing waits on it.");
                }
                if (b.Type == BuildingType.Granary || b.Type == BuildingType.Cart || Town.IsPeasantHome(b.Type) || b.Type == BuildingType.Church || b.Type == BuildingType.Alehouse || b.Type == BuildingType.BailiffHall)
                {
                    bool any = false;
                    for (int g = 0; g < Balance.GoodCount; g++)
                    {
                        if (b.Stock[g] < 0.5f) continue;
                        if (!any) { Heading(b.Type == BuildingType.Church ? "Tithe barn" : Town.IsPeasantHome(b.Type) ? "Pantry and woodpile" : "Stores"); any = true; }
                        Row(Defs.GoodNames[g], Mathf.FloorToInt(b.Stock[g]).ToString());
                    }
                }
                if (b.Coin > 0.01f) Row("Tax chest", M(b.Coin));
                if (Town.IsPeasantHome(b.Type) && (b.OwedCoin > 0.01f || b.OwedGrain > 0.5f)) Row("Owes the reeve", M(b.OwedCoin) + " · " + Mathf.FloorToInt(b.OwedGrain) + " grain", b.Arrears > 0.5f ? Ui.Bad : Ui.Ink);
                if (b.Type == BuildingType.Mill) Row("Ground today", Mathf.FloorToInt(T.Today != null ? T.Today.Milled : 0) + " grain");
                if (b.Type == BuildingType.Church) { Row("At mass last Sunday", b.Attendance.ToString()); Row("Graves", b.Graves.ToString()); }
            }
            if (b.Type != BuildingType.Cart && b.Type != BuildingType.Pavilion)
            {
                by += 6;
                bool confirm = confirmPullDown == b.Id;
                if (Ui.Button(new Rect(bx, by, bw, 15), confirm ? "Click again to pull it down" : b.Built ? "Pull down" : "Clear the site"))
                {
                    if (confirm) { var bb = b; Later(() => { T.Remove(bb); gc.Select(Selection.None); confirmPullDown = -1; }); }
                    else confirmPullDown = b.Id;
                }
                by += 18;
            }
        }

        // ================================================================== map tooltip
        void MapTooltip()
        {
            if (!gc.PointerOnMap || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;
            string s = null; Color c = Ui.Ink; var h = gc.HoverTile;
            if (gc.Tool == ToolKind.Inspect)
            {
                var pick = gc.View.PickAt(gc.HoverPixel);
                if (pick.Kind == SelectionKind.Person)
                {
                    var p = T.GetCitizen(pick.Id);
                    if (p != null) s = T.Title(p) + "\n" + Defs.RankName(p) + " · " + ChoreText(p);
                }
                else if (pick.Kind == SelectionKind.Building)
                {
                    var b = T.GetBuilding(pick.Id);
                    if (b != null)
                    {
                        s = b.Def.Name + (b.Built ? "" : " (being built, " + Mathf.RoundToInt(b.BuildFraction * 100) + "%)");
                        if (b.Built && Town.IsHome(b.Type)) s += "\n" + (b.Residents.Count == 0 ? "Empty" : b.Residents.Count + " living here");
                        if (b.Built && b.Type == BuildingType.Strip) { var hd = T.GetCitizen(b.HolderId); s += "\n" + (hd != null ? "The " + hd.Family + " family's strips" : "No family holds it"); }
                    }
                }
                else
                {
                    byte t = T.TerrainAt(h.x, h.y);
                    if (t == Tile.Hill) s = "Hills: too steep to build";
                    else if (t == Tile.Tree) s = "The king's woods";
                    else if (t == Tile.Sapling) s = "Sapling";
                    else if (t == Tile.Rock) s = "Boulder";
                    else if (t == Tile.Water && T.OccupantAt(h.x, h.y) == null) s = "Water: a track can ford it";
                }
            }
            else if (gc.Tool == ToolKind.Clear)
            {
                byte t = T.TerrainAt(h.x, h.y); var b = T.OccupantAt(h.x, h.y);
                if (b != null && b.Type != BuildingType.Road) s = b.Def.Name + ": select it to pull it down";
                else if (t == Tile.Tree) s = "Fell tree · +2 timber";
                else if (t == Tile.Sapling) s = "Pull sapling";
                else if (t == Tile.Rock) s = "Break boulder · +3 stone";
                else if (t == Tile.Stump) s = "Pull stump";
            }
            else
            {
                var d = Defs.Get(gc.BuildType);
                var a = gc.PlacementAnchor;
                string err = T.CanPlace(gc.BuildType, a.x, a.y);
                if (err != null) { s = err; c = Ui.Bad; }
                else s = d.Name + (d.Type == BuildingType.Road ? "" : " · " + Town.MaterialsText(d));
            }
            if (s == null) return;
            var m = Event.current.mousePosition;
            var lines = s.Split('\n');
            float tw = lines.Max(l => PixelFont.Width(l)) + 12, ht = lines.Length * PixelFont.LineH + 7;
            var r = new Rect(Mathf.Min(m.x + 12, W - tw - 4), Mathf.Min(m.y + 14, H - ht - 4), tw, ht);
            Ui.Tip(r);
            float y = r.y + 5;
            foreach (var l in lines) { Ui.Text(r.x + 6, y, l, c); y += PixelFont.LineH; c = Ui.InkSoft; }
        }
    }
}
