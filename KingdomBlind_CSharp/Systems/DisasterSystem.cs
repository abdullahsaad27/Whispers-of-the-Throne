using System;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class DisasterSystem
    {
        private static Random _rand = new Random();
        public static GameActionResult ProcessDailyDisasters(GameState state)
        {
            var result = new GameActionResult { Success = true, Title = "الكوارث الطبيعية والأزمات" };
            StringBuilder report = new StringBuilder();
            bool hasReport = false;
            bool shouldPause = false;

            // 1. Process active disasters
            for (int i = state.ActiveDisasters.Count - 1; i >= 0; i--)
            {
                var disaster = state.ActiveDisasters[i];
                disaster.DaysRemaining -= 1;
                
                var prov = state.Provinces.FirstOrDefault(p => p.Name == disaster.ProvinceName);
                if (prov != null)
                {
                    // Apply daily penalties to province (optional if it's already applied continuously in EconomySystem)
                    // We'll apply it per month or per week to avoid overwhelming the player.
                    // Actually, if we just reduce the Satisfaction here directly over time, it could drop fast.
                    // So let's only reduce satisfaction periodically or just let EconomySystem read ActiveDisasters.
                }

                if (disaster.DaysRemaining <= 0)
                {
                    state.ActiveDisasters.RemoveAt(i);
                    report.AppendLine($"الحمد لله! انتهت أزمة {disaster.Name} في مقاطعة {disaster.ProvinceName}. ستعود الحياة لطبيعتها تدريجياً.");
                    hasReport = true;
                }
            }

            // 2. Chance to spawn a new disaster (roughly once a year per province, so very low daily chance)
            // 1 / 1000 chance per day to have some disaster somewhere
            if (_rand.Next(1000) < 3 && state.Provinces.Count > 0)
            {
                var randomProv = state.Provinces[_rand.Next(state.Provinces.Count)];
                
                // Ensure the province doesn't already have a disaster
                if (!state.ActiveDisasters.Any(d => d.ProvinceName == randomProv.Name))
                {
                    Disaster newDisaster = null;
                    int disasterType = _rand.Next(3);
                    
                    if (disasterType == 0)
                    {
                        newDisaster = new Disaster 
                        { 
                            Name = "الجفاف والمجاعة", 
                            ProvinceId = randomProv.Id, 
                            ProvinceName = randomProv.Name, 
                            DaysRemaining = _rand.Next(60, 120),
                            IncomePenalty = 50,
                            SatisfactionPenalty = 20
                        };
                    }
                    else if (disasterType == 1)
                    {
                        newDisaster = new Disaster 
                        { 
                            Name = "طاعون محلي", 
                            ProvinceId = randomProv.Id, 
                            ProvinceName = randomProv.Name, 
                            DaysRemaining = _rand.Next(30, 90),
                            IncomePenalty = 30,
                            SatisfactionPenalty = 40
                        };
                    }
                    else
                    {
                        newDisaster = new Disaster 
                        { 
                            Name = "فيضانات مدمرة", 
                            ProvinceId = randomProv.Id, 
                            ProvinceName = randomProv.Name, 
                            DaysRemaining = _rand.Next(15, 45),
                            IncomePenalty = 70,
                            SatisfactionPenalty = 10
                        };
                    }

                    if (newDisaster != null)
                    {
                        state.ActiveDisasters.Add(newDisaster);
                        report.AppendLine($"تنبيه عاجل: لقد ضربت كارثة {newDisaster.Name} مقاطعة {newDisaster.ProvinceName}! هذا سيؤثر على الدخل والرضا الشعبي لعدة أسابيع.");
                        hasReport = true;
                        shouldPause = true;
                    }
                }
            }

            if (hasReport)
            {
                result.ShouldNarrate = true;
                result.ShouldPauseTime = shouldPause;
                if (shouldPause) result.SoundEffectKey = "beacon"; // Could use a new alert sound
                result.MainMessage = report.ToString().Trim();
            }

            return result;
        }

        // Methods to relieve disaster
        public static GameActionResult ProvideRelief(GameState state, string disasterId, int goldAmount)
        {
            var disaster = state.ActiveDisasters.FirstOrDefault(d => d.Id == disasterId);
            if (disaster == null)
            {
                return new GameActionResult { Success = false, MainMessage = "الكارثة غير موجودة أو قد انتهت." };
            }

            if (state.Gold < goldAmount)
            {
                return new GameActionResult { Success = false, MainMessage = "لا يوجد ذهب كافٍ لتقديم الإغاثة." };
            }

            state.Gold -= goldAmount;
            
            // Relieving reduces days remaining
            int daysReduced = goldAmount / 5;
            disaster.DaysRemaining -= daysReduced;
            
            if (disaster.DaysRemaining <= 0)
            {
                state.ActiveDisasters.Remove(disaster);
                return new GameActionResult { Success = true, MainMessage = $"تم إرسال {goldAmount} ذهب كإغاثة لمقاطعة {disaster.ProvinceName}. بفضل جهودك، انتهت كارثة {disaster.Name} مبكراً!" };
            }
            
            return new GameActionResult { Success = true, MainMessage = $"تم إرسال {goldAmount} ذهب كإغاثة لمقاطعة {disaster.ProvinceName}. لقد قلل ذلك من مدة أزمة {disaster.Name} بمقدار {daysReduced} يوماً." };
        }
    }
}
