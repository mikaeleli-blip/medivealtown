using System;
using System.Collections.Generic;

namespace MedievalTown.Simulation
{
    /// <summary>Saves store these as numbers: only append.</summary>
    public enum BuildingType
    {
        Road, Cottage, FreeHouse, Keep, Church, Granary, Strip, Demesne, Woodcutter, Quarry, Mill, Alehouse, Pavilion, Cart
    }
    public enum Category { Roads, Homes, Fields, Works, Church, Hidden }

    /// <summary>Where someone stands in the order of things, from the bottom up.</summary>
    public enum Rank { Serf, Freeman, Reeve, Priest, Knight, Noble, Lord }

    /// <summary>What someone is doing today.</summary>
    public enum Chore { None, OwnStrip, WeekWork, Build, Gather, Trade, Mass, Pray, Court, Patrol, Play, Rest, Flee }

    /// <summary>A lord's (or anyone's) temperament. Lords' traits shape the whole manor.</summary>
    public enum Trait { Just, Pious, Greedy, Cruel, Brave, Gentle }

    public enum LogKind { Info, Good, Bad, Crown, Church, Build, Death, Birth }

    /// <summary>Saves keep one number per good in this order: only append.</summary>
    public enum Good { Grain, Flour, Firewood, Timber, Stone, Ale }

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
        public bool Unique, IsStorage, IsHome, IsField;
        public int Workers, Beds, Radius;
        public bool NeedsStone;
        public Rank? WorkerRank;         // who takes the work: freemen trades, or anyone
        public string Color;             // palette swatch and minimap
        public bool Instant { get { return Work <= 0; } }
    }

    /// <summary>All tunable numbers live here so balancing happens in one place.</summary>
    public static class Balance
    {
        public const int Cols = 160, Rows = 110;   // 16 px tiles
        public const int GoodCount = 6;
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
        public const float StripYield = 320f, DemesneYield = 700f;
        public const float StripGrowth = 0.03f, DemesneGrowth = 0.0125f;   // growth per worker-day in spring and summer
        public const float HarvestPerWorker = 13f;
        public const float ReeveBonus = 1.15f;
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
        public const float AlePrice = 0.08f;
        public const float MerchantGrain = 0.25f;   // what the travelling merchant pays for grain on market day
        public const float BuyTimber = 0.7f, BuyStone = 0.9f;
        public const float FirewoodPrice = 0.05f;   // what freemen pay the lord for firewood   // and what he charges for building materials
        public const float FreemanStartCoin = 6f, SerfStartCoin = 1f;
        public const float StartCoin = 100f;
        public static readonly float[] StartStock = { 420f, 0f, 60f, 70f, 30f, 0f };

        // ---- the crown
        public const float TributeBase = 20f, TributePerAdult = 2f;
        public const float StartFavour = 60f;

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
            Add(new BuildingDef { Type = BuildingType.Keep, Category = Category.Homes, Name = "Keep", Blurb = "The lord's stone keep and great hall. Court is held here. Until it stands the lord's family lives in a pavilion.", W = 4, H = 4, Timber = 30, Stone = 80, Work = 40, Unique = true, IsHome = true, Beds = 12, Color = "#8d877d" });
            Add(new BuildingDef { Type = BuildingType.Church, Category = Category.Church, Name = "Parish Church", Blurb = "Stone nave and bell tower. The priest says mass on Sundays and takes a tenth of every harvest.", W = 3, H = 4, Timber = 20, Stone = 50, Work = 30, Unique = true, Beds = 1, Color = "#e8e2d4" });
            Add(new BuildingDef { Type = BuildingType.Granary, Category = Category.Works, Name = "Granary", Blurb = "The lord's barn and stores: grain, flour, firewood, timber and stone.", W = 3, H = 2, Timber = 24, Stone = 6, Work = 10, IsStorage = true, Color = "#b5432f" });
            Add(new BuildingDef { Type = BuildingType.Strip, Category = Category.Fields, Name = "Strip Field", Blurb = "A serf family's strips in the open field. They work it on the days they don't owe the lord.", W = 3, H = 3, Work = 2, IsField = true, Color = "#a08452" });
            Add(new BuildingDef { Type = BuildingType.Demesne, Category = Category.Fields, Name = "Demesne Field", Blurb = "The lord's own field, worked by serfs on their week-work days. Its harvest fills the granary.", W = 4, H = 4, Work = 3, IsField = true, Color = "#d8b060" });
            Add(new BuildingDef { Type = BuildingType.Woodcutter, Category = Category.Works, Name = "Woodward's Lodge", Blurb = "Freemen fell the lord's trees for timber and split them for firewood.", W = 2, H = 2, Timber = 8, Work = 5, Workers = 3, Radius = 10, WorkerRank = Rank.Freeman, Color = "#7a4e2c" });
            Add(new BuildingDef { Type = BuildingType.Quarry, Category = Category.Works, Name = "Quarry", Blurb = "Freemen break stone from a hillside or boulders nearby.", W = 2, H = 2, Timber = 6, Work = 5, Workers = 3, Radius = 6, NeedsStone = true, WorkerRank = Rank.Freeman, Color = "#a39c90" });
            Add(new BuildingDef { Type = BuildingType.Mill, Category = Category.Works, Name = "Windmill", Blurb = "The lord's mill. Serfs must grind their grain here and leave the miller's share for the lord. Bread feeds better than pottage.", W = 2, H = 2, Timber = 20, Stone = 6, Work = 12, Workers = 1, WorkerRank = Rank.Freeman, Color = "#f0ece0" });
            Add(new BuildingDef { Type = BuildingType.Alehouse, Category = Category.Works, Name = "Alehouse", Blurb = "A freeman brews ale from grain and sells it of an evening. A cup now and then keeps folk content.", W = 2, H = 2, Timber = 14, Stone = 2, Work = 8, Workers = 2, WorkerRank = Rank.Freeman, Color = "#c49a3a" });
            Add(new BuildingDef { Type = BuildingType.Pavilion, Category = Category.Hidden, Name = "Lord's Pavilion", Blurb = "A striped tent where the lord's family lives until the keep is built.", W = 2, H = 2, Beds = 12, IsHome = true, Color = "#b5432f" });
            Add(new BuildingDef { Type = BuildingType.Cart, Category = Category.Hidden, Name = "Baggage Cart", Blurb = "Everything the household brought. It's unloaded into the granary once one stands.", W = 2, H = 2, IsStorage = true, Color = "#8a5a34" });
        }

        public static readonly Category[] PaletteOrder = { Category.Roads, Category.Homes, Category.Fields, Category.Works, Category.Church };
        public static readonly string[] CategoryNames = { "Tracks", "Homes", "Fields", "Works", "Church", "" };
        public static IEnumerable<BuildingDef> InCategory(Category c) { foreach (var d in all.Values) if (d.Category == c) yield return d; }

        public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
        public static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        public static readonly string[] GoodNames = { "Grain", "Flour", "Firewood", "Timber", "Stone", "Ale" };

        public static readonly string[] RankNames = { "Serf", "Freeman", "Reeve", "Priest", "Knight", "Noble", "Lord" };
        public static string RankName(Citizen p)
        {
            if (p.IsChild) return p.Rank >= Rank.Noble ? (p.Female ? "Young lady" : "Young lord") : "Child";
            switch (p.Rank)
            {
                case Rank.Lord: return p.Female ? "Lady of the manor" : "Lord of the manor";
                case Rank.Noble: return p.Female ? "Lady" : "Noble";
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
            "Holds fair courts. Folk trust the manor more.",
            "Gives to the church and keeps the feast days. Folk are a little more devout.",
            "Counts every coin. Takes a bigger cut of the mill and market.",
            "Rules by fear. Folk resent the manor, but fewer dare to flee.",
            "Wins the king's respect. Royal favour falls more slowly.",
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
        public static readonly string[] Saints = { "St Michael", "St Mary", "St Peter", "St Andrew", "St Botolph", "St Giles", "All Saints", "St Lawrence" };
    }
}
