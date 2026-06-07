using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public sealed class AiConversationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SpeakerId { get; set; } = "";
        public string SpeakerName { get; set; } = "";
        public string SpeakerRole { get; set; } = "";
        public string Text { get; set; } = "";
        public string DateText { get; set; } = "";
        public int DayNumber { get; set; }
        public bool IsKing { get; set; }
    }

    public sealed class AiConversationSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public AiAgentRole CharacterRole { get; set; } = AiAgentRole.RoyalNarrator;
        public AiProviderType ProviderType { get; set; } = AiProviderType.Disabled;
        public string Model { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public string LastUpdatedDate { get; set; } = "";
        public int LastDayNumber { get; set; }
        public int MaxMessages { get; set; } = 24;
        public List<AiConversationMessage> Messages { get; set; } = new List<AiConversationMessage>();
    }

    public sealed class AiMeetingRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Topic { get; set; } = "";
        public string Scope { get; set; } = "";
        public string KingStatement { get; set; } = "";
        public string DateText { get; set; } = "";
        public int DayNumber { get; set; }
        public List<string> ParticipantNames { get; set; } = new List<string>();
        public string Transcript { get; set; } = "";
        public string ProviderSummary { get; set; } = "";
    }

    public sealed class AiSessionReply
    {
        public string SessionId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public AiAgentRole CharacterRole { get; set; } = AiAgentRole.RoyalNarrator;
        public string Text { get; set; } = "";
        public bool UsedFallback { get; set; }
        public string ProviderName { get; set; } = "";
        public string Model { get; set; } = "";
    }
}
