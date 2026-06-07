using System;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class CalendarTimeSystem
    {
        public static GameActionResult AdvanceDay(GameState state)
        {
            var result = new GameActionResult { Success = true, Title = "يوم جديد" };
            bool wasPaused = state.Time.IsPaused;
            
            state.ReconcileOldSaves();
            DiplomacySystem.SynchronizeDiplomacyState(state);
            state.Time.AddDays(1);
            
            StringBuilder dailyReport = new StringBuilder();
            bool hasReport = false;

            var economyRes = EconomySystem.ProcessDailyEconomy(state);
            var armyRes = ArmyCommandSystem.ProcessDailyArmy(state);
            var warfareRes = WarfareSystem.ProcessDailySieges(state);
            var factionRes = FactionSystem.ProcessDailyFactions(state);
            var intelRes = IntelligenceSystem.ProcessDailyIntelligence(state);
            var firstMinisterRes = FirstMinisterSystem.ProcessDailyFirstMinister(state);
            var dynastyRes = DynastySystem.ProcessDailyDynasty(state);
            var diplomacyRes = DiplomacySystem.ProcessDailyTreaties(state);
            var disasterRes = DisasterSystem.ProcessDailyDisasters(state);
            var eventRes = EventSystem.ProcessDailyRandomEvents(state);
            var livingRealmRes = LivingRealmSystem.ProcessDailyLivingRealm(state);
            var directorRes = RoyalDirectorSystem.ProcessDailyDirector(state);
            var schemeRes = GrandStrategySystem.ProcessDailySchemes(state);
            var chainRes = EventChainSystem.ProcessDailyChains(state);
            AiAgentSystem.DecrementCooldowns(state);
            
            GameActionResult aiRes = null;
            GameActionResult personalObjectiveRes = null;
            GameActionResult aiCharacterRes = null;
            GameActionResult aiProposalRes = null;
            GameActionResult aiDelegationRes = null;
            GameActionResult aiWorldActorRes = null;
            if (state.Time.Day == 30)
            {
                aiRes = LivingRealmSystem.ProcessMonthlyAI(state);
                personalObjectiveRes = PersonalObjectiveSystem.ProcessMonthlyObjectives(state);
                aiCharacterRes = AiAutonomousCharacterSystem.ProcessMonthlyAiCharacters(state);
                aiProposalRes = AiProposalQueue.GenerateMonthlyProposals(state);
                aiDelegationRes = AiProposalQueue.ProcessAutonomousDelegates(state);
                aiWorldActorRes = AiWorldActorSystem.ProcessMonthlyWorldActors(state);
            }

            if (aiRes != null && aiRes.ShouldNarrate)
            {
                hasReport = true;
                if (aiRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(aiRes.MainMessage);
            }

            if (economyRes != null && economyRes.ShouldNarrate)
            {
                hasReport = true;
                if (economyRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(economyRes.MainMessage);
            }
            if (!string.IsNullOrEmpty(armyRes))
            {
                hasReport = true;
                if (armyRes.Contains("هجوم")) result.ShouldPauseTime = true;
                dailyReport.AppendLine(armyRes);
            }
            if (!string.IsNullOrEmpty(warfareRes))
            {
                hasReport = true;
                if (warfareRes.Contains("اقتحام") || warfareRes.Contains("استسلام"))
                    result.ShouldPauseTime = true;
                dailyReport.AppendLine(warfareRes);
            }
            if (factionRes != null && factionRes.ShouldNarrate)
            {
                hasReport = true;
                if (factionRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(factionRes.MainMessage);
            }
            if (intelRes != null && !string.IsNullOrEmpty(intelRes.MainMessage))
            {
                hasReport = true;
                if (intelRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(intelRes.MainMessage);
                foreach(var w in intelRes.Warnings)
                {
                    dailyReport.AppendLine(w);
                }
            }
            if (firstMinisterRes != null && firstMinisterRes.ShouldNarrate)
            {
                hasReport = true;
                if (firstMinisterRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(firstMinisterRes.MainMessage);
            }
            if (dynastyRes != null && dynastyRes.ShouldNarrate)
            {
                hasReport = true;
                if (dynastyRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(dynastyRes.MainMessage);
            }
            if (!string.IsNullOrWhiteSpace(diplomacyRes))
            {
                hasReport = true;
                dailyReport.AppendLine(diplomacyRes);
            }
            if (disasterRes != null && disasterRes.ShouldNarrate)
            {
                hasReport = true;
                if (disasterRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(disasterRes.SoundEffectKey)) result.SoundEffectKey = disasterRes.SoundEffectKey;
                dailyReport.AppendLine(disasterRes.MainMessage);
            }

            if (eventRes != null && eventRes.ShouldNarrate)
            {
                hasReport = true;
                if (eventRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(eventRes.SoundEffectKey)) result.SoundEffectKey = eventRes.SoundEffectKey;
                dailyReport.AppendLine(eventRes.MainMessage);
            }

            if (livingRealmRes != null && livingRealmRes.ShouldNarrate)
            {
                hasReport = true;
                if (livingRealmRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(livingRealmRes.SoundEffectKey)) result.SoundEffectKey = livingRealmRes.SoundEffectKey;
                dailyReport.AppendLine(livingRealmRes.MainMessage);
            }

            if (directorRes != null && directorRes.ShouldNarrate)
            {
                hasReport = true;
                if (directorRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(directorRes.SoundEffectKey)) result.SoundEffectKey = directorRes.SoundEffectKey;
                dailyReport.AppendLine(directorRes.MainMessage);
            }

            if (personalObjectiveRes != null && personalObjectiveRes.ShouldNarrate)
            {
                hasReport = true;
                if (personalObjectiveRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(personalObjectiveRes.MainMessage);
            }

            if (aiCharacterRes != null && aiCharacterRes.ShouldNarrate)
            {
                hasReport = true;
                if (aiCharacterRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(aiCharacterRes.SoundEffectKey)) result.SoundEffectKey = aiCharacterRes.SoundEffectKey;
                dailyReport.AppendLine(aiCharacterRes.MainMessage);
            }

            if (aiProposalRes != null && aiProposalRes.ShouldNarrate)
            {
                hasReport = true;
                if (aiProposalRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(aiProposalRes.SoundEffectKey)) result.SoundEffectKey = aiProposalRes.SoundEffectKey;
                dailyReport.AppendLine(aiProposalRes.MainMessage);
            }

            if (aiDelegationRes != null && aiDelegationRes.ShouldNarrate)
            {
                hasReport = true;
                if (aiDelegationRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(aiDelegationRes.SoundEffectKey)) result.SoundEffectKey = aiDelegationRes.SoundEffectKey;
                dailyReport.AppendLine(aiDelegationRes.MainMessage);
            }

            if (aiWorldActorRes != null && aiWorldActorRes.ShouldNarrate)
            {
                hasReport = true;
                if (aiWorldActorRes.ShouldPauseTime) result.ShouldPauseTime = true;
                if (!string.IsNullOrEmpty(aiWorldActorRes.SoundEffectKey)) result.SoundEffectKey = aiWorldActorRes.SoundEffectKey;
                dailyReport.AppendLine(aiWorldActorRes.MainMessage);
            }

            if (schemeRes != null && schemeRes.ShouldNarrate)
            {
                hasReport = true;
                if (schemeRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(schemeRes.MainMessage);
            }

            if (chainRes != null && chainRes.ShouldNarrate)
            {
                hasReport = true;
                if (chainRes.ShouldPauseTime) result.ShouldPauseTime = true;
                dailyReport.AppendLine(chainRes.MainMessage);
            }


            state.Time.IsPaused = result.ShouldPauseTime || wasPaused;

            if (state.RulerIsDead)
            {
                result.ShouldPauseTime = true;
                state.Time.IsPaused = true;
                string oldName = state.RulerName;
                dailyReport.AppendLine(GrandStrategySystem.HandleRulerDeathAndSuccession(state, oldName));
                hasReport = true;
            }

            if (hasReport)
            {
                result.ShouldNarrate = true;
                result.MainMessage = dailyReport.ToString().Trim();
            }
            else
            {
                result.ShouldNarrate = false;
            }
            
            return result;
        }

        public static GameActionResult AdvanceWeek(GameState state)
        {
            return AdvancePeriod(state, 7, "تقديم أسبوع");
        }

        public static GameActionResult AdvanceMonth(GameState state)
        {
            return AdvancePeriod(state, 30, "تقديم شهر");
        }

        private static GameActionResult AdvancePeriod(GameState state, int requestedDays, string title)
        {
            var result = new GameActionResult { Success = true, Title = title, ShouldNarrate = true };
            StringBuilder periodReport = new StringBuilder();
            bool initialPause = state.Time.IsPaused;
            int daysAdvanced = 0;
            string stopReason = "";

            state.Time.IsPaused = false;
            for (int i = 0; i < requestedDays; i++)
            {
                var dailyRes = AdvanceDay(state);
                daysAdvanced++;

                if (dailyRes.ShouldNarrate && !string.IsNullOrWhiteSpace(dailyRes.MainMessage))
                {
                    periodReport.AppendLine($"[{state.Time.GetDateString()}]");
                    periodReport.AppendLine(dailyRes.MainMessage);
                }

                if (dailyRes.ShouldPauseTime)
                {
                    stopReason = ExtractStopReason(dailyRes.MainMessage);
                    break;
                }

                state.Time.IsPaused = false;
            }

            state.Time.IsPaused = initialPause || !string.IsNullOrWhiteSpace(stopReason);

            var summary = new StringBuilder();
            summary.AppendLine($"تم تقديم {daysAdvanced} من أصل {requestedDays} يوم.");
            summary.AppendLine($"التاريخ الآن: {state.Time.GetDateString()}.");
            if (!string.IsNullOrWhiteSpace(stopReason))
            {
                summary.AppendLine($"توقف الزمن بسبب حدث مهم: {stopReason}");
                result.ShouldPauseTime = true;
            }
            else
            {
                summary.AppendLine("لم يحدث ما يستدعي إيقاف الزمن.");
            }

            if (periodReport.Length > 0)
            {
                summary.AppendLine();
                summary.AppendLine("ملخص الأحداث:");
                summary.Append(periodReport.ToString().Trim());
            }

            result.MainMessage = summary.ToString().Trim();
            return result;
        }

        private static string ExtractStopReason(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "حدث مهم.";

            var firstLine = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (firstLine.Length == 0)
                return "حدث مهم.";

            return firstLine[0];
        }
    }
}
