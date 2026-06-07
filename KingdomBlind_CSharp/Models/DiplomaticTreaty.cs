using System;

namespace KingdomBlind_CSharp.Models
{
    public class DiplomaticTreaty
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TreatyType { get; set; } // NonAggressionPact, DefensiveAlliance, OffensiveAlliance, TradeAgreement, PoliticalLoanAgreement, MilitarySupportAgreement, PeaceTreaty, TributeAgreement, MarriageAlliance
        public string KingdomAId { get; set; } // Could be the player's ID ("Player") or another kingdom
        public string KingdomBId { get; set; }
        public int StartDateDays { get; set; }
        public int EndDateDays { get; set; }
        public int DurationDays { get; set; }
        public bool IsActive { get; set; } = true;
        public string Terms { get; set; }
        public int TrustEffect { get; set; }
        public int OpinionEffect { get; set; }
        public int PrestigeEffect { get; set; }
        public int BreakPenalty { get; set; }
        public string Notes { get; set; }
    }
}
