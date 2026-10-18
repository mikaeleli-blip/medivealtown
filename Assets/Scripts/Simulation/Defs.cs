using System;
using System.Collections.Generic;

namespace MedievalTown.Simulation
{
    /// <summary>Saves store these as numbers: only append.</summary>
    public enum BuildingType
    {
        Road, Cottage, FreeHouse, Keep, Church, Granary, Strip, Demesne, Woodcutter, Quarry, Mill, Alehouse, Pavilion, Cart, StewardHall, Wall, WallTower, Gatehouse, VillageCross, Manor, BoundaryStone,
        Pasture, Sheepfold, Fishpond, Bakehouse, Butcher, Weaver
    }
    public enum Category { Roads, Homes, Fields, Works, Trades, Church, Crown, Realm, Hidden }

    /// <summary>Where someone stands in the order of things, from the bottom up.</summary>
    public enum Rank { Serf, Freeman, Reeve, Collector, Steward, Priest, Knight, Baron, Noble, King }

    /// <summary>What someone is doing today.</summary>
    public enum Chore { None, OwnStrip, WeekWork, Build, Gather, Trade, Mass, Pray, Court, Patrol, Play, Rest, Flee, Collect, Deliver, Accounts }

    /// <summary>A temperament. The king's shapes the whole realm.</summary>
    public enum Trait { Just, Pious, Greedy, Cruel, Brave, Gentle }

    public enum LogKind { Info, Good, Bad, Crown, Church, Build, Death, Birth }

    /// <summary>Saves keep one number per good in this order: only append.</summary>
    public enum Good { Grain, Flour, Firewood, Timber, Stone, Ale, Bread, Meat, Fish, Wool, Cloth, Beasts }

    /// <summary>Terrain tile kinds. Values are stored in Town.Terrain.</summary>
    public static class Tile
    {
        public const byte Grass = 0, Water = 1, Tree = 2, Rock = 3, Hill = 4, Sapling = 5, Stump = 6, OutOfBounds = 255;
    }

    public sealed class BuildingDef
    {
        public BuildingType Type;
        public Category Category;
        public string Name, Blurb;
        public int W, H;
        public int Timber, Stone;        // building materials
        public float Work;               // labour-days to finish
        public bool Unique, IsStorage, IsHome, IsField, IsWall;
        public int Workers, Beds, Radius;
        public bool NeedsStone, NeedsWater;
        // a trade a freeman family runs for itself: it makes Sells (from Buys, if any) and keeps the takings, less the market toll
        public bool IsShop;
        public Good? Sells, Buys;
        public float Price;              // what one unit of Sells fetches
        public float Cap;                // stock it keeps on hand before it stops making more
        public Rank? WorkerRank;         // who takes the work: freemen trades, or anyone
        public string Color;             // palette swatch and minimap
        public bool Instant { get { return Work <= 0; } }
    }

    /// <summary>All tunable numbers live here so balancing happens in one place.</summary>
    public static class Balance
    {
        public const int Cols = 224, Rows = 160;   // 16 px tiles
        public const int GoodCount = 12;
        public static readonly int[] SeasonDays = { 14, 21, 21, 14 };
        public const int DaysPerYear = 70, StartYear = 1240;
        public const float DaysPerYearF = 70f;
        public static int SeasonStart(int s) { int d = 0; for (int i = 0; i < s; i++) d += SeasonDays[i]; return d; }

        public const float AdultAge = 14f, ElderAge = 50f;

        // ---- food: what a body needs each day, and what a measure of each thing feeds
        public const float Appetite = 1f, ChildAppetite = 0.6f;
        public const float FlourFeeds = 1f, GrainFeeds = 0.7f;     // grain boiled into pottage goes less far than bread
        public static readonly float[] GardenBySeason = { 0.5f, 0.9f, 0.7f, 0f };   // a cottage's toft garden, per day

