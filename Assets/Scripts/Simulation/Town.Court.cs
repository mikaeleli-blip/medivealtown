using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>The hierarchy: the king and his heirs, the bailiff he appoints, the reeve the villagers choose, and the taxes
    /// that climb from the serfs' houses through the reeve and the bailiff to the castle.</summary>
    public partial class Town
    {
        // =====================================================================
        // The royal family
        // =====================================================================
        /// <summary>The heir by the custom of the realm: the eldest son, then the eldest daughter, then royal kin.</summary>
        public Citizen HeirOf(Citizen king)
        {
            if (king == null) return null;
            var kids = People.Where(p => (p.FatherId == king.Id || p.MotherId == king.Id) && p.IsNoble).ToList();
            var son = kids.Where(p => !p.Female).OrderByDescending(p => p.Age).FirstOrDefault();
            if (son != null) return son;
            var daughter = kids.Where(p => p.Female).OrderByDescending(p => p.Age).FirstOrDefault();
            if (daughter != null) return daughter;
            return People.Where(p => p.Rank == Rank.Noble && p.Family == king.Family && p.Id != king.PartnerId && !p.IsChild).OrderByDescending(p => p.Age).FirstOrDefault();
        }
        public Citizen Heir { get { return HeirOf(King); } }

        void Succession(Citizen dead)
        {
            var heir = HeirOf(dead);
            var widow = People.FirstOrDefault(p => p.Id == dead.PartnerId);
            if (widow != null) widow.Rank = Rank.Noble;
            if (heir != null)
            {
                heir.Rank = Rank.King; KingId = heir.Id; Generation++;
                if (heir.IsChild)
                {
                    var regent = widow ?? People.Where(p => p.Rank == Rank.Noble && !p.IsChild).OrderByDescending(p => p.Age).FirstOrDefault()
                                 ?? People.FirstOrDefault(p => p.Rank == Rank.Knight);
                    RegentId = regent != null ? regent.Id : -1;
                    AddLog(heir.First + ", a child of " + (int)heir.Age + ", is " + (heir.Female ? "queen" : "king") + " now." + (regent != null ? " " + Title(regent) + " rules as regent until " + (heir.Female ? "she" : "he") + " comes of age." : ""), LogKind.Crown);
                }
                else AddLog(Title(heir) + " is crowned in " + (Church != null ? ChurchName : "the great hall") + ". Long live the " + (heir.Female ? "queen" : "king") + ".", LogKind.Crown);
                foreach (var p in People) if (!RoyalHousehold(p)) p.Loyalty = Clamp(p.Loyalty * 0.85f + 55 * 0.15f, 0, 100);
                return;
            }
            // no heir: a claimant from another house takes the throne
            string house = Pick(Defs.NobleHouses.Where(h => h != Dynasty).ToList());
            var nk = AddPerson(Chance(0.85) ? false : true, house, Rnd(24, 40), Rank.King);
            nk.Trait = (Trait)Rng.Next(6);
            nk.HomeId = Seat != null ? Seat.Id : -1;
            KingId = nk.Id; RegentId = -1; Dynasty = house; Generation = 1;
            AddLog(Title(dead) + " left no heir. " + Title(nk) + " of the house of " + house + " came with an armed retinue and took the throne.", LogKind.Crown);
            foreach (var p in People) if (!RoyalHousehold(p)) p.Loyalty = Clamp(p.Loyalty * 0.6f + 45 * 0.4f, 0, 100);
        }

        void ComeOfAge()
        {
            var k = King;
            if (k == null || RegentId < 0 || k.IsChild) return;
            var r = Regent;
            AddLog(Title(k) + " has come of age and rules in " + (k.Female ? "her" : "his") + " own right." + (r != null ? " " + Title(r) + "'s regency is over." : ""), LogKind.Crown);
            RegentId = -1;
        }

        /// <summary>An unwed monarch is a worry to the realm: a match is made, and the royal bride or groom arrives with a dowry.</summary>
        void Matchmaking()
        {
            var k = King;
            if (k == null || k.IsChild || k.PartnerId >= 0 || k.Age > 55) { Crown.SuitorDay = -1; return; }
            if (Crown.SuitorDay < 0) { Crown.SuitorDay = Day + Rng.Next(15, 35); AddLog("Envoys have gone abroad to find " + Title(k) + " a match.", LogKind.Crown); return; }
            if (Day < Crown.SuitorDay) return;
            var house = Pick(Defs.NobleHouses.Where(h => h != k.Family).ToList());
            var bride = AddPerson(!k.Female, house, Math.Max(17, k.Age - Rnd(2, 12)), Rank.Noble);
            bride.Trait = (Trait)Rng.Next(6);
            Marry(k, bride);
            if (!k.Female) bride.Family = k.Family;
            bride.HomeId = Seat != null ? Seat.Id : -1;
            float dowry = 15 + Rng.Next(0, 16);
            Coin += dowry; WeekIn += dowry;
            Crown.SuitorDay = -1;
            AddLog(Title(k) + " married " + bride.First + " of the house of " + house + ", who brought a dowry of " + Money(dowry) + ".", LogKind.Crown);
            Rehouse();
        }

        // =====================================================================
        // The officers
        // =====================================================================
        public Citizen Bailiff { get { return GetCitizen(BailiffId); } }
        public Building BailiffHall { get { return OfType(BuildingType.BailiffHall).FirstOrDefault(); } }
        /// <summary>Where the bailiff keeps the tax chest: his hall, or his lodging until it's built.</summary>
        public Building TaxChest { get { return BailiffHall ?? GetBuilding(Bailiff != null ? Bailiff.HomeId : -1) ?? Seat; } }

        /// <summary>Each spring the villagers choose a reeve from among the serfs. They pick someone respected: steady, loyal enough,
        /// not too young, and they have a fair idea who among them is honest.</summary>
        void ChooseReeve(bool vacancy, int exclude = -1)
        {
            var old = Reeve;
            if (old != null) old.Rank = Rank.Serf;
            var pool = People.Where(p => !p.IsChild && !p.Female && p.Age >= 20 && p.Age < 62 && (p.Rank == Rank.Serf || p.Rank == Rank.Reeve) && p.Health > 40 && p.Id != exclude).ToList();
            if (pool.Count == 0) pool = People.Where(p => !p.IsChild && p.Age >= 20 && p.Rank == Rank.Serf && p.Health > 40 && p.Id != exclude).ToList();
            if (pool.Count == 0) { ReeveId = -1; return; }
            var pick = pool.OrderByDescending(p => p.Loyalty * 0.5f + Math.Min(p.Age, 50) * 0.4f + p.Piety * 0.15f + p.Honesty * 18 + p.Seed * 12 + (old != null && p.Id == old.Id && exclude < 0 ? 6 : 0)).First();
            pick.Rank = Rank.Reeve; ReeveId = pick.Id;
            if (old != null && old.Id == pick.Id) AddLog("The village kept " + pick.Name + " on as reeve for another year.", LogKind.Info);
            else AddLog("The villagers chose " + pick.Name + " as reeve" + (vacancy ? " to fill the office." : " for the year.") + " He'll see the king's fields worked and collect the taxes.", LogKind.Info);
        }

        /// <summary>The crown tells the village to choose another reeve.</summary>
        public void NewReeve()
        {
            var old = Reeve;
            if (old == null) { ChooseReeve(true); return; }
            AddLog("By order of the crown, " + old.Name + " is reeve no longer.", LogKind.Info);
            foreach (var p in People.Where(p => !RoyalHousehold(p))) p.Loyalty = Clamp(p.Loyalty + (old.Honesty < 0.55f ? 4 : -2), 0, 100);   // folk know whether he deserved it
            ChooseReeve(true, old.Id);
        }

        /// <summary>The king dismisses his bailiff. He leaves the village with whatever he's put by; a new one is sent for.</summary>
        public void DismissBailiff()
        {
            var b = Bailiff;
            if (b == null) return;
            var chest = TaxChest;
            // what's in the chest stays for the crown
            if (chest != null)
            {
                Receive(chest.Coin, 0); chest.Coin = 0;
            }
            AddLog(Title(b) + " was dismissed and left " + ManorName + (b.Coin > 15 ? " a richer man than he came." : ".") + " A new bailiff has been sent for.", LogKind.Crown);
            People.Remove(b); BailiffId = -1;
            Crown.BailiffDay = Day + Balance.BailiffArrives;
            Rehouse();
        }

        void BailiffArrives()
        {
            if (BailiffId >= 0 || (Crown.BailiffDay >= 0 && Day < Crown.BailiffDay)) return;
            var b = AddPerson(false, Pick(Defs.KnightNames.Concat(Defs.PeasantNames).ToList()), Rnd(28, 50), Rank.Bailiff);
            b.Honesty = (float)Math.Round(Rnd(0.25f, 1f), 2); b.Loyalty = 70; b.Coin = 3;
            BailiffId = b.Id; Crown.BailiffDay = -1;
            AddLog(b.Name + " arrived to serve as the king's bailiff.", LogKind.Crown);
            Rehouse();
        }

        // =====================================================================
        // Taxes
        // =====================================================================
        /// <summary>Every household in the village that the reeve collects from.</summary>
        IEnumerable<Building> TaxHouses { get { return Buildings.Where(b => b.Built && IsPeasantHome(b.Type) && b.Residents.Count > 0); } }

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

        /// <summary>Friday: the reeve goes from door to door collecting what each household owes, keeps his share and carries the rest
        /// to the bailiff. A crooked reeve squeezes them for a little more and pockets it.</summary>
        void ReeveCollects()
        {
            var reeve = Reeve;
            if (reeve == null || reeve.Health < 30) return;
            float coinIn = 0, grainIn = 0, extra = 0;
            int short_ = 0;
            float greed = Math.Max(0, 0.6f - reeve.Honesty) / 0.6f;   // 0 for an honest man, up to 1
            var reeveHome = HomeOf(reeve);
            foreach (var h in TaxHouses.ToList())
            {
                if (h.OwedCoin <= 0.001f && h.OwedGrain <= 0.001f) continue;
                if (h.Residents.Contains(reeve.Id)) { PayOwn(h); continue; }   // his own household's dues go straight in
                var folk = h.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild).OrderByDescending(p => p.Coin).ToList();
                // grain first, out of the barn
                float g = Math.Min(h.OwedGrain, h.Stock[(int)Good.Grain]);
                h.Stock[(int)Good.Grain] -= g; h.OwedGrain -= g; grainIn += g;
                // then coin; what they can't pay in coin he takes in grain, at the merchant's price
                float c = TakeCoin(folk, h.OwedCoin);
                h.OwedCoin -= c; coinIn += c;
                if (h.OwedCoin > 0.01f)
                {
                    float asGrain = Math.Min(h.Stock[(int)Good.Grain] * 0.5f, h.OwedCoin / Balance.MerchantGrain);
                    h.Stock[(int)Good.Grain] -= asGrain; grainIn += asGrain; h.OwedCoin -= asGrain * Balance.MerchantGrain;
                }
                if (h.OwedCoin > 0.01f || h.OwedGrain > 0.5f)
                {
                    short_++;
                    foreach (var p in folk) p.Loyalty = Clamp(p.Loyalty - 2, 0, 100);
                }
                h.Arrears = h.OwedCoin;
                // the squeeze
                if (greed > 0)
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
            // his share
            float rc = coinIn * Policy.ReeveCut, rg = grainIn * Policy.ReeveCut;
            reeve.Coin += rc; if (reeveHome != null) reeveHome.Stock[(int)Good.Grain] += rg;
            Taxes.ReeveCutCoin += rc; Taxes.ReeveCutGrain += rg;
            coinIn -= rc; grainIn -= rg;
            ToBailiff(coinIn, grainIn);
            if (extra > 0.3f && Day - Crown.GrumbleDay > 14)
            {
                Crown.GrumbleDay = Day;
                AddLog("There's grumbling in the village that " + reeve.Name + " takes more than is owed.", LogKind.Bad);
            }
            if (short_ > 0 && Chance(0.3)) AddLog(short_ + (short_ == 1 ? " family" : " families") + " couldn't pay the reeve in full this week.", LogKind.Bad);
        }

        /// <summary>The reeve hands what he's collected to the bailiff, or straight to the castle when there's no bailiff.</summary>
        void ToBailiff(float coin, float grain)
        {
            Taxes.CollectedCoin += coin; Taxes.CollectedGrain += grain;
            var chest = TaxChest;
            if (chest != null && BailiffId >= 0) { chest.Coin += coin; chest.Stock[(int)Good.Grain] += grain; }
            else Receive(coin, grain);
        }

        /// <summary>The reeve's own household pays without fuss.</summary>
        void PayOwn(Building h)
        {
            var folk = h.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild).OrderByDescending(p => p.Coin).ToList();
            float g = Math.Min(h.OwedGrain, h.Stock[(int)Good.Grain]); h.Stock[(int)Good.Grain] -= g; h.OwedGrain -= g;
            float c = TakeCoin(folk, h.OwedCoin); h.OwedCoin -= c;
            h.Arrears = h.OwedCoin;
            ToBailiff(c, g);
        }

        static float TakeCoin(List<Citizen> folk, float owed)
        {
            float paid = 0;
            foreach (var p in folk) { float t = Math.Min(p.Coin, owed - paid); if (t <= 0) continue; p.Coin -= t; paid += t; if (paid >= owed - 0.0001f) break; }
            return paid;
        }

        /// <summary>Saturday: the bailiff carries the tax chest to the castle, keeping his share. A crooked one keeps a good deal more.</summary>
        void BailiffDelivers()
        {
            var b = Bailiff; var chest = TaxChest;
            if (b == null || chest == null || b.Health < 30) return;
            float coin = chest.Coin, grain = chest.Stock[(int)Good.Grain];
            if (coin < 0.01f && grain < 0.5f) return;
            float skim = Math.Max(0, 0.65f - b.Honesty) / 0.65f * Balance.SkimMax;
            float cut = Policy.BailiffCut + skim;
            float bc = coin * cut, bg = grain * cut;
            chest.Coin = 0; chest.Stock[(int)Good.Grain] -= grain;
            b.Coin += bc;
            var home = HomeOf(b); if (home != null) home.Stock[(int)Good.Grain] += bg;
            Taxes.BailiffCutCoin += bc; Taxes.BailiffCutGrain += bg;
            Receive(coin - bc, grain - bg);
        }

        void Receive(float coin, float grain)
        {
            Coin += coin; WeekIn += coin; PutInStores(Good.Grain, grain);
            Taxes.ReceivedCoin += coin; Taxes.ReceivedGrain += grain;
            WeekTaxCoin += coin; WeekTaxGrain += grain;
        }

        /// <summary>The year's accounts at Michaelmas, the end of autumn.</summary>
        void Michaelmas()
        {
            var t = Taxes;
            float handed = t.CollectedCoin + t.CollectedGrain * Balance.MerchantGrain;
            float kept = t.BailiffCutCoin + t.BailiffCutGrain * Balance.MerchantGrain;
            AddLog("Michaelmas accounts: the treasury received " + Money(t.ReceivedCoin) + " and " + Math.Round(t.ReceivedGrain) + " measures of grain in taxes this year.", LogKind.Crown);
            if (handed > 5 && kept > handed * (Policy.BailiffCut + 0.06f))
                AddLog("The king's clerks find the bailiff's accounts don't add up: about " + Money(kept - handed * Policy.BailiffCut) + " more than his share is missing.", LogKind.Bad);
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
            AddLog("Martinmas: the crown's tallage of " + Money(Policy.Tallage) + " was laid on " + n + " serf families. The reeve will collect it.", LogKind.Info);
        }

        /// <summary>Midwinter: the king keeps Christmas in the hall and feeds the whole village, if the granary can bear it.</summary>
        void Christmas()
        {
            int n = People.Count;
            float need = n * 1.5f;
            var r = Ruler;
            bool stingy = RulerIs(Trait.Greedy) && AverageLoyalty > 40;
            if (stingy || StoreStock(Good.Grain) + StoreStock(Good.Flour) < need + 40)
            {
                AddLog(stingy ? (r != null ? Title(r) : "The king") + " kept Christmas with the court alone. The village noticed." : "The granary was too low to keep a Christmas feast for the village.", LogKind.Bad);
                foreach (var p in People) if (!RoyalHousehold(p)) p.Loyalty = Clamp(p.Loyalty - 4, 0, 100);
                return;
            }
            float f = TakeFromStores(Good.Flour, need); TakeFromStores(Good.Grain, (need - f) / Balance.GrainFeeds);
            TakeFromStores(Good.Ale, n * 0.5f);
            foreach (var p in People) { p.Loyalty = Clamp(p.Loyalty + 8, 0, 100); p.Fed = 1; }
            AddLog("Christmas: " + (r != null ? Title(r) : "The king") + " feasted the whole village in the " + (Keep != null ? "great hall" : "pavilion") + ".", LogKind.Good);
        }

        // =====================================================================
        // Revolt
        // =====================================================================
        /// <summary>A village pushed too far rises: after two weeks with loyalty near nothing, the king is overthrown.</summary>
        void CheckRevolt()
        {
            if (AverageLoyalty < 12) Crown.LowDays++; else Crown.LowDays = 0;
            if (Crown.LowDays == 5) AddLog("There is open talk of rising against the crown.", LogKind.Bad);
            if (Crown.LowDays < Balance.RevoltDays || Crown.Stripped) return;
            Crown.Stripped = true;
            var k = Ruler;
            AddLog("The village rose. They burned the tax rolls and drove " + (k != null ? Title(k) : "the king") + " from " + ManorName + ".", LogKind.Bad);
        }

        // =====================================================================
        // The church
        // =====================================================================
        void PriestDied()
        {
            PriestId = -1;
            AddLog("The parish has no priest. The bishop will send another before long.", LogKind.Church);
        }

        void SendPriest()
        {
            if (PriestId >= 0 || !Chance(1 / 10.0)) return;
            var p = AddPerson(false, Pick(Defs.PeasantNames), Rnd(26, 45), Rank.Priest);
            p.Piety = Rnd(70, 95); PriestId = p.Id;
            AddLog("The bishop sent " + Title(p) + " to serve the parish.", LogKind.Church);
            Rehouse();
        }
    }
}
