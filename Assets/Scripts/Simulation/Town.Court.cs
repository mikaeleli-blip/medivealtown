using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>The hierarchy: the king and his heirs, the steward he appoints, the reeve the villagers choose, and the taxes
    /// that climb from the serfs' houses through the reeve and the steward to the castle.</summary>
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
