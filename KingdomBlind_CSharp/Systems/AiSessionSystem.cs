using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiSessionSystem
    {
        private const int DefaultSessionMessageLimit = 24;

        public static AiConversationSession GetOrCreateSession(
            GameState state,
            AiAgentProfile profile,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            providerSettings ??= new AiProviderSettings();
            actorSettings ??= new AiActorSettings();
            string model = ResolveModel(profile, providerSettings, actorSettings);

            var session = state.AiConversationSessions.FirstOrDefault(s =>
                s.CharacterId == profile.CharacterId &&
                s.ProviderType == providerSettings.ProviderType &&
                string.Equals(s.Model ?? "", model, StringComparison.OrdinalIgnoreCase));

            if (session != null)
            {
                session.CharacterName = profile.CharacterName;
                session.CharacterRole = profile.Role;
                if (session.MaxMessages <= 0) session.MaxMessages = DefaultSessionMessageLimit;
                session.Messages ??= new List<AiConversationMessage>();
                return session;
            }

            session = new AiConversationSession
            {
                CharacterId = profile.CharacterId,
                CharacterName = profile.CharacterName,
                CharacterRole = profile.Role,
                ProviderType = providerSettings.ProviderType,
                Model = model,
                CreatedDate = state.Time.GetDateString(),
                LastUpdatedDate = state.Time.GetDateString(),
                LastDayNumber = DiplomacySystem.GetCurrentDayNumber(state),
                MaxMessages = DefaultSessionMessageLimit
            };
            state.AiConversationSessions.Add(session);
            return session;
        }

        public static AiSessionReply GenerateReply(
            GameState state,
            AiAgentProfile profile,
            string topic,
            string kingStatement,
            string meetingScope,
            AiProviderSettings? providerSettings = null,
            AiActorSettings? actorSettings = null)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            providerSettings ??= new AiProviderSettings();
            actorSettings ??= new AiActorSettings();
            var session = GetOrCreateSession(state, profile, providerSettings, actorSettings);
            int today = DiplomacySystem.GetCurrentDayNumber(state);
            string dateText = state.Time.GetDateString();
            string roleName = AiAgentSystem.GetRoleDisplayName(profile.Role);

            AddMessage(session, new AiConversationMessage
            {
                SpeakerId = "player",
                SpeakerName = state.RulerName,
                SpeakerRole = "الخليفة",
                Text = NormalizeLine(kingStatement, "ما رأيك؟"),
                DateText = dateText,
                DayNumber = today,
                IsKing = true
            });

            var effectiveProvider = BuildEffectiveProvider(profile, providerSettings, actorSettings);
            string context = BuildPromptContext(state, profile, session, topic, kingStatement, meetingScope, actorSettings);
            var provider = AiProviderFactory.Create(effectiveProvider);
            var response = provider.GenerateDialogue(state, new AiDialogueRequest
            {
                CharacterName = profile.CharacterName,
                CharacterRole = roleName,
                Context = context,
                RulerName = state.RulerName,
                PoliticalMemories = BuildPoliticalMemoryList(state, profile)
            });

            string replyText = NormalizeLine(response.Text, BuildLocalFallbackLine(state, profile, topic));
            AddMessage(session, new AiConversationMessage
            {
                SpeakerId = profile.CharacterId,
                SpeakerName = profile.CharacterName,
                SpeakerRole = roleName,
                Text = replyText,
                DateText = dateText,
                DayNumber = today,
                IsKing = false
            });

            session.LastUpdatedDate = dateText;
            session.LastDayNumber = today;
            TrimSession(session);

            return new AiSessionReply
            {
                SessionId = session.Id,
                CharacterName = profile.CharacterName,
                CharacterRole = profile.Role,
                Text = replyText,
                UsedFallback = response.UsedFallback,
                ProviderName = response.ProviderName,
                Model = session.Model
            };
        }

        public static string GetSessionReport(GameState state)
        {
            state.ReconcileOldSaves();
            if (state.AiConversationSessions.Count == 0)
                return "لا توجد جلسات حوار محفوظة بعد. ستنشأ جلسة مستقلة لكل شخصية عند أول اجتماع أو مخاطبة.";

            var lines = state.AiConversationSessions
                .OrderByDescending(s => s.LastDayNumber)
                .ThenBy(s => s.CharacterName)
                .Take(30)
                .Select(s =>
                    $"{s.CharacterName}، {AiAgentSystem.GetRoleDisplayName(s.CharacterRole)}. المزود: {s.ProviderType}. النموذج: {DisplayModel(s.Model)}. الرسائل: {s.Messages.Count}. آخر تحديث: {s.LastUpdatedDate}.");

            return "جلسات الشخصيات المحفوظة:\n" + string.Join("\n", lines);
        }

        public static string GetSessionDetail(GameState state, string sessionId)
        {
            state.ReconcileOldSaves();
            var session = state.AiConversationSessions.FirstOrDefault(s => s.Id == sessionId);
            if (session == null)
                return "لم يتم العثور على هذه الجلسة.";

            var sb = new StringBuilder();
            sb.AppendLine($"جلسة {session.CharacterName}، {AiAgentSystem.GetRoleDisplayName(session.CharacterRole)}.");
            sb.AppendLine($"المزود: {session.ProviderType}. النموذج: {DisplayModel(session.Model)}.");
            sb.AppendLine($"آخر تحديث: {session.LastUpdatedDate}.");
            sb.AppendLine();
            foreach (var message in session.Messages.TakeLast(12))
                sb.AppendLine($"{message.SpeakerName}: {message.Text}");

            return sb.ToString().Trim();
        }

        public static bool ResetSession(GameState state, string sessionId)
        {
            state.ReconcileOldSaves();
            var session = state.AiConversationSessions.FirstOrDefault(s => s.Id == sessionId);
            if (session == null)
                return false;

            state.AiConversationSessions.Remove(session);
            return true;
        }

        public static string ResolveModel(AiAgentProfile profile, AiProviderSettings providerSettings, AiActorSettings actorSettings)
        {
            providerSettings ??= new AiProviderSettings();
            actorSettings ??= new AiActorSettings();
            providerSettings.CharacterModelOverrides ??= new Dictionary<string, string>();
            actorSettings.RoleModelOverrides ??= new Dictionary<string, string>();

            if (providerSettings.CharacterModelOverrides.TryGetValue(profile.CharacterId, out var characterModel) &&
                !string.IsNullOrWhiteSpace(characterModel))
                return characterModel.Trim();

            if (actorSettings.RoleModelOverrides.TryGetValue(profile.Role.ToString(), out var roleModel) &&
                !string.IsNullOrWhiteSpace(roleModel))
                return roleModel.Trim();

            if (!string.IsNullOrWhiteSpace(actorSettings.DefaultModel))
                return actorSettings.DefaultModel.Trim();

            return (providerSettings.Model ?? "").Trim();
        }

        private static AiProviderSettings BuildEffectiveProvider(AiAgentProfile profile, AiProviderSettings providerSettings, AiActorSettings actorSettings)
        {
            bool enabledForRole = IsDialogueEnabledForRole(actorSettings, profile.Role);
            var effective = CloneProviderSettings(providerSettings);
            effective.Model = ResolveModel(profile, providerSettings, actorSettings);

            if (!enabledForRole)
            {
                effective.ProviderType = AiProviderType.Disabled;
                effective.AllowOnlineRequests = false;
            }

            return effective;
        }

        private static AiProviderSettings CloneProviderSettings(AiProviderSettings settings)
        {
            settings ??= new AiProviderSettings();
            return new AiProviderSettings
            {
                ProviderType = settings.ProviderType,
                Endpoint = settings.Endpoint ?? "",
                Model = settings.Model ?? "",
                ApiKeyEnvironmentVariable = settings.ApiKeyEnvironmentVariable ?? "",
                AllowOnlineRequests = settings.AllowOnlineRequests,
                TimeoutSeconds = settings.TimeoutSeconds,
                CharacterModelOverrides = settings.CharacterModelOverrides == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(settings.CharacterModelOverrides)
            };
        }

        private static bool IsDialogueEnabledForRole(AiActorSettings settings, AiAgentRole role)
        {
            settings ??= new AiActorSettings();
            if (!settings.SmartDialoguesEnabled)
                return false;

            return role switch
            {
                AiAgentRole.SpouseQueen => settings.ApplyToSpouses,
                AiAgentRole.FirstMinister or AiAgentRole.Spymaster or AiAgentRole.MilitaryCommander or AiAgentRole.Cleric or AiAgentRole.DiplomaticAdvisor => settings.ApplyToMinisters,
                AiAgentRole.Heir => settings.ApplyToHeirs,
                AiAgentRole.Governor => settings.ApplyToGovernors,
                AiAgentRole.FactionLeader => settings.ApplyToFactions,
                AiAgentRole.NeighborRuler => settings.ApplyToNeighborRulers,
                AiAgentRole.MerchantRepresentative => true,
                AiAgentRole.RoyalNarrator => true,
                _ => settings.ApplyToOtherCharacters
            };
        }

        private static string BuildPromptContext(
            GameState state,
            AiAgentProfile profile,
            AiConversationSession session,
            string topic,
            string kingStatement,
            string meetingScope,
            AiActorSettings actorSettings)
        {
            var context = AiContextBuilder.BuildContext(state, profile);
            string detailLevel = actorSettings.DialogueLengthLevel switch
            {
                AiDialogueLengthLevel.Brief => "أجب بجملة واحدة واضحة.",
                AiDialogueLengthLevel.Detailed => "أجب بفقرة قصيرة من ثلاث إلى خمس جمل، مع سبب سياسي واحد على الأقل.",
                _ => "أجب بجملتين أو ثلاث: رأي مختصر ثم سبب."
            };

            var history = session.Messages
                .TakeLast(8)
                .Select(m => $"{m.SpeakerName}: {m.Text}")
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(context.ToString());
            sb.AppendLine();
            sb.AppendLine($"نوع الجلسة: {meetingScope}.");
            sb.AppendLine($"موضوع الاجتماع: {topic}.");
            sb.AppendLine($"كلام الخليفة الآن: {kingStatement}");
            if (history.Count > 0)
            {
                sb.AppendLine("ذاكرة الجلسة مع هذه الشخصية:");
                foreach (var item in history)
                    sb.AppendLine(item);
            }
            sb.AppendLine("تعليمات الدور: تحدث بصوت الشخصية لا بصوت الراوي. لا تغير حالة اللعبة ولا تصدر أمراً تنفيذياً. لا تكشف سراً لا تعرفه الشخصية. إن كنت حاكماً مجاوراً فتحدث من مصلحة دولتك لا من مصلحة العباسيين.");
            sb.AppendLine(detailLevel);
            return sb.ToString().Trim();
        }

        private static List<string> BuildPoliticalMemoryList(GameState state, AiAgentProfile profile)
        {
            var memories = new List<string>();
            if (profile.MemoryNotes != null)
                memories.AddRange(profile.MemoryNotes.TakeLast(3));

            if (state.PoliticalMemories != null)
            {
                memories.AddRange(state.PoliticalMemories
                    .Where(m => !m.IsArchived && (m.ActorName == profile.CharacterName || m.Summary.Contains(profile.CharacterName)))
                    .OrderByDescending(m => m.CreatedDay)
                    .Take(3)
                    .Select(m => m.Summary));
            }

            return memories.Where(m => !string.IsNullOrWhiteSpace(m)).TakeLast(5).ToList();
        }

        private static void AddMessage(AiConversationSession session, AiConversationMessage message)
        {
            session.Messages ??= new List<AiConversationMessage>();
            if (string.IsNullOrWhiteSpace(message.Id))
                message.Id = Guid.NewGuid().ToString();
            session.Messages.Add(message);
            TrimSession(session);
        }

        private static void TrimSession(AiConversationSession session)
        {
            int max = session.MaxMessages <= 0 ? DefaultSessionMessageLimit : session.MaxMessages;
            while (session.Messages.Count > max)
                session.Messages.RemoveAt(0);
        }

        private static string NormalizeLine(string text, string fallback)
        {
            string normalized = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = fallback;

            normalized = normalized.Replace("\r\n", " ").Replace("\n", " ").Trim();
            return normalized.Length > 900 ? normalized.Substring(0, 900).Trim() + "..." : normalized;
        }

        private static string BuildLocalFallbackLine(GameState state, AiAgentProfile profile, string topic)
        {
            string ruler = state.RulerName;
            string lower = (topic ?? "").ToLowerInvariant();
            if (profile.Role == AiAgentRole.NeighborRuler)
                return $"يا {ruler}، سأزن كلامك بميزان حدودي ومصلحة دولتي، فالعهود لا تعيش إلا إذا حمتها القوة والمنفعة.";
            if (profile.Role == AiAgentRole.SpouseQueen)
                return $"يا {ruler}، القرار سيصل صداه إلى القصر والورثة قبل أن يصل إلى الحدود، فلا تجعل السياسة تكسر البيت.";
            if (lower.Contains("حرب"))
                return $"مولاي {ruler}، لا أرفض الحزم، لكنني أطلب حساب المؤونة والولاء قبل أن نفتح باب الحرب.";
            if (lower.Contains("اقتصاد") || lower.Contains("خزينة"))
                return $"مولاي {ruler}، الطريق الأقصر إلى المال قد يطيل طريق السخط، فلنبدأ بما يزيد الدخل دون جباية قاسية.";
            return $"مولاي {ruler}، رأيي أن نزن الكلفة والذكرى السياسية قبل إصدار الأمر.";
        }

        private static string DisplayModel(string model)
        {
            return string.IsNullOrWhiteSpace(model) ? "افتراضي" : model;
        }
    }
}
