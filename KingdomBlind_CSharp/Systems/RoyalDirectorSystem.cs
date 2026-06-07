using System;
using System.Collections.Generic;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class RoyalDirectorSystem
    {
        public static GameActionResult ProcessDailyDirector(GameState state)
        {
            var result = new GameActionResult { Title = "المخرج الملكي", Success = true, ShouldNarrate = false };
            state.ReconcileOldSaves();

            if (state.SuppressRandomMajorEvents)
                return result;

            if (state.RoyalDirectorCooldownDays > 0)
            {
                state.RoyalDirectorCooldownDays--;
                return result;
            }

            var candidate = ChooseCandidate(state);
            if (candidate == null)
                return result;

            if (state.LivingRealmLog.Any(e => !e.IsResolved && e.EventType == candidate.EventType))
                return result;

            var realmEvent = new LivingRealmEvent
            {
                EventType = candidate.EventType,
                ActorType = candidate.ActorType,
                ActorId = candidate.ActorId,
                ActorName = candidate.ActorName,
                Title = candidate.Title,
                Description = candidate.Description,
                CouncilAdvice = candidate.CouncilAdvice,
                DateText = state.Time?.GetDateString() ?? "",
                CreatedDay = DiplomacySystem.GetCurrentDayNumber(state),
                Severity = candidate.Severity,
                RequiresPause = candidate.RequiresPause,
                RequiresDecision = true
            };

            state.LivingRealmLog.Add(realmEvent);
            state.LastRoyalDirectorEventKey = candidate.EventType;
            state.RoyalDirectorCooldownDays = Math.Clamp(18 + candidate.Severity * 5, 18, 45);

            result.ShouldNarrate = true;
            result.ShouldPauseTime = candidate.RequiresPause;
            result.MainMessage = $"{realmEvent.Title}\n{realmEvent.Description}";
            result.SoundEffectKey = "paper";

            DynastyChronicleSystem.RecordEvent(
                state,
                "Director",
                candidate.Title,
                candidate.Description,
                candidate.GloryPreview,
                candidate.Severity);

            return result;
        }

        public static GameActionResult ResolveDirectorEvent(GameState state, LivingRealmEvent realmEvent, string choice)
        {
            var result = new GameActionResult { Title = "قرار المخرج الملكي" };
            state.ReconcileOldSaves();

            switch (realmEvent.EventType)
            {
                case "DirectorAmbitiousPlot":
                    ResolveAmbitiousPlot(state, realmEvent, choice, result);
                    break;
                case "DirectorPostWarDemands":
                    ResolvePostWarDemands(state, realmEvent, choice, result);
                    break;
                case "DirectorTradeOpportunity":
                    ResolveTradeOpportunity(state, realmEvent, choice, result);
                    break;
                case "DirectorSuccessionPressure":
                    ResolveSuccessionPressure(state, realmEvent, choice, result);
                    break;
                case "DirectorSpymasterShadowWar":
                    ResolveSpymasterShadowWar(state, realmEvent, choice, result);
                    break;
                case "DirectorBorderEnvy":
                    ResolveBorderEnvy(state, realmEvent, choice, result);
                    break;
                default:
                    result.Success = true;
                    result.MainMessage = "تم إغلاق الحدث.";
                    break;
            }

            if (result.Success)
                realmEvent.IsResolved = true;

            return result;
        }

        private static DirectorCandidate? ChooseCandidate(GameState state)
        {
            var candidates = new List<DirectorCandidate>();
            int totalIncome = state.Provinces.Where(p => !p.Occupied).Sum(p => p.Income);
            bool stable = state.ActiveWar == null && state.Satisfaction >= 70 && state.Gold >= 1600;

            if (stable)
            {
                var ambitious = state.Governors
                    .OrderByDescending(g => g.Ambition + g.Influence - g.OpinionOfKing)
                    .FirstOrDefault(g => g.Ambition >= 65 || g.Influence >= 65);
                if (ambitious != null)
                {
                    candidates.Add(new DirectorCandidate
                    {
                        EventType = "DirectorAmbitiousPlot",
                        ActorType = "Governor",
                        ActorId = ambitious.Id,
                        ActorName = ambitious.Name,
                        Priority = 85,
                        Severity = 3,
                        Title = $"طموح في ظل رخاء بغداد",
                        Description = $"استقرار الخزينة ورضا الرعية جعلا بعض أهل النفوذ ينظرون إلى العرش كأنه أقل يقظة. {ambitious.Name} يكثر لقاءاته الخاصة ويجمع كلمات المديح حوله.",
                        CouncilAdvice = "الوزير الأول يقترح احتواءه بمنصب واضح. مسؤول الاستخبارات يطلب مراقبة هادئة. القائد يرى أن إظهار القوة يكفي.",
                        GloryPreview = -2
                    });
                }
            }

            if (state.ActiveWar == null && state.DynastyChronicle.Any(e => e.Category == "War" && e.DayNumber >= DiplomacySystem.GetCurrentDayNumber(state) - 90))
            {
                var faction = state.Factions.OrderByDescending(f => f.Discontent).FirstOrDefault(f => f.IsActive);
                candidates.Add(new DirectorCandidate
                {
                    EventType = "DirectorPostWarDemands",
                    ActorType = faction != null ? "Faction" : "Council",
                    ActorId = faction?.Id ?? "council",
                    ActorName = faction?.Name ?? "مجلس الولاة",
                    Priority = 78,
                    Severity = 3,
                    Title = "مطالب ما بعد الحرب",
                    Description = "لم تجف أخبار الحرب بعد، والولاة والجند يطلبون مكافآت أو تخفيفاً للضرائب. المملكة خرجت من الضغط العسكري، لكن السياسة بدأت تطالب بثمنها.",
                    CouncilAdvice = "الوزير الأول يقترح تنازلاً محدوداً. القائد يخشى أن يبدو البلاط مرهقاً. رجل الدين يفضل تهدئة الناس.",
                    GloryPreview = 0
                });
            }

            if ((state.Treaties.Any(t => t.IsActive && t.TreatyType == "TradeAgreement") ||
                 state.ProtectedTradeRoutes.Count > 0 ||
                 state.SeasonalMarketDaysLeft > 0) &&
                state.MerchantsTrust >= 55)
            {
                candidates.Add(new DirectorCandidate
                {
                    EventType = "DirectorTradeOpportunity",
                    ActorType = "Merchants",
                    ActorId = "merchant_guild",
                    ActorName = "نقابة تجار بغداد",
                    Priority = 74 + state.ProtectedTradeRoutes.Count * 3,
                    Severity = 2,
                    Title = "قافلة نادرة على طرق بغداد",
                    Description = "ازدهار الطرق جذب قافلة كبيرة تحمل بضائع نادرة. التجار يعرضون توسيع العقود إذا حصلوا على حماية ورعاية رسمية.",
                    CouncilAdvice = "وزير المالية يرى فيها دخلاً طويل الأمد. القائد يطلب حراسة للطريق. الوزير الأول يحذر من إعطاء التجار نفوذاً بلا رقابة.",
                    GloryPreview = 4
                });
            }

            bool weakHeir = string.IsNullOrWhiteSpace(state.HeirName) ||
                            state.HeirAge < 8 ||
                            state.Factions.Any(f => f.IsActive && f.Type.Contains("Succession"));
            if (weakHeir)
            {
                candidates.Add(new DirectorCandidate
                {
                    EventType = "DirectorSuccessionPressure",
                    ActorType = "Dynasty",
                    ActorId = "succession",
                    ActorName = "بيت الخلافة",
                    Priority = 82,
                    Severity = 4,
                    Title = "همس حول الخلافة",
                    Description = "بعض أهل البلاط يسألون بصوت خافت: من يحمل العهد بعد الخليفة؟ الغموض صار مادة للنساء والوزراء والولاة.",
                    CouncilAdvice = "الوزير الأول يطلب إعلاناً واضحاً. الملكة تريد حماية جناحها. رجل الدين يفضل تثبيت الشرعية قبل أن تتحول الهمسات إلى بيعة بديلة.",
                    GloryPreview = -3
                });
            }

            var spymaster = state.Council.Values.FirstOrDefault(c => c.Title.Contains("استخ"));
            if (spymaster != null && spymaster.Influence >= 78)
            {
                candidates.Add(new DirectorCandidate
                {
                    EventType = "DirectorSpymasterShadowWar",
                    ActorType = "Councilor",
                    ActorId = spymaster.Title,
                    ActorName = spymaster.Name,
                    Priority = 80,
                    Severity = 4,
                    Title = "حرب ظل داخل القصر",
                    Description = $"{spymaster.Name} صار يمسك بخيوط كثيرة. الحراس والكتبة يتجنبون ذكر اسمه، وبعض الوزراء بدأوا يطلبون موافقته قبل رفع التقارير.",
                    CouncilAdvice = "الوزير الأول يخشى دولة داخل الدولة. القائد يعرض حماية المجلس. مسؤول الاستخبارات نفسه يزعم أن النفوذ ضرورة لحماية الخليفة.",
                    GloryPreview = -4
                });
            }

            var hostile = state.Neighbors
                .Where(n => !n.IsAlly && !n.HasNonAggressionPact && n.Opinion < -35)
                .OrderByDescending(n => n.MilitaryAmbition + Math.Max(0, n.Army - state.Army / 5))
                .FirstOrDefault();
            if (hostile != null && state.ActiveWar == null && state.Army < Math.Max(900, hostile.Army * 4))
            {
                candidates.Add(new DirectorCandidate
                {
                    EventType = "DirectorBorderEnvy",
                    ActorType = "Neighbor",
                    ActorId = hostile.Id,
                    ActorName = hostile.Name,
                    Priority = 76,
                    Severity = 3,
                    Title = $"حسد حدودي من {hostile.Name}",
                    Description = $"{hostile.Name} تراقب دخل الخلافة وضعف بعض الحاميات. ليس إعلان حرب، لكنه اختبار لهيبة بغداد على الحدود.",
                    CouncilAdvice = "القائد يطلب تقوية الحاميات. الوزير الأول يفضل رسالة دبلوماسية قاسية. مسؤول الجواسيس يقترح معرفة نية البلاط المقابل.",
                    GloryPreview = 0
                });
            }

            return candidates
                .Where(c => c.EventType != state.LastRoyalDirectorEventKey || c.Priority >= 85)
                .OrderByDescending(c => c.Priority)
                .FirstOrDefault();
        }

        private static void ResolveAmbitiousPlot(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var governor = state.Governors.FirstOrDefault(g => g.Id == realmEvent.ActorId);
            if (choice == "Watch")
            {
                if (state.Gold < 80)
                {
                    Fail(result, "تحتاج إلى 80 ذهب لتمويل مراقبة هادئة.");
                    return;
                }
                state.Gold -= 80;
                if (governor != null)
                {
                    governor.Fear = Math.Clamp(governor.Fear + 8, 0, 100);
                    governor.SecretPlan = "مراقب من البلاط";
                }
                Success(result, "بدأت مراقبة هادئة للطموح السياسي. ارتفع خوف صاحب النفوذ وانخفض خطر المفاجأة.", -80, 2);
                DynastyChronicleSystem.RecordEvent(state, "Intrigue", "مراقبة طموح داخلي", $"وضع الخليفة {realmEvent.ActorName} تحت مراقبة هادئة.", 2, 2);
            }
            else if (choice == "Honor")
            {
                state.Prestige = Math.Max(0, state.Prestige - 10);
                if (governor != null) governor.OpinionOfKing = Math.Clamp(governor.OpinionOfKing + 10, -100, 100);
                Success(result, "منحته تكريماً محدوداً داخل البلاط. كسبت وقتاً، لكن بعض الهيبة صُرفت على الاحتواء.", 0, 1);
            }
            else
            {
                if (governor != null) governor.Ambition = Math.Clamp(governor.Ambition + 5, 0, 100);
                Success(result, "تجاهلت الهمس حالياً. الطموح لم ينطفئ، لكنه لم يتحول إلى أزمة مباشرة.", 0, -1);
            }
        }

        private static void ResolvePostWarDemands(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            if (choice == "Concede")
            {
                state.Gold = Math.Max(0, state.Gold - 120);
                foreach (var faction in state.Factions.Where(f => f.IsActive))
                    faction.Discontent = Math.Max(0, faction.Discontent - 10);
                Success(result, "قدمت عطايا وتنازلات محدودة. هدأ السخط، لكن الخزينة دفعت الثمن.", -120, 2);
            }
            else if (choice == "Promise")
            {
                LivingRealmSystem.AddPromise(state, "PostWarRelief", "Realm", "realm", "الولاة والجند", "مكافآت وتخفيف بعد الحرب", 60, "حافظ على ذهب كافٍ وخفف الضرائب أو امنح عطايا من شاشة الاقتصاد.", 10, 16);
                Success(result, "قطعت وعداً سياسياً بتخفيف آثار الحرب. سيمنحك هذا وقتاً، لا عذراً دائماً.", 0, 1);
            }
            else
            {
                foreach (var faction in state.Factions.Where(f => f.IsActive))
                    faction.Discontent = Math.Clamp(faction.Discontent + 8, 0, 100);
                Success(result, "رفضت المطالب. فهم المجلس أن العرش لا يساوم بسهولة، لكن السخط ارتفع.", 0, -2);
            }
        }

        private static void ResolveTradeOpportunity(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            if (choice == "Fund")
            {
                if (state.Gold < 150)
                {
                    Fail(result, "تحتاج إلى 150 ذهب لرعاية القافلة.");
                    return;
                }
                state.Gold -= 150;
                state.ActiveSupplyContracts += 1;
                state.MerchantsTrust = Math.Clamp(state.MerchantsTrust + 8, 0, 100);
                Success(result, "رعيت القافلة رسمياً. زادت ثقة التجار وبدأ عقد توريد جديد يدر دخلاً شهرياً.", -150, 5);
                DynastyChronicleSystem.RecordEvent(state, "Trade", "قافلة بغداد الكبرى", "رعت الخلافة قافلة نادرة وحولتها إلى عقد تجاري دائم.", 8, 2);
            }
            else if (choice == "Escort")
            {
                var route = state.ProtectedTradeRoutes.Contains("طريق بغداد-دمشق") ? "طريق بغداد-حلب" : "طريق بغداد-دمشق";
                var protect = EconomySystem.ProtectTradeRoute(state, route);
                if (!protect.Success)
                {
                    result.Success = false;
                    result.MainMessage = protect.MainMessage;
                    return;
                }
                result.Success = true;
                result.MainMessage = protect.MainMessage;
                result.ResourceChanges = protect.ResourceChanges;
                DynastyChronicleSystem.RecordEvent(state, "Trade", "حماية قافلة نادرة", $"أرسلت الخلافة قوات لحماية {route}.", 5, 2);
            }
            else
            {
                state.MerchantsTrust = Math.Clamp(state.MerchantsTrust - 4, 0, 100);
                Success(result, "تركت القافلة تمضي بلا رعاية. لم تخسر ذهباً، لكن التجار رأوا أن البلاط بطيء في اقتناص الفرص.", 0, -1);
            }
        }

        private static void ResolveSuccessionPressure(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            if (choice == "Proclaim")
            {
                if (string.IsNullOrWhiteSpace(state.HeirName))
                {
                    Fail(result, "لا يوجد وريث واضح لإعلانه الآن.");
                    return;
                }
                state.Prestige += 8;
                state.Satisfaction = Math.Clamp(state.Satisfaction + 2, 0, 100);
                Success(result, $"أعلنت دعمك العلني لولاية {state.HeirName}. هدأت بعض الهمسات وزادت هيبة الانتقال.", 0, 6);
                DynastyChronicleSystem.RecordEvent(state, "Succession", "إعلان ولاية العهد", $"ثبت الخليفة {state.HeirName} في كتاب العرش.", 10, 3);
            }
            else if (choice == "Council")
            {
                foreach (var member in state.Council.Values)
                    member.Trust = Math.Clamp(member.Trust + 3, 0, 100);
                Success(result, "جمعت المجلس حول ملف الخلافة. لم يُحسم كل شيء، لكن القرار صار مؤسسياً أكثر.", 0, 3);
            }
            else
            {
                state.Prestige = Math.Max(0, state.Prestige - 5);
                Success(result, "أجلت الحديث عن الخلافة. الهدوء بقي ظاهرياً، لكن الغموض ازداد في القصر.", 0, -4);
            }
        }

        private static void ResolveSpymasterShadowWar(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var spymaster = state.Council.Values.FirstOrDefault(c => c.Title.Contains("استخ"));
            if (choice == "Limit")
            {
                if (spymaster != null)
                {
                    spymaster.Influence = Math.Clamp(spymaster.Influence - 12, 0, 100);
                    spymaster.Trust = Math.Clamp(spymaster.Trust - 4, 0, 100);
                }
                Success(result, "حددت صلاحيات مدير الاستخبارات ووزعت بعض مفاتيحه على المجلس. انخفض نفوذه، وربما تضررت ثقته بك.", 0, 3);
            }
            else if (choice == "FundCounter")
            {
                if (state.Gold < 120)
                {
                    Fail(result, "تحتاج إلى 120 ذهب لتمويل رقابة مضادة.");
                    return;
                }
                state.Gold -= 120;
                state.CounterIntelligenceLevel = Math.Clamp(state.CounterIntelligenceLevel + 8, 0, 100);
                Success(result, "مولت رقابة مضادة داخل القصر. ارتفع مستوى مكافحة التجسس وخفت قبضة الظل قليلاً.", -120, 4);
            }
            else
            {
                if (spymaster != null) spymaster.Influence = Math.Clamp(spymaster.Influence + 5, 0, 100);
                Success(result, "تركت شبكة الظل تعمل كما هي. ربما أفادتك اليوم، لكنها ستكبر غداً.", 0, -3);
            }
        }

        private static void ResolveBorderEnvy(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == realmEvent.ActorId);
            if (choice == "Envoy")
            {
                if (state.Gold < 60)
                {
                    Fail(result, "تحتاج إلى 60 ذهب لإرسال وفد قوي.");
                    return;
                }
                state.Gold -= 60;
                if (neighbor != null)
                {
                    neighbor.Opinion = Math.Clamp(neighbor.Opinion + 8, -100, 100);
                    neighbor.MilitaryAmbition = Math.Clamp(neighbor.MilitaryAmbition - 5, 0, 100);
                }
                Success(result, "أرسلت وفداً حازماً. تحسنت العلاقة قليلاً وانخفض اندفاع الجار نحو الحدود.", -60, 2);
            }
            else if (choice == "Fortify")
            {
                var border = state.Provinces.OrderBy(p => p.LocalGarrison).FirstOrDefault();
                if (state.Gold < 100 || border == null)
                {
                    Fail(result, "تحتاج إلى 100 ذهب ومقاطعة صالحة لتقوية الحدود.");
                    return;
                }
                state.Gold -= 100;
                border.LocalGarrison += 120;
                Success(result, $"قويت حامية {border.Name}. الحدود ستتحدث بلغة أوضح من الرسائل.", -100, 3);
            }
            else
            {
                if (neighbor != null) neighbor.MilitaryAmbition = Math.Clamp(neighbor.MilitaryAmbition + 4, 0, 100);
                Success(result, "لم تتحرك الآن. الجار قرأ الصمت بطريقته.", 0, -2);
            }
        }

        private static void Success(GameActionResult result, string message, int goldChange, int gloryChange)
        {
            result.Success = true;
            result.MainMessage = message;
            if (goldChange != 0)
                result.ResourceChanges.Add("الذهب", goldChange);
            if (gloryChange != 0)
                result.ResourceChanges.Add("مجد السلالة", gloryChange);
        }

        private static void Fail(GameActionResult result, string message)
        {
            result.Success = false;
            result.MainMessage = message;
        }

        private sealed class DirectorCandidate
        {
            public string EventType { get; set; } = "";
            public string ActorType { get; set; } = "";
            public string ActorId { get; set; } = "";
            public string ActorName { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string CouncilAdvice { get; set; } = "";
            public int Priority { get; set; }
            public int Severity { get; set; } = 1;
            public bool RequiresPause { get; set; } = true;
            public int GloryPreview { get; set; }
        }
    }
}
