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

        /// <summary>Every serf family with a cottage holds a strip field; strips without a family go to those without.</summary>
        public void AssignStrips()
        {
            var strips = OfType(BuildingType.Strip).ToList();
            var heads = new HashSet<int>();
            foreach (var home in Buildings.Where(b => b.Built && b.Type == BuildingType.Cottage && b.Residents.Count > 0))
            {
                var h = HeadOf(home);
                if (h != null && !(h.Rank == Rank.Freeman)) heads.Add(h.Id);
            }
            // a strip whose holder is gone passes to whoever leads that household now
            foreach (var s in strips)
            {
                if (s.HolderId < 0 || heads.Contains(s.HolderId)) continue;
                var old = GetCitizen(s.HolderId);
                int now = old != null ? HouseholdOf(old) : -1;
                s.HolderId = heads.Contains(now) ? now : -1;
            }
            var holding = new HashSet<int>(strips.Where(s => s.HolderId >= 0).Select(s => s.HolderId));
            var dupes = strips.Where(s => s.HolderId >= 0).GroupBy(s => s.HolderId).Where(g => g.Count() > 1).SelectMany(g => g.Skip(1)).ToList();
            foreach (var s in dupes) s.HolderId = -1;
            var without = heads.Where(h => !holding.Contains(h)).ToList();
            // a family takes a strip in its own village, or else the nearest one going
            foreach (var s in strips.Where(s => s.HolderId < 0))
            {
                if (without.Count == 0) break;
                int pick = -1, best = int.MaxValue;
                foreach (var id in without)
                {
                    var home = HomeOf(GetCitizen(id)); if (home == null) continue;
                    int dx = home.X - s.X, dy = home.Y - s.Y, d = dx * dx + dy * dy + (home.VillageId == s.VillageId && s.VillageId >= 0 ? 0 : 100000);
                    if (d < best) { best = d; pick = id; }
                }
                if (pick < 0) pick = without[0];
                s.HolderId = pick; without.Remove(pick);
            }
        }

        public Building StripOf(Citizen p)
        {
            int head = HouseholdOf(p);
            foreach (var b in Buildings) if (b.Type == BuildingType.Strip && b.Built && b.HolderId == head) return b;
            return null;
        }

        /// <summary>Freemen take up the trades: the woodward's lodge, the quarry, the mill and the alehouse.</summary>
        public void Hire()
        {
            foreach (var b in Buildings)
            {
                if (!b.Built || b.Def.Workers <= 0) { b.Employees.Clear(); continue; }
                b.Employees.RemoveAll(id => { var p = GetCitizen(id); return p == null || p.WorkId != b.Id; });
            }
            foreach (var p in People) if (p.WorkId >= 0 && GetBuilding(p.WorkId) == null) p.WorkId = -1;
            var free = People.Where(p => !p.IsChild && p.Rank == Rank.Freeman && p.WorkId < 0 && p.Age < 62 && p.Health > 30).OrderBy(p => p.Female ? 1 : 0).ToList();
            // the mill first: the whole village needs it
            foreach (var b in Buildings.Where(b => b.Built && b.Def.Workers > 0).OrderBy(b => b.Type == BuildingType.Mill ? 0 : b.Type == BuildingType.Woodcutter ? 1 : b.Type == BuildingType.Alehouse ? 2 : 3))
            {
                while (b.Employees.Count < b.Def.Workers && free.Count > 0)
                {
                    // the alewife: brewing was women's work
                    var p = b.Type == BuildingType.Alehouse ? free.OrderByDescending(x => x.Female ? 1 : 0).First() : free[0];
                    free.Remove(p);
                    p.WorkId = b.Id; b.Employees.Add(p.Id);
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
                Coin += merchet; WeekIn += merchet;
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
            if (p.Rank == Rank.Serf && !p.IsChild && home != null && home.Stock[(int)Good.Grain] > 20) { float h = Math.Min(10, home.Stock[(int)Good.Grain] * 0.1f); home.Stock[(int)Good.Grain] -= h; PutInStores(Good.Grain, h); }
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
            int openJobs = Buildings.Where(b => b.Built && b.Def.Workers > 0).Sum(b => b.Def.Workers - b.Employees.Count);
            var freeHouse = empty.FirstOrDefault(b => b.Type == BuildingType.FreeHouse);
            bool free = openJobs > 0 && (freeHouse != null || !People.Any(p => p.Rank == Rank.Freeman && p.WorkId < 0 && !p.IsChild));
            if (!free && !empty.Any(b => b.Type == BuildingType.Cottage)) return;
            if (!Chance(0.5)) return;
            Rank rank = free ? Rank.Freeman : Rank.Serf;
            string fam = Pick(Defs.PeasantNames);
            var dad = AddPerson(false, fam, Rnd(20, 40), rank);
            var mom = AddPerson(true, fam, Rnd(18, 36), rank);
            Marry(dad, mom);
            int n = Rng.Next(0, 3);
            for (int k = 0; k < n; k++) Child(mom, dad, Rnd(0.5f, 10));
            Arrived += 2 + n;
            var home = rank == Rank.Freeman ? (freeHouse ?? empty[0]) : empty.OrderBy(b => Rng.Next()).First(b => b.Type == BuildingType.Cottage);
            var hv = VillageOf(home);
            // they bring enough to see them to their first harvest, roughly
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
