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
        // Taxes: what a household owes and hasn't yet handed to the reeve; the coin in the steward's chest
        public float OwedGrain, OwedCoin, Arrears;
        public float Coin, ChestGrain;    // a tax chest: village cross, baron's manor, steward's hall
        public int VillageId = -1;       // the village it belongs to
        public float WeekSales, LastSales; // a shop's takings this week and last
        public float Debt; public int DebtTo = -1;   // what a shop owes the supplier who let it have goods on credit
        // A trade as a business: the crown runs it until a freeman family takes it up, paying the entry fine
        public int OwnerId = -1;         // the freeman who holds the shop (-1: the crown runs it)
        public float Wage;               // a day's pay for a hired hand
        public int Desired = -1;         // hands the shop wants (owner and family included); -1 until first decided
        public float Price;              // what it asks for what it sells (0: the usual price)
        public float WeekCosts, LastProfit;   // what it paid out this week, and last week's takings less costs
        public int LossWeeks;            // weeks running it has lost money
        public float FineOwed;           // what's left of the entry fine, paid to the lord week by week
        public int MissedWeeks;          // weeks the fine went unpaid; four and the lord takes the shop back
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
        public float Seed;               // 0..1, drives looks and small quirks
        public int ArrivedDay;
        public int MassDay = -999;       // last Sunday at mass
        public int AleDay = -999;        // last cup at the alehouse
        public int BreadDay = -999, MeatDay = -999, FishDay = -999;   // last ate bought bread, meat, fish
        public int Pregnant;             // days to go
        public bool WasFree;             // a freeman serving as reeve, who goes back to being free when his year's done
        public bool FamilyHand;          // works the family's shop for no wage
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
        public float HearthTax = 0.25f;     // coin per household per week
        public float Rent = 0.6f;          // coin per freeman household per week, on top of the hearth tax
        public float Tallage = 1f;         // coin per serf household, assessed at Martinmas (start of winter)
        public float ReeveCut = 0.05f;     // the reeve's share of what he collects
        public float BaronCut = 0.15f;     // a baron's share of what his collector brings in
        public float StewardCut = 0.1f;    // the steward's share of what he takes on to the castle
        public float Multure = 0.08f;      // the crown's share of every sack ground at the mill
        public float GrainPrice = 0.3f;     // what freemen pay for a measure of the king's grain
        public float FreeWage = 0.6f;      // a day's pay for freemen working for the crown
        public bool Dole = true;           // the granary feeds the hungry
        public bool SellSurplus = true;    // the merchant buys the granary's grain above the reserve
        public float Reserve = 200f;       // (no longer used: see FamineDays)
        public float FamineDays = 10f;     // the famine store: days of grain for every crown family the royal granary keeps for bad years
        public bool BuyMaterials = true;   // buy timber and stone from the merchant for waiting building sites
        public float CoinReserve = 40f;    // coin the treasury won't spend on materials
        public bool Births = true;         // couples have children (the royal family always may)
        public float MarketToll = 0.1f;    // the crown's share of every sale a shop makes, collected by the reeve
    }

    /// <summary>Where the taxes went, stage by stage up the chain: households, reeves, barons, the steward, the king.</summary>
    [Serializable]
    public class TaxBook
    {
        public float AssessedCoin, AssessedGrain;          // what households were told they owe
        public float ReeveCutCoin, ReeveCutGrain;
        public float CollectedCoin, CollectedGrain;        // what reached the village chests
        public float BaronCutCoin, BaronCutGrain;
        public float StewardCutCoin, StewardCutGrain;
        public float ReceivedCoin, ReceivedGrain;          // what reached the castle
        public float Withheld;                             // coin rebellious barons kept back
        public float Tolls;                                // market tolls laid on the shops' sales (part of AssessedCoin)
        public float Unpaid;                               // coin households still owe
        public void Clear() { AssessedCoin = AssessedGrain = ReeveCutCoin = ReeveCutGrain = CollectedCoin = CollectedGrain = BaronCutCoin = BaronCutGrain = StewardCutCoin = StewardCutGrain = ReceivedCoin = ReceivedGrain = Withheld = Unpaid = Tolls = 0; }
        public void Add(TaxBook o)
        {
            AssessedCoin += o.AssessedCoin; AssessedGrain += o.AssessedGrain; ReeveCutCoin += o.ReeveCutCoin; ReeveCutGrain += o.ReeveCutGrain;
            CollectedCoin += o.CollectedCoin; CollectedGrain += o.CollectedGrain; BaronCutCoin += o.BaronCutCoin; BaronCutGrain += o.BaronCutGrain;
            StewardCutCoin += o.StewardCutCoin; StewardCutGrain += o.StewardCutGrain; ReceivedCoin += o.ReceivedCoin; ReceivedGrain += o.ReceivedGrain;
            Withheld += o.Withheld; Unpaid += o.Unpaid; Tolls += o.Tolls;
        }
    }

    /// <summary>A village: its cross on the green, its reeve, and whom it pays its taxes to (a baron, or the crown directly).</summary>
    [Serializable]
    public class Village
    {
        public int Id;
        public string Name;
        public int CrossId = -1;           // the village cross; its chest holds the reeve's takings
        public int ReeveId = -1;
        public int BaronyId = -1;          // -1: the crown's own village
        public bool Capital;
        public int Founded;
        public int GrumbleDay = -999;
        public float LastCoin, LastGrain;  // what the reeve brought in last week
        public float[] Wants = new float[Balance.GoodCount];   // what its families wanted this week and couldn't buy anywhere in the barony
        public int PetitionDay = -1;       // a petition to the king for grain, sent on this day and not yet answered
        public float PetitionGrain;        // how much they asked for
        public int LastPetition = -999;
        public string MerchantNote;        // what the barony's merchant did last
    }

    /// <summary>A barony: a baron in his manor, ruling some villages for the king, with his own tax collector.</summary>
    [Serializable]
    public class Barony
    {
        public int Id;
        public int SeatVillageId = -1;     // the village the manor stands in
        public int ManorId = -1;           // the manor; its chest holds what the collector brings in
        public int BaronId = -1;
        public int CollectorId = -1;
        public float Loyalty = Balance.BaronStartLoyalty;   // to the crown
        public bool Rebel;                 // withholding his taxes
        public int BaronDue = -1;          // a new baron arrives on this day
        public float LastCoin, LastGrain;  // what he sent on last week
    }

    /// <summary>The state of the realm: whether the village has risen.</summary>
    [Serializable]
    public class Crown
    {
        public int LowDays;                // days in a row loyalty has been near nothing
        public bool Stripped;              // the village rose and deposed the king: game over
        public int SuitorDay = -1;         // a royal bride arrives on this day
        public int StewardDay = -1;        // a new steward arrives on this day
        public int CollectorId = -1;       // the steward's man who rides out to the barons and the crown's villages
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
