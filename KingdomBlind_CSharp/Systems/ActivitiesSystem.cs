using System;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class ActivitiesSystem
    {
        private static Random rand = new Random();

        public static GameActionResult HoldHuntingTrip(GameState state)
        {
            var res = new GameActionResult { Title = "رحلة صيد ملكية", Success = false };

            if (state.Gold < 150)
            {
                res.MainMessage = "لا تملك الذهب الكافي لتمويل رحلة صيد (تحتاج 150 ذهب).";
                return res;
            }

            state.Gold -= 150;
            
            // Random outcomes for hunting
            int outcome = rand.Next(100);
            if (outcome < 20)
            {
                // Bad outcome
                state.RulerStress += 10;
                res.MainMessage = "رحلة الصيد كانت مخيبة للآمال. لم تصطد شيئاً وتعرضت للإرهاق. (الضغط النفسي +10)";
                res.SoundEffectKey = "wall";
            }
            else if (outcome < 80)
            {
                // Normal outcome
                state.RulerStress = Math.Max(0, state.RulerStress - 20);
                state.Prestige += 10;
                state.Food += 50;
                res.MainMessage = "رحلة صيد ناجحة! اصطدت بعض الغزلان واستمتعت بالهواء الطلق. (انخفض الضغط النفسي، وزادت الهيبة والمؤونة)";
            }
            else
            {
                // Great outcome
                state.RulerStress = 0;
                state.Prestige += 30;
                state.Food += 150;
                res.MainMessage = "رحلة صيد أسطورية! لقد اصطدت دباً ضخماً وأثبتّ شجاعتك أمام حاشيتك! (الضغط النفسي أصبح صفراً، هيبة كبيرة، ومؤونة وفيرة)";
            }

            res.Success = true;
            return res;
        }

        public static GameActionResult HoldGrandTournament(GameState state)
        {
            var res = new GameActionResult { Title = "بطولة فروسية كبرى", Success = false };

            if (state.Gold < 400)
            {
                res.MainMessage = "تحتاج 400 ذهب لإقامة بطولة فروسية تليق بمقامك.";
                return res;
            }

            state.Gold -= 400;
            state.Prestige += 100;
            
            foreach(var prov in state.Provinces)
            {
                prov.Satisfaction = Math.Min(100, prov.Satisfaction + 15);
            }
            
            res.Success = true;
            res.MainMessage = "أقيمت بطولة فروسية عظيمة حضرها النبلاء والعامة! زادت هيبتك بشدة، وارتفع رضا الشعب في كل المقاطعات لاستطاعتهم مشاهدة هذه الاحتفالات.";
            return res;
        }
    }
}
