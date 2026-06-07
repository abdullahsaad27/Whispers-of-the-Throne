using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class GrandStrategySystem
    {
        private static readonly Random Rand = new Random();

        public static string GetRoyalDashboard(GameState state)
        {
            state.ReconcileOldSaves();
            var items = new List<(int Priority, string Text)>();

            if (state.Gold < 250)
                items.Add((95, $"خطر مالي: الخزينة منخفضة جداً ({state.Gold} ذهب)."));
            if (state.Food < 250)
                items.Add((90, $"خطر مؤونة: المخازن لا تكفي لأزمة طويلة ({state.Food} مؤونة)."));
            if (state.ActiveWar != null)
                items.Add((85, $"حرب قائمة: {state.CurrentWarGoal?.TargetKingdomName ?? "عدو مجهول"}، الهدف {state.ActiveWar.TargetProvince}، نتيجة الحرب {state.ActiveWar.WarScore}."));
            if (state.Factions.Any(f => f.IsActive && f.Discontent >= 70))
                items.Add((80, "خطر داخلي: فصيل ساخط يقترب من إنذار نهائي."));
            if (state.Wives.Any(w => !w.IsDead && w.Ambition > 70 && w.OpinionOfKing < 35))
                items.Add((75, "خطر قصر: زوجة طموحة وغاضبة قد تحرك جناحاً سياسياً ضد وريثك."));
            if (state.Neighbors.Any(n => n.Opinion < -60 && n.MilitaryAmbition > 70))
                items.Add((72, "خطر حدودي: جار عدائي يملك طموحاً عسكرياً مرتفعاً."));
            if (state.CharacterSecrets.Any(s => s.IsKnownToPlayer && !s.IsExposed))
                items.Add((65, "فرصة ابتزاز: لديك أسرار معروفة يمكن تحويلها إلى خطافات سياسية."));
            if (state.ReignObjectives.Any(o => !o.IsCompleted))
                items.Add((55, "هدف عهد مفتوح: راجع أهداف الملك لتوجيه القرارات الطويلة."));
            if (state.Treaties.Any(t => t.IsActive && t.TreatyType == "TradeAgreement"))
                items.Add((45, "فرصة اقتصادية: طرق التجارة النشطة تمنح دخلاً شهرياً وتستحق الحماية."));

            var sb = new StringBuilder();
            sb.AppendLine("لوحة الملك السريعة");
            sb.AppendLine();
            foreach (var item in items.OrderByDescending(i => i.Priority).Take(5))
                sb.AppendLine("- " + item.Text);

            if (items.Count == 0)
                sb.AppendLine("- لا توجد مخاطر عاجلة. المملكة في حالة هدوء نسبي.");

            return sb.ToString().Trim();
        }

        public static string GetCurrentKingSummary(GameState state)
        {
            state.ReconcileOldSaves();
            var sb = new StringBuilder();
            sb.AppendLine("ملخص الملك الآن");
            sb.AppendLine($"الخليفة: {state.RulerName}، العمر {state.RulerAge}، التاريخ {state.Time.GetDateString()}.");
            sb.AppendLine($"الخزينة: {state.Gold} ذهب، المؤونة: {state.Food}، رضا الرعية: {state.Satisfaction}.");
            sb.AppendLine($"الحرب: {(state.ActiveWar == null ? "لا توجد حرب خارجية نشطة" : $"حرب على {state.ActiveWar.TargetProvince}، نتيجة الحرب {state.ActiveWar.WarScore}")}.");
            sb.AppendLine($"الخلافة: {(string.IsNullOrWhiteSpace(state.HeirName) ? "لا يوجد وريث معلن" : $"الوريث {state.HeirName}، عمره {state.HeirAge}")}.");
            sb.AppendLine($"مجد السلالة: {state.DynastyGlory} - {DynastyChronicleSystem.GetGloryRank(state.DynastyGlory)}.");

            var biggestRisk = GetRiskItems(state).FirstOrDefault();
            var bestOpportunity = GetOpportunityItems(state).FirstOrDefault();
            sb.AppendLine($"الخطر الأكبر: {(string.IsNullOrWhiteSpace(biggestRisk.Text) ? "لا خطر عاجل" : biggestRisk.Text)}");
            sb.AppendLine($"الفرصة الأكبر: {(string.IsNullOrWhiteSpace(bestOpportunity.Text) ? "لا فرصة بارزة" : bestOpportunity.Text)}");
            sb.AppendLine($"النصيحة: {GetSuggestedDecision(state)}");
            return sb.ToString().Trim();
        }

        public static string GetTopRisksReport(GameState state)
        {
            state.ReconcileOldSaves();
            var risks = GetRiskItems(state).Take(5).ToList();
            return risks.Count == 0
                ? "أهم 5 أخطار\nلا توجد أخطار عاجلة الآن."
                : "أهم 5 أخطار\n" + string.Join("\n", risks.Select((r, i) => $"{i + 1}. {r.Text}"));
        }

        public static string GetTopOpportunitiesReport(GameState state)
        {
            state.ReconcileOldSaves();
            var opportunities = GetOpportunityItems(state).Take(5).ToList();
            return opportunities.Count == 0
                ? "أفضل 5 فرص\nلا توجد فرص واضحة الآن."
                : "أفضل 5 فرص\n" + string.Join("\n", opportunities.Select((o, i) => $"{i + 1}. {o.Text}"));
        }

        public static string GetSuggestedDecision(GameState state)
        {
            state.ReconcileOldSaves();
            var topRisk = GetRiskItems(state).FirstOrDefault();
            if (topRisk.Priority >= 90)
                return topRisk.Advice;

            var topOpportunity = GetOpportunityItems(state).FirstOrDefault();
            if (topOpportunity.Priority >= 65)
                return topOpportunity.Advice;

            if (state.ActiveWar == null && state.Gold > 800)
                return "استثمر في الأسواق أو حماية طريق تجاري قبل بدء حرب جديدة.";

            return "حافظ على الاستقرار يوماً آخر وراجع أهداف الشخصيات قبل قرار كبير.";
        }

        public static string GetRecentEventsSummary(GameState state)
        {
            state.ReconcileOldSaves();
            var events = state.LivingRealmLog
                .OrderByDescending(e => e.CreatedDay)
                .Take(8)
                .Select(e => $"- {e.DateText}: {e.Title} ({(e.IsResolved ? "محسوم" : "ينتظر قراراً")})")
                .ToList();
            var chronicle = state.DynastyChronicle
                .OrderByDescending(e => e.DayNumber)
                .Take(5)
                .Select(e => $"- {e.DateText}: {e.Title}")
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("آخر الأحداث");
            sb.AppendLine(events.Count == 0 ? "لا توجد أحداث سياسية حديثة." : string.Join("\n", events));
            sb.AppendLine();
            sb.AppendLine("آخر ما دخل كتاب العرش:");
            sb.AppendLine(chronicle.Count == 0 ? "لا توجد سجلات في كتاب العرش بعد." : string.Join("\n", chronicle));
            return sb.ToString().Trim();
        }

        public static string CondenseForScreenReader(string title, string report, int maxLines = 8)
        {
            if (string.IsNullOrWhiteSpace(report))
                return title;

            var lines = report
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Take(Math.Max(1, maxLines))
                .ToList();

            return $"{title}\n" + string.Join("\n", lines);
        }

        private static List<(int Priority, string Text, string Advice)> GetRiskItems(GameState state)
        {
            var risks = new List<(int Priority, string Text, string Advice)>();
            if (state.Gold < 250)
                risks.Add((100, $"الخزينة منخفضة جداً: {state.Gold} ذهب.", "افتح الخزينة واطلب قرضاً أو ابدأ إجراء دخل سريع."));
            if (state.Food < 250)
                risks.Add((95, $"المؤونة منخفضة: {state.Food}.", "أوقف التوسع العسكري واستثمر في المزارع أو شراء الغذاء."));
            if (state.ActiveWar != null)
                risks.Add((90, $"حرب قائمة على {state.ActiveWar.TargetProvince}.", "لا تبدأ حرباً جديدة وركز على الحصار أو السلام."));
            if (string.IsNullOrWhiteSpace(state.HeirName))
                risks.Add((88, "لا يوجد وريث معلن.", "ادخل إلى الأسرة الحاكمة وثبت الخلافة قبل أن تنمو فصائل الورثة."));
            if (state.Factions.Any(f => f.IsActive && f.Discontent >= 70))
                risks.Add((84, "فصيل ساخط يقترب من التمرد.", "افتح الولاة والفصائل وخفف السخط أو استعد عسكرياً."));
            if (state.Council.Values.Any(c => c.Title.Contains("استخ") && c.Influence >= 80))
                risks.Add((80, "مدير الاستخبارات يملك نفوذاً عالياً جداً.", "راجع المجلس وحدد صلاحياته أو موّل رقابة مضادة."));
            if (state.Neighbors.Any(n => n.Opinion < -60 && n.MilitaryAmbition > 70))
                risks.Add((76, "جار عدائي يملك طموحاً عسكرياً مرتفعاً.", "افتح الدبلوماسية المتقدمة وأرسل هدية أو حصن الحدود."));

            return risks.OrderByDescending(r => r.Priority).ToList();
        }

        private static List<(int Priority, string Text, string Advice)> GetOpportunityItems(GameState state)
        {
            var opportunities = new List<(int Priority, string Text, string Advice)>();
            if (state.MerchantsTrust >= 55 && state.Gold >= 200)
                opportunities.Add((85, "ثقة التجار جيدة ويمكن تحويلها إلى دخل مستقر.", "افتح تنمية التجارة والأسواق وأقم سوقاً موسمياً أو عقداً تجارياً."));
            if (state.ProtectedTradeRoutes.Count == 0 && state.Army >= 300 && state.Gold >= 100)
                opportunities.Add((78, "يمكنك حماية أول طريق تجاري ورفع الدخل الشهري.", "أرسل قوات لحماية طريق بغداد-دمشق."));
            if (state.CharacterSecrets.Any(s => s.IsKnownToPlayer && !s.IsExposed))
                opportunities.Add((74, "لديك أسرار يمكن تحويلها إلى خطافات سياسية.", "افتح الأسرار والخطافات واستعملها على هدف مهم."));
            if (state.Gold >= 1000 && state.ActiveWar == null)
                opportunities.Add((70, "الخزينة تسمح بمشروع طويل الأمد.", "استثمر في سوق أو مزرعة بدلاً من ترك الذهب ساكناً."));
            if (state.ReignObjectives.Any(o => !o.IsCompleted))
                opportunities.Add((60, "هناك هدف عهد غير مكتمل يعطي اللعبة اتجاهاً.", "افتح أهداف عهد الملك واختر هدفاً واحداً لتركز عليه."));
            if (state.Neighbors.Any(n => n.Opinion >= 30 && !n.IsAlly))
                opportunities.Add((58, "جار ودود يمكن تحويله إلى تحالف أو زواج دبلوماسي.", "افتح الدبلوماسية المتقدمة وابحث عن زواج أو تحالف."));

            return opportunities.OrderByDescending(o => o.Priority).ToList();
        }

        public static string ExplainPoliticalDecision(GameState state, string actorType, string actorId)
        {
            state.ReconcileOldSaves();
            var actor = FindCharacter(state, actorType, actorId);
            if (actor == null)
                return "لا توجد شخصية كافية لتفسير هذا القرار.";

            var memories = state.PoliticalMemories
                .Where(m => m.ActorId == actor.SourceId || m.ActorId == actor.Id)
                .OrderByDescending(m => m.CreatedDay)
                .Take(2)
                .Select(m => m.Summary)
                .ToList();

            int fear = 0;
            int ambition = actor.Skills.Intrigue + actor.Skills.Martial;
            int playerWeakness = Math.Clamp(60 - state.Army / 100, 0, 60);

            if (actor.SourceType == "Governor")
            {
                var gov = state.Governors.FirstOrDefault(g => g.Id == actor.SourceId);
                if (gov != null)
                {
                    fear = gov.Fear;
                    ambition = gov.Ambition;
                }
            }
            else if (actor.SourceType == "Spouse")
            {
                var wife = state.Wives.FirstOrDefault(w => w.Id == actor.SourceId);
                if (wife != null)
                {
                    fear = wife.Trust;
                    ambition = wife.Ambition + wife.Jealousy;
                }
            }
            else if (actor.SourceType == "Neighbor")
            {
                var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == actor.SourceId);
                if (neighbor != null)
                {
                    fear = neighbor.FearOfPlayer;
                    ambition = neighbor.MilitaryAmbition + Math.Max(0, -neighbor.Opinion / 2);
                    playerWeakness = Math.Clamp(neighbor.Army - state.Army, 0, 100);
                }
            }

            var ai = AiProviderFactory.Create(AppConfig.Load().AiProvider);
            var response = ai.EvaluateDecision(state, new AiDecisionRequest
            {
                ActorName = actor.Name,
                ActorType = actor.SourceType,
                DecisionContext = "WhyDidThisHappen",
                Factors = new Dictionary<string, int>
                {
                    { "Fear", fear },
                    { "Ambition", ambition },
                    { "PlayerWeakness", playerWeakness }
                }
            });

            var sb = new StringBuilder();
            sb.AppendLine("لماذا حدث هذا؟");
            sb.AppendLine(response.Explanation);
            sb.AppendLine($"العوامل: الخوف {fear}، الطموح {ambition}، تقدير ضعفك {playerWeakness}.");
            if (memories.Count > 0)
                sb.AppendLine("ذكريات مؤثرة: " + string.Join("؛ ", memories));

            return sb.ToString().Trim();
        }

        public static string GetStrategicAtlas(GameState state, string provinceName = "")
        {
            state.ReconcileOldSaves();
            var provinces = string.IsNullOrWhiteSpace(provinceName)
                ? state.Provinces
                : state.Provinces.Where(p => p.Name == provinceName).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("الأطلس الاستراتيجي النصي");
            sb.AppendLine();

            foreach (var province in provinces)
            {
                var governor = state.Governors.FirstOrDefault(g => g.ProvinceId == province.Id || g.ProvinceName == province.Name);
                var contract = state.FeudalContracts.FirstOrDefault(c => c.ProvinceId == province.Id);
                var armies = state.Armies.Where(a => a.CurrentProvince == province.Name).Sum(a => a.TotalSoldiers);
                var enemies = state.EnemyArmies.Where(a => a.CurrentProvince == province.Name).Sum(a => a.TotalSoldiers);

                sb.AppendLine(province.Name);
                sb.AppendLine($"الحاكم: {governor?.Name ?? province.GovernorName ?? province.Vassal}");
                sb.AppendLine($"الدخل: {province.Income}، الرضا: {province.Satisfaction}، الدين: {province.Religion}");
                sb.AppendLine($"الجوار: {(province.ConnectedProvinces.Count == 0 ? "غير محدد" : string.Join("، ", province.ConnectedProvinces))}");
                sb.AppendLine($"الحامية: {province.LocalGarrison}، الجيش الملكي الحاضر: {armies}، العدو الحاضر: {enemies}");
                if (contract != null)
                    sb.AppendLine($"العقد: ضرائب {contract.TaxPercent}%، جند {contract.LevyPercent}%، استقلال {contract.Autonomy}%.");
                if (province.Occupied)
                    sb.AppendLine($"تحذير: المقاطعة محتلة من {province.OccupiedBy}.");
                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        public static GameActionResult DiscoverSecret(GameState state, string sourceType, string sourceId)
        {
            var result = new GameActionResult { Title = "اكتشاف سر" };
            state.ReconcileOldSaves();

            var character = FindCharacter(state, sourceType, sourceId);
            if (character == null)
            {
                result.Success = false;
                result.MainMessage = "لم يتم العثور على شخصية مناسبة للبحث عن أسرارها.";
                return result;
            }

            var existingSecret = state.CharacterSecrets.FirstOrDefault(s => s.OwnerCharacterId == character.Id && !s.IsExposed);
            if (existingSecret == null)
            {
                existingSecret = new CharacterSecret
                {
                    OwnerCharacterId = character.Id,
                    OwnerName = character.Name,
                    Type = character.Role == CharacterRoleType.Councilor ? SecretType.Corruption : SecretType.Treason,
                    Severity = Math.Clamp(character.Skills.Intrigue, 1, 10),
                    Summary = character.Role == CharacterRoleType.Councilor
                        ? $"{character.Name} يخفي اختلاساً صغيراً من الموارد الشهرية."
                        : $"{character.Name} يتواصل مع خصوم البلاط بحثاً عن ضمانات سياسية."
                };
                state.CharacterSecrets.Add(existingSecret);
                character.SecretIds.Add(existingSecret.Id);
            }

            existingSecret.IsKnownToPlayer = true;

            if (!state.PoliticalHooks.Any(h => h.SecretId == existingSecret.Id && !h.IsUsed))
            {
                state.PoliticalHooks.Add(new PoliticalHook
                {
                    TargetCharacterId = character.Id,
                    TargetName = character.Name,
                    SecretId = existingSecret.Id,
                    Strength = existingSecret.Severity >= 7 ? HookStrength.Strong : HookStrength.Weak,
                    ExpiresDay = DiplomacySystem.GetCurrentDayNumber(state) + 720
                });
            }

            result.Success = true;
            result.MainMessage = $"اكتشف الجواسيس سراً عن {character.Name}: {existingSecret.Summary} تم تحويله إلى خطاف سياسي يمكن استخدامه لاحقاً.";
            result.SoundEffectKey = "paper";
            return result;
        }

        public static GameActionResult StartScheme(GameState state, SchemeType type, string targetCharacterId)
        {
            var result = new GameActionResult { Title = "بدء مكيدة نشطة" };
            state.ReconcileOldSaves();
            var target = state.RealmCharacters.FirstOrDefault(c => c.Id == targetCharacterId || c.SourceId == targetCharacterId);
            if (target == null)
            {
                result.Success = false;
                result.MainMessage = "هدف المكيدة غير موجود.";
                return result;
            }

            if (state.ActiveSchemes.Any(s => !s.IsResolved && s.Type == type && s.TargetCharacterId == target.Id))
            {
                result.Success = false;
                result.MainMessage = "هناك مكيدة نشطة بالفعل ضد هذا الهدف.";
                return result;
            }

            int cost = type == SchemeType.Murder ? 300 : 120;
            if (state.Gold < cost)
            {
                result.Success = false;
                result.MainMessage = $"تحتاج إلى {cost} ذهب لبدء هذه المكيدة.";
                return result;
            }

            state.Gold -= cost;
            var scheme = new ActiveScheme
            {
                Type = type,
                TargetCharacterId = target.Id,
                TargetName = target.Name,
                OwnerName = state.RulerName,
                DaysRemaining = type == SchemeType.Murder ? 45 : 25,
                SuccessChance = Math.Clamp(25 + GetPlayerIntrigue(state) * 5 - target.Skills.Intrigue * 3, 5, 85),
                Secrecy = Math.Clamp(60 + GetPlayerIntrigue(state) * 3, 10, 95)
            };

            state.ActiveSchemes.Add(scheme);

            result.Success = true;
            result.ResourceChanges.Add("الذهب", -cost);
            result.MainMessage = $"بدأت مكيدة {GetSchemeName(type)} ضد {target.Name}. ستتقدم عبر مراحل بدلاً من الحسم الفوري.";
            return result;
        }

        public static GameActionResult ProcessDailySchemes(GameState state)
        {
            var result = new GameActionResult { Title = "المكائد النشطة", Success = true, ShouldNarrate = false };
            state.ReconcileOldSaves();
            var reports = new List<string>();

            foreach (var scheme in state.ActiveSchemes.Where(s => !s.IsResolved).ToList())
            {
                scheme.DaysRemaining--;
                scheme.Progress = Math.Min(100, scheme.Progress + Rand.Next(2, 6));

                if (scheme.Progress >= 35 && scheme.Stage == SchemeStage.Planning)
                    scheme.Stage = SchemeStage.RecruitingAgents;
                if (scheme.Progress >= 70 && scheme.Stage == SchemeStage.RecruitingAgents)
                    scheme.Stage = SchemeStage.Preparing;

                if (scheme.DaysRemaining <= 0 || scheme.Progress >= 100)
                {
                    ResolveScheme(state, scheme, reports);
                    result.ShouldPauseTime = true;
                }
            }

            if (reports.Count > 0)
            {
                result.ShouldNarrate = true;
                result.MainMessage = string.Join("\n", reports);
            }

            return result;
        }

        public static string GetReignObjectivesReport(GameState state)
        {
            state.ReconcileOldSaves();
            RefreshReignObjectives(state);
            var sb = new StringBuilder();
            sb.AppendLine("أهداف عهد الملك");
            foreach (var objective in state.ReignObjectives)
            {
                string done = objective.IsCompleted ? "مكتمل" : "جارٍ";
                sb.AppendLine($"- {objective.Title}: {done}. التقدم {objective.Progress}/{objective.Target}. {objective.Description}");
            }

            return sb.ToString().Trim();
        }

        public static void RefreshReignObjectives(GameState state)
        {
            foreach (var objective in state.ReignObjectives)
            {
                switch (objective.ObjectiveType)
                {
                    case "UnifySyria":
                        objective.Progress = state.Neighbors.Count(n => n.Relation == "تابع" || n.Relation == "مضمومة");
                        break;
                    case "SecureSuccession":
                        objective.Progress = string.IsNullOrWhiteSpace(state.HeirName) ? 0 : 1;
                        break;
                    case "RepairTreasury":
                        objective.Progress = Math.Min(state.Gold, objective.Target);
                        break;
                }

                objective.IsCompleted = objective.Progress >= objective.Target;
            }
        }

        public static string HandleRulerDeathAndSuccession(GameState state, string oldRulerName)
        {
            state.ReconcileOldSaves();
            var sb = new StringBuilder();
            sb.AppendLine("--- وفاة الملك ---");

            foreach (var child in state.Children.Where(c => !c.IsDead))
            {
                var character = state.RealmCharacters.FirstOrDefault(c => c.SourceType == "Child" && c.SourceId == child.Id);
                if (character == null)
                    continue;

                if (!state.CharacterClaims.Any(c => c.HolderCharacterId == character.Id && c.TargetType == "Throne"))
                {
                    var claim = new CharacterClaim
                    {
                        HolderCharacterId = character.Id,
                        HolderName = child.Name,
                        TargetType = "Throne",
                        TargetId = "PlayerThrone",
                        TargetName = "عرش المملكة",
                        IsStrong = child.IsHeir
                    };
                    state.CharacterClaims.Add(claim);
                    character.ClaimIds.Add(claim.Id);
                }
            }

            if (!string.IsNullOrEmpty(state.HeirName))
            {
                state.RulerName = state.HeirName;
                state.RulerAge = state.HeirAge;
                state.RulerIsDead = false;
                state.HeirName = null;
                state.HeirAge = 0;
                state.Satisfaction = Math.Max(0, state.Satisfaction - 5);
                sb.AppendLine($"مات الملك {oldRulerName}. انتقل الحكم إلى {state.RulerName}.");
                DynastyChronicleSystem.RecordEvent(state, "Succession", "انتقال الخلافة", $"توفي {oldRulerName} وانتقل العرش إلى {state.RulerName}.", 12, 4);

                var claimant = state.Children.FirstOrDefault(c => !c.IsDead && !c.IsHeir);
                if (claimant != null && state.Governors.Any(g => g.OpinionOfKing < -30 || g.Ambition > 75))
                {
                    var leader = state.Governors.OrderByDescending(g => g.Ambition - g.OpinionOfKing).First();
                    state.Factions.Add(new Faction
                    {
                        Name = $"فصيل دعم مطالبة {claimant.Name}",
                        Type = "SuccessionClaim",
                        LeaderGovernorId = leader.Id,
                        MemberGovernorIds = new List<string> { leader.Id },
                        DemandText = $"دعم مطالبة {claimant.Name} بالعرش",
                        MainReason = "أزمة خلافة بعد وفاة الملك",
                        PowerPercent = Math.Clamp(leader.MilitaryPower + leader.Influence / 2, 10, 100),
                        Discontent = 60,
                        DaysUntilUltimatum = 90
                    });
                    sb.AppendLine($"بدأت أزمة خلافة: بعض النبلاء يدرسون دعم {claimant.Name} بدلاً من الملك الجديد.");
                }
            }
            else
            {
                sb.AppendLine($"مات الملك {oldRulerName} ولم يترك وريثاً. سقطت المملكة في فوضى عارمة.");
                DynastyChronicleSystem.RecordEvent(state, "Succession", "فوضى بلا وريث", $"توفي {oldRulerName} دون وريث واضح، فاهتز كتاب العرش.", -40, 5);
            }

            return sb.ToString().Trim();
        }

        private static void ResolveScheme(GameState state, ActiveScheme scheme, List<string> reports)
        {
            scheme.Stage = SchemeStage.Execution;
            int roll = Rand.Next(1, 101);
            bool exposed = roll > scheme.Secrecy;
            bool success = roll <= scheme.SuccessChance;

            if (exposed)
            {
                scheme.Stage = SchemeStage.Exposed;
                state.Prestige = Math.Max(0, state.Prestige - 15);
                reports.Add($"انكشفت مكيدة {GetSchemeName(scheme.Type)} ضد {scheme.TargetName}. تضررت هيبتك في البلاط.");
            }
            else if (success)
            {
                ApplySchemeSuccess(state, scheme, reports);
            }
            else
            {
                reports.Add($"فشلت مكيدة {GetSchemeName(scheme.Type)} ضد {scheme.TargetName} دون أن تنكشف الأدلة الكاملة.");
            }

            scheme.IsResolved = true;
            scheme.Stage = success && !exposed ? SchemeStage.Resolved : scheme.Stage;
        }

        private static void ApplySchemeSuccess(GameState state, ActiveScheme scheme, List<string> reports)
        {
            switch (scheme.Type)
            {
                case SchemeType.Murder:
                    var target = state.RealmCharacters.FirstOrDefault(c => c.Id == scheme.TargetCharacterId);
                    if (target != null)
                    {
                        target.IsDead = true;
                        MarkSourceDead(state, target);
                    }
                    reports.Add($"نجحت مكيدة الاغتيال ضد {scheme.TargetName}. سيهتز البلاط عندما تنتشر الشائعات.");
                    LivingRealmSystem.AdjustRoyalReputation(state, "Cruel", 8);
                    break;
                case SchemeType.FabricateHook:
                    DiscoverSecret(state, "Character", scheme.TargetCharacterId);
                    reports.Add($"نجحت المكيدة في صناعة خطاف سياسي ضد {scheme.TargetName}.");
                    break;
                case SchemeType.SupportHeir:
                    state.Prestige += 5;
                    reports.Add($"نجحت مكيدة دعم الوريث داخل البلاط. زادت شرعية خيارك قليلاً.");
                    break;
                default:
                    reports.Add($"نجحت مكيدة {GetSchemeName(scheme.Type)} ضد {scheme.TargetName}.");
                    break;
            }
        }

        private static void MarkSourceDead(GameState state, RealmCharacter target)
        {
            if (target.SourceType == "Spouse")
            {
                var wife = state.Wives.FirstOrDefault(w => w.Id == target.SourceId);
                if (wife != null) wife.IsDead = true;
            }
            else if (target.SourceType == "Child")
            {
                var child = state.Children.FirstOrDefault(c => c.Id == target.SourceId);
                if (child != null) child.IsDead = true;
            }
            else if (target.SourceType == "Governor")
            {
                var governor = state.Governors.FirstOrDefault(g => g.Id == target.SourceId);
                if (governor != null) governor.IsImprisoned = true;
            }
        }

        private static RealmCharacter? FindCharacter(GameState state, string sourceType, string sourceId)
        {
            if (sourceType == "Character")
                return state.RealmCharacters.FirstOrDefault(c => c.Id == sourceId);

            return state.RealmCharacters.FirstOrDefault(c => c.SourceType == sourceType && c.SourceId == sourceId);
        }

        private static int GetPlayerIntrigue(GameState state)
        {
            var ruler = state.RealmCharacters.FirstOrDefault(c => c.SourceType == "Ruler" && c.SourceId == "player");
            return ruler?.Skills.Intrigue ?? 4;
        }

        private static string GetSchemeName(SchemeType type)
        {
            return type switch
            {
                SchemeType.Murder => "اغتيال",
                SchemeType.FabricateHook => "اختلاق خطاف",
                SchemeType.Discredit => "تشويه سمعة",
                SchemeType.SupportHeir => "دعم وريث",
                SchemeType.SabotageSupplies => "تخريب مؤونة",
                _ => "تقرب سياسي"
            };
        }
    }
}
