using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public enum AiAgentRole
    {
        FirstMinister,
        Spymaster,
        MilitaryCommander,
        Cleric,
        DiplomaticAdvisor,
        Governor,
        FactionLeader,
        NeighborRuler,
        SpouseQueen,
        Heir,
        MerchantRepresentative,
        RoyalNarrator
    }

    public enum AiAuthorityLevel
    {
        None,
        Advisor,
        LimitedDelegate,
        TrustedDelegate,
        RoyalRightHand,
        Rogue
    }

    public enum AiActionType
    {
        BuildSpyNetwork,
        ImproveCounterIntelligence,
        InvestigateGovernor,
        DisruptFaction,
        ProtectHeir,
        ReviewSpymasterReports,
        SendReliefToProvince,
        NegotiateMerchantLoan,
        ProtectTradeRoute,
        RecommendConstruction,
        RequestCouncilMeeting,
        WarnAboutSuccessionRisk,
        ProposeMarriageAlliance,
        ImproveClergyRelations,
        PrepareDefense,
        MoveArmyRecommendation,
        SendDiplomaticMessage,
        OfferPeaceTerms,
        SupportHeir,
        CalmAngryGovernor,
        OrganizeSeasonalMarket,
        EscortTradeCaravan
    }

    public enum AiActionTargetType
    {
        None,
        Realm,
        Council,
        Province,
        Governor,
        Faction,
        NeighborKingdom,
        SpyNetwork,
        TradeRoute,
        Heir,
        Spouse,
        Army
    }

    public enum AiProposalStatus
    {
        Pending,
        Approved,
        Rejected,
        Deferred,
        Executed,
        Failed,
        Cancelled
    }

    public enum AiDialogueLengthLevel
    {
        Brief,
        Normal,
        Detailed
    }

    public sealed class AiAgentProfile
    {
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string SourceId { get; set; } = "";
        public AiAgentRole Role { get; set; } = AiAgentRole.RoyalNarrator;
        public AiAuthorityLevel AuthorityLevel { get; set; } = AiAuthorityLevel.Advisor;
        public int Loyalty { get; set; } = 50;
        public int Trust { get; set; } = 50;
        public int Ambition { get; set; } = 40;
        public int RiskTolerance { get; set; } = 40;
        public string MoralStyle { get; set; } = "Pragmatic";
        public string PreferredStrategy { get; set; } = "Balance";
        public List<AiActionType> AllowedActionTypes { get; set; } = new List<AiActionType>();
        public int MonthlyBudget { get; set; } = 0;
        public Dictionary<string, int> Cooldowns { get; set; } = new Dictionary<string, int>();
        public bool IsAutonomous { get; set; } = false;
        public int RequiresApprovalAboveRisk { get; set; } = 35;
        public List<string> LastActions { get; set; } = new List<string>();
        public List<string> MemoryNotes { get; set; } = new List<string>();
    }

    public sealed class DelegatedAuthoritySettings
    {
        public Dictionary<string, AiAuthorityLevel> RoleAuthorityLevels { get; set; } = new Dictionary<string, AiAuthorityLevel>();
        public bool AllowAutonomousActions { get; set; } = false;
        public int MaxAutonomousMonthlyBudget { get; set; } = 200;
        public int AutonomousBudgetSpentThisMonth { get; set; } = 0;
        public int BudgetMonthKey { get; set; } = 0;
        public List<string> DisabledSimilarProposalKeys { get; set; } = new List<string>();
    }

    public sealed class AiActionRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AgentCharacterId { get; set; } = "";
        public string AgentName { get; set; } = "";
        public AiAgentRole Role { get; set; } = AiAgentRole.RoyalNarrator;
        public AiActionType ActionType { get; set; } = AiActionType.RequestCouncilMeeting;
        public AiActionTargetType TargetType { get; set; } = AiActionTargetType.None;
        public string TargetId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public string Reason { get; set; } = "";
        public string ExpectedBenefit { get; set; } = "";
        public int EstimatedRisk { get; set; } = 10;
        public int GoldCost { get; set; } = 0;
        public int TimeCostDays { get; set; } = 0;
        public bool RequiresKingApproval { get; set; } = true;
        public int Confidence { get; set; } = 50;
        public string SpokenJustification { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public int CreatedDay { get; set; } = 0;
        public AiProposalStatus Status { get; set; } = AiProposalStatus.Pending;
        public string StatusReason { get; set; } = "";
        public string SimilarityKey { get; set; } = "";
    }

    public sealed class AiActionLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Date { get; set; } = "";
        public int DayNumber { get; set; }
        public string AgentName { get; set; } = "";
        public AiAgentRole Role { get; set; } = AiAgentRole.RoyalNarrator;
        public string ActionTaken { get; set; } = "";
        public int Cost { get; set; }
        public string Result { get; set; } = "";
        public int Risk { get; set; }
        public bool WasSuccessful { get; set; }
        public bool PlayerCanRespond { get; set; }
    }

    public sealed class AiAgentContext
    {
        public string AgentName { get; set; } = "";
        public AiAgentRole Role { get; set; } = AiAgentRole.RoyalNarrator;
        public string Summary { get; set; } = "";
        public List<string> KnownFacts { get; set; } = new List<string>();
        public List<string> UnknownLimits { get; set; } = new List<string>();
        public List<string> RecentMemories { get; set; } = new List<string>();

        public override string ToString()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Summary)) parts.Add(Summary);
            if (KnownFacts.Count > 0) parts.Add("المعروف للشخصية:\n" + string.Join("\n", KnownFacts));
            if (UnknownLimits.Count > 0) parts.Add("حدود المعرفة:\n" + string.Join("\n", UnknownLimits));
            if (RecentMemories.Count > 0) parts.Add("ذاكرة مختصرة:\n" + string.Join("\n", RecentMemories));
            return string.Join("\n\n", parts);
        }
    }
}
