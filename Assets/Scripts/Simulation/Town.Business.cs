using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>
    /// The trades as businesses. A new shop belongs to the lord, who runs it with hired freemen until a freeman family can pay
    /// the entry fine to take it up (a quarter down, the rest by the week). The shop keeps its own till: takings go in, supplies
    /// and hired hands' wages come out, and on Sunday the owner takes home most of what's above the float. The owner's spouse and
    /// grown children work it for no wage. Each Sunday the shop decides how many hands it wants and moves its price with its stock.
    /// </summary>
    public partial class Town
    {
        /// <summary>Shops that sell to households over the counter (and so set their own price).</summary>
        public static bool SellsToFolk(BuildingType t)
        {
            return t == BuildingType.Bakehouse || t == BuildingType.Butcher || t == BuildingType.Fishpond || t == BuildingType.Weaver || t == BuildingType.Alehouse;
        }
        /// <summary>What a shop is asking for what it sells.</summary>
        public float AskPrice(Building b) { return b.Price > 0 ? b.Price : b.Def.Price; }
        public Citizen OwnerOf(Building b) { return b == null || b.OwnerId < 0 ? null : GetCitizen(b.OwnerId); }
        public bool CrownRun(Building b) { return b.Def.IsShop && b.OwnerId < 0; }
        /// <summary>What a freeman family pays the lord to take up a shop.</summary>
        public float EntryFine(Building b) { var d = b.Def; return (float)Math.Round(Math.Max(1f, (d.Timber + d.Stone) * Balance.FinePerMaterial), 1); }
        public Building ShopOwnedBy(Citizen p) { if (p == null) return null; foreach (var b in Buildings) if (b.Def.IsShop && b.OwnerId == p.Id) return b; return null; }
        /// <summary>The shop a freeman's household holds, his or his wife's.</summary>
        public Building FamilyShop(Citizen p) { return ShopOwnedBy(p) ?? ShopOwnedBy(GetCitizen(p.PartnerId)); }
        public float WageOf(Building b) { return b.Wage > 0 ? b.Wage : Policy.FreeWage; }
        /// <summary>Hands paid a wage: not the owner, nor his family.</summary>
        public List<Citizen> HiredHands(Building b)
        {
            var l = new List<Citizen>();
            foreach (var id in b.Employees) { var p = GetCitizen(id); if (p != null && p.Id != b.OwnerId && !p.FamilyHand) l.Add(p); }
            return l;
        }
        /// <summary>What a shop keeps in its till to buy what it works and pay its hands for a few days.</summary>
        public float ShopFloat(Building b)
        {
            var d = b.Def;
            float supplies = !d.Buys.HasValue ? 0.5f : d.Buys == Good.Grain ? 2.5f : d.Buys == Good.Beasts ? 4f : 2f;
            return WageOf(b) * HiredHands(b).Count * 3 + supplies;
        }
        /// <summary>Days a household's grain, flour, bread, meat and fish will feed it.</summary>
        float FoodDays(Building h)
        {
            float f = h.Stock[(int)Good.Grain] * Balance.GrainFeeds + h.Stock[(int)Good.Flour] + h.Stock[(int)Good.Bread] + h.Stock[(int)Good.Meat] + h.Stock[(int)Good.Fish];
            return f / Math.Max(0.1f, Needs(h));
        }
        /// <summary>The shop pays for something out of its till.</summary>
        void ShopPays(Building b, float v) { if (v <= 0) return; b.Coin -= v; b.WeekCosts += v; }
        /// <summary>Coin the till can spend on supplies today.</summary>
        float ShopPurse(Building b) { return Math.Max(0, b.Coin - WageOf(b) * HiredHands(b).Count); }

        // =====================================================================
        // Taking up a shop
        // =====================================================================
        /// <summary>Each day a shop the lord runs goes to the freeman family in its lands with the most put by, if they can pay
        /// the entry fine in full or a quarter of it down. One shop to a family.</summary>
        void AssignOwners()
        {
            foreach (var b in Buildings)
            {
                if (!b.Built || !b.Def.IsShop || b.OwnerId >= 0) continue;
                int dom = DomainOf(b);
                float fine = EntryFine(b);
                Citizen buyer = null; float best = 0;
                foreach (var p in People)
                {
                    if (p.IsChild || p.Rank != Rank.Freeman || p.Age >= 60 || p.Health < 40 || p.FamilyHand) continue;
                    var home = HomeOf(p); if (home == null || DomainOf(home) != dom) continue;
                    var work = GetBuilding(p.WorkId); if (work != null && work.Type == BuildingType.MerchantHouse) continue;
                    if (FamilyShop(p) != null) continue;
                    var sp = GetCitizen(p.PartnerId);
                    if (sp != null && sp.FamilyHand) continue;
                    float savings = p.Coin + (sp != null ? sp.Coin : 0);
                    if (savings - 0.5f < fine * Balance.FineDown) continue;
                    // the man of the house takes it up, unless only his wife can work it
                    if (savings > best + 0.01f || (Math.Abs(savings - best) < 0.01f && buyer != null && buyer.Female && !p.Female)) { best = savings; buyer = p; }
                }
                if (buyer != null) TakeUp(b, buyer, best, fine);
            }
        }

        void TakeUp(Building b, Citizen buyer, float savings, float fine)
        {
            int dom = DomainOf(b);
            var payers = new List<Citizen> { buyer }; var sp = GetCitizen(buyer.PartnerId); if (sp != null) payers.Add(sp);
            payers = payers.OrderByDescending(x => x.Coin).ToList();
            float down = savings - 0.5f >= fine ? fine : Math.Min(fine, (float)Math.Round(fine * Balance.FineDown, 2));
            Pay(payers, down); PayLord(dom, down, "Entry fines for shops");
            // the lord keeps what was in the till; the family puts some of its savings in to start
            if (b.Coin > 0.01f) PayLord(dom, b.Coin, "Takings of the lord's shops");
            float stake = Math.Max(0, CoinOf(payers) - 0.3f) * Balance.OwnerStake;
            Pay(payers, stake); b.Coin = stake;
            b.OwnerId = buyer.Id; b.FineOwed = fine - down; b.MissedWeeks = 0; b.LossWeeks = 0;
            // he leaves whatever he did before
            var was = GetBuilding(buyer.WorkId); if (was != null && was != b) was.Employees.Remove(buyer.Id);
            buyer.WorkId = b.Id; buyer.FamilyHand = false;
            if (!b.Employees.Contains(buyer.Id)) b.Employees.Insert(0, buyer.Id);
            while (b.Employees.Count > b.Def.Workers)
            {
                var hand = HiredHands(b).LastOrDefault(); if (hand == null) break;
                b.Employees.Remove(hand.Id); hand.WorkId = -1;
            }
            if (b.Desired < 1) b.Desired = b.Def.Workers;
            var v = VillageOf(b);
            AddLog(buyer.Name + " took up the " + b.Def.Name + (v != null ? " in " + v.Name : "") + " from " + (dom < 0 ? "the crown" : "the baron") + ", paying " + Money(down) +
                   (b.FineOwed > 0.01f ? " of its " + Money(fine) + " entry fine. The rest is paid by the week." : ", the whole entry fine."), LogKind.Good);
        }

        /// <summary>A shop goes back to its lord, who runs it until another family takes it up.</summary>
        void LoseShop(Building b, string why)
        {
            var o = OwnerOf(b);
            foreach (var id in b.Employees.ToList())
            {
                var p = GetCitizen(id); if (p == null) continue;
                if (p.Id == b.OwnerId || p.FamilyHand) { b.Employees.Remove(id); p.WorkId = -1; p.FamilyHand = false; }
            }
            b.OwnerId = -1; b.FineOwed = 0; b.MissedWeeks = 0; b.Desired = b.Def.Workers;
            if (why != null) AddLog(why, LogKind.Bad);
            if (o != null) o.Loyalty = Math.Max(0, o.Loyalty - 10);
        }

        /// <summary>When an owner dies or leaves, his wife or a grown child working there carries it on; otherwise it goes back to the lord.</summary>
        void ShopHeirs()
        {
            foreach (var b in Buildings)
            {
                if (!b.Def.IsShop || b.OwnerId < 0) continue;
                var o = GetCitizen(b.OwnerId);
                if (o != null && o.WorkId == b.Id) continue;
                if (o != null && o.WorkId != b.Id) { o.WorkId = b.Id; if (!b.Employees.Contains(o.Id)) b.Employees.Insert(0, o.Id); continue; }   // he's still the owner: back to his shop
                var heir = b.Employees.Select(GetCitizen).FirstOrDefault(p => p != null && p.FamilyHand && !p.IsChild);
                if (heir != null)
                {
                    heir.FamilyHand = false; b.OwnerId = heir.Id;
                    AddLog(heir.Name + " carries on the family's " + b.Def.Name + ".", LogKind.Info);
                    continue;
                }
                float till = b.Coin; b.Coin = 0;
                if (till > 0.01f) PayLord(DomainOf(b), till, "Takings of the lord's shops");
                LoseShop(b, "The " + b.Def.Name + " went back to " + (DomainOf(b) < 0 ? "the crown" : "the baron") + ": nobody of the family was left to keep it.");
            }
        }

        // =====================================================================
        // The till, day by day
        // =====================================================================
        /// <summary>Morning: the lord keeps the tills of the shops he runs stocked for wages and supplies; an owner whose family has
        /// nothing in the house takes a little out of the shop, and a family that bakes, butchers or fishes eats some of its own.</summary>
        void ShopsMorning()
        {
            foreach (var b in Buildings)
            {
                if (!b.Built || !b.Def.IsShop) continue;
                if (b.OwnerId < 0)
                {
                    if (b.Employees.Count == 0) continue;
                    float need = ShopFloat(b) - b.Coin;
                    if (need < 0.05f) continue;
                    int dom = DomainOf(b);
                    float put = dom < 0 ? Math.Min(need, Math.Max(0, Coin - Policy.CoinReserve * 0.5f)) : TakeFromLord(dom, need);
                    if (dom < 0 && put > 0) { Coin -= put; WeekOut += put; Book("Running the lord's shops", -put); }
                    b.Coin += put;
                    continue;
                }
                var o = OwnerOf(b); var home = HomeOf(o);
                if (o == null || home == null) continue;
                var purse = Purse(home);
                float fl = ShopFloat(b), spare = b.Coin - fl;
                // the owner keeps the till stocked from the family's savings, keeping a little by
                if (spare < 0 && CoinOf(purse) > 2f) { float put = Math.Min(-spare, CoinOf(purse) - 2f); Pay(purse, put); b.Coin += put; }
                // and takes out what the family needs for a few days' bread when the house is short of coin
                else if (CoinOf(purse) < 1.5f && spare > 0.05f) { float t = Math.Min(spare, 1.5f - CoinOf(purse)); b.Coin -= t; o.Coin += t; }
                var g = b.Def.Sells.Value;
                if (g == Good.Bread || g == Good.Meat || g == Good.Fish)
                {
                    float want = Needs(home) * 2 - home.Stock[(int)g];
                    float t = Math.Min(Math.Max(0, want), b.Stock[(int)g] * 0.5f);
                    if (t > 0.05f) { b.Stock[(int)g] -= t; home.Stock[(int)g] += t; }
                }
            }
        }

        /// <summary>Evening: hired hands are paid their day's wage out of the till. A shop that can't pay lets them go.</summary>
        void ShopPayroll()
        {
            if (IsSunday) return;
            foreach (var b in Buildings.ToList())
            {
                if (!b.Built || !b.Def.IsShop) continue;
                foreach (var p in HiredHands(b))
                {
                    float w = WageOf(b);
                    if (b.Coin >= w) { ShopPays(b, w); p.Coin += w; continue; }
                    b.Employees.Remove(p.Id); p.WorkId = -1;
                    b.Desired = Math.Max(1, Math.Min(b.Desired < 0 ? b.Def.Workers : b.Desired, b.Employees.Count));
                    var o = OwnerOf(b);
                    AddLog((o != null ? o.Name : b.OwnerId < 0 ? (DomainOf(b) < 0 ? "The crown" : "The baron") : "The shop") + " couldn't pay " + p.Name + " at the " + b.Def.Name + " and let " + (p.Female ? "her" : "him") + " go.", LogKind.Bad);
                }
            }
        }

        // =====================================================================
        // Sunday: the week's accounts
        // =====================================================================
        void ShopsWeekly()
        {
            foreach (var b in Buildings.ToList())
            {
                if (b.Type == BuildingType.MerchantHouse) { b.LastSales = b.WeekSales; b.WeekSales = 0; continue; }
                if (!b.Def.IsShop) continue;
                var d = b.Def; int dom = DomainOf(b);
                b.LastProfit = b.WeekSales - b.WeekCosts;
                b.LastSales = b.WeekSales; b.WeekSales = 0; b.WeekCosts = 0;
                if (!b.Built) continue;
                // the float kept back: wages for the hands it wants and its makings
                int famHands = b.Employees.Count(id => { var c = GetCitizen(id); return c != null && (c.Id == b.OwnerId || c.FamilyHand); });
                float reserve = Math.Max(ShopFloat(b) * 1.5f, WageOf(b) * Math.Max(0, (b.Desired < 0 ? b.Def.Workers : b.Desired) - famHands) * 5 + ShopFloat(b));
                var o = OwnerOf(b);
                if (o != null)
                {
                    // the entry fine, a week's share of it
                    if (b.FineOwed > 0.01f)
                    {
                        float due = Math.Min(b.FineOwed, EntryFine(b) * (1 - Balance.FineDown) / Balance.FineWeeks);
                        float fromTill = Math.Min(due, Math.Max(0, b.Coin - ShopFloat(b) * 0.5f));
                        b.Coin -= fromTill;
                        var purse = Purse(HomeOf(o)); float fromHome = Math.Min(due - fromTill, Math.Max(0, CoinOf(purse) - 0.5f));
                        Pay(purse, fromHome);
                        float paid = fromTill + fromHome;
                        b.FineOwed -= paid; PayLord(dom, paid, "Entry fines for shops");
                        if (b.FineOwed <= 0.01f) { b.FineOwed = 0; b.MissedWeeks = 0; AddLog(o.Name + " has paid off the entry fine on the " + d.Name + ".", LogKind.Good); }
                        else if (paid >= due * 0.75f) b.MissedWeeks = 0;
                        else if (++b.MissedWeeks >= 4)
                        {
                            float till = b.Coin; b.Coin = 0; PayLord(dom, till, "Takings of the lord's shops");
                            LoseShop(b, (dom < 0 ? "The crown" : "The baron") + " took the " + d.Name + " back from " + o.Name + ": four weeks behind on the entry fine.");
                            o = null;
                        }
                        else AddLog(o.Name + " fell behind on the entry fine for the " + d.Name + ".", LogKind.Bad);
                    }
                    // the owner takes home most of what's above the float
                    if (o != null && b.Coin > reserve)
                    {
                        float draw = (b.Coin - reserve) * Balance.OwnerDraw;
                        b.Coin -= draw; o.Coin += draw;
                    }
                }
                else if (b.Coin > reserve)
                {
                    // the lord's own shop: the takings go to him
                    float sweep = b.Coin - reserve; b.Coin -= sweep;
                    PayLord(dom, sweep, "Takings of the lord's shops");
                }
                if (b.LastProfit >= 0) b.LossWeeks = 0; else b.LossWeeks++;
                if (b.Wage <= 0) b.Wage = Policy.FreeWage;
                // hands for next week, and letting go who isn't needed: hired hands first, then family
                if (Day - b.BuiltDay >= 6)
                {
                    b.Desired = StaffingDecision(b);
                    while (b.Employees.Count > b.Desired)
                    {
                        var p = HiredHands(b).LastOrDefault() ?? b.Employees.Select(GetCitizen).LastOrDefault(x => x != null && x.Id != b.OwnerId);
                        if (p == null) break;
                        bool fam = p.FamilyHand;
                        b.Employees.Remove(p.Id); p.WorkId = -1; p.FamilyHand = false;
                        AddLog(fam ? p.Name + " left the family's " + d.Name.ToLower() + " to look for paid work: there isn't enough to do."
                                   : (o != null ? o.Name : dom < 0 ? "The crown" : "The baron") + " let " + p.Name + " go at the " + d.Name + ". Trade is slow.", LogKind.Info);
                    }
                }
                // the price follows the stock: up when it sells out, down when it piles up
                if (SellsToFolk(b.Type))
                {
                    float fill = b.Stock[(int)d.Sells.Value] / d.Cap, price = AskPrice(b), step = d.Price * Balance.PriceStep;
                    if (fill < 0.2f && b.LastSales > 0.01f) price += step;
                    else if (fill > 0.6f) price -= step;
                    b.Price = (float)Math.Round(Math.Min(d.Price * Balance.PriceHigh, Math.Max(d.Price * Balance.PriceLow, price)), 3);
                }
            }
        }

        /// <summary>How many hands a shop wants next week: more while what it makes sells and the till can pay, fewer when stock
        /// piles up, money runs short or there's nothing to work with. The owner always counts as one.</summary>
        int StaffingDecision(Building b)
        {
            var d = b.Def;
            int n = Math.Max(1, b.Employees.Count), max = d.Workers;
            bool lord = b.OwnerId < 0;
            float w = WageOf(b);
            int hired = HiredHands(b).Count;
            bool canPay = lord ? (DomainOf(b) < 0 ? Coin > Policy.CoinReserve : LordPurse(DomainOf(b)) > w * 7) : b.Coin > w * (hired + 1) * 4;
            if (!lord && hired > 0 && b.Coin < w * hired * 2) return Math.Max(1, n - 1);
            float fill = b.Stock[(int)d.Sells.Value] / d.Cap;
            if (d.Buys.HasValue)
            {
                bool makings = b.Stock[(int)d.Buys.Value] > 0.5f || b.Type == BuildingType.Bakehouse && WorkingMill != null || b.Type == BuildingType.Alehouse;
                if ((!lord && b.LastProfit < 0 && b.LossWeeks >= 2) || fill > 0.85f) return Math.Max(1, n - 1);
                if (fill < 0.4f && makings && canPay) return Math.Min(max, n + 1);
                return Math.Min(max, n);
            }
            if (fill < 0.5f && canPay) return Math.Min(max, n + 1);
            if (fill > 0.9f) return Math.Max(1, n - 1);
            return Math.Min(max, n);
        }
    }
}
