using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class EventChainSystem
    {
        public static EventChain StartChain(GameState state, string chainType, string actorCharacterId, string actorName, params EventChainStep[] steps)
        {
            state.ReconcileOldSaves();
            var chain = new EventChain
            {
                ChainType = chainType,
                ActorCharacterId = actorCharacterId ?? "",
                ActorName = actorName ?? ""
            };
            chain.Steps.AddRange(steps);
            state.EventChains.Add(chain);
            return chain;
        }

        public static string GetCurrentStepText(GameState state, EventChain chain)
        {
            if (chain == null || chain.Steps.Count == 0 || chain.CurrentStepIndex >= chain.Steps.Count)
                return "لا توجد خطوة سردية حالية.";

            var step = chain.Steps[chain.CurrentStepIndex];
            return LocalizationSystem.Get(step.TitleKey) + "\n" + LocalizationSystem.Get(step.BodyKey);
        }

        public static GameActionResult ResolveCurrentStep(GameState state, string chainId, string choiceKey)
        {
            var result = new GameActionResult { Title = "سلسلة أحداث" };
            state.ReconcileOldSaves();
            var chain = state.EventChains.FirstOrDefault(c => c.Id == chainId);
            if (chain == null)
            {
                result.Success = false;
                result.MainMessage = "لم يتم العثور على سلسلة الأحداث.";
                return result;
            }

            if (chain.IsComplete)
            {
                result.Success = false;
                result.MainMessage = "سلسلة الأحداث مكتملة بالفعل.";
                return result;
            }

            chain.Steps[chain.CurrentStepIndex].IsResolved = true;
            chain.CurrentStepIndex++;
            chain.IsComplete = chain.CurrentStepIndex >= chain.Steps.Count;

            result.Success = true;
            result.MainMessage = chain.IsComplete
                ? $"اكتملت سلسلة أحداث {chain.ChainType}."
                : GetCurrentStepText(state, chain);
            return result;
        }

        public static GameActionResult ProcessDailyChains(GameState state)
        {
            var result = new GameActionResult { Title = "سلاسل الأحداث", Success = true, ShouldNarrate = false };
            state.ReconcileOldSaves();
            var pending = state.EventChains.FirstOrDefault(c => !c.IsComplete && c.Steps.Any(s => s.RequiresDecision && !s.IsResolved));
            if (pending == null)
                return result;

            result.ShouldNarrate = true;
            result.ShouldPauseTime = true;
            result.MainMessage = GetCurrentStepText(state, pending);
            return result;
        }
    }
}
