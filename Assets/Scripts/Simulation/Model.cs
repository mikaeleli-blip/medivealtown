using System;
using System.Collections.Generic;

namespace MedievalTown.Simulation
{
    [Serializable]
    public class Building
    {
        public int Id;
        public BuildingType Type;
        public int X, Y;
        public int BuiltDay;

        // Construction
        public bool Built = true;
        public float Progress;                                     // labour-days done
        public float[] Delivered = new float[Balance.GoodCount];   // materials on site

        // Goods held here: the granary's stores, a household's pantry and woodpile, the church's tithe
        public float[] Stock = new float[Balance.GoodCount];

        public List<int> Residents = new List<int>();
        public List<int> Employees = new List<int>();

        // Fields
        public int HolderId = -1;        // the serf household (its head) that holds a strip
        public float CropGrowth;         // 0..1 through spring and summer
        public float CropLeft;           // grain still standing in autumn
        public float Harvested;          // brought in this year
        // Woodward and quarry: progress on the current tree or boulder
        public float ChopWork;
        public int TargetTile = -1;
        // Church
        public int Attendance;
        public int Graves;
        // Alehouse: cups sold today
        public int SoldToday;
        // Taxes: what a household owes and hasn't yet handed to the reeve; the coin in the bailiff's chest
        public float OwedGrain, OwedCoin, Arrears;
        public float Coin;
        public int Mask;                 // walls: which neighbours they join (1 north, 2 east, 4 south, 8 west; 16: the north one is a tower or gate)

        public BuildingDef Def { get { return Defs.Get(Type); } }
        public bool Contains(int tx, int ty)
        {
            var d = Def;
            return tx >= X && tx < X + d.W && ty >= Y && ty < Y + d.H;
        }
        public float MaterialsFraction
        {
            get
            {
                var d = Def; float need = d.Timber + d.Stone;
                if (need <= 0) return 1f;
                return (Delivered[(int)Good.Timber] + Delivered[(int)Good.Stone]) / need;
            }
        }
        public float BuildFraction { get { var d = Def; return d.Work <= 0 ? 1f : Math.Min(1f, Progress / d.Work); } }
    }

    [Serializable]
    public class Citizen
    {
        public int Id;
        public string First, Family;
        public bool Female;
        public float Age;                // years
        public int PartnerId = -1;
        public int MotherId = -1, FatherId = -1;
        public Rank Rank;
        public Trait Trait;
        public int HomeId = -1;
        public int WorkId = -1;          // a trade: woodward, quarry, mill, alehouse
        public Chore Chore;              // today
        public int TaskId = -1;          // the building they work at today
        public float Fed = 1f;
        public bool Warm = true;
        public float Health = 100f;
        public int Hungry;
        public float Loyalty = Balance.LoyaltyStart;   // to the lord of the manor
        public float Piety = 40f;
        public float Coin;
        public int Mask;                 // walls: which neighbours they join (1 north, 2 east, 4 south, 8 west; 16: the north one is a tower or gate)
        public float Seed;               // 0..1, drives looks and small quirks
        public int ArrivedDay;
        public int MassDay = -999;       // last Sunday at mass
        public int AleDay = -999;        // last cup at the alehouse
        public int Pregnant;             // days to go
        public float Honesty = 0.7f;     // 0..1: officers below about 0.6 take more than their due
        public string Name { get { return First + " " + Family; } }
        public bool IsChild { get { return Age < Balance.AdultAge; } }
        public bool IsNoble { get { return Rank == Rank.King || Rank == Rank.Noble; } }
    }

    /// <summary>The crown's rules for the village: what the serfs owe, how taxes are collected, how the crown trades.</summary>
    [Serializable]
    public class ManorPolicy
    {
        public int WeekWork = 2;           // days a week each serf owes on the king's land
        public float HarvestTax = 0.3f;    // share of every strip's harvest owed in grain
        public float HearthTax = 0.1f;     // coin per household per week
        public float Rent = 1f;            // coin per freeman household per week, on top of the hearth tax
        public float Tallage = 1f;         // coin per serf household, assessed at Martinmas (start of winter)
        public float ReeveCut = 0.05f;     // the reeve's share of what he collects
        public float BailiffCut = 0.1f;    // the bailiff's share of what he takes on to the castle
        public float Multure = 0.08f;      // the crown's share of every sack ground at the mill
        public float GrainPrice = 0.2f;    // what freemen pay for a measure of the king's grain
        public float FreeWage = 0.6f;      // a day's pay for freemen working for the crown
        public bool Dole = true;           // the granary feeds the hungry
        public bool SellSurplus = true;    // the merchant buys the granary's grain above the reserve
        public float Reserve = 200f;       // grain the granary keeps back beyond the household's needs until harvest
        public bool BuyMaterials = true;   // buy timber and stone from the merchant for waiting building sites
        public float CoinReserve = 40f;    // coin the treasury won't spend on materials
    }

    /// <summary>Where the taxes went: assessed on the households, collected by the reeve, taken on by the bailiff, received by the king.</summary>
    [Serializable]
    public class TaxBook
    {
        public float AssessedCoin, AssessedGrain;          // what households were told they owe
        public float CollectedCoin, CollectedGrain;        // what the reeve brought to the bailiff (after his cut)
        public float ReeveCutCoin, ReeveCutGrain;
        public float BailiffCutCoin, BailiffCutGrain;
        public float ReceivedCoin, ReceivedGrain;          // what reached the castle
        public float Unpaid;                               // coin households couldn't pay (still owed)
        public void Clear() { AssessedCoin = AssessedGrain = CollectedCoin = CollectedGrain = ReeveCutCoin = ReeveCutGrain = BailiffCutCoin = BailiffCutGrain = ReceivedCoin = ReceivedGrain = Unpaid = 0; }
        public void Add(TaxBook o)
        {
            AssessedCoin += o.AssessedCoin; AssessedGrain += o.AssessedGrain; CollectedCoin += o.CollectedCoin; CollectedGrain += o.CollectedGrain;
            ReeveCutCoin += o.ReeveCutCoin; ReeveCutGrain += o.ReeveCutGrain; BailiffCutCoin += o.BailiffCutCoin; BailiffCutGrain += o.BailiffCutGrain;
            ReceivedCoin += o.ReceivedCoin; ReceivedGrain += o.ReceivedGrain; Unpaid += o.Unpaid;
        }
    }

    /// <summary>The state of the realm: whether the village has risen.</summary>
    [Serializable]
    public class Crown
    {
        public int LowDays;                // days in a row loyalty has been near nothing
        public bool Stripped;              // the village rose and deposed the king: game over
        public int SuitorDay = -1;         // a royal bride arrives on this day
        public int BailiffDay = -1;        // a new bailiff arrives on this day
        public int GrumbleDay = -999;      // the last time the villagers complained about the reeve
    }

    [Serializable]
    public class LogEntry
    {
        public int Day;
        public string Message;
        public LogKind Kind;
    }

    [Serializable]
    public class HistoryPoint
    {
        public int Day, Population;
        public float Coin, Grain, Loyalty;
    }
}
