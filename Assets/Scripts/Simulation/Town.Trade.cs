using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>
    /// The trades: freeman families who raise, catch or make goods and sell them for coin. The herdsman, shepherd and fisher
    /// bring in raw goods; the baker, butcher, weaver and alewife buy what they need and sell to the households. Serf families
    /// earn their coin selling spare grain to the bakers and brewers. Every sale owes the crown its market toll, which the
    /// reeve collects on Friday with the other taxes, so it climbs the same chain.
    /// </summary>
    public partial class Town
    {
        // =====================================================================
        // Money between people
        // =====================================================================
        /// <summary>The grown folk of a shop: who works there, and whose purses pay for what it buys.</summary>
        List<Citizen> Staff(Building b) { return b.Employees.Select(GetCitizen).Where(p => p != null).ToList(); }
        List<Citizen> Purse(Building home) { return home == null ? new List<Citizen>() : home.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild).OrderByDescending(p => p.Coin).ToList(); }
        static float CoinOf(List<Citizen> folk) { float c = 0; foreach (var p in folk) c += p.Coin; return c; }
        static void Pay(List<Citizen> from, float amount)
        {
            foreach (var p in from) { float t = Math.Min(p.Coin, amount); p.Coin -= t; amount -= t; if (amount <= 0.0001f) break; }
        }
        static void Share(List<Citizen> to, float amount) { if (to.Count == 0) return; foreach (var p in to) p.Coin += amount / to.Count; }

        /// <summary>A shop takes coin for a sale. The crown's toll is laid on the shopkeeper's household for the reeve to collect.</summary>
        void Takings(Building shop, float value)
        {
            if (value <= 0) return;
            var staff = Staff(shop);
            // a shop that owes its supplier pays back from its takings first
            float repay = 0;
            var creditor = GetBuilding(shop.DebtTo);
            if (shop.Debt > 0.001f && creditor != null && creditor != shop) { repay = Math.Min(shop.Debt, value * 0.6f); shop.Debt -= repay; Takings(creditor, repay); }
            else if (creditor == null) shop.Debt = 0;
            Share(staff, value - repay);
            shop.WeekSales += value;
            float toll = value * Policy.MarketToll * (RulerIs(Trait.Greedy) ? 1.3f : 1f);
            var home = staff.Count > 0 ? HomeOf(staff[0]) : null;
            if (toll > 0 && home != null && home.VillageId >= 0) { Assess(home, toll, 0); Taxes.Tolls += toll; }
        }

        /// <summary>Coin a household can spend: what it has, less what it owes the reeve and a little put by.</summary>
        float Spendable(Building home, List<Citizen> purse) { return Math.Max(0, CoinOf(purse) - home.OwedCoin - 0.4f); }

        static float Dist2(Building a, Building b) { int dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

        IEnumerable<Building> Shops(BuildingType t) { return OfType(t).Where(b => b.Employees.Count > 0); }

        /// <summary>The shop of a kind a household would go to: its own village's first, then the nearest with stock.</summary>
        Building ShopFor(Building home, BuildingType t, Good g, float atLeast)
        {
            Building best = null; float bd = float.MaxValue;
            foreach (var s in Shops(t))
            {
                if (s.Stock[(int)g] < atLeast) continue;
                float d = Dist2(s, home) + (s.VillageId == home.VillageId && s.VillageId >= 0 ? 0 : 6000);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // =====================================================================
        // A day in the trade
        // =====================================================================
        /// <summary>Whether a shop has anything to do: it's short of stock and has (or can get) what it makes it from.</summary>
        bool ShopNeeded(Building b)
        {
            var d = b.Def;
            if (b.Stock[(int)d.Sells.Value] >= d.Cap) return false;
            if (b.Type == BuildingType.Bakehouse && WorkingMill == null) return false;
            if (d.Buys.HasValue && b.Stock[(int)d.Buys.Value] < 0.2f) return false;
            if (b.Type == BuildingType.Fishpond && WaterNear(b) == 0) return false;
            return true;
        }

        /// <summary>A freeman's day in his own trade.</summary>
        void ShopWork(Building b, Citizen p, float hands)
        {
            int si = Season;
            float season = si == 3 ? 0.5f : 1f;
            var s = b.Stock;
            switch (b.Type)
            {
                case BuildingType.Pasture: s[(int)Good.Beasts] += Balance.BeastsPerWorker * hands * season; break;
                case BuildingType.Sheepfold: s[(int)Good.Wool] += Balance.WoolPerWorker * hands * season; break;
                case BuildingType.Fishpond: s[(int)Good.Fish] += Balance.FishPerWorker * hands * season; break;
                case BuildingType.Bakehouse:
                {
                    // the grain goes to the king's mill, which keeps its share, and comes back as flour for the oven
                    float q = Math.Min(s[(int)Good.Grain], Balance.BakePerWorker * hands);
                    if (q <= 0 || WorkingMill == null) break;
                    float multure = q * Policy.Multure * (RulerIs(Trait.Greedy) ? 1.5f : 1f);
                    s[(int)Good.Grain] -= q; PutInStores(Good.Flour, multure); Today.Multure += multure; Today.Milled += q;
                    s[(int)Good.Bread] += q - multure;
                    break;
                }
                case BuildingType.Butcher:
                {
                    float q = Math.Min(1f * hands, s[(int)Good.Beasts]);
                    s[(int)Good.Beasts] -= q; s[(int)Good.Meat] += q * Balance.MeatPerBeast;
                    break;
                }
                case BuildingType.Weaver:
                {
                    float c = Math.Min(Balance.ClothPerWorker * hands, s[(int)Good.Wool] / Balance.WoolPerCloth);
                    s[(int)Good.Wool] -= c * Balance.WoolPerCloth; s[(int)Good.Cloth] += c;
                    break;
                }
                case BuildingType.Alehouse:
                {
                    float g = Math.Min(s[(int)Good.Grain], Balance.BrewPerWorker * 0.5f * hands);
                    s[(int)Good.Grain] -= g; s[(int)Good.Ale] += g * 2;
                    break;
                }
            }
            var d = b.Def;
            if (s[(int)d.Sells.Value] > d.Cap * 1.2f) s[(int)d.Sells.Value] = d.Cap * 1.2f;   // what won't keep is given away or spoils
        }

        public int WaterNear(Building b)
        {
            var d = b.Def; int n = 0;
            for (int y = b.Y - d.Radius; y < b.Y + d.H + d.Radius; y++)
                for (int x = b.X - d.Radius; x < b.X + d.W + d.Radius; x++)
                    if (TerrainAt(x, y) == Tile.Water) n++;
            return n;
        }

        /// <summary>Each evening the shops buy what they'll need: bakers and brewers buy serfs' spare grain (then the king's),
        /// the butcher buys beasts from the pastures and the weaver buys wool from the sheepfolds.</summary>
        void Procure()
        {
            foreach (var b in Buildings.Where(x => x.Built && x.Def.IsShop && x.Def.Buys.HasValue && x.Employees.Count > 0).ToList())
            {
                var d = b.Def; var staff = Staff(b);
                float purse = Math.Max(0, CoinOf(staff) - 0.5f);
                if (b.Stock[(int)d.Sells.Value] >= d.Cap) continue;
                Good g = d.Buys.Value;
                if (g == Good.Grain)
                {
                    float rate = b.Type == BuildingType.Bakehouse ? Balance.BakePerWorker : Balance.BrewPerWorker * 0.5f;
                    float want = rate * b.Employees.Count * 3 - b.Stock[(int)Good.Grain];
                    if (want < 2 || purse < 0.3f) continue;
                    // serf families with grain to spare, nearest first
                    foreach (var h in Buildings.Where(x => x.Built && IsPeasantHome(x.Type) && x.Residents.Count > 0).OrderBy(x => Dist2(x, b) + (x.VillageId == b.VillageId ? 0 : 6000)).ToList())
                    {
                        if (want < 1 || purse < Balance.VillageGrain) break;
                        float spare = h.Stock[(int)Good.Grain] - GrainKeep(h);
                        if (spare < 3) continue;
                        float q = Math.Min(Math.Min(spare, want), purse / Balance.VillageGrain);
                        float v = q * Balance.VillageGrain;
                        h.Stock[(int)Good.Grain] -= q; b.Stock[(int)Good.Grain] += q; want -= q; purse -= v;
                        Pay(staff, v);
                        var seller = Purse(h); Share(seller.Take(1).ToList(), v);
                    }
                    // the king's granary sells what it can spare
                    if (want >= 1 && purse >= Policy.GrainPrice)
                    {
                        float q = Math.Min(want, Math.Min(purse / Math.Max(0.01f, Policy.GrainPrice), Math.Max(0, StoreStock(Good.Grain) - RoyalKeep * 0.5f)));
                        if (q >= 1) { q = TakeFromStores(Good.Grain, q); float v = q * Policy.GrainPrice; Pay(staff, v); Coin += v; WeekIn += v; b.Stock[(int)Good.Grain] += q; }
                    }
                }
                else
                {
                    // beasts from the pastures, wool from the sheepfolds
                    var from = g == Good.Beasts ? BuildingType.Pasture : BuildingType.Sheepfold;
                    float want = g == Good.Beasts ? 2 - b.Stock[(int)g] : Balance.WoolPerCloth * Balance.ClothPerWorker * b.Employees.Count * 4 - b.Stock[(int)g];
                    float unit = Defs.Get(from).Price;
                    foreach (var src in Shops(from).OrderBy(x => Dist2(x, b)).ToList())
                    {
                        if (want < 0.2f) break;
                        // paid for in coin if the shop has it, otherwise on credit up to a limit, paid back as it sells
                        float credit = b.DebtTo < 0 || b.DebtTo == src.Id ? Math.Max(0, Balance.CreditLimit - b.Debt) : 0;
                        float q = Math.Min(Math.Min(src.Stock[(int)g], want), (purse + credit) / unit);
                        if (q <= 0.05f) continue;
                        float v = q * unit, cash = Math.Min(v, purse), owed = v - cash;
                        src.Stock[(int)g] -= q; b.Stock[(int)g] += q; want -= q; purse -= cash;
                        if (cash > 0) { Pay(staff, cash); Takings(src, cash); }
                        if (owed > 0) { b.Debt += owed; b.DebtTo = src.Id; }
                    }
                    if (b.Debt < 0.001f) b.DebtTo = -1;
                }
            }
        }

        /// <summary>Grain a household holds back: enough to see it to the harvest, and seed for a little more.</summary>
        float GrainKeep(Building h) { return Needs(h) * (DaysUntilHarvest + 10) / Balance.GrainFeeds + 20 - h.Stock[(int)Good.Flour]; }

        /// <summary>Each day some households go to the shops: bread (freemen, who have no strips, live on it), meat and fish now
        /// and then, cloth before winter. They spend what they can spare after what they owe the reeve.</summary>
        void Shopping()
        {
            if (IsSunday) return;
            foreach (var h in Buildings.Where(x => x.Built && IsPeasantHome(x.Type) && x.Residents.Count > 0).ToList())
            {
                if (!Chance(0.5)) continue;
                var purse = Purse(h); if (purse.Count == 0) continue;
                float cash = Spendable(h, purse);
                if (cash < 0.1f) continue;
                float need = Needs(h);
                var head = HeadOf(h);
                bool free = head != null && head.Rank == Rank.Freeman;
                int folk = h.Residents.Count;
                // cloth first when the cold's coming and the family's short of it; a family with coin to spare keeps more
                float clothWant = (Season >= 1 ? 0.5f : 0.25f) * folk + (cash > 8 ? 0.4f * folk : 0);
                if (h.Stock[(int)Good.Cloth] < clothWant) cash -= Buy(h, purse, BuildingType.Weaver, Good.Cloth, 1, cash);
                // bread: freemen, who have no strips, live on it; serfs buy it when they have coin from selling their grain
                float food = h.Stock[(int)Good.Grain] * Balance.GrainFeeds + h.Stock[(int)Good.Flour] + h.Stock[(int)Good.Bread];
                float breadDays = free || food < need * 10 ? 4 : cash > 4 ? 3 : cash > 1.5f ? 1 : 0;
                if (h.Stock[(int)Good.Bread] < need * breadDays * 0.5f) cash -= Buy(h, purse, BuildingType.Bakehouse, Good.Bread, need * breadDays - h.Stock[(int)Good.Bread], cash * 0.6f);
                // meat and fish now and then, more often the more they can spare
                double treat = cash > 6 ? 0.6 : cash > 2 ? 0.4 : 0.15;
                if (h.Stock[(int)Good.Meat] < 0.5f && Chance(treat)) cash -= Buy(h, purse, BuildingType.Butcher, Good.Meat, need, cash * 0.5f);
                if (h.Stock[(int)Good.Fish] < 0.5f && Chance(treat)) cash -= Buy(h, purse, BuildingType.Fishpond, Good.Fish, need, cash * 0.5f);
            }
        }

        /// <summary>A household buys up to q of a good from the nearest shop that has it. Returns what it paid.</summary>
        float Buy(Building home, List<Citizen> purse, BuildingType shopType, Good g, float q, float budget)
        {
            if (q <= 0.1f || budget <= 0.05f) return 0;
            var shop = ShopFor(home, shopType, g, Math.Min(q, 1f));
            if (shop == null) return 0;
            float price = shop.Def.Price;
            q = Math.Min(Math.Min(q, shop.Stock[(int)g]), budget / price);
            if (g == Good.Cloth) q = (float)Math.Floor(q);
            if (q <= 0.05f) return 0;
            float v = q * price;
            shop.Stock[(int)g] -= q; home.Stock[(int)g] += q;
            Pay(purse, v); Takings(shop, v);
            return v;
        }

        /// <summary>Clothes wear out; a family without enough feels the winter.</summary>
        void ClothWear()
        {
            foreach (var h in Buildings.Where(x => x.Built && x.Residents.Count > 0 && IsPeasantHome(x.Type)))
                h.Stock[(int)Good.Cloth] = Math.Max(0, h.Stock[(int)Good.Cloth] - h.Residents.Count * Balance.ClothWear / Balance.DaysPerYearF);
        }
        public bool Ragged(Building home) { return home != null && IsPeasantHome(home.Type) && home.Stock[(int)Good.Cloth] < home.Residents.Count * 0.15f; }

        void CloseShopWeek() { foreach (var b in Buildings) { if (!b.Def.IsShop) continue; b.LastSales = b.WeekSales; b.WeekSales = 0; } }
    }
}
