using System;

namespace KingdomBlind_CSharp.Models
{
    public class IntelligenceOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string OperationType { get; set; } // e.g., Scout, DisbandFaction, MonitorGovernor, CounterIntelligence
        public string TargetType { get; set; } // Internal, External, Faction, Court
        public string TargetId { get; set; }
        public string AssignedSpyNetworkId { get; set; }
        
        public int DaysRemaining { get; set; }
        public int GoldCost { get; set; }
        public int SuccessChance { get; set; }
        public int ExposureChance { get; set; }
        
        // Preparing, Active, Completed, Failed, Exposed, Cancelled
        public string Status { get; set; } = "Active"; 
        
        public string ResultSummary { get; set; } = "";
        
        public bool RequiresPause { get; set; } = true;
    }
}
