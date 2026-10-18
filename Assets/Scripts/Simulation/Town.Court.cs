using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>The hierarchy: the king above, the lord and his heirs, the priest, the reeve the villagers choose.</summary>
    public partial class Town
    {
        /// <summary>The lord's heir by the custom of the realm: his eldest son, then his eldest daughter, then a brother.</summary>
        public Citizen HeirOf(Citizen lord)
        {
            if (lord == null) return null;
            var kids = People.Where(p => (p.FatherId == lord.Id || p.MotherId == lord.Id) && p.IsNoble).ToList();
            var son = kids.Where(p => !p.Female).OrderByDescending(p => p.Age).FirstOrDefault();
            if (son != null) return son;
            var daughter = kids.Where(p => p.Female).OrderByDescending(p => p.Age).FirstOrDefault();
            if (daughter != null) return daughter;
            return People.Where(p => p.Rank == Rank.Noble && p.Family == lord.Family && p.Id != lord.PartnerId && !p.IsChild).OrderByDescending(p => p.Age).FirstOrDefault();
        }
        public Citizen Heir { get { return HeirOf(Lord); } }

        void Succession(Citizen dead)
        {
            var heir = HeirOf(dead);
            var widow = People.FirstOrDefault(p => p.Id == dead.PartnerId);
            if (heir != null)
            {
                heir.Rank = Rank.Lord; LordId = heir.Id; Generation++;
                if (dead.PartnerId >= 0) { }
                if (heir.IsChild)
                {
                    var regent = widow ?? People.Where(p => p.Rank == Rank.Noble && !p.IsChild).OrderByDescending(p => p.Age).FirstOrDefault()
                                 ?? People.FirstOrDefault(p => p.Rank == Rank.Knight);
                    RegentId = regent != null ? regent.Id : -1;
                    AddLog(heir.First + ", a child of " + (int)heir.Age + ", is lord of " + ManorName + " now." + (regent != null ? " " + Title(regent) + " rules as regent until " + (heir.Female ? "she" : "he") + " comes of age." : ""), LogKind.Crown);
                }
                else AddLog(Title(heir) + " inherits the manor of " + ManorName + " and swears fealty to " + Crown.King + ".", LogKind.Crown);
                if (widow != null) widow.Rank = Rank.Noble;
                // a smooth succession pleases the king; a child lord less so
                Crown.Favour = Clamp(Crown.Favour + (heir.IsChild ? -8 : 2), 0, 100);
                foreach (var p in People) if (!HouseholdOfLord(p)) p.Loyalty = Clamp(p.Loyalty * 0.85f + 55 * 0.15f, 0, 100);
                return;
            }
            // no heir: the manor escheats to the crown, and the king grants it to someone new
            string house = Pick(Defs.NobleHouses.Where(h => h != Dynasty).ToList());
            var nl = AddPerson(Chance(0.85) ? false : true, house, Rnd(24, 40), Rank.Lord);
            nl.Trait = (Trait)Rng.Next(6);
            nl.HomeId = Seat != null ? Seat.Id : -1;
            LordId = nl.Id; RegentId = -1; Dynasty = house; Generation = 1;
            if (widow != null) { widow.Rank = Rank.Noble; }
            Crown.Favour = 50;
            AddLog(Title(dead) + " left no heir. The manor fell back to the crown, and " + Crown.King + " granted it to " + Title(nl) + " of the house of " + house + ".", LogKind.Crown);
            foreach (var p in People) if (!HouseholdOfLord(p)) p.Loyalty = Clamp(p.Loyalty * 0.6f + 45 * 0.4f, 0, 100);
        }

        /// <summary>A regent hands over when the young lord comes of age.</summary>
        void ComeOfAge()
        {
            var l = Lord;
            if (l == null || RegentId < 0 || l.IsChild) return;
            var r = Regent;
            AddLog(Title(l) + " has come of age and takes the manor into " + (l.Female ? "her" : "his") + " own hands." + (r != null ? " " + Title(r) + "'s regency is over." : ""), LogKind.Crown);
            RegentId = -1;
        }

        /// <summary>Each spring the villagers choose a reeve from among the serfs to oversee the lord's fields.
        /// They pick someone respected: steady, loyal enough, not too young.</summary>
        void ChooseReeve(bool vacancy)
        {
            var old = Reeve;
            if (old != null) old.Rank = Rank.Serf;
            var pool = People.Where(p => !p.IsChild && !p.Female && p.Age >= 20 && p.Age < 62 && (p.Rank == Rank.Serf || p.Rank == Rank.Reeve) && p.Health > 40).ToList();
            if (pool.Count == 0) pool = People.Where(p => !p.IsChild && p.Age >= 20 && p.Rank == Rank.Serf && p.Health > 40).ToList();
            if (pool.Count == 0) { ReeveId = -1; return; }
            var pick = pool.OrderByDescending(p => p.Loyalty * 0.6f + Math.Min(p.Age, 50) * 0.5f + p.Piety * 0.2f + p.Seed * 10 + (old != null && p.Id == old.Id ? 6 : 0)).First();
            pick.Rank = Rank.Reeve; ReeveId = pick.Id;
            if (old != null && old.Id == pick.Id) AddLog("The village kept " + pick.Name + " on as reeve for another year.", LogKind.Info);
            else AddLog("The villagers chose " + pick.Name + " as reeve" + (vacancy ? " to fill the office." : " for the year.") + " He'll see the lord's fields worked.", LogKind.Info);
        }

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

        // =====================================================================
        // The crown
        // =====================================================================
        /// <summary>At Michaelmas, the end of autumn, the lord owes the king his tribute.</summary>
        void Michaelmas()
        {
            float due = TributeDue;
            float paid = Math.Min(Math.Max(0, Coin), due);
            Coin -= paid; WeekOut += paid;
            Crown.LastDue = due; Crown.LastTribute = paid; Crown.TributeYear = Year;
            var r = Ruler;
            float frac = due <= 0 ? 1 : paid / due;
            if (frac >= 0.999f)
            {
                Crown.Favour = Clamp(Crown.Favour + 10, 0, 100);
                AddLog("Michaelmas: " + (r != null ? Title(r) : "The lord") + " sent " + Crown.King + " his tribute of " + Money(paid) + " in full.", LogKind.Crown);
                if (Crown.Favour >= 90 && Chance(0.5))
                {
                    float gift = 20 + Adults.Count();
                    Coin += gift; WeekIn += gift;
                    AddLog(Crown.King + " is well pleased with " + ManorName + " and sent back a purse of " + Money(gift) + ".", LogKind.Crown);
                }
            }
            else
            {
                float loss = paid <= 0 ? 30 : 30 * (1 - frac);
                if (RulerIs(Trait.Brave)) loss *= 0.75f;
                Crown.Favour = Clamp(Crown.Favour - loss, 0, 100);
                AddLog("Michaelmas: the treasury could only send " + Money(paid) + " of the " + Money(due) + " the king is owed. " + Crown.King + " is displeased.", LogKind.Bad);
            }
            CheckStripped();
        }

        /// <summary>Royal favour drifts with how the manor is run: a loyal, devout village with a keep pleases the king; unrest and empty fields don't.</summary>
        void WeeklyFavour()
        {
            float loyal = AverageLoyalty, d = 0;
            if (loyal > 65) d += 1; else if (loyal < 35) d -= 1.5f;
            if (Keep == null && Year > Balance.StartYear + 1) d -= 0.5f;
            if (Church != null) d += 0.25f;
            if (RulerIs(Trait.Brave)) d += 0.25f;
            Crown.Favour = Clamp(Crown.Favour + d, 0, 100);
            CheckStripped();
        }

        void CheckStripped()
        {
            if (Crown.Stripped || Crown.Favour > 0) return;
            Crown.Stripped = true;
            AddLog(Crown.King + " has lost all patience. He has taken back the manor of " + ManorName + " and given it to another house.", LogKind.Bad);
        }

        /// <summary>A lord without a wife is a worry to the king: he arranges a match, and the bride arrives with a dowry.</summary>
        void Matchmaking()
        {
            var l = Lord;
            if (l == null || l.IsChild || l.PartnerId >= 0 || l.Age > 55) { Crown.SuitorDay = -1; return; }
            if (Crown.SuitorDay < 0) { Crown.SuitorDay = Day + Rng.Next(15, 35); AddLog(Crown.King + " has promised to find " + Title(l) + " a match.", LogKind.Crown); return; }
            if (Day < Crown.SuitorDay) return;
            var house = Pick(Defs.NobleHouses.Where(h => h != l.Family).ToList());
            var bride = AddPerson(!l.Female, house, Math.Max(17, l.Age - Rnd(2, 12)), Rank.Noble);
            bride.Trait = (Trait)Rng.Next(6);
            Marry(l, bride);
            if (!l.Female) bride.Family = l.Family; else l.Family = l.Family;
            bride.HomeId = Seat != null ? Seat.Id : -1;
            float dowry = 15 + Rng.Next(0, 16);
            Coin += dowry; WeekIn += dowry;
            Crown.SuitorDay = -1;
            Crown.Favour = Clamp(Crown.Favour + 3, 0, 100);
            AddLog(Title(l) + " married " + bride.First + " of the house of " + house + ", who brought a dowry of " + Money(dowry) + ".", LogKind.Crown);
            Rehouse();
        }

        // =====================================================================
        // Feast days and dues
        // =====================================================================
        /// <summary>Martinmas, the start of winter: the lord takes tallage from every serf household.</summary>
        void Martinmas()
        {
            if (Policy.Tallage <= 0) return;
            float got = 0; int short_ = 0;
            foreach (var home in Buildings.Where(b => b.Built && b.Type == BuildingType.Cottage && b.Residents.Count > 0))
            {
                var folk = home.Residents.Select(GetCitizen).Where(p => p != null && !p.IsChild && p.Rank != Rank.Freeman).ToList();
                if (folk.Count == 0) continue;
                float owed = Policy.Tallage, paid = 0;
                foreach (var p in folk.OrderByDescending(p => p.Coin)) { float t = Math.Min(p.Coin, owed - paid); p.Coin -= t; paid += t; }
                if (paid < owed - 0.01f)
                {
                    short_++;
                    // what can't be paid in coin is taken in grain
                    float grain = Math.Min(home.Stock[(int)Good.Grain], (owed - paid) / Balance.MerchantGrain);
                    home.Stock[(int)Good.Grain] -= grain; PutInStores(Good.Grain, grain);
                    foreach (var p in folk) p.Loyalty = Clamp(p.Loyalty - 6, 0, 100);
                }
                foreach (var p in folk) p.Loyalty = Clamp(p.Loyalty - Policy.Tallage * 2, 0, 100);
                got += paid;
            }
            Coin += got; WeekIn += got;
            AddLog("Martinmas: the lord's tallage brought in " + Money(got) + "." + (short_ > 0 ? " " + short_ + " families couldn't pay in coin and gave up grain instead." : ""), LogKind.Info);
        }

        /// <summary>Midwinter: the lord keeps Christmas in the hall and feeds the whole village, if the granary can bear it.</summary>
        void Christmas()
        {
            int n = People.Count;
            float need = n * 1.5f;
            var r = Ruler;
            bool stingy = RulerIs(Trait.Greedy) && AverageLoyalty > 40;
            if (stingy || StoreStock(Good.Grain) + StoreStock(Good.Flour) < need + 40)
            {
                AddLog(stingy ? (r != null ? Title(r) : "The lord") + " kept Christmas with the household alone. The village noticed." : "The granary was too low to keep a Christmas feast for the village.", LogKind.Bad);
                foreach (var p in People) if (!HouseholdOfLord(p)) p.Loyalty = Clamp(p.Loyalty - 4, 0, 100);
                return;
            }
            float f = TakeFromStores(Good.Flour, need); TakeFromStores(Good.Grain, (need - f) / Balance.GrainFeeds);
            TakeFromStores(Good.Ale, n * 0.5f);
            foreach (var p in People) { p.Loyalty = Clamp(p.Loyalty + 8, 0, 100); p.Fed = 1; }
            AddLog("Christmas: " + (r != null ? Title(r) : "The lord") + " feasted the whole village in the " + (Keep != null ? "great hall" : "pavilion") + ".", LogKind.Good);
        }
    }
}
