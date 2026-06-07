using System;
using KingdomBlind_CSharp.Models;
using System.Linq;

namespace KingdomBlind_CSharp.Systems
{
    public static class PoliticalSystem
    {
        public static GameActionResult SendBribe(GameState state, string governorId)
        {
            var res = new GameActionResult { Title = "رشوة سياسية" };
            var governor = state.Governors.FirstOrDefault(g => g.Id == governorId);
            
            if (governor == null)
            {
                res.Success = false;
                res.MainMessage = "لم يتم العثور على الوالي.";
                return res;
            }

            if (state.Gold < 200)
            {
                res.Success = false;
                res.MainMessage = "لا تملك ذهباً كافياً. الرشوة تكلف 200 ذهب.";
                return res;
            }

            state.Gold -= 200;
            governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 25);
            governor.Loyalty = Math.Min(100, governor.Loyalty + 10);
            governor.UpdateMood();
            LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, "Bribe", "تلقى هدية سياسية من الملك.", 0, 0, 0, 1, 360, true);
            LivingRealmSystem.AdjustRoyalReputation(state, "Generous", 2);
            
            res.Success = true;
            res.SoundEffectKey = "coin";
            res.MainMessage = $"تم إرسال هدية وذهب بقيمة 200 إلى {governor.Name}. رأيه بالملك تحسن إلى {governor.OpinionOfKing} وموقفه أصبح {GetArabicMood(governor.CurrentMood)}.";
            return res;
        }

        public static GameActionResult Threaten(GameState state, string governorId)
        {
            var res = new GameActionResult { Title = "تهديد الوالي" };
            var governor = state.Governors.FirstOrDefault(g => g.Id == governorId);
            
            if (governor == null) { res.Success = false; return res; }

            governor.Fear = Math.Min(100, governor.Fear + 30);
            governor.OpinionOfKing = Math.Max(-100, governor.OpinionOfKing - 40);
            governor.Loyalty = Math.Max(0, governor.Loyalty - 10);
            governor.UpdateMood();
            LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, "Threat", "هدده الملك بالقوة.", 0, 0, 0, 2, 540, false);
            LivingRealmSystem.AdjustRoyalReputation(state, "Cruel", 4);
            
            res.Success = true;
            res.SoundEffectKey = "sword";
            res.MainMessage = $"تم إرسال تهديد صريح إلى {governor.Name}. زاد خوفه بشكل كبير ليصل إلى {governor.Fear}، لكن كراهيته لك زادت أيضاً. موقفه الحالي: {GetArabicMood(governor.CurrentMood)}.";
            return res;
        }

        public static GameActionResult GrantTitle(GameState state, string governorId)
        {
            var res = new GameActionResult { Title = "منح لقب أو شرف" };
            var governor = state.Governors.FirstOrDefault(g => g.Id == governorId);
            
            if (governor == null) { res.Success = false; return res; }

            if (state.Prestige < 50)
            {
                res.Success = false;
                res.MainMessage = "لا تملك هيبة كافية لمنح الألقاب. يتطلب 50 هيبة.";
                return res;
            }

            state.Prestige -= 50;
            governor.Loyalty = Math.Min(100, governor.Loyalty + 30);
            governor.Influence = Math.Min(100, governor.Influence + 20);
            governor.OpinionOfKing = Math.Min(100, governor.OpinionOfKing + 20);
            governor.UpdateMood();
            LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, "GrantedTitle", "منحه الملك لقباً وشرفاً علنياً.", 0, 0, 0, 2, 540, true);
            LivingRealmSystem.AdjustRoyalReputation(state, "Just", 3);

            res.Success = true;
            res.MainMessage = $"تم منح لقب شرفي لـ {governor.Name}. زاد ولاؤه ورأيه فيك، لكن نفوذه السياسي في المملكة زاد ليصبح {governor.Influence}.";
            return res;
        }

        public static GameActionResult DismissGovernor(GameState state, string governorId)
        {
            var res = new GameActionResult { Title = "عزل الوالي" };
            var governor = state.Governors.FirstOrDefault(g => g.Id == governorId);
            
            if (governor == null) { res.Success = false; return res; }

            // Political consequences
            state.Prestige = Math.Max(0, state.Prestige - 100);
            string provinceName = governor.ProvinceName;
            
            // Angering other governors
            foreach (var g in state.Governors)
            {
                if (g.Id != governorId)
                {
                    g.OpinionOfKing = Math.Max(-100, g.OpinionOfKing - 15);
                    g.Loyalty = Math.Max(0, g.Loyalty - 10);
                    g.UpdateMood();
                    LivingRealmSystem.AddMemory(state, "Governor", g.Id, g.Name, "DismissalShock", $"تذكر عزل {governor.Name} من {provinceName}.", 0, 0, 0, 2, 720, false);
                }
            }

            state.Governors.Remove(governor);
            
            // Create a new loyal governor
            string[] fakeNames = { "الوالي العادل", "الحاكم الجديد", "الأمير الشاب", "القائد الموالي" };
            var newGov = new Governor {
                ProvinceId = governor.ProvinceId,
                ProvinceName = provinceName,
                Name = fakeNames[new Random().Next(fakeNames.Length)],
                Age = new Random().Next(25, 40),
                OpinionOfKing = 50,
                Loyalty = 80,
                Fear = 60
            };
            newGov.UpdateMood();
            state.Governors.Add(newGov);
            LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, "Dismissed", $"عزله الملك من حكم {provinceName}.", 0, 0, 0, 3, 900, false);
            LivingRealmSystem.AdjustRoyalReputation(state, "Cruel", 6);

            res.Success = true;
            res.MainMessage = $"تم عزل {governor.Name} من حكم {provinceName}. قرارك الجريء تسبب في صدمة سياسية أدت لانخفاض هيبتك وغضب النبلاء الآخرين. تم تعيين {newGov.Name} والياً جديداً.";
            return res;
        }
        
        public static string GetArabicMood(string mood)
        {
            switch (mood)
            {
                case "Loyal": return "مخلص";
                case "Angry": return "غاضب";
                case "Afraid": return "خائف";
                case "Opportunist": return "انتهازي خطير";
                case "Rebellious": return "متمرد";
                default: return "محايد";
            }
        }
    }
}