        // ---- fields
        public const float StripYield = 440f, DemesneYield = 700f;
        public const float StripGrowth = 0.026f, DemesneGrowth = 0.0125f;   // growth per worker-day in spring and summer
        public const float HarvestPerWorker = 17f;
        public const float ReeveBonus = 1.1f;     // the reeve sees the strips ploughed and reaped on time
        public const float ChildFieldWork = 0.4f;                          // a child of ten or more helps a little
        public const float Tithe = 0.1f;

        // ---- firewood per household per day, by season; what gathering deadwood brings
        public static readonly float[] FirewoodBySeason = { 0.3f, 0.1f, 0.5f, 1.2f };
        public const float GatherWood = 0.7f;
        public const float KeepFirewood = 2f;      // the keep's great hall burns a lot more than a cottage

        // ---- works, per worker-day
        public const float TreeWork = 0.5f;        // two worker-days to fell a tree
        public const int TimberPerTree = 6;
        public const float SplitPerWorker = 2f, FirewoodPerTimber = 4f;
        public const float StonePerWorker = 2.2f;
        public const int StonePerRock = 6;
        public const float MillPerDay = 140f;      // grain a miller grinds in a day
        public const float BrewPerWorker = 6f;     // ale per worker-day, from half as much grain
        public const float HaulPerDay = 10f;
        public const float SaplingGrowChance = 1f / 30f;

        // ---- money
        public const float AlePrice = 0.15f;
        public const float MerchantGrain = 0.25f;   // what the travelling merchant pays for grain on market day
        public const float BuyTimber = 0.7f, BuyStone = 0.9f, BuyGrain = 0.4f;
        public const float FirewoodPrice = 0.05f;   // what freemen pay the lord for firewood   // and what he charges for building materials
        public const float FreemanStartCoin = 6f, SerfStartCoin = 1f;
        public const float StartCoin = 100f;
        public static readonly float[] StartStock = { 420f, 0f, 60f, 70f, 30f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

        // ---- trades and shops (prices in coin; a day's food for one is about a third of a coin)
        public const float VillageGrain = 0.22f;    // what bakers and brewers pay a serf family for its spare grain
        public const float BreadFeeds = 1f, MeatFeeds = 1.3f, FishFeeds = 1f;
        public const float BakePerWorker = 14f;     // loaves a baker makes in a day, a loaf from a measure of grain
        public const float BeastsPerWorker = 0.18f; // beasts a herdsman raises in a day
        public const float MeatPerBeast = 10f;
        public const float WoolPerWorker = 0.9f;    // fleeces a shepherd brings in a day (half in winter)
        public const float WoolPerCloth = 3f, ClothPerWorker = 0.5f;
        public const float FishPerWorker = 4f;
        public const float ClothWear = 1.4f;        // cloth a person wears out in a year
        public const float BeastPrice = 3.6f, WoolPrice = 0.5f;
        public const float CreditLimit = 4f;        // what a supplier lets a shop have on credit, paid back from its takings

        // ---- villages and baronies
        public const int VillageRadius = 14;       // tiles from a village cross that belong to its village
        public const int StoneReach = 10;          // tiles a boundary stone adds to its village's land around it
        public const int BaronyReach = 45;         // a new village this close to a baron's manor falls under him
        public const float BaronStartLoyalty = 65f;

        // ---- the officers
        public const float SkimMax = 0.35f;        // the most a thoroughly crooked officer takes on top of his cut
        public const int StewardArrives = 6;       // days for a new steward to come after one's dismissed
        public const int RevoltDays = 14;          // days of near-zero loyalty before the village rises

        // ---- people
        public const float LoyaltyStart = 55f;
        public const float FleeBelow = 15f;
        public const int HouseholdMax = 7;
    }

    public static class Defs
    {
        static readonly Dictionary<BuildingType, BuildingDef> all = new Dictionary<BuildingType, BuildingDef>();
        public static BuildingDef Get(BuildingType t) { return all[t]; }
        public static IEnumerable<BuildingDef> All { get { return all.Values; } }

