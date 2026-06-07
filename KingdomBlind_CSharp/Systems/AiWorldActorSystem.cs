using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiWorldActorSystem
    {
        public static GameActionResult ProcessMonthlyWorldActors(GameState state)
        {
            var config = AppConfig.Load();
            config.AiActors ??= new AiActorSettings();
            return ProcessMonthlyWorldActors(state, config.AiActors);
        }

        public static GameActionResult ProcessMonthlyWorldActors(GameState state, AiActorSettings settings)
        {
            var result = new GameActionResult
            {
                Title = "قرارات العالم بالذكاء الاصطناعي",
                Success = true,
                ShouldNarrate = false
            };

            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);
            settings ??= new AiActorSettings();

            if (state.SuppressRandomMajorEvents)
                return result;

            var reports = new List<string>();
            bool shouldPause = false;

            if (settings.AllowAiNeighborRealmManagement)
                reports.AddRange(ProcessNeighborRealmManagement(state));

            if (settings.AllowAiGovernorDecisions)
                reports.AddRange(ProcessGovernorInternalDecisions(state));

            if (settings.AllowAiFactionDecisions)
            {
                var factionReports = ProcessFactionDecisions(state, out shouldPause);
                reports.AddRange(factionReports);
            }

            if (reports.Count == 0)
                return result;

            result.ShouldNarrate = true;
            result.ShouldPauseTime = shouldPause;
            result.SoundEffectKey = shouldPause ? "beacon" : "paper";
            result.MainMessage =
                "حركة العالم السياسي هذا الشهر:\n" +
                string.Join("\n", reports.Take(10));

            return result;
        }

        private static IEnumerable<string> ProcessNeighborRealmManagement(GameState state)
        {
            var reports = new List<string>();

            foreach (var neighbor in state.Neighbors.Where(n => n.Relation != "تابع" && n.Relation != "مضمومة"))
            {
                string ruler = string.IsNullOrWhiteSpace(neighbor.RulerName) ? neighbor.Ruler : neighbor.RulerName;
                string action;
                string publicReport;
                int risk = 12;

                if (neighbor.CourtStability < 35)
                {
                    neighbor.CourtStability = Math.Clamp(neighbor.CourtStability + 12, 0, 100);
                    neighbor.CouncilCompetence = Math.Clamp(neighbor.CouncilCompetence + 5, 0, 100);
                    neighbor.EconomicTrouble = Math.Clamp(neighbor.EconomicTrouble + 2, 0, 100);
                    neighbor.DevelopmentFocus = "مصالحة الوزراء";
                    action = "مصالحة وزراء البلاط";
                    publicReport = $"وصلت إشارات من {neighbor.Name}: {ruler} جمع وزراءه لتهدئة صراع البلاط. استقر مجلسه قليلاً.";
                    risk = 18;
                }
                else if (neighbor.EconomicTrouble > 60)
                {
                    neighbor.EconomicTrouble = Math.Clamp(neighbor.EconomicTrouble - 12, 0, 100);
                    neighbor.EconomicStrength = Math.Clamp(neighbor.EconomicStrength + 3, 0, 100);
                    neighbor.CourtStability = Math.Clamp(neighbor.CourtStability + 3, 0, 100);
                    neighbor.DevelopmentFocus = "إصلاح الأسواق والجباية";
                    action = "إصلاح الأسواق والجباية";
                    publicReport = $"{neighbor.Name} عالجت اضطراب أسواقها عبر وزرائها. الأزمة الاقتصادية تراجعت قليلاً.";
                }
                else if (neighbor.MilitaryAmbition > 65 || neighbor.Army < neighbor.MilitaryStrength * 8)
                {
                    int soldiers = Math.Max(60, 70 + neighbor.MilitaryStrength);
                    neighbor.Army += soldiers;
                    neighbor.MilitaryStrength = Math.Clamp(neighbor.MilitaryStrength + 1, 0, 100);
                    neighbor.EconomicTrouble = Math.Clamp(neighbor.EconomicTrouble + 4, 0, 100);
                    neighbor.DevelopmentFocus = "تقوية الجيش";
                    if (neighbor.Opinion < -30 && !neighbor.HasNonAggressionPact && !neighbor.IsAlly)
                        neighbor.SecretPlan = "اختبار الحدود بعد تدريب الجيش";
                    action = "تدريب جيش الدولة";
                    publicReport = $"{neighbor.Name} دربت وحدات جديدة. التقدير العلني لقوتها ارتفع، لكن كلفة الجيش ضغطت اقتصادها.";
                    risk = 34;
                }
                else
                {
                    neighbor.EconomicStrength = Math.Clamp(neighbor.EconomicStrength + 4, 0, 100);
                    neighbor.EconomicTrouble = Math.Clamp(neighbor.EconomicTrouble - 4, 0, 100);
                    neighbor.CouncilCompetence = Math.Clamp(neighbor.CouncilCompetence + 2, 0, 100);
                    neighbor.AllianceDesire = Math.Clamp(neighbor.AllianceDesire + (neighbor.Opinion > 0 ? 2 : 0), 0, 100);
                    neighbor.DevelopmentFocus = "تنمية الأسواق";
                    action = "تنمية الأسواق الداخلية";
                    publicReport = $"{neighbor.Name} استثمرت في أسواقها الداخلية وقوافلها. اقتصادها صار أكثر تماسكاً.";
                }

                AddNeighborInternalNote(neighbor, $"{state.Time.GetDateString()}: {action}");
                AddLog(state, ruler, AiAgentRole.NeighborRuler, action, publicReport, 0, risk, true, risk >= 30);
                reports.Add(publicReport);
            }

            return reports;
        }

        private static IEnumerable<string> ProcessGovernorInternalDecisions(GameState state)
        {
            var reports = new List<string>();

            foreach (var governor in state.Governors.Where(g => !g.IsImprisoned))
            {
                var province = state.Provinces.FirstOrDefault(p => p.Id == governor.ProvinceId || p.Name == governor.ProvinceName);
                if (province == null)
                    continue;

                string action;
                string report;
                int risk = 10;

                if (governor.OpinionOfKing < -35 && governor.Ambition > 60)
                {
                    governor.Wealth += 20;
                    governor.Loyalty = Math.Clamp(governor.Loyalty - 4, 0, 100);
                    governor.SecretPlan = "إخفاء ثروة محلية وبناء أنصار";
                    governor.UpdateMood();
                    action = "إخفاء ثروة محلية";
                    report = $"همس من {governor.ProvinceName}: {governor.Name} زاد ثروته المحلية بعيداً عن الديوان. التقرير غير كامل لكنه مقلق.";
                    risk = 45;
                }
                else if (province.Satisfaction < 60 && governor.Wealth >= 20)
                {
                    governor.Wealth -= 20;
                    governor.Loyalty = Math.Clamp(governor.Loyalty + 2, 0, 100);
                    governor.OpinionOfKing = Math.Clamp(governor.OpinionOfKing + 2, -100, 100);
                    province.Satisfaction = Math.Clamp(province.Satisfaction + 6, 0, 100);
                    governor.CurrentGoal = "تهدئة الرعية";
                    governor.UpdateMood();
                    action = "تهدئة الرعية محلياً";
                    report = $"{governor.Name} أنفق من ماله في {province.Name} لتهدئة الرعية. الرضا المحلي تحسن.";
                }
                else if (province.LocalGarrison < 450 && governor.Wealth >= 25)
                {
                    governor.Wealth -= 25;
                    governor.MilitaryPower = Math.Clamp(governor.MilitaryPower + 2, 0, 100);
                    province.LocalGarrison += 80;
                    governor.CurrentGoal = "تقوية حامية المقاطعة";
                    action = "تقوية الحامية المحلية";
                    report = $"{governor.Name} قوّى حامية {province.Name} من موارده. هذا يحمي الطريق، لكنه يزيد وزن الولاية العسكري.";
                    risk = governor.OpinionOfKing < 0 ? 28 : 14;
                }
                else if (governor.Loyalty > 65 && governor.Wealth >= 30)
                {
                    governor.Wealth -= 30;
                    var building = province.Buildings.FirstOrDefault(b => b.BuildingType == "سوق") ??
                                   province.Buildings.FirstOrDefault(b => b.BuildingType == "مزرعة");
                    if (building == null)
                    {
                        province.Buildings.Add(new LocalBuilding { BuildingType = "سوق", Level = 1 });
                        province.Income += 5;
                    }
                    else
                    {
                        building.Level++;
                        if (building.BuildingType == "سوق") province.Income += 5;
                    }

                    governor.CurrentGoal = "تنمية المقاطعة تحت راية العرش";
                    action = "تنمية المقاطعة";
                    report = $"{governor.Name} طور منشآت {province.Name} من ثروة الولاية. دخل المقاطعة تحسن قليلاً.";
                }
                else
                {
                    governor.Wealth += 10;
                    governor.CurrentGoal = "تجميع موارد محلية";
                    action = "تجميع موارد محلية";
                    report = $"{governor.Name} ركز هذا الشهر على تجميع موارد {province.Name} وانتظار فرصة سياسية أو إدارية.";
                    risk = governor.Ambition > 70 ? 24 : 8;
                }

                AddLog(state, governor.Name, AiAgentRole.Governor, action, report, 0, risk, true, risk >= 40);
                reports.Add(report);
            }

            return reports;
        }

        private static IEnumerable<string> ProcessFactionDecisions(GameState state, out bool shouldPause)
        {
            shouldPause = false;
            var reports = new List<string>();

            foreach (var faction in state.Factions.Where(f => f.IsActive))
            {
                var leader = state.Governors.FirstOrDefault(g => g.Id == faction.LeaderGovernorId);
                string agentName = leader?.Name ?? faction.Name;
                string action;
                string report;
                int risk;

                var candidate = state.Governors
                    .Where(g => !faction.MemberGovernorIds.Contains(g.Id) &&
                                !g.IsImprisoned &&
                                (g.OpinionOfKing < -20 || g.CurrentMood == "Opportunist" || g.CurrentMood == "Angry"))
                    .OrderByDescending(g => g.Influence + g.MilitaryPower)
                    .FirstOrDefault();

                if (candidate != null && faction.PowerPercent < 90)
                {
                    faction.MemberGovernorIds.Add(candidate.Id);
                    faction.PowerPercent = Math.Clamp(faction.PowerPercent + Math.Max(5, candidate.MilitaryPower / 3), 0, 100);
                    faction.Discontent = Math.Clamp(faction.Discontent + 6, 0, 100);
                    candidate.SecretPlan = "التنسيق مع فصيل ساخط";
                    action = "ضم والٍ ساخط";
                    report = $"{faction.Name} ضم {candidate.Name} إلى صفه. قوة الفصيل أصبحت {faction.PowerPercent}%.";
                    risk = 45;
                }
                else if (faction.Discontent >= 75 && faction.PowerPercent >= 35 && faction.DaysUntilUltimatum < 0)
                {
                    faction.DaysUntilUltimatum = 14;
                    faction.IsPreparingRebellion = true;
                    action = "إرسال إنذار سياسي";
                    report = $"{faction.Name} صاغ إنذاراً سياسياً جديداً. المطلب: {faction.DemandText}. أمام العرش 14 يوماً قبل التصعيد.";
                    risk = 70;
                    shouldPause = true;
                }
                else if (state.TaxLevel == "منخفض" && faction.Type == "LowerTaxes")
                {
                    faction.Discontent = Math.Clamp(faction.Discontent - 10, 0, 100);
                    faction.PowerPercent = Math.Clamp(faction.PowerPercent - 4, 0, 100);
                    action = "تهدئة مؤقتة";
                    report = $"{faction.Name} هدأ قليلاً بسبب انخفاض الضرائب، لكنه لم يتفكك بالكامل.";
                    risk = 15;
                }
                else
                {
                    faction.Discontent = Math.Clamp(faction.Discontent + 5, 0, 100);
                    action = "حشد عرائض ونقاشات";
                    report = $"{faction.Name} واصل حشد العرائض في مجالس الولاة. السخط الآن {faction.Discontent}/100.";
                    risk = faction.Discontent > 65 ? 38 : 22;
                }

                AddLog(state, agentName, AiAgentRole.FactionLeader, action, report, 0, risk, true, risk >= 45);
                reports.Add(report);
            }

            return reports;
        }

        private static void AddNeighborInternalNote(Neighbor neighbor, string note)
        {
            neighbor.InternalDecisionLog ??= new List<string>();
            neighbor.InternalDecisionLog.Add(note);
            while (neighbor.InternalDecisionLog.Count > 12)
                neighbor.InternalDecisionLog.RemoveAt(0);
        }

        private static void AddLog(GameState state, string agentName, AiAgentRole role, string action, string result, int cost, int risk, bool successful, bool playerCanRespond)
        {
            state.AiActionLog ??= new List<AiActionLogEntry>();
            state.AiActionLog.Add(new AiActionLogEntry
            {
                Date = state.Time.GetDateString(),
                DayNumber = DiplomacySystem.GetCurrentDayNumber(state),
                AgentName = agentName,
                Role = role,
                ActionTaken = action,
                Cost = cost,
                Result = result,
                Risk = risk,
                WasSuccessful = successful,
                PlayerCanRespond = playerCanRespond
            });

            while (state.AiActionLog.Count > 100)
                state.AiActionLog.RemoveAt(0);
        }
    }
}
