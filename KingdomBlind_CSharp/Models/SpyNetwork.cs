using System;

namespace KingdomBlind_CSharp.Models
{
    public class SpyNetwork
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string TargetType { get; set; } // InternalProvince, RoyalCourt, ForeignKingdom, Faction
        public string TargetId { get; set; }
        
        public int Strength { get; set; } = 10;
        public int Secrecy { get; set; } = 10;
        public int Infiltration { get; set; } = 5;
        public int Analysis { get; set; } = 5;
        public int ExposureRisk { get; set; } = 0;
        
        public bool IsCompromised { get; set; } = false;
        
        public string LastReport { get; set; } = "لا توجد تقارير بعد.";
        public int DaysUntilNextReport { get; set; } = 7;
        
        public int ActiveOperationsCount { get; set; } = 0;
    }
}
