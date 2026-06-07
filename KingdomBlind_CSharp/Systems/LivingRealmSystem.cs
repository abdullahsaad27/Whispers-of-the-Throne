using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class LivingRealmSystem
    {
        private static readonly Random Rand = new Random();

        
        public static GameActionResult ProcessMonthlyAI(GameState state)
        {
            var res = new GameActionResult { Success = true, ShouldNarrate = false, MainMessage = "" };
            System.Text.StringBuilder msg = new System.Text.StringBuilder();
            System.Random rand = new System.Random();
            var aiSettings = AppConfig.Load().AiActors ?? new AiActorSettings();

            // 1. Governors autonomous development
            if (!aiSettings.AllowAiGovernorDecisions)
            {
                foreach (var gov in state.Governors)
                {
                    if (gov.Loyalty > 60 && rand.Next(100) < 15) // 15% chance
                    {
                        var prov = state.Provinces.FirstOrDefault(p => p.Name == gov.ProvinceName);
                        if (prov != null)
                        {
                            var bType = rand.Next(100) < 50 ? "مزرعة" : "سوق";
                            var b = prov.Buildings.FirstOrDefault(x => x.BuildingType == bType);
                            if (b != null)
                            {
                                b.Level++;
                                msg.AppendLine($"قام الوالي {gov.Name} بتطوير {bType} في {prov.Name} من ماله الخاص لرفع مستوى المقاطعة!");
                                res.ShouldNarrate = true;
                            }
                        }
                    }

                    // Rivalries
                    if (state.Governors.Count > 1 && rand.Next(100) < 5)
                    {
                        var rival = state.Governors.FirstOrDefault(g => g.Id != gov.Id);
                        if (rival != null)
                        {
                            gov.Loyalty = Math.Max(0, gov.Loyalty - 10);
                            msg.AppendLine($"نشوب خلاف بين الوالي {gov.Name} والوالي {rival.Name}. أثر ذلك سلباً على ولاء {gov.Name}.");
                            res.ShouldNarrate = true;
                        }
                    }
                }
            }

            // 2. Neighbors autonomous wars and development
            if (!aiSettings.AllowAiNeighborRealmManagement)
            {
                foreach (var neighbor in state.Neighbors)
                {
                    if (neighbor.Relation == "تابع") continue; // Vassals don't act independently
                    
                    // Base growth
                    neighbor.Army += rand.Next(50, 150);
                    
                    // Wars against OTHER AI neighbors
                    if (rand.Next(100) < 5 && state.Neighbors.Count > 1) // 5% chance
                    {
                        var target = state.Neighbors.FirstOrDefault(n => n.Name != neighbor.Name && n.Relation != "تابع");
                        if (target != null)
                        {
                            int losses1 = rand.Next(100, 500);
                            int losses2 = rand.Next(100, 500);
                            neighbor.Army = Math.Max(0, neighbor.Army - losses1);
                            target.Army = Math.Max(0, target.Army - losses2);
                            
                            msg.AppendLine($"اندلعت مناوشات حدودية بين {neighbor.Name} و {target.Name}. تكبد الطرفان خسائر في الأرواح وتراجع تعداد جيشهما.");
                            res.ShouldNarrate = true;
                        }
                    }
                }
            }

            res.MainMessage = msg.ToString().Trim();
            return res;
        }

        public static GameActionResult ProcessDailyLivingRealm(GameState state)
        {
            var result = new GameActionResult
            {
                Title = "نبض العالم السياسي",
                Success = true,
                ShouldNarrate = false
            };

            state.ReconcileOldSaves();
            int today = DiplomacySystem.GetCurrentDayNumber(state);
            var reports = new List<string>();

            ArchiveExpiredMemories(state, today);
            string promiseReport = EvaluatePromises(state, today);
            if (!string.IsNullOrWhiteSpace(promiseReport))
                reports.Add(promiseReport);

            foreach (var neighbor in state.Neighbors)
            {
                neighbor.DaysUntilNextMove--;
                if (neighbor.DaysUntilNextMove <= 0)
                {
                    var realmEvent = CreateNeighborMove(state, neighbor, today);
                    neighbor.DaysUntilNextMove = Rand.Next(22, 46);
                    if (realmEvent != null)
                    {
                        AddRealmEvent(state, realmEvent);
                        reports.Add($"{realmEvent.Title}\n{realmEvent.Description}");
                        if (realmEvent.RequiresPause) result.ShouldPauseTime = true;
                    }
                }
            }

            foreach (var governor in state.Governors)
            {
                governor.DaysUntilNextMove--;
                if (governor.DaysUntilNextMove <= 0)
                {
                    var realmEvent = CreateGovernorMove(state, governor, today);
                    governor.DaysUntilNextMove = Rand.Next(24, 50);
                    if (realmEvent != null)
                    {
                        AddRealmEvent(state, realmEvent);
                        reports.Add($"{realmEvent.Title}\n{realmEvent.Description}");
                        if (realmEvent.RequiresPause) result.ShouldPauseTime = true;
                    }
                }
            }

            foreach (var wife in state.Wives.Where(w => !w.IsDead))
            {
                wife.DaysUntilNextCourtMove--;
                if (wife.DaysUntilNextCourtMove <= 0)
                {
                    var realmEvent = CreateCourtMove(state, wife, today);
                    wife.DaysUntilNextCourtMove = Rand.Next(25, 55);
                    if (realmEvent != null)
                    {
                        AddRealmEvent(state, realmEvent);
                        reports.Add($"{realmEvent.Title}\n{realmEvent.Description}");
                        if (realmEvent.RequiresPause) result.ShouldPauseTime = true;
                    }
                }
            }

            UpdateDerivedReputation(state);

            if (reports.Count > 0)
            {
                result.ShouldNarrate = true;
                result.MainMessage = string.Join("\n\n", reports);
            }

            return result;
        }

        public static PoliticalMemory AddMemory(
            GameState state,
            string actorType,
            string actorId,
            string actorName,
            string category,
            string summary,
            int opinionEffect,
            int trustEffect,
            int fearEffect,
            int severity,
            int durationDays,
            bool isPositive)
        {
            state.ReconcileOldSaves();
            int today = DiplomacySystem.GetCurrentDayNumber(state);
            var memory = new PoliticalMemory
            {
                ActorType = actorType,
                ActorId = actorId,
                ActorName = actorName,
                Category = category,
                Summary = summary,
                CreatedDay = today,
                ExpiresDay = durationDays <= 0 ? 0 : today + durationDays,
                OpinionEffect = opinionEffect,
                TrustEffect = trustEffect,
                FearEffect = fearEffect,
                Severity = severity,
                IsPositive = isPositive
            };

            state.PoliticalMemories.Add(memory);
            ApplyMemoryEffects(state, memory);

            if (state.PoliticalMemories.Count > 120)
                state.PoliticalMemories.RemoveRange(0, state.PoliticalMemories.Count - 120);

            return memory;
        }

        public static RoyalPromise AddPromise(
            GameState state,
            string promiseType,
            string targetType,
            string targetId,
            string targetName,
            string description,
            int dueInDays,
            string fulfillmentHint,
            int trustReward = 10,
            int breachPenalty = 15)
        {
            state.ReconcileOldSaves();
            int today = DiplomacySystem.GetCurrentDayNumber(state);
            var promise = new RoyalPromise
            {
                PromiseType = promiseType,
                TargetType = targetType,
                TargetId = targetId,
                TargetName = targetName,
                Description = description,
                FulfillmentHint = fulfillmentHint,
                CreatedDay = today,
                DueDay = today + Math.Max(1, dueInDays),
                TrustReward = trustReward,
                BreachPenalty = breachPenalty
            };

            state.RoyalPromises.Add(promise);
            AddMemory(state, targetType, targetId, targetName, "Promise", "قطع الملك وعداً: " + description, 3, 5, 0, 1, dueInDays + 90, true);
            return promise;
        }

        public static GameActionResult ResolveLivingEvent(GameState state, string eventId, string choice)
        {
            var result = new GameActionResult { Title = "قرار العالم الحي" };
            state.ReconcileOldSaves();
            var realmEvent = state.LivingRealmLog.FirstOrDefault(e => e.Id == eventId);
            if (realmEvent == null)
            {
                result.Success = false;
                result.MainMessage = "لم يتم العثور على الحدث السياسي.";
                return result;
            }

            if (realmEvent.IsResolved)
            {
                result.Success = false;
                result.MainMessage = "تم التعامل مع هذا الحدث مسبقاً.";
                return result;
            }

            switch (realmEvent.EventType)
            {
                case "EconomicAidRequest":
                    ResolveEconomicAidRequest(state, realmEvent, choice, result);
                    break;
                case "ForeignMarriageProposal":
                    ResolveMarriageProposal(state, realmEvent, choice, result);
                    break;
                case "SecretFundingFaction":
                    ResolveSecretFunding(state, realmEvent, choice, result);
                    break;
                case "GovernorDemand":
                    ResolveGovernorDemand(state, realmEvent, choice, result);
                    break;
                case "BorderWarPreparation":
                    ResolveBorderPreparation(state, realmEvent, choice, result);
                    break;
                case "WifeInfluenceRequest":
                    ResolveWifeInfluence(state, realmEvent, choice, result);
                    break;
                case "TradeRouteCrisis":
                    ResolveTradeRouteCrisis(state, realmEvent, choice, result);
                    break;
                case "DirectorAmbitiousPlot":
                case "DirectorPostWarDemands":
                case "DirectorTradeOpportunity":
                case "DirectorSuccessionPressure":
                case "DirectorSpymasterShadowWar":
                case "DirectorBorderEnvy":
                    var directorResult = RoyalDirectorSystem.ResolveDirectorEvent(state, realmEvent, choice);
                    result.Success = directorResult.Success;
                    result.Title = directorResult.Title;
                    result.MainMessage = directorResult.MainMessage;
                    result.SoundEffectKey = directorResult.SoundEffectKey;
                    foreach (var change in directorResult.ResourceChanges)
                        result.ResourceChanges[change.Key] = change.Value;
                    foreach (var warning in directorResult.Warnings)
                        result.Warnings.Add(warning);
                    break;
                case "AiMinisterCouncilProposal":
                case "AiSpouseCourtProposal":
                case "AiNeighborAudienceInvitation":
                    var aiCharacterResult = AiAutonomousCharacterSystem.ResolveAiCharacterEvent(state, realmEvent, choice);
                    result.Success = aiCharacterResult.Success;
                    result.Title = aiCharacterResult.Title;
                    result.MainMessage = aiCharacterResult.MainMessage;
                    result.SoundEffectKey = aiCharacterResult.SoundEffectKey;
                    foreach (var change in aiCharacterResult.ResourceChanges)
                        result.ResourceChanges[change.Key] = change.Value;
                    foreach (var warning in aiCharacterResult.Warnings)
                        result.Warnings.Add(warning);
                    break;
                default:
                    realmEvent.IsResolved = true;
                    result.Success = true;
                    result.MainMessage = "تم إغلاق الحدث دون أثر إضافي.";
                    break;
            }

            if (result.Success)
                realmEvent.IsResolved = true;

            return result;
        }

        public static string GetLivingRealmReport(GameState state)
        {
            state.ReconcileOldSaves();
            var sb = new StringBuilder();
            sb.AppendLine("تقرير العالم السياسي الحي");
            sb.AppendLine();
            sb.AppendLine($"السمعة الغالبة: {GetDominantReputation(state)}");

            var pendingEvents = state.LivingRealmLog.Where(e => !e.IsResolved && e.RequiresDecision).OrderByDescending(e => e.CreatedDay).Take(5).ToList();
            sb.AppendLine();
            sb.AppendLine("الأحداث التي تنتظر قراراً:");
            sb.AppendLine(pendingEvents.Count == 0 ? "لا توجد أحداث معلقة." : string.Join("\n", pendingEvents.Select(e => $"- {e.Title}: {e.ActorName}")));

            var promises = state.RoyalPromises.Where(p => !p.IsFulfilled && !p.IsBroken).OrderBy(p => p.DueDay).Take(5).ToList();
            sb.AppendLine();
            sb.AppendLine("الوعود النشطة:");
            sb.AppendLine(promises.Count == 0 ? "لا توجد وعود نشطة." : string.Join("\n", promises.Select(p => $"- {p.Description} ({p.TargetName})")));

            sb.AppendLine();
            sb.AppendLine("أهداف الممالك المجاورة:");
            foreach (var neighbor in state.Neighbors)
                sb.AppendLine($"- {neighbor.Name}: {neighbor.PoliticalGoal}. الخطة السرية: {neighbor.SecretPlan}.");

            var recentMemories = state.PoliticalMemories.Where(m => !m.IsArchived).OrderByDescending(m => m.CreatedDay).Take(6).ToList();
            sb.AppendLine();
            sb.AppendLine("أحدث الذكريات السياسية:");
            sb.AppendLine(recentMemories.Count == 0 ? "لا توجد ذاكرة سياسية بعد." : string.Join("\n", recentMemories.Select(m => $"- {m.ActorName}: {m.Summary}")));

            return sb.ToString().Trim();
        }

        public static string GetDominantReputation(GameState state)
        {
            if (state.RoyalReputationScores == null || state.RoyalReputationScores.Count == 0)
                return "الملك غير معروف السمعة بعد";

            var best = state.RoyalReputationScores.OrderByDescending(kv => kv.Value).First();
            if (best.Value <= 0)
                return "الملك غير معروف السمعة بعد";

            return best.Key switch
            {
                "Just" => "الملك العادل",
                "Deceptive" => "الملك المخادع",
                "Warrior" => "الملك المحارب",
                "Cruel" => "الملك القاسي",
                "Generous" => "الملك الكريم",
                "Pious" => "الملك المتدين",
                "PromiseKeeper" => "الملك الذي يفي بوعوده",
                "OathBreaker" => "الملك الذي لا يؤتمن على وعد",
                "TradeProtector" => "حامي التجارة",
                _ => best.Key
            };
        }

        public static void AdjustRoyalReputation(GameState state, string key, int amount)
        {
            state.ReconcileOldSaves();
            AdjustReputation(state, key, amount);
        }

        private static LivingRealmEvent CreateNeighborMove(GameState state, Neighbor neighbor, int today)
        {
            if (neighbor.EconomicTrouble >= 65 && !HasOpenEvent(state, "EconomicAidRequest", neighbor.Id))
            {
                return CreateEvent(state, "EconomicAidRequest", "Neighbor", neighbor.Id, neighbor.Name,
                    $"أزمة اقتصادية في {neighbor.Name}",
                    $"{neighbor.Name} تعاني من ضائقة في الأسواق والمخازن. وصل رسول يطلب ذهباً وقمحاً مقابل تعهد بتحسين العلاقات وفتح القوافل.",
                    "الوزير الأول يرى أن المساعدة ستشتري نفوذاً. قائد الجند يحذر من إضعاف مخزون الحرب. مسؤول الجواسيس يقترح التأكد من حقيقة الأزمة.",
                    2, true);
            }

            if (neighbor.AllianceDesire >= 70 && !neighbor.IsAlly && neighbor.Opinion >= 20 && !HasOpenEvent(state, "ForeignMarriageProposal", neighbor.Id))
            {
                return CreateEvent(state, "ForeignMarriageProposal", "Neighbor", neighbor.Id, neighbor.Name,
                    $"عرض زواج من {neighbor.Name}",
                    $"{neighbor.RulerName ?? neighbor.Ruler} يلمح إلى زواج دبلوماسي يربط البيتين الحاكمين ويغلق باب الحرب مؤقتاً.",
                    "الملكة ترى أن الزواج سيزيد نفوذ امرأة أجنبية في القصر. الوزير الأول يراه ضماناً دبلوماسياً مفيداً.",
                    2, true);
            }

            if (neighbor.SecretPlan.Contains("تمويل") && neighbor.Opinion < -20 && !HasOpenEvent(state, "SecretFundingFaction", neighbor.Id))
            {
                neighbor.IsSuspectedOfEspionage = true;
                return CreateEvent(state, "SecretFundingFaction", "Neighbor", neighbor.Id, neighbor.Name,
                    $"أموال غامضة من {neighbor.Name}",
                    $"وصلت إشارات إلى أن {neighbor.Name} تحاول تمويل ساخطين داخل المملكة. الدليل غير كامل، لكن أثر الفضة ظهر في مجالس بعض الولاة.",
                    "مسؤول الجواسيس يطلب تمويلاً للتحقق. رجل الدين يحذر من اتهام بلا بينة. القائد يفضل ردعاً علنياً.",
                    3, true);
            }

            if (neighbor.MilitaryAmbition >= 70 && state.ActiveWar == null && !neighbor.IsAlly && !neighbor.HasNonAggressionPact && !HasOpenEvent(state, "BorderWarPreparation", neighbor.Id))
            {
                if (string.IsNullOrWhiteSpace(neighbor.ClaimedProvince) && neighbor.ClaimableProvinces.Count > 0)
                    neighbor.ClaimedProvince = neighbor.ClaimableProvinces[0].Name;

                neighbor.HasClaim = !string.IsNullOrWhiteSpace(neighbor.ClaimedProvince);
                neighbor.Opinion = Math.Max(-100, neighbor.Opinion - 5);
                AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "BorderAmbition", $"بدأت {neighbor.Name} تتطلع إلى {neighbor.ClaimedProvince}.", -5, -3, 0, 2, 360, false);

                return CreateEvent(state, "BorderWarPreparation", "Neighbor", neighbor.Id, neighbor.Name,
                    $"تحركات حدودية من {neighbor.Name}",
                    $"{neighbor.Name} تحشد حول {neighbor.BorderTarget}. يبدو أن بلاطهم يختبر ضعف المملكة وربما يجهز مطالبة على {neighbor.ClaimedProvince}.",
                    "القائد يطلب تعبئة احتياطية. الوزير الأول يفضل سفارة عاجلة. مسؤول الجواسيس يقترح مراقبة معسكراتهم.",
                    3, true);
            }

            neighbor.Opinion = Math.Clamp(neighbor.Opinion + Rand.Next(-2, 3), -100, 100);
            return null;
        }

        private static LivingRealmEvent CreateGovernorMove(GameState state, Governor governor, int today)
        {
            if ((governor.OpinionOfKing < -25 || governor.CurrentMood == "Opportunist") && !HasOpenEvent(state, "GovernorDemand", governor.Id))
            {
                governor.SecretPlan = "جمع أنصار من الولاة";
                return CreateEvent(state, "GovernorDemand", "Governor", governor.Id, governor.Name,
                    $"طلب سياسي من {governor.Name}",
                    $"{governor.Name} والي {governor.ProvinceName} يلمح إلى أن الضرائب والقرارات المركزية تضغط على مقاطعته. يريد وعداً واضحاً أو تنازلاً الآن.",
                    "الوزير الأول يرى أن الوعد قد يكسب الوقت. القائد يحذر من الظهور بمظهر الضعف. رجل الدين يفضل العدل وخفض التوتر.",
                    2, true);
            }

            if (governor.Loyalty > 80 && governor.OpinionOfKing > 35)
            {
                governor.CurrentGoal = "دعم العرش";
                governor.Wealth = Math.Max(0, governor.Wealth - 5);
                state.Gold += 20;
                AddMemory(state, "Governor", governor.Id, governor.Name, "Support", $"أرسل دعماً مالياً صغيراً من {governor.ProvinceName}.", 3, 4, 0, 1, 180, true);
                return CreateEvent(state, "GovernorSupport", "Governor", governor.Id, governor.Name,
                    $"دعم من {governor.ProvinceName}",
                    $"{governor.Name} أرسل 20 ذهباً للخزينة ليؤكد ولاءه للعرش.",
                    "الوزير الأول يقترح شكره علناً حتى يقتدي به الآخرون.",
                    1, false, false);
            }

            return null;
        }

        private static LivingRealmEvent CreateCourtMove(GameState state, Spouse wife, int today)
        {
            if (wife.Influence < 25 || wife.Trust > 80)
                return null;

            if (string.IsNullOrWhiteSpace(wife.PreferredChildId))
            {
                var child = state.Children.FirstOrDefault(c => c.MotherSpouseId == wife.Id) ?? state.Children.FirstOrDefault();
                if (child != null)
                    wife.PreferredChildId = child.Id;
            }

            if (!HasOpenEvent(state, "WifeInfluenceRequest", wife.Id))
            {
                return CreateEvent(state, "WifeInfluenceRequest", "Spouse", wife.Id, wife.Name,
                    $"طلب من {wife.Name}",
                    $"{wife.Name} تريد نفوذاً أكبر داخل القصر وتلمح إلى دعم مرشحها داخل السلالة. تجاهلها قد يزيد الغيرة والتنافس بين الزوجات.",
                    "الوزير الأول يحذر من تضخم نفوذ الجناح الداخلي. الملكة ترى أن الاعتراف بمكانتها سيحفظ هدوء القصر.",
                    2, true);
            }

            return null;
        }

        private static string EvaluatePromises(GameState state, int today)
        {
            var reports = new List<string>();
            foreach (var promise in state.RoyalPromises.Where(p => !p.IsFulfilled && !p.IsBroken).ToList())
            {
                if (IsPromiseFulfilledNow(state, promise))
                {
                    promise.IsFulfilled = true;
                    AdjustReputation(state, "PromiseKeeper", 10);
                    state.Prestige += Math.Max(5, promise.TrustReward / 2);
                    AddMemory(state, promise.TargetType, promise.TargetId, promise.TargetName, "PromiseFulfilled", "وفى الملك بوعده: " + promise.Description, 8, promise.TrustReward, 0, 2, 540, true);
                    reports.Add($"تم الوفاء بوعد: {promise.Description}");
                }
                else if (today > promise.DueDay)
                {
                    promise.IsBroken = true;
                    state.Prestige = Math.Max(0, state.Prestige - promise.BreachPenalty);
                    state.ReligiousLegitimacy = Math.Max(0, state.ReligiousLegitimacy - Math.Max(1, promise.BreachPenalty / 2));
                    AdjustReputation(state, "OathBreaker", promise.BreachPenalty);
                    AddMemory(state, promise.TargetType, promise.TargetId, promise.TargetName, "PromiseBroken", "كسر الملك وعده: " + promise.Description, -promise.BreachPenalty, -promise.BreachPenalty, 0, 3, 720, false);
                    reports.Add($"تم كسر وعد: {promise.Description}");
                }
            }

            return string.Join("\n", reports);
        }

        private static bool IsPromiseFulfilledNow(GameState state, RoyalPromise promise)
        {
            return promise.PromiseType switch
            {
                "LowerTaxes" => state.TaxLevel == "منخفض",
                "ProtectTradeRoute" => state.ProtectedTradeRoutes.Contains(promise.TargetId) || state.ProtectedTradeRoutes.Contains(promise.TargetName),
                "NonAggression" => !state.Neighbors.Any(n => n.Id == promise.TargetId && (n.IsAtWarWithPlayer || n.Relation == "حرب")),
                _ => false
            };
        }

        private static void ResolveEconomicAidRequest(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == realmEvent.ActorId);
            if (neighbor == null) { result.Success = false; result.MainMessage = "الدولة لم تعد موجودة."; return; }

            if (choice == "Aid")
            {
                if (state.Gold < 100 || state.Food < 150)
                {
                    result.Success = false;
                    result.MainMessage = "تحتاج إلى 100 ذهب و150 مؤونة لتلبية طلب الإغاثة.";
                    return;
                }

                state.Gold -= 100;
                state.Food -= 150;
                neighbor.Opinion = Math.Min(100, neighbor.Opinion + 20);
                neighbor.Trust = Math.Min(100, neighbor.Trust + 15);
                neighbor.EconomicTrouble = Math.Max(0, neighbor.EconomicTrouble - 30);
                AdjustReputation(state, "Generous", 8);
                AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "Aid", "ساعدته في أزمة اقتصادية.", 20, 15, 0, 2, 540, true);
                result.Success = true;
                result.ResourceChanges.Add("الذهب", -100);
                result.ResourceChanges.Add("المؤونة", -150);
                result.MainMessage = $"أرسلت قوافل إغاثة إلى {neighbor.Name}. تحسنت الثقة والعلاقة، وانخفضت أزمتهم الاقتصادية.";
            }
            else if (choice == "PromiseAid")
            {
                AddPromise(state, "SendAid", "Neighbor", neighbor.Id, neighbor.Name, $"إرسال إغاثة إلى {neighbor.Name}", 45, "أرسل الإغاثة من شاشة العالم الحي قبل نهاية المهلة.", 12, 18);
                result.Success = true;
                result.MainMessage = $"وعدت {neighbor.Name} بإرسال الإغاثة خلال 45 يوماً. الوعد الآن مسجل في ذاكرة البلاط.";
            }
            else
            {
                neighbor.Opinion = Math.Max(-100, neighbor.Opinion - 10);
                neighbor.Trust = Math.Max(0, neighbor.Trust - 10);
                AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "RefusedAid", "رفض الملك مساعدته وقت الأزمة.", -10, -10, 0, 2, 540, false);
                result.Success = true;
                result.MainMessage = $"رفضت طلب {neighbor.Name}. سيتذكر بلاطهم أنك تركتهم في الضائقة.";
            }
        }

        private static void ResolveMarriageProposal(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            int idx = state.Neighbors.FindIndex(n => n.Id == realmEvent.ActorId);
            if (idx < 0) { result.Success = false; result.MainMessage = "الدولة لم تعد موجودة."; return; }
            var neighbor = state.Neighbors[idx];

            if (choice == "Accept")
            {
                var marriage = DynastySystem.ArrangeMarriage(state, idx);
                result.Success = marriage.Success;
                result.MainMessage = marriage.MainMessage;
                foreach (var change in marriage.ResourceChanges)
                    result.ResourceChanges[change.Key] = change.Value;
                if (marriage.Success)
                {
                    AdjustReputation(state, "Just", 3);
                    AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "MarriageAccepted", "قبل الملك عرض الزواج السياسي.", 12, 10, 0, 2, 720, true);
                }
            }
            else
            {
                neighbor.Opinion = Math.Max(-100, neighbor.Opinion - 8);
                neighbor.Trust = Math.Max(0, neighbor.Trust - 8);
                AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "MarriageRefused", "رفض الملك عرض الزواج السياسي.", -8, -8, 0, 1, 360, false);
                result.Success = true;
                result.MainMessage = $"رفضت عرض الزواج من {neighbor.Name}. العلاقة لم تنكسر، لكنهم شعروا بالإهانة.";
            }
        }

        private static void ResolveSecretFunding(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == realmEvent.ActorId);
            if (neighbor == null) { result.Success = false; result.MainMessage = "الدولة لم تعد موجودة."; return; }

            if (choice == "Investigate")
            {
                if (state.Gold < 80)
                {
                    result.Success = false;
                    result.MainMessage = "تحتاج إلى 80 ذهب لتمويل التحقيق السري.";
                    return;
                }

                state.Gold -= 80;
                neighbor.IsSuspectedOfEspionage = true;
                state.SecretReports.Add($"[{state.Time.GetDateString()}] دلائل على تمويل {neighbor.Name} لفصائل داخلية. الثقة: متوسطة.");
                AdjustReputation(state, "Deceptive", 4);
                result.Success = true;
                result.ResourceChanges.Add("الذهب", -80);
                result.MainMessage = $"مولت تحقيقاً سرياً ضد {neighbor.Name}. حصلت على تقرير استخباراتي، ويمكنك الآن اتهامهم من الدبلوماسية المتقدمة.";
            }
            else if (choice == "Accuse")
            {
                var accusation = DiplomacySystem.AccuseOfEspionage(state, neighbor.Id);
                result.Success = accusation.Success;
                result.MainMessage = accusation.MainMessage;
            }
            else
            {
                foreach (var faction in state.Factions.Where(f => f.IsActive))
                    faction.Discontent = Math.Min(100, faction.Discontent + 5);
                AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "IgnoredSubversion", "تجاهل الملك شبهات تمويل الفصائل.", -3, -5, 0, 1, 360, false);
                result.Success = true;
                result.MainMessage = "تجاهلت الشبهات. إن كانت صحيحة فقد تزداد جرأة الفصائل الداخلية.";
            }
        }

        private static void ResolveGovernorDemand(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var governor = state.Governors.FirstOrDefault(g => g.Id == realmEvent.ActorId);
            if (governor == null) { result.Success = false; result.MainMessage = "الوالي لم يعد موجوداً."; return; }

            if (choice == "GrantNow")
            {
                state.TaxLevel = "منخفض";
                governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 18);
                governor.Loyalty = Math.Min(100, governor.Loyalty + 10);
                governor.UpdateMood();
                AdjustReputation(state, "Just", 6);
                AddMemory(state, "Governor", governor.Id, governor.Name, "DemandGranted", "استجاب الملك لطلبه وخفض الضرائب.", 18, 10, 0, 2, 540, true);
                result.Success = true;
                result.MainMessage = $"خفضت الضرائب فوراً استجابة لطلب {governor.Name}. هدأ التوتر، لكن دخل الشهر القادم سيتأثر.";
            }
            else if (choice == "PromiseLowerTaxes")
            {
                AddPromise(state, "LowerTaxes", "Governor", governor.Id, governor.Name, $"خفض الضرائب استجابة لطلب {governor.Name}", 60, "غيّر مستوى الضرائب إلى منخفض قبل نهاية المهلة.", 12, 20);
                result.Success = true;
                result.MainMessage = $"وعدت {governor.Name} بخفض الضرائب خلال 60 يوماً. إن لم تفعل سيتحول الوعد إلى سبب كراهية.";
            }
            else
            {
                governor.OpinionOfKing = Math.Max(-100, governor.OpinionOfKing - 15);
                governor.Loyalty = Math.Max(0, governor.Loyalty - 8);
                governor.UpdateMood();
                AdjustReputation(state, "Cruel", 4);
                AddMemory(state, "Governor", governor.Id, governor.Name, "DemandRefused", "رفض الملك طلبه السياسي.", -15, -8, 5, 2, 540, false);
                result.Success = true;
                result.MainMessage = $"رفضت طلب {governor.Name}. زاد خوفه قليلاً، لكن رأيه وولاءه تراجعا.";
            }
        }

        private static void ResolveBorderPreparation(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == realmEvent.ActorId);
            if (neighbor == null) { result.Success = false; result.MainMessage = "الدولة لم تعد موجودة."; return; }

            if (choice == "SendEnvoy")
            {
                if (state.Gold < 50)
                {
                    result.Success = false;
                    result.MainMessage = "تحتاج إلى 50 ذهب لإرسال السفارة العاجلة.";
                    return;
                }

                state.Gold -= 50;
                neighbor.Opinion = Math.Min(100, neighbor.Opinion + 10);
                neighbor.Trust = Math.Min(100, neighbor.Trust + 5);
                neighbor.MilitaryAmbition = Math.Max(0, neighbor.MilitaryAmbition - 15);
                result.Success = true;
                result.ResourceChanges.Add("الذهب", -50);
                result.MainMessage = $"أرسلت سفارة عاجلة إلى {neighbor.Name}. تراجعت حدة التحركات مؤقتاً.";
            }
            else if (choice == "PrepareArmy")
            {
                state.Prestige += 5;
                neighbor.FearOfPlayer = Math.Min(100, neighbor.FearOfPlayer + 15);
                AdjustReputation(state, "Warrior", 5);
                result.Success = true;
                result.ResourceChanges.Add("الهيبة", 5);
                result.MainMessage = $"أعلنت تعبئة دفاعية. زاد خوف {neighbor.Name} من قوتك، وارتفعت سمعة الملك المحارب.";
            }
            else
            {
                neighbor.MilitaryAmbition = Math.Min(100, neighbor.MilitaryAmbition + 10);
                result.Success = true;
                result.MainMessage = $"تركت تحركات {neighbor.Name} بلا رد. قد يقرأون الصمت كعلامة ضعف.";
            }
        }

        private static void ResolveWifeInfluence(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var wife = state.Wives.FirstOrDefault(w => w.Id == realmEvent.ActorId);
            if (wife == null) { result.Success = false; result.MainMessage = "الزوجة لم تعد في القصر."; return; }

            if (choice == "Support")
            {
                wife.Trust = Math.Min(100, wife.Trust + 12);
                wife.Influence = Math.Min(100, wife.Influence + 8);
                wife.Jealousy = Math.Max(0, wife.Jealousy - 5);
                AddMemory(state, "Spouse", wife.Id, wife.Name, "CourtSupport", "منحها الملك دعماً داخل القصر.", 10, 12, 0, 2, 540, true);
                result.Success = true;
                result.MainMessage = $"دعمت {wife.Name} داخل القصر. زادت ثقتها ونفوذها، وقد تغار زوجات أخريات لاحقاً.";
            }
            else if (choice == "Delay")
            {
                AddPromise(state, "CourtInfluence", "Spouse", wife.Id, wife.Name, $"منح {wife.Name} نفوذاً أكبر في القصر", 45, "ادعمها من جناح الملكات أو من حدث قادم.", 10, 15);
                result.Success = true;
                result.MainMessage = $"أجلت طلب {wife.Name} لكنك وعدتها بالنظر فيه. الوعد مسجل الآن.";
            }
            else
            {
                wife.Trust = Math.Max(0, wife.Trust - 10);
                wife.Jealousy = Math.Min(100, wife.Jealousy + 10);
                AddMemory(state, "Spouse", wife.Id, wife.Name, "CourtRefusal", "رفض الملك طلبها داخل القصر.", -8, -10, 0, 2, 540, false);
                result.Success = true;
                result.MainMessage = $"رفضت طلب {wife.Name}. تراجعت الثقة وزادت الغيرة داخل الجناح.";
            }
        }

        private static void ResolveTradeRouteCrisis(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            if (choice == "SendGuards")
            {
                if (state.Gold < 100)
                {
                    result.Success = false;
                    result.MainMessage = "تحتاج إلى 100 ذهب لتأمين الطريق التجاري.";
                    return;
                }

                state.Gold -= 100;
                if (!state.ProtectedTradeRoutes.Contains(realmEvent.ActorId))
                    state.ProtectedTradeRoutes.Add(realmEvent.ActorId);
                state.MerchantsTrust = Math.Min(100, state.MerchantsTrust + 15);
                AdjustReputation(state, "TradeProtector", 10);
                result.Success = true;
                result.ResourceChanges.Add("الذهب", -100);
                result.MainMessage = "أرسلت حراسة منظمة للطريق التجاري. زادت ثقة التجار وارتفعت سمعة حامي التجارة.";
            }
            else if (choice == "PromiseProtection")
            {
                AddPromise(state, "ProtectTradeRoute", "TradeRoute", realmEvent.ActorId, realmEvent.ActorName, $"حماية الطريق التجاري {realmEvent.ActorName}", 45, "احمِ الطريق من شاشة العالم الحي قبل نهاية المهلة.", 12, 18);
                result.Success = true;
                result.MainMessage = "وعدت التجار بحماية الطريق خلال 45 يوماً.";
            }
            else
            {
                state.MerchantsTrust = Math.Max(0, state.MerchantsTrust - 10);
                result.Success = true;
                result.MainMessage = "تجاهلت أزمة الطريق التجاري. ثقة التجار تراجعت.";
            }
        }

        private static LivingRealmEvent CreateEvent(GameState state, string eventType, string actorType, string actorId, string actorName, string title, string description, string advice, int severity, bool requiresPause, bool requiresDecision = true)
        {
            return new LivingRealmEvent
            {
                EventType = eventType,
                ActorType = actorType,
                ActorId = actorId,
                ActorName = actorName,
                Title = title,
                Description = description,
                CouncilAdvice = advice,
                Severity = severity,
                RequiresPause = requiresPause,
                RequiresDecision = requiresDecision,
                CreatedDay = DiplomacySystem.GetCurrentDayNumber(state),
                DateText = state.Time.GetDateString()
            };
        }

        private static void AddRealmEvent(GameState state, LivingRealmEvent realmEvent)
        {
            state.LivingRealmLog.Add(realmEvent);
            if (state.LivingRealmLog.Count > 100)
                state.LivingRealmLog.RemoveRange(0, state.LivingRealmLog.Count - 100);
        }

        private static bool HasOpenEvent(GameState state, string eventType, string actorId)
        {
            return state.LivingRealmLog.Any(e => !e.IsResolved && e.EventType == eventType && e.ActorId == actorId);
        }

        private static void ArchiveExpiredMemories(GameState state, int today)
        {
            foreach (var memory in state.PoliticalMemories.Where(m => !m.IsArchived && m.ExpiresDay > 0 && m.ExpiresDay < today))
                memory.IsArchived = true;
        }

        private static void ApplyMemoryEffects(GameState state, PoliticalMemory memory)
        {
            if (memory.ActorType == "Neighbor")
            {
                var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == memory.ActorId);
                if (neighbor == null) return;
                neighbor.Opinion = Math.Clamp(neighbor.Opinion + memory.OpinionEffect, -100, 100);
                neighbor.Trust = Math.Clamp(neighbor.Trust + memory.TrustEffect, 0, 100);
                neighbor.FearOfPlayer = Math.Clamp(neighbor.FearOfPlayer + memory.FearEffect, 0, 100);
            }
            else if (memory.ActorType == "Governor")
            {
                var governor = state.Governors.FirstOrDefault(g => g.Id == memory.ActorId);
                if (governor == null) return;
                governor.OpinionOfKing = Math.Clamp(governor.OpinionOfKing + memory.OpinionEffect, -100, 100);
                governor.Loyalty = Math.Clamp(governor.Loyalty + memory.TrustEffect, 0, 100);
                governor.Fear = Math.Clamp(governor.Fear + memory.FearEffect, 0, 100);
                governor.UpdateMood();
            }
            else if (memory.ActorType == "Spouse")
            {
                var wife = state.Wives.FirstOrDefault(w => w.Id == memory.ActorId);
                if (wife == null) return;
                wife.OpinionOfKing = Math.Clamp(wife.OpinionOfKing + memory.OpinionEffect, -100, 100);
                wife.Trust = Math.Clamp(wife.Trust + memory.TrustEffect, 0, 100);
            }
        }

        private static void AdjustReputation(GameState state, string key, int amount)
        {
            if (!state.RoyalReputationScores.ContainsKey(key))
                state.RoyalReputationScores[key] = 0;
            state.RoyalReputationScores[key] = Math.Clamp(state.RoyalReputationScores[key] + amount, 0, 100);
        }

        private static void UpdateDerivedReputation(GameState state)
        {
            if (state.Piety > 150) AdjustReputation(state, "Pious", 1);
            if (state.Prestige > 180 && state.ActiveWar != null) AdjustReputation(state, "Warrior", 1);
            if (state.MerchantsTrust > 75) AdjustReputation(state, "TradeProtector", 1);
            if (state.ReligiousLegitimacy < 25) AdjustReputation(state, "OathBreaker", 1);
        }
    }
}
