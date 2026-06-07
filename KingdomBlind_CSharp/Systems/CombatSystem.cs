using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class CombatSystem
    {
        private static Random rand = new Random();

        public static string DeclareWar(GameState state, int neighborIdx, string warType, string liberationTarget = null)
        {
            var n = state.Neighbors[neighborIdx];
            
            string targetName = "";
            int garrison = 0;
            
            if (warType == "conquest")
            {
                targetName = n.ClaimedProvince ?? "";
                garrison = 50;
                var claimable = n.ClaimableProvinces.FirstOrDefault(cp => cp.Name == targetName);
                if (claimable != null)
                {
                    garrison = claimable.Garrison;
                }
            }
            else // liberation
            {
                targetName = liberationTarget;
                garrison = (int)(n.Army * 0.3);
            }
            
            n.Relation = "في حرب";
            n.TradeTreaty = false;
            
            state.ActiveWar = new ActiveWar
            {
                Type = warType,
                NeighborIdx = neighborIdx,
                TargetProvince = targetName,
                Garrison = garrison,
                Turns = 0,
                AllyCalled = false
            };
            
            string warTypeStr = warType == "conquest" ? "غزو وتوسع" : "تحرير واستعادة";
            
            string warText = 
                $"⚔️ إعلان حرب {warTypeStr}! ⚔️\n\n" +
                $"أعلن {state.RulerName} الحرب على {n.Name} للسيطرة على مقاطعة {targetName}!\n\n" +
                $"تقدمت جيوشك الملكية ({state.Army} مقاتل) نحو أسوار {targetName} وبدأت في حصارها.\n" +
                $"قوة حامية العدو في المقاطعة: {garrison} مقاتلاً.\n" +
                $"دقت طبول الحرب واشتعل سعير المعركة!";
                
            state.GameMode = "siege";
            return warText;
        }

        public static string SiegeContinue(GameState state)
        {
            if (state.ActiveWar == null) return "لا توجد حرب نشطة.";
            
            var war = state.ActiveWar;
            war.Turns += 1;
            
            state.Gold -= 30;
            state.Food -= 40;
            
            double uniform = rand.NextDouble() * (0.35 - 0.20) + 0.20;
            int reduction = (int)(war.Garrison * uniform);
            reduction = Math.Max(5, reduction);
            war.Garrison = Math.Max(0, war.Garrison - reduction);
            
            double eventRoll = rand.NextDouble();
            string siegeEvent = "";
            
            if (eventRoll < 0.20)
            {
                int extraLoss = (int)(war.Garrison * 0.15);
                war.Garrison = Math.Max(0, war.Garrison - extraLoss);
                siegeEvent = $"\n💠 تفشى وباء في صفوف المحاصرين! فقدوا {extraLoss} مقاتلاً إضافياً.";
            }
            else if (eventRoll < 0.35)
            {
                int extraLoss = rand.Next(5, 16);
                war.Garrison = Math.Max(0, war.Garrison - extraLoss);
                siegeEvent = $"\n🏃 فر {extraLoss} من جنود العدو من القلعة وانضموا لجيشك!";
                state.Army += extraLoss;
            }
            else if (eventRoll < 0.50)
            {
                int ourLoss = rand.Next(5, 16);
                state.Army = Math.Max(10, state.Army - ourLoss);
                siegeEvent = $"\n⚔️ شن العدو غارة مفاجئة من القلعة! خسرنا {ourLoss} مقاتلاً.";
            }
            else if (eventRoll < 0.60)
            {
                state.Food -= 30;
                siegeEvent = "\n🌾 نقص في إمدادات الجيش المحاصِر! خسرنا 30 مؤونة إضافية.";
            }
            
            if (war.Garrison <= 0)
            {
                return SiegeVictory(state);
            }
            
            string warning = "";
            if (state.Gold <= 0 || state.Food <= 0 || state.Army <= 10)
            {
                warning = "\n\nتنبيه: مواردك على وشك النفاد! يجب الانسحاب أو المخاطرة بالاقتحام.";
            }
            
            return 
                $"تقدم الحصار - الدور {war.Turns}:\n\n" +
                $"انخفضت حامية العدو بمقدار {reduction} مقاتل.{siegeEvent}\n" +
                $"الحامية المتبقية: {war.Garrison} مقاتل.\n" +
                $"تكلفة الحصار: -30 ذهب، -40 مؤونة." + warning;
        }

        public static string SiegeStorm(GameState state)
        {
            if (state.ActiveWar == null) return "لا توجد حرب نشطة.";
            
            var war = state.ActiveWar;
            var n = state.Neighbors[war.NeighborIdx];
            
            double braveBonus = state.RulerTraits.Contains("شجاع") ? 1.2 : 1.0;
            int effectiveArmy = (int)(state.Army * braveBonus);
            int winChance = (int)(((double)effectiveArmy / Math.Max(1, effectiveArmy + war.Garrison)) * 100);
            winChance = Math.Max(10, Math.Min(95, winChance));
            
            int roll = rand.Next(1, 101);
            
            if (roll <= winChance)
            {
                int casualties = (int)(war.Garrison * (rand.NextDouble() * (0.5 - 0.3) + 0.3));
                state.Army = Math.Max(10, state.Army - casualties);
                return SiegeVictory(state);
            }
            else
            {
                int casualties = (int)(state.Army * (rand.NextDouble() * (0.45 - 0.25) + 0.25));
                state.Army = Math.Max(10, state.Army - casualties);
                state.RulerStress = Math.Min(100, state.RulerStress + 30);
                
                return 
                    $"هزيمة مدوية عند أسوار {war.TargetProvince}!\n\n" +
                    $"صد المدافعون الهجوم بشراسة وكبدوا جيشك خسائر فادحة!\n" +
                    $"خسرت {casualties} مقاتلاً. الجيش المتبقي: {state.Army}.\n" +
                    $"ارتفع الضغط النفسي للملك (+30 ضغط).\n\n" +
                    $"لا يزال بإمكانك مواصلة الحصار أو الانسحاب.";
            }
        }

        public static string SiegeAllySupport(GameState state)
        {
            if (state.ActiveWar == null) return "لا توجد حرب نشطة.";
            
            var war = state.ActiveWar;
            
            Neighbor ally = state.Neighbors.FirstOrDefault(an => an.Alliance && an.Relation != "في حرب");
            
            if (ally == null)
            {
                return "لا يوجد حليف عسكري متاح للمساعدة!";
            }
            
            war.AllyCalled = true;
            int reinforcements = rand.Next(50, 81);
            state.Army += reinforcements;
            ally.Army = Math.Max(20, ally.Army - reinforcements);
            
            return 
                $"وصلت نجدات عسكرية من الحليف {ally.Name}! " +
                $"انضم {reinforcements} فارساً مدججين بالسلاح إلى جيشك الملكي. " +
                $"قوة جيشك الحالية: {state.Army} مقاتل.";
        }

        public static string SiegeVictory(GameState state)
        {
            if (state.ActiveWar == null) return "لا توجد حرب نشطة.";
            
            var war = state.ActiveWar;
            var n = state.Neighbors[war.NeighborIdx];
            
            string victoryText = "";
            
            if (war.Type == "conquest")
            {
                NeighborProvince newProvData = n.ClaimableProvinces.FirstOrDefault(cp => cp.Name == war.TargetProvince);
                
                if (newProvData != null)
                {
                    string[] vassals = { "حمزة", "خالد", "أسامة", "صهيب", "سلمان" };
                    string newVassal = "الوالي " + vassals[rand.Next(vassals.Length)];
                    
                    state.Provinces.Add(new Province
                    {
                        Name = newProvData.Name,
                        Vassal = newVassal,
                        VassalReligion = "سُني أشعري",
                        Income = newProvData.Income,
                        Garrison = 20,
                        Satisfaction = 50,
                        Opinion = rand.Next(10, 41),
                        Religion = newProvData.Religion,
                        Minorities = newProvData.Minorities,
                        HolySite = null,
                        Occupied = false,
                        OccupiedBy = null
                    });
                    
                    n.ClaimableProvinces.RemoveAll(cp => cp.Name == war.TargetProvince);
                }
                
                n.HasClaim = false;
                n.ClaimedProvince = null;
                
                n.Army = Math.Max(50, n.Army - 80);
                n.Relation = "هدنة";
                n.Opinion = Math.Max(-100, n.Opinion - 40);
                
                state.Prestige += 40;
                state.Piety += 20;
                state.Gold += 80;
                
                victoryText = 
                    $"🏆 انتصار عظيم! سقطت مقاطعة {war.TargetProvince}! 🏆\n\n" +
                    $"بعد حصار دام {war.Turns} أدوار، انهارت دفاعات {n.Name} وسقطت المقاطعة بيد جيوش سلالة {state.DynastyName}!\n\n" +
                    $"تم ضم {war.TargetProvince} كمقاطعة جديدة لمملكتك مع تعيين والٍ جديد عليها.\n" +
                    $"+40 هيبة، +20 تقوى، +80 ذهب (غنائم حرب).\n" +
                    $"تقلصت قوة {n.Name} العسكرية وأصبحت العلاقة هدنة.";
            }
            else // Liberation
            {
                var p = state.Provinces.FirstOrDefault(prov => prov.Name == war.TargetProvince);
                if (p != null)
                {
                    p.Occupied = false;
                    p.OccupiedBy = null;
                }
                
                n.Army = Math.Max(50, n.Army - 60);
                n.Relation = "هدنة";
                n.Opinion = Math.Max(-100, n.Opinion - 30);
                
                state.Prestige += 50;
                state.Piety += 30;
                
                victoryText = 
                    $"🏆 تحرير مقاطعة {war.TargetProvince}! 🏆\n\n" +
                    $"بعد حصار مجيد دام {war.Turns} أدوار، حرر جيش {state.RulerName} مقاطعة {war.TargetProvince} من قبضة المحتل {n.Name}!\n\n" +
                    $"عادت المقاطعة لسيطرتك الكاملة واستؤنفت عائداتها ومؤنها.\n" +
                    $"+50 هيبة، +30 تقوى. الحمد لله على النصر!";
            }
            
            state.ActiveWar = null;
            state.GameMode = "sandbox";
            
            return victoryText;
        }
    }
}
