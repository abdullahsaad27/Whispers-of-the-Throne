using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class ArmyCommandSystem
    {
        public static GameActionResult SendArmy(GameState state, string armyId, string targetProv)
        {
            var res = new GameActionResult { Success = false, Title = "أوامر عسكرية" };
            var army = state.Armies.FirstOrDefault(a => a.Id == armyId);
            if (army != null)
            {
                army.CurrentOrder = "MoveToProvince";
                army.DestinationProvince = targetProv;
                army.DaysToDestination = 5;
                res.Success = true;
                res.MainMessage = $"تم إصدار الأمر لجيش {army.Name} بالتحرك إلى {targetProv}. سيستغرق {army.DaysToDestination} أيام.";
            }
            return res;
        }

        public static GameActionResult PrepareProvinceDefense(GameState state, string provinceIdOrName, int goldCost = 100, int soldiers = 120)
        {
            var res = new GameActionResult { Title = "تحضير دفاع المقاطعة" };
            var province = state.Provinces.FirstOrDefault(p => p.Id == provinceIdOrName || p.Name == provinceIdOrName);
            if (province == null)
            {
                res.Success = false;
                res.MainMessage = "المقاطعة المطلوبة غير موجودة.";
                return res;
            }

            if (state.Gold < goldCost)
            {
                res.Success = false;
                res.MainMessage = $"الذهب لا يكفي لتحصين {province.Name}. المطلوب {goldCost} ذهب.";
                return res;
            }

            state.Gold -= goldCost;
            province.LocalGarrison += soldiers;
            province.Satisfaction = Math.Min(100, province.Satisfaction + 2);

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -goldCost);
            res.MainMessage = $"تم تجهيز دفاعات {province.Name} وإضافة {soldiers} مقاتلاً للحامية. شعر أهل المقاطعة أن الطريق إلى مدينتهم لم يعد مكشوفاً.";
            res.SoundEffectKey = "sword";
            return res;
        }

        public static string ProcessDailyArmy(GameState state)
        {
            string events = "";
            foreach (var army in state.Armies.ToList())
            {
                if (army.CurrentOrder == "MoveToProvince" && army.DaysToDestination > 0)
                {
                    army.DaysToDestination--;
                    if (army.DaysToDestination <= 0)
                    {
                        army.CurrentProvince = army.DestinationProvince;
                        army.CurrentOrder = "Idle";
                        army.DestinationProvince = null;
                        
                        // Check if the destination is hostile or has enemy armies.
                        // Simplified check: if destination is part of the active war target, or another condition
                        bool isHostile = false;
                        if (state.ActiveWar != null && state.ActiveWar.TargetProvince == army.CurrentProvince)
                        {
                            isHostile = true;
                        }

                        if (isHostile)
                        {
                            events += $"وصل جيش {army.Name} إلى {army.CurrentProvince} وبدأت معركة!\n";
                        }
                    }
                }
            }
            
            // Process Reinforcements
            foreach (var r in state.Reinforcements.ToList())
            {
                if (r.DaysRemaining > 0)
                {
                    r.DaysRemaining--;
                    if (r.DaysRemaining <= 0)
                    {
                        var targetArmy = state.Armies.FirstOrDefault(a => a.Id == r.TargetArmyId);
                        if (targetArmy != null)
                        {
                            targetArmy.TotalSoldiers += r.Soldiers;
                            targetArmy.Supply += r.Food;
                        }
                        state.Reinforcements.Remove(r);
                    }
                }
            }
            
            return events.Trim();
        }

        public static bool MergeArmies(GameState state, string army1Id, string army2Id)
        {
            var army1 = state.Armies.FirstOrDefault(a => a.Id == army1Id);
            var army2 = state.Armies.FirstOrDefault(a => a.Id == army2Id);

            if (army1 != null && army2 != null && army1.CurrentProvince == army2.CurrentProvince)
            {
                army1.TotalSoldiers += army2.TotalSoldiers;
                army1.Supply = (army1.Supply + army2.Supply) / 2;
                state.Armies.Remove(army2);
                return true;
            }
            return false;
        }
    }
}
