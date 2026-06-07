using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public class Faction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; } // e.g. "LowerTaxes", "Independence", "IncreaseNobilityPower"
        public string LeaderGovernorId { get; set; }
        public List<string> MemberGovernorIds { get; set; } = new List<string>();
        
        public string DemandText { get; set; }
        public string MainReason { get; set; }
        
        public int PowerPercent { get; set; } // e.g. 0-100 relative to king
        public int Discontent { get; set; } = 0; // 0-100
        
        public int DaysUntilUltimatum { get; set; } = -1;
        public bool IsActive { get; set; } = true;
        public bool IsPreparingRebellion { get; set; } = false;
        public bool IsRebellionStarted { get; set; } = false;
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
