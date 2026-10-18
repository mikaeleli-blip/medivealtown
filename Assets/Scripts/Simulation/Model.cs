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
        public float Seed;               // 0..1, drives looks and small quirks
        public int ArrivedDay;
        public int MassDay = -999;       // last Sunday at mass
        public int AleDay = -999;        // last cup at the alehouse
        public int Pregnant;             // days to go
        public string Name { get { return First + " " + Family; } }
        public bool IsChild { get { return Age < Balance.AdultAge; } }
        public bool IsNoble { get { return Rank == Rank.Lord || Rank == Rank.Noble; } }
    }

    /// <summary>The lord's rules for the manor: what the serfs owe and how the lord trades.</summary>
    [Serializable]
    public class ManorPolicy
    {
        public int WeekWork = 2;           // days a week each serf owes on the lord's land
        public float Rent = 1f;            // coin per freeman household per week
        public float Tallage = 1f;         // coin per serf household, taken at Martinmas (start of winter)
        public float Multure = 0.08f;      // the lord's share of every sack ground at the mill
        public float GrainPrice = 0.2f;     // what freemen pay for a measure of the lord's grain
        public float FreeWage = 0.6f;      // a day's pay for freemen working for the lord
        public bool Dole = true;           // the granary feeds the hungry
        public bool SellSurplus = true;    // the merchant buys the granary's grain above the reserve
        public float Reserve = 200f;       // grain the granary keeps back beyond the household's needs until harvest
        public bool BuyMaterials = true;   // buy timber and stone from the merchant for waiting building sites
        public float CoinReserve = 60f;    // coin the treasury won't spend on materials (the king's tribute)
    }

    /// <summary>The king, far away, and how he regards this manor.</summary>
    [Serializable]
    public class Crown
    {
        public string King;
        public float Favour = Balance.StartFavour;
        public int TributeYear;            // the last year tribute was settled
        public float LastTribute, LastDue;
        public bool Stripped;              // the king has taken the manor back: game over
        public int SuitorDay = -1;         // a bride the king promised arrives on this day
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
        public float Coin, Grain, Loyalty, Favour;
    }
}