        static void Add(BuildingDef d) { all[d.Type] = d; }

        static Defs()
        {
            Add(new BuildingDef { Type = BuildingType.Road, Category = Category.Roads, Name = "Track", Blurb = "A beaten earth track. People walk faster on it.", W = 1, H = 1, Color = "#c4a878" });
            Add(new BuildingDef { Type = BuildingType.Cottage, Category = Category.Homes, Name = "Cottage", Blurb = "Wattle, daub and thatch on a cruck frame, with a toft garden behind. Houses one serf family.", W = 2, H = 3, Timber = 10, Work = 6, IsHome = true, Beds = Balance.HouseholdMax, Color = "#c8a050" });
            Add(new BuildingDef { Type = BuildingType.FreeHouse, Category = Category.Homes, Name = "Freeman's House", Blurb = "A timber-framed house on a stone sill. Freemen pay rent in coin instead of labour.", W = 2, H = 3, Timber = 16, Stone = 4, Work = 9, IsHome = true, Beds = Balance.HouseholdMax, Color = "#e8dcc4" });
            Add(new BuildingDef { Type = BuildingType.Keep, Category = Category.Crown, Name = "Castle", Blurb = "The king's stone keep and great hall. Court is held here, and the taxes end up in its treasury. Until it stands the royal family lives in a pavilion.", W = 7, H = 6, Timber = 40, Stone = 120, Work = 60, Unique = true, IsHome = true, Beds = 12, Color = "#8d877d" });
            Add(new BuildingDef { Type = BuildingType.Church, Category = Category.Church, Name = "Parish Church", Blurb = "Stone nave and bell tower. The priest says mass on Sundays and takes a tenth of every harvest.", W = 3, H = 4, Timber = 20, Stone = 50, Work = 30, Unique = true, Beds = 1, Color = "#e8e2d4" });
            Add(new BuildingDef { Type = BuildingType.Granary, Category = Category.Crown, Name = "Royal Granary", Blurb = "The king's barn and stores: grain, flour, firewood, timber and stone. The grain taxes end up here.", W = 3, H = 2, Timber = 24, Stone = 6, Work = 10, IsStorage = true, Color = "#b5432f" });
            Add(new BuildingDef { Type = BuildingType.Strip, Category = Category.Fields, Name = "Strip Field", Blurb = "A serf family's strips in the open field. They work it on the days they don't owe the king, and give part of the harvest in tax.", W = 5, H = 4, Work = 3, IsField = true, Color = "#a08452" });
            Add(new BuildingDef { Type = BuildingType.Demesne, Category = Category.Hidden, Name = "Royal Demesne", Blurb = "No longer used.", W = 4, H = 4, Work = 3, IsField = true, Color = "#d8b060" });
            Add(new BuildingDef { Type = BuildingType.Woodcutter, Category = Category.Works, Name = "Woodward's Lodge", Blurb = "Freemen fell the king's trees for timber and split them for firewood.", W = 2, H = 2, Timber = 8, Work = 5, Workers = 3, Radius = 10, WorkerRank = Rank.Freeman, Color = "#7a4e2c" });
            Add(new BuildingDef { Type = BuildingType.Quarry, Category = Category.Works, Name = "Quarry", Blurb = "Freemen break stone from a hillside or boulders nearby.", W = 2, H = 2, Timber = 6, Work = 5, Workers = 3, Radius = 6, NeedsStone = true, WorkerRank = Rank.Freeman, Color = "#a39c90" });
            Add(new BuildingDef { Type = BuildingType.Mill, Category = Category.Works, Name = "Windmill", Blurb = "The king's mill. Everyone must grind their grain here and leave the miller's share for the crown. Bread feeds better than pottage.", W = 2, H = 2, Timber = 20, Stone = 6, Work = 12, Workers = 1, WorkerRank = Rank.Freeman, Color = "#f0ece0" });
            Add(new BuildingDef { Type = BuildingType.Alehouse, Category = Category.Trades, Name = "Alehouse", Blurb = "A freeman family brews ale from grain it buys and sells it of an evening. A cup now and then keeps folk content.", W = 2, H = 2, Timber = 14, Stone = 2, Work = 8, Workers = 2, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Ale, Buys = Good.Grain, Price = Balance.AlePrice, Cap = 60, Color = "#c49a3a" });
            // raw goods: a freeman family raises or catches them and sells them on
            Add(new BuildingDef { Type = BuildingType.Pasture, Category = Category.Trades, Name = "Pasture", Blurb = "A herdsman's family keeps cattle and pigs on fenced grazing and sells the fattened beasts to the butcher.", W = 4, H = 3, Timber = 8, Work = 4, Workers = 1, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Beasts, Price = Balance.BeastPrice, Cap = 6, Color = "#7a9a4a" });
            Add(new BuildingDef { Type = BuildingType.Sheepfold, Category = Category.Trades, Name = "Sheepfold", Blurb = "A shepherd's family grazes a flock and sells the wool to the weaver.", W = 4, H = 3, Timber = 8, Work = 4, Workers = 1, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Wool, Price = Balance.WoolPrice, Cap = 30, Color = "#e8e2d0" });
            Add(new BuildingDef { Type = BuildingType.Fishpond, Category = Category.Trades, Name = "Fishpond", Blurb = "A fisher's hut and stew pond by the water. The fisher sells fish to any household with a coin for it.", W = 3, H = 2, Timber = 8, Work = 4, Workers = 2, Radius = 3, NeedsWater = true, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Fish, Price = 0.35f, Cap = 40, Color = "#4a7a9a" });
            // shops: they buy what they need and sell to the households
            Add(new BuildingDef { Type = BuildingType.Bakehouse, Category = Category.Trades, Name = "Bakehouse", Blurb = "A baker buys serfs' spare grain, has it ground at the king's mill and sells bread. Needs a working windmill.", W = 2, H = 2, Timber = 10, Stone = 8, Work = 8, Workers = 2, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Bread, Buys = Good.Grain, Price = 0.4f, Cap = 50, Color = "#c88a4a" });
            Add(new BuildingDef { Type = BuildingType.Butcher, Category = Category.Trades, Name = "Butcher's Shop", Blurb = "Buys fattened beasts from the pasture and sells meat. Folk who eat meat now and then are hardier and happier.", W = 2, H = 2, Timber = 12, Stone = 2, Work = 7, Workers = 1, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Meat, Buys = Good.Beasts, Price = 0.6f, Cap = 40, Color = "#a8443a" });
            Add(new BuildingDef { Type = BuildingType.Weaver, Category = Category.Trades, Name = "Weaver's House", Blurb = "Buys wool from the sheepfold and weaves cloth. A family without decent clothes feels the cold of winter.", W = 2, H = 2, Timber = 12, Work = 7, Workers = 1, WorkerRank = Rank.Freeman, IsShop = true, Sells = Good.Cloth, Buys = Good.Wool, Price = 3f, Cap = 12, Color = "#6a5a8a" });
            Add(new BuildingDef { Type = BuildingType.Pavilion, Category = Category.Hidden, Name = "Royal Pavilion", Blurb = "A striped tent where the royal family lives until the castle is built.", W = 2, H = 2, Beds = 12, IsHome = true, Color = "#b5432f" });
            Add(new BuildingDef { Type = BuildingType.Cart, Category = Category.Hidden, Name = "Baggage Cart", Blurb = "Everything the household brought. It's unloaded into the granary once one stands.", W = 2, H = 2, IsStorage = true, Color = "#8a5a34" });
            Add(new BuildingDef { Type = BuildingType.StewardHall, Category = Category.Crown, Name = "Steward's Hall", Blurb = "Where the king's steward lives and keeps the crown's tax chest. His collector brings the taxes here every Monday; the steward takes them on to the castle on Tuesday.", W = 3, H = 2, Timber = 16, Stone = 18, Work = 12, Unique = true, IsHome = true, Beds = 4, Color = "#3e5a7a" });
            Add(new BuildingDef { Type = BuildingType.VillageCross, Category = Category.Realm, Name = "Village Cross", Blurb = "Founds a new village: a stone cross and a well on a green. Homes, strips and works within 14 tiles belong to it. Families come once there are cottages, and they choose their own reeve.", W = 2, H = 2, Timber = 2, Stone = 6, Work = 3, Color = "#d8c8a0" });
            Add(new BuildingDef { Type = BuildingType.BoundaryStone, Category = Category.Realm, Name = "Boundary Stone", Blurb = "A carved stone that marks a village's bounds. Set one on a village's land and the land around it, 10 tiles each way, belongs to that village too. Chain them to push the bounds further.", W = 1, H = 1, Stone = 2, Work = 1, Color = "#bdb6aa" });
            Add(new BuildingDef { Type = BuildingType.Manor, Category = Category.Realm, Name = "Baron's Manor", Blurb = "Makes a village the seat of a barony. A baron comes to live here with his tax collector. Villages you found nearby will owe their taxes to him, and he to the crown.", W = 4, H = 3, Timber = 30, Stone = 40, Work = 30, IsHome = true, Beds = 6, Color = "#7a3a4a" });
            Add(new BuildingDef { Type = BuildingType.Wall, Category = Category.Crown, Name = "Castle Wall", Blurb = "A stone curtain wall with a walkway and battlements. Drag to lay a line of it; it joins up with towers and the gatehouse. Nobody can walk through it.", W = 1, H = 1, Stone = 3, Work = 1, IsWall = true, Color = "#6e6860" });
            Add(new BuildingDef { Type = BuildingType.WallTower, Category = Category.Crown, Name = "Wall Tower", Blurb = "A round stone tower to anchor the corners and long runs of the wall.", W = 2, H = 2, Timber = 4, Stone = 20, Work = 8, IsWall = true, Color = "#7a746a" });
            Add(new BuildingDef { Type = BuildingType.Gatehouse, Category = Category.Crown, Name = "Gatehouse", Blurb = "Twin towers over an arched gate with a portcullis. The way through the walls.", W = 3, H = 2, Timber = 8, Stone = 30, Work = 14, IsWall = true, Color = "#8a847a" });
        }

