using System.Collections.Generic;

namespace WhispersOfTheThrone.Models
{
    public enum AiProviderType
    {
        Disabled,
        Local,
        OpenAICompatible,
        Gemini,
        OpenRouter,
        Mistral,
        Grok,
        Groq,
        OpenCode,
        CustomHttp
    }

    public sealed class AiProviderSettings
    {
        public AiProviderType ProviderType { get; set; } = AiProviderType.Disabled;
        public string Endpoint { get; set; } = "";
        public string Model { get; set; } = "";
        public string ApiKeyEnvironmentVariable { get; set; } = "";
        public bool AllowOnlineRequests { get; set; } = false;
        public int TimeoutSeconds { get; set; } = 20;
        public Dictionary<string, string> CharacterModelOverrides { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, ProviderConfig> PerProviderConfigs { get; set; } = new Dictionary<string, ProviderConfig>();
        public List<string> HiddenProviders { get; set; } = new List<string>();
    }

    public sealed class AiActorSettings
    {
        public bool SmartDialoguesEnabled { get; set; } = true;
        public AiDialogueLengthLevel DialogueLengthLevel { get; set; } = AiDialogueLengthLevel.Normal;
        public bool AllowAutonomousActions { get; set; } = false;
        public int MaxAutonomousMonthlyBudget { get; set; } = 200;
        public bool UseSuperTonicForAiDialogue { get; set; } = false;
        public bool ApplyToSpouses { get; set; } = true;
        public bool ApplyToMinisters { get; set; } = true;
        public bool ApplyToHeirs { get; set; } = true;
        public bool ApplyToGovernors { get; set; } = true;
        public bool ApplyToFactions { get; set; } = true;
        public bool ApplyToNeighborRulers { get; set; } = true;
        public bool ApplyToEnemies { get; set; } = true;
        public bool ApplyToOtherCharacters { get; set; } = false;
        public bool AllowAiMinisterDecisions { get; set; } = false;
        public bool AllowAiSpouseDecisions { get; set; } = false;
        public bool AllowAiNeighborDecisions { get; set; } = false;
        public bool AllowAiGovernorDecisions { get; set; } = false;
        public bool AllowAiFactionDecisions { get; set; } = false;
        public bool AllowAiNeighborRealmManagement { get; set; } = false;
        public bool OpenRouterFreeOnly { get; set; } = false;
        public string DefaultModel { get; set; } = "";
        public Dictionary<string, string> RoleModelOverrides { get; set; } = new Dictionary<string, string>();
        public List<CharacterProviderAssignment> CharacterProviderAssignments { get; set; } = new List<CharacterProviderAssignment>();
    }

    public sealed class AiModelDescriptor
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Provider { get; set; } = "";
        public bool IsLocal { get; set; }
        public string Source { get; set; } = "";
    }

    public sealed class AiDialogueRequest
    {
        public string CharacterName { get; set; } = "";
        public string CharacterRole { get; set; } = "";
        public string Context { get; set; } = "";
        public string RulerName { get; set; } = "";
        public List<string> PoliticalMemories { get; set; } = new List<string>();
    }

    public sealed class AiDialogueResponse
    {
        public string Text { get; set; } = "";
        public bool UsedFallback { get; set; } = true;
        public string ProviderName { get; set; } = "Fallback";
    }

    public sealed class AiDecisionRequest
    {
        public string ActorName { get; set; } = "";
        public string ActorType { get; set; } = "";
        public string DecisionContext { get; set; } = "";
        public Dictionary<string, int> Factors { get; set; } = new Dictionary<string, int>();
    }

    public sealed class AiDecisionResponse
    {
        public string DecisionKey { get; set; } = "";
        public string Explanation { get; set; } = "";
        public bool UsedFallback { get; set; } = true;
    }

    public sealed class ProviderConfig
    {
        public string ApiKeyEnvironmentVariable { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string Model { get; set; } = "";
        public bool AllowOnlineRequests { get; set; } = false;
        public int TimeoutSeconds { get; set; } = 20;
        public List<string> CustomModels { get; set; } = new List<string>();
        public List<string> RemovedModels { get; set; } = new List<string>();
        public string DisplayAlias { get; set; } = "";
    }

    public sealed class CharacterProviderAssignment
    {
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public AiProviderType ProviderType { get; set; } = AiProviderType.Disabled;
        public string Model { get; set; } = "";
    }
}
