using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiProposalQueue
    {
        public static GameActionResult GenerateMonthlyProposals(GameState state, int maxNewProposals = 3)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var result = new GameActionResult { Title = "مقترحات وكلاء البلاط", Success = true, ShouldNarrate = false };
            if (state.SuppressRandomMajorEvents)
                return result;

            int created = 0;

            foreach (var profile in SelectProposalAgents(state))
            {
                if (created >= maxNewProposals)
                    break;

                var request = CreateProposal(state, profile);
                if (request == null)
                    continue;

                AiActionValidator.MarkApprovalRequirement(state, request);
                if (IsDuplicateOrDisabled(state, request))
                    continue;

                request.SpokenJustification = BuildLocalJustification(state, profile, request);
                state.AiProposalQueue.Add(request);
                created++;
            }

            if (created > 0)
            {
                result.ShouldNarrate = true;
                result.MainMessage = $"وصلت {created} مقترحات جديدة من وكلاء البلاط. راجع شاشة التفويض الملكي أو المقترحات المعلقة.";
                result.SoundEffectKey = "paper";
            }

            return result;
        }

        public static GameActionResult ProcessAutonomousDelegates(GameState state)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var result = new GameActionResult { Title = "الأفعال التلقائية لوكلاء البلاط", Success = true, ShouldNarrate = false };
            if (!state.DelegatedAuthoritySettings.AllowAutonomousActions || state.SuppressRandomMajorEvents)
                return result;

            var messages = new List<string>();
            foreach (var profile in state.AiAgentProfiles
                         .Where(p => p.IsAutonomous && p.AuthorityLevel >= AiAuthorityLevel.LimitedDelegate)
                         .OrderByDescending(p => p.Trust + p.Loyalty - p.Ambition)
                         .Take(4))
            {
                var request = CreateProposal(state, profile);
                if (request == null)
                    continue;

                AiActionValidator.MarkApprovalRequirement(state, request);
                if (request.RequiresKingApproval || IsDuplicateOrDisabled(state, request))
                    continue;

                request.Status = AiProposalStatus.Approved;
                var executed = AiActionValidator.ValidateAndExecute(state, request, approvedByKing: false);
                if (executed.Success)
                    messages.Add($"{profile.CharacterName}: {AiAgentSystem.GetActionDisplayName(request.ActionType)}. {executed.MainMessage}");
            }

            if (messages.Count > 0)
            {
                result.ShouldNarrate = true;
                result.MainMessage = "قرارات المجلس التلقائية هذا الشهر:\n" + string.Join("\n", messages);
                result.SoundEffectKey = "paper";
            }

            return result;
        }

        public static GameActionResult ApproveProposal(GameState state, string proposalId)
        {
            state.ReconcileOldSaves();
            var request = state.AiProposalQueue.FirstOrDefault(p => p.Id == proposalId);
            if (request == null)
                return new GameActionResult { Success = false, Title = "مقترح AI", MainMessage = "لم يتم العثور على المقترح." };

            request.Status = AiProposalStatus.Approved;
            var result = AiActionValidator.ValidateAndExecute(state, request, approvedByKing: true);
            return result;
        }

        public static GameActionResult RejectProposal(GameState state, string proposalId)
        {
            var request = state.AiProposalQueue.FirstOrDefault(p => p.Id == proposalId);
            if (request == null)
                return new GameActionResult { Success = false, Title = "رفض مقترح", MainMessage = "لم يتم العثور على المقترح." };

            request.Status = AiProposalStatus.Rejected;
            request.StatusReason = "رفضه الملك.";
            state.AiActionLog.Add(new AiActionLogEntry
            {
                Date = state.Time.GetDateString(),
                DayNumber = DiplomacySystem.GetCurrentDayNumber(state),
                AgentName = request.AgentName,
                Role = request.Role,
                ActionTaken = "رفض مقترح: " + AiAgentSystem.GetActionDisplayName(request.ActionType),
                Cost = 0,
                Result = "رفض الملك المقترح دون تغيير حالة المملكة.",
                Risk = request.EstimatedRisk,
                WasSuccessful = true,
                PlayerCanRespond = false
            });

            return new GameActionResult
            {
                Success = true,
                Title = "رفض مقترح",
                MainMessage = $"تم رفض اقتراح {request.AgentName}. لم تتغير حالة المملكة."
            };
        }

        public static GameActionResult DeferProposal(GameState state, string proposalId)
        {
            var request = state.AiProposalQueue.FirstOrDefault(p => p.Id == proposalId);
            if (request == null)
                return new GameActionResult { Success = false, Title = "تأجيل مقترح", MainMessage = "لم يتم العثور على المقترح." };

            request.Status = AiProposalStatus.Deferred;
            request.StatusReason = "أجله الملك.";
            return new GameActionResult
            {
                Success = true,
                Title = "تأجيل مقترح",
                MainMessage = "تم تأجيل المقترح. سيبقى في السجل دون تنفيذ."
            };
        }

        public static GameActionResult DisableSimilarProposal(GameState state, string proposalId)
        {
            state.DelegatedAuthoritySettings ??= new DelegatedAuthoritySettings();
            state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys ??= new List<string>();
            var request = state.AiProposalQueue.FirstOrDefault(p => p.Id == proposalId);
            if (request == null)
                return new GameActionResult { Success = false, Title = "تعطيل مقترحات مشابهة", MainMessage = "لم يتم العثور على المقترح." };

            if (!state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys.Contains(request.SimilarityKey))
                state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys.Add(request.SimilarityKey);

            request.Status = AiProposalStatus.Cancelled;
            request.StatusReason = "عُطلت المقترحات المشابهة بأمر الملك.";
            return new GameActionResult
            {
                Success = true,
                Title = "تعطيل مقترحات مشابهة",
                MainMessage = "لن تقترح الشخصيات هذا النوع من الطلبات المشابهة حالياً."
            };
        }

        public static string GetPendingProposalReport(GameState state, bool includeDetails = false)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var pending = state.AiProposalQueue
                .Where(p => p.Status == AiProposalStatus.Pending)
                .OrderByDescending(p => p.CreatedDay)
                .Take(12)
                .ToList();

            if (pending.Count == 0)
                return "لا توجد مقترحات معلقة حالياً.";

            var sb = new StringBuilder();
            sb.AppendLine($"المقترحات المعلقة: {pending.Count}.");
            sb.AppendLine("ملخص قصير أولاً، ويمكن فتح أي مقترح لقراءة التفاصيل.");
            sb.AppendLine();

            foreach (var proposal in pending)
            {
                sb.AppendLine(AiActionValidator.FormatProposal(state, proposal, includeDetails));
                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        public static string GetActionLogReport(GameState state)
        {
            state.ReconcileOldSaves();
            var entries = state.AiActionLog
                .OrderByDescending(l => l.DayNumber)
                .Take(20)
                .ToList();

            if (entries.Count == 0)
                return "سجل قرارات المجلس فارغ حالياً.";

            var sb = new StringBuilder();
            sb.AppendLine("سجل قرارات المجلس.");
            sb.AppendLine("ملخص قصير: كل بند هنا مر عبر التحقق أو سُجل كرفض/فشل.");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                sb.AppendLine($"{entry.Date}: {entry.AgentName}، {AiAgentSystem.GetRoleDisplayName(entry.Role)}.");
                sb.AppendLine($"الفعل: {entry.ActionTaken}. التكلفة: {entry.Cost} ذهب. الخطر: {DescribeRisk(entry.Risk)}.");
                sb.AppendLine($"النتيجة: {(entry.WasSuccessful ? "نجح" : "فشل")}. {entry.Result}");
                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        public static AiActionRequest CreateProposal(GameState state, AiAgentProfile profile)
        {
            if (profile == null || profile.AuthorityLevel == AiAuthorityLevel.None)
                return null;

            return profile.Role switch
            {
                AiAgentRole.Spymaster => CreateSpymasterProposal(state, profile),
                AiAgentRole.FirstMinister => CreateFirstMinisterProposal(state, profile),
                AiAgentRole.MilitaryCommander => CreateCommanderProposal(state, profile),
                AiAgentRole.Cleric => CreateClericProposal(state, profile),
                AiAgentRole.DiplomaticAdvisor => CreateDiplomaticProposal(state, profile),
                AiAgentRole.Governor => CreateGovernorProposal(state, profile),
                AiAgentRole.NeighborRuler => CreateNeighborProposal(state, profile),
                AiAgentRole.SpouseQueen => CreateSpouseProposal(state, profile),
                AiAgentRole.Heir => CreateHeirProposal(state, profile),
                AiAgentRole.MerchantRepresentative => CreateMerchantProposal(state, profile),
                _ => CreateRequest(state, profile, AiActionType.RequestCouncilMeeting, AiActionTargetType.Council, "council", "المجلس", "طلب قراءة سياسية موجزة", "تنظيم القرار", 5, 0, 0, 65)
            };
        }

        private static IEnumerable<AiAgentProfile> SelectProposalAgents(GameState state)
        {
            return state.AiAgentProfiles
                .Where(p => p.AuthorityLevel != AiAuthorityLevel.None && p.Role != AiAgentRole.RoyalNarrator)
                .OrderByDescending(p => RolePriority(p.Role))
                .ThenByDescending(p => p.Trust + p.Ambition)
                .Take(10);
        }

        private static int RolePriority(AiAgentRole role)
        {
            return role switch
            {
                AiAgentRole.Spymaster => 100,
                AiAgentRole.FirstMinister => 95,
                AiAgentRole.MilitaryCommander => 80,
                AiAgentRole.MerchantRepresentative => 70,
                AiAgentRole.Cleric => 65,
                AiAgentRole.DiplomaticAdvisor => 60,
                AiAgentRole.Governor => 45,
                AiAgentRole.NeighborRuler => 40,
                AiAgentRole.SpouseQueen => 35,
                _ => 10
            };
        }

        private static AiActionRequest CreateSpymasterProposal(GameState state, AiAgentProfile profile)
        {
            if (!state.SpyNetworks.Any(n => n.TargetType == "RoyalCourt"))
                return CreateRequest(state, profile, AiActionType.BuildSpyNetwork, AiActionTargetType.Council, "court", "البلاط العباسي", "لا توجد عيون كافية داخل القصر", "كشف المؤامرات مبكراً", 18, 150, 14, 80);

            if (state.CounterIntelligenceLevel < 55)
                return CreateRequest(state, profile, AiActionType.ImproveCounterIntelligence, AiActionTargetType.Council, "court", "القصر", "مكافحة الاستخبارات دون المستوى الآمن", "تقليل خطر الاغتيال والتسلل", 12, 200, 7, 82);

            var faction = state.Factions.Where(f => f.IsActive).OrderByDescending(f => f.PowerPercent + f.Discontent).FirstOrDefault();
            if (faction != null && faction.PowerPercent + faction.Discontent > 90)
                return CreateRequest(state, profile, AiActionType.DisruptFaction, AiActionTargetType.Faction, faction.Id, faction.Name, "الفصيل يتضخم قبل الإنذار", "خفض قوة الفصيل وسخطه", 42, 200, 7, 68);

            var governor = state.Governors.OrderBy(g => g.Loyalty + g.OpinionOfKing).FirstOrDefault();
            if (governor != null && governor.Loyalty + governor.OpinionOfKing < 70)
                return CreateRequest(state, profile, AiActionType.InvestigateGovernor, AiActionTargetType.Governor, governor.Id, governor.Name, "ولاء الوالي منخفض وتحتاج الدولة إلى تقدير أوضح", "تقرير استخباراتي محدود الثقة", 22, 50, 3, 70);

            return CreateRequest(state, profile, AiActionType.ReviewSpymasterReports, AiActionTargetType.Council, "reports", "التقارير السرية", "مراجعة ما وصل بدل اختلاق معلومات", "تنظيم المعرفة دون كشف غير مشروع", 4, 0, 0, 90);
        }

        private static AiActionRequest CreateFirstMinisterProposal(GameState state, AiAgentProfile profile)
        {
            var disaster = state.ActiveDisasters.OrderByDescending(d => d.DaysRemaining).FirstOrDefault();
            if (disaster != null)
                return CreateRequest(state, profile, AiActionType.SendReliefToProvince, AiActionTargetType.Province, disaster.ProvinceId, disaster.ProvinceName, "كارثة نشطة تضغط على الرضا والدخل", "تقليل مدة الكارثة وتهدئة الرعية", 15, 100, 2, 82);

            if (state.Gold < 250 && state.MerchantsTrust >= 30)
                return CreateRequest(state, profile, AiActionType.NegotiateMerchantLoan, AiActionTargetType.Realm, "treasury", "الخزينة", "الخزينة منخفضة وقد تتأخر الرواتب", "قرض سريع من التجار", 28, 0, 1, 72);

            var angryGovernor = state.Governors.OrderBy(g => g.OpinionOfKing + g.Loyalty).FirstOrDefault();
            if (angryGovernor != null && angryGovernor.OpinionOfKing < -20)
                return CreateRequest(state, profile, AiActionType.CalmAngryGovernor, AiActionTargetType.Governor, angryGovernor.Id, angryGovernor.Name, "والي غاضب قد يغذي الفصائل", "تحسين رأيه وولائه", 20, 80, 5, 75);

            if (state.SeasonalMarketDaysLeft <= 0 && state.Gold >= 200 && state.Food >= 100)
                return CreateRequest(state, profile, AiActionType.OrganizeSeasonalMarket, AiActionTargetType.Realm, "markets", "بغداد والطرق الكبرى", "تنشيط السوق يزيد الدخل وثقة التجار", "دخل شهري وثقة تجارية أعلى", 10, 200, 30, 84);

            var province = state.Provinces.OrderBy(p => p.Buildings.Where(b => b.BuildingType == "سوق").Sum(b => b.Level)).FirstOrDefault();
            return province == null
                ? null
                : CreateRequest(state, profile, AiActionType.RecommendConstruction, AiActionTargetType.Province, province.Id, province.Name, "تنمية الدخل تحتاج مشروعاً واضحاً", "بناء أو ترقية سوق", 12, 100, 30, 76);
        }

        private static AiActionRequest CreateCommanderProposal(GameState state, AiAgentProfile profile)
        {
            var weakProvince = state.Provinces.OrderBy(p => p.LocalGarrison).FirstOrDefault();
            if (weakProvince != null && weakProvince.LocalGarrison < 500)
                return CreateRequest(state, profile, AiActionType.PrepareDefense, AiActionTargetType.Province, weakProvince.Id, weakProvince.Name, "الحامية ضعيفة وقد تغري جاراً أو متمرداً", "رفع الحامية وطمأنة الرعية", 18, 100, 7, 78);

            if (state.ActiveWar != null)
                return CreateRequest(state, profile, AiActionType.MoveArmyRecommendation, AiActionTargetType.Province, "", state.ActiveWar.TargetProvince, "الحرب تحتاج تركيز الجيش", "توصية تحرك عسكري", 35, 0, 5, 62);

            var route = GetSuggestedTradeRoute(state);
            return CreateRequest(state, profile, AiActionType.EscortTradeCaravan, AiActionTargetType.TradeRoute, route, route, "أمان الطرق يحفظ الدخل والهيبة", "زيادة ثقة التجار ودخل الطريق", 16, 100, 10, 74);
        }

        private static AiActionRequest CreateClericProposal(GameState state, AiAgentProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(state.HeirName) && state.ReligiousLegitimacy < 70)
                return CreateRequest(state, profile, AiActionType.SupportHeir, AiActionTargetType.Heir, "heir", state.HeirName, "الوريث يحتاج دعماً شرعياً", "خفض خطر فصائل الخلافة", 10, 150, 1, 80);

            return CreateRequest(state, profile, AiActionType.ImproveClergyRelations, AiActionTargetType.Realm, "poor", "الفقراء ورجال الدين", "رضا الناس والشرعية يحتاجان عناية", "رفع الرضا والشرعية وخفض التوتر", 8, 200, 3, 78);
        }

        private static AiActionRequest CreateDiplomaticProposal(GameState state, AiAgentProfile profile)
        {
            var neighbor = state.Neighbors
                .Where(n => !n.IsAtWarWithPlayer)
                .OrderByDescending(n => n.Opinion + n.Trust)
                .FirstOrDefault();
            if (neighbor == null)
                return null;

            if (!neighbor.IsAlly && neighbor.Opinion >= 0)
                return CreateRequest(state, profile, AiActionType.ProposeMarriageAlliance, AiActionTargetType.NeighborKingdom, neighbor.Id, neighbor.Name, "العلاقة تسمح بتحالف زواج إن وافق الملك", "تحالف وتثبيت حدود", 30, 0, 1, 65);

            return CreateRequest(state, profile, AiActionType.SendDiplomaticMessage, AiActionTargetType.NeighborKingdom, neighbor.Id, neighbor.Name, "رسالة هادئة تمنع برود العلاقة", "رفع الرأي والثقة", 8, 50, 3, 82);
        }

        private static AiActionRequest CreateGovernorProposal(GameState state, AiAgentProfile profile)
        {
            var governor = state.Governors.FirstOrDefault(g => g.Id == profile.SourceId);
            if (governor == null)
                return null;

            var disaster = state.ActiveDisasters.FirstOrDefault(d => d.ProvinceId == governor.ProvinceId || d.ProvinceName == governor.ProvinceName);
            if (disaster != null)
                return CreateRequest(state, profile, AiActionType.SendReliefToProvince, AiActionTargetType.Province, disaster.ProvinceId, disaster.ProvinceName, "الوالي يطلب إغاثة لمقاطعته", "رضا محلي وتقصير الكارثة", 14, 100, 2, 70);

            if (governor.OpinionOfKing < -25)
                return CreateRequest(state, profile, AiActionType.CalmAngryGovernor, AiActionTargetType.Governor, governor.Id, governor.Name, "الوالي يطلب لقاءً قبل أن ينزلق للتمرد", "خفض التوتر المحلي", 18, 80, 5, 68);

            return CreateRequest(state, profile, AiActionType.PrepareDefense, AiActionTargetType.Province, governor.ProvinceId, governor.ProvinceName, "الوالي يطلب حماية مقاطعته", "رفع الحامية المحلية", 16, 100, 7, 70);
        }

        private static AiActionRequest CreateNeighborProposal(GameState state, AiAgentProfile profile)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == profile.SourceId);
            if (neighbor == null || neighbor.IsAtWarWithPlayer)
                return null;

            if (neighbor.EconomicTrouble > 60 && neighbor.Opinion >= 10)
                return CreateRequest(state, profile, AiActionType.NegotiateMerchantLoan, AiActionTargetType.NeighborKingdom, neighbor.Id, neighbor.Name, "الحاكم المجاور يعرض قناة مالية وسياسية", "قرض سياسي أو تجاري يفتح باب نفوذ", 34, 0, 3, 55);

            if (!neighbor.IsAlly && neighbor.Opinion > 20)
                return CreateRequest(state, profile, AiActionType.ProposeMarriageAlliance, AiActionTargetType.NeighborKingdom, neighbor.Id, neighbor.Name, "الجار يريد ربط الحدود بالمصاهرة", "تحالف أو تهدئة طويلة", 30, 0, 1, 60);

            return CreateRequest(state, profile, AiActionType.SendDiplomaticMessage, AiActionTargetType.NeighborKingdom, neighbor.Id, neighbor.Name, "رسالة سياسية لا تكشف أسراراً", "اختبار النوايا وتحسين العلاقة", 10, 50, 3, 70);
        }

        private static AiActionRequest CreateSpouseProposal(GameState state, AiAgentProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(state.HeirName))
                return CreateRequest(state, profile, AiActionType.WarnAboutSuccessionRisk, AiActionTargetType.Heir, "heir", state.HeirName, "القصر يتحدث عن الخلافة والأنصار", "تنبيه مبكر دون إنفاق", 6, 0, 0, 72);

            return CreateRequest(state, profile, AiActionType.RequestCouncilMeeting, AiActionTargetType.Council, "court", "القصر", "الملكة ترى أن المجلس لا يسمع همس القصر", "جمع رأي القصر بالمجلس", 8, 0, 0, 68);
        }

        private static AiActionRequest CreateHeirProposal(GameState state, AiAgentProfile profile)
        {
            return CreateRequest(state, profile, AiActionType.WarnAboutSuccessionRisk, AiActionTargetType.Heir, "heir", state.HeirName, "الوريث يريد تثبيت موقعه دون قطيعة", "تنبيه سياسي مبكر", 8, 0, 0, 70);
        }

        private static AiActionRequest CreateMerchantProposal(GameState state, AiAgentProfile profile)
        {
            if (state.SeasonalMarketDaysLeft <= 0 && state.Gold >= 200 && state.Food >= 100)
                return CreateRequest(state, profile, AiActionType.OrganizeSeasonalMarket, AiActionTargetType.Realm, "markets", "بغداد والطرق الكبرى", "السوق الموسمي يحرك القوافل", "دخل وثقة تجارية", 8, 200, 30, 86);

            string route = GetSuggestedTradeRoute(state);
            return CreateRequest(state, profile, AiActionType.ProtectTradeRoute, AiActionTargetType.TradeRoute, route, route, "التجار يريدون طريقاً آمناً قبل توسيع القوافل", "دخل شهري وثقة أعلى", 14, 100, 10, 78);
        }

        private static AiActionRequest CreateRequest(
            GameState state,
            AiAgentProfile profile,
            AiActionType action,
            AiActionTargetType targetType,
            string targetId,
            string targetName,
            string reason,
            string benefit,
            int risk,
            int goldCost,
            int days,
            int confidence)
        {
            return new AiActionRequest
            {
                AgentCharacterId = profile.CharacterId,
                AgentName = profile.CharacterName,
                Role = profile.Role,
                ActionType = action,
                TargetType = targetType,
                TargetId = targetId ?? "",
                TargetName = targetName ?? "",
                Reason = reason,
                ExpectedBenefit = benefit,
                EstimatedRisk = risk,
                GoldCost = goldCost,
                TimeCostDays = days,
                Confidence = confidence,
                CreatedDate = state.Time.GetDateString(),
                CreatedDay = DiplomacySystem.GetCurrentDayNumber(state),
                SimilarityKey = $"{profile.Role}:{action}:{targetType}:{targetId}"
            };
        }

        private static bool IsDuplicateOrDisabled(GameState state, AiActionRequest request)
        {
            if (state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys.Contains(request.SimilarityKey))
                return true;

            return state.AiProposalQueue.Any(p =>
                p.Status == AiProposalStatus.Pending &&
                p.SimilarityKey == request.SimilarityKey);
        }

        private static string BuildLocalJustification(GameState state, AiAgentProfile profile, AiActionRequest request)
        {
            string opening = profile.Role switch
            {
                AiAgentRole.Spymaster => "ملخص: الهمس لا ينتظر حتى يصير سيفاً.",
                AiAgentRole.FirstMinister => "ملخص: هذا إجراء إداري محدود قبل أن يكبر الخلل.",
                AiAgentRole.MilitaryCommander => "ملخص: الطريق أو الحامية الضعيفة دعوة لمن يتربص.",
                AiAgentRole.Cleric => "ملخص: الشرعية إذا ضعفت احتاجت صدقة وعدلاً لا خطبة فقط.",
                AiAgentRole.NeighborRuler => "ملخص: هذه رسالة مصلحة لا محبة مطلقة.",
                AiAgentRole.SpouseQueen => "ملخص: القصر يرى ما لا يصل دائماً إلى المجلس.",
                AiAgentRole.MerchantRepresentative => "ملخص: القافلة الآمنة تملأ الخزينة أكثر من السوق الخائف.",
                _ => "ملخص: هذا اقتراح محدود ومقروء."
            };

            return $"{opening}\n{profile.CharacterName}: {request.Reason}. أتوقع {request.ExpectedBenefit}.";
        }

        private static string GetSuggestedTradeRoute(GameState state)
        {
            var candidates = new[]
            {
                "طريق بغداد - دمشق",
                "طريق بغداد - حلب",
                "طريق دمشق - القدس",
                "طريق الموصل التجاري"
            };

            return candidates.FirstOrDefault(route => !state.ProtectedTradeRoutes.Contains(route)) ?? candidates[0];
        }

        private static string DescribeRisk(int risk)
        {
            if (risk >= 70) return "عالٍ";
            if (risk >= 40) return "متوسط";
            if (risk >= 15) return "منخفض";
            return "بسيط";
        }
    }
}