        public static readonly Category[] PaletteOrder = { Category.Roads, Category.Homes, Category.Fields, Category.Works, Category.Trades, Category.Church, Category.Crown, Category.Realm };
        public static readonly string[] CategoryNames = { "Tracks", "Homes", "Fields", "Works", "Trades", "Church", "Crown", "Realm", "" };
        public static IEnumerable<BuildingDef> InCategory(Category c) { foreach (var d in all.Values) if (d.Category == c) yield return d; }

        public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
        public static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        public static readonly string[] GoodNames = { "Grain", "Flour", "Firewood", "Timber", "Stone", "Ale", "Bread", "Meat", "Fish", "Wool", "Cloth", "Beasts" };

        public static readonly string[] RankNames = { "Serf", "Freeman", "Reeve", "Collector", "Steward", "Priest", "Knight", "Baron", "Royal", "King" };
        public static string RankName(Citizen p)
        {
            if (p.IsChild) return p.Rank >= Rank.Noble ? (p.Female ? "Princess" : "Prince") : "Child";
            switch (p.Rank)
            {
                case Rank.King: return p.Female ? "Queen regnant" : "King";
                case Rank.Noble: return p.Female ? (p.PartnerId >= 0 ? "Queen" : "Princess") : (p.PartnerId >= 0 ? "Prince consort" : "Prince");
                case Rank.Steward: return "The king's steward";
                case Rank.Baron: return p.Female ? "Baroness" : "Baron";
                case Rank.Collector: return "Tax collector";
                case Rank.Knight: return "Knight";
                case Rank.Priest: return "Priest";
                case Rank.Reeve: return "Reeve";
                case Rank.Freeman: return p.Female ? "Freewoman" : "Freeman";
                default: return "Serf";
            }
        }

