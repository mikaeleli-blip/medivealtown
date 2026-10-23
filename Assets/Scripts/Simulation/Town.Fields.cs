using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalTown.Simulation
{
    /// <summary>
    /// The village's fields. An open field holds shares for several serf families: they work it together, and each family
    /// takes the crop of its own strips. An old strip field is a field with one share.
    /// </summary>
    public partial class Town
    {
        public static bool IsFarm(BuildingType t) { return t == BuildingType.Strip || t == BuildingType.OpenField; }
        /// <summary>Family shares a field holds.</summary>
        public int Slots(Building f) { return f.Type == BuildingType.Strip ? 1 : f.Type == BuildingType.OpenField ? f.Def.Shares : 0; }
        void FitShares(Building f) { if (f.Shares == null) f.Shares = new List<int>(); while (f.Shares.Count < f.Def.Shares) f.Shares.Add(-1); }
        public int HolderAt(Building f, int i) { if (f.Type == BuildingType.Strip) return f.HolderId; FitShares(f); return f.Shares[i]; }
        void SetHolder(Building f, int i, int id) { if (f.Type == BuildingType.Strip) f.HolderId = id; else { FitShares(f); f.Shares[i] = id; } }
        public int HeldShares(Building f) { int n = 0; for (int i = 0; i < Slots(f); i++) if (HolderAt(f, i) >= 0) n++; return n; }
        public int SharesOf(Building f, int head) { int n = 0; for (int i = 0; i < Slots(f); i++) if (HolderAt(f, i) == head) n++; return n; }
        /// <summary>The households holding shares, once each.</summary>
        public List<int> HoldersOf(Building f) { var l = new List<int>(); for (int i = 0; i < Slots(f); i++) { int h = HolderAt(f, i); if (h >= 0 && !l.Contains(h)) l.Add(h); } return l; }
        /// <summary>What a field yields fully tended: a strip's worth for each share held (the lord's demesne its own).</summary>
        float FieldYield(Building f)
        {
            if (f.Type == BuildingType.Demesne) return Balance.DemesneYield;
            return Balance.StripYield * (f.Type == BuildingType.OpenField ? HeldShares(f) : 1);
        }
        /// <summary>How fast a day's work grows the crop: a big field needs as many hands as the families on it.</summary>
        float FieldGrowth(Building f)
        {
            if (f.Type == BuildingType.Demesne) return Balance.DemesneGrowth;
            return Balance.StripGrowth / Math.Max(1, f.Type == BuildingType.OpenField ? HeldShares(f) : 1);
        }
    }
}
