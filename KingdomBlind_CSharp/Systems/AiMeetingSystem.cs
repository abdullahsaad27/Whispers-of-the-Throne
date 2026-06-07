using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiMeetingSystem
    {
        public static GameActionResult RunCouncilMeeting(
            GameState state,
            string topic,
            string kingStatement,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var roles = new[]
            {
                AiAgentRole.FirstMinister,
                AiAgentRole.MilitaryCommander,
                AiAgentRole.DiplomaticAdvisor,
                AiAgentRole.Spymaster,
                AiAgentRole.Cleric
            };

            var participants = state.AiAgentProfiles
                .Where(p => roles.Contains(p.Role))
                .GroupBy(p => p.Role)
                .Select(g => g.OrderByDescending(p => p.Trust).First())
                .OrderBy(p => Array.IndexOf(roles, p.Role))
                .ToList();

            if ((topic ?? "").Contains("اقتصاد") || (topic ?? "").Contains("خزينة") || (topic ?? "").Contains("تجارة"))
            {
                var merchant = state.AiAgentProfiles.FirstOrDefault(p => p.Role == AiAgentRole.MerchantRepresentative);
                if (merchant != null)
                    participants.Add(merchant);
            }

            return RunMeeting(state, topic, kingStatement, "مجلس المستشارين", participants, providerSettings, actorSettings);
        }

        public static GameActionResult RunSpouseMeeting(
            GameState state,
            string topic,
            string kingStatement,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var participants = state.AiAgentProfiles
                .Where(p => p.Role == AiAgentRole.SpouseQueen)
                .OrderByDescending(p => p.Trust)
                .ThenByDescending(p => p.Ambition)
                .Take(4)
                .ToList();

            return RunMeeting(state, topic, kingStatement, "جناح الزوجات والقصر", participants, providerSettings, actorSettings);
        }

        public static GameActionResult RunNeighborAudience(
            GameState state,
            string neighborId,
            string kingStatement,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == neighborId || n.Name == neighborId);
            if (neighbor == null)
            {
                return new GameActionResult
                {
                    Success = false,
                    Title = "مخاطبة دبلوماسية",
                    MainMessage = "لم يتم العثور على الدولة المجاورة المطلوبة."
                };
            }

            var profile = state.AiAgentProfiles.FirstOrDefault(p => p.Role == AiAgentRole.NeighborRuler && p.SourceId == neighbor.Id);
            if (profile == null)
            {
                return new GameActionResult
                {
                    Success = false,
                    Title = "مخاطبة دبلوماسية",
                    MainMessage = "لم يتم العثور على جلسة شخصية لحاكم هذه الدولة."
                };
            }

            return RunMeeting(
                state,
                $"مخاطبة {neighbor.Name}",
                kingStatement,
                "مخاطبة دولة مجاورة",
                new[] { profile },
                providerSettings,
                actorSettings);
        }

        public static GameActionResult RunGovernorMeeting(
            GameState state,
            string topic,
            string kingStatement,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var participants = state.AiAgentProfiles
                .Where(p => p.Role == AiAgentRole.Governor)
                .OrderBy(p => p.Loyalty)
                .ThenByDescending(p => p.Ambition)
                .Take(5)
                .ToList();

            return RunMeeting(state, topic, kingStatement, "مجلس الولاة", participants, providerSettings, actorSettings);
        }

        public static GameActionResult RunFactionMeeting(
            GameState state,
            string topic,
            string kingStatement,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var participants = state.AiAgentProfiles
                .Where(p => p.Role == AiAgentRole.FactionLeader)
                .OrderByDescending(p => p.RiskTolerance)
                .Take(4)
                .ToList();

            return RunMeeting(state, topic, kingStatement, "جلسة مع الفصائل", participants, providerSettings, actorSettings);
        }

        public static string GetMeetingHistoryReport(GameState state)
        {
            state.ReconcileOldSaves();
            if (state.AiMeetingHistory.Count == 0)
                return "لا توجد محاضر اجتماعات ذكية محفوظة بعد.";

            var lines = state.AiMeetingHistory
                .OrderByDescending(m => m.DayNumber)
                .Take(20)
                .Select(m => $"{m.DateText}: {m.Scope} - {m.Topic}. المشاركون: {string.Join("، ", m.ParticipantNames.Take(6))}.");

            return "محاضر الاجتماعات الأخيرة:\n" + string.Join("\n", lines);
        }

        private static GameActionResult RunMeeting(
            GameState state,
            string topic,
            string kingStatement,
            string scope,
            IEnumerable<AiAgentProfile> participants,
            AiProviderSettings? providerSettings,
            AiActorSettings? actorSettings)
        {
            providerSettings ??= new AiProviderSettings();
            actorSettings ??= new AiActorSettings();
            string cleanStatement = string.IsNullOrWhiteSpace(kingStatement)
                ? "أريد رأيكم قبل أن أتخذ القرار."
                : kingStatement.Trim();

            var participantList = participants
                .Where(p => p != null)
                .GroupBy(p => p.CharacterId)
                .Select(g => g.First())
                .Take(8)
                .ToList();

            if (participantList.Count == 0)
            {
                return new GameActionResult
                {
                    Success = false,
                    Title = scope,
                    MainMessage = "لا توجد شخصيات مناسبة لهذا الاجتماع حالياً."
                };
            }

            var replies = new List<AiSessionReply>();
            foreach (var participant in participantList)
            {
                replies.Add(AiSessionSystem.GenerateReply(
                    state,
                    participant,
                    topic,
                    cleanStatement,
                    scope,
                    providerSettings,
                    actorSettings));
            }

            string transcript = BuildTranscript(state, topic, cleanStatement, scope, replies);
            var record = new AiMeetingRecord
            {
                Topic = topic ?? "",
                Scope = scope,
                KingStatement = cleanStatement,
                DateText = state.Time.GetDateString(),
                DayNumber = DiplomacySystem.GetCurrentDayNumber(state),
                ParticipantNames = replies.Select(r => r.CharacterName).ToList(),
                Transcript = transcript,
                ProviderSummary = BuildProviderSummary(replies)
            };

            state.AiMeetingHistory.Add(record);
            while (state.AiMeetingHistory.Count > 30)
                state.AiMeetingHistory.RemoveAt(0);

            AddMeetingLog(state, scope, topic, replies);

            return new GameActionResult
            {
                Success = true,
                Title = scope,
                MainMessage = transcript,
                SoundEffectKey = "paper",
                ShouldNarrate = true
            };
        }

        private static string BuildTranscript(GameState state, string topic, string kingStatement, string scope, List<AiSessionReply> replies)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{scope}.");
            sb.AppendLine($"التاريخ: {state.Time.GetDateString()}.");
            sb.AppendLine($"الموضوع: {topic}.");
            sb.AppendLine();
            sb.AppendLine($"{state.RulerName}: {kingStatement}");
            sb.AppendLine();

            foreach (var reply in replies)
            {
                sb.AppendLine($"{reply.CharacterName}، {AiAgentSystem.GetRoleDisplayName(reply.CharacterRole)}:");
                sb.AppendLine(reply.Text);
                sb.AppendLine();
            }

            sb.AppendLine("ملخص نظامي: هذا محضر رأي فقط، ولم يغير الحرب أو الخزينة أو المعاهدات.");
            sb.AppendLine($"مصادر الردود: {BuildProviderSummary(replies)}.");
            return sb.ToString().Trim();
        }

        private static string BuildProviderSummary(List<AiSessionReply> replies)
        {
            var groups = replies
                .GroupBy(r => $"{r.ProviderName}{(string.IsNullOrWhiteSpace(r.Model) ? "" : " / " + r.Model)}")
                .Select(g => $"{g.Key}: {g.Count()}");

            return string.Join("، ", groups);
        }

        private static void AddMeetingLog(GameState state, string scope, string topic, List<AiSessionReply> replies)
        {
            state.AiActionLog ??= new List<AiActionLogEntry>();
            state.AiActionLog.Add(new AiActionLogEntry
            {
                Date = state.Time.GetDateString(),
                DayNumber = DiplomacySystem.GetCurrentDayNumber(state),
                AgentName = "محضر الجلسات الذكية",
                Role = AiAgentRole.RoyalNarrator,
                ActionTaken = scope,
                Cost = 0,
                Result = $"{topic}: {replies.Count} ردود محفوظة في جلسات مستقلة.",
                Risk = 0,
                WasSuccessful = true,
                PlayerCanRespond = false
            });

            while (state.AiActionLog.Count > 100)
                state.AiActionLog.RemoveAt(0);
        }
    }
}
