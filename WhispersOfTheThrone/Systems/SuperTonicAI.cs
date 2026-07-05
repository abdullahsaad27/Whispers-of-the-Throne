using System.Linq;
using WhispersOfTheThrone.Models;

namespace WhispersOfTheThrone.Systems
{
    public static class SuperTonicAI
    {
        public static string GenerateDialogue(string character, GameState state, string context)
        {
            var config = AppConfig.Load();
            config.AiProvider ??= new AiProviderSettings();
            config.AiActors ??= new AiActorSettings();

            // Backward compatibility: the old toggle now means "use a local AI style"
            // until a real provider backend is configured.
            if (config.UseSuperTonic && config.AiProvider.ProviderType == AiProviderType.Disabled)
                config.AiProvider.ProviderType = AiProviderType.Local;

            if (!config.AiActors.SmartDialoguesEnabled)
            {
                var disabledSettings = new AiProviderSettings { ProviderType = AiProviderType.Disabled };
                return AiProviderFactory.Create(disabledSettings).GenerateDialogue(state, new AiDialogueRequest
                {
                    CharacterName = character,
                    CharacterRole = character,
                    Context = context ?? "",
                    RulerName = state?.RulerName ?? ""
                }).Text;
            }

            if (!AiRuntimePolicySystem.IsEnabledForLabel(config.AiActors, character))
            {
                var disabledSettings = new AiProviderSettings { ProviderType = AiProviderType.Disabled };
                return AiProviderFactory.Create(disabledSettings).GenerateDialogue(state, new AiDialogueRequest
                {
                    CharacterName = character,
                    CharacterRole = character,
                    Context = context ?? "",
                    RulerName = state?.RulerName ?? ""
                }).Text;
            }

            var request = new AiDialogueRequest
            {
                CharacterName = character,
                CharacterRole = character,
                Context = context ?? "",
                RulerName = state?.RulerName ?? ""
            };

            if (state?.PoliticalMemories != null)
            {
                request.PoliticalMemories = state.PoliticalMemories
                    .Where(m => !m.IsArchived)
                    .OrderByDescending(m => m.CreatedDay)
                    .Take(3)
                    .Select(m => $"{m.ActorName}: {m.Summary}")
                    .ToList();
            }

            return AiProviderFactory.Create(config.AiProvider).GenerateDialogue(state, request).Text;
        }
    }
}
