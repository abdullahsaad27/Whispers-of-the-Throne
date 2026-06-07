using System;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class ReligionSystem
    {
        public static GameActionResult RequestWarBlessing(GameState state, string targetKingdom)
        {
            var res = new GameActionResult { Title = "طلب مباركة الحرب" };
            if (state.HeadCleric == null) state.HeadCleric = new Cleric();
            
            if (state.HeadCleric.OpinionOfKing < 30)
            {
                res.Success = false;
                state.ReligiousLegitimacy -= 5;
                state.Prestige -= 10;
                res.MainMessage = $"رفض {state.HeadCleric.Name} مباركة الحرب ضد {targetKingdom} واعتبرها حرباً غير عادلة! انخفضت شرعيتك الدينية.";
                return res;
            }

            res.Success = true;
            state.ReligiousLegitimacy += 10;
            state.Prestige += 20;
            state.HeadCleric.OpinionOfKing -= 10; // Exhausts some political capital
            res.MainMessage = $"بارك {state.HeadCleric.Name} مساعيك لغزو {targetKingdom} معتبراً إياها حرباً مقدسة وعادلة. ارتفعت هيبتك وشرعيتك الدينية!";
            return res;
        }

        public static GameActionResult SupportHeir(GameState state)
        {
            var res = new GameActionResult { Title = "دعم رجل الدين للوريث" };
            if (state.HeadCleric == null) state.HeadCleric = new Cleric();
            
            if (string.IsNullOrEmpty(state.HeirName))
            {
                res.Success = false;
                res.MainMessage = "ليس لديك وريث معين بعد لطلب دعمه!";
                return res;
            }

            if (state.Gold < 150)
            {
                res.Success = false;
                res.MainMessage = "رجل الدين يطلب 150 ذهب لتوزيع الصدقات باسم الوريث كشرط للدعم!";
                return res;
            }

            state.Gold -= 150;
            state.ReligiousLegitimacy += 5;
            state.HeadCleric.OpinionOfKing += 5;
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -150);
            res.MainMessage = $"دفعنا 150 ذهب كصدقات. أعلن {state.HeadCleric.Name} في الصلوات أن {state.HeirName} هو خليفتك الشرعي، مما قلل من خطر فصائل العرش.";
            return res;
        }

        public static GameActionResult SupportPoor(GameState state)
        {
            var res = new GameActionResult { Title = "دعم الفقراء" };
            if (state.Gold < 200)
            {
                res.Success = false;
                res.MainMessage = "لا تملك 200 ذهب لدعم الفقراء!";
                return res;
            }

            state.Gold -= 200;
            state.Satisfaction = Math.Min(100, state.Satisfaction + 15);
            state.ReligiousLegitimacy = Math.Min(100, state.ReligiousLegitimacy + 20);
            state.ReligiousTension = Math.Max(0, state.ReligiousTension - 30);
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -200);
            res.MainMessage = $"تم توزيع 200 ذهب على الفقراء والمحتاجين. ارتفع رضا الشعب بشكل ملحوظ وانخفض التوتر الديني!";
            return res;
        }
        
        public static GameActionResult FundReligiousInstitution(GameState state)
        {
            var res = new GameActionResult { Title = "تمويل مؤسسة دينية" };
            if (state.Gold < 500)
            {
                res.Success = false;
                res.MainMessage = "لا تملك 500 ذهب لتمويل بناء مؤسسة دينية كبرى!";
                return res;
            }

            state.Gold -= 500;
            state.ReligiousLegitimacy = Math.Min(100, state.ReligiousLegitimacy + 30);
            if (state.HeadCleric != null) state.HeadCleric.OpinionOfKing = Math.Min(100, state.HeadCleric.OpinionOfKing + 40);
            state.Prestige += 50;

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -500);
            res.MainMessage = "بدأ بناء مؤسسة دينية ضخمة بتمويل 500 ذهب. رجل الدين مسرور جداً وشرعيتك الدينية تعانق السماء!";
            return res;
        }
    }
}
