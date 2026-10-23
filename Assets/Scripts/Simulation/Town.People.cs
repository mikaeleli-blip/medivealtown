using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    public partial class Town
    {
        // =====================================================================
        // Households
        // =====================================================================
        /// <summary>Who someone lives with: a couple and their children, with unmarried grown children staying on with their parents.
        /// Returns the id of the household's head.</summary>
        public int HouseholdOf(Citizen p)
        {
            for (int guard = 0; guard < 6 && p != null; guard++)
            {
                if (RoyalHousehold(p)) return RoyalKey;
                if (IsOfficer(p)) return p.Id;
                var partner = GetCitizen(p.PartnerId);
                if (partner != null) return p.Female ? partner.Id : p.Id;
                // a widowed elder lives with a married son or daughter
                if (p.Age >= 50)
                {
                    var kid = People.FirstOrDefault(c => (c.MotherId == p.Id || c.FatherId == p.Id) && !c.IsChild && c.PartnerId >= 0 && !RoyalHousehold(c) && !IsOfficer(c));
                    if (kid != null) return kid.Female ? kid.PartnerId : kid.Id;
                }
                // unmarried: with a parent if there is one in the village
                var parent = GetCitizen(p.MotherId) ?? GetCitizen(p.FatherId);
                if (parent != null && !RoyalHousehold(parent) && !IsOfficer(parent) && (p.IsChild || p.Age < 24)) { p = parent; continue; }
                return p.Id;
            }
            return p != null ? p.Id : -1;
        }
        public const int RoyalKey = -2;

        public Citizen HeadOf(Building home)
        {
            if (home == null) return null;
            var folk = home.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild).ToList();
            return folk.OrderBy(p => p.Female ? 1 : 0).ThenByDescending(p => p.PartnerId >= 0 ? 1 : 0).ThenByDescending(p => p.Age).FirstOrDefault();
        }

        public Building HomeOf(Citizen p) { return p == null ? null : GetBuilding(p.HomeId); }

        /// <summary>Puts everyone under a roof that suits their station: the lord's household in the keep (or pavilion), the priest at the church,
        /// serf families in cottages and freemen in freemen's houses (or a spare cottage). One family to a house; the rest camp in tents.</summary>
        public void Rehouse()
        {
            var units = new Dictionary<int, List<Citizen>>();
            foreach (var p in People)
            {
                int k = HouseholdOf(p);
                List<Citizen> l;
                if (!units.TryGetValue(k, out l)) { l = new List<Citizen>(); units[k] = l; }
                l.Add(p);
            }
            foreach (var b in Buildings) b.Residents.Clear();
            var taken = new HashSet<int>();
            var seat = Seat;
            // the lord's household
            List<Citizen> court;
            if (units.TryGetValue(RoyalKey, out court))
            {
                foreach (var p in court) p.HomeId = seat != null ? seat.Id : -1;
                if (seat != null) taken.Add(seat.Id);
            }
            // the priest lives at the church, or with the lord until there is one
            var church = Church;
            foreach (var p in People.Where(p => p.Rank == Rank.Priest)) p.HomeId = church != null ? church.Id : seat != null ? seat.Id : -1;
            if (church != null) taken.Add(church.Id);
            // the steward lives at his hall, or with the court until it's built
            var hall = StewardHall;
            foreach (var p in People.Where(p => p.Rank == Rank.Steward)) p.HomeId = hall != null ? hall.Id : seat != null ? seat.Id : -1;
            if (hall != null) taken.Add(hall.Id);
            var crownCol = GetCitizen(Crown.CollectorId);
            if (crownCol != null) crownCol.HomeId = hall != null ? hall.Id : seat != null ? seat.Id : -1;
            // each baron and his collector at the manor
            foreach (var bar in Baronies)
            {
                var manor = GetBuilding(bar.ManorId);
                foreach (var id in new[] { bar.BaronId, bar.CollectorId }) { var o = GetCitizen(id); if (o != null) o.HomeId = manor != null ? manor.Id : -1; }
            }

            // families keep their house if it suits them; the head's house wins when two families claim one
            var families = units.Where(kv => kv.Key != RoyalKey && !kv.Value.Any(IsOfficer)).ToList();
            var homeless = new List<KeyValuePair<int, List<Citizen>>>();
            foreach (var kv in families.OrderByDescending(kv => kv.Value.Count(p => !p.IsChild && p.PartnerId >= 0)).ThenByDescending(kv => kv.Value.Count))
            {
                var head = GetCitizen(kv.Key) ?? kv.Value[0];
                var current = GetBuilding(head.HomeId);
                if (current == null || !current.Built || !IsPeasantHome(current.Type) || taken.Contains(current.Id))
                {
                    // try any member's house (a widow keeps the family cottage)
                    current = kv.Value.Select(m => GetBuilding(m.HomeId)).FirstOrDefault(h => h != null && h.Built && IsPeasantHome(h.Type) && !taken.Contains(h.Id));
                }
                if (current != null && Suits(current, head)) { taken.Add(current.Id); foreach (var m in kv.Value) m.HomeId = current.Id; }
                else homeless.Add(kv);
            }
            foreach (var kv in homeless)
            {
                var head = GetCitizen(kv.Key) ?? kv.Value[0];
                var was = GetBuilding(head.HomeId); int vid = was != null ? was.VillageId : -1;
                var free = Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && !taken.Contains(b.Id) && Suits(b, head))
                                    .OrderBy(b => b.VillageId == vid ? 0 : 1).ThenBy(b => head.Rank == Rank.Freeman ? (b.Type == BuildingType.FreeHouse ? 0 : 1) : (b.Type == BuildingType.Cottage ? 0 : 1))
                                    .FirstOrDefault();
                if (free != null) { taken.Add(free.Id); foreach (var m in kv.Value) m.HomeId = free.Id; }
                else foreach (var m in kv.Value) m.HomeId = -1;
            }
            foreach (var p in People) { var h = GetBuilding(p.HomeId); if (h != null) h.Residents.Add(p.Id); }
        }

        /// <summary>Serfs live in cottages; freemen prefer their own houses but will take a cottage.</summary>
        static bool Suits(Building home, Citizen head)
        {
            if (head.Rank == Rank.Freeman) return home.Type == BuildingType.FreeHouse || home.Type == BuildingType.Cottage;
            return home.Type == BuildingType.Cottage;
        }

        /// <summary>Every serf family with a cottage holds a share in a field (a share of an open field, or an old strip field);
        /// shares without a family go to those without, and any still spare to families with the hands for two.</summary>
        public void AssignStrips()
        {
            var fields = Buildings.Where(b => b.Built && IsFarm(b.Type)).ToList();
            var slots = new List<KeyValuePair<Building, int>>();
            foreach (var f in fields) for (int i = 0; i < Slots(f); i++) slots.Add(new KeyValuePair<Building, int>(f, i));
            var heads = new HashSet<int>();
            foreach (var home in Buildings.Where(b => b.Built && b.Type == BuildingType.Cottage && b.Residents.Count > 0))
            {
                var h = HeadOf(home);
                if (h != null && !(h.Rank == Rank.Freeman)) heads.Add(h.Id);
            }
            Func<KeyValuePair<Building, int>, int> H = sl => HolderAt(sl.Key, sl.Value);
            // a share whose holder is gone passes to whoever leads that household now
            foreach (var sl in slots)
            {
                int id = H(sl); if (id < 0 || heads.Contains(id)) continue;
                var old = GetCitizen(id);
                int now = old != null ? HouseholdOf(old) : -1;
                SetHolder(sl.Key, sl.Value, heads.Contains(now) ? now : -1);
            }
            // no family holds more than two
            foreach (var g in slots.Where(x => H(x) >= 0).GroupBy(x => H(x)).Where(g => g.Count() > 2).ToList())
                foreach (var x in g.Skip(2)) SetHolder(x.Key, x.Value, -1);
            var count = new Dictionary<int, int>();
            foreach (var x in slots) { int id = H(x); if (id >= 0) { int c; count.TryGetValue(id, out c); count[id] = c + 1; } }
            Func<int, int> held = id => { int c; return count.TryGetValue(id, out c) ? c : 0; };
            Action<KeyValuePair<Building, int>, int> give = (sl, id) => { int was = H(sl); if (was >= 0) count[was]--; SetHolder(sl.Key, sl.Value, id); int c; count.TryGetValue(id, out c); count[id] = c + 1; };
            var without = heads.Where(h => held(h) == 0).ToList();
            Func<int, Building, int> dist = (id, st) =>
            {
                var home = HomeOf(GetCitizen(id)); if (home == null) return int.MaxValue / 2;
                int dx = home.X - st.X, dy = home.Y - st.Y; return dx * dx + dy * dy + (home.VillageId == st.VillageId && st.VillageId >= 0 ? 0 : 100000);
            };
            // every family gets a share before any gets a second: a family without one takes a free share, or else a big family's second
            foreach (var id in without.ToList())
            {
                var free = slots.Where(x => H(x) < 0).OrderBy(x => dist(id, x.Key)).Select(x => (KeyValuePair<Building, int>?)x).FirstOrDefault();
                if (free == null) free = slots.Where(x => H(x) >= 0 && held(H(x)) > 1).OrderBy(x => dist(id, x.Key)).Select(x => (KeyValuePair<Building, int>?)x).FirstOrDefault();
                if (free == null) break;
                give(free.Value, id); without.Remove(id);
            }
            // shares still going spare go to families with the hands to work two (four grown workers or more), in their own village
            foreach (var sl in slots.Where(x => H(x) < 0).ToList())
            {
                int pick = -1, bestHands = 3;
                foreach (var id in heads)
                {
                    if (held(id) != 1) continue;
                    var home = HomeOf(GetCitizen(id)); if (home == null || home.VillageId != sl.Key.VillageId) continue;
                    int hands = home.Residents.Count(r => { var c = GetCitizen(r); return c != null && !c.IsChild && c.Age < 58 && c.Rank == Rank.Serf; });
                    if (hands > bestHands) { bestHands = hands; pick = id; }
                }
                if (pick >= 0) give(sl, pick);
            }
        }

        /// <summary>Field shares in a village a newcomer family could take: free ones, and big families' second shares.</summary>
        public int SpareStrips(int villageId)
        {
            int shares = Buildings.Where(b => b.Built && IsFarm(b.Type) && b.VillageId == villageId).Sum(b => Slots(b));
            int families = Buildings.Count(b => b.Built && b.Type == BuildingType.Cottage && b.Residents.Count > 0 && b.VillageId == villageId && HeadOf(b) != null && HeadOf(b).Rank != Rank.Freeman);
            return shares - families;
        }

        /// <summary>The field the family holds a share in that most wants work today (a big family may hold two).</summary>
        public Building StripOf(Citizen p)
        {
            int head = HouseholdOf(p);
            Building first = null, best = null; float bestScore = float.MaxValue;
            foreach (var b in Buildings)
            {
                if (!b.Built || !IsFarm(b.Type) || SharesOf(b, head) == 0) continue;
                if (first == null) first = b;
                float score = Season <= 1 ? b.CropGrowth : -b.CropLeft;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best ?? first;
        }

        /// <summary>Places a building will take a freeman on for: the crown's works while there are hands short, a shop for the
        /// hands it wants (an owned shop only with a few days' wages in hand for each hired hand).</summary>
        public int OpenPlaces(Building b)
        {
            if (!b.Built || b.Def.Workers <= 0) return 0;
            if (!b.Def.IsShop) return Math.Max(0, b.Def.Workers - b.Employees.Count);
            int want = Math.Min(b.Def.Workers, b.Desired < 0 ? b.Def.Workers : b.Desired);
            int open = want - b.Employees.Count;
            if (open > 0 && b.OwnerId >= 0)
            {
                int hired = HiredHands(b).Count;
                int afford = (int)(Math.Max(0, b.Coin) / (WageOf(b) * 4f)) - hired;
                open = Math.Min(open, Math.Max(0, afford));
            }
            return Math.Max(0, open);
        }

        /// <summary>Who works where. Shops go to freeman families who can pay the entry fine; an owner's wife and grown children
        /// work the family shop; then the places the crown and the shops will pay for are filled from freemen without work,
        /// the mill first, folk from the same lands first.</summary>
        public void Hire()
        {
            foreach (var b in Buildings)
            {
                if (!b.Built || b.Def.Workers <= 0) { b.Employees.Clear(); continue; }
                b.Employees.RemoveAll(id => { var p = GetCitizen(id); return p == null || p.WorkId != b.Id; });
            }
            foreach (var p in People) if (p.WorkId >= 0 && GetBuilding(p.WorkId) == null) { p.WorkId = -1; p.FamilyHand = false; }
            foreach (var p in People) if (p.FamilyHand && p.WorkId < 0) p.FamilyHand = false;
            ShopHeirs();
            AssignOwners();

            // the family works the family shop, as many as there's work for
            foreach (var b in Buildings)
            {
                if (!b.Built || !b.Def.IsShop || b.OwnerId < 0) continue;
                var o = GetCitizen(b.OwnerId); if (o == null) continue;
                int want = Math.Min(b.Def.Workers, b.Desired < 0 ? b.Def.Workers : b.Desired);
                if (b.Employees.Count >= want) continue;
                var sp = GetCitizen(o.PartnerId);
                foreach (var k in People.Where(c => !c.IsChild && c.HomeId == o.HomeId && c.Id != o.Id && c.Rank == Rank.Freeman && c.WorkId < 0 && c.Age < 62 && c.Health > 30 &&
                                                    (c.Id == o.PartnerId || c.MotherId == o.Id || c.FatherId == o.Id || (sp != null && (c.MotherId == sp.Id || c.FatherId == sp.Id)))).ToList())
                {
                    if (b.Employees.Count >= want) break;
                    k.WorkId = b.Id; k.FamilyHand = true; b.Employees.Add(k.Id);
                }
            }

            // the places there are: the crown's works, and the shops (a shop only takes on a hand it has the wages for)
            var places = new List<KeyValuePair<Building, int>>();
            foreach (var b in Buildings)
            {
                if (!b.Built || b.Def.Workers <= 0) continue;
                int open = OpenPlaces(b);
                if (open > 0) places.Add(new KeyValuePair<Building, int>(b, open));
            }
            Func<Building, int> rank = b => b.Type == BuildingType.Mill ? 0 : !TradeNeeded(b) ? 9 : b.Type == BuildingType.Bakehouse ? 1 : b.Def.IsShop ? 2 : b.Type == BuildingType.Woodcutter ? 3 : b.Type == BuildingType.Quarry ? 4 : 5;
            places = places.OrderBy(x => rank(x.Key)).ThenBy(x => x.Key.Id).ToList();

            // a freeman standing idle at the crown's woods or quarry takes a place with work in his own lands
            foreach (var w in Buildings.Where(b => b.Built && (b.Type == BuildingType.Woodcutter || b.Type == BuildingType.Quarry) && b.Employees.Count > 0 && !TradeNeeded(b)).ToList())
                foreach (var id in w.Employees.ToList())
                {
                    var p = GetCitizen(id); if (p == null || p.Rank != Rank.Freeman) continue;
                    int dom = DomainOf(w);
                    if (!places.Any(x => x.Key != w && rank(x.Key) < 9 && DomainOf(x.Key) == dom)) break;
                    w.Employees.Remove(id); p.WorkId = -1;
                }
            var free = People.Where(p => !p.IsChild && p.Rank == Rank.Freeman && p.WorkId < 0 && p.Age < 62 && p.Health > 30).OrderBy(p => p.Female ? 1 : 0).ToList();
            // the mill can't stand idle: if no freeman is free, an idle quarryman or a shop's hired hand leaves for it
            foreach (var mill in Buildings.Where(b => b.Built && b.Type == BuildingType.Mill && b.Employees.Count == 0))
            {
                if (free.Count > 0) break;
                Citizen p = null; Building from = null;
                foreach (var b in Buildings.Where(b => b.Built && b.Employees.Count > 0 && (b.Type == BuildingType.Woodcutter || b.Type == BuildingType.Quarry || b.Def.IsShop && b.Type != BuildingType.Bakehouse))
                                           .OrderBy(b => b.Def.IsShop ? 1 : 0).ThenBy(b => TradeNeeded(b) ? 1 : 0).ThenBy(b => b.LastSales))
                {
                    var c = (b.Def.IsShop ? HiredHands(b) : Staff(b)).FirstOrDefault(x => x.Rank == Rank.Freeman);
                    if (c != null) { p = c; from = b; break; }
                }
                if (p == null) break;
                from.Employees.Remove(p.Id); p.WorkId = -1; free.Add(p);
            }
            foreach (var kv in places)
            {
                var b = kv.Key; int open = kv.Value;
                for (int k = 0; k < open && free.Count > 0; k++)
                {
                    // folk from the same lands first; the alewife: brewing was women's work
                    int dom = DomainOf(b);
                    var p = free.OrderBy(x => DomainOf(x) == dom ? 0 : 1).ThenBy(x => b.Type == BuildingType.Alehouse ? (x.Female ? 0 : 1) : (x.Female ? 1 : 0)).First();
                    free.Remove(p);
                    p.WorkId = b.Id; p.FamilyHand = false; b.Employees.Add(p.Id);
                    if (b.Def.IsShop && b.Wage <= 0) b.Wage = Policy.FreeWage;
                }
            }
        }

        // =====================================================================
        // Lives: ageing, births, deaths, marriages
        // =====================================================================
        void Lives()
        {
            foreach (var p in People) p.Age += 1f / Balance.DaysPerYearF;

            // births
            foreach (var mom in People.Where(p => p.Female && p.Pregnant > 0).ToList())
            {
                mom.Pregnant--;
                if (mom.Pregnant > 0) continue;
                var dad = GetCitizen(mom.PartnerId);
                var baby = Child(mom, dad, 0);
                Births++;
                if (mom.IsNoble || (dad != null && dad.IsNoble))
                    AddLog((dad != null ? dad.Name : mom.Name) + "'s " + (dad != null ? "wife " + mom.First : "") + " was delivered of a " + (baby.Female ? "daughter" : "son") + ", " + baby.First + ".", LogKind.Birth);
                else AddLog(mom.Name + " had a " + (baby.Female ? "girl" : "boy") + ", " + baby.First + ". " + (Church != null ? "Christened at " + ChurchName + "." : ""), LogKind.Birth);
                if (Chance(0.04)) Die(mom, "in childbed");
                if (Chance(0.12)) Die(baby, "in the cradle");
            }
            foreach (var mom in People.Where(p => p.Female && !p.IsChild && p.Age < 42 && p.Pregnant == 0 && p.PartnerId >= 0))
            {
                var dad = GetCitizen(mom.PartnerId);
                if (dad == null) continue;
                int kids = People.Count(c => c.MotherId == mom.Id && c.Age < 2);
                if (kids > 0) continue;
                if (mom.Fed < 0.6f) continue;
                // the law can forbid it (the royal family always may have heirs); and families hold off when the village is short of grain
                bool royal = mom.IsNoble || dad.IsNoble;
                if (!royal && !Policy.Births) continue;
                if (!royal) { var mv = VillageOf(mom); if (mv != null && GrainDays(mv) < 10 && DaysUntilHarvest > 10) continue; }
                if (Chance(1 / 110.0)) mom.Pregnant = 40;
            }

            // deaths
            foreach (var p in People.ToList())
            {
                if (!People.Contains(p)) continue;
                if (p.Health <= 0) { Die(p, p.Hungry > 3 ? "of hunger" : !p.Warm ? "of the cold" : "of a fever"); continue; }
                double old = p.Age < 1 ? 0.0008 : p.Age < 45 ? 0.00012 : Math.Pow((p.Age - 40) / 40.0, 3) * 0.02;
                if (Chance(old)) Die(p, p.Age > 55 ? "full of years" : "of a sudden illness");
            }

            // marriages: once a season the village pairs off its young folk, and the lord takes the merchet
            if (DayOfSeason == 7) Marriages();
        }

        void Marriages()
        {
            var singles = People.Where(p => !p.IsChild && p.PartnerId < 0 && p.Age < 40 && (p.Rank == Rank.Serf || p.Rank == Rank.Freeman || p.Rank == Rank.Reeve)).ToList();
            var men = Shuffle(singles.Where(p => !p.Female && p.Age >= 17).ToList());
            var women = singles.Where(p => p.Female && p.Age >= 16).ToList();
            foreach (var m in men)
            {
                var w = women.Where(x => x.Family != m.Family && x.MotherId != m.MotherId).OrderBy(x => Math.Abs(x.Age - m.Age + 3)).FirstOrDefault();
                if (w == null || !Chance(0.5)) continue;
                women.Remove(w);
                Marry(m, w);
                w.Family = m.Family;
                // a serf woman marrying a freeman goes free; marrying a serf, she stays bound
                if (m.Rank == Rank.Freeman) w.Rank = Rank.Freeman; else if (w.Rank == Rank.Freeman) w.Rank = Rank.Serf;
                float merchet = m.Rank == Rank.Freeman ? 0f : Math.Min(0.5f, m.Coin + w.Coin);
                float fromM = Math.Min(m.Coin, merchet); m.Coin -= fromM; w.Coin -= merchet - fromM;
                Coin += merchet; WeekIn += merchet; Book("Merchet (marriage fines)", merchet);
                AddLog(m.Name + " and " + w.First + " were married" + (Church != null ? " at the church door" : "") + (merchet > 0 ? ". The crown took " + Money(merchet) + " merchet." : "."), LogKind.Good);
            }
            Rehouse();
        }

        public void Die(Citizen p, string how)
        {
            if (!People.Contains(p)) return;
            Deaths++;
            bool notable = HouseholdOf(p) == RoyalKey || p.Rank == Rank.Priest || p.Rank == Rank.Reeve || p.Age > 1;
            if (notable) AddLog(Title(p) + " died " + how + (p.Age >= 1 ? ", aged " + (int)p.Age : "") + ".", LogKind.Death);
            var partner = GetCitizen(p.PartnerId); if (partner != null) partner.PartnerId = -1;
            foreach (var c in People) { if (c.MotherId == p.Id) c.MotherId = -1; if (c.FatherId == p.Id) c.FatherId = -1; }
            var church = Church; if (church != null && p.Age >= 1) church.Graves++;
            var home = GetBuilding(p.HomeId); if (home != null) home.Residents.Remove(p.Id);
            // heriot: when a serf dies the lord takes something from the house
            if (p.Rank == Rank.Serf && !p.IsChild && home != null && home.Stock[(int)Good.Grain] > 20) { float h = Math.Min(10, home.Stock[(int)Good.Grain] * 0.1f); home.Stock[(int)Good.Grain] -= h; PutLocal(VillageOf(home), Good.Grain, h); }
            var work = GetBuilding(p.WorkId); if (work != null) work.Employees.Remove(p.Id);
            People.Remove(p);
            if (p.Id == KingId) Succession(p);
            if (p.Id == RegentId) RegentId = -1;
            if (p.Id == PriestId) PriestDied();
            if (p.Rank == Rank.Reeve) ReeveVacancies(false);
            if (p.Id == Crown.CollectorId) Crown.CollectorId = -1;
            foreach (var bar in Baronies)
            {
                if (bar.CollectorId == p.Id) bar.CollectorId = -1;
                if (bar.BaronId == p.Id) { bar.BaronId = -1; bar.BaronDue = Day + Balance.StewardArrives; bar.Rebel = false; AddLog("The crown will send a new baron to " + (GetVillage(bar.SeatVillageId) != null ? GetVillage(bar.SeatVillageId).Name : "the barony") + ".", LogKind.Crown); }
            }
            if (p.Id == StewardId) { StewardId = -1; Crown.StewardDay = Day + Balance.StewardArrives; AddLog("A new steward has been sent for.", LogKind.Crown); }
            Rehouse(); AssignStrips(); Hire();
        }

        /// <summary>A serf family slips away by night to try their luck in a town where the lord can't reach them.</summary>
        void Flee(int head, List<Citizen> family)
        {
            var h = GetCitizen(head) ?? family[0];
            AddLog("The " + h.Family + " family fled in the night" + (family.Any(p => p.Rank == Rank.Serf) ? " rather than stay bound to the land." : "."), LogKind.Bad);
            foreach (var p in family)
            {
                var partner = GetCitizen(p.PartnerId); if (partner != null && !family.Contains(partner)) partner.PartnerId = -1;
                People.Remove(p); Fled++;
            }
            Rehouse(); AssignStrips(); Hire();
            ReeveVacancies(false);
        }

        /// <summary>Folk come to the manor: a serf family seeking a lord's protection when a cottage stands empty,
        /// or a freeman's family looking for a trade when there's a house and work for them.</summary>
        void Newcomers()
        {
            if (AverageLoyalty < 45) return;
            var empty = Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count == 0).ToList();
            if (empty.Count == 0) return;
            // a freeman's family asks first whether there's work in those lands, and bread to be had
            bool idleFree = People.Any(p => p.Rank == Rank.Freeman && p.WorkId < 0 && !p.IsChild && p.Age < 62);
            var freeHouse = empty.Where(b => (b.Type == BuildingType.FreeHouse || !idleFree) && FoodAhead(VillageOf(b) ?? Capital) >= 8)
                                 .OrderBy(b => b.Type == BuildingType.FreeHouse ? 0 : 1)
                                 .FirstOrDefault(b => Buildings.Any(w => DomainOf(w) == DomainOf(b) && OpenPlaces(w) > 0));
            bool free = freeHouse != null;
            // a serf family only comes to an empty cottage in a village with a strip field going spare: no land, no living
            var cottages = empty.Where(b => b.Type == BuildingType.Cottage && SpareStrips(b.VillageId) > 0).ToList();
            if (!free && cottages.Count == 0) return;
            if (!Chance(0.5)) return;
            Rank rank = free ? Rank.Freeman : Rank.Serf;
            string fam = Pick(Defs.PeasantNames);
            var dad = AddPerson(false, fam, Rnd(20, 40), rank);
            var mom = AddPerson(true, fam, Rnd(18, 36), rank);
            Marry(dad, mom);
            int n = Rng.Next(0, 3);
            for (int k = 0; k < n; k++) Child(mom, dad, Rnd(0.5f, 10));
            Arrived += 2 + n;
            var home = rank == Rank.Freeman ? freeHouse : cottages[Rng.Next(cottages.Count)];
            var hv = VillageOf(home);
            // they bring enough to see them to their first harvest, roughly
            home.Stock[(int)Good.Cloth] += 2 + n;
            home.Stock[(int)Good.Grain] += Math.Max(30, (2 + n * 0.6f) * Math.Min(DaysUntilHarvest, 50) * 0.6f / Balance.GrainFeeds);
            foreach (var p in People.Where(p => p.Family == fam && p.HomeId < 0 && p.ArrivedDay == Day)) { p.HomeId = home.Id; p.Loyalty = 60; }
            AddLog(rank == Rank.Freeman ? "A freeman, " + dad.Name + ", came with his family looking for work and took the empty house."
                                        : dad.Name + " and his family came to " + (hv != null ? hv.Name : ManorName) + " seeking the king's protection, and swore to serve the crown as villeins.", LogKind.Good);
            Rehouse(); AssignStrips(); Hire();
        }

        // =====================================================================
        // Names and titles
        // =====================================================================
        public string Title(Citizen p)
        {
            if (p == null) return "nobody";
            if (p.Id == KingId) return (p.Female ? "Queen " : "King ") + p.First;
            if (p.Rank == Rank.Knight) return "Sir " + p.First + " " + p.Family;
            if (p.Rank == Rank.Priest) return "Father " + p.First;
            if (p.Rank == Rank.Noble)
            {
                bool consort = p.PartnerId >= 0 && p.PartnerId == KingId;
                bool born = p.MotherId >= 0 || p.FatherId >= 0 || p.IsChild;
                if (p.Female) return (consort ? "Queen " : born ? "Princess " : "Queen dowager ") + p.First;
                return (consort ? "Prince consort " : "Prince ") + p.First;
            }
            if (p.Rank == Rank.Steward) return "Steward " + p.First + " " + p.Family;
            if (p.Rank == Rank.Baron) { var bar = BaronyOfBaron(p); var sv = bar != null ? GetVillage(bar.SeatVillageId) : null; return (p.Female ? "Baroness " : "Baron ") + p.First + (sv != null ? " of " + sv.Name : " " + p.Family); }
            if (p.Rank == Rank.Collector) return p.Name + " the collector";
            if (p.Rank == Rank.Reeve) return p.Name + " the reeve";
            return p.Name;
        }
    }
}
