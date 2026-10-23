using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    public partial class Town
    {
        // =====================================================================
        // The day's plan: who does what
        // =====================================================================
        /// <summary>The first days of the week belong to the lord (week-work); Sunday to God; the rest to the serfs' own strips.</summary>
        public bool IsWeekWorkDay { get { return !IsSunday && WeekdayIndex < Policy.WeekWork; } }

        public static int SiteCrew(BuildingDef d) { return Math.Max(2, Math.Min(6, d.W * d.H / 2 + 1)); }

        /// <summary>Whether labour can move a building site forward today: there's work done on what's delivered, or materials to haul.</summary>
        bool SiteCanProgress(Building s)
        {
            var d = s.Def;
            if (s.Progress < s.MaterialsFraction * d.Work - 0.01f) return true;
            if (s.Delivered[(int)Good.Timber] < d.Timber && StockForSite(s, Good.Timber) > 0.5f) return true;
            if (s.Delivered[(int)Good.Stone] < d.Stone && StockForSite(s, Good.Stone) > 0.5f) return true;
            return d.Timber + d.Stone == 0 && s.Progress < d.Work;
        }

        bool FieldNeedsWork(Building f)
        {
            if (!f.Built) return false;
            int s = Season;
            if (s <= 1) return f.CropGrowth < 1f;
            if (s == 2) return f.CropLeft > 0.5f;
            return false;
        }

        public void PlanDay()
        {
            foreach (var p in People) { p.Chore = Chore.None; p.TaskId = -1; }
            var sites = Sites.Where(SiteCanProgress).OrderBy(s => s.Type == BuildingType.Road ? 9 : s.Def.IsWall ? 6 : s.Def.IsField ? 0 : s.Type == BuildingType.Granary || s.Type == BuildingType.VillageCross ? 1 : s.Type == BuildingType.Mill ? 2 : s.Type == BuildingType.Cottage ? 3 : 4).ThenBy(s => s.Def.Timber + s.Def.Stone - s.Delivered[(int)Good.Timber] - s.Delivered[(int)Good.Stone]).ThenBy(s => s.Id).ToList();   // the small jobs first: a castle mustn't hold up every house
            var crew = sites.ToDictionary(s => s.Id, s => 0);
            var demesnes = OfType(BuildingType.Demesne).Where(FieldNeedsWork).ToList();
            // sites waiting on timber or stone the stores don't have: week-work goes to the quarry and the woods
            bool needStone = Sites.Any(x => x.Delivered[(int)Good.Stone] < x.Def.Stone) && StoreStock(Good.Stone) < 1;
            bool needTimber = Sites.Any(x => x.Delivered[(int)Good.Timber] < x.Def.Timber) && StoreStock(Good.Timber) < 1;
            var quarry = needStone ? OfType(BuildingType.Quarry).FirstOrDefault(q => QuarryOnHill(q) || RocksNear(q) > 0) : null;
            var woods = needTimber ? OfType(BuildingType.Woodcutter).FirstOrDefault(w => TreesNear(w) > 0) : null;
            int mc = 0;
            Dictionary<int, float> fieldHands = null;
            var seat = Seat; var church = Church;
            int dc = 0;
            Func<Building> nextSite = () =>
            {
                foreach (var s in sites) if (crew[s.Id] < SiteCrew(s.Def)) { crew[s.Id]++; return s; }
                return null;
            };

            foreach (var p in People.OrderBy(p => p.Rank == Rank.Freeman ? 1 : 0).ThenBy(p => p.Id))
            {
                if (p.Health < 30) { p.Chore = Chore.Rest; continue; }
                if (MassToday) { p.Chore = Chore.Mass; p.TaskId = church.Id; continue; }
                if (p.IsChild)
                {
                    p.Chore = Chore.Play;
                    continue;
                }
                switch (p.Rank)
                {
                    case Rank.King: case Rank.Noble:
                        p.Chore = Chore.Court; p.TaskId = seat != null ? seat.Id : -1; continue;
                    case Rank.Knight:
                        p.Chore = Chore.Patrol; continue;
                    case Rank.Priest:
                        p.Chore = Chore.Pray; p.TaskId = church != null ? church.Id : -1; continue;
                    case Rank.Steward:
                        // Tuesday he carries the chest to the castle; other days he keeps the books
                        var chest = TaxChest;
                        if (WeekdayIndex == 1 && chest != null && (chest.Coin > 0.01f || chest.ChestGrain > 0.5f)) { p.Chore = Chore.Deliver; p.TaskId = chest.Id; }
                        else { p.Chore = Chore.Accounts; p.TaskId = chest != null ? chest.Id : -1; }
                        continue;
                    case Rank.Baron:
                        { var bar = BaronyOfBaron(p); p.Chore = Chore.Court; p.TaskId = bar != null ? bar.ManorId : -1; }
                        continue;
                    case Rank.Collector:
                        if (p.Id == Crown.CollectorId)
                        {
                            // Monday he rides out to the crown's villages and the manors
                            var ch = TaxChest;
                            if (WeekdayIndex == 0) { p.Chore = Chore.Collect; p.TaskId = ch != null ? ch.Id : -1; }
                            else { p.Chore = Chore.Accounts; p.TaskId = ch != null ? ch.Id : -1; }
                        }
                        else
                        {
                            // Saturday a baron's collector rides round the baron's villages
                            var bar = BaronyOfBaron(p);
                            p.Chore = bar != null && WeekdayIndex == 5 ? Chore.Collect : Chore.Accounts;
                            p.TaskId = bar != null ? bar.ManorId : -1;
                        }
                        continue;
                }
                if (IsSunday) { p.Chore = Chore.Rest; continue; }
                if (p.Age >= 64) { p.Chore = Chore.Rest; continue; }

                if (p.Rank == Rank.Freeman || (p.Rank == Rank.Reeve && p.WasFree && WeekdayIndex != 4))   // a freeman reeve keeps to his trade but for collecting day
                {
                    var w = GetBuilding(p.WorkId);
                    if (w != null && w.Built && w.Type == BuildingType.MerchantHouse)
                    {
                        // the merchant's week: buying at home, the road, the capital's market, the road home, selling
                        int wd = WeekdayIndex; var cap = CrossOf(Capital);
                        p.Chore = wd == 1 || wd == 3 ? Chore.Travel : wd == 2 ? Chore.Market : Chore.Trade;
                        p.TaskId = wd == 2 && cap != null ? cap.Id : w.Id;
                        continue;
                    }
                    var hearth = HomeOf(p);
                    bool cold = Season >= 2 && hearth != null && hearth.Stock[(int)Good.Firewood] < 12;
                    if (w != null && w.Built && !cold && TradeNeeded(w)) { p.Chore = Chore.Trade; p.TaskId = w.Id; continue; }
                    // freemen without a trade hire out as day labourers: at harvest reaping comes first, for a family that can't get
                    // its crop in or on the lord's demesne; otherwise the crown's building work for a wage, if he can pay
                    if (fieldHands == null) fieldHands = FieldHands();
                    if (Season == 2) { var rf = HireField(p, fieldHands); if (rf != null) { p.Chore = Chore.Labour; p.TaskId = rf.Id; continue; } }
                    if (Coin >= Policy.FreeWage) { var s = nextSite(); if (s != null) { p.Chore = Chore.Build; p.TaskId = s.Id; continue; } }
                    var lf = HireField(p, fieldHands);
                    if (lf != null) { p.Chore = Chore.Labour; p.TaskId = lf.Id; continue; }
                    var home = HomeOf(p);
                    p.Chore = home != null && home.Stock[(int)Good.Firewood] < 40 ? Chore.Gather : Chore.Rest;
                    continue;
                }

                // the reeve's collecting day
                if (p.Rank == Rank.Reeve && WeekdayIndex == 4) { var vc = CrossOf(VillageOf(p)); p.Chore = Chore.Collect; p.TaskId = vc != null ? vc.Id : -1; continue; }
                // at harvest a family reaps its own strip first: the lord doesn't call week-work while the crop stands
                var own = StripOf(p);
                if (Season == 2 && own != null && FieldNeedsWork(own)) { p.Chore = Chore.OwnStrip; p.TaskId = own.Id; continue; }
                // serfs and the reeve
                if (IsWeekWorkDay || p.Rank == Rank.Reeve)
                {
                    var s = nextSite();
                    if (s != null) { p.Chore = Chore.Build; p.TaskId = s.Id; continue; }
                    var mat = quarry != null && woods != null ? (mc++ % 2 == 0 ? quarry : woods) : quarry ?? woods;
                    if (mat != null) { p.Chore = Chore.WeekWork; p.TaskId = mat.Id; continue; }
                    // gathering wood for the king's fires, if the stores want it; otherwise the reeve lets them go to their strips
                    if (IsWeekWorkDay && StoreStock(Good.Firewood) < 150) { p.Chore = Chore.WeekWork; p.TaskId = -1; continue; }
                }
                var strip = StripOf(p);
                if (strip != null && FieldNeedsWork(strip)) { p.Chore = Chore.OwnStrip; p.TaskId = strip.Id; continue; }
                var h = HomeOf(p);
                p.Chore = h == null || h.Stock[(int)Good.Firewood] < 50 ? Chore.Gather : Chore.Rest;
            }
            // children of ten and up follow a parent into the strips
            foreach (var c in People.Where(c => c.IsChild && c.Age >= 10 && c.Chore == Chore.Play))
            {
                var parent = GetCitizen(c.MotherId) ?? GetCitizen(c.FatherId);
                if (parent != null && parent.Chore == Chore.OwnStrip) { c.Chore = Chore.OwnStrip; c.TaskId = parent.TaskId; }
                else if (parent != null && parent.Chore == Chore.Gather) c.Chore = Chore.Gather;
            }
        }

        // =====================================================================
        // The day itself
        // =====================================================================
        public sealed class DayReport { public float Harvest, Tithe, Milled, Multure, Eaten, Short; public int Hungry, Cold, AtMass; }
        [NonSerialized] public DayReport Today = new DayReport();

        void SimulateDay()
        {
            if (Today == null) Today = new DayReport();
            Today = new DayReport();
            int si = Season;

            // ---- the day's work
            foreach (var p in People)
            {
                float hands = p.IsChild ? Balance.ChildFieldWork : p.Age > 58 ? 0.7f : 1f;
                if (p.Health < 50) hands *= 0.6f;
                var t = GetBuilding(p.TaskId);
                switch (p.Chore)
                {
                    case Chore.OwnStrip: if (t != null) FieldWork(t, hands, ReeveBonus(t), HomeOf(GetCitizen(t.HolderId) ?? p)); break;
                    case Chore.WeekWork:
                        if (t != null && t.Def.IsField) FieldWork(t, hands, ReeveBonus(t), null);
                        else if (t != null) Materials(t, hands);
                        else PutInStores(Good.Firewood, Balance.GatherWood * hands);
                        break;
                    case Chore.Build: if (t != null && !t.Built) BuildWork(t, p); break;
                    case Chore.Labour: if (t != null) Labour(t, p, hands); break;
                    case Chore.Gather: { var h = HomeOf(p); if (h != null) h.Stock[(int)Good.Firewood] += Balance.GatherWood * hands * (si == 3 ? 0.8f : 1f); } break;
                    case Chore.Trade: if (t != null && t.Type != BuildingType.MerchantHouse) { if (t.Def.IsShop) ShopWork(t, p, hands); else TradeWork(t, p); } break;
                }
            }
            foreach (var s in Sites.ToList()) if (s.Progress >= s.Def.Work - 0.001f && s.MaterialsFraction >= 0.999f) OnBuilt(s, false);

            Milling();
            Spoilage();
            ShopsMorning();
            Shopping();
            Eating(si);
            Warmth(si);
            Alehouse();
            ClothWear();
            Mass();
            Moods();

            // ---- the calendar
            int doy = DayOfYear;
            if (doy == 0) SpringStart();
            if (doy == Balance.SeasonStart(3) - 1) { Michaelmas(); CropsRot(); }
            if (doy == Balance.SeasonStart(3)) Martinmas();
            if (doy == Balance.SeasonStart(3) + 6) Christmas();
            if (doy == Balance.SeasonStart(2) - 1) AutumnComes();
            if (WeekdayIndex == 4) ReevesCollect();
            if (IsMarketDay) { BaronsCollect(); MarketDay(); }
            if (WeekdayIndex == 0) StewardCollects();
            if (WeekdayIndex == 1) StewardDelivers();
            if (IsSunday) { Weekly(); Trees(); BaronMoods(); ReeveVacancies(false); FreemenMoveOn(); WeeklyWants(); BaronsProvision(); }
            Merchants();
            Petitions();
            CourtBuys();
            StewardArrives();
            CheckRevolt();

            Lives();
            ComeOfAge();
            Matchmaking();
            SendPriest();
            Flight();
            Rehouse(); AssignStrips(); Hire();
            Procure();
            ShopPayroll();
        }

        /// <summary>Strips in a village with a reeve in office are worked better.</summary>
        float ReeveBonus(Building strip)
        {
            var r = ReeveOf(VillageOf(strip));
            return r != null && r.Health > 30 ? Balance.ReeveBonus : 1f;
        }

        void SpringStart()
        {
            foreach (var f in Buildings.Where(b => b.Def.IsField)) { f.CropGrowth = 0; f.CropLeft = 0; f.Harvested = 0; }
            ReeveVacancies(true);
            if (Year > Balance.StartYear) AddLog("A new year, " + Year + ". The fields are ploughed and sown again.", LogKind.Info);
        }

        /// <summary>The last day of summer: what has grown is ripe and stands waiting for the sickle.</summary>
        void AutumnComes()
        {
            foreach (var f in Buildings.Where(b => b.Def.IsField && b.Built))
            {
                f.CropLeft = f.CropGrowth * FieldYield(f);
                f.Harvested = 0;
            }
        }

        void CropsRot()
        {
            float lost = Buildings.Where(b => b.Def.IsField).Sum(b => b.CropLeft);
            foreach (var f in Buildings.Where(b => b.Def.IsField)) f.CropLeft = 0;
            if (lost > 20) AddLog(Math.Round(lost) + " measures of grain were never brought in and rotted in the fields.", LogKind.Bad);
        }

        /// <summary>Spring and summer: the crop grows with the hours put into it. Autumn: it's reaped. Every harvest gives the church its tithe.
        /// An open field's crop goes to the families holding shares in it, each its own strips' worth. A hired reaper keeps his cut.</summary>
        float FieldWork(Building f, float hands, float bonus, Building pantry, Building reaper = null)
        {
            int si = Season;
            if (si <= 1)
            {
                f.CropGrowth = Math.Min(1f, f.CropGrowth + FieldGrowth(f) * hands * bonus);
                return 0;
            }
            if (si != 2) return 0;
            float q = Math.Min(f.CropLeft, Balance.HarvestPerWorker * hands * bonus);
            if (q <= 0) return 0;
            f.CropLeft -= q; f.Harvested += q;
            float tithe = Church != null ? q * Balance.Tithe : 0;
            if (tithe > 0) Church.Stock[(int)Good.Grain] += tithe;
            float net = q - tithe;
            if (reaper != null) { float cut = net * Balance.ReaperShare; reaper.Stock[(int)Good.Grain] += cut; net -= cut; }
            Action<Building, float> toPantry = (home, g) =>
            {
                home.Stock[(int)Good.Grain] += g;
                if (home.VillageId >= 0) Assess(home, 0, g * Policy.HarvestTax);   // the crown's share, for the reeve to collect (a house outside every village goes untaxed)
            };
            if (f.Type == BuildingType.OpenField)
            {
                int held = Math.Max(1, HeldShares(f));
                for (int i = 0; i < Slots(f); i++)
                {
                    int h = HolderAt(f, i); if (h < 0) continue;
                    var home = HomeOf(GetCitizen(h));
                    if (home != null) toPantry(home, net / held); else PutInStores(Good.Grain, net / held);
                }
            }
            else if (pantry != null) toPantry(pantry, net);
            else PutInStores(Good.Grain, net);
            Today.Harvest += q; Today.Tithe += tithe;
            return q - tithe;
        }

        // =====================================================================
        // Day labour
        // =====================================================================
        /// <summary>Hands already set to each field today (serfs on their strips, week-work, hired men).</summary>
        Dictionary<int, float> FieldHands()
        {
            var d = new Dictionary<int, float>();
            foreach (var p in People)
                if ((p.Chore == Chore.OwnStrip || p.Chore == Chore.WeekWork || p.Chore == Chore.Labour) && p.TaskId >= 0)
                { float n; d.TryGetValue(p.TaskId, out n); d[p.TaskId] = n + (p.IsChild ? Balance.ChildFieldWork : 1f); }
            return d;
        }

        /// <summary>A field a freeman with nothing else to do can hire on to: at harvest, the strip or demesne in his own lands most likely
        /// to be left to rot; in spring and summer, the lord's demesne if the lord can pay.</summary>
        Building HireField(Citizen p, Dictionary<int, float> hands)
        {
            int si = Season, dom = DomainOf(p);
            bool lordPays = dom < 0 ? Coin >= Policy.FreeWage + Policy.CoinReserve * 0.5f : LordPurse(dom) >= Policy.FreeWage;
            Building best = null; float bestShort = 0;
            if (si == 2)
            {
                int left = Math.Max(1, DaysInSeason - DayOfSeason + 1);
                foreach (var f in Buildings)
                {
                    if (!f.Built || !f.Def.IsField || f.CropLeft < 1 || DomainOf(f) != dom) continue;
                    if (f.Type == BuildingType.Demesne ? !lordPays : f.Type == BuildingType.OpenField ? HeldShares(f) == 0 : HomeOf(GetCitizen(f.HolderId)) == null) continue;
                    float n; hands.TryGetValue(f.Id, out n);
                    float shortBy = f.CropLeft - n * Balance.HarvestPerWorker * left * 0.9f;
                    if (shortBy > bestShort) { bestShort = shortBy; best = f; }
                }
            }
            else if (si <= 1 && lordPays)
                foreach (var f in Buildings)
                {
                    if (!f.Built || f.Type != BuildingType.Demesne || f.CropGrowth >= 0.98f || DomainOf(f) != dom) continue;
                    float n; hands.TryGetValue(f.Id, out n);
                    if (n < 4) { best = f; break; }
                }
            if (best != null) { float m; hands.TryGetValue(best.Id, out m); hands[best.Id] = m + 1; }
            return best;
        }

        /// <summary>A hired day in the fields. The lord pays a day's wage on his demesne; a family pays its reaper in grain, a share of what he cuts.</summary>
        void Labour(Building f, Citizen p, float hands)
        {
            bool lords = f.Type == BuildingType.Demesne;
            var pantry = lords || f.Type == BuildingType.OpenField ? null : HomeOf(GetCitizen(f.HolderId));
            if (!lords && f.Type == BuildingType.Strip && pantry == null) return;
            if (!lords && f.Type == BuildingType.OpenField && HeldShares(f) == 0) return;
            FieldWork(f, hands, ReeveBonus(f), pantry, lords ? null : HomeOf(p));
            if (lords)
            {
                int dom = DomainOf(f);
                if (dom < 0) { float w = Math.Min(Policy.FreeWage, Math.Max(0, Coin)); Coin -= w; WeekOut += w; p.Coin += w; Book("Wages: the demesne", -w); }
                else p.Coin += TakeFromLord(dom, Policy.FreeWage);
            }
        }

        void BuildWork(Building s, Citizen p)
        {
            var d = s.Def;
            if (p.Rank == Rank.Freeman)
            {
                float w = Math.Min(Policy.FreeWage, Math.Max(0, Coin));
                Coin -= w; WeekOut += w; p.Coin += w; Book("Wages: building", -w);
            }
            // haul first, then build on what's here
            foreach (var g in new[] { Good.Timber, Good.Stone })
            {
                float need = (g == Good.Timber ? d.Timber : d.Stone) - s.Delivered[(int)g];
                if (need <= 0.01f) continue;
                if (s.Progress < s.MaterialsFraction * d.Work - 0.5f) break;   // work on what's here first
                float got = TakeForSite(s, g, Math.Min(need, Balance.HaulPerDay));
                if (got > 0) { s.Delivered[(int)g] += got; return; }
            }
            float cap = s.MaterialsFraction * d.Work;
            if (d.Timber + d.Stone == 0) cap = d.Work;
            s.Progress = Math.Min(cap, s.Progress + 1f);
        }

        /// <summary>A day at one of the crown's works (the woods and the quarry), for the crown's wage, paid only for a day's real work.
        /// The miller isn't paid in coin: he keeps his share of the multure (see Milling).</summary>
        void TradeWork(Building b, Citizen p)
        {
            bool worked = false;
            switch (b.Type)
            {
                case BuildingType.Woodcutter:
                {
                    // split timber for the fires when firewood runs low, otherwise fell
                    var wv = VillageOf(b);
                    if (!(LocalStock(wv, Good.Firewood) < 80 && LocalStock(wv, Good.Timber) >= 4))
                    {
                        if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Tree) b.TargetTile = NearestTile(b, Tile.Tree);
                        if (b.TargetTile < 0) break;
                        worked = true;
                        b.ChopWork += Balance.TreeWork;
                        if (b.ChopWork >= 1f)
                        {
                            b.ChopWork = 0;
                            SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Stump);
                            PutLocal(wv, Good.Timber, Balance.TimberPerTree);
                            b.TargetTile = -1;
                        }
                    }
                    else
                    {
                        float t = TakeLocal(wv, Good.Timber, Balance.SplitPerWorker);
                        PutLocal(wv, Good.Firewood, t * Balance.FirewoodPerTimber);
                        worked = t > 0;
                    }
                    break;
                }
                case BuildingType.Quarry:
                {
                    if (QuarryOnHill(b)) { PutLocal(VillageOf(b), Good.Stone, Balance.StonePerWorker); worked = true; break; }
                    if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Rock) b.TargetTile = NearestTile(b, Tile.Rock);
                    if (b.TargetTile < 0) break;
                    worked = true;
                    PutLocal(VillageOf(b), Good.Stone, Balance.StonePerWorker);
                    b.ChopWork += Balance.StonePerWorker / Balance.StonePerRock;
                    if (b.ChopWork >= 1f) { b.ChopWork = 0; SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Grass); b.TargetTile = -1; }
                    break;
                }
            }
            if (worked)
            {
                float w = Math.Min(Policy.FreeWage, Math.Max(0, Coin));
                Coin -= w; WeekOut += w; p.Coin += w; Book("Wages: " + b.Def.Name, -w);
            }
        }

        /// <summary>Serfs on week-work at the quarry or in the woods: unpaid, and not as handy as the freemen who do it every day.</summary>
        void Materials(Building b, float hands)
        {
            if (b.Type == BuildingType.Quarry)
            {
                if (QuarryOnHill(b)) { PutLocal(VillageOf(b), Good.Stone, Balance.StonePerWorker * 0.8f * hands); return; }
                if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Rock) b.TargetTile = NearestTile(b, Tile.Rock);
                if (b.TargetTile < 0) return;
                PutLocal(VillageOf(b), Good.Stone, Balance.StonePerWorker * 0.8f * hands);
                b.ChopWork += Balance.StonePerWorker * 0.8f * hands / Balance.StonePerRock;
                if (b.ChopWork >= 1f) { b.ChopWork = 0; SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Grass); b.TargetTile = -1; }
                return;
            }
            if (b.Type == BuildingType.Woodcutter)
            {
                if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Tree) b.TargetTile = NearestTile(b, Tile.Tree);
                if (b.TargetTile < 0) return;
                b.ChopWork += Balance.TreeWork * 0.8f * hands;
                if (b.ChopWork >= 1f) { b.ChopWork = 0; SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Stump); PutLocal(VillageOf(b), Good.Timber, Balance.TimberPerTree); b.TargetTile = -1; }
            }
        }

        /// <summary>Whether a trade has work today: the woods and quarry stand idle when the stores are full and nothing waits on them.</summary>
        public bool TradeNeeded(Building b)
        {
            if (b.Def.IsShop) return ShopNeeded(b);
            switch (b.Type)
            {
                // the crown's works only run when the stores are short of what the sites still need
                case BuildingType.Quarry:
                    return LocalStock(VillageOf(b), Good.Stone) < SitesStillNeed(Good.Stone, DomainOf(b)) + 30;
                case BuildingType.Woodcutter:
                    return LocalStock(VillageOf(b), Good.Timber) < SitesStillNeed(Good.Timber, DomainOf(b)) + 40 || LocalStock(VillageOf(b), Good.Firewood) < 80;
                case BuildingType.MerchantHouse:
                    return true;
                default: return true;
            }
        }

        public float SitesStillNeed(Good g, int domain = -99)
        {
            float n = 0;
            foreach (var s in Buildings) if (!s.Built && (domain == -99 || DomainOf(s) == domain || domain < 0)) n += Math.Max(0, (g == Good.Timber ? s.Def.Timber : s.Def.Stone) - s.Delivered[(int)g]);
            return n;
        }

        int NearestTile(Building b, byte kind)
        {
            var d = b.Def; int r = d.Radius, cx = b.X + d.W / 2, cy = b.Y + d.H / 2, best = -1, bd = int.MaxValue;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    int dd = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (dd > r * r || dd >= bd || TerrainAt(x, y) != kind) continue;
                    bd = dd; best = y * Cols + x;
                }
            return best;
        }

        // ---- the mill: every household's grain is ground here, and the lord takes his share
        public Building WorkingMill { get { return OfType(BuildingType.Mill).FirstOrDefault(m => m.Employees.Count > 0); } }
        void Milling()
        {
            var mills = OfType(BuildingType.Mill).Where(m => m.Employees.Count > 0).ToList();
            if (mills.Count == 0) return;
            float cap = mills.Count * Balance.MillPerDay;
            float multure = Policy.Multure * (RulerIs(Trait.Greedy) ? 1.5f : 1f);
            // households keep about ten days of flour ground
            var homes = Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0).OrderBy(b => b.Stock[(int)Good.Flour]).ToList();
            foreach (var h in homes)
            {
                if (cap <= 0) break;
                float need = Needs(h) * 10f - h.Stock[(int)Good.Flour];
                float q = Math.Min(Math.Min(need, h.Stock[(int)Good.Grain]), cap);
                if (q <= 1) continue;
                h.Stock[(int)Good.Grain] -= q; cap -= q;
                float share = q * multure;
                h.Stock[(int)Good.Flour] += q - share;
                PutInStores(Good.Flour, MillerTakes(share));
                Today.Milled += q; Today.Multure += share;
            }
            // and the lord's own grain for the household
            float lordNeed = RoyalNeeds() * 10f - StoreStock(Good.Flour);
            if (lordNeed > 0 && cap > 0)
            {
                float q = TakeFromStores(Good.Grain, Math.Min(lordNeed, Math.Min(cap, Math.Max(0, StoreStock(Good.Grain) - 20))));
                PutInStores(Good.Flour, q); Today.Milled += q;
            }
        }

        /// <summary>The miller's toll: he keeps a share of the multure in flour for his family (his pay), the crown gets the rest.</summary>
        float MillerTakes(float share)
        {
            var mill = WorkingMill; if (mill == null) return share;
            var miller = GetCitizen(mill.Employees[0]); var home = HomeOf(miller);
            if (home == null) return share;
            if (VillageOf(mill) != null && !VillageOf(mill).Capital) { float his0 = share * Balance.MillerShare; home.Stock[(int)Good.Flour] += his0; PutLocal(VillageOf(mill), Good.Flour, share - his0); return 0; }
            float his = share * Balance.MillerShare;
            home.Stock[(int)Good.Flour] += his;
            return share - his;
        }

        /// <summary>Days until this year's harvest is in (or next year's, once autumn's well along).</summary>
        public int DaysUntilHarvest
        {
            get
            {
                // once the reaping has begun, what's brought in has to last until next year's harvest
                int target = Balance.SeasonStart(2) + 10, doy = DayOfYear;
                return doy < Balance.SeasonStart(2) ? target - doy : Balance.DaysPerYear - doy + target;
            }
        }
        /// <summary>Grain the granary must keep to feed the lord's household until the harvest is in.</summary>
        public float HouseholdKeep { get { return RoyalNeeds() / (WorkingMill != null ? 1f : Balance.GrainFeeds) * DaysUntilHarvest - StoreStock(Good.Flour); } }
        /// <summary>What the merchant may not buy: the household's keep plus the reserve the lord sets.</summary>
        /// <summary>What the merchant may not buy from the royal granary: its whole stockpile target.</summary>
        public float RoyalKeep { get { return CrownTarget; } }
        /// <summary>What the royal granary keeps back from the baker and the barons: the court's and the freemen's bread until harvest.</summary>
        public float HomeKeep { get { return (Math.Max(0, HouseholdKeep) + FreemenKeep + 200) * 0.5f; } }
        /// <summary>Grain the freemen will want to buy from the granary until the harvest (they have no strips of their own).</summary>
        public float FreemenKeep
        {
            get
            {
                float n = 0;
                foreach (var h in Buildings) { if (!h.Built || !IsPeasantHome(h.Type) || h.Residents.Count == 0) continue; var hd = HeadOf(h); if (hd != null && (hd.Rank == Rank.Freeman || hd.WasFree)) n += Needs(h); }
                return n * DaysUntilHarvest / Balance.GrainFeeds * 0.3f;   // they buy much of it from serf families
            }
        }

        // ---- food
        public float Needs(Building home)
        {
            float n = 0;
            foreach (var id in home.Residents) { var p = GetCitizen(id); if (p != null) n += p.IsChild ? Balance.ChildAppetite : Balance.Appetite; }
            return n;
        }
        public float RoyalNeeds()
        {
            float n = 0;
            foreach (var p in People) if (RoyalHousehold(p) || (p.Rank == Rank.Priest && Church == null)) n += p.IsChild ? Balance.ChildAppetite : Balance.Appetite;
            return n;
        }

        /// <summary>Takes food for one person: bought meat, fish and bread first, then their own flour, then grain as pottage.
        /// Returns how much of their need was met, and notes what they had.</summary>
        float Eat(float[] stock, float need, Citizen p)
        {
            float got = 0;
            foreach (var g in new[] { Good.Meat, Good.Fish, Good.Bread })
            {
                if (got >= need) break;
                float feeds = g == Good.Meat ? Balance.MeatFeeds : g == Good.Fish ? Balance.FishFeeds : Balance.BreadFeeds;
                float q = Math.Min(stock[(int)g], (need - got) / feeds);
                if (q <= 0.001f) continue;
                stock[(int)g] -= q; got += q * feeds;
                if (p != null && q > need * 0.2f) { if (g == Good.Meat) p.MeatDay = Day; else if (g == Good.Fish) p.FishDay = Day; else p.BreadDay = Day; }
            }
            return got + Eat(stock, need - got);
        }

        static float Eat(float[] stock, float need)
        {
            float got = 0;
            if (need <= 0) return 0;
            float f = Math.Min(stock[(int)Good.Flour], need / Balance.FlourFeeds);
            stock[(int)Good.Flour] -= f; got += f * Balance.FlourFeeds;
            if (got < need)
            {
                float g = Math.Min(stock[(int)Good.Grain], (need - got) / Balance.GrainFeeds);
                stock[(int)Good.Grain] -= g; got += g * Balance.GrainFeeds;
            }
            return got;
        }

        float EatFromStores(float need, Village v = null)
        {
            if (v != null && !v.Capital)
            {
                float lg = 0;
                foreach (var g in new[] { Good.Meat, Good.Fish, Good.Bread, Good.Flour })
                {
                    if (lg >= need) break;
                    float feeds = g == Good.Meat ? Balance.MeatFeeds : g == Good.Fish ? Balance.FishFeeds : 1f;
                    lg += TakeLocal(v, g, (need - lg) / feeds) * feeds;
                }
                if (lg < need) lg += TakeLocal(v, Good.Grain, (need - lg) / Balance.GrainFeeds) * Balance.GrainFeeds;
                return lg;
            }
            float got = 0;
            // the court's table: meat, fish and bread bought from the village shops first
            got += TakeFromStores(Good.Meat, need / Balance.MeatFeeds) * Balance.MeatFeeds;
            if (got < need) got += TakeFromStores(Good.Fish, (need - got) / Balance.FishFeeds) * Balance.FishFeeds;
            if (got < need) got += TakeFromStores(Good.Bread, need - got);
            if (got >= need) return got;
            float f = TakeFromStores(Good.Flour, need - got); got += f;
            if (got < need) got += TakeFromStores(Good.Grain, (need - got) / Balance.GrainFeeds) * Balance.GrainFeeds;
            return got;
        }

        void Eating(int si)
        {
            var church = Church;
            bool dole = Policy.Dole || RulerIs(Trait.Gentle);
            foreach (var p in People)
            {
                float need = p.IsChild ? Balance.ChildAppetite : Balance.Appetite;
                float got = 0;
                var home = HomeOf(p);
                if (RoyalHousehold(p) || (p.Rank == Rank.Priest && church == null)) { got = EatFromStores(need); if (got < need * 0.7f) got += Purvey(-1, need - got); }
                else if (p.Rank == Rank.Priest && church != null) { got = Eat(church.Stock, need); if (got < need) got += EatFromStores(need - got); }
                else if (p.Rank == Rank.Steward || p.Rank == Rank.Collector || p.Rank == Rank.Baron)
                {
                    bool manor = home != null && home.Type == BuildingType.Manor;
                    if (home != null && !IsPeasantHome(home.Type)) got = Eat(home.Stock, need);
                    if (got < need) got += EatFromStores(need - got, manor ? null : VillageOf(p));   // a baron with a bare granary is fed at the king's table
                    if (got < need * 0.7f) got += Purvey(manor ? DomainOf(home) : -1, need - got);
                }
                else
                {
                    // the toft garden gives a little each day outside winter
                    if (home != null && IsPeasantHome(home.Type)) got += Math.Min(need, Balance.GardenBySeason[si] / Math.Max(1, home.Residents.Count) * 1.5f);
                    if (home != null && got < need) got += Eat(home.Stock, need - got, p);
                    // the hungry: alms from the church's tithe, then the lord's dole
                    if (got < need * 0.7f && church != null && church.Stock[(int)Good.Grain] > 0.5f && Priest != null) got += Eat(church.Stock, need * 0.8f - got);
                    var pv = VillageOf(p);
                    if (got < need * 0.7f && dole && (pv == null || pv.Capital || DomainOf(pv) < 0 && BarnsOf(pv).All(b => b.Stock[(int)Good.Grain] < 1) ? StoreStock(Good.Grain) > Math.Max(10, HouseholdKeep) : LocalStock(pv, Good.Grain) > 10))
                        got += EatFromStores(need * 0.8f - got, pv);
                }
                p.Fed = Math.Min(1f, got / need);
                Today.Eaten += got; Today.Short += Math.Max(0, need - got);
                if (p.Fed < 0.6f) { p.Hungry++; p.Health -= (1 - p.Fed) * (p.IsChild ? 9 : 7); Today.Hungry++; }
                else { p.Hungry = 0; p.Health = Math.Min(100, p.Health + 1.5f); }
            }
        }

        void Warmth(int si)
        {
            float perHome = Balance.FirewoodBySeason[si];
            foreach (var home in Buildings.Where(b => b.Built && b.Residents.Count > 0))
            {
                bool warm;
                if (home.Type == BuildingType.Keep || home.Type == BuildingType.Pavilion || home.Type == BuildingType.Church || home.Type == BuildingType.StewardHall || home.Type == BuildingType.Manor)
                {
                    float need = home.Type == BuildingType.Keep ? Balance.KeepFirewood * Balance.FirewoodBySeason[si] / 0.6f : perHome;
                    warm = (home.Type == BuildingType.Manor ? TakeLocal(VillageOf(home), Good.Firewood, need) : TakeFromStores(Good.Firewood, need)) >= need * 0.9f || si == 1;
                }
                else
                {
                    // a family in rags burns more to keep warm
                    float want = perHome * (si >= 2 && Ragged(home) ? 1.3f : 1f);
                    float t = Math.Min(home.Stock[(int)Good.Firewood], want);
                    home.Stock[(int)Good.Firewood] -= t;
                    warm = t >= want * 0.9f || si == 1;
                }
                foreach (var id in home.Residents)
                {
                    var p = GetCitizen(id); if (p == null) continue;
                    p.Warm = warm;
                    if (!warm && si == 3) { p.Health -= p.IsChild || p.Age > 55 ? 4 : 2.5f; Today.Cold++; }
                }
            }
            // those camping out in tents
            foreach (var p in People.Where(p => p.HomeId < 0))
            {
                p.Warm = si <= 1;
                if (si == 3) { p.Health -= 3.5f; Today.Cold++; }
                else if (si == 2) p.Health -= 0.6f;
            }
        }

        /// <summary>Of an evening grown folk with a coin to spare stop in for a cup.</summary>
        void Alehouse()
        {
            foreach (var b in OfType(BuildingType.Alehouse)) b.SoldToday = 0;
            var houses = OfType(BuildingType.Alehouse).Where(b => b.Employees.Count > 0).ToList();
            if (houses.Count == 0 || IsSunday) return;
            foreach (var p in People.Where(p => !p.IsChild && !RoyalHousehold(p) && p.Rank != Rank.Priest && p.Coin >= 0.45f))
            {
                if (Day - p.AleDay < 3 || !Chance(0.35)) continue;
                var house = houses.Where(h => h.Stock[(int)Good.Ale] >= 1).OrderBy(h => h.SoldToday).FirstOrDefault();
                if (house == null) break;
                float cup = AskPrice(house); if (p.Coin < cup + 0.3f) continue;
                house.Stock[(int)Good.Ale] -= 1; house.SoldToday++;
                p.Coin -= cup; p.AleDay = Day;
                Takings(house, cup);
            }
        }

        void Mass()
        {
            if (!MassToday) return;
            var church = Church; church.Attendance = 0;
            float plate = 0;
            foreach (var p in People.Where(p => p.Chore == Chore.Mass))
            {
                church.Attendance++;
                p.MassDay = Day;
                p.Piety = Math.Min(100, p.Piety + (100 - p.Piety) * (RulerIs(Trait.Pious) ? 0.1f : 0.08f));
                if (p.Coin > 0.3f) { float c = Math.Min(p.Coin, 0.05f); p.Coin -= c; plate += c; }
            }
            Today.AtMass = church.Attendance;
            if (RulerIs(Trait.Pious) && Coin > 5) { float g = Math.Min(2, Coin * 0.05f); Coin -= g; WeekOut += g; plate += g; Book("Alms to the church", -g); }
            // the priest keeps the plate for the church's poor box: it buys grain for alms in a bad week
            if (plate > 0) { church.Stock[(int)Good.Grain] += plate / Balance.MerchantGrain * 0.5f; }
        }

        /// <summary>Loyalty to the lord drifts toward what folk feel they deserve: fed, warm, housed, not too burdened, God-fearing.</summary>
        void Moods()
        {
            var r = Ruler;
            float trait = 0;
            if (r != null) trait = r.Trait == Trait.Just ? 8 : r.Trait == Trait.Cruel ? -10 : r.Trait == Trait.Gentle ? 4 : r.Trait == Trait.Greedy ? -4 : 0;
            bool hasKeep = Keep != null, hasChurch = Church != null;
            foreach (var p in People)
            {
                // piety fades without the church
                p.Piety = Math.Max(0, p.Piety - (hasChurch && Priest != null ? 0.3f : 0.5f));
                if (RoyalHousehold(p) || IsOfficer(p) || p.IsChild) { if (p.IsChild) p.Loyalty = 55; continue; }
                float target = 50 + trait;
                target += p.Fed >= 0.95f ? 10 : p.Fed < 0.6f ? -25 : -8;
                if (!p.Warm && Season >= 2) target -= 12;
                if (p.HomeId < 0) target -= 15;
                if (p.Rank == Rank.Serf || p.Rank == Rank.Reeve) target -= Policy.WeekWork * 5 - 5;   // two days is custom; more is a burden
                if (p.Rank == Rank.Freeman) target -= Math.Max(0, Policy.Rent - 0.6f) * 8;
                target -= Math.Max(0, Policy.HarvestTax - 0.3f) * 60 + Math.Max(0, Policy.HearthTax - 0.25f) * 20;   // taxes above custom sting
                var hh = HomeOf(p); if (hh != null && hh.Arrears > 0.5f) target -= 6;
                target += (p.Piety - 40) * 0.2f;
                if (Day - p.AleDay < 7) target += 4;
                // a varied table: meat or fish this week, bought bread; and decent clothes when it's cold
                if (Day - p.MeatDay < 7 || Day - p.FishDay < 7) target += 3;
                if (Day - p.BreadDay < 7) target += 2;
                if (Season >= 2 && Ragged(hh)) target -= 4;
                if (p.Rank == Rank.Freeman) target -= Math.Max(0, Policy.MarketToll - 0.1f) * 40;
                if (hasKeep) target += 3;
                if (p.Rank == Rank.Reeve) target += 8;
                if (Today.Cold > 0 && !p.Warm) target -= 4;
                p.Loyalty = Clamp(p.Loyalty + (Clamp(target, 0, 100) - p.Loyalty) * 0.05f, 0, 100);
            }
        }

        /// <summary>The discontented slip away: a whole household at once, by night. Knights and a cruel lord deter them.</summary>
        void Flight()
        {
            int knights = People.Count(p => p.Rank == Rank.Knight && !p.IsChild);
            foreach (var unit in People.Where(p => !RoyalHousehold(p) && !IsOfficer(p)).GroupBy(HouseholdOf).ToList())
            {
                var grown = unit.Where(p => !p.IsChild).ToList();
                if (grown.Count == 0) continue;
                float loyal = grown.Average(p => p.Loyalty);
                float limit = Balance.FleeBelow + (unit.Any(p => p.Rank == Rank.Freeman) ? 5 : 0);
                if (loyal >= limit) continue;
                double chance = 0.03 * (limit - loyal) / limit;
                if (knights > 0) chance *= 0.6;
                if (RulerIs(Trait.Cruel)) chance *= 0.5;
                if (RulerIs(Trait.Brave)) chance *= 0.7;
                if (Chance(chance)) { Flee(unit.Key, unit.ToList()); return; }
            }
        }

        /// <summary>Saturday is market day: a merchant comes through and buys up spare grain; freemen buy what they need from the granary.</summary>
        void MarketDay()
        {
            float price = Balance.MerchantGrain;
            // the merchant buys households' surplus
            foreach (var h in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0))
            {
                float spare = h.Stock[(int)Good.Grain] - GrainKeep(h);
                if (spare < 10) continue;
                float q = Math.Min(spare * 0.5f, h.Stock[(int)Good.Grain]);
                h.Stock[(int)Good.Grain] -= q;
                var head = HeadOf(h); if (head != null) head.Coin += q * price * 0.9f;
            }
            // the miller sells the flour his family can't eat
            foreach (var mill in OfType(BuildingType.Mill).Where(m => m.Employees.Count > 0))
            {
                var miller = GetCitizen(mill.Employees[0]); var home = HomeOf(miller); if (home == null) continue;
                float spare = home.Stock[(int)Good.Flour] - Needs(home) * 20;
                if (spare < 5) continue;
                home.Stock[(int)Good.Flour] -= spare; miller.Coin += spare * price * 1.2f;
            }
            // and the lord's grain above the reserve
            if (Policy.SellSurplus)
            {
                float spare = StoreStock(Good.Grain) - RoyalKeep;
                if (spare > 5)
                {
                    float q = TakeFromStores(Good.Grain, spare);
                    float pay = q * price * (RulerIs(Trait.Greedy) ? 1.1f : 1f);
                    Coin += pay; WeekIn += pay; Book("Grain sold to the merchant", pay);
                    AddLog("Market day: the merchant bought " + Math.Round(q) + " measures of the king's grain for " + Money(pay) + ".", LogKind.Info);
                }
            }
            // the lord buys timber and stone the building sites are waiting on
            if (Policy.BuyMaterials)
                foreach (var g in new[] { Good.Timber, Good.Stone })
                {
                    float need = Sites.Sum(x => (g == Good.Timber ? x.Def.Timber : x.Def.Stone) - x.Delivered[(int)g]) - StoreStock(g);
                    float unit = g == Good.Timber ? Balance.BuyTimber : Balance.BuyStone;
                    float q = Math.Min(Math.Min(need, 60f), (Coin - Policy.CoinReserve) / unit);
                    if (q < 2) continue;
                    q = (float)Math.Floor(q);
                    Coin -= q * unit; WeekOut += q * unit; PutInStores(g, q); Book("Bought " + Defs.GoodNames[(int)g].ToLower(), -q * unit);
                    AddLog("Market day: the crown bought " + q + " " + Defs.GoodNames[(int)g].ToLower() + " from the merchant for " + Money(q * unit) + ".", LogKind.Info);
                }
            // freemen buy firewood from the lord's stores when their woodpile's low
            foreach (var h in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && HeadOf(b) != null && HeadOf(b).Rank == Rank.Freeman))
            {
                float want = 30 - h.Stock[(int)Good.Firewood];
                var payer = h.Residents.Select(GetCitizen).Where(p => p != null).OrderByDescending(p => p.Coin).FirstOrDefault();
                if (want < 5 || payer == null) continue;
                var fv = VillageOf(h);
                float q = Math.Min(want, Math.Min(payer.Coin / Balance.FirewoodPrice, Math.Max(0, LocalStock(fv, Good.Firewood) - 40)));
                if (q < 1) continue;
                q = TakeLocal(fv, Good.Firewood, q);
                payer.Coin -= q * Balance.FirewoodPrice; PayLord(DomainOf(fv), q * Balance.FirewoodPrice, "Firewood sold to freemen");
                h.Stock[(int)Good.Firewood] += q;
            }
            // the crown buys grain when the granary won't feed the court until harvest: from its own villagers first, then the merchant
            {
                // up to the harvest from what families can spare; on up to the court's year only from big surpluses
                float want = CourtYear - StoreStock(Good.Grain), urgent = CourtHold - StoreStock(Good.Grain);
                foreach (var h in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && DomainOf(b) < 0).OrderByDescending(b => b.Stock[(int)Good.Grain] - GrainKeep(b)).ToList())
                {
                    if (want < 1 || Coin - Policy.CoinReserve * 0.5f < Balance.VillageGrain) break;
                    float spare = FamilySpare(h);
                    if (urgent > 0) spare = Math.Max(spare, Math.Min(urgent, h.Stock[(int)Good.Grain] - GrainKeep(h)));
                    if (spare < 3) continue;
                    float bq = (float)Math.Floor(Math.Min(Math.Min(spare, want), (Coin - Policy.CoinReserve * 0.5f) / Balance.VillageGrain));
                    urgent -= bq;
                    if (bq < 1) continue;
                    h.Stock[(int)Good.Grain] -= bq; PutInStores(Good.Grain, bq); want -= bq;
                    float pay = bq * Balance.VillageGrain; Coin -= pay; WeekOut += pay; Book("Bought grain from villagers", -pay);
                    var seller = HeadOf(h); if (seller != null) seller.Coin += pay;
                }
                want = Math.Min(want, CrownFloor - StoreStock(Good.Grain));   // from outside only in real need
                float q = (float)Math.Floor(Math.Min(Math.Min(want, 120f), (Coin - Policy.CoinReserve * 0.5f) / Balance.BuyGrain));
                if (q >= 5)
                {
                    Coin -= q * Balance.BuyGrain; WeekOut += q * Balance.BuyGrain; PutInStores(Good.Grain, q); Book("Bought grain", -q * Balance.BuyGrain);
                    AddLog("Market day: the crown bought " + q + " measures of grain from the merchant for " + Money(q * Balance.BuyGrain) + ".", LogKind.Info);
                }
            }
            // freemen (who have no strips, wherever they live) buy grain for the coming weeks: from serf families with grain to spare,
            // then from the king's granary
            var sellers = Buildings.Where(b => b.Built && b.Type == BuildingType.Cottage && b.Residents.Count > 0).ToList();   // (in the buyer's own domain)
            foreach (var h in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0).ToList())
            {
                var head = HeadOf(h); if (head == null || (head.Rank != Rank.Freeman && !head.WasFree)) continue;
                float want = Needs(h) * 21f - h.Stock[(int)Good.Grain] - h.Stock[(int)Good.Flour] - h.Stock[(int)Good.Bread] * 1.4f;
                if (want < 5) continue;
                var buyers = Purse(h);
                int dom = DomainOf(h);
                foreach (var s in sellers.Where(x => DomainOf(x) == dom).OrderBy(x => x.VillageId == h.VillageId ? 0 : 1).ThenByDescending(x => x.Stock[(int)Good.Grain] - GrainKeep(x)))   // whoever has most to spare
                {
                    if (want < 1) break;
                    float spare = s.Stock[(int)Good.Grain] - GrainKeep(s);
                    if (spare < 3) continue;
                    float q2 = Math.Min(Math.Min(spare, want), CoinOf(buyers) / Balance.VillageGrain);
                    if (q2 < 1) break;
                    float v = q2 * Balance.VillageGrain;
                    s.Stock[(int)Good.Grain] -= q2; h.Stock[(int)Good.Grain] += q2; want -= q2;
                    Pay(buyers, v); var sh = HeadOf(s); if (sh != null) sh.Coin += v;
                }
                if (want < 5) continue;
                float purse = h.Residents.Select(GetCitizen).Where(p => p != null).Sum(p => p.Coin);
                var hv = VillageOf(h);
                float local = LocalStock(hv, Good.Grain);
                float q = Math.Min(want, Math.Min(purse / Math.Max(0.01f, Policy.GrainPrice), local));
                if (local < want) Want(h, Good.Grain, want - local);   // the store's short, not just their purse
                if (q <= 0) { if (purse > 0.3f) Buy(h, Purse(h), BuildingType.MerchantHouse, Good.Grain, want, purse); continue; }
                q = TakeLocal(hv, Good.Grain, q);
                float pay = q * Policy.GrainPrice;
                foreach (var p in h.Residents.Select(GetCitizen).Where(p => p != null).OrderByDescending(p => p.Coin)) { float t = Math.Min(p.Coin, pay); p.Coin -= t; pay -= t; if (pay <= 0) break; }
                PayLord(dom, q * Policy.GrainPrice, "Grain sold to freemen");
                h.Stock[(int)Good.Grain] += q;
            }
        }

        /// <summary>Sunday night: the week's hearth tax and rent are assessed, newcomers arrive, and the week's books close.</summary>
        void Weekly()
        {
            WeeklyAssessment();
            ShopsWeekly();
            Newcomers();
            LastIn = WeekIn; LastOut = WeekOut; WeekIn = WeekOut = 0;
            LastTaxCoin = WeekTaxCoin; LastTaxGrain = WeekTaxGrain; WeekTaxCoin = WeekTaxGrain = 0;
            History.Add(new HistoryPoint { Day = Day, Population = People.Count, Coin = Coin, Grain = StoreStock(Good.Grain) + StoreStock(Good.Flour), Loyalty = AverageLoyalty });
            if (History.Count > 200) History.RemoveAt(0);
        }

        /// <summary>Once a week: saplings grow up, stumps sprout again, and the woods creep back over time.</summary>
        void Trees()
        {
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    byte t = Terrain[y * Cols + x];
                    if (t == Tile.Sapling && Chance(1 / 4.0) && OccupantAt(x, y) == null) SetTerrain(x, y, Tile.Tree);
                    else if (t == Tile.Stump && Chance(1 / 4.0) && OccupantAt(x, y) == null) SetTerrain(x, y, Tile.Sapling);
                }
        }
    }
}
