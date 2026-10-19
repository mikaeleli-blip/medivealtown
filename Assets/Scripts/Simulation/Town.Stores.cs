using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>
    /// Goods are local. What a village makes is kept in its barn, and its families and shops buy there first; a village with no
    /// barn draws on its lord's granary: the baron's manor in a barony, the royal granary in the crown's own villages. Folk only
    /// shop within their own barony (or the crown's lands), so a barony can run short of what it doesn't make. When grain runs
    /// short a village petitions the king; and each barony's merchant trades its surplus at the capital's market for what it lacks.
    /// </summary>
    public partial class Town
    {
        // =====================================================================
        // Whose land, whose stores
        // =====================================================================
        /// <summary>A lord's domain: a barony's id, or -1 for the crown's own lands (and anything outside every village).</summary>
        public int DomainOf(Village v) { return v == null ? -1 : v.BaronyId; }
        public int DomainOf(Building b) { return DomainOf(VillageOf(b)); }
        public int DomainOf(Citizen p) { return DomainOf(VillageOf(p)); }
        public string DomainName(int domain) { var b = GetBarony(domain); return b == null ? "the crown's lands" : BaronyName(b); }

        public IEnumerable<Building> BarnsOf(Village v) { return v == null ? Enumerable.Empty<Building>() : Buildings.Where(b => b.Built && b.Type == BuildingType.VillageBarn && b.VillageId == v.Id); }

        /// <summary>The lord's granary for a domain: the baron's manor, or the royal stores.</summary>
        IEnumerable<Building> LordStores(int domain)
        {
            var bar = GetBarony(domain);
            if (bar == null) return Stores;
            var manor = GetBuilding(bar.ManorId);
            return manor != null ? new[] { manor } : Enumerable.Empty<Building>();
        }

        /// <summary>Where a village keeps and finds its goods: its own barn, then its lord's granary. The capital's barn is the royal granary.</summary>
        IEnumerable<Building> LocalStores(Village v)
        {
            if (v == null || v.Capital) return Stores;
            return BarnsOf(v).Concat(LordStores(DomainOf(v)));
        }

        /// <summary>What a baron's granary holds back for his own household: a year's bread. His villagers can buy what's above it.</summary>
        float ManorKeep(Building manor, Good g)
        {
            if (manor.Type != BuildingType.Manor || (g != Good.Grain && g != Good.Flour)) return 0;
            float n = 0; foreach (var id in manor.Residents) { var p = GetCitizen(id); if (p != null) n += p.IsChild ? Balance.ChildAppetite : Balance.Appetite; }
            return g == Good.Grain ? n * Balance.DaysPerYear / Balance.GrainFeeds : 0;
        }

        // =====================================================================
        // The crown's stockpile
        // =====================================================================
        /// <summary>What the court eats in a day: the royal family and knights, the steward and his collector, a priest with no church.</summary>
        public float CourtNeeds
        {
            get
            {
                float n = 0;
                foreach (var p in People)
                    if (RoyalHousehold(p) || p.Rank == Rank.Steward || p.Id == Crown.CollectorId || (p.Rank == Rank.Priest && Church == null)) n += p.IsChild ? Balance.ChildAppetite : Balance.Appetite;
                return n;
            }
        }
        /// <summary>A year of the court's bread, which the royal granary never sells or gives away.</summary>
        public float CourtYear { get { return CourtNeeds * Balance.DaysPerYear / (WorkingMill != null ? 1f : Balance.GrainFeeds); } }
        /// <summary>The famine store: so many days' grain for every family in a domain, kept for bad years, petitions and the dole.</summary>
        public float FamineStore(int domain)
        {
            float n = 0;
            foreach (var h in Buildings) if (h.Built && IsPeasantHome(h.Type) && h.Residents.Count > 0 && DomainOf(h) == domain) n += Needs(h);
            return n * Policy.FamineDays / Balance.GrainFeeds;
        }
        [NonSerialized] int targetDay = -1; [NonSerialized] float targetCache, yearCache;
        void CacheTargets() { if (targetDay == Day) return; targetDay = Day; yearCache = CourtYear; targetCache = yearCache + FamineStore(-1); }
        /// <summary>What the royal granary aims to hold: a year for the court and the famine store for the crown's villages.</summary>
        public float CrownTarget { get { CacheTargets(); return targetCache; } }
        /// <summary>Below this the crown buys grain from outside at the merchant's price: when the court can't eat until the harvest.</summary>
        public float CrownFloor { get { return Math.Max(0, HouseholdKeep) + 30; } }

        /// <summary>Grain spoils a little in store, so hoarding isn't free.</summary>
        void Spoilage()
        {
            foreach (var b in Buildings)
                if (b.Built && (b.Type == BuildingType.Granary || b.Type == BuildingType.VillageBarn || b.Type == BuildingType.Manor || b.Type == BuildingType.Cart))
                { b.Stock[(int)Good.Grain] *= 1f - Balance.GrainSpoil; b.Stock[(int)Good.Flour] *= 1f - Balance.GrainSpoil * 2; }
        }
        float Avail(Building b, Good g, bool lords)
        {
            if (lords) return b.Stock[(int)g];
            // the royal granary holds back the court's own bread until harvest
            if (b.Type == BuildingType.Granary && (g == Good.Grain || g == Good.Flour) && b == Stores.FirstOrDefault()) return Math.Max(0, b.Stock[(int)g] - (g == Good.Grain ? CourtHold : 0));
            return Math.Max(0, b.Stock[(int)g] - ManorKeep(b, g));
        }
        [NonSerialized] int courtHoldDay = -1; [NonSerialized] float courtHold;
        /// <summary>Grain the royal granary won't sell or give to its own people: what the court eats until the harvest, and a little over.
        /// (Everything above the full stockpile target is what merchants may buy.)</summary>
        float CourtHold { get { if (courtHoldDay != Day) { courtHoldDay = Day; courtHold = Math.Max(0, HouseholdKeep) + 30; } return courtHold; } }

        public float LocalStock(Village v, Good g, bool lords = false) { float s = 0; foreach (var b in LocalStores(v)) s += Avail(b, g, lords); return s; }
        public float TakeLocal(Village v, Good g, float q, bool lords = false)
        {
            float got = 0;
            foreach (var b in LocalStores(v).OrderBy(b => b.Type == BuildingType.Cart ? 0 : 1))
            {
                if (got >= q) break;
                float t = Math.Min(Avail(b, g, lords), q - got);
                b.Stock[(int)g] -= t; got += t;
            }
            return got;
        }
        public void PutLocal(Village v, Good g, float q)
        {
            if (q <= 0) return;
            if (v == null || v.Capital) { PutInStores(g, q); return; }
            var dest = BarnsOf(v).FirstOrDefault() ?? LordStores(DomainOf(v)).FirstOrDefault();
            if (dest == null) { PutInStores(g, q); return; }
            dest.Stock[(int)g] += q;
        }

        /// <summary>Materials for a building site: from its own village's stores, then from the crown's (the king builds everything).</summary>
        float TakeForSite(Building site, Good g, float q)
        {
            var v = VillageOf(site);
            float got = v != null && !v.Capital ? TakeLocal(v, g, q) : 0;
            if (got < q) got += TakeFromStores(g, q - got);
            return got;
        }
        float StockForSite(Building site, Good g) { var v = VillageOf(site); return (v != null && !v.Capital ? LocalStock(v, g) : 0) + StoreStock(g); }

        /// <summary>The lord of a domain is paid for something sold from his granary.</summary>
        void PayLord(int domain, float v, string what)
        {
            if (v <= 0) return;
            var bar = GetBarony(domain);
            if (bar == null) { Coin += v; WeekIn += v; Book(what, v); return; }
            var baron = GetCitizen(bar.BaronId);
            if (baron != null) baron.Coin += v;
            else { var manor = GetBuilding(bar.ManorId); if (manor != null) manor.Coin += v; }
        }

        /// <summary>The going price of a good, a measure of it.</summary>
        public static float PriceOf(Good g)
        {
            switch (g)
            {
                case Good.Grain: return 0.25f;
                case Good.Flour: return 0.3f;
                case Good.Bread: return Defs.Get(BuildingType.Bakehouse).Price;
                case Good.Meat: return Defs.Get(BuildingType.Butcher).Price;
                case Good.Fish: return Defs.Get(BuildingType.Fishpond).Price;
                case Good.Cloth: return Defs.Get(BuildingType.Weaver).Price;
                case Good.Ale: return Balance.AlePrice;
                case Good.Wool: return Balance.WoolPrice;
                case Good.Beasts: return Balance.BeastPrice;
                case Good.Timber: return Balance.BuyTimber;
                case Good.Stone: return Balance.BuyStone;
                default: return 0.05f;
            }
        }
        /// <summary>Room a unit takes on a merchant's cart.</summary>
        static float Bulk(Good g) { return g == Good.Cloth || g == Good.Beasts ? 5f : 1f; }

        // =====================================================================
        // What a village wants
        // =====================================================================
        /// <summary>A family wanted something and couldn't buy it anywhere in its barony.</summary>
        void Want(Building home, Good g, float q)
        {
            var v = VillageOf(home); if (v == null || q <= 0) return;
            if (v.Wants == null || v.Wants.Length != Balance.GoodCount) v.Wants = Fit(v.Wants);
            v.Wants[(int)g] += q;
        }
        /// <summary>What a domain as a whole wanted last week.</summary>
        public float DomainWants(int domain, Good g) { float n = 0; foreach (var v in Villages) if (DomainOf(v) == domain && v.Wants != null && v.Wants.Length > (int)g) n += v.Wants[(int)g]; return n; }

        /// <summary>Days of grain a village has in its barn, its lord's granary (its share) and its families' pantries.</summary>
        public float GrainDays(Village v)
        {
            float need = 0, pantry = 0;
            foreach (var h in HousesOf(v)) { need += Needs(h); pantry += h.Stock[(int)Good.Grain] * Balance.GrainFeeds + h.Stock[(int)Good.Flour] + h.Stock[(int)Good.Bread]; }
            if (need <= 0) return 999;
            float barn = BarnsOf(v).Sum(b => b.Stock[(int)Good.Grain] + b.Stock[(int)Good.Flour]);
            return (pantry + barn) / need;
        }

        [NonSerialized] int purveyLogDay = -999;
        /// <summary>Purveyance: when the lord's own table is bare, his men take grain from his villagers' pantries for a pittance.
        /// Folk hate it. Returns how much of the need it met.</summary>
        float Purvey(int domain, float need)
        {
            float want = need / Balance.GrainFeeds, got = 0;
            foreach (var h in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && DomainOf(b) == domain).OrderByDescending(b => b.Stock[(int)Good.Grain] / Math.Max(0.5f, Needs(b))))
            {
                if (got >= want) break;
                float spare = h.Stock[(int)Good.Grain] - Needs(h) * 10 / Balance.GrainFeeds;
                if (spare < 1) continue;
                float t = Math.Min(spare, want - got);
                h.Stock[(int)Good.Grain] -= t; got += t;
                var head = HeadOf(h); float pay = t * Balance.VillageGrain * 0.5f;
                if (domain < 0 && Coin >= pay) { Coin -= pay; WeekOut += pay; Book("Purveyance", -pay); if (head != null) head.Coin += pay; }
                foreach (var id in h.Residents) { var p = GetCitizen(id); if (p != null && !p.IsChild) p.Loyalty = Clamp(p.Loyalty - 0.5f, 0, 100); }
            }
            if (got > 0 && Day - purveyLogDay > 6) { purveyLogDay = Day; AddLog((domain < 0 ? "The king's purveyors" : "The baron's men") + " took grain from the villagers' own stores for the " + (domain < 0 ? "court" : "manor") + "'s table. The granary is empty.", LogKind.Bad); }
            return got * Balance.GrainFeeds;
        }

        /// <summary>A baron whose granary won't feed his household until the harvest buys grain from the king's granary.</summary>
        void BaronsProvision()
        {
            foreach (var bar in Baronies)
            {
                var manor = GetBuilding(bar.ManorId); var baron = GetCitizen(bar.BaronId);
                if (manor == null || baron == null) continue;
                float shortBy = ManorKeep(manor, Good.Grain) * 0.4f + 20 - manor.Stock[(int)Good.Grain];
                if (shortBy < 5) continue;
                float q = Math.Min(Math.Min(shortBy, Math.Max(0, StoreStock(Good.Grain) - HomeKeep)), baron.Coin / Policy.GrainPrice);
                if (q < 3) continue;
                q = TakeFromStores(Good.Grain, q);
                manor.Stock[(int)Good.Grain] += q; baron.Coin -= q * Policy.GrainPrice;
                Coin += q * Policy.GrainPrice; WeekIn += q * Policy.GrainPrice; Book("Grain sold to barons", q * Policy.GrainPrice);
            }
        }

        void WeeklyWants() { foreach (var v in Villages) { if (v.Wants == null || v.Wants.Length != Balance.GoodCount) v.Wants = Fit(v.Wants); for (int g = 0; g < Balance.GoodCount; g++) v.Wants[g] *= 0.5f; } }

        // =====================================================================
        // Petitions for grain
        // =====================================================================
        /// <summary>A village whose families are going hungry before the harvest sends to the king for grain.</summary>
        void Petitions()
        {
            foreach (var v in Villages)
            {
                if (v.Capital) continue;
                if (v.PetitionDay >= 0)
                {
                    if (Day - v.PetitionDay >= Balance.PetitionDays) { RefusePetition(v, true); }
                    continue;
                }
                if (Day - v.LastPetition < 14) continue;
                var houses = HousesOf(v).ToList(); if (houses.Count == 0) continue;
                int hungry = houses.Sum(h => h.Residents.Count(id => { var p = GetCitizen(id); return p != null && p.Fed < 0.7f; }));
                float days = GrainDays(v);
                if (hungry == 0 && (days > 6 || DaysUntilHarvest < 5)) continue;
                float need = houses.Sum(Needs);
                v.PetitionGrain = (float)Math.Ceiling(need * Math.Min(DaysUntilHarvest + 3, 21) / Balance.GrainFeeds);
                v.PetitionDay = Day; v.LastPetition = Day;
                AddLog("The reeve of " + v.Name + " has come to court to beg the king for grain: " + Math.Round(v.PetitionGrain) + " measures to see them to the harvest.", LogKind.Bad);
            }
        }

        public IEnumerable<Village> PendingPetitions { get { return Villages.Where(v => v.PetitionDay >= 0); } }

        /// <summary>The king sends the grain from the royal granary at once. The village won't forget it.</summary>
        public void GrantPetition(Village v)
        {
            if (v == null || v.PetitionDay < 0) return;
            float q = Math.Min(v.PetitionGrain, Math.Max(0, StoreStock(Good.Grain) - CourtHold * 0.5f));   // the famine store is for this; the court keeps enough to eat
            q = TakeFromStores(Good.Grain, q);
            var houses = HousesOf(v).ToList();
            float need = houses.Sum(Needs);
            foreach (var h in houses) h.Stock[(int)Good.Grain] += need > 0 ? q * Needs(h) / need : q / houses.Count;
            foreach (var p in People.Where(p => !p.IsChild && VillageOf(p) == v)) p.Loyalty = Clamp(p.Loyalty + (q >= v.PetitionGrain * 0.8f ? 10 : 5), 0, 100);
            AddLog("The king sent " + Math.Round(q) + " measures of grain to " + v.Name + (q < v.PetitionGrain * 0.8f ? ", all the granary could spare." : ". The village blesses his name."), LogKind.Crown);
            v.PetitionDay = -1;
        }

        public void RefusePetition(Village v, bool unanswered = false)
        {
            if (v == null || v.PetitionDay < 0) return;
            foreach (var p in People.Where(p => !p.IsChild && VillageOf(p) == v)) p.Loyalty = Clamp(p.Loyalty - (unanswered ? 5 : 8), 0, 100);
            AddLog(unanswered ? "No answer came from court to " + v.Name + "'s plea for grain. They'll remember it." : "The king refused " + v.Name + "'s plea for grain.", LogKind.Bad);
            v.PetitionDay = -1;
        }

        // =====================================================================
        // The barony merchants
        // =====================================================================
        static readonly Good[] Tradeable = { Good.Grain, Good.Cloth, Good.Wool, Good.Beasts, Good.Fish, Good.Meat };

        public Building MerchantHouseOf(Barony b) { return Buildings.FirstOrDefault(x => x.Built && x.Type == BuildingType.MerchantHouse && DomainOf(x) == b.Id); }
        public Citizen MerchantOf(Building house) { return house == null || house.Employees.Count == 0 ? null : GetCitizen(house.Employees[0]); }

        /// <summary>What a domain has to spare of a good: in its lord's granary and barns above what it needs to keep, in its families'
        /// pantries above their keep (grain), or in its shops above half their stock.</summary>
        float Surplus(int domain, Good g)
        {
            float s = 0;
            if (g == Good.Grain)
            {
                float keep = 0;
                foreach (var h in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && DomainOf(b) == domain)) s += FamilySpare(h);
                return s + LordSpare(domain);
            }
            if (DomainWants(domain, g) > 1) return 0;   // its own folk want it: none to spare
            foreach (var shop in Buildings.Where(b => b.Built && b.Def.IsShop && b.Def.Sells == g && DomainOf(b) == domain)) s += ShopSpare(shop, g);
            return s;
        }

        /// <summary>A family only sells to a merchant out of a big surplus: more than twice what it needs to keep.</summary>
        float FamilySpare(Building h) { return Math.Max(0, h.Stock[(int)Good.Grain] - GrainKeep(h) * 2 - 20); }
        /// <summary>A shop only sells to a merchant what its own village won't buy: it keeps two weeks of what it sells, and a third of its stock.</summary>
        float ShopSpare(Building shop, Good g)
        {
            float price = shop.Def.Price > 0 ? shop.Def.Price : PriceOf(g);
            float keep = Math.Max(shop.Def.Cap * 0.3f, shop.LastSales / price * 2);
            return Math.Max(0, shop.Stock[(int)g] - keep);
        }
        /// <summary>Grain a lord will sell to merchants: what's above his household's year and his villages' famine store.</summary>
        float LordSpare(int domain)
        {
            if (domain < 0) return CrownSpareGrain;
            return Math.Max(0, StoresIn(domain).Sum(b => Avail(b, Good.Grain, false)) - FamineStore(domain));
        }

        /// <summary>Grain the royal granary can spare to merchants: above what the court and the freemen need till harvest, and half the reserve.</summary>
        public float CrownSpareGrain { get { return Math.Max(0, StoreStock(Good.Grain) - CrownTarget); } }

        /// <summary>All the granaries of a domain: the lord's and the village barns.</summary>
        IEnumerable<Building> StoresIn(int domain)
        {
            var barns = Buildings.Where(b => b.Built && b.Type == BuildingType.VillageBarn && DomainOf(b) == domain);
            return LordStores(domain).Concat(barns);
        }

        /// <summary>What a domain will pay for a good this week: how much it wanted and couldn't get, and for grain, how short it is.</summary>
        float Demand(int domain, Good g)
        {
            float d = DomainWants(domain, g);
            if (g == Good.Grain)
            {
                // villages running low, and the lord's own granary short of what his household needs
                foreach (var v in Villages.Where(x => DomainOf(x) == domain)) { float days = GrainDays(v); if (days < 7) d += HousesOf(v).Sum(Needs) * (10 - days); }
                if (domain < 0) d += Math.Max(0, CourtHold - StoreStock(Good.Grain));
                else { var bar = GetBarony(domain); var manor = bar != null ? GetBuilding(bar.ManorId) : null; if (manor != null) d += Math.Max(0, ManorKeep(manor, Good.Grain) * 0.4f + 20 - manor.Stock[(int)Good.Grain]); }
            }
            return d;
        }

        /// <summary>Takes up to q of a domain's spare goods, a little from each seller rather than emptying the first, paying whoever held
        /// them. Families sell only big surpluses, shops only what their own village won't buy, and the lord's granary last.</summary>
        float BuyFrom(int domain, Good g, float q, Citizen buyer, float unitPrice)
        {
            var sellers = new List<KeyValuePair<float, Action<float>>>();   // spare, and how to take an amount of it
            if (g == Good.Grain)
                foreach (var h0 in Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && DomainOf(b) == domain).ToList())
                {
                    var h = h0; float spare = FamilySpare(h); if (spare < 2) continue;
                    sellers.Add(new KeyValuePair<float, Action<float>>(spare, t => { h.Stock[(int)Good.Grain] -= t; var seller = HeadOf(h); if (seller != null) seller.Coin += t * unitPrice; }));
                }
            else if (DomainWants(domain, g) <= 1)
                foreach (var s0 in Buildings.Where(b => b.Built && b.Def.IsShop && b.Def.Sells == g && DomainOf(b) == domain).ToList())
                {
                    var shop = s0; float spare = ShopSpare(shop, g); if (spare < 0.2f) continue;
                    sellers.Add(new KeyValuePair<float, Action<float>>(spare, t => { shop.Stock[(int)g] -= t; Takings(shop, t * unitPrice); }));
                }
            float got = 0;
            float total = sellers.Sum(x => x.Key);
            float want = Math.Min(q, buyer.Coin / unitPrice);
            if (total > 0 && want > 0)
            {
                // the same share from each
                float frac = Math.Min(1f, want / total);
                foreach (var s1 in sellers)
                {
                    float t = s1.Key * frac; if (g == Good.Cloth || g == Good.Beasts) t = (float)Math.Floor(t + 0.001f);
                    if (t <= 0.01f) continue;
                    s1.Value(t); got += t; buyer.Coin -= t * unitPrice;
                }
            }
            // the lord's granary sells last, and only from what's above his stockpile
            if (g == Good.Grain && got < q && buyer.Coin >= unitPrice)
            {
                float t = Math.Min(Math.Min(LordSpare(domain), q - got), buyer.Coin / unitPrice);
                if (t > 0)
                {
                    float taken = domain < 0 ? TakeFromStores(Good.Grain, t) : TakeFromAvail(StoresIn(domain), Good.Grain, t);
                    got += taken; buyer.Coin -= taken * unitPrice;
                    PayLord(domain, taken * unitPrice, "Grain sold to merchants");
                }
            }
            return got;
        }
        float TakeFromAvail(IEnumerable<Building> from, Good g, float q)
        {
            float got = 0;
            foreach (var b in from) { if (got >= q) break; float t = Math.Min(Avail(b, g, false), q - got); b.Stock[(int)g] -= t; got += t; }
            return got;
        }

        /// <summary>Sells cargo into a domain: to the shops that use it or sell it on (wholesale), to the lord's granary for grain it's
        /// short of, and what's left stays in the merchant's house for families to buy. Returns coin taken.</summary>
        float SellInto(int domain, Building house, Citizen merchant, Good g)
        {
            float have = house.Stock[(int)g]; if (have <= 0.01f) return 0;
            float price = PriceOf(g) * Balance.MerchantWholesale, earned = 0;
            // shops that need it as their stuff: bakers and brewers take grain, the weaver wool, the butcher beasts; and shops that sell it
            var buyers = Buildings.Where(b => b.Built && b.Def.IsShop && b.Employees.Count > 0 && DomainOf(b) == domain && (b.Def.Buys == g || b.Def.Sells == g)).ToList();
            foreach (var shop in buyers)
            {
                if (have <= 0.01f) break;
                var staff = Staff(shop); float purse = Math.Max(0, CoinOf(staff) - 0.5f);
                float room = shop.Def.Buys == g ? (g == Good.Grain ? 60 : g == Good.Beasts ? 3 : 15) - shop.Stock[(int)g] : shop.Def.Cap - shop.Stock[(int)g];
                float t = Math.Min(Math.Min(have, room), purse / price);
                if (t <= 0.05f) continue;
                shop.Stock[(int)g] += t; have -= t; Pay(staff, t * price); earned += t * price;
            }
            // the lord's granary buys grain it's short of
            if (g == Good.Grain && have > 0.5f)
            {
                float shortBy = Demand(domain, Good.Grain) - DomainWants(domain, Good.Grain) + DomainWants(domain, Good.Grain) * 0.5f;
                var bar = GetBarony(domain); var baron = bar != null ? GetCitizen(bar.BaronId) : null;
                float purse = domain < 0 ? Math.Max(0, Coin - Policy.CoinReserve * 0.5f) : baron != null ? baron.Coin : 0;
                float t = Math.Min(Math.Min(have, shortBy), purse / price);
                if (t > 0.5f)
                {
                    if (domain < 0) { PutInStores(Good.Grain, t); Coin -= t * price; WeekOut += t * price; Book("Grain bought from merchants", -t * price); }
                    else { var dest = StoresIn(domain).FirstOrDefault(); if (dest != null) dest.Stock[(int)Good.Grain] += t; baron.Coin -= t * price; }
                    have -= t; earned += t * price;
                }
            }
            house.Stock[(int)g] = have;
            merchant.Coin += earned;
            if (earned > 0) { var home = HomeOf(merchant); float toll = earned * Policy.MarketToll; if (home != null && home.VillageId >= 0 && toll > 0) { Assess(home, toll, 0); Taxes.Tolls += toll; } house.WeekSales += earned; }
            return earned;
        }

        /// <summary>The merchant's week. Monday he buys what his barony has too much of and the capital wants; Tuesday he takes the road;
        /// Wednesday he sells at the capital's market and buys what his barony is short of; Thursday he comes home; Friday and market
        /// day he sells it on. He trades on his own purse and only buys what he expects to sell at a profit.</summary>
        void Merchants()
        {
            var capital = Capital;
            foreach (var bar in Baronies)
            {
                var house = MerchantHouseOf(bar); var m = MerchantOf(house);
                if (house == null || m == null || m.Health < 30 || capital == null) continue;
                if (house.Stock == null || house.Stock.Length != Balance.GoodCount) house.Stock = Fit(house.Stock);
                var seat = GetVillage(bar.SeatVillageId);
                int wd = WeekdayIndex;
                // he trades with the house's purse, not his family's
                float personal = m.Coin; m.Coin = house.Coin;
                try {
                if (wd == 0)
                {
                    // load up with what home has to spare and the capital will take
                    float room = Balance.CartLoad - Load(house), spent = 0; var note = new List<string>();
                    foreach (var g in Tradeable.OrderByDescending(x => Demand(-1, x) * PriceOf(x)))
                    {
                        if (room < Bulk(g) || m.Coin < 1) break;
                        float want = Demand(-1, g) * 1.2f - house.Stock[(int)g];
                        if (g == Good.Meat || g == Good.Fish) want = Math.Min(want, 20);   // it won't keep long on the road
                        float spare = Surplus(bar.Id, g);
                        float q = Math.Min(Math.Min(want, spare), room / Bulk(g));
                        if (q < (g == Good.Grain ? 10 : 1)) continue;
                        float before = m.Coin;
                        float got = BuyFrom(bar.Id, g, q, m, PriceOf(g) * Balance.MerchantBuys);
                        house.Stock[(int)g] += got; room -= got * Bulk(g); spent += before - m.Coin; Tally("to the capital: " + Defs.GoodNames[(int)g].ToLower(), got);
                        if (got > 0.5f) note.Add(Math.Round(got) + " " + Defs.GoodNames[(int)g].ToLower());
                    }
                    if (note.Count > 0) { seat.MerchantNote = m.Name + " loaded " + string.Join(", ", note.ToArray()) + " for the capital."; }
                }
                else if (wd == 2)
                {
                    // the capital's market: sell, then buy what home is short of
                    float earned = 0; foreach (var g in Tradeable) earned += SellInto(-1, house, m, g);
                    float room = Balance.CartLoad - Load(house); var note = new List<string>();
                    foreach (var g in Tradeable.OrderByDescending(x => Demand(bar.Id, x) * PriceOf(x)))
                    {
                        if (room < Bulk(g) || m.Coin < 1) break;
                        float want = Demand(bar.Id, g) - house.Stock[(int)g];
                        if (g == Good.Meat || g == Good.Fish) want = Math.Min(want, 20);
                        float spare = Surplus(-1, g);
                        float q = Math.Min(Math.Min(want, spare), room / Bulk(g));
                        if (q < (g == Good.Grain ? 10 : 1)) continue;
                        float got = BuyFrom(-1, g, q, m, PriceOf(g) * Balance.MerchantBuys);
                        house.Stock[(int)g] += got; room -= got * Bulk(g); Tally("home to the barony: " + Defs.GoodNames[(int)g].ToLower(), got);
                        if (got > 0.5f) note.Add(Math.Round(got) + " " + Defs.GoodNames[(int)g].ToLower());
                    }
                    if (earned > 0.05f || note.Count > 0)
                        seat.MerchantNote = m.Name + " sold " + Money(earned) + " of goods at the capital's market" + (note.Count > 0 ? " and is bringing home " + string.Join(", ", note.ToArray()) + "." : ".");
                }
                else if (wd == 4 || wd == 5)
                {
                    float earned = 0; foreach (var g in Tradeable) earned += SellInto(bar.Id, house, m, g);
                    if (earned > 0.05f) seat.MerchantNote = m.Name + " sold " + Money(earned) + " of goods from the capital in " + DomainName(bar.Id) + ".";
                }
                // what won't keep spoils on the cart
                house.Stock[(int)Good.Meat] *= 0.9f; house.Stock[(int)Good.Fish] *= 0.85f;
                } finally { house.Coin = m.Coin; m.Coin = personal; }
                // on Sunday he takes half of what he's made above his stake to live on
                if (IsSunday && house.Coin > Balance.MerchantStake) { float take = (house.Coin - Balance.MerchantStake) * 0.5f; house.Coin -= take; m.Coin += take; }
            }
        }
        float Load(Building house) { float l = 0; foreach (var g in Tradeable) l += house.Stock[(int)g] * Bulk(g); return l; }

        /// <summary>A merchant's house that's just been finished: the merchant sets up with his savings.</summary>
        void MerchantSetsUp(Building house)
        {
            house.Coin = Balance.MerchantStake;   // handed to the merchant when he takes it up (see Hire)
        }
    }
}
