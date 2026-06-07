using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class DynastySystem
    {
        private static readonly Random Rand = new Random();

        public static GameActionResult HoldRoyalBanquet(GameState state)
        {
            var res = new GameActionResult { Title = "إقامة مأدبة ملكية" };
            if (state.Gold < 200 || state.Food < 100)
            {
                res.Success = false;
                res.MainMessage = "ليس لديك ما يكفي من الموارد. تحتاج إلى 200 ذهب و 100 مؤونة لإقامة مأدبة.";
                return res;
            }

            state.Gold -= 200;
            state.Food -= 100;
            state.Prestige += 50;
            state.QueenHappiness = Math.Min(100, state.QueenHappiness + 20);

            foreach (var neighbor in state.Neighbors)
            {
                if (neighbor.Relation != "حرب" && neighbor.Relation != "عدائية")
                {
                    neighbor.Opinion += 15;
                }
            }

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -200);
            res.ResourceChanges.Add("المؤونة", -100);
            res.ResourceChanges.Add("الهيبة", 50);
            res.MainMessage = "أقمت مأدبة ملكية كبرى حضرها النبلاء. زادت هيبتك، تحسنت علاقاتك مع الدول المجاورة، وأسعدت الملكة.";
            res.SoundEffectKey = "harp";
            return res;
        }

        public static GameActionResult EducateHeir(GameState state, string focus)
        {
            var res = new GameActionResult { Title = "تعليم ولي العهد" };
            if (state.Gold < 50)
            {
                res.Success = false;
                res.MainMessage = "تحتاج 50 ذهباً لتوظيف أفضل المعلمين لولي العهد.";
                return res;
            }

            state.Gold -= 50;

            if (state.HeirSkills == null)
            {
                state.HeirSkills = new System.Collections.Generic.Dictionary<string, int> { {"عسكري", 0}, {"اقتصادي", 0}, {"دبلوماسي", 0}, {"ديني", 0} };
            }

            if (state.HeirSkills.ContainsKey(focus))
            {
                state.HeirSkills[focus] += 1;
            }

            res.Success = true;
            res.MainMessage = $"تلقى ولي العهد تعليماً مركزاً في المجال {focus}. مهارته في هذا المجال ارتفعت وتجهزه ليكون حاكماً عظيماً في المستقبل.";
            res.SoundEffectKey = "paper";
            return res;
        }

        public static GameActionResult ArrangeMarriage(GameState state, int neighborIndex)
        {
            var res = new GameActionResult { Title = "زواج دبلوماسي" };
            if (neighborIndex < 0 || neighborIndex >= state.Neighbors.Count)
            {
                res.Success = false; res.MainMessage = "دولة غير صالحة."; return res;
            }

            var neighbor = state.Neighbors[neighborIndex];
            
            if (neighbor.Relation == "حرب")
            {
                res.Success = false;
                res.MainMessage = $"لا يمكنك ترتيب زواج مع {neighbor.Name} وأنت في حالة حرب معهم!";
                return res;
            }

            if (neighbor.Opinion < 0)
            {
                res.Success = false;
                res.MainMessage = $"يرفض حاكم {neighbor.Name} الزواج بسبب علاقتكما المتوترة.";
                return res;
            }

            if (state.Wives.Any(w => w.OriginType == "ForeignKingdom" && w.OriginId == neighbor.Id))
            {
                res.Success = false;
                res.MainMessage = $"يوجد بالفعل زواج دبلوماسي مرتبط بـ {neighbor.Name}.";
                return res;
            }

            if (state.Prestige < 50)
            {
                res.Success = false;
                res.MainMessage = "تحتاج إلى 50 هيبة على الأقل لعقد زواج دبلوماسي مؤثر.";
                return res;
            }

            state.Prestige -= 50;
            var newWife = CreateDiplomaticSpouse(state, neighbor);
            neighbor.Opinion += 40;
            neighbor.Trust += 15;
            neighbor.Alliance = true;
            neighbor.IsAlly = true;
            neighbor.Relation = "تحالف";

            int today = DiplomacySystem.GetCurrentDayNumber(state);
            if (!DiplomacySystem.HasActiveTreaty(state, neighbor.Id, "MarriageAlliance"))
            {
                state.Treaties.Add(new DiplomaticTreaty
                {
                    TreatyType = "MarriageAlliance",
                    KingdomAId = "Player",
                    KingdomBId = neighbor.Id,
                    StartDateDays = today,
                    DurationDays = 360 * 5,
                    EndDateDays = today + 360 * 5,
                    BreakPenalty = 50,
                    Notes = $"تحالف زواج عبر {newWife.Name}"
                });
            }

            DiplomacySystem.SynchronizeDiplomacyState(state);
            LivingRealmSystem.AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "MarriageAlliance", $"ارتبط بيت الحكم بزواج دبلوماسي عبر {newWife.Name}.", 0, 0, 0, 2, 900, true);
            LivingRealmSystem.AddMemory(state, "Spouse", newWife.Id, newWife.Name, "DiplomaticMarriage", $"دخلت القصر بصفتها صلة سياسية مع {neighbor.Name}.", 0, 0, 0, 2, 900, true);

            res.Success = true;
            res.ResourceChanges.Add("الهيبة", -50);
            res.MainMessage = $"تم عقد زواج دبلوماسي مع بيت الحكم في {neighbor.Name}. انضمت {newWife.Name} إلى القصر، وزادت العلاقات بشكل كبير وأصبحتم حلفاء.";
            res.SoundEffectKey = "harp";
            return res;
        }

        public static string AgeCharacters(GameState state)
        {
            state.RulerAge += 1;
            if (!string.IsNullOrEmpty(state.HeirName))
            {
                state.HeirAge += 1;
            }

            foreach (var child in state.Children)
                child.Age += 1;
                
            foreach (var wife in state.Wives)
            {
                if (!wife.IsDead) wife.Age += 1;
            }

            string report = $"مرت سنة جديدة من حكم {state.RulerName}. عمرك الآن {state.RulerAge} عاماً.";
            
            // Death chance
            if (state.RulerAge > 40 && Rand.Next(100) < (state.RulerAge - 35))
            {
                state.RulerIsDead = true;
                report += " للأسف، لقد وافتك المنية لأسباب طبيعية! ينتقل الحكم الآن لولي العهد...";
            }

            return report;
        }

        public static Spouse CreateDiplomaticSpouse(GameState state, Neighbor neighbor)
        {
            var existing = state.Wives.FirstOrDefault(w => w.OriginType == "ForeignKingdom" && w.OriginId == neighbor.Id);
            if (existing != null)
                return existing;

            string[] titles = { "الأميرة", "الشريفة", "السيدة" };
            var spouse = new Spouse
            {
                Name = $"{titles[Rand.Next(titles.Length)]} من {neighbor.Name}",
                OriginType = "ForeignKingdom",
                OriginId = neighbor.Id,
                OpinionOfKing = 65,
                Trust = 55,
                Influence = 20,
                DiplomacySkill = 7,
                PoliticalSkill = 6
            };

            state.Wives.Add(spouse);
            return spouse;
        }

        public static bool TryAssignExclusiveDuty(GameState state, string spouseId, string task, string targetId, string targetName, int days, out string message)
        {
            var wife = state.Wives.FirstOrDefault(w => w.Id == spouseId);
            if (wife == null || wife.IsDead)
            {
                message = "لم يتم العثور على الملكة أو أنها متوفية.";
                return false;
            }

            if (wife.DutyDaysRemaining > 0 && !string.IsNullOrWhiteSpace(wife.CurrentTask))
            {
                message = $"{wife.Name} مكلفة حالياً بمهمة أخرى: {GetDutyDisplayName(wife.CurrentTask)}.";
                return false;
            }

            foreach (var other in state.Wives.Where(w => w.Id != spouseId && !w.IsDead && w.DutyDaysRemaining > 0))
            {
                if (IsExclusiveTask(task) && other.CurrentTask == task)
                {
                    message = $"{other.Name} مكلفة بالفعل بهذه المهمة الحصرية: {GetDutyDisplayName(task)}.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(targetId) && other.DutyTargetId == targetId)
                {
                    message = $"{other.Name} تعمل بالفعل على نفس الهدف السياسي: {targetName}.";
                    return false;
                }
            }

            wife.CurrentTask = task;
            wife.DutyTargetId = targetId;
            wife.DutyTargetName = targetName;
            wife.DutyDaysRemaining = Math.Max(1, days);
            message = "تم التكليف بنجاح.";
            return true;
        }

        public static GameActionResult ProcessDailyDynasty(GameState state)
        {
            var res = new GameActionResult { Title = "تحديث السلالة اليومي", Success = true, ShouldNarrate = false };
            string narrative = "";

            foreach (var wife in state.Wives)
            {
                if (wife.IsDead)
                {
                    wife.CurrentTask = null;
                    wife.DutyDaysRemaining = 0;
                    continue;
                }

                if (wife.DutyDaysRemaining > 0)
                {
                    wife.DutyDaysRemaining--;
                    if (wife.DutyDaysRemaining == 0)
                    {
                        narrative += $"أنهت {wife.Name} مهمة {GetDutyDisplayName(wife.CurrentTask)}.\n";
                        wife.CurrentTask = null;
                        wife.DutyTargetId = null;
                        wife.DutyTargetName = null;
                    }
                }

                if (wife.IsPregnant)
                {
                    wife.PregnancyDaysLeft--;
                    if (wife.PregnancyDaysLeft <= 0)
                    {
                        wife.IsPregnant = false;
                        wife.PregnancyDaysLeft = 0;
                        var child = CreateNewborn(state, wife);
                        narrative += $"خبر من القصر: ولدت {wife.Name} طفلاً جديداً باسم {child.Name}.\n";
                        res.ShouldPauseTime = true;
                    }
                }
            }

            if (state.Time.Month == 1 && state.Time.Day == 1)
                narrative += AgeCharacters(state) + "\n";

            if (!string.IsNullOrWhiteSpace(narrative))
            {
                res.ShouldNarrate = true;
                res.MainMessage = narrative.Trim();
            }

            return res;
        }

        public static GameActionResult ConsultWife(GameState state, string spouseId)
        {
            var res = new GameActionResult { Title = "استشارة الملكة" };
            var wife = state.Wives.Find(w => w.Id == spouseId);
            if (wife == null)
            {
                res.Success = false;
                res.MainMessage = "لم يتم العثور على الملكة.";
                return res;
            }

            wife.Trust = Math.Min(100, wife.Trust + 10);
            
            res.Success = true;
            if (wife.PoliticalSkill > 5)
            {
                res.MainMessage = $"قضيت وقتاً في استشارة {wife.Name}. زادت ثقتها بك وأعطتك بعض النصائح السياسية المفيدة.";
            }
            else
            {
                res.MainMessage = $"قضيت وقتاً في التحدث مع {wife.Name}. زادت ثقتها بك.";
            }
            res.SoundEffectKey = "paper";
            return res;
        }

        public static GameActionResult CalmProvinceByWife(GameState state, string spouseId)
        {
            var res = new GameActionResult { Title = "تهدئة الأوضاع عبر الملكة" };
            var wife = state.Wives.Find(w => w.Id == spouseId);
            if (wife == null)
            {
                res.Success = false;
                res.MainMessage = "لم يتم العثور على الملكة.";
                return res;
            }

            if (wife.OriginType != "GovernorFamily" || string.IsNullOrEmpty(wife.RelatedGovernorId))
            {
                res.Success = false;
                res.MainMessage = "هذه الملكة ليست من عائلة حاكم ولا يمكنها التأثير بشكل مباشر على الحكام.";
                return res;
            }

            var governor = state.Governors.Find(g => g.Id == wife.RelatedGovernorId);
            if (governor == null)
            {
                res.Success = false;
                res.MainMessage = "عائلة الملكة لم تعد تحكم أي ولاية.";
                return res;
            }

            if (!TryAssignExclusiveDuty(state, spouseId, "CalmProvince", governor.ProvinceId, governor.ProvinceName, 14, out var dutyMessage))
            {
                res.Success = false;
                res.MainMessage = dutyMessage;
                return res;
            }

            governor.Loyalty = Math.Min(100, governor.Loyalty + 20);
            governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 20);
            governor.UpdateMood();

            foreach (var faction in state.Factions)
            {
                if (faction.MemberGovernorIds.Contains(governor.Id) || faction.LeaderGovernorId == governor.Id)
                {
                    faction.Discontent = Math.Max(0, faction.Discontent - 15);
                }
            }

            res.Success = true;
            res.MainMessage = $"تدخلت {wife.Name} لدى عائلتها في {governor.ProvinceName}. زاد ولاء الحاكم {governor.Name} وانخفضت نسبة التذمر في فصيله. ستبقى مكلفة بهذا المسار 14 يوماً.";
            res.SoundEffectKey = "harp";
            return res;
        }

        public static GameActionResult SpendPrivateTime(GameState state, string spouseId)
        {
            var res = new GameActionResult { Title = "قضاء وقت خاص" };
            var wife = state.Wives.Find(w => w.Id == spouseId);
            if (wife == null)
            {
                res.Success = false;
                res.MainMessage = "لم يتم العثور على الملكة.";
                return res;
            }

            wife.Trust = Math.Min(100, wife.Trust + 15);
            wife.OpinionOfKing = Math.Min(100, wife.OpinionOfKing + 10);
            
            Random rnd = new Random();
            if (!wife.IsPregnant && rnd.Next(100) < 30) // 30% chance
            {
                wife.IsPregnant = true;
                wife.PregnancyDaysLeft = 270; // 9 months
                res.MainMessage = $"قضيت وقتاً خاصاً مع {wife.Name}. زادت محبتها لك، وهناك أنباء سارة بأنها تحمل وريثاً محتملاً!";
            }
            else
            {
                res.MainMessage = $"قضيت وقتاً خاصاً مع {wife.Name}. زادت محبتها لك وتوطدت علاقتكما.";
            }

            res.Success = true;
            res.SoundEffectKey = "harp";
            return res;
        }

        public static GameActionResult ArrangeInternalMarriage(GameState state, string governorId)
        {
            var res = new GameActionResult { Title = "زواج داخلي سياسي" };
            var governor = state.Governors.Find(g => g.Id == governorId);
            if (governor == null)
            {
                res.Success = false;
                res.MainMessage = "لم يتم العثور على الحاكم.";
                return res;
            }

            if (state.Gold < 100)
            {
                res.Success = false;
                res.MainMessage = "تحتاج إلى 100 ذهب لترتيب مراسم الزواج.";
                return res;
            }

            state.Gold -= 100;
            governor.Loyalty = Math.Min(100, governor.Loyalty + 40);
            governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 40);
            governor.UpdateMood();

            Spouse newWife = new Spouse
            {
                Name = $"ابنة {governor.Name}",
                OriginType = "GovernorFamily",
                OriginId = governor.ProvinceName,
                RelatedProvinceId = governor.ProvinceId,
                RelatedGovernorId = governor.Id,
                OpinionOfKing = 70,
                Trust = 50
            };

            state.Wives.Add(newWife);
            LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, "InternalMarriage", $"تزوج الملك من ابنته {newWife.Name}.", 0, 0, 0, 2, 900, true);
            LivingRealmSystem.AddMemory(state, "Spouse", newWife.Id, newWife.Name, "PoliticalMarriage", $"دخلت القصر من بيت {governor.Name}.", 0, 0, 0, 2, 900, true);

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -100);
            res.MainMessage = $"تزوجت من {newWife.Name}، ابنة الحاكم {governor.Name}. أدى هذا المصاهرة إلى رفع ولاء الحاكم بشكل كبير.";
            res.SoundEffectKey = "harp";
            return res;
        }

        
        public static GameActionResult SuperviseBanquet(GameState state, string spouseId)
        {
            var res = new GameActionResult { Title = "الإشراف على مأدبة" };
            if (state.Gold < 50)
            {
                res.Success = false;
                res.MainMessage = "تحتاج إلى 50 ذهب لإقامة المأدبة.";
                return res;
            }

            if (!TryAssignExclusiveDuty(state, spouseId, "BanquetSupervision", "", "", 7, out var dutyMessage))
            {
                res.Success = false;
                res.MainMessage = dutyMessage;
                return res;
            }

            state.Gold -= 50;
            state.Prestige += 10;
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -50);
            res.MainMessage = $"تم تكليف الملكة بالإشراف على مأدبة فخمة للنبلاء. ستستغرق التحضيرات 7 أيام، وقد رفعت من هيبتك بين الحضور.";
            res.SoundEffectKey = "harp";
            return res;
        }

        public static GameActionResult ReceiveGuests(GameState state, string spouseId)
        {
            var res = new GameActionResult { Title = "استقبال ضيوف دبلوماسيين" };
            if (!TryAssignExclusiveDuty(state, spouseId, "GuestReception", "", "", 10, out var dutyMessage))
            {
                res.Success = false;
                res.MainMessage = dutyMessage;
                return res;
            }

            state.Prestige += 5;
            res.Success = true;
            res.MainMessage = $"تم تكليف الملكة باستقبال المبعوثين الدبلوماسيين في غيابك. ستقوم بهذه المهمة لمدة 10 أيام.";
            res.SoundEffectKey = "paper";
            return res;
        }

        
        public static GameActionResult PoliticalMediation(GameState state, string spouseId)
        {
            var res = new GameActionResult { Title = "وساطة سياسية لتحسين العلاقات" };
            if (!TryAssignExclusiveDuty(state, spouseId, "PoliticalMediation", "", "", 14, out var dutyMessage))
            {
                res.Success = false;
                res.MainMessage = dutyMessage;
                return res;
            }

            state.Satisfaction = System.Math.Min(100, state.Satisfaction + 5);
            res.Success = true;
            res.MainMessage = $"تم تكليف الملكة بالقيام بوساطات سياسية لتحسين العلاقات مع النبلاء. ستستغرق المهمة 14 يوماً.";
            res.SoundEffectKey = "harp";
            return res;
        }

        private static Child CreateNewborn(GameState state, Spouse mother)
        {
            string[] names = { "الحسن", "الحسين", "عبد الرحمن", "زين", "ليلى", "مريم", "فاطمة", "صفية" };
            var child = new Child
            {
                Name = names[Rand.Next(names.Length)] + " بن " + state.RulerName,
                Age = 0,
                MotherSpouseId = mother.Id,
                IsHeir = string.IsNullOrWhiteSpace(state.HeirName)
            };

            state.Children.Add(child);
            if (child.IsHeir)
            {
                state.HeirName = child.Name;
                state.HeirAge = 0;
                mother.IsMotherOfHeir = true;
            }

            return child;
        }

        private static bool IsExclusiveTask(string task)
        {
            return task == "BanquetSupervision" ||
                   task == "GuestReception" ||
                   task == "CalmProvince" ||
                   task == "PoliticalMediation";
        }

        private static string GetDutyDisplayName(string task)
        {
            return task switch
            {
                "BanquetSupervision" => "الإشراف على المأدبة",
                "GuestReception" => "استقبال الضيوف",
                "CalmProvince" => "تهدئة ولاية",
                "PoliticalMediation" => "وساطة سياسية",
                _ => string.IsNullOrWhiteSpace(task) ? "غير محددة" : task
            };
        }
    }
}
