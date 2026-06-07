using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiContextBuilder
    {
        public static AiAgentContext BuildContext(GameState state, AiAgentProfile profile)
        {
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);

            var context = new AiAgentContext
            {
                AgentName = profile.CharacterName,
                Role = profile.Role,
                Summary = $"{profile.CharacterName} يتصرف بصفته {AiAgentSystem.GetRoleDisplayName(profile.Role)}. النبرة: {AiAgentSystem.GetRoleTone(profile.Role)}"
            };

            switch (profile.Role)
            {
                case AiAgentRole.Spymaster:
                    BuildSpymasterContext(state, profile, context);
                    break;
                case AiAgentRole.FirstMinister:
                    BuildFirstMinisterContext(state, context);
                    break;
                case AiAgentRole.MilitaryCommander:
                    BuildCommanderContext(state, context);
                    break;
                case AiAgentRole.Cleric:
                    BuildClericContext(state, context);
                    break;
                case AiAgentRole.DiplomaticAdvisor:
                    BuildDiplomatContext(state, context);
                    break;
                case AiAgentRole.Governor:
                    BuildGovernorContext(state, profile, context);
                    break;
                case AiAgentRole.NeighborRuler:
                    BuildNeighborContext(state, profile, context);
                    break;
                case AiAgentRole.SpouseQueen:
                    BuildSpouseContext(state, profile, context);
                    break;
                case AiAgentRole.Heir:
                    BuildHeirContext(state, context);
                    break;
                case AiAgentRole.MerchantRepresentative:
                    BuildMerchantContext(state, context);
                    break;
                default:
                    context.KnownFacts.Add($"التاريخ: {state.Time.GetDateString()}.");
                    context.KnownFacts.Add($"الذهب العام في الخزينة: {state.Gold}.");
                    break;
            }

            foreach (var note in profile.MemoryNotes.TakeLast(5))
                context.RecentMemories.Add(note);

            return context;
        }

        public static bool HasSufficientKnowledge(GameState state, AiAgentProfile profile, AiActionRequest request, out string reason)
        {
            reason = "";
            if (profile == null)
            {
                reason = "لا يوجد ملف وكيل لهذه الشخصية.";
                return false;
            }

            switch (request.TargetType)
            {
                case AiActionTargetType.Governor:
                    var governor = !string.IsNullOrWhiteSpace(request.TargetId)
                        ? state.Governors.FirstOrDefault(g => g.Id == request.TargetId)
                        : null;
                    governor ??= state.Governors.FirstOrDefault(g => g.Name == request.TargetName || g.ProvinceName == request.TargetName);
                    if (governor == null)
                    {
                        reason = "الوالي المستهدف غير معروف أو غير موجود.";
                        return false;
                    }

                    if (profile.Role == AiAgentRole.Governor && profile.SourceId != governor.Id)
                    {
                        reason = "الوالي يعرف شؤون مقاطعته فقط ولا يملك معرفة كافية عن ولاة آخرين.";
                        return false;
                    }
                    break;
                case AiActionTargetType.Faction:
                    if (!state.Factions.Any(f => f.Id == request.TargetId || f.Name == request.TargetName))
                    {
                        reason = "الفصيل المستهدف غير موجود.";
                        return false;
                    }
                    if (profile.Role != AiAgentRole.Spymaster && profile.Role != AiAgentRole.FirstMinister)
                    {
                        reason = "هذا النوع من الأهداف يحتاج معرفة أمنية أو إدارية لا يملكها هذا الدور.";
                        return false;
                    }
                    break;
                case AiActionTargetType.NeighborKingdom:
                    if (!state.Neighbors.Any(n => n.Id == request.TargetId || n.Name == request.TargetName))
                    {
                        reason = "الدولة المستهدفة غير معروفة.";
                        return false;
                    }
                    break;
                case AiActionTargetType.Province:
                    if (!state.Provinces.Any(p => p.Id == request.TargetId || p.Name == request.TargetName))
                    {
                        reason = "المقاطعة المستهدفة غير موجودة.";
                        return false;
                    }
                    if (profile.Role == AiAgentRole.Governor)
                    {
                        var ownGovernor = state.Governors.FirstOrDefault(g => g.Id == profile.SourceId);
                        if (ownGovernor != null && ownGovernor.ProvinceId != request.TargetId && ownGovernor.ProvinceName != request.TargetName)
                        {
                            reason = "الوالي لا يملك معرفة كافية خارج مقاطعته.";
                            return false;
                        }
                    }
                    break;
            }

            if (request.ActionType is AiActionType.InvestigateGovernor or AiActionType.DisruptFaction)
            {
                bool hasNetwork = state.SpyNetworks.Any(n =>
                    n.TargetType == "RoyalCourt" ||
                    n.TargetId == request.TargetId ||
                    n.TargetId == request.TargetName);
                if (!hasNetwork && request.ActionType != AiActionType.BuildSpyNetwork)
                {
                    reason = "لا توجد شبكة استخبارات مناسبة لهذا الهدف. يستطيع مسؤول الجواسيس اقتراح بناء شبكة أولاً.";
                    return false;
                }
            }

            return true;
        }

        public static string BuildDialogueContext(GameState state, AiAgentProfile profile, AiActionRequest request)
        {
            var context = BuildContext(state, profile);
            string detailLevel = AppConfig.Load().AiActors.DialogueLengthLevel switch
            {
                AiDialogueLengthLevel.Brief => "مختصر جداً: سطران كحد أقصى.",
                AiDialogueLengthLevel.Detailed => "تفصيلي لكن دون إطالة مرهقة لقارئ الشاشة.",
                _ => "عادي: ملخص قصير ثم سبب واحد أو سببين."
            };

            return context + "\n\n" +
                   $"طلب الفعل: {AiAgentSystem.GetActionDisplayName(request.ActionType)}.\n" +
                   $"الهدف: {request.TargetName}.\n" +
                   $"الفائدة المتوقعة: {request.ExpectedBenefit}.\n" +
                   $"الخطر: {request.EstimatedRisk} من 100.\n" +
                   $"تعليمات الوصول: {detailLevel} ابدأ دائماً بملخص قصير، ولا تكشف معلومات لا يعرفها الدور.";
        }

        private static void BuildSpymasterContext(GameState state, AiAgentProfile profile, AiAgentContext context)
        {
            context.KnownFacts.Add($"مستوى مكافحة الاستخبارات: {state.CounterIntelligenceLevel}/100.");
            context.KnownFacts.Add($"عدد شبكات الجواسيس: {state.SpyNetworks.Count}.");
            context.KnownFacts.Add($"العمليات الجارية: {state.IntelligenceOperations.Count(o => o.Status == "Active")}.");
            context.KnownFacts.Add($"الفصائل المشبوهة: {state.Factions.Count(f => f.IsActive && (f.Discontent > 50 || f.PowerPercent > 45))}.");
            context.KnownFacts.Add($"ثقة الملك به: {profile.Trust}/100، نفوذه التقريبي: {state.Council.GetValueOrDefault("spymaster")?.Influence ?? profile.Ambition}/100.");
            context.UnknownLimits.Add("لا يعرف الأسرار غير المكتشفة ولا نتائج العمليات قبل اكتمالها.");
        }

        private static void BuildFirstMinisterContext(GameState state, AiAgentContext context)
        {
            context.KnownFacts.Add($"الذهب: {state.Gold}. المؤونة: {state.Food}. الرضا العام: {state.Satisfaction}/100.");
            context.KnownFacts.Add($"مستوى الضرائب: {state.TaxLevel}. ثقة التجار: {state.MerchantsTrust}/100.");
            context.KnownFacts.Add($"الكوارث النشطة: {state.ActiveDisasters.Count}. الفصائل النشطة: {state.Factions.Count(f => f.IsActive)}.");
            context.KnownFacts.Add($"متوسط رأي الولاة: {(state.Governors.Count == 0 ? 0 : (int)state.Governors.Average(g => g.OpinionOfKing))}.");
            context.UnknownLimits.Add("لا يعرف خطط الجواسيس التفصيلية ولا نوايا الدول إلا عبر تقارير معلنة.");
        }

        private static void BuildCommanderContext(GameState state, AiAgentContext context)
        {
            context.KnownFacts.Add($"عدد الجيوش: {state.Armies.Count}. القوة الإجمالية: {state.Army}.");
            context.KnownFacts.Add(state.ActiveWar == null ? "لا توجد حرب خارجية قائمة." : $"حرب قائمة ونتيجتها التقريبية: {state.ActiveWar.WarScore}.");
            context.KnownFacts.Add($"أضعف حامية: {state.Provinces.OrderBy(p => p.LocalGarrison).FirstOrDefault()?.Name ?? "غير معروف"}.");
            context.UnknownLimits.Add("لا يستطيع معرفة نية العدو الدقيقة دون استطلاع.");
        }

        private static void BuildClericContext(GameState state, AiAgentContext context)
        {
            context.KnownFacts.Add($"الشرعية الدينية: {state.ReligiousLegitimacy}/100. رأي رجال الدين: {state.ClergyOpinion}/100.");
            context.KnownFacts.Add($"التوتر الديني: {state.ReligiousTension}/100. التقوى: {state.Piety}.");
            context.KnownFacts.Add(string.IsNullOrWhiteSpace(state.HeirName) ? "لا يوجد وريث معلن." : $"الوريث المعلن: {state.HeirName}.");
            context.UnknownLimits.Add("لا يحكم على الأسرار السياسية إلا إن ظهرت للعلن.");
        }

        private static void BuildDiplomatContext(GameState state, AiAgentContext context)
        {
            foreach (var neighbor in state.Neighbors.Take(5))
                context.KnownFacts.Add($"{neighbor.Name}: الرأي {neighbor.Opinion}، الثقة {neighbor.Trust}، العلاقة {neighbor.Relation}.");
            context.UnknownLimits.Add("تقدير القوة الأجنبية تقريبي ولا يكشف خططهم السرية.");
        }

        private static void BuildGovernorContext(GameState state, AiAgentProfile profile, AiAgentContext context)
        {
            var governor = state.Governors.FirstOrDefault(g => g.Id == profile.SourceId);
            var province = governor == null ? null : state.Provinces.FirstOrDefault(p => p.Id == governor.ProvinceId || p.Name == governor.ProvinceName);
            if (governor != null)
            {
                context.KnownFacts.Add($"المقاطعة: {governor.ProvinceName}. الولاء: {governor.Loyalty}. الرأي بالملك: {governor.OpinionOfKing}.");
                context.KnownFacts.Add($"القوة المحلية: {governor.MilitaryPower}. الثروة المحلية: {governor.Wealth}. المزاج: {governor.CurrentMood}.");
            }
            if (province != null)
                context.KnownFacts.Add($"رضا الرعية في المقاطعة: {province.Satisfaction}. الحامية: {province.LocalGarrison}. الدخل: {province.Income}.");
            context.UnknownLimits.Add("لا يعرف الوالي تفاصيل بقية الولايات إلا ما يظهر في المجلس أو السوق.");
        }

        private static void BuildNeighborContext(GameState state, AiAgentProfile profile, AiAgentContext context)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == profile.SourceId);
            if (neighbor != null)
            {
                context.KnownFacts.Add($"دولته: {neighbor.Name}. موقفه: {neighbor.DiplomaticStance}. رأيه بك: {neighbor.Opinion}.");
                context.KnownFacts.Add($"جيشه المعلن: {neighbor.Army}. ثقته بك: {neighbor.Trust}. هدفه السياسي: {neighbor.PoliticalGoal}.");
                context.KnownFacts.Add($"تقديره لقوتك لا يتجاوز إشارات الحدود، لا يعرف أرقامك الداخلية الدقيقة.");
            }
            context.UnknownLimits.Add("لا يعرف ذهبك الحقيقي ولا أسرارك الداخلية إلا عبر استخباراته المحدودة.");
        }

        private static void BuildSpouseContext(GameState state, AiAgentProfile profile, AiAgentContext context)
        {
            var wife = state.Wives.FirstOrDefault(w => w.Id == profile.SourceId);
            if (wife != null)
            {
                context.KnownFacts.Add($"رأيها بالملك: {wife.OpinionOfKing}. ثقتها: {wife.Trust}. نفوذها: {wife.Influence}.");
                context.KnownFacts.Add($"هدفها في القصر: {wife.CourtGoal}. أم الوريث: {(wife.IsMotherOfHeir ? "نعم" : "لا")}.");
                context.KnownFacts.Add(string.IsNullOrWhiteSpace(state.HeirName) ? "الخلافة غير محسومة." : $"الوريث المعلن: {state.HeirName}.");
            }
            context.UnknownLimits.Add("لا تعرف ما تخفيه شبكات الجواسيس خارج القصر.");
        }

        private static void BuildHeirContext(GameState state, AiAgentContext context)
        {
            context.KnownFacts.Add(string.IsNullOrWhiteSpace(state.HeirName) ? "لا يوجد وريث معلن." : $"الوريث المعلن: {state.HeirName} بعمر {state.HeirAge}.");
            context.KnownFacts.Add($"الهيبة: {state.Prestige}. الشرعية الدينية: {state.ReligiousLegitimacy}.");
            context.UnknownLimits.Add("لا يرى الخلافات السرية بين الوزراء إلا من خلال سلوكهم الظاهر.");
        }

        private static void BuildMerchantContext(GameState state, AiAgentContext context)
        {
            context.KnownFacts.Add($"ثقة التجار: {state.MerchantsTrust}/100. الطرق المحمية: {state.ProtectedTradeRoutes.Count}.");
            context.KnownFacts.Add($"السوق الموسمي: {(state.SeasonalMarketDaysLeft > 0 ? "نشط" : "غير نشط")}. عقود التوريد: {state.ActiveSupplyContracts}.");
            context.KnownFacts.Add($"الذهب المتاح في الخزينة حسب العلن: {state.Gold}.");
            context.UnknownLimits.Add("لا يعرف الأسرار العسكرية أو الدبلوماسية إلا أثرها على الطرق والأسعار.");
        }
    }
}
