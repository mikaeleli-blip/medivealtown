using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>
    /// The whole village: map, buildings, people, the crown's stores and treasury, the tax chain, the church.
    /// Engine-free so it can be tested headless and saved as plain JSON. The Unity layer reads it and calls Advance / Place / Remove.
    /// </summary>
    [Serializable]
    public partial class Town
    {
        public const int CurrentVersion = 3;
        public int Version = CurrentVersion;
        public int Seed;
        public int Day = 1;
        public float TimeOfDay = 0.3f;           // 0 = midnight, 0.5 = noon
        public float Coin;                       // the royal treasury
        public ManorPolicy Policy = new ManorPolicy();
        public int NextId = 1;
        public string ManorName, Dynasty, ChurchName;

        public byte[] Terrain;
        public List<Building> Buildings = new List<Building>();
        public List<Citizen> People = new List<Citizen>();
        public List<LogEntry> Log = new List<LogEntry>();
        public List<HistoryPoint> History = new List<HistoryPoint>();
        public List<Village> Villages = new List<Village>();
        public List<Barony> Baronies = new List<Barony>();
        public int NextRealmId = 1;

        public int KingId = -1, RegentId = -1, PriestId = -1, StewardId = -1;
        public int Generation = 1;               // monarchs of this house so far
        public TaxBook Taxes = new TaxBook(), LastTaxes = new TaxBook();   // this year's and last year's
        public float WeekTaxCoin, WeekTaxGrain, LastTaxCoin, LastTaxGrain;   // taxes received this week and last
        public Crown Crown = new Crown();
        public int Births, Deaths, Fled, Arrived;

        // this week's books
        public float WeekIn, WeekOut, LastIn, LastOut;

        // ---- runtime only ----
        /// <summary>Where the treasury's coin came from and went, since the game was loaded.</summary>
        [NonSerialized] public Dictionary<string, float> Ledger = new Dictionary<string, float>();
        [NonSerialized] public Dictionary<string, float> TradeTally = new Dictionary<string, float>();
        void Tally(string what, float q) { if (TradeTally == null) TradeTally = new Dictionary<string, float>(); float v; TradeTally.TryGetValue(what, out v); TradeTally[what] = v + q; }
        void Book(string what, float amount) { if (Ledger == null) Ledger = new Dictionary<string, float>(); float v; Ledger.TryGetValue(what, out v); Ledger[what] = v + amount; }
        [NonSerialized] Random rng;
        [NonSerialized] int[] occ;
        [NonSerialized] bool occDirty = true;
        [NonSerialized] public int MapVersion = 1;
        [NonSerialized] public List<int> DirtyTiles = new List<int>();
        /// <summary>Fired after each simulated day.</summary>
        [NonSerialized] public Action<int> DayEnded;

        public const int Cols = Balance.Cols, Rows = Balance.Rows;

        Random Rng { get { if (rng == null) rng = new Random(Seed ^ (Day * 7919)); return rng; } }

        /// <summary>Call after loading from JSON to rebuild runtime caches.</summary>
        public void AfterLoad()
        {
            rng = null; occDirty = true; MapVersion++;
            if (DirtyTiles == null) DirtyTiles = new List<int>();
            if (Policy == null) Policy = new ManorPolicy();
            if (Crown == null) Crown = new Crown();
            if (History == null) History = new List<HistoryPoint>();
            if (Villages == null) Villages = new List<Village>();
            if (Baronies == null) Baronies = new List<Barony>();
            foreach (var b in Buildings)
            {
                b.Stock = Fit(b.Stock); b.Delivered = Fit(b.Delivered);
                if (b.Residents == null) b.Residents = new List<int>();
                if (b.Employees == null) b.Employees = new List<int>();
                // older saves: the freeman family already working a shop holds it, with nothing owed
                if (b.Def.IsShop && b.OwnerId < 0 && b.Wage <= 0 && b.Employees.Count > 0)
                {
                    var o = b.Employees.Select(GetCitizen).FirstOrDefault(p => p != null && p.Rank == Rank.Freeman && !p.IsChild);
                    if (o != null)
                    {
                        b.OwnerId = o.Id; b.Wage = Policy.FreeWage; b.Desired = b.Def.Workers;
                        foreach (var id in b.Employees) { var p = GetCitizen(id); if (p != null && p != o && p.HomeId == o.HomeId) p.FamilyHand = true; }
                    }
                }
            }
        }

        static float[] Fit(float[] a)
        {
            var r = new float[Balance.GoodCount];
            if (a != null) Array.Copy(a, r, Math.Min(a.Length, r.Length));
            return r;
        }

        // =====================================================================
        // Calendar
        // =====================================================================
        public static int SeasonOf(int day)
        {
            int d = (day - 1) % Balance.DaysPerYear;
            for (int s = 0; s < 4; s++) { if (d < Balance.SeasonDays[s]) return s; d -= Balance.SeasonDays[s]; }
            return 3;
        }
        public int Season { get { return SeasonOf(Day); } }
        public int Year { get { return Balance.StartYear + (Day - 1) / Balance.DaysPerYear; } }
        public int DayOfYear { get { return (Day - 1) % Balance.DaysPerYear; } }
        public int DayOfSeason { get { return DayOfYear - Balance.SeasonStart(Season) + 1; } }
        public int DaysInSeason { get { return Balance.SeasonDays[Season]; } }
        public int WeekdayIndex { get { return (Day - 1) % 7; } }   // 0 Monday .. 6 Sunday
        public string Weekday { get { return Defs.Weekdays[WeekdayIndex]; } }
        public bool IsSunday { get { return WeekdayIndex == 6; } }
        public bool IsMarketDay { get { return WeekdayIndex == 5; } }
        public string DateLabel { get { return Defs.Seasons[Season] + " " + DayOfSeason + ", " + Year; } }
        public string DateOf(int day) { int y = Balance.StartYear + (day - 1) / Balance.DaysPerYear, s = SeasonOf(day), doy = (day - 1) % Balance.DaysPerYear; return Defs.Seasons[s] + " " + (doy - Balance.SeasonStart(s) + 1) + ", " + y; }
        public float SeasonProgress { get { return (DayOfSeason - 1 + TimeOfDay) / DaysInSeason; } }
        /// <summary>0 in daylight, rising to 1 at the darkest point of night.</summary>
        public float Nightness
        {
            get
            {
                float t = TimeOfDay;
                if (t < 0.15f) return (0.15f - t) / 0.15f;
                if (t > 0.82f) return Math.Min(1f, (t - 0.82f) / 0.12f);
                return 0f;
            }
        }

        // =====================================================================
        // Small helpers
        // =====================================================================
        float Rnd(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
        bool Chance(double p) { return Rng.NextDouble() < p; }
        T Pick<T>(IList<T> list) { return list[Rng.Next(list.Count)]; }
        List<T> Shuffle<T>(List<T> a)
        {
            for (int i = a.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }
        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        public static string Money(float v) { return Math.Round(v, 1).ToString("0.#") + "c"; }

        bool NearWater(int x, int y, int w, int h, int r)
        {
            for (int yy = y - r; yy < y + h + r; yy++) for (int xx = x - r; xx < x + w + r; xx++) if (TerrainAt(xx, yy) == Tile.Water) return true;
            return false;
        }

        public Building GetBuilding(int id) { if (id < 0) return null; for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].Id == id) return Buildings[i]; return null; }
        public Citizen GetCitizen(int id) { if (id < 0) return null; for (int i = 0; i < People.Count; i++) if (People[i].Id == id) return People[i]; return null; }
        public IEnumerable<Building> OfType(BuildingType t) { return Buildings.Where(b => b.Type == t && b.Built); }
        public IEnumerable<Building> Sites { get { return Buildings.Where(b => !b.Built); } }
        public IEnumerable<Citizen> Adults { get { return People.Where(p => !p.IsChild); } }
        public Building Keep { get { return OfType(BuildingType.Keep).FirstOrDefault(); } }
        public Building Church { get { return OfType(BuildingType.Church).FirstOrDefault(); } }
        public Building Pavilion { get { return OfType(BuildingType.Pavilion).FirstOrDefault(); } }
        public Building Seat { get { return Keep ?? Pavilion; } }    // where the king holds court
        public Citizen King { get { return GetCitizen(KingId); } }
        public Citizen Regent { get { return GetCitizen(RegentId); } }
        public Citizen Priest { get { return GetCitizen(PriestId); } }
        /// <summary>Whoever actually rules: the king, or the regent while he's a child.</summary>
        public Citizen Ruler { get { var l = King; if (l != null && l.IsChild && Regent != null) return Regent; return l; } }
        public bool RulerIs(Trait t) { var r = Ruler; return r != null && r.Trait == t; }
        public bool MassToday { get { return IsSunday && Church != null && Priest != null; } }

        public static bool IsHome(BuildingType t) { return Defs.Get(t).IsHome; }
        public static bool IsPeasantHome(BuildingType t) { return t == BuildingType.Cottage || t == BuildingType.FreeHouse; }
        public static bool RoyalHousehold(Citizen p) { return p.Rank == Rank.King || p.Rank == Rank.Noble || p.Rank == Rank.Knight; }

        /// <summary>The average loyalty of grown folk outside the royal household.</summary>
        public float AverageLoyalty
        {
            get
            {
                var l = People.Where(p => !p.IsChild && !RoyalHousehold(p) && !IsOfficer(p)).ToList();
                return l.Count == 0 ? 50f : l.Average(p => p.Loyalty);
            }
        }
        public float AveragePiety { get { var l = Adults.ToList(); return l.Count == 0 ? 0f : l.Average(p => p.Piety); } }

        // =====================================================================
        // Stores: the granary (or the baggage cart until one stands)
        // =====================================================================
        public IEnumerable<Building> Stores
        {
            get
            {
                var g = Buildings.Where(b => b.Built && b.Type == BuildingType.Granary).ToList();
                var c = Buildings.Where(b => b.Type == BuildingType.Cart);
                return g.Concat(c);
            }
        }
        public float StoreStock(Good g) { float s = 0; foreach (var b in Stores) s += b.Stock[(int)g]; return s; }
        public float TakeFromStores(Good g, float q)
        {
            float got = 0;
            foreach (var b in Stores.OrderBy(b => b.Type == BuildingType.Cart ? 0 : 1))   // the cart empties first
            {
                if (got >= q) break;
                float t = Math.Min(b.Stock[(int)g], q - got);
                b.Stock[(int)g] -= t; got += t;
            }
            return got;
        }
        public void PutInStores(Good g, float q)
        {
            if (q <= 0) return;
            var dest = Buildings.FirstOrDefault(b => b.Built && b.Type == BuildingType.Granary) ?? Buildings.FirstOrDefault(b => b.Type == BuildingType.Cart);
            if (dest == null)
            {
                // no granary and no cart: the seat keeps it
                dest = Seat; if (dest == null) return;
            }
            dest.Stock[(int)g] += q;
        }

        // =====================================================================
        // Map
        // =====================================================================
        public byte TerrainAt(int x, int y) { return x < 0 || y < 0 || x >= Cols || y >= Rows ? Tile.OutOfBounds : Terrain[y * Cols + x]; }
        void SetTerrain(int x, int y, byte v) { if (TerrainAt(x, y) == Tile.OutOfBounds) return; Terrain[y * Cols + x] = v; MarkDirty(x, y); }
        void MarkDirty(int x, int y) { DirtyTiles.Add(y * Cols + x); MapVersion++; }
        void MarkDirty(Building b) { var d = b.Def; for (int dx = 0; dx < d.W; dx++) for (int dy = 0; dy < d.H; dy++) MarkDirty(b.X + dx, b.Y + dy); occDirty = true; }
        public static bool IsTreeish(byte t) { return t == Tile.Tree || t == Tile.Sapling; }

        void RebuildOcc()
        {
            if (occ == null) occ = new int[Cols * Rows];
            for (int i = 0; i < occ.Length; i++) occ[i] = -1;
            for (int i = 0; i < Buildings.Count; i++)
            {
                var b = Buildings[i]; var d = b.Def;
                for (int dx = 0; dx < d.W; dx++)
                    for (int dy = 0; dy < d.H; dy++)
                    {
                        int x = b.X + dx, y = b.Y + dy;
                        if (x >= 0 && y >= 0 && x < Cols && y < Rows) occ[y * Cols + x] = i;
                    }
            }
            occDirty = false;
        }

        public Building OccupantAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Cols || y >= Rows) return null;
            if (occDirty || occ == null) RebuildOcc();
            int i = occ[y * Cols + x];
            return i >= 0 && i < Buildings.Count ? Buildings[i] : null;
        }
        public bool IsRoad(int x, int y) { var b = OccupantAt(x, y); return b != null && b.Type == BuildingType.Road; }

        public const float TimberPerClearedTree = 2f, StonePerClearedRock = 3f;

        /// <summary>Clears a tree, sapling, stump or boulder. Felled trees and broken boulders go to the stores.</summary>
        public string ClearTile(int x, int y)
        {
            byte t = TerrainAt(x, y);
            if (t != Tile.Tree && t != Tile.Rock && t != Tile.Sapling && t != Tile.Stump) return null;
            if (OccupantAt(x, y) != null) return null;
            if (t == Tile.Tree) PutInStores(Good.Timber, TimberPerClearedTree);
            if (t == Tile.Rock) PutInStores(Good.Stone, StonePerClearedRock);
            SetTerrain(x, y, Tile.Grass);
            return null;
        }

        int CountInRadius(int cx, int cy, int r, Func<byte, bool> pred)
        {
            int n = 0;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    if (pred(TerrainAt(x, y))) n++;
                }
            return n;
        }
        public int TreesNear(Building b) { var d = b.Def; return CountInRadius(b.X + d.W / 2, b.Y + d.H / 2, d.Radius, t => t == Tile.Tree); }
        public int RocksNear(Building b) { var d = b.Def; return CountInRadius(b.X + d.W / 2, b.Y + d.H / 2, d.Radius, t => t == Tile.Rock); }

        bool TouchesHill(int x, int y, int w, int h)
        {
            for (int dx = -1; dx <= w; dx++)
                for (int dy = -1; dy <= h; dy++)
                {
                    bool edge = dx == -1 || dy == -1 || dx == w || dy == h;
                    if (edge && TerrainAt(x + dx, y + dy) == Tile.Hill) return true;
                }
            return false;
        }
        public bool QuarryOnHill(Building b) { var d = b.Def; return TouchesHill(b.X, b.Y, d.W, d.H); }
        bool NearStone(int x, int y, int w, int h, int r)
        {
            if (TouchesHill(x, y, w, h)) return true;
            return CountInRadius(x + w / 2, y + h / 2, r, t => t == Tile.Rock) > 0;
        }

        /// <summary>Returns why a building can't go here, or null if it can.</summary>
        public string CanPlace(BuildingType type, int x, int y)
        {
            var d = Defs.Get(type);
            if (x < 0 || y < 0 || x + d.W > Cols || y + d.H > Rows) return "Too close to the edge of the map";
            for (int dx = 0; dx < d.W; dx++)
                for (int dy = 0; dy < d.H; dy++)
                {
                    if (OccupantAt(x + dx, y + dy) != null) return "That ground is already taken";
                    byte t = TerrainAt(x + dx, y + dy);
                    if (t == Tile.Hill) return "Too steep. Nothing can be built on the hills";
                    if (t == Tile.Water && type != BuildingType.Road) return "Only a track can ford the water";
                }
            if (d.Unique && Buildings.Any(b => b.Type == type)) return "There's already a " + d.Name;
            var realm = RealmCheck(type, x, y); if (realm != null) return realm;
            if (d.NeedsStone && !NearStone(x, y, d.W, d.H, d.Radius)) return "A quarry needs a hillside next to it or boulders nearby";
            if (d.NeedsWater && !NearWater(x, y, d.W, d.H, d.Radius)) return "A fishpond has to be next to a river or lake";
            return null;
        }

        /// <summary>Lays out a building site (or a track, which is instant). Returns the new building, or null with an error message.</summary>
        public Building Place(BuildingType type, int x, int y, out string error, bool free = false)
        {
            error = CanPlace(type, x, y);
            if (error != null) return null;
            var d = Defs.Get(type);
            for (int dx = 0; dx < d.W; dx++)
                for (int dy = 0; dy < d.H; dy++)
                {
                    byte t = TerrainAt(x + dx, y + dy);
                    if (!free && t == Tile.Tree) PutInStores(Good.Timber, TimberPerClearedTree);   // the ground is cleared: the timber
                    if (!free && t == Tile.Rock) PutInStores(Good.Stone, StonePerClearedRock);    // and stone are kept
                    if (t == Tile.Tree || t == Tile.Rock || t == Tile.Sapling || t == Tile.Stump) Terrain[(y + dy) * Cols + x + dx] = Tile.Grass;
                }
            var b = new Building { Id = NextId++, Type = type, X = x, Y = y, BuiltDay = Day, Built = free || d.Instant };
            if (b.Built) { b.Progress = d.Work; b.Delivered[(int)Good.Timber] = d.Timber; b.Delivered[(int)Good.Stone] = d.Stone; }
            Buildings.Add(b);
            AssignVillage(b);
            MarkDirty(b);
            if (d.IsWall) RemaskWalls(b);
            if (b.Built) OnBuilt(b, free);
            else AddLog("The reeve staked out a " + d.Name + ". It needs " + MaterialsText(d) + " and " + d.Work + " days of labour.", LogKind.Build);
            return b;
        }

        /// <summary>Walls join their neighbours: each wall tile records which sides have a wall, tower or gatehouse next to it.</summary>
        void RemaskWalls(Building around)
        {
            var d = around.Def;
            for (int y = around.Y - 1; y <= around.Y + d.H; y++)
                for (int x = around.X - 1; x <= around.X + d.W; x++)
                {
                    var w = OccupantAt(x, y);
                    if (w == null || w.Type != BuildingType.Wall) continue;
                    w.Mask = WallAt(x, y - 1, w) * 1 + WallAt(x + 1, y, w) * 2 + WallAt(x, y + 1, w) * 4 + WallAt(x - 1, y, w) * 8;
                    var north = OccupantAt(x, y - 1);
                    if (north != null && north.Def.IsWall && north.Type != BuildingType.Wall) w.Mask |= 16;   // a tower or gatehouse to the north covers the joint
                }
        }
        int WallAt(int x, int y, Building self) { var o = OccupantAt(x, y); return o != null && o != self && o.Def.IsWall ? 1 : 0; }

        public static string MaterialsText(BuildingDef d)
        {
            var parts = new List<string>();
            if (d.Timber > 0) parts.Add(d.Timber + " timber");
            if (d.Stone > 0) parts.Add(d.Stone + " stone");
            return parts.Count == 0 ? "no materials" : string.Join(" and ", parts.ToArray());
        }

        void OnBuilt(Building b, bool quiet)
        {
            b.Built = true; b.BuiltDay = Day; MarkDirty(b);
            if (quiet || b.Type == BuildingType.Road) { Rehouse(); return; }
            AddLog("The " + b.Def.Name + " is finished.", LogKind.Build);
            switch (b.Type)
            {
                case BuildingType.Granary:
                    // the baggage cart is unloaded and broken up for its timber
                    foreach (var cart in Buildings.Where(c => c.Type == BuildingType.Cart).ToList())
                    {
                        for (int g = 0; g < Balance.GoodCount; g++) b.Stock[g] += cart.Stock[g];
                        b.Stock[(int)Good.Timber] += 4;
                        Buildings.Remove(cart); MarkDirty(cart);
                        AddLog("The baggage cart was unloaded into the granary.", LogKind.Info);
                    }
                    break;
                case BuildingType.Keep:
                    var pav = Pavilion;
                    if (pav != null)
                    {
                        for (int g = 0; g < Balance.GoodCount; g++) PutInStores((Good)g, pav.Stock[g]);
                        Buildings.Remove(pav); MarkDirty(pav);
                    }
                    var r = Ruler;
                    AddLog((r != null ? Title(r) : "The king") + " moved the court into the castle. The treasury is kept in the great hall now.", LogKind.Crown);
                    foreach (var p in People) if (!RoyalHousehold(p)) p.Loyalty = Clamp(p.Loyalty + 3, 0, 100);
                    break;
                case BuildingType.VillageCross:
                    FoundVillage(b, false);
                    break;
                case BuildingType.Manor:
                    RaiseBarony(b);
                    break;
                case BuildingType.BoundaryStone:
                    StoneSet(b);
                    break;
                case BuildingType.MerchantHouse:
                    MerchantSetsUp(b);
                    break;
                case BuildingType.Church:
                    AddLog("The bishop consecrated " + ChurchName + ". There will be mass on Sundays.", LogKind.Church);
                    break;
            }
            Rehouse();
        }

        public void Remove(Building b)
        {
            if (b.Type == BuildingType.Pavilion || b.Type == BuildingType.Cart) return;
            var d = b.Def;
            foreach (var p in People)
            {
                if (p.HomeId == b.Id) p.HomeId = -1;
                if (p.WorkId == b.Id) p.WorkId = -1;
                if (p.TaskId == b.Id) { p.TaskId = -1; p.Chore = Chore.Rest; }
            }
            if (b.Type == BuildingType.VillageCross) VillageLost(b);
            if (b.Type == BuildingType.Manor) foreach (var bar in Baronies.Where(x => x.ManorId == b.Id).ToList()) EndBarony(bar, "lost its manor");
            if (b.Def.IsShop) { var o = OwnerOf(b); if (o != null) { o.Coin += Math.Max(0, b.Coin); b.Coin = 0; } foreach (var p in People) if (p.WorkId == b.Id) p.FamilyHand = false; }
            if (b.Coin > 0 || b.ChestGrain > 0) { Receive(b.Coin, b.ChestGrain); b.Coin = 0; b.ChestGrain = 0; }
            Buildings.Remove(b);
            MarkDirty(b);
            if (d.IsWall) RemaskWalls(b);
            // half the materials come back, and whatever was kept here
            PutInStores(Good.Timber, b.Delivered[(int)Good.Timber] * 0.5f);
            PutInStores(Good.Stone, b.Delivered[(int)Good.Stone] * 0.5f);
            for (int g = 0; g < Balance.GoodCount; g++) if (b.Stock[g] > 0) PutInStores((Good)g, b.Stock[g]);
            if (b.Type != BuildingType.Road) AddLog("The " + d.Name + (b.Built ? " was pulled down." : " site was cleared."), LogKind.Bad);
            Rehouse();
        }

        public void AddLog(string msg, LogKind kind = LogKind.Info)
        {
            Log.Add(new LogEntry { Day = Day, Message = msg, Kind = kind });
            if (Log.Count > 400) Log.RemoveRange(0, Log.Count - 400);
        }

        // =====================================================================
        // Time
        // =====================================================================
        /// <summary>Advance the clock by a fraction of a day; simulates each day that completes.</summary>
        public void Advance(float days)
        {
            if (Crown.Stripped) return;
            TimeOfDay += days;
            while (TimeOfDay >= 1f)
            {
                TimeOfDay -= 1f;
                SimulateDay();
                Day++;
                rng = null;
                PlanDay();
                if (DayEnded != null) DayEnded(Day);
                if (Crown.Stripped) { TimeOfDay = 0.5f; break; }
            }
        }

        // =====================================================================
        // A new manor
        // =====================================================================
        public const int WagonX = Cols / 2 + 5, WagonY = Rows / 2 - 1;   // the village centre (the terrain is built round it)
        public const int TrailRow = WagonY + 2;                          // the old track through the village
        public const int VillageX = WagonX + 1 - 13;                     // the first cottage

        /// <summary>The king grants a new lord a manor: an old village of serfs, with no keep, church or mill yet.</summary>
        public static Town CreateNew(int seed)
        {
            var t = new Town { Seed = seed, Coin = Balance.StartCoin };
            t.Terrain = TerrainGen.Generate(seed, Cols, Rows);
            t.ManorName = t.Pick(new[] { "Ashby", "Thornbury", "Oakhurst", "Wexley", "Hollowmere", "Elmstead", "Brackenford", "Coldharbour", "Wyndham", "Fernley" });
            t.Dynasty = t.Pick(Defs.NobleHouses);
            t.ChurchName = t.Pick(Defs.Saints) + "'s";

            // clear the old village ground
            for (int y = TrailRow - 9; y <= TrailRow + 11; y++)
                for (int x = VillageX - 3; x <= VillageX + 26; x++)
                    if (t.TerrainAt(x, y) != Tile.OutOfBounds) t.Terrain[y * Cols + x] = Tile.Grass;
            // the old track through it
            string err;
            for (int x = 0; x < Cols; x++)
            {
                if (t.TerrainAt(x, TrailRow) == Tile.Hill) t.Terrain[TrailRow * Cols + x] = Tile.Grass;
                if (t.TerrainAt(x, TrailRow) == Tile.Tree || t.TerrainAt(x, TrailRow) == Tile.Rock) t.Terrain[TrailRow * Cols + x] = Tile.Grass;
                t.Place(BuildingType.Road, x, TrailRow, out err, true);
            }

            // the village cross on the green: the capital
            for (int dx = 0; dx < 2; dx++) for (int dy = 1; dy < 3; dy++) t.Terrain[(TrailRow + dy) * Cols + VillageX + 17 + dx] = Tile.Grass;
            var cross = t.Place(BuildingType.VillageCross, VillageX + 17, TrailRow + 1, out err, true);
            t.FoundVillage(cross, true, t.ManorName);

            // the royal household arrives with a pavilion and a cart
            var pav = t.Place(BuildingType.Pavilion, VillageX + 8, TrailRow - 8, out err, true);
            var cart = t.Place(BuildingType.Cart, VillageX + 11, TrailRow - 8, out err, true);
            for (int g = 0; g < Balance.GoodCount; g++) cart.Stock[g] = Balance.StartStock[g];
            for (int y = TrailRow - 6; y < TrailRow; y++) t.Place(BuildingType.Road, VillageX + 11, y, out err, true);

            var lord = t.AddPerson(false, t.Dynasty, t.Rnd(26, 36), Rank.King);
            lord.First = t.Pick(Defs.Kings);
            var lady = t.AddPerson(true, t.Dynasty, t.Rnd(20, 30), Rank.Noble);
            Marry(lord, lady);
            lord.Trait = (Trait)t.Rng.Next(6); lady.Trait = (Trait)t.Rng.Next(6);
            t.KingId = lord.Id;
            int kids = t.Rng.Next(1, 3);
            for (int k = 0; k < kids; k++) t.Child(lady, lord, t.Rnd(1, 8));
            var knight = t.AddPerson(false, t.Pick(Defs.KnightNames), t.Rnd(24, 40), Rank.Knight);
            knight.Loyalty = 75;
            var priest = t.AddPerson(false, t.Pick(Defs.PeasantNames), t.Rnd(30, 50), Rank.Priest);
            priest.Piety = 90; t.PriestId = priest.Id;
            var steward = t.AddPerson(false, t.Pick(Defs.KnightNames), t.Rnd(30, 48), Rank.Steward);
            steward.Honesty = (float)Math.Round(t.Rnd(0.3f, 1f), 2); steward.Loyalty = 70; steward.Coin = 5; t.StewardId = steward.Id;
            foreach (var p in new[] { lord, lady, knight, priest, steward }) p.HomeId = pav.Id;

            // the village: six serf families with their cottages and strips, and two freemen
            for (int i = 0; i < 6; i++)
            {
                var home = t.Place(BuildingType.Cottage, VillageX + i * 3, TrailRow - 3, out err, true);
                var strip = t.Place(BuildingType.Strip, VillageX - 1 + (i % 3) * 6, TrailRow + 2 + (i / 3) * 5, out err, true);
                string fam = t.Pick(Defs.PeasantNames);
                var dad = t.AddPerson(false, fam, t.Rnd(22, 44), Rank.Serf);
                var mom = t.AddPerson(true, fam, t.Rnd(19, 38), Rank.Serf);
                Marry(dad, mom);
                int n = t.Rng.Next(0, 4);
                for (int k = 0; k < n; k++) t.Child(mom, dad, t.Rnd(0.5f, 13));
                if (i == 0) { var gran = t.AddPerson(true, fam, t.Rnd(52, 64), Rank.Serf); gran.MotherId = -1; dad.MotherId = gran.Id; }   // his widowed mother lives with them
                home.Stock[(int)Good.Grain] = 130; home.Stock[(int)Good.Firewood] = 20; home.Stock[(int)Good.Cloth] = 8;
                strip.HolderId = dad.Id;
                foreach (var p in t.People.Where(p => p.Family == fam && p.HomeId < 0)) p.HomeId = home.Id;
            }
            for (int i = 0; i < 2; i++)
            {
                var home = t.Place(BuildingType.FreeHouse, VillageX + 19 + i * 3, TrailRow - 3, out err, true);
                string fam = t.Pick(Defs.PeasantNames);
                var dad = t.AddPerson(false, fam, t.Rnd(24, 44), Rank.Freeman);
                var mom = t.AddPerson(true, fam, t.Rnd(20, 38), Rank.Freeman);
                Marry(dad, mom);
                int n = t.Rng.Next(0, 3);
                for (int k = 0; k < n; k++) t.Child(mom, dad, t.Rnd(0.5f, 12));
                home.Stock[(int)Good.Grain] = 60; home.Stock[(int)Good.Firewood] = 15; home.Stock[(int)Good.Cloth] = 8;
                foreach (var p in t.People.Where(p => p.Family == fam && p.HomeId < 0)) p.HomeId = home.Id;
            }
            // a grown son still living at home, who'll want a cottage of his own before long
            {
                var mom = t.People.Where(p => p.Female && p.Rank == Rank.Serf && p.PartnerId >= 0).OrderByDescending(p => p.Age).First();
                var lad = t.AddPerson(false, mom.Family, Math.Min(t.Rnd(15, 19), mom.Age - 15), Rank.Serf);
                lad.MotherId = mom.Id; lad.FatherId = mom.PartnerId;
            }

            t.Rehouse();
            t.ReeveVacancies(true);
            t.AssignStrips();
            t.Hire();
            t.PlanDay();
            t.DirtyTiles.Clear();
            t.Log.Clear();
            t.AddLog(t.Title(lord) + " of the house of " + t.Dynasty + " has come to " + t.ManorName + " to make it the seat of his kingdom.", LogKind.Crown);
            t.AddLog("The village has six serf families, two freemen and an old track. There is no keep, no church and no mill.", LogKind.Info);
            t.AddLog("Taxes climb the chain: every Friday the reeve collects from each household into the chest at the village cross. On Monday it goes to " + t.Title(steward) + ", the king's steward, who brings it to the king on Tuesday. Each keeps a share.", LogKind.Crown);
            t.AddLog("Serfs owe the king " + t.Policy.WeekWork + " days' work a week. Stake out a Granary first, then the Steward's Hall, the Castle and the Church. Later, found new villages with a Village Cross and raise a baron in one with a Manor.", LogKind.Info);
            return t;
        }

        static void Marry(Citizen a, Citizen b) { a.PartnerId = b.Id; b.PartnerId = a.Id; }

        Citizen AddPerson(bool female, string family, float age, Rank rank)
        {
            var p = new Citizen
            {
                Id = NextId++, Female = female, Family = family, Age = age, Rank = rank,
                First = Pick(female ? Defs.WomenNames : Defs.MenNames), Seed = (float)Rng.NextDouble(), ArrivedDay = Day,
                Trait = (Trait)Rng.Next(6),
                Coin = rank == Rank.Freeman ? Balance.FreemanStartCoin : rank == Rank.Serf ? Balance.SerfStartCoin : 0,
                Loyalty = Balance.LoyaltyStart + Rnd(-8, 8), Piety = Rnd(25, 60), Honesty = (float)Math.Round(Rnd(0.15f, 1f), 2)
            };
            People.Add(p);
            return p;
        }

        Citizen Child(Citizen mother, Citizen father, float age)
        {
            var c = AddPerson(Chance(0.5), father != null ? father.Family : mother.Family, age, mother.Rank == Rank.King ? Rank.Noble : father != null && father.IsNoble ? Rank.Noble : mother.Rank == Rank.Noble ? Rank.Noble : father != null && father.Rank == Rank.Freeman ? Rank.Freeman : mother.Rank == Rank.Freeman ? Rank.Freeman : Rank.Serf);
            c.MotherId = mother.Id; c.FatherId = father != null ? father.Id : -1;
            c.HomeId = mother.HomeId;
            c.Coin = 0;
            return c;
        }
    }
}
