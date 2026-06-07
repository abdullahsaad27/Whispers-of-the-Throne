using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiActionValidator
    {
        public static GameActionResult ValidateAndExecute(GameState state, AiActionRequest request, bool approvedByKing)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var profile = AiAgentSystem.GetProfile(state, request.AgentCharacterId);
            var validation = ValidateOnly(state, request, profile, approvedByKing);
            if (!validation.Success)
            {
                request.Status = AiProposalStatus.Failed;
                request.StatusReason = validation.MainMessage;
                AddLog(state, request, validation, false, approvedByKing);
                return validation;
            }

            int goldBefore = state.Gold;
            var result = ExecuteThroughGameSystem(state, request, profile);
            int actualCost = Math.Max(0, goldBefore - state.Gold);

            if (result.Success)
            {
                request.Status = AiProposalStatus.Executed;
                request.StatusReason = "تم التنفيذ عبر النظام المختص.";
                RegisterCooldown(profile, request);
                RegisterAutonomousBudget(state, actualCost, approvedByKing);
                AddMemory(profile, request, result);
            }
            else
            {
                request.Status = AiProposalStatus.Failed;
                request.StatusReason = result.MainMessage;
            }

            AddLog(state, request, result, result.Success, approvedByKing);
            return result;
        }

        public static GameActionResult ValidateOnly(GameState state, AiActionRequest request, AiAgentProfile profile, bool approvedByKing)
        {
            var res = new GameActionResult { Title = "تحقق فعل وكيل البلاط", Success = false };

            if (request == null)
            {
                res.MainMessage = "طلب الذكاء الاصطناعي غير موجود.";
                return res;
            }

            if (profile == null)
            {
                res.MainMessage = "لا يوجد وكيل مرتبط بهذا الطلب.";
                return res;
            }

            if (profile.AuthorityLevel == AiAuthorityLevel.None)
            {
                res.MainMessage = $"{profile.CharacterName} لا يملك صلاحية سياسية حالياً، ويمكنه الحوار فقط.";
                return res;
            }

            if (!profile.AllowedActionTypes.Contains(request.ActionType))
            {
                res.MainMessage = $"{AiAgentSystem.GetRoleDisplayName(profile.Role)} لا يملك صلاحية طلب: {AiAgentSystem.GetActionDisplayName(request.ActionType)}.";
                return res;
            }

            if (!AiContextBuilder.HasSufficientKnowledge(state, profile, request, out var knowledgeReason))
            {
                res.MainMessage = knowledgeReason;
                return res;
            }

            if (state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys.Contains(request.SimilarityKey))
            {
                res.MainMessage = "هذا النوع من المقترحات معطل بأمر سابق من الملك.";
                return res;
            }

            string cooldownKey = request.ActionType.ToString() + ":" + request.TargetId;
            if (profile.Cooldowns.TryGetValue(cooldownKey, out var cooldown) && cooldown > 0)
            {
                res.MainMessage = $"هذا الفعل على فترة انتظار لدى {profile.CharacterName}. المتبقي {cooldown} يوم.";
                return res;
            }

            if (request.GoldCost > 0 && state.Gold < request.GoldCost)
            {
                res.MainMessage = $"الخزينة لا تكفي. المطلوب {request.GoldCost} ذهب والمتاح {state.Gold}.";
                return res;
            }

            if (request.RequiresKingApproval && !approvedByKing)
            {
                res.MainMessage = "هذا الطلب يحتاج موافقة الملك قبل التنفيذ.";
                return res;
            }

            if (!approvedByKing)
            {
                if (!state.DelegatedAuthoritySettings.AllowAutonomousActions || !profile.IsAutonomous)
                {
                    res.MainMessage = "الأفعال التلقائية غير مفعلة أو أن هذا الوكيل غير مفوض للتنفيذ المباشر.";
                    return res;
                }

                if (request.EstimatedRisk > profile.RequiresApprovalAboveRisk)
                {
                    res.MainMessage = $"الخطر المقدر {request.EstimatedRisk}/100 أعلى من حد تفويض {profile.CharacterName}، ويحتاج موافقة الملك.";
                    return res;
                }

                if (request.GoldCost > profile.MonthlyBudget)
                {
                    res.MainMessage = $"تكلفة الطلب تتجاوز ميزانية {profile.CharacterName} الشهرية.";
                    return res;
                }

                if (state.DelegatedAuthoritySettings.AutonomousBudgetSpentThisMonth + request.GoldCost > state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget)
                {
                    res.MainMessage = "ميزانية الأفعال التلقائية لهذا الشهر لا تسمح بهذا التنفيذ.";
                    return res;
                }
            }

            var legality = ValidateLegality(state, request);
            if (!legality.Success)
                return legality;

            res.Success = true;
            res.MainMessage = "الطلب قانوني ويمكن تنفيذه عبر النظام المختص.";
            return res;
        }

        public static void MarkApprovalRequirement(GameState state, AiActionRequest request)
        {
            AiAgentSystem.EnsureAgents(state);
            var profile = AiAgentSystem.GetProfile(state, request.AgentCharacterId);
            if (profile == null)
            {
                request.RequiresKingApproval = true;
                return;
            }

            request.RequiresKingApproval =
                profile.AuthorityLevel <= AiAuthorityLevel.Advisor ||
                request.EstimatedRisk > profile.RequiresApprovalAboveRisk ||
                request.GoldCost > profile.MonthlyBudget ||
                !state.DelegatedAuthoritySettings.AllowAutonomousActions;
        }

        public static string FormatProposal(GameState state, AiActionRequest request, bool includeDetails)
        {
            var profile = AiAgentSystem.GetProfile(state, request.AgentCharacterId);
            string speaker = string.IsNullOrWhiteSpace(request.AgentName) ? profile?.CharacterName ?? "وكيل البلاط" : request.AgentName;
            string text =
                $"اقتراح من {speaker}:\n" +
                $"{AiAgentSystem.GetActionDisplayName(request.ActionType)}.\n" +
                $"الهدف: {(string.IsNullOrWhiteSpace(request.TargetName) ? "عام" : request.TargetName)}.\n" +
                $"التكلفة: {request.GoldCost} ذهب.\n" +
                $"المدة: {request.TimeCostDays} يوم.\n" +
                $"الخطر: {DescribeRisk(request.EstimatedRisk)}.\n" +
                $"الفائدة المتوقعة: {request.ExpectedBenefit}.";

            if (includeDetails)
            {
                text += "\n\n" +
                        $"السبب: {request.Reason}\n" +
                        $"الثقة: {request.Confidence}/100.\n" +
                        $"يحتاج موافقة الملك: {(request.RequiresKingApproval ? "نعم" : "لا")}.\n" +
                        $"تبرير المتحدث: {request.SpokenJustification}";
            }

            return text;
        }

        private static GameActionResult ValidateLegality(GameState state, AiActionRequest request)
        {
            var res = new GameActionResult { Title = "تحقق قانوني", Success = true, MainMessage = "" };

            if (request.ActionType == AiActionType.OfferPeaceTerms && state.ActiveWar == null)
            {
                res.Success = false;
                res.MainMessage = "لا توجد حرب قائمة حتى تُعرض شروط الصلح.";
                return res;
            }

            if (request.ActionType == AiActionType.ProposeMarriageAlliance)
            {
                var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == request.TargetId || n.Name == request.TargetName);
                if (neighbor == null)
                {
                    res.Success = false;
                    res.MainMessage = "الدولة المقترحة للزواج السياسي غير موجودة.";
                    return res;
                }
                if (neighbor.IsAtWarWithPlayer || neighbor.Relation == "حرب")
                {
                    res.Success = false;
                    res.MainMessage = "لا يمكن اقتراح زواج سياسي أثناء الحرب مع الدولة نفسها.";
                    return res;
                }
            }

            if (request.ActionType == AiActionType.DisruptFaction && request.EstimatedRisk > 55 && !request.RequiresKingApproval)
            {
                res.Success = false;
                res.MainMessage = "تفكيك فصيل عالي الخطر يحتاج موافقة ملكية صريحة.";
                return res;
            }

            return res;
        }

        private static GameActionResult ExecuteThroughGameSystem(GameState state, AiActionRequest request, AiAgentProfile profile)
        {
            switch (request.ActionType)
            {
                case AiActionType.BuildSpyNetwork:
                    return ExecuteBuildSpyNetwork(state, request);
                case AiActionType.ImproveCounterIntelligence:
                    return IntelligenceSystem.ImproveCounterIntelligence(state, request.GoldCost > 0 ? request.GoldCost : 200, 5);
                case AiActionType.InvestigateGovernor:
                    return ExecuteSpyOperation(state, request, "مراقبة والٍ", 50, 3);
                case AiActionType.DisruptFaction:
                    return ExecuteSpyOperation(state, request, "تفكيك فصيل", 200, 7);
                case AiActionType.ProtectHeir:
                case AiActionType.SupportHeir:
                    return ReligionSystem.SupportHeir(state);
                case AiActionType.ReviewSpymasterReports:
                    return ReviewSpymasterReports(state);
                case AiActionType.SendReliefToProvince:
                    return ExecuteRelief(state, request);
                case AiActionType.NegotiateMerchantLoan:
                    return EconomySystem.RequestMerchantLoan(state, 500);
                case AiActionType.ProtectTradeRoute:
                case AiActionType.EscortTradeCaravan:
                    return EconomySystem.ProtectTradeRoute(state, string.IsNullOrWhiteSpace(request.TargetName) ? "طريق بغداد التجاري" : request.TargetName);
                case AiActionType.RecommendConstruction:
                    return ExecuteConstruction(state, request);
                case AiActionType.RequestCouncilMeeting:
                    return RequestCouncilMeeting(state, profile);
                case AiActionType.WarnAboutSuccessionRisk:
                    return WarnAboutSuccession(state, profile);
                case AiActionType.ProposeMarriageAlliance:
                    return ExecuteMarriageAlliance(state, request);
                case AiActionType.ImproveClergyRelations:
                    return ReligionSystem.SupportPoor(state);
                case AiActionType.PrepareDefense:
                    return ArmyCommandSystem.PrepareProvinceDefense(state, string.IsNullOrWhiteSpace(request.TargetId) ? request.TargetName : request.TargetId, request.GoldCost > 0 ? request.GoldCost : 100, 120);
                case AiActionType.MoveArmyRecommendation:
                    return ExecuteArmyMove(state, request, profile);
                case AiActionType.SendDiplomaticMessage:
                    return ExecuteDiplomaticMessage(state, request);
                case AiActionType.OfferPeaceTerms:
                    return WarfareSystem.NegotiatePeace(state, "WhitePeace");
                case AiActionType.CalmAngryGovernor:
                    return ExecuteCalmGovernor(state, request, profile);
                case AiActionType.OrganizeSeasonalMarket:
                    return EconomySystem.StartSeasonalMarket(state);
                default:
                    return new GameActionResult { Success = false, Title = "فعل غير مدعوم", MainMessage = "هذا الفعل لم يربط بعد بنظام تنفيذي آمن." };
            }
        }

        private static GameActionResult ExecuteBuildSpyNetwork(GameState state, AiActionRequest request)
        {
            string targetType = request.TargetType switch
            {
                AiActionTargetType.Province or AiActionTargetType.Governor => "InternalProvince",
                AiActionTargetType.Faction => "Faction",
                AiActionTargetType.NeighborKingdom => "ForeignKingdom",
                _ => "RoyalCourt"
            };
            string targetId = string.IsNullOrWhiteSpace(request.TargetId) ? request.TargetName : request.TargetId;
            if (string.IsNullOrWhiteSpace(targetId)) targetId = "court";
            string name = targetType == "RoyalCourt" ? "شبكة ظل البلاط" : $"شبكة {request.TargetName}";
            return IntelligenceSystem.EstablishNetwork(state, name, targetType, targetId);
        }

        private static GameActionResult ExecuteSpyOperation(GameState state, AiActionRequest request, string operationType, int cost, int days)
        {
            var network = state.SpyNetworks
                .Where(n => n.TargetType == "RoyalCourt" || n.TargetId == request.TargetId || n.TargetId == request.TargetName)
                .OrderByDescending(n => n.Strength + n.Infiltration + n.Analysis)
                .FirstOrDefault();

            if (network == null)
            {
                return new GameActionResult
                {
                    Success = false,
                    Title = operationType,
                    MainMessage = "لا توجد شبكة مناسبة لتنفيذ العملية. اقترح بناء شبكة أولاً."
                };
            }

            string targetType = request.TargetType == AiActionTargetType.Faction ? "Faction" : "InternalProvince";
            string targetId = request.TargetType == AiActionTargetType.Faction
                ? request.TargetId
                : ResolveGovernorProvinceName(state, request);

            return IntelligenceSystem.StartOperation(state, operationType, operationType, targetType, targetId, network.Id, cost, days);
        }

        private static string ResolveGovernorProvinceName(GameState state, AiActionRequest request)
        {
            var governor = state.Governors.FirstOrDefault(g => g.Id == request.TargetId || g.Name == request.TargetName || g.ProvinceName == request.TargetName);
            return governor?.ProvinceName ?? request.TargetName;
        }

        private static GameActionResult ReviewSpymasterReports(GameState state)
        {
            string recent = state.SecretReports.Count == 0
                ? "لا توجد تقارير سرية مكتملة بعد."
                : string.Join("\n", state.SecretReports.TakeLast(5));

            return new GameActionResult
            {
                Success = true,
                Title = "مراجعة تقارير الجواسيس",
                MainMessage = "تمت مراجعة تقارير مسؤول الجواسيس.\n" + recent
            };
        }

        private static GameActionResult ExecuteRelief(GameState state, AiActionRequest request)
        {
            var disaster = state.ActiveDisasters.FirstOrDefault(d =>
                d.ProvinceId == request.TargetId ||
                d.ProvinceName == request.TargetName ||
                d.Id == request.TargetId);

            if (disaster == null)
            {
                return new GameActionResult
                {
                    Success = false,
                    Title = "إغاثة مقاطعة",
                    MainMessage = "لا توجد كارثة نشطة في المقاطعة المحددة حالياً."
                };
            }

            return DisasterSystem.ProvideRelief(state, disaster.Id, request.GoldCost > 0 ? request.GoldCost : 100);
        }

        private static GameActionResult ExecuteConstruction(GameState state, AiActionRequest request)
        {
            int provinceIndex = state.Provinces.FindIndex(p => p.Id == request.TargetId || p.Name == request.TargetName);
            if (provinceIndex < 0)
            {
                provinceIndex = state.Provinces
                    .Select((p, i) => new { p, i })
                    .OrderBy(p => p.p.Buildings.Where(b => b.BuildingType == "سوق").Sum(b => b.Level))
                    .ThenByDescending(p => p.p.Income)
                    .FirstOrDefault()?.i ?? -1;
            }

            if (provinceIndex < 0)
                return new GameActionResult { Success = false, Title = "اقتراح بناء", MainMessage = "لا توجد مقاطعة مناسبة للبناء." };

            return EconomySystem.UpgradeBuilding(state, provinceIndex, "سوق");
        }

        private static GameActionResult RequestCouncilMeeting(GameState state, AiAgentProfile profile)
        {
            state.Prestige = Math.Min(9999, state.Prestige + 1);
            return new GameActionResult
            {
                Success = true,
                Title = "اجتماع مجلس",
                MainMessage = $"{profile.CharacterName} دعا إلى اجتماع محدود للمجلس. لم يتغير شيء خطير، لكن انتظام الحكم زاد قليلاً."
            };
        }

        private static GameActionResult WarnAboutSuccession(GameState state, AiAgentProfile profile)
        {
            string message = string.IsNullOrWhiteSpace(state.HeirName)
                ? "الخلافة بلا وريث معلن، وهذا يفتح باب الفصائل عند موت الخليفة."
                : $"الوريث {state.HeirName} يحتاج دعماً سياسياً ودينياً حتى لا يتحول الطموح حوله إلى أزمة.";

            profile.MemoryNotes.Add("قدّم تحذيراً عن الخلافة.");
            return new GameActionResult
            {
                Success = true,
                Title = "تحذير خلافة",
                MainMessage = $"{profile.CharacterName}: {message}"
            };
        }

        private static GameActionResult ExecuteMarriageAlliance(GameState state, AiActionRequest request)
        {
            int index = state.Neighbors.FindIndex(n => n.Id == request.TargetId || n.Name == request.TargetName);
            if (index < 0)
                return new GameActionResult { Success = false, Title = "زواج سياسي", MainMessage = "الدولة المقترحة غير موجودة." };

            return DynastySystem.ArrangeMarriage(state, index);
        }

        private static GameActionResult ExecuteArmyMove(GameState state, AiActionRequest request, AiAgentProfile profile)
        {
            var army = state.Armies.OrderByDescending(a => a.TotalSoldiers).FirstOrDefault();
            string target = string.IsNullOrWhiteSpace(request.TargetName) ? state.Provinces.OrderBy(p => p.LocalGarrison).FirstOrDefault()?.Name : request.TargetName;
            if (army == null || string.IsNullOrWhiteSpace(target))
                return new GameActionResult { Success = false, Title = "تحريك الجيش", MainMessage = "لا يوجد جيش أو هدف واضح للتحرك." };

            if (profile.AuthorityLevel < AiAuthorityLevel.RoyalRightHand)
            {
                return new GameActionResult
                {
                    Success = true,
                    Title = "توصية عسكرية",
                    MainMessage = $"{profile.CharacterName} أوصى بتحريك {army.Name} نحو {target}، لكنه لا يملك صلاحية إصدار أمر حركة مباشر."
                };
            }

            return ArmyCommandSystem.SendArmy(state, army.Id, target);
        }

        private static GameActionResult ExecuteDiplomaticMessage(GameState state, AiActionRequest request)
        {
            int index = state.Neighbors.FindIndex(n => n.Id == request.TargetId || n.Name == request.TargetName);
            if (index < 0)
                return new GameActionResult { Success = false, Title = "رسالة دبلوماسية", MainMessage = "الدولة المستهدفة غير موجودة." };

            string text = DiplomacySystem.SendEnvoy(state, index);
            return new GameActionResult
            {
                Success = !text.Contains("لا تملك"),
                Title = "رسالة دبلوماسية",
                MainMessage = text,
                ResourceChanges = text.Contains("50 ذهب") ? new System.Collections.Generic.Dictionary<string, int> { { "الذهب", -50 } } : new System.Collections.Generic.Dictionary<string, int>()
            };
        }

        private static GameActionResult ExecuteCalmGovernor(GameState state, AiActionRequest request, AiAgentProfile profile)
        {
            var governor = state.Governors
                .OrderBy(g => g.OpinionOfKing + g.Loyalty)
                .FirstOrDefault(g => string.IsNullOrWhiteSpace(request.TargetId) || g.Id == request.TargetId || g.Name == request.TargetName || g.ProvinceName == request.TargetName);

            if (governor == null)
                return new GameActionResult { Success = false, Title = "تهدئة والٍ", MainMessage = "لم يتم العثور على والٍ مناسب." };

            int cost = request.GoldCost > 0 ? request.GoldCost : 80;
            if (state.Gold < cost)
                return new GameActionResult { Success = false, Title = "تهدئة والٍ", MainMessage = $"تحتاج إلى {cost} ذهب لإرسال وفد وهدية سياسية." };

            state.Gold -= cost;
            governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 10);
            governor.Loyalty = Math.Min(100, governor.Loyalty + 6);
            governor.UpdateMood();

            return new GameActionResult
            {
                Success = true,
                Title = "تهدئة والٍ",
                MainMessage = $"{profile.CharacterName} رتّب وفداً هادئاً إلى {governor.Name} في {governor.ProvinceName}. تحسن رأيه وهدأ مزاجه.",
                ResourceChanges = new System.Collections.Generic.Dictionary<string, int> { { "الذهب", -cost } },
                SoundEffectKey = "paper"
            };
        }

        private static void RegisterCooldown(AiAgentProfile profile, AiActionRequest request)
        {
            string key = request.ActionType.ToString() + ":" + request.TargetId;
            int days = Math.Max(7, request.TimeCostDays);
            profile.Cooldowns[key] = days;
        }

        private static void RegisterAutonomousBudget(GameState state, int actualCost, bool approvedByKing)
        {
            if (!approvedByKing && actualCost > 0)
                state.DelegatedAuthoritySettings.AutonomousBudgetSpentThisMonth += actualCost;
        }

        private static void AddMemory(AiAgentProfile profile, AiActionRequest request, GameActionResult result)
        {
            string text = $"{request.CreatedDate}: {AiAgentSystem.GetActionDisplayName(request.ActionType)} - {(result.Success ? "نجح" : "فشل")}.";
            profile.LastActions.Add(text);
            profile.MemoryNotes.Add(result.Success ? request.ExpectedBenefit : result.MainMessage);
            while (profile.LastActions.Count > 8) profile.LastActions.RemoveAt(0);
            while (profile.MemoryNotes.Count > 12) profile.MemoryNotes.RemoveAt(0);
        }

        private static void AddLog(GameState state, AiActionRequest request, GameActionResult result, bool successful, bool approvedByKing)
        {
            state.AiActionLog.Add(new AiActionLogEntry
            {
                Date = state.Time.GetDateString(),
                DayNumber = DiplomacySystem.GetCurrentDayNumber(state),
                AgentName = request.AgentName,
                Role = request.Role,
                ActionTaken = AiAgentSystem.GetActionDisplayName(request.ActionType),
                Cost = request.GoldCost,
                Result = result.MainMessage,
                Risk = request.EstimatedRisk,
                WasSuccessful = successful,
                PlayerCanRespond = !approvedByKing || !successful
            });

            while (state.AiActionLog.Count > 80)
                state.AiActionLog.RemoveAt(0);
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
