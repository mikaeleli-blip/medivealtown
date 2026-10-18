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
            if (s.Delivered[(int)Good.Timber] < d.Timber && StoreStock(Good.Timber) > 0.5f) return true;
            if (s.Delivered[(int)Good.Stone] < d.Stone && StoreStock(Good.Stone) > 0.5f) return true;
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
            var sites = Sites.Where(SiteCanProgress).OrderBy(s => s.Type == BuildingType.Road ? 9 : s.Def.IsField ? 0 : s.Type == BuildingType.Granary ? 1 : s.Type == BuildingType.Mill ? 2 : s.Type == BuildingType.Cottage ? 3 : 4).ThenBy(s => s.Id).ToList();
            var crew = sites.ToDictionary(s => s.Id, s => 0);
            var demesnes = OfType(BuildingType.Demesne).Where(FieldNeedsWork).ToList();
            // sites waiting on timber or stone the stores don't have: week-work goes to the quarry and the woods
            bool needStone = Sites.Any(x => x.Delivered[(int)Good.Stone] < x.Def.Stone) && StoreStock(Good.Stone) < 1;
            bool needTimber = Sites.Any(x => x.Delivered[(int)Good.Timber] < x.Def.Timber) && StoreStock(Good.Timber) < 1;
            var quarry = needStone ? OfType(BuildingType.Quarry).FirstOrDefault(q => QuarryOnHill(q) || RocksNear(q) > 0) : null;
            var woods = needTimber ? OfType(BuildingType.Woodcutter).FirstOrDefault(w => TreesNear(w) > 0) : null;
            int mc = 0;
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
                    case Rank.Bailiff:
                        // Saturday he carries the chest to the castle; other days he keeps the books
                        var chest = TaxChest;
                        if (IsMarketDay && chest != null && (chest.Coin > 0.01f || chest.Stock[(int)Good.Grain] > 0.5f)) { p.Chore = Chore.Deliver; p.TaskId = chest.Id; }
                        else { p.Chore = Chore.Accounts; p.TaskId = chest != null ? chest.Id : -1; }
                        continue;
                }
                if (IsSunday) { p.Chore = Chore.Rest; continue; }
                if (p.Age >= 64) { p.Chore = Chore.Rest; continue; }

                if (p.Rank == Rank.Freeman)
                {
                    var w = GetBuilding(p.WorkId);
                    var hearth = HomeOf(p);
                    bool cold = Season >= 2 && hearth != null && hearth.Stock[(int)Good.Firewood] < 12;
                    if (w != null && w.Built && !cold && TradeNeeded(w)) { p.Chore = Chore.Trade; p.TaskId = w.Id; continue; }
                    // freemen without a trade hire on to the crown's building work for a wage, if he can pay
                    if (Coin >= Policy.FreeWage) { var s = nextSite(); if (s != null) { p.Chore = Chore.Build; p.TaskId = s.Id; continue; } }
                    var home = HomeOf(p);
                    p.Chore = home != null && home.Stock[(int)Good.Firewood] < 40 ? Chore.Gather : Chore.Rest;
                    continue;
                }

                // the reeve's collecting day
                if (p.Id == ReeveId && WeekdayIndex == 4) { p.Chore = Chore.Collect; p.TaskId = TaxChest != null && BailiffId >= 0 ? TaxChest.Id : seat != null ? seat.Id : -1; continue; }
                // serfs and the reeve
                if (IsWeekWorkDay || p.Id == ReeveId)
                {
                    var s = nextSite();
                    if (s != null) { p.Chore = Chore.Build; p.TaskId = s.Id; continue; }
                    var mat = quarry != null && woods != null ? (mc++ % 2 == 0 ? quarry : woods) : quarry ?? woods;
                    // at harvest the king's field comes first; while the crop grows, hands are split with the carting
                    bool harvest = Season == 2;
                    if (demesnes.Count > 0 && (mat == null || harvest || (p.Id % 2 == 0))) { p.Chore = Chore.WeekWork; p.TaskId = demesnes[dc++ % demesnes.Count].Id; continue; }
                    if (mat != null) { p.Chore = Chore.WeekWork; p.TaskId = mat.Id; continue; }
                    if (IsWeekWorkDay) { p.Chore = Chore.WeekWork; p.TaskId = -1; continue; }   // gathering wood for the king
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
            float reeve = Reeve != null && Reeve.Health > 30 ? Balance.ReeveBonus : 1f;

            // ---- the day's work
            foreach (var p in People)
            {
                float hands = p.IsChild ? Balance.ChildFieldWork : p.Age > 58 ? 0.7f : 1f;
                if (p.Health < 50) hands *= 0.6f;
                var t = GetBuilding(p.TaskId);
                switch (p.Chore)
                {
                    case Chore.OwnStrip: if (t != null) FieldWork(t, hands, 1f, HomeOf(GetCitizen(t.HolderId) ?? p)); break;
                    case Chore.WeekWork:
                        if (t != null && t.Def.IsField) FieldWork(t, hands, reeve, null);
                        else if (t != null) Materials(t, hands);
                        else PutInStores(Good.Firewood, Balance.GatherWood * hands);
                        break;
                    case Chore.Build: if (t != null && !t.Built) BuildWork(t, p); break;
                    case Chore.Gather: { var h = HomeOf(p); if (h != null) h.Stock[(int)Good.Firewood] += Balance.GatherWood * hands * (si == 3 ? 0.8f : 1f); } break;
                    case Chore.Trade: if (t != null) TradeWork(t, p); break;
                }
            }
            foreach (var s in Sites.ToList()) if (s.Progress >= s.Def.Work - 0.001f && s.MaterialsFraction >= 0.999f) OnBuilt(s, false);

            Milling();
            Brewing();
            Eating(si);
            Warmth(si);
            Alehouse();
            Mass();
            Moods();

            // ---- the calendar
            int doy = DayOfYear;
            if (doy == 0) SpringStart();
            if (doy == Balance.SeasonStart(3) - 1) { Michaelmas(); CropsRot(); }
            if (doy == Balance.SeasonStart(3)) Martinmas();
            if (doy == Balance.SeasonStart(3) + 6) Christmas();
            if (doy == Balance.SeasonStart(2) - 1) AutumnComes();
            if (WeekdayIndex == 4) ReeveCollects();
            if (IsMarketDay) { BailiffDelivers(); MarketDay(); }
            if (IsSunday) { Weekly(); Trees(); }
            BailiffArrives();
            CheckRevolt();

            Lives();
            ComeOfAge();
            Matchmaking();
            SendPriest();
            Flight();
            Rehouse(); AssignStrips(); Hire();
        }

        void SpringStart()
        {
            foreach (var f in Buildings.Where(b => b.Def.IsField)) { f.CropGrowth = 0; f.CropLeft = 0; f.Harvested = 0; }
            ChooseReeve(false);
            if (Year > Balance.StartYear) AddLog("A new year, " + Year + ". The fields are ploughed and sown again.", LogKind.Info);
        }

        /// <summary>The last day of summer: what has grown is ripe and stands waiting for the sickle.</summary>
        void AutumnComes()
        {
            foreach (var f in Buildings.Where(b => b.Def.IsField && b.Built))
            {
                f.CropLeft = f.CropGrowth * (f.Type == BuildingType.Demesne ? Balance.DemesneYield : Balance.StripYield);
                f.Harvested = 0;
            }
        }

        void CropsRot()
        {
            float lost = Buildings.Where(b => b.Def.IsField).Sum(b => b.CropLeft);
            foreach (var f in Buildings.Where(b => b.Def.IsField)) f.CropLeft = 0;
            if (lost > 20) AddLog(Math.Round(lost) + " measures of grain were never brought in and rotted in the fields.", LogKind.Bad);
        }

        /// <summary>Spring and summer: the crop grows with the hours put into it. Autumn: it's reaped. Every harvest gives the church its tithe.</summary>
        void FieldWork(Building f, float hands, float bonus, Building pantry)
        {
            int si = Season;
            bool lords = f.Type == BuildingType.Demesne;
            if (si <= 1)
            {
                f.CropGrowth = Math.Min(1f, f.CropGrowth + (lords ? Balance.DemesneGrowth : Balance.StripGrowth) * hands * bonus);
                return;
            }
            if (si != 2) return;
            float q = Math.Min(f.CropLeft, Balance.HarvestPerWorker * hands * bonus);
            if (q <= 0) return;
            f.CropLeft -= q; f.Harvested += q;
            float tithe = Church != null ? q * Balance.Tithe : 0;
            if (tithe > 0) Church.Stock[(int)Good.Grain] += tithe;
            if (pantry != null)
            {
                pantry.Stock[(int)Good.Grain] += q - tithe;
                Assess(pantry, 0, (q - tithe) * Policy.HarvestTax);   // the crown's share, for the reeve to collect
            }
            else PutInStores(Good.Grain, q - tithe);
            Today.Harvest += q; Today.Tithe += tithe;
        }

        void BuildWork(Building s, Citizen p)
        {
            var d = s.Def;
            if (p.Rank == Rank.Freeman)
            {
                float w = Math.Min(Policy.FreeWage, Math.Max(0, Coin));
                Coin -= w; WeekOut += w; p.Coin += w;
            }
            // haul first, then build on what's here
            foreach (var g in new[] { Good.Timber, Good.Stone })
            {
                float need = (g == Good.Timber ? d.Timber : d.Stone) - s.Delivered[(int)g];
                if (need <= 0.01f) continue;
                if (s.Progress < s.MaterialsFraction * d.Work - 0.5f) break;   // work on what's here first
                float got = TakeFromStores(g, Math.Min(need, Balance.HaulPerDay));
                if (got > 0) { s.Delivered[(int)g] += got; return; }
            }
            float cap = s.MaterialsFraction * d.Work;
            if (d.Timber + d.Stone == 0) cap = d.Work;
            s.Progress = Math.Min(cap, s.Progress + 1f);
        }

        void TradeWork(Building b, Citizen p)
        {
            if (b.Type != BuildingType.Alehouse)
            {
                float w = Math.Min(Policy.FreeWage, Math.Max(0, Coin));
                Coin -= w; WeekOut += w; p.Coin += w;
            }
            switch (b.Type)
            {
                case BuildingType.Woodcutter:
                {
                    // split timber for the fires when firewood runs low, otherwise fell
                    if (!(StoreStock(Good.Firewood) < 80 && StoreStock(Good.Timber) >= 4))
                    {
                        if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Tree) b.TargetTile = NearestTile(b, Tile.Tree);
                        if (b.TargetTile < 0) break;
                        b.ChopWork += Balance.TreeWork;
                        if (b.ChopWork >= 1f)
                        {
                            b.ChopWork = 0;
                            SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Stump);
                            PutInStores(Good.Timber, Balance.TimberPerTree);
                            b.TargetTile = -1;
                        }
                    }
                    else
                    {
                        float t = TakeFromStores(Good.Timber, Balance.SplitPerWorker);
                        PutInStores(Good.Firewood, t * Balance.FirewoodPerTimber);
                    }
                    break;
                }
                case BuildingType.Quarry:
                {
                    if (QuarryOnHill(b)) { PutInStores(Good.Stone, Balance.StonePerWorker); break; }
                    if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Rock) b.TargetTile = NearestTile(b, Tile.Rock);
                    if (b.TargetTile < 0) break;
                    PutInStores(Good.Stone, Balance.StonePerWorker);
                    b.ChopWork += Balance.StonePerWorker / Balance.StonePerRock;
                    if (b.ChopWork >= 1f) { b.ChopWork = 0; SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Grass); b.TargetTile = -1; }
                    break;
                }
                case BuildingType.Alehouse:
                    b.ChopWork += 1;   // a brewing day; the batch is made in Brewing()
                    break;
            }
        }

        /// <summary>Serfs on week-work at the quarry or in the woods: unpaid, and not as handy as the freemen who do it every day.</summary>
        void Materials(Building b, float hands)
        {
            if (b.Type == BuildingType.Quarry)
            {
                if (QuarryOnHill(b)) { PutInStores(Good.Stone, Balance.StonePerWorker * 0.8f * hands); return; }
                if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Rock) b.TargetTile = NearestTile(b, Tile.Rock);
                if (b.TargetTile < 0) return;
                PutInStores(Good.Stone, Balance.StonePerWorker * 0.8f * hands);
                b.ChopWork += Balance.StonePerWorker * 0.8f * hands / Balance.StonePerRock;
                if (b.ChopWork >= 1f) { b.ChopWork = 0; SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Grass); b.TargetTile = -1; }
                return;
            }
            if (b.Type == BuildingType.Woodcutter)
            {
                if (b.TargetTile < 0 || TerrainAt(b.TargetTile % Cols, b.TargetTile / Cols) != Tile.Tree) b.TargetTile = NearestTile(b, Tile.Tree);
                if (b.TargetTile < 0) return;
                b.ChopWork += Balance.TreeWork * 0.8f * hands;
                if (b.ChopWork >= 1f) { b.ChopWork = 0; SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Stump); PutInStores(Good.Timber, Balance.TimberPerTree); b.TargetTile = -1; }
            }
        }

        /// <summary>Whether a trade has work today: the woods and quarry stand idle when the stores are full and nothing waits on them.</summary>
        public bool TradeNeeded(Building b)
        {
            switch (b.Type)
            {
                case BuildingType.Quarry:
                    return StoreStock(Good.Stone) < 30 || Sites.Any(x => x.Delivered[(int)Good.Stone] < x.Def.Stone) && StoreStock(Good.Stone) < 150;
                case BuildingType.Woodcutter:
                    return StoreStock(Good.Timber) < 60 || StoreStock(Good.Firewood) < 80 || Sites.Any(x => x.Delivered[(int)Good.Timber] < x.Def.Timber) && StoreStock(Good.Timber) < 200;
                case BuildingType.Alehouse:
                    return b.Stock[(int)Good.Ale] < 60;
                default: return true;
            }
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
                PutInStores(Good.Flour, share);
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

        // ---- the alehouse: the alewife brews from grain she buys from the granary
        void Brewing()
        {
            foreach (var b in OfType(BuildingType.Alehouse))
            {
                float days = b.ChopWork; b.ChopWork = 0;
                if (days <= 0 || b.Stock[(int)Good.Ale] > 60) continue;
                var brewer = b.Employees.Select(GetCitizen).FirstOrDefault(p => p != null);
                float grainNeed = days * Balance.BrewPerWorker * 0.5f;
                float have = Math.Min(b.Stock[(int)Good.Grain], grainNeed);
                if (have < grainNeed && brewer != null)
                {
                    float buy = Math.Min(grainNeed - have, Math.Max(0, StoreStock(Good.Grain) - Policy.Reserve * 0.5f));
                    buy = Math.Min(buy, brewer.Coin / Math.Max(0.01f, Policy.GrainPrice));
                    if (buy > 0) { TakeFromStores(Good.Grain, buy); float pay = buy * Policy.GrainPrice; brewer.Coin -= pay; Coin += pay; WeekIn += pay; b.Stock[(int)Good.Grain] += buy; }
                    have = Math.Min(b.Stock[(int)Good.Grain], grainNeed);
                }
                b.Stock[(int)Good.Grain] -= have;
                b.Stock[(int)Good.Ale] += have * 2;
            }
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
        public float RoyalKeep { get { return Math.Max(0, HouseholdKeep) + Policy.Reserve; } }

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

        /// <summary>Takes food for one person: flour first (bread), then grain as pottage. Returns how much of their need was met.</summary>
        static float Eat(float[] stock, float need)
        {
            float got = 0;
            float f = Math.Min(stock[(int)Good.Flour], need / Balance.FlourFeeds);
            stock[(int)Good.Flour] -= f; got += f * Balance.FlourFeeds;
            if (got < need)
            {
                float g = Math.Min(stock[(int)Good.Grain], (need - got) / Balance.GrainFeeds);
                stock[(int)Good.Grain] -= g; got += g * Balance.GrainFeeds;
            }
            return got;
        }

        float EatFromStores(float need)
        {
            float got = 0;
            float f = TakeFromStores(Good.Flour, need); got += f;
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
                if (RoyalHousehold(p) || (p.Rank == Rank.Priest && church == null)) got = EatFromStores(need);
                else if (p.Rank == Rank.Priest && church != null) { got = Eat(church.Stock, need); if (got < need) got += EatFromStores(need - got); }
                else if (p.Rank == Rank.Bailiff) { if (home != null && home.Type == BuildingType.BailiffHall) got = Eat(home.Stock, need); if (got < need) got += EatFromStores(need - got); }
                else
                {
                    // the toft garden gives a little each day outside winter
                    if (home != null && IsPeasantHome(home.Type)) got += Math.Min(need, Balance.GardenBySeason[si] / Math.Max(1, home.Residents.Count) * 1.5f);
                    if (home != null && got < need) got += Eat(home.Stock, need - got);
                    // the hungry: alms from the church's tithe, then the lord's dole
                    if (got < need * 0.7f && church != null && church.Stock[(int)Good.Grain] > 0.5f && Priest != null) got += Eat(church.Stock, need * 0.8f - got);
                    if (got < need * 0.7f && dole && StoreStock(Good.Grain) > Math.Max(10, HouseholdKeep)) got += EatFromStores(need * 0.8f - got);
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
                if (home.Type == BuildingType.Keep || home.Type == BuildingType.Pavilion || home.Type == BuildingType.Church || home.Type == BuildingType.BailiffHall)
                {
                    float need = home.Type == BuildingType.Keep ? Balance.KeepFirewood * Balance.FirewoodBySeason[si] / 0.6f : perHome;
                    warm = TakeFromStores(Good.Firewood, need) >= need * 0.9f || si == 1;
                }
                else
                {
                    float t = Math.Min(home.Stock[(int)Good.Firewood], perHome);
                    home.Stock[(int)Good.Firewood] -= t;
                    warm = t >= perHome * 0.9f || si == 1;
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
            foreach (var p in People.Where(p => !p.IsChild && !RoyalHousehold(p) && p.Rank != Rank.Priest && p.Coin >= Balance.AlePrice))
            {
                if (Day - p.AleDay < 3 || !Chance(0.35)) continue;
                var house = houses.Where(h => h.Stock[(int)Good.Ale] >= 1).OrderBy(h => h.SoldToday).FirstOrDefault();
                if (house == null) break;
                house.Stock[(int)Good.Ale] -= 1; house.SoldToday++;
                p.Coin -= Balance.AlePrice; p.AleDay = Day;
                var brewer = house.Employees.Select(GetCitizen).FirstOrDefault(x => x != null);
                if (brewer != null) brewer.Coin += Balance.AlePrice;
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
            if (RulerIs(Trait.Pious) && Coin > 5) { float g = Math.Min(2, Coin * 0.05f); Coin -= g; WeekOut += g; plate += g; }
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
                if (RoyalHousehold(p) || p.Rank == Rank.Priest || p.Rank == Rank.Bailiff || p.IsChild) { if (p.IsChild) p.Loyalty = 55; continue; }
                float target = 50 + trait;
                target += p.Fed >= 0.95f ? 10 : p.Fed < 0.6f ? -25 : -8;
                if (!p.Warm && Season >= 2) target -= 12;
                if (p.HomeId < 0) target -= 15;
                if (p.Rank == Rank.Serf || p.Rank == Rank.Reeve) target -= Policy.WeekWork * 5 - 5;   // two days is custom; more is a burden
                if (p.Rank == Rank.Freeman) target -= Math.Max(0, Policy.Rent - 1) * 6;
                target -= Math.Max(0, Policy.HarvestTax - 0.15f) * 60 + Math.Max(0, Policy.HearthTax - 0.1f) * 20;   // taxes above custom sting
                var hh = HomeOf(p); if (hh != null && hh.Arrears > 0.5f) target -= 6;
                target += (p.Piety - 40) * 0.2f;
                if (Day - p.AleDay < 7) target += 4;
                if (hasKeep) target += 3;
                if (p.Id == ReeveId) target += 8;
                if (Today.Cold > 0 && !p.Warm) target -= 4;
                p.Loyalty = Clamp(p.Loyalty + (Clamp(target, 0, 100) - p.Loyalty) * 0.05f, 0, 100);
            }
        }

        /// <summary>The discontented slip away: a whole household at once, by night. Knights and a cruel lord deter them.</summary>
        void Flight()
        {
            int knights = People.Count(p => p.Rank == Rank.Knight && !p.IsChild);
            foreach (var unit in People.Where(p => !RoyalHousehold(p) && p.Rank != Rank.Priest && p.Rank != Rank.Bailiff).GroupBy(HouseholdOf).ToList())
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
                float keep = Needs(h) * (DaysUntilHarvest + 10) / Balance.GrainFeeds + 20;
                float spare = h.Stock[(int)Good.Grain] + h.Stock[(int)Good.Flour] - keep;
                if (spare < 10 || Season < 2) continue;
                float q = Math.Min(spare * 0.5f, h.Stock[(int)Good.Grain]);
                h.Stock[(int)Good.Grain] -= q;
                var head = HeadOf(h); if (head != null) head.Coin += q * price * 0.9f;
            }
            // and the lord's grain above the reserve
            if (Policy.SellSurplus)
            {
                float spare = StoreStock(Good.Grain) - RoyalKeep;
                if (spare > 5)
                {
                    float q = TakeFromStores(Good.Grain, spare);
                    float pay = q * price * (RulerIs(Trait.Greedy) ? 1.1f : 1f);
                    Coin += pay; WeekIn += pay;
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
                    Coin -= q * unit; WeekOut += q * unit; PutInStores(g, q);
                    AddLog("Market day: the crown bought " + q + " " + Defs.GoodNames[(int)g].ToLower() + " from the merchant for " + Money(q * unit) + ".", LogKind.Info);
                }
            // freemen buy firewood from the lord's stores when their woodpile's low
            foreach (var h in Buildings.Where(b => b.Built && b.Type == BuildingType.FreeHouse && b.Residents.Count > 0))
            {
                float want = 30 - h.Stock[(int)Good.Firewood];
                var payer = h.Residents.Select(GetCitizen).Where(p => p != null).OrderByDescending(p => p.Coin).FirstOrDefault();
                if (want < 5 || payer == null) continue;
                float q = Math.Min(want, Math.Min(payer.Coin / Balance.FirewoodPrice, Math.Max(0, StoreStock(Good.Firewood) - 40)));
                if (q < 1) continue;
                q = TakeFromStores(Good.Firewood, q);
                payer.Coin -= q * Balance.FirewoodPrice; Coin += q * Balance.FirewoodPrice; WeekIn += q * Balance.FirewoodPrice;
                h.Stock[(int)Good.Firewood] += q;
            }
            // freemen buy grain for the coming weeks
            foreach (var h in Buildings.Where(b => b.Built && b.Type == BuildingType.FreeHouse && b.Residents.Count > 0))
            {
                var head = HeadOf(h); if (head == null) continue;
                float want = Needs(h) * 21f - h.Stock[(int)Good.Grain] - h.Stock[(int)Good.Flour];
                if (want < 5) continue;
                float purse = h.Residents.Select(GetCitizen).Where(p => p != null).Sum(p => p.Coin);
                float q = Math.Min(want, Math.Min(purse / Math.Max(0.01f, Policy.GrainPrice), StoreStock(Good.Grain)));
                if (q <= 0) continue;
                q = TakeFromStores(Good.Grain, q);
                float pay = q * Policy.GrainPrice;
                foreach (var p in h.Residents.Select(GetCitizen).Where(p => p != null).OrderByDescending(p => p.Coin)) { float t = Math.Min(p.Coin, pay); p.Coin -= t; pay -= t; if (pay <= 0) break; }
                Coin += q * Policy.GrainPrice; WeekIn += q * Policy.GrainPrice;
                h.Stock[(int)Good.Grain] += q;
            }
        }

        /// <summary>Sunday night: the week's hearth tax and rent are assessed, newcomers arrive, and the week's books close.</summary>
        void Weekly()
        {
            WeeklyAssessment();
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
