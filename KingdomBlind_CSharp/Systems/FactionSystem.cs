using System;
using KingdomBlind_CSharp.Models;
using System.Linq;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Systems
{
    public static class FactionSystem
    {
        public static GameActionResult ProcessDailyFactions(GameState state)
        {
            var res = new GameActionResult { Title = "تحديث الفصائل اليومي", Success = true, ShouldNarrate = false };
            
            // Only do checks periodically or silently to avoid spam
            bool majorEventOccurred = false;
            string narrativeMsg = "";

            // 1. Update Governor Moods based on global state
            foreach (var gov in state.Governors)
            {
                if (state.TaxLevel == "مرتفع")
                {
                    gov.OpinionOfKing = Math.Max(-100, gov.OpinionOfKing - 1);
                }
                else if (state.TaxLevel == "منخفض")
                {
                    gov.OpinionOfKing = Math.Min(100, gov.OpinionOfKing + 1);
                }
                
                // Length of war exhausts governors
                if (state.ActiveWar != null && state.Time.Day % 5 == 0)
                {
                    gov.OpinionOfKing = Math.Max(-100, gov.OpinionOfKing - 1);
                }

                gov.UpdateMood();
            }

            // 2. Check for Faction Formation
            var angryGovs = state.Governors.Where(g => (g.CurrentMood == "Angry" || g.CurrentMood == "Opportunist") && !g.IsRebellious).ToList();
            
            // Look for existing tax faction
            var taxFaction = state.Factions.FirstOrDefault(f => f.Type == "LowerTaxes" && f.IsActive);
            
            if (state.TaxLevel == "مرتفع" && angryGovs.Count > 0)
            {
                if (taxFaction == null && angryGovs.Any(g => g.Influence > 60))
                {
                    var leader = angryGovs.OrderByDescending(g => g.Influence).First();
                    taxFaction = new Faction {
                        Name = "فصيل نبلاء الضرائب العادلة",
                        Type = "LowerTaxes",
                        LeaderGovernorId = leader.Id,
                        DemandText = "خفض الضرائب فوراً",
                        MainReason = "الضرائب المرتفعة لفترة طويلة",
                        PowerPercent = leader.MilitaryPower
                    };
                    taxFaction.MemberGovernorIds.Add(leader.Id);
                    state.Factions.Add(taxFaction);
                    
                    majorEventOccurred = true;
                    narrativeMsg += $"أحداث سياسية: أسس {leader.Name}، {taxFaction.Name}، للمطالبة بخفض الضرائب.\n";
                }
                else if (taxFaction != null)
                {
                    // Existing faction, add new angry members
                    foreach (var gov in angryGovs)
                    {
                        if (!taxFaction.MemberGovernorIds.Contains(gov.Id))
                        {
                            taxFaction.MemberGovernorIds.Add(gov.Id);
                            taxFaction.PowerPercent = Math.Min(100, taxFaction.PowerPercent + (gov.MilitaryPower / 2));
                            if (gov.Influence > 70)
                            {
                                majorEventOccurred = true;
                                narrativeMsg += $"أحداث سياسية: انضم الوالي القوي {gov.Name} إلى {taxFaction.Name}.\n";
                            }
                        }
                    }
                }
            }

            // 3. Evolve Factions
            foreach (var faction in state.Factions.Where(f => f.IsActive))
            {
                // Increase discontent if demands not met
                if (faction.Type == "LowerTaxes" && state.TaxLevel == "مرتفع")
                {
                    faction.Discontent = Math.Min(100, faction.Discontent + 2);
                }
                
                if (faction.Discontent >= 100 && faction.DaysUntilUltimatum == -1 && faction.PowerPercent > 25)
                {
                    faction.DaysUntilUltimatum = 14; // 2 weeks ultimatum
                    majorEventOccurred = true;
                    narrativeMsg += $"تحذير خطير: {faction.Name} أرسل إنذاراً نهائياً!\nالمطلب: {faction.DemandText}\nقوة الفصيل: {faction.PowerPercent}%\nلديك {faction.DaysUntilUltimatum} يوماً للاستجابة قبل التمرد المسلح.\n";
                    res.ShouldPauseTime = true; // Auto-pause time!
                }
                
                if (faction.DaysUntilUltimatum > 0)
                {
                    faction.DaysUntilUltimatum--;
                    if (faction.DaysUntilUltimatum == 0)
                    {
                        majorEventOccurred = true;
                        narrativeMsg += $"انتهت المهلة! {faction.Name} بدأ تمرداً مسلحاً ضد العرش!\n";
                        TriggerRebellion(state, faction);
                        res.ShouldPauseTime = true;
                    }
                }
            }

            if (majorEventOccurred)
            {
                res.ShouldNarrate = true;
                res.MainMessage = narrativeMsg;
            }

            return res;
        }

        public static GameActionResult HandleUltimatum(GameState state, string factionId, string actionType)
        {
            var res = new GameActionResult { Title = "الرد على الإنذار" };
            var faction = state.Factions.FirstOrDefault(f => f.Id == factionId);
            if (faction == null) { res.Success = false; return res; }

            switch (actionType)
            {
                case "Accept":
                    if (faction.Type == "LowerTaxes") state.TaxLevel = "منخفض";
                    faction.IsActive = false;
                    faction.Discontent = 0;
                    res.MainMessage = $"قبلت مطالب {faction.Name}. تم تنفيذ مطالبهم وتفكك الفصيل.";
                    state.Prestige = Math.Max(0, state.Prestige - 20);
                    break;
                case "Reject":
                    faction.DaysUntilUltimatum = 0; // Immediate rebellion
                    res.MainMessage = $"رفضت مطالب {faction.Name} بغضب. لقد أعلنوا التمرد فوراً!";
                    TriggerRebellion(state, faction);
                    break;
                case "Bribe":
                    if (state.Gold >= 500)
                    {
                        state.Gold -= 500;
                        faction.Discontent = Math.Max(0, faction.Discontent - 50);
                        faction.DaysUntilUltimatum = -1; // Reset ultimatum
                        res.MainMessage = $"تم دفع 500 ذهب لرشوة قائد الفصيل. تراجعوا عن الإنذار مؤقتاً.";
                    }
                    else
                    {
                        res.Success = false; res.MainMessage = "لا تملك الذهب الكافي للرشوة."; return res;
                    }
                    break;
            }
            res.Success = true;
            return res;
        }

        public static void TriggerRebellion(GameState state, Faction faction)
        {
            faction.IsRebellionStarted = true;
            faction.IsActive = false;
            
            // Mark governors as rebellious
            foreach(var govId in faction.MemberGovernorIds)
            {
                var gov = state.Governors.FirstOrDefault(g => g.Id == govId);
                if (gov != null)
                {
                    gov.IsRebellious = true;
                    gov.UpdateMood();
                    
                    var province = state.Provinces.FirstOrDefault(p => p.Id == gov.ProvinceId);
                    if (province != null)
                    {
                        if (state.EnemyArmies.Any(a => a.Id.StartsWith("rebel_") && a.CurrentProvince == province.Name))
                            continue;

                        // Convert province forces into an enemy army
                        int rebelForces = province.LocalGarrison + province.RecruitableLevy;
                        province.LocalGarrison = 0; // They left the garrison
                        province.Occupied = true; // Mark as contested internally
                        province.OccupiedBy = "المتمردون";
                        
                        state.EnemyArmies.Add(new Army {
                            Id = "rebel_" + Guid.NewGuid().ToString().Substring(0, 5),
                            Name = $"جيش المتمردين من {province.Name}",
                            TotalSoldiers = rebelForces,
                            CurrentProvince = province.Name,
                            Morale = 100,
                            CommanderName = gov.Name
                        });
                    }
                }
            }
        }
    }
}