        public static readonly string[] TraitNames = { "Just", "Pious", "Greedy", "Cruel", "Brave", "Gentle" };
        public static readonly string[] TraitBlurbs =
        {
            "Holds fair courts. Folk trust the crown more.",
            "Gives to the church and keeps the feast days. Folk are a little more devout.",
            "Counts every coin. Takes a bigger cut of the mill and market.",
            "Rules by fear. Folk resent the crown, but fewer dare to flee.",
            "Leads his knights himself. Families think twice about fleeing.",
            "Soft-hearted. Feeds the hungry from the granary."
        };

        public static readonly string[] MenNames =
        {
            "William", "John", "Robert", "Richard", "Walter", "Hugh", "Thomas", "Roger", "Geoffrey", "Ralph", "Henry", "Simon", "Adam",
            "Nicholas", "Gilbert", "Peter", "Stephen", "Alan", "Osbert", "Reginald", "Philip", "Hamo", "Baldwin", "Godfrey", "Wat", "Hob",
            "Jordan", "Elias", "Martin", "Lambert", "Ivo", "Warin", "Odo", "Gervase", "Benedict"
        };
        public static readonly string[] WomenNames =
        {
            "Alice", "Matilda", "Agnes", "Joan", "Emma", "Isabel", "Margery", "Cecily", "Edith", "Avice", "Beatrice", "Juliana", "Christina",
            "Margaret", "Sybil", "Rose", "Mabel", "Lettice", "Hawise", "Amice", "Petronilla", "Eleanor", "Maud", "Idonea", "Olive", "Denise"
        };
        public static readonly string[] PeasantNames =
        {
            "Miller", "Smith", "Carter", "atte Wode", "atte Welle", "Thatcher", "Shepherd", "Hayward", "Fowler", "Webb", "Tanner", "Baker",
            "Brewster", "Cooper", "Fletcher", "Wright", "Ploughman", "Godwin", "Hobbes", "Brook", "Hill", "Green", "Ashby", "Thornton",
            "Pyke", "Swan", "Crowe", "Bunce", "Dyer", "Mason", "Wynn", "Colyn", "Reed", "Lytle"
        };
        public static readonly string[] NobleHouses = { "de Ravel", "de Mortaine", "Fitzhugh", "de Aubrey", "de Lisle", "Corbet", "de Valmont", "Fitzwarin", "de Brienne", "Marchaunt" };
        public static readonly string[] KnightNames = { "Fitzroy", "de Cressy", "Malet", "de Grey", "Basset", "de Tracy", "Peverel", "de Hastings", "Mauduit", "de Burgh" };
        public static readonly string[] Kings = { "Edmund", "Aldric", "Henry", "Stephen", "Edward", "Richard" };
        public static readonly string[] Ordinals = { "", " II", " III", " IV", " V" };
        public static readonly string[] VillageNames = { "Ashby", "Thornbury", "Oakhurst", "Wexley", "Hollowmere", "Elmstead", "Brackenford", "Coldharbour", "Wyndham", "Fernley",
            "Millbrook", "Stonebridge", "Harwick", "Lindale", "Ravensby", "Kettlewell", "Marston", "Bramley", "Hawkridge", "Whitmoor", "Dunham", "Ely Cross", "Fallowfield", "Greystead" };
        public static readonly string[] Saints = { "St Michael", "St Mary", "St Peter", "St Andrew", "St Botolph", "St Giles", "All Saints", "St Lawrence" };
    }
}
