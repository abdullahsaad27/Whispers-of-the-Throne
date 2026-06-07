using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public class Spouse
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public int Age { get; set; } = 20;
        public bool IsDead { get; set; } = false;
        
        // OriginType: ForeignKingdom, GovernorFamily, NobleFamily, LocalProvince
        public string OriginType { get; set; }
        public string OriginId { get; set; } // The name of the kingdom, province, or governor
        
        public int OpinionOfKing { get; set; } = 50;
        public int Trust { get; set; } = 50;
        public int Influence { get; set; } = 10;
        public int Ambition { get; set; } = 30;
        public int Jealousy { get; set; } = 0;
        
        public int PoliticalSkill { get; set; } = 5;
        public int DiplomacySkill { get; set; } = 5;
        public int IntrigueSkill { get; set; } = 5;
        
        public bool IsMotherOfHeir { get; set; } = false;
        public string SupportedHeirId { get; set; } = null;
        
        public string RelatedProvinceId { get; set; } = null;
        public string RelatedGovernorId { get; set; } = null;

        public bool IsPregnant { get; set; } = false;
        public int PregnancyDaysLeft { get; set; } = 0;

        public string? CurrentTask { get; set; } = null;
        public string? DutyTargetId { get; set; } = null;
        public string? DutyTargetName { get; set; } = null;
        public int DutyDaysRemaining { get; set; } = 0;

        public string? PreferredChildId { get; set; } = null;
        public string CourtGoal { get; set; } = "";
        public int DaysUntilNextCourtMove { get; set; } = 30;
    }
}
