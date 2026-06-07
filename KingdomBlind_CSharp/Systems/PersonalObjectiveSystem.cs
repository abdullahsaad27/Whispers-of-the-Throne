using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class PersonalObjectiveSystem
    {
        public static void EnsurePersonalObjectives(GameState state)
        {
            state.ReconcileOldSaves();
            foreach (var character in state.RealmCharacters.Where(c => !c.IsDead))
            {
                if (state.CharacterObjectives.Any(o => o.IsActive && o.CharacterId == character.Id))
                    continue;

                state.CharacterObjectives.Add(CreateObjectiveForCharacter(state, character));
            }
        }

        public static GameActionResult ProcessMonthlyObjectives(GameState state)
        {
            var result = new GameActionResult { Title = "أهداف الشخصيات", Success = true, ShouldNarrate = false };
            state.ReconcileOldSaves();
            EnsurePersonalObjectives(state);

            int today = DiplomacySystem.GetCurrentDayNumber(state);
            var reports = new List<string>();

            foreach (var objective in state.CharacterObjectives.Where(o => o.IsActive).ToList())
            {
                objective.LastAdvancedDay = today;
                objective.Progress = Math.Clamp(objective.Progress + Math.Max(1, objective.Urgency / 20), 0, 100);

                if (objective.Progress < 100)
                    continue;

                ApplyObjectivePressure(state, objective, reports);
                objective.Progress = 35;
                objective.Urgency = Math.Clamp(objective.Urgency + 8, 0, 100);
            }

            if (reports.Count > 0)
            {
                result.ShouldNarrate = true;
                result.MainMessage = string.Join("\n", reports);
            }

            return result;
        }

        public static string GetObjectivesReport(GameState state)
        {
            state.ReconcileOldSaves();
            EnsurePersonalObjectives(state);
            var sb = new StringBuilder();
            sb.AppendLine("أهداف وطموحات الشخصيات");
            sb.AppendLine();

            foreach (var objective in state.CharacterObjectives
                .Where(o => o.IsActive && o.IsRevealedToPlayer)
                .OrderByDescending(o => o.Urgency)
                .ThenByDescending(o => o.Progress)
                .Take(20))
            {
                sb.AppendLine($"- {objective.CharacterName}: {objective.Title}");
                sb.AppendLine($"  {objective.Description} الإلحاح {objective.Urgency}/100، التقدم {objective.Progress}/100.");
            }

            return sb.ToString().Trim();
        }

        private static CharacterObjective CreateObjectiveForCharacter(GameState state, RealmCharacter character)
        {
            string type;
            string title;
            string description;
            int urgency;

            switch (character.Role)
            {
                case CharacterRoleType.Spouse:
                    var wife = state.Wives.FirstOrDefault(w => w.Id == character.SourceId);
                    bool hasChild = wife != null && state.Children.Any(c => c.MotherSpouseId == wife.Id);
                    type = "SpouseHeirInfluence";
                    title = hasChild ? "تثبيت ابنها في الخلافة" : "توسيع جناحها داخل القصر";
                    description = hasChild
                        ? "تعمل بهدوء ليصبح ابنها أو جناحها أقرب إلى ولاية العهد."
                        : "تبحث عن حلفاء وخدم ومكانة تجعل صوتها مسموعاً.";
                    urgency = Math.Clamp((wife?.Ambition ?? 45) + Math.Max(0, 50 - (wife?.OpinionOfKing ?? 50)) / 2, 20, 95);
                    break;
                case CharacterRoleType.Governor:
                    var governor = state.Governors.FirstOrDefault(g => g.Id == character.SourceId);
                    type = "GovernorAutonomy";
                    title = governor != null && governor.OpinionOfKing < 0 ? "تخفيف ضغط المركز" : "زيادة نفوذ المقاطعة";
                    description = governor != null && governor.OpinionOfKing < 0
                        ? "يريد ضرائب أخف واستقلالاً أوسع قبل أن ينضم إلى فصيل ساخط."
                        : "يريد مشاريع وامتيازات ترفع مكانة مقاطعته بين الولاة.";
                    urgency = Math.Clamp((governor?.Ambition ?? 45) + Math.Max(0, -(governor?.OpinionOfKing ?? 0)), 20, 100);
                    break;
                case CharacterRoleType.Councilor:
                    type = character.CurrentCouncilPosition.Contains("جند") ? "MinisterWarGlory" :
                           character.CurrentCouncilPosition.Contains("مال") ? "MinisterRevenue" :
                           character.CurrentCouncilPosition.Contains("استخ") ? "MinisterSpyInfluence" :
                           "MinisterStability";
                    title = type switch
                    {
                        "MinisterWarGlory" => "حملة تزيد مجد الجند",
                        "MinisterRevenue" => "مشروع يزيد دخل الخزينة",
                        "MinisterSpyInfluence" => "توسيع شبكة النفوذ الخفي",
                        _ => "حفظ استقرار الدواوين"
                    };
                    description = "ينظر إلى منصبه كطريق لإثبات الكفاءة وتوسيع النفوذ داخل المجلس.";
                    urgency = Math.Clamp(character.Skills.Intrigue * 8 + character.Skills.Stewardship * 4, 20, 90);
                    break;
                case CharacterRoleType.NeighborRuler:
                    var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == character.SourceId);
                    type = "NeighborBorderClaim";
                    title = "تحقيق مصلحة حدودية";
                    description = "يراقب ضعف الخلافة وفرص التجارة والحرب ليكسب مقاطعة أو معاهدة أفضل.";
                    urgency = Math.Clamp((neighbor?.MilitaryAmbition ?? 40) + Math.Max(0, -(neighbor?.Opinion ?? 0)), 20, 100);
                    break;
                case CharacterRoleType.Child:
                    type = "HeirLegitimacy";
                    title = "تثبيت الشرعية بين النبلاء";
                    description = "يحتاج إلى دعم القصر والولاة حتى لا يتحول الانتقال القادم إلى أزمة.";
                    urgency = 55;
                    break;
                default:
                    type = "CourtInfluence";
                    title = "مكانة داخل البلاط";
                    description = "يريد موقعاً أفضل في القصر أو حماية من تبدل الولاءات.";
                    urgency = 35;
                    break;
            }

            return new CharacterObjective
            {
                CharacterId = character.Id,
                CharacterName = character.Name,
                SourceType = character.SourceType,
                ObjectiveType = type,
                Title = title,
                Description = description,
                Urgency = urgency,
                Progress = Math.Clamp(urgency / 3, 0, 80)
            };
        }

        private static void ApplyObjectivePressure(GameState state, CharacterObjective objective, List<string> reports)
        {
            switch (objective.ObjectiveType)
            {
                case "GovernorAutonomy":
                    var governor = state.Governors.FirstOrDefault(g => g.Name == objective.CharacterName);
                    if (governor != null)
                    {
                        governor.Loyalty = Math.Clamp(governor.Loyalty - 5, 0, 100);
                        governor.OpinionOfKing = Math.Clamp(governor.OpinionOfKing - 4, -100, 100);
                        governor.SecretPlan = "ضغط من أجل امتيازات إقطاعية";
                        governor.UpdateMood();
                    }
                    reports.Add($"{objective.CharacterName} صعّد هدفه: {objective.Title}. بدأ يضغط على البلاط بوضوح أكبر.");
                    break;
                case "SpouseHeirInfluence":
                    var wife = state.Wives.FirstOrDefault(w => w.Name == objective.CharacterName);
                    if (wife != null)
                    {
                        wife.Influence = Math.Clamp(wife.Influence + 5, 0, 100);
                        wife.Ambition = Math.Clamp(wife.Ambition + 3, 0, 100);
                    }
                    reports.Add($"{objective.CharacterName} وسّعت شبكتها في القصر من أجل هدفها: {objective.Title}.");
                    break;
                case "MinisterSpyInfluence":
                    var spymaster = state.Council.Values.FirstOrDefault(c => c.Title.Contains("استخ"));
                    if (spymaster != null) spymaster.Influence = Math.Clamp(spymaster.Influence + 6, 0, 100);
                    reports.Add("مدير الاستخبارات وسّع نفوذه بهدوء داخل البلاط.");
                    break;
                case "NeighborBorderClaim":
                    var neighbor = state.Neighbors.FirstOrDefault(n => (n.RulerName ?? n.Ruler) == objective.CharacterName);
                    if (neighbor != null)
                    {
                        neighbor.MilitaryAmbition = Math.Clamp(neighbor.MilitaryAmbition + 6, 0, 100);
                        neighbor.SecretPlan = "اختبار حدود الخلافة";
                    }
                    reports.Add($"{objective.CharacterName} صار أكثر اندفاعاً نحو هدف حدودي.");
                    break;
                default:
                    reports.Add($"{objective.CharacterName} تقدم في هدفه الشخصي: {objective.Title}.");
                    break;
            }
        }
    }
}
