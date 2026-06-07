using System;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class IntrigueSystem
    {
        private static Random rand = new Random();

        public static GameActionResult AttemptAssassination(GameState state, int neighborIndex)
        {
            var res = new GameActionResult { Title = "محاولة اغتيال" };
            if (neighborIndex < 0 || neighborIndex >= state.Neighbors.Count)
            {
                res.Success = false; res.MainMessage = "هدف غير صالح."; return res;
            }

            var neighbor = state.Neighbors[neighborIndex];
            int cost = 150;

            if (state.Gold < cost)
            {
                res.Success = false; res.MainMessage = "لا تملك الذهب الكافي (تحتاج 150 ذهب) لتمويل هذه المؤامرة الحساسة."; return res;
            }

            state.Gold -= cost;
            int roll = rand.Next(1, 101);

            if (roll <= 40) // 40% chance of success
            {
                string[] newRulers = { "الأمير المتمرد", "القائد عثمان", "شقيق الحاكم" };
                string newRuler = newRulers[rand.Next(newRulers.Length)];
                string oldRuler = neighbor.Ruler;
                
                neighbor.Ruler = newRuler;
                neighbor.Opinion = 0; 
                
                res.Success = true;
                res.SoundEffectKey = "sword";
                res.MainMessage = $"نجحت المؤامرة! تم اغتيال {oldRuler} في الخفاء. الحاكم الجديد لـ {neighbor.Name} هو {newRuler}. كلفتك العملية {cost} ذهب.";
            }
            else
            {
                neighbor.Opinion -= 50;
                res.Success = true; // the action was executed, though it failed in-game
                res.MainMessage = $"فشلت عملية الاغتيال واُكتشف جواسيسك! غضب حاكم {neighbor.Name} بشدة وتدهورت العلاقات بينكما. (تكلفة المؤامرة: {cost} ذهب).";
            }
            return res;
        }

        public static GameActionResult ForgeClaim(GameState state, int neighborIndex, string provinceName)
        {
            var res = new GameActionResult { Title = "تزوير مطالبة شرعية" };
            if (neighborIndex < 0 || neighborIndex >= state.Neighbors.Count)
            {
                res.Success = false; res.MainMessage = "هدف غير صالح."; return res;
            }

            var neighbor = state.Neighbors[neighborIndex];
            if (state.Gold < 100 || state.Prestige < 15)
            {
                res.Success = false; res.MainMessage = "تحتاج 100 ذهب و 15 هيبة لتمويل تزوير المطالبة."; return res;
            }

            state.Gold -= 100;
            state.Prestige -= 15;
            
            int roll = rand.Next(1, 101);
            if (roll <= 60) // 60% chance
            {
                neighbor.HasClaim = true;
                neighbor.ClaimedProvince = provinceName;
                res.Success = true;
                res.SoundEffectKey = "paper";
                res.MainMessage = $"نجح المستشارون في توفير وثائق ومخطوطات قديمة تثبت حقك الشرعي في حكم {provinceName}. يمكنك الآن إعلان حرب مبررة.";
            }
            else
            {
                neighbor.Opinion -= 30;
                res.Success = true;
                res.MainMessage = $"فشلت محاولة تزوير الوثائق، واُكتشفت الفضيحة! انخفضت العلاقات الدبلوماسية مع {neighbor.Name}.";
            }
            return res;
        }

        public static string GenerateContextualEvent(GameState state)
        {
            int roll = rand.Next(1, 101);
            if (roll > 30) return ""; // 30% chance for an event per turn

            if (state.Food < 100)
            {
                int satisfactionLoss = rand.Next(10, 20);
                state.Satisfaction = Math.Max(0, state.Satisfaction - satisfactionLoss);
                return $"🎲 حدث مفاجئ: نقص المؤونة الحاد أدى إلى أعمال شغب في العاصمة! انخفض رضا الشعب بمقدار {satisfactionLoss}.";
            }
            
            if (state.Gold < 50)
            {
                int goldGain = rand.Next(100, 200);
                state.Gold += goldGain;
                state.Prestige -= 10;
                return $"🎲 حدث مفاجئ: قدم لك أحد التجار الكبار قرضاً بقيمة {goldGain} ذهباً لتفادي الإفلاس، لكن ذلك قلل من هيبتك بين النبلاء.";
            }

            if (state.ActiveWar != null && state.ActiveWar.Turns > 3)
            {
                state.Satisfaction = Math.Max(0, state.Satisfaction - 5);
                return "🎲 حدث مفاجئ: طول مدة الحرب أرهق الشعب وبدأ الناس يطالبون بعودة أبنائهم من الجبهة. انخفض رضا الشعب.";
            }

            // Default positive event
            int prestigeGain = rand.Next(5, 15);
            state.Prestige += prestigeGain;
            return $"🎲 حدث مفاجئ: أقيم مهرجان شعبي ناجح في المدينة! زادت هيبتك ومحبة الناس لك.";
        }
    }
}
