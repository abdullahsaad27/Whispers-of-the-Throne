using System;
using System.Collections.Generic;

namespace WhispersOfTheThrone.Models
{
    public class Faction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; } // e.g. "LowerTaxes", "Independence", "IncreaseNobilityPower", "VassalUltimatum"
        public string LeaderGovernorId { get; set; }
        public List<string> MemberGovernorIds { get; set; } = new List<string>();
        
        public string DemandText { get; set; }
        public string MainReason { get; set; }
        
        public int PowerPercent { get; set; } // e.g. 0-100 relative to king
        public int Discontent { get; set; } = 0; // 0-100
        
        public int DaysUntilUltimatum { get; set; } = -1;
        public bool IsUltimatumPending { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool IsPreparingRebellion { get; set; } = false;
        public bool IsRebellionStarted { get; set; } = false;

        // CK3-style: concession tracking
        public bool HasBeenBribed { get; set; } = false;
        public int BribeCount { get; set; } = 0;
        public bool HasBeenNegotiated { get; set; } = false;
        public bool HasConcededDemand { get; set; } = false; // partial concession made
        public int ConsiderationDays { get; set; } = 0; // days bought by negotiation/pressure
        public int IntimidationAttempts { get; set; } = 0; // how many times dread was used
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
