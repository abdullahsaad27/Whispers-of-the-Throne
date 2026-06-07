using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class EventSystem
    {
        private static Random rand = new Random();

        public static string SearchForSpouse(GameState state)
        {
            var candidates = state.Neighbors.Where(n => n.Relation != "عدائية").ToList();
            if (candidates.Count == 0)
            {
                return "لا يوجد ممالك مجاورة مستعدة لتقديم أميراتها للزواج حالياً بسبب التوترات السياسية.";
            }
            return "تم إرسال رسل ومبعوثين للممالك المجاورة. من تختار لتكون زوجة لك وحليفة لعرشك؟";
        }

        public static string ExecuteMarriage(GameState state, string neighborName)
        {
            state.SpouseName = $"أميرة {neighborName}";
            state.QueenHappiness = 80;
            
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Name == neighborName);
            if (neighbor != null)
            {
                neighbor.Relation = "موالية";
                neighbor.Opinion = 100;
                neighbor.Alliance = true;
            }

            return $"تم الزواج المبارك من {state.SpouseName}! عمت الاحتفالات، وأصبحت العلاقة مع {neighborName} موالية تماماً. تم تأسيس تحالف عسكري يمكنك من طلب الدعم العسكري منهم في الحروب!";
        }

        public static string EducateChild(GameState state, int childIdx, string trait)
        {
            if (childIdx >= 0 && childIdx < state.Children.Count)
            {
                var child = state.Children[childIdx];
                return $"تم توجيه {child.Name} للتعليم لاكتساب صفة '{trait}'.";
            }
            return "Child not found.";
        }

        public static string CrushPeasantRebellion(GameState state, int provinceIdx)
        {
            var p = state.Provinces[provinceIdx];
            int losses = 30;
            foreach(var army in state.Armies) {
                if (army.TotalSoldiers >= losses) { army.TotalSoldiers -= losses; break; }
                else { losses -= army.TotalSoldiers; army.TotalSoldiers = 0; }
            }
            p.Satisfaction = Math.Max(10, p.Satisfaction - 20);
            
            return $"أرسلت فيالق الجيش الملكي لقمع الثائرين في {p.Name}. دارت معركة دموية قاسية في الشوارع تم سحق الثورة فيها بنجاح، ولكن خسرت 30 جندياً وتراجع رضا الشعب هناك بمقدار 20% نتيجة بطش العسكر.";
        }

        public static string PacifyPeasantRebellion(GameState state, int provinceIdx)
        {
            var p = state.Provinces[provinceIdx];
            state.Gold = Math.Max(0, state.Gold - 100);
            state.Prestige = Math.Max(0, state.Prestige - 30);
            p.Satisfaction = 50;
            
            return $"فضلت الحكمة الملكية وحقن الدماء. أرسلت وفداً يحمل 100 قطع ذهبية وعهوداً باحترام شعائر مذهب {p.Religion}. انفضت الجموع الثائرة بسلام وعاد الهدوء للمقاطعة، ولكن كلفك ذلك 100 ذهب و30 هيبة.";
        }

        public static string FightVassalRebellion(GameState state, int provinceIdx)
        {
            var p = state.Provinces[provinceIdx];
            int rebelForces = p.Garrison * 2;
            int totalArmy = state.Armies.Sum(a => a.TotalSoldiers);
            
            int winChance = (int)(((double)totalArmy / (totalArmy + rebelForces + 1)) * 100);
            winChance = Math.Max(10, Math.Min(95, winChance));
            
            int roll = rand.Next(1, 101);
            if (roll <= winChance)
            {
                int lossArmy = (int)(rebelForces * 0.4);
                int losses = lossArmy;
                foreach(var army in state.Armies) {
                    if (army.TotalSoldiers >= losses) { army.TotalSoldiers -= losses; break; }
                    else { losses -= army.TotalSoldiers; army.TotalSoldiers = 0; }
                }
                state.Prestige += 30;
                state.Piety += 15;
                
                string oldVassal = p.Vassal;
                string[] newNames = new string[] { "حمزة", "خالد", "أسامة", "صهيب" };
                string newVassal = $"الوالي {newNames[rand.Next(newNames.Length)]}";
                
                p.Vassal = newVassal;
                p.VassalReligion = "سُني أشعري";
                p.Opinion = rand.Next(30, 61);
                p.Satisfaction = Math.Max(10, p.Satisfaction - 10);
                
                return $"انتصار كاسح لجيوش الشرعية! دارت معركة طاحنة تحت أسوار المقاطعة، تمكنا فيها من سحق القوات المتمردة بالكامل وأسر {oldVassal} وتجريده من ألقابه.\nتكبد جيشنا {lossArmy} شهيداً، وعينّا {newVassal} والياً سُني أشعرياً جديداً. ارتفعت هيبتك وتقواك لشجاعتك في قمع التمرد.";
            }
            else
            {
                state.GameMode = "game_over";
                return $"هزيمة مدمرة لقوات الشرعية! انكسرت صفوف جيشنا الملكي في معركة {p.Name} الدامية، واجتاحت قوات الوالي المتمرد {p.Vassal} باقي أرجاء البلاد وعزلتكم عن العرش للأبد!\n\nسقطت سلالة {state.DynastyName} الحاكمة وتفككت المملكة في مهب الريح الإقطاعية.";
            }
        }

        public static string PayVassalRebellion(GameState state, int provinceIdx)
        {
            var p = state.Provinces[provinceIdx];
            state.Gold = Math.Max(0, state.Gold - 150);
            state.Prestige = Math.Max(0, state.Prestige - 30);
            p.Opinion = 20;
            
            return $"وافقت على تسوية مالية مع الوالي {p.Vassal}. تم إرسال 150 قطعة ذهبية لخزائنه وتأكيد ألقابه والتغاضي عن تمرده. عادت المقاطعة للهدوء بسلام ملكي، ولكن تراجعت هيبتك بمقدار 30 بتكلفة 150 ذهب.";
        }

        public static string GenerateRandomTurnEvents(GameState state)
        {
            string eventsReport = "";
            double roll = rand.NextDouble();

            // Disasters / Bandits (10% chance)
            if (roll < 0.10)
            {
                int stolen = rand.Next(30, 80);
                state.Gold = Math.Max(0, state.Gold - stolen);
                eventsReport += $"?? هجوم قطاع طرق! نهبت عصابة مسلحة قوافل المملكة وسرقت {stolen} قطعة ذهبية.\n";
            }
            // Marriage Proposal from Ally (10% chance)
            else if (roll > 0.90 && string.IsNullOrEmpty(state.SpouseName))
            {
                var ally = state.Neighbors.FirstOrDefault(n => n.Relation == "موالية" || n.Alliance);
                if (ally != null)
                {
                    state.SpouseName = $"أميرة {ally.Name}";
                    state.QueenHappiness = 100;
                    eventsReport += $"?? عرض مصاهرة سياسي مفاجئ! أرسل حاكم {ally.Name} يعرض زواجك من أميرته لتوثيق التحالف. تم الزواج وعمت الأفراح!\n";
                }
            }

            return eventsReport;
        }
    

        public static GameActionResult ProcessDailyRandomEvents(GameState state)
        {
            var result = new GameActionResult { Success = true, Title = "الأحداث الكبرى" };
            if (state.SuppressRandomMajorEvents)
                return new GameActionResult { ShouldNarrate = false };
            
            // Very low chance for a major event each day (e.g. 1 in 300)
            if (rand.Next(300) < 1)
            {
                int eventType = rand.Next(4);
                result.ShouldPauseTime = true;
                result.ShouldNarrate = true;
                
                if (eventType == 0)
                {
                    state.Gold += 500;
                    result.SoundEffectKey = "coins";
                    result.MainMessage = "حدث سعيد! تم اكتشاف منجم ذهب صغير في إحدى المقاطعات، وتلقت الخزينة 500 ذهب.";
                }
                else if (eventType == 1)
                {
                    state.Prestige += 100;
                    state.Food += 300;
                    result.MainMessage = "حدث سعيد! عام الحصاد الوفير! ازدادت المؤونة 300 وارتفعت الهيبة.";
                }
                else if (eventType == 2)
                {
                    state.RulerStress += 30;
                    state.Gold = Math.Max(0, state.Gold - 200);
                    result.SoundEffectKey = "sword";
                    result.MainMessage = "حدث خطير! محاولة اغتيال فاشلة للملك! تم النجاة بأعجوبة ولكن حرس القصر طلبوا 200 ذهب لتشديد الحراسة، وازداد الضغط النفسي (Stress).";
                }
                else if (eventType == 3)
                {
                    var prov = state.Provinces.Count > 0 ? state.Provinces[rand.Next(state.Provinces.Count)] : null;
                    if (prov != null)
                    {
                        prov.Satisfaction = Math.Max(0, prov.Satisfaction - 30);
                        result.MainMessage = $"حدث سيء! تمرد ديني أو عشائري مصغر في مقاطعة {prov.Name}. انخفض رضا الشعب هناك بشدة.";
                    }
                    else
                    {
                        result.ShouldNarrate = false;
                        result.ShouldPauseTime = false;
                    }
                }
                return result;
            }
            
            return new GameActionResult { ShouldNarrate = false };
        }
}
}
