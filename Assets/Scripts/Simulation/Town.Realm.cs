using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>
    /// The realm: the capital and the villages you found around it, the baronies some of them become, and the tax chain that
    /// climbs from the households through the reeves, the barons' collectors and the steward's collector to the king.
    ///
    ///   Friday    each village's reeve collects door to door into the village chest (at its cross), keeping his share
    ///   Saturday  each baron's collector rides round the baron's villages and brings their chests to the manor; the baron keeps his share
    ///   Monday    the steward's collector rides to every manor and every crown village and brings it all to the Steward's Hall
    ///   Tuesday   the steward carries the chest to the castle, keeping his share
    /// </summary>
    public partial class Town
    {
        // =====================================================================
        // Lookups
        // =====================================================================
        public Village GetVillage(int id) { if (id < 0) return null; foreach (var v in Villages) if (v.Id == id) return v; return null; }
        public Barony GetBarony(int id) { if (id < 0) return null; foreach (var b in Baronies) if (b.Id == id) return b; return null; }
        public Village Capital { get { return Villages.FirstOrDefault(v => v.Capital); } }
        public Village VillageOf(Citizen p) { var h = HomeOf(p); return h != null ? GetVillage(h.VillageId) : null; }
        public Village VillageOf(Building b) { return b != null ? GetVillage(b.VillageId) : null; }
        public Building CrossOf(Village v) { return v != null ? GetBuilding(v.CrossId) : null; }
        public Barony BaronyOf(Village v) { return v != null ? GetBarony(v.BaronyId) : null; }
        public Barony BaronyOfBaron(Citizen p) { return p == null ? null : Baronies.FirstOrDefault(b => b.BaronId == p.Id || b.CollectorId == p.Id); }
        public IEnumerable<Village> VillagesOf(Barony b) { return Villages.Where(v => v.BaronyId == b.Id); }
        public string LiegeName(Village v)
        {
            var b = BaronyOf(v); var baron = b != null ? GetCitizen(b.BaronId) : null;
            return b == null ? "the crown" : baron != null ? Title(baron) : "an empty barony";
        }
        public Citizen Steward { get { return GetCitizen(StewardId); } }
        public Building StewardHall { get { return OfType(BuildingType.StewardHall).FirstOrDefault(); } }
        /// <summary>Where the steward keeps the crown's tax chest: his hall, or the court until it's built.</summary>
        public Building TaxChest { get { return StewardHall ?? Seat; } }
        public static bool IsOfficer(Citizen p) { return p.Rank == Rank.Priest || p.Rank == Rank.Steward || p.Rank == Rank.Collector || p.Rank == Rank.Baron; }

        IEnumerable<Building> HousesOf(Village v) { return Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && b.VillageId == v.Id); }
        public int PopulationOf(Village v)
        {
            int n = 0;
            foreach (var b in Buildings) if (b.VillageId == v.Id && b.Residents.Count > 0 && (IsPeasantHome(b.Type) || b.Type == BuildingType.Manor)) n += b.Residents.Count;
            return n;
        }

        // =====================================================================
        // Founding villages, raising baronies
        // =====================================================================
        struct Claim { public int X, Y, R, Village; }

        /// <summary>The points a village's land is measured from: its cross, and every boundary stone it has set.</summary>
        List<Claim> Claims(int extra)
        {
            var l = new List<Claim>();
            var byId = new Dictionary<int, Village>();
            foreach (var v in Villages) byId[v.Id] = v;
            foreach (var b in Buildings)
            {
                Village v;
                if (b.VillageId < 0 || !byId.TryGetValue(b.VillageId, out v)) continue;
                if (b.Type == BuildingType.VillageCross && v.CrossId == b.Id)
                    l.Add(new Claim { X = b.X + 1, Y = b.Y + 1, R = (v.Capital ? Balance.VillageRadius + 8 : Balance.VillageRadius) + extra, Village = v.Id });
                else if (b.Type == BuildingType.BoundaryStone && b.Built)
                    l.Add(new Claim { X = b.X, Y = b.Y, R = Balance.StoneReach + extra, Village = v.Id });
            }
            return l;
        }

        static int Nearest(List<Claim> claims, int x, int y)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var c in claims)
            {
                int dx = c.X - x, dy = c.Y - y, d = dx * dx + dy * dy;
                if (d > c.R * c.R) continue;
                float rel = d;   // the nearest marker wins where two villages' lands meet
                if (rel < bd) { bd = rel; best = c.Village; }
            }
            return best;
        }

        /// <summary>The village whose land a tile is on: the one with the nearest cross or boundary stone in reach.</summary>
        public Village VillageAt(int x, int y, int extra = 0) { return GetVillage(Nearest(Claims(extra), x, y)); }

        /// <summary>Which village every tile belongs to (-1: none), for the map.</summary>
        public int[] LandMap()
        {
            var claims = Claims(0);
            var m = new int[Cols * Rows];
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++) m[y * Cols + x] = Nearest(claims, x, y);
            return m;
        }

        /// <summary>A newly placed building belongs to the nearest village.</summary>
        void AssignVillage(Building b)
        {
            if (b.Type == BuildingType.Road || b.Def.IsWall) return;
            if (b.Type == BuildingType.VillageCross) return;   // set when the village is founded
            var d = b.Def;
            var v = VillageAt(b.X + d.W / 2, b.Y + d.H / 2);
            b.VillageId = v != null ? v.Id : -1;
        }

        string RealmCheck(BuildingType type, int x, int y)
        {
            var d = Defs.Get(type);
            if (type == BuildingType.VillageCross)
            {
                var on = VillageAt(x + 1, y + 1);
                if (on != null) return "That's " + on.Name + "'s land. A Boundary Stone extends it; a new cross needs land of its own";
                var near = VillageAt(x + 1, y + 1, 6);
                if (near != null) return "Too close to " + near.Name + ". A new village needs room for its own land";
            }
            if (type == BuildingType.BoundaryStone && VillageAt(x, y) == null) return "A boundary stone has to stand on a village's land, to push its bounds further";
            if (type == BuildingType.Manor)
            {
                var v = VillageAt(x + d.W / 2, y + d.H / 2);
                if (v == null) return "A baron's manor has to be built in a village. Found one with a Village Cross first";
                if (v.Capital) return "The capital belongs to the king. Raise a baron in another village";
                if (Baronies.Any(b => b.SeatVillageId == v.Id) || Buildings.Any(b => b.Type == BuildingType.Manor && b.VillageId == v.Id)) return v.Name + " already has a manor";
            }
            return null;
        }

        /// <summary>A village cross is finished: a new village is founded. It answers to the nearest baron, or to the crown.</summary>
        void FoundVillage(Building cross, bool capital, string name = null)
        {
            var v = new Village { Id = NextRealmId++, CrossId = cross.Id, Capital = capital, Founded = Day };
            var used = new HashSet<string>(Villages.Select(x => x.Name));
            v.Name = name ?? Defs.VillageNames.FirstOrDefault(n => !used.Contains(n) && n != ManorName) ?? ("Newton " + v.Id);
            Villages.Add(v);
            cross.VillageId = v.Id;
            if (!capital)
            {
                var best = LiegeFor(cross.X, cross.Y);
                if (best != null) v.BaronyId = best.Id;
            }
            // the land round the cross that no other village claims
            foreach (var b in Buildings)
            {
                if (b == cross || b.VillageId >= 0 || b.Type == BuildingType.Road || b.Def.IsWall) continue;
                var d = b.Def;
                if (VillageAt(b.X + d.W / 2, b.Y + d.H / 2) == v) b.VillageId = v.Id;
            }
            if (!capital) AddLog(v.Name + " was founded" + (v.BaronyId >= 0 ? ", in the barony of " + LiegeName(v) + "." : ", a crown village.") + " Build cottages and strips and families will come.", LogKind.Good);
        }

        /// <summary>The barony a village founded here would fall to: the nearest manor within reach, or none (the crown).</summary>
        public Barony LiegeFor(int x, int y)
        {
            Barony best = null; int bd = int.MaxValue;
            foreach (var b in Baronies)
            {
                var m = GetBuilding(b.ManorId); if (m == null) continue;
                int dx = m.X - x, dy = m.Y - y, dd = dx * dx + dy * dy;
                if (dd <= Balance.BaronyReach * Balance.BaronyReach && dd < bd) { bd = dd; best = b; }
            }
            return best;
        }
        public string BaronyName(Barony b) { var v = b != null ? GetVillage(b.SeatVillageId) : null; return v != null ? "the barony of " + v.Name : "a barony"; }

        /// <summary>A boundary stone is set: the village's land grows, and anything already standing there with no village joins it.</summary>
        void StoneSet(Building stone)
        {
            var v = VillageOf(stone); if (v == null) return;
            int n = 0;
            foreach (var b in Buildings)
            {
                if (b.VillageId >= 0 || b.Type == BuildingType.Road || b.Def.IsWall || b.Type == BuildingType.VillageCross) continue;
                var d = b.Def;
                if (VillageAt(b.X + d.W / 2, b.Y + d.H / 2) == v) { b.VillageId = v.Id; n++; }
            }
            AddLog("A boundary stone was set at the edge of " + v.Name + ". Its land reaches further now" + (n > 0 ? ", taking in " + n + (n == 1 ? " building" : " buildings") + " that belonged to no village." : "."), LogKind.Build);
        }

        /// <summary>A manor is finished in a village: it becomes the seat of a barony, and a baron is sent for.</summary>
        void RaiseBarony(Building manor)
        {
            var v = VillageOf(manor);
            if (v == null || v.Capital) return;
            var b = new Barony { Id = NextRealmId++, SeatVillageId = v.Id, ManorId = manor.Id };
            Baronies.Add(b);
            v.BaronyId = b.Id;
            BaronArrives(b);
        }

        void BaronArrives(Barony b)
        {
            var v = GetVillage(b.SeatVillageId);
            string house = Pick(Defs.NobleHouses.Where(h => h != Dynasty && !Baronies.Any(o => o != b && GetCitizen(o.BaronId) != null && GetCitizen(o.BaronId).Family == h)).DefaultIfEmpty(Pick(Defs.NobleHouses)).ToList());
            var baron = AddPerson(Chance(0.9) ? false : true, house, Rnd(28, 50), Rank.Baron);
            baron.Trait = (Trait)Rng.Next(6); baron.Loyalty = 70;
            b.BaronId = baron.Id; b.BaronDue = -1; b.Loyalty = Balance.BaronStartLoyalty; b.Rebel = false;
            var col = HireCollector(b);
            var manor = GetBuilding(b.ManorId);
            if (manor != null) manor.Stock[(int)Good.Grain] += 60;
            AddLog(baron.Name + " was made " + (baron.Female ? "Baroness" : "Baron") + " of " + (v != null ? v.Name : "the barony") + ", with " + col.Name + " to collect " + (baron.Female ? "her" : "his") + " taxes.", LogKind.Crown);
            Rehouse();
        }

        Citizen HireCollector(Barony b)
        {
            var col = GetCitizen(b.CollectorId);
            if (col != null) return col;
            col = AddPerson(false, Pick(Defs.PeasantNames), Rnd(22, 45), Rank.Collector);
            col.Loyalty = 70; b.CollectorId = col.Id;
            return col;
        }

        void VillageLost(Building cross)
        {
            var v = GetVillage(cross.VillageId);
            if (v == null || v.CrossId != cross.Id) return;
            var reeve = GetCitizen(v.ReeveId); if (reeve != null) StepDown(reeve);
            foreach (var b in Buildings) if (b.VillageId == v.Id) b.VillageId = -1;
            foreach (var bar in Baronies.Where(x => x.SeatVillageId == v.Id).ToList()) EndBarony(bar, "lost its seat");
            Villages.Remove(v);
            AddLog(v.Name + " is no longer a village.", LogKind.Bad);
        }

        void EndBarony(Barony b, string why)
        {
            var baron = GetCitizen(b.BaronId); var col = GetCitizen(b.CollectorId);
            var manor = GetBuilding(b.ManorId);
            if (manor != null && manor.Coin > 0) { Receive(manor.Coin, manor.ChestGrain); manor.Coin = 0; manor.ChestGrain = 0; }
            foreach (var v in Villages.Where(x => x.BaronyId == b.Id)) v.BaronyId = -1;
            if (baron != null) People.Remove(baron);
            if (col != null) People.Remove(col);
            Baronies.Remove(b);
            AddLog("The barony " + why + ". Its villages answer to the crown directly now.", LogKind.Crown);
            Rehouse();
        }

        /// <summary>The king sets a village under a different lord: the crown, or one of his barons.</summary>
        public void SetLiege(Village v, int baronyId)
        {
            if (v == null || v.Capital) return;
            if (Baronies.Any(b => b.SeatVillageId == v.Id)) return;   // a barony's seat stays with its baron
            v.BaronyId = GetBarony(baronyId) != null ? baronyId : -1;
            AddLog(v.Name + " now answers to " + LiegeName(v) + ".", LogKind.Crown);
        }

        /// <summary>The king strips a baron of his title. A new one is sent for, but the other barons wonder who's next.</summary>
        public void StripBaron(Barony b)
        {
            var baron = GetCitizen(b.BaronId);
            if (baron == null) return;
            var manor = GetBuilding(b.ManorId);
            // what's in his chest goes to the crown
            if (manor != null) { Receive(manor.Coin, manor.ChestGrain); manor.Coin = 0; manor.ChestGrain = 0; }
            AddLog(Title(baron) + " was stripped of the barony" + (baron.Coin > 20 ? ", and of the " + Money(baron.Coin) + " he'd put by." : "."), LogKind.Crown);
            Coin += baron.Coin * 0.5f; Book("Seized from barons", baron.Coin * 0.5f);
            People.Remove(baron);
            b.BaronId = -1; b.BaronDue = Day + Balance.StewardArrives; b.Rebel = false;
            foreach (var o in Baronies) if (o != b) o.Loyalty = Clamp(o.Loyalty - 15, 0, 100);
            if (Baronies.Count > 1) AddLog("The other barons are uneasy.", LogKind.Bad);
            Rehouse();
        }

        /// <summary>Barons' loyalty to the crown drifts with their share and the king's temperament; one pushed too far withholds his taxes.</summary>
        void BaronMoods()
        {
            var r = Ruler;
            float trait = r == null ? 0 : r.Trait == Trait.Just ? 8 : r.Trait == Trait.Brave ? 6 : r.Trait == Trait.Cruel ? -10 : r.Trait == Trait.Greedy ? -6 : 0;
            foreach (var b in Baronies)
            {
                if (b.BaronId < 0)
                {
                    if (b.BaronDue >= 0 && Day >= b.BaronDue) BaronArrives(b);
                    continue;
                }
                var baron = GetCitizen(b.BaronId);
                if (GetCitizen(b.CollectorId) == null) { var c = HireCollector(b); AddLog(Title(baron) + " took on " + c.Name + " to collect his taxes.", LogKind.Info); Rehouse(); }
                float target = 60 + trait + (Policy.BaronCut - 0.15f) * 250 - Math.Max(0, Policy.HarvestTax - 0.3f) * 40;
                if (Keep != null) target += 4;
                if (baron != null && baron.Trait == Trait.Greedy) target -= 6;
                b.Loyalty = Clamp(b.Loyalty + (Clamp(target, 0, 100) - b.Loyalty) * 0.15f, 0, 100);
                if (!b.Rebel && b.Loyalty < 20)
                {
                    b.Rebel = true;
                    AddLog(Title(baron) + " refuses to send his taxes to the crown.", LogKind.Bad);
                }
                else if (b.Rebel && b.Loyalty > 35)
                {
                    b.Rebel = false;
                    AddLog(Title(baron) + " has made his peace with the crown and will send his taxes again.", LogKind.Crown);
                }
            }
        }

        // =====================================================================
        // Reeves
        // =====================================================================
        /// <summary>Each spring every village chooses a reeve from its serfs. They pick someone respected: steady, loyal enough,
        /// not too young, and they have a fair idea who among them is honest.</summary>
        void ChooseReeve(Village v, bool vacancy, int exclude = -1)
        {
            var old = GetCitizen(v.ReeveId);
            if (old != null) StepDown(old);
            v.ReeveId = -1;
            var folk = People.Where(p => !p.IsChild && p.Id != exclude && p.Health > 40 && p.Age >= 18 && p.Age < 62 && (p.Rank == Rank.Serf || p.Rank == Rank.Reeve) && VillageOf(p) == v).ToList();
            // a village of freemen chooses one of them
            if (folk.Count == 0) folk = People.Where(p => !p.IsChild && p.Id != exclude && p.Health > 40 && p.Age >= 18 && p.Age < 62 && p.Rank == Rank.Freeman && VillageOf(p) == v).ToList();
            if (folk.Count == 0) return;
            var pool = folk.Where(p => !p.Female && p.Age >= 20).ToList();
            if (pool.Count == 0) pool = folk;
            var pick = pool.OrderByDescending(p => p.Loyalty * 0.5f + Math.Min(p.Age, 50) * 0.4f + p.Piety * 0.15f + p.Honesty * 18 + p.Seed * 12 + (old != null && p.Id == old.Id && exclude < 0 ? 6 : 0)).First();
            pick.WasFree = pick.Rank == Rank.Freeman || (pick.Rank == Rank.Reeve && pick.WasFree);
            pick.Rank = Rank.Reeve; v.ReeveId = pick.Id;
            if (old != null && old.Id == pick.Id) AddLog(v.Name + " kept " + pick.Name + " on as reeve for another year.", LogKind.Info);
            else AddLog("The villagers of " + v.Name + " chose " + pick.Name + " as reeve" + (vacancy ? " to fill the office." : " for the year.") + " He'll collect the village's taxes.", LogKind.Info);
        }

        /// <summary>A reeve's year is over: back to the strips, or to his trade if he was a freeman.</summary>
        void StepDown(Citizen r)
        {
            if (r.Rank != Rank.Reeve) return;
            r.Rank = r.WasFree ? Rank.Freeman : Rank.Serf; r.WasFree = false;
        }

        /// <summary>The crown tells a village to choose another reeve.</summary>
        public void NewReeve(Village v)
        {
            var old = GetCitizen(v.ReeveId);
            if (old == null) { ChooseReeve(v, true); return; }
            AddLog("By order of the crown, " + old.Name + " is reeve of " + v.Name + " no longer.", LogKind.Info);
            foreach (var p in People.Where(p => !RoyalHousehold(p) && VillageOf(p) == v)) p.Loyalty = Clamp(p.Loyalty + (old.Honesty < 0.55f ? 4 : -2), 0, 100);
            ChooseReeve(v, true, old.Id);
        }

        void ReeveVacancies(bool spring)
        {
            foreach (var v in Villages.ToList())
            {
                var r = GetCitizen(v.ReeveId);
                if (spring) ChooseReeve(v, false);
                else if (r == null || r.Rank != Rank.Reeve || VillageOf(r) != v) { if (r != null) StepDown(r); v.ReeveId = -1; if (HousesOf(v).Any()) ChooseReeve(v, true); }
            }
        }
        public Citizen ReeveOf(Village v) { return v != null ? GetCitizen(v.ReeveId) : null; }

        // =====================================================================
        // The steward
        // =====================================================================
        public void DismissSteward()
        {
            var b = Steward;
            if (b == null) return;
            AddLog(Title(b) + " was dismissed and left" + (b.Coin > 15 ? " a richer man than he came." : ".") + " A new steward has been sent for.", LogKind.Crown);
            People.Remove(b); StewardId = -1;
            Crown.StewardDay = Day + Balance.StewardArrives;
            Rehouse();
        }

        void StewardArrives()
        {
            if (StewardId < 0 && (Crown.StewardDay < 0 || Day >= Crown.StewardDay))
            {
                var b = AddPerson(false, Pick(Defs.KnightNames.Concat(Defs.PeasantNames).ToList()), Rnd(28, 50), Rank.Steward);
                b.Honesty = (float)Math.Round(Rnd(0.25f, 1f), 2); b.Loyalty = 70; b.Coin = 3;
                StewardId = b.Id; Crown.StewardDay = -1;
                AddLog(b.Name + " arrived to serve as the king's steward.", LogKind.Crown);
                Rehouse();
            }
            // the steward's collector, once there's more than the capital to ride to
            if (GetCitizen(Crown.CollectorId) == null && Villages.Count > 1)
            {
                var c = AddPerson(false, Pick(Defs.PeasantNames), Rnd(22, 45), Rank.Collector);
                c.Loyalty = 70; Crown.CollectorId = c.Id;
                AddLog(c.Name + " was taken on as the steward's collector, to ride out to the villages and barons.", LogKind.Crown);
                Rehouse();
            }
        }

        // =====================================================================
        // Taxes
        // =====================================================================
        /// <summary>Households that pay taxes: those in a village. A house outside every village has no reeve to collect from it.</summary>
        IEnumerable<Building> TaxHouses { get { return Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0 && b.VillageId >= 0); } }

        void Assess(Building home, float coin, float grain)
        {
            home.OwedCoin += coin; home.OwedGrain += grain;
            Taxes.AssessedCoin += coin; Taxes.AssessedGrain += grain;
        }

        /// <summary>Sunday night: each household owes its hearth tax, freemen their rent.</summary>
        void WeeklyAssessment()
        {
            foreach (var h in TaxHouses.ToList())
            {
                var head = HeadOf(h);
                bool free = head != null && head.Rank == Rank.Freeman;
                Assess(h, Policy.HearthTax + (free ? Policy.Rent : 0), 0);
            }
        }

        static float TakeCoin(List<Citizen> folk, float owed)
        {
            float paid = 0;
            foreach (var p in folk) { float t = Math.Min(p.Coin, owed - paid); if (t <= 0) continue; p.Coin -= t; paid += t; if (paid >= owed - 0.0001f) break; }
            return paid;
        }

        /// <summary>Friday: every village's reeve goes door to door and puts what he collects in the village chest at the cross.
        /// A crooked reeve squeezes them for a little more and pockets it.</summary>
        void ReevesCollect()
        {
            foreach (var v in Villages)
            {
                var reeve = GetCitizen(v.ReeveId); var cross = CrossOf(v);
                if (reeve == null || reeve.Health < 30 || cross == null) continue;
                float coinIn = 0, grainIn = 0, extra = 0;
                int short_ = 0;
                float greed = Math.Max(0, 0.6f - reeve.Honesty) / 0.6f;
                var reeveHome = HomeOf(reeve);
                foreach (var h in HousesOf(v).ToList())
                {
                    if (h.OwedCoin <= 0.001f && h.OwedGrain <= 0.001f) continue;
                    bool own = h.Residents.Contains(reeve.Id);
                    var folk = h.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild).OrderByDescending(p => p.Coin).ToList();
                    float g = Math.Min(h.OwedGrain, h.Stock[(int)Good.Grain]);
                    h.Stock[(int)Good.Grain] -= g; h.OwedGrain -= g; grainIn += g;
                    float c = TakeCoin(folk, h.OwedCoin);
                    h.OwedCoin -= c; coinIn += c;
                    if (h.OwedCoin > 0.01f)
                    {
                        // what they can't pay in coin he takes in grain, at the merchant's price
                        float asGrain = Math.Min(h.Stock[(int)Good.Grain] * 0.5f, h.OwedCoin / Balance.MerchantGrain);
                        h.Stock[(int)Good.Grain] -= asGrain; grainIn += asGrain; h.OwedCoin -= asGrain * Balance.MerchantGrain;
                    }
                    h.Arrears = h.OwedCoin;
                    if (h.OwedCoin > 0.01f || h.OwedGrain > 0.5f) { short_++; foreach (var p in folk) p.Loyalty = Clamp(p.Loyalty - 2, 0, 100); }
                    if (greed > 0 && !own)
                    {
                        float sq = TakeCoin(folk, (c + g * Balance.MerchantGrain) * greed * Balance.SkimMax);
                        if (sq < 0.01f)
                        {
                            float gg = Math.Min(h.Stock[(int)Good.Grain] * 0.1f, g * greed * Balance.SkimMax);
                            h.Stock[(int)Good.Grain] -= gg; if (reeveHome != null) reeveHome.Stock[(int)Good.Grain] += gg;
                            sq = gg * Balance.MerchantGrain;
                        }
                        else reeve.Coin += sq;
                        extra += sq;
                        foreach (var p in folk) p.Loyalty = Clamp(p.Loyalty - 3 * greed, 0, 100);
                    }
                }
                float rc = coinIn * Policy.ReeveCut, rg = grainIn * Policy.ReeveCut;
                reeve.Coin += rc; if (reeveHome != null) reeveHome.Stock[(int)Good.Grain] += rg;
                Taxes.ReeveCutCoin += rc; Taxes.ReeveCutGrain += rg;
                coinIn -= rc; grainIn -= rg;
                cross.Coin += coinIn; cross.ChestGrain += grainIn;
                Taxes.CollectedCoin += coinIn; Taxes.CollectedGrain += grainIn;
                v.LastCoin = coinIn; v.LastGrain = grainIn;
                if (extra > 0.3f && Day - v.GrumbleDay > 14)
                {
                    v.GrumbleDay = Day;
                    AddLog("There's grumbling in " + v.Name + " that " + reeve.Name + " the reeve takes more than is owed.", LogKind.Bad);
                }
                if (short_ > 0 && Chance(0.2)) AddLog(short_ + (short_ == 1 ? " family" : " families") + " in " + v.Name + " couldn't pay the reeve in full this week.", LogKind.Bad);
            }
        }

        /// <summary>Saturday: each baron's collector rides round the baron's villages and brings their chests to the manor.
        /// The baron keeps his share, and a grasping one more.</summary>
        void BaronsCollect()
        {
            foreach (var b in Baronies)
            {
                var col = GetCitizen(b.CollectorId); var manor = GetBuilding(b.ManorId); var baron = GetCitizen(b.BaronId);
                if (col == null || manor == null || col.Health < 30) continue;
                float coin = 0, grain = 0;
                foreach (var v in VillagesOf(b))
                {
                    var cross = CrossOf(v); if (cross == null) continue;
                    coin += cross.Coin; grain += cross.ChestGrain; cross.Coin = 0; cross.ChestGrain = 0;
                }
                if (coin < 0.01f && grain < 0.5f) continue;
                // the baron's share: the custom, and what his honesty lets him add
                float skim = baron == null ? 0 : Math.Max(0, 0.6f - baron.Honesty) / 0.6f * Balance.SkimMax;
                float cut = baron == null ? 0 : Policy.BaronCut + skim;
                float bc = coin * cut, bg = grain * cut;
                if (baron != null) baron.Coin += bc;
                manor.Stock[(int)Good.Grain] += bg;
                Taxes.BaronCutCoin += bc; Taxes.BaronCutGrain += bg;
                manor.Coin += coin - bc; manor.ChestGrain += grain - bg;
            }
        }

        /// <summary>Monday: the steward's collector rides to every manor and every crown village and brings it all to the Steward's Hall.
        /// A rebel baron sends him away empty-handed.</summary>
        void StewardCollects()
        {
            var col = GetCitizen(Crown.CollectorId);
            var chest = TaxChest; if (chest == null) return;
            bool capitalOnly = col == null || col.Health < 30;
            foreach (var v in Villages)
            {
                if (v.BaronyId >= 0) continue;
                if (capitalOnly && !v.Capital) continue;   // without a collector, only the capital's chest is fetched
                var cross = CrossOf(v); if (cross == null) continue;
                chest.Coin += cross.Coin; chest.ChestGrain += cross.ChestGrain; cross.Coin = 0; cross.ChestGrain = 0;
            }
            if (capitalOnly) return;
            foreach (var b in Baronies)
            {
                var manor = GetBuilding(b.ManorId); if (manor == null) continue;
                if (b.Rebel) { Taxes.Withheld += manor.Coin + manor.ChestGrain * Balance.MerchantGrain; continue; }
                b.LastCoin = manor.Coin; b.LastGrain = manor.ChestGrain;
                chest.Coin += manor.Coin; chest.ChestGrain += manor.ChestGrain; manor.Coin = 0; manor.ChestGrain = 0;
            }
        }

        /// <summary>Tuesday: the steward carries the chest to the castle, keeping his share. A crooked one keeps a good deal more.</summary>
        void StewardDelivers()
        {
            var s = Steward; var chest = TaxChest;
            if (chest == null) return;
            float coin = chest.Coin, grain = chest.ChestGrain;
            if (coin < 0.01f && grain < 0.5f) return;
            float cut = 0;
            if (s != null && s.Health >= 30) cut = Policy.StewardCut + Math.Max(0, 0.65f - s.Honesty) / 0.65f * Balance.SkimMax;
            else if (s == null && Crown.StewardDay >= 0) return;   // the chest waits for the new steward
            float sc = coin * cut, sg = grain * cut;
            chest.Coin = 0; chest.ChestGrain = 0;
            if (s != null) { s.Coin += sc; var home = HomeOf(s); if (home != null) home.Stock[(int)Good.Grain] += sg; }
            Taxes.StewardCutCoin += sc; Taxes.StewardCutGrain += sg;
            Receive(coin - sc, grain - sg);
        }

        void Receive(float coin, float grain)
        {
            Coin += coin; WeekIn += coin; PutInStores(Good.Grain, grain); Book("Taxes", coin);
            Taxes.ReceivedCoin += coin; Taxes.ReceivedGrain += grain;
            WeekTaxCoin += coin; WeekTaxGrain += grain;
        }

        /// <summary>The year's accounts at Michaelmas, the end of autumn.</summary>
        void Michaelmas()
        {
            var t = Taxes;
            AddLog("Michaelmas accounts: the treasury received " + Money(t.ReceivedCoin) + " and " + Math.Round(t.ReceivedGrain) + " measures of grain in taxes this year." + (t.Withheld > 1 ? " Rebel barons kept back about " + Money(t.Withheld) + "." : ""), LogKind.Crown);
            // the clerks compare what reached the steward's hall with what he brought to the castle
            float into = t.ReceivedCoin + t.StewardCutCoin + (t.ReceivedGrain + t.StewardCutGrain) * Balance.MerchantGrain;
            float kept = t.StewardCutCoin + t.StewardCutGrain * Balance.MerchantGrain;
            if (into > 5 && kept > into * (Policy.StewardCut + 0.06f))
                AddLog("The king's clerks find the steward's accounts don't add up: about " + Money(kept - into * Policy.StewardCut) + " more than his share is missing.", LogKind.Bad);
            foreach (var b in Baronies)
            {
                var baron = GetCitizen(b.BaronId);
                if (baron != null && baron.Honesty < 0.45f && Chance(0.7)) AddLog("Word reaches the court that " + Title(baron) + " keeps more than his share of the taxes.", LogKind.Bad);
            }
            Taxes.Unpaid = TaxHouses.Sum(h => h.OwedCoin);
            LastTaxes = new TaxBook(); LastTaxes.Add(Taxes);
            Taxes.Clear();
        }

        /// <summary>Martinmas, the start of winter: the crown's tallage is assessed on every serf household.</summary>
        void Martinmas()
        {
            if (Policy.Tallage <= 0) return;
            int n = 0;
            foreach (var h in TaxHouses.Where(b => b.Type == BuildingType.Cottage).ToList())
            {
                var head = HeadOf(h);
                if (head == null || head.Rank == Rank.Freeman) continue;
                Assess(h, Policy.Tallage, 0); n++;
                foreach (var p in h.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild)) p.Loyalty = Clamp(p.Loyalty - Policy.Tallage * 2, 0, 100);
            }
            AddLog("Martinmas: the crown's tallage of " + Money(Policy.Tallage) + " was laid on " + n + " serf families. The reeves will collect it.", LogKind.Info);
        }
    }
}
