using System;

namespace KingdomBlind_CSharp.Models
{
    public class PoliticalMemory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ActorType { get; set; } = "";
        public string ActorId { get; set; } = "";
        public string ActorName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Summary { get; set; } = "";
        public int CreatedDay { get; set; }
        public int ExpiresDay { get; set; }
        public int OpinionEffect { get; set; }
        public int TrustEffect { get; set; }
        public int FearEffect { get; set; }
        public int Severity { get; set; }
        public bool IsPositive { get; set; }
        public bool IsArchived { get; set; }
    }

    public class RoyalPromise
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PromiseType { get; set; } = "";
        public string TargetType { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public string Description { get; set; } = "";
        public string FulfillmentHint { get; set; } = "";
        public int CreatedDay { get; set; }
        public int DueDay { get; set; }
        public int TrustReward { get; set; } = 10;
        public int BreachPenalty { get; set; } = 15;
        public bool IsFulfilled { get; set; }
        public bool IsBroken { get; set; }
    }

    public class LivingRealmEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventType { get; set; } = "";
        public string ActorType { get; set; } = "";
        public string ActorId { get; set; } = "";
        public string ActorName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string CouncilAdvice { get; set; } = "";
        public string DateText { get; set; } = "";
        public int CreatedDay { get; set; }
        public int Severity { get; set; }
        public bool RequiresPause { get; set; }
        public bool RequiresDecision { get; set; } = true;
        public bool IsResolved { get; set; }
    }
}
