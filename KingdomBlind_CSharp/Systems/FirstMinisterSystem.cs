using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class FirstMinisterSystem
    {
        private static void EnsureFirstMinisterState(GameState state)
        {
            if (state.FirstMinister == null)
                state.FirstMinister = new FirstMinister();

            if (state.MinisterBudgets == null)
                state.MinisterBudgets = new System.Collections.Generic.Dictionary<string, int>();

            if (!state.MinisterBudgets.ContainsKey("first_minister"))
                state.MinisterBudgets["first_minister"] = state.FirstMinister.MonthlyBudgetPercent;

            if (string.IsNullOrWhiteSpace(state.FirstMinister.CurrentTask))
                state.FirstMinister.CurrentTask = "انتظار الأوامر";
        }

        public static GameActionResult AppointMinister(GameState state, string name, int admin, int diplomacy, int integrity)
        {
            EnsureFirstMinisterState(state);
            state.FirstMinister = new FirstMinister
            {
                Name = name,
                AdministrativeSkill = admin,
                DiplomacySkill = diplomacy,
                Integrity = integrity,
                IsAppointed = true,
                Loyalty = 100,
                OpinionOfKing = 100,
                Influence = 50,
                CurrentTask = "انتظار الأوامر"
            };

            state.MinisterBudgets["first_minister"] = state.FirstMinister.MonthlyBudgetPercent;
            return new GameActionResult { Success = true, MainMessage = $"تم تعيين {name} في منصب الوزير الأول للمملكة." };
        }

        public static GameActionResult SetMonthlyBudget(GameState state, int percent)
        {
            EnsureFirstMinisterState(state);
            var res = new GameActionResult { Title = "ميزانية الوزير الأول" };
            if (state.FirstMinister == null || !state.FirstMinister.IsAppointed)
            {
                res.Success = false;
                res.MainMessage = "لا يوجد وزير أول معين.";
                return res;
            }

            if (percent < 0 || percent > 40)
            {
                res.Success = false;
                res.MainMessage = "ميزانية الوزير الأول يجب أن تكون بين 0% و40% حتى لا تختنق بقية الدواوين.";
                return res;
            }

            state.MinisterBudgets["first_minister"] = percent;
            state.FirstMinister.MonthlyBudgetPercent = percent;
            res.Success = true;
            res.MainMessage = $"تم تحديد ميزانية الوزير الأول إلى {percent}% من الدخل الشهري.";
            return res;
        }

        public static GameActionResult AssignTask(GameState state, string taskKey)
        {
            EnsureFirstMinisterState(state);
            var res = new GameActionResult { Title = "تكليف الوزير الأول" };
            if (state.FirstMinister == null || !state.FirstMinister.IsAppointed)
            {
                res.Success = false;
                res.MainMessage = "لا يوجد وزير أول معين.";
                return res;
            }

            if (state.FirstMinister.TaskDaysRemaining > 0)
            {
                res.Success = false;
                res.MainMessage = $"الوزير الأول مشغول حالياً بمهمة: {state.FirstMinister.CurrentTask}. متبقٍ {state.FirstMinister.TaskDaysRemaining} يوم.";
                return res;
            }

            var (taskName, cost, days) = taskKey switch
            {
                "AuditTaxes" => ("مراجعة سجلات الجباية", 100, 20),
                "CoordinateCouncil" => ("تنسيق أعمال المجلس", 80, 15),
                "AppeaseGovernors" => ("تهدئة الولاة الكبار", 120, 25),
                "CentralizeDiwans" => ("مركزة الدواوين العباسية", 150, 35),
                "AntiCorruption" => ("تفتيش الفساد في الدواوين", 100, 20),
                "RoadTaxReform" => ("تنظيم مكوس الطرق والقوافل", 120, 25),
                _ => ("مهمة إدارية عامة", 50, 10)
            };

            if (state.Gold < cost)
            {
                res.Success = false;
                res.MainMessage = $"تحتاج إلى {cost} ذهب لتكليف الوزير الأول بهذه المهمة.";
                return res;
            }

            state.Gold -= cost;
            state.FirstMinister.CurrentTask = taskName;
            state.FirstMinister.TaskDaysRemaining = days;
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -cost);
            res.MainMessage = $"بدأ الوزير الأول مهمة: {taskName}. ستستغرق {days} يوماً.";
            res.SoundEffectKey = "paper";
            return res;
        }

        public static GameActionResult ProcessDailyFirstMinister(GameState state)
        {
            EnsureFirstMinisterState(state);
            var res = new GameActionResult { Title = "الوزير الأول", Success = true, ShouldNarrate = false };
            if (state.FirstMinister == null || !state.FirstMinister.IsAppointed || state.FirstMinister.TaskDaysRemaining <= 0)
                return res;

            state.FirstMinister.TaskDaysRemaining--;
            if (state.FirstMinister.TaskDaysRemaining > 0)
                return res;

            string completedTask = state.FirstMinister.CurrentTask;
            state.FirstMinister.CurrentTask = "انتظار الأوامر";

            switch (completedTask)
            {
                case "مراجعة سجلات الجباية":
                    int taxGain = 120 + (state.FirstMinister.AdministrativeSkill * 20);
                    state.Gold += taxGain;
                    state.MerchantsTrust = Math.Max(0, state.MerchantsTrust - 2);
                    res.MainMessage = $"أكمل الوزير الأول مراجعة سجلات الجباية. دخل للخزينة {taxGain} ذهب، لكن بعض التجار تذمروا من التدقيق.";
                    res.ResourceChanges.Add("الذهب", taxGain);
                    break;
                case "تنسيق أعمال المجلس":
                    foreach (var member in state.Council.Values)
                    {
                        member.Loyalty = Math.Min(100, member.Loyalty + 4);
                        member.Trust = Math.Min(100, member.Trust + 3);
                    }
                    state.Prestige += 8;
                    res.MainMessage = "أكمل الوزير الأول تنسيق أعمال المجلس. تحسن ولاء المستشارين وارتفعت هيبة البلاط.";
                    break;
                case "تهدئة الولاة الكبار":
                    foreach (var governor in state.Governors)
                    {
                        governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 8);
                        governor.Loyalty = Math.Min(100, governor.Loyalty + 5);
                        governor.UpdateMood();
                    }
                    foreach (var faction in state.Factions.Where(f => f.IsActive))
                        faction.Discontent = Math.Max(0, faction.Discontent - 6);
                    res.MainMessage = "عاد الوزير الأول من جولة تهدئة الولاة. انخفض سخط الفصائل وتحسن رأي الولاة بك.";
                    break;
                case "مركزة الدواوين العباسية":
                    state.CrownAuthority = state.CrownAuthority < CrownAuthorityLevel.Absolute
                        ? state.CrownAuthority + 1
                        : state.CrownAuthority;
                    state.Satisfaction = Math.Max(0, state.Satisfaction - 3);
                    state.Prestige += 12;
                    res.MainMessage = $"اكتملت مركزة الدواوين العباسية. ارتفعت سلطة التاج إلى {state.CrownAuthority}، لكن بعض الرعية تضايقوا من تشدد الدولة.";
                    break;
                case "تفتيش الفساد في الدواوين":
                    int revealed = 0;
                    foreach (var member in state.Council.Values.Where(m => m.IsCorrupt))
                    {
                        member.CorruptionDiscovered = true;
                        member.HiddenCorruptionRate = Math.Max(0, member.HiddenCorruptionRate - 3);
                        revealed++;
                    }
                    state.Prestige += revealed > 0 ? 10 : 3;
                    res.MainMessage = revealed > 0
                        ? $"كشف الوزير الأول {revealed} موضع فساد داخل الدواوين وخفّض الاختلاس الشهري."
                        : "أنهى الوزير الأول التفتيش ولم يجد فساداً مؤكداً، لكن هيبة الانضباط الإداري زادت.";
                    break;
                case "تنظيم مكوس الطرق والقوافل":
                    state.MerchantsTrust = Math.Min(100, state.MerchantsTrust + 8);
                    state.Gold += 80;
                    res.MainMessage = "نظم الوزير الأول مكوس الطرق والقوافل. زادت ثقة التجار ودخلت رسوم عادلة إلى الخزينة.";
                    res.ResourceChanges.Add("الذهب", 80);
                    break;
                default:
                    state.Prestige += 2;
                    res.MainMessage = "أنهى الوزير الأول مهمة إدارية عامة ورفع انتظام الديوان قليلاً.";
                    break;
            }

            res.Success = true;
            res.ShouldNarrate = true;
            return res;
        }

        public static GameActionResult GenerateComprehensiveReport(GameState state)
        {
            if (state.FirstMinister == null || !state.FirstMinister.IsAppointed)
            {
                return new GameActionResult { Success = false, MainMessage = "لا يوجد وزير أول معين لتقديم تقرير." };
            }

            string report = "تقرير الوزير الأول الشامل:\n\n";
            report += $"المهمة الحالية: {state.FirstMinister.CurrentTask}";
            if (state.FirstMinister.TaskDaysRemaining > 0)
                report += $"، متبقٍ {state.FirstMinister.TaskDaysRemaining} يوم";
            report += $"\nميزانيته الشهرية: {state.FirstMinister.MonthlyBudgetPercent}% من الدخل.\n\n";

            report += $"الاقتصاد: الخزينة فيها {state.Gold} ذهب، ومستوى الضرائب {state.TaxLevel}. ";
            if (state.Gold < 200) report += "نحن نعاني مالياً يا مولاي، يجب البحث عن مصادر دخل جديدة أو قروض.\n";
            else report += "الاقتصاد مستقر.\n";

            report += $"العسكر: إجمالي الجنود المتاحين {state.Army}. ";
            if (state.ActiveWar != null) report += "نحن في حالة حرب، ويجب توجيه الموارد لدعم المجهود الحربي.\n";
            else report += "لا توجد حروب حالياً.\n";

            if (state.Factions.Count > 0)
            {
                report += $"الفصائل: هناك {state.Factions.Count} فصائل نشطة. ";
                if (state.Factions.Exists(f => f.Discontent > 80)) report += "بعض الفصائل خطيرة جداً وتستعد للتمرد!\n";
                else report += "لكنهم تحت السيطرة.\n";
            }
            else
            {
                report += "الوضع الداخلي مستقر ولا توجد فصائل معارضة.\n";
            }

            if (state.Council.ContainsKey("spymaster") && state.Council["spymaster"].Influence > 80)
            {
                report += "\n[تحذير خاص]: مسؤول الجواسيس أصبح يمتلك نفوذاً هائلاً في البلاط. إذا لم يكن ولاؤه مطلقاً، فقد يشكل خطراً كبيراً على العرش.";
            }

            return new GameActionResult { Success = true, MainMessage = report, ShouldNarrate = true };
        }
    }
}
