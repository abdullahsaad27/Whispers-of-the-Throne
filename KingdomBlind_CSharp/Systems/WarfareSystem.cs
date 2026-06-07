using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class WarfareSystem
    {
        public static GameActionResult DeclareWar(GameState state, int neighborIndex, bool forceUnjustWar)
        {
            var res = new GameActionResult { Title = "إعلان الحرب" };
            if (neighborIndex < 0 || neighborIndex >= state.Neighbors.Count)
            {
                res.Success = false; res.MainMessage = "هدف غير صالح."; return res;
            }

            var neighbor = state.Neighbors[neighborIndex];
            var warPermission = DiplomacySystem.CanDeclareWar(state, neighbor.Id);
            if (!warPermission.CanDeclare)
            {
                res.Success = false;
                res.MainMessage = warPermission.Reason;
                return res;
            }

            int totalArmy = state.Armies.Sum(a => a.TotalSoldiers);
            if (totalArmy < 100)
            {
                res.Success = false; res.MainMessage = "جيوشك صغيرة جداً لإعلان الحرب."; return res;
            }
                
            if (!neighbor.HasClaim && !forceUnjustWar)
            {
                res.Success = false; 
                res.MainMessage = $"لا تملك مطالبة شرعية لأراضي {neighbor.Name}. هل تريد إعلان حرب ظالمة رغم عقوباتها؟"; 
                return res;
            }

            if (string.IsNullOrEmpty(neighbor.ClaimedProvince))
            {
                // Auto select a province if it's an unjust war without a specific claim
                if (neighbor.ClaimableProvinces.Count > 0)
                    neighbor.ClaimedProvince = neighbor.ClaimableProvinces[0].Name;
                else
                {
                    res.Success = false; res.MainMessage = "لا يوجد أراضٍ يمكن غزوها في هذه الدولة."; return res;
                }
            }

            // Apply costs
            if (!neighbor.HasClaim && forceUnjustWar)
            {
                state.Prestige -= 50;
                state.Piety -= 30;
                state.Satisfaction -= 20;
                res.Warnings.Add("تم إعلان حرب ظالمة! انخفضت الهيبة، الرضا، والتقوى بشكل كبير.");
            }
            else
            {
                state.Prestige -= 10; // normal war cost
            }

            neighbor.Relation = "حرب";
            neighbor.Opinion = -100;
            neighbor.IsAtWarWithPlayer = true;
            
            var targetProv = neighbor.ClaimableProvinces.Find(p => p.Name == neighbor.ClaimedProvince);
            int garrisonSize = targetProv != null ? targetProv.Garrison : 50;

            state.ActiveWar = new ActiveWar
            {
                Type = "conquest",
                NeighborIdx = neighborIndex,
                TargetProvince = neighbor.ClaimedProvince,
                Garrison = garrisonSize,
                Turns = 0,
                AllyCalled = false
            };
            state.CurrentWarGoal = new WarGoal
            {
                Type = neighbor.HasClaim ? WarGoalType.Claim : WarGoalType.Conquest,
                TargetProvince = neighbor.ClaimedProvince,
                TargetKingdomId = neighbor.Id,
                TargetKingdomName = neighbor.Name,
                WarScore = 0
            };
            
            state.SiegeData = new SiegeData
            {
                TargetName = neighbor.ClaimedProvince,
                TargetGarrison = garrisonSize,
                PlayerArmy = totalArmy, // Will be ignored in ProcessDailySieges
                Turns = 0
            };
            
            res.Success = true;
            res.MainMessage = $"قرعنا طبول الحرب! أعلنت الحرب على {neighbor.Name} للسيطرة على {neighbor.ClaimedProvince}. توجهت قواتك لبدء الحصار.";
            res.SoundEffectKey = "sword";
            return res;
        }

        public static GameActionResult ProcessSiegeCommand(GameState state, string command)
        {
            var res = new GameActionResult { Title = "إدارة الحصار" };
            if (state.ActiveWar == null || state.SiegeData == null)
            {
                res.Success = false; res.MainMessage = "لا يوجد حصار نشط."; return res;
            }

            var siege = state.SiegeData;
            int totalBesiegingSoldiers = state.Armies.Where(a => a.CurrentProvince == siege.TargetName).Sum(a => a.TotalSoldiers);

            if (totalBesiegingSoldiers == 0 && command == "اقتحام")
            {
                res.Success = false; res.MainMessage = "لا يوجد جيوش لك في المحافظة لاقتحامها!";
                return res;
            }

            Random rnd = new Random();

            if (command == "اقتحام")
            {
                int playerLoss = rnd.Next(30, 80);
                int enemyLoss = rnd.Next(40, 100) + (totalBesiegingSoldiers / 100);
                
                // Distribute playerLoss across besieging armies
                int remainingLoss = playerLoss;
                foreach(var army in state.Armies.ToList())
                {
                    if (army.CurrentProvince == siege.TargetName)
                    {
                        if (army.TotalSoldiers >= remainingLoss)
                        {
                            army.TotalSoldiers -= remainingLoss;
                            remainingLoss = 0;
                            break;
                        }
                        else
                        {
                            remainingLoss -= army.TotalSoldiers;
                            army.TotalSoldiers = 0;
                        }
                    }
                }
                
                state.Armies.RemoveAll(a => a.TotalSoldiers <= 0);
                
                siege.TargetGarrison = Math.Max(0, siege.TargetGarrison - enemyLoss);
                
                res.Success = true;
                res.SoundEffectKey = "sword";
                res.MainMessage = $"أمرت قواتك باقتحام أسوار {siege.TargetName}! قتال عنيف في الأزقة. خسرنا {playerLoss} جندي، وقتلنا {enemyLoss} من العدو.";
                
                CheckSiegeEnd(state, res);
                return res;
            }
            else if (command == "انسحاب")
            {
                ResolveWarDefeat(state);
                res.Success = true;
                res.MainMessage = "أمرت قواتك بفك الحصار والانسحاب تكتيكياً. خسرت الحرب وانخفضت هيبتك.";
                return res;
            }
            else if (command == "بناء_منجنيق")
            {
                int costGold = 50;
                int costSilver = 100;
                if (state.Gold >= costGold && state.SilverCoins >= costSilver)
                {
                    state.Gold -= costGold;
                    state.SilverCoins -= costSilver;
                    siege.Catapults += 1;
                    res.Success = true;
                    res.SoundEffectKey = "build";
                    res.MainMessage = $"تم بناء منجنيق جديد لضرب أسوار {siege.TargetName}! العدد الحالي لآلات الحصار: {siege.Catapults}";
                }
                else
                {
                    res.Success = false; res.MainMessage = "لا تملك الذهب أو الفضة الكافية لبناء المنجنيق (يحتاج 50 ذهب و 100 فضة).";
                }
                return res;
            }
            else if (command == "تخريب")
            {
                int costGold = 100;
                if (state.Gold >= costGold)
                {
                    state.Gold -= costGold;
                    int poisoned = rnd.Next(20, 60);
                    siege.TargetGarrison = Math.Max(0, siege.TargetGarrison - poisoned);
                    res.Success = true;
                    res.MainMessage = $"نجح رجال الاستخبارات في التسلل ليلاً وتسميم آبار المياه وتخريب الأسوار! مات {poisoned} من حامية العدو وتضعضعت دفاعاتهم.";
                    CheckSiegeEnd(state, res);
                }
                else
                {
                    res.Success = false; res.MainMessage = "تحتاج إلى 100 ذهب لتمويل عملية التخريب الاستخباراتية.";
                }
                return res;
            }
            else if (command == "دفاع_مباغت")
            {
                if (siege.SortieExpected)
                {
                    siege.SortieExpected = false;
                    int enemyKilled = rnd.Next(30, 80);
                    siege.TargetGarrison = Math.Max(0, siege.TargetGarrison - enemyKilled);
                    res.Success = true;
                    res.SoundEffectKey = "sword";
                    res.MainMessage = $"نصبنا كميناً محكماً بناءً على تقارير الجواسيس! وقع العدو في الفخ وخسر {enemyKilled} جندياً أثناء محاولتهم التسلل لحرق خيامنا.";
                    CheckSiegeEnd(state, res);
                }
                else
                {
                    res.Success = false; res.MainMessage = "لا يوجد هجوم مباغت متوقع من العدو لتجهيز الدفاع ضده!";
                }
                return res;
            }

            res.Success = false;
            res.MainMessage = "أمر غير معروف.";
            return res;
        }

        public static string ProcessDailySieges(GameState state)
        {
            if (state.ActiveWar == null || state.SiegeData == null)
                return "";

            var siege = state.SiegeData;
            int totalBesiegingSoldiers = state.Armies.Where(a => a.CurrentProvince == siege.TargetName).Sum(a => a.TotalSoldiers);

            state.ActiveWar.Turns += 1;
            siege.Turns += 1;

            if (totalBesiegingSoldiers == 0)
            {
                return $"لا يوجد جيوش لك في {siege.TargetName} لمواصلة الحصار. أرسل جيشاً إلى هناك!";
            }
            
            // Winter Attrition (Months 11, 12, 1)
            bool isWinter = (state.Time.Month == 11 || state.Time.Month == 12 || state.Time.Month == 1);
            int attritionMultiplier = isWinter ? 3 : 1;

            state.Gold -= 20 * attritionMultiplier;
            state.Food -= 30 * attritionMultiplier;
            
            Random rnd = new Random();

            // Mercenary Betrayal / Bankruptcy check
            if (state.Gold <= 0 && state.SilverCoins <= 0 && totalBesiegingSoldiers > 0)
            {
                if (rnd.Next(100) < 5) // 5% chance daily if broke
                {
                    int desertion = totalBesiegingSoldiers / 4;
                    foreach(var army in state.Armies.Where(a => a.CurrentProvince == siege.TargetName).ToList())
                    {
                        army.TotalSoldiers = Math.Max(0, army.TotalSoldiers - desertion);
                    }
                    return $"⚠️ كارثة في الحصار! نفدت أموال الخزينة بالكامل، وتمرد المرتزقة في جيشك وغادروا أرض المعركة! فقدنا {desertion} جندي.";
                }
            }

            // Enemy Sortie Execution
            if (siege.SortieExpected)
            {
                int surpriseLoss = rnd.Next(50, 150) + (totalBesiegingSoldiers / 10);
                foreach(var army in state.Armies.Where(a => a.CurrentProvince == siege.TargetName).ToList())
                {
                    army.TotalSoldiers = Math.Max(0, army.TotalSoldiers - surpriseLoss);
                }
                siege.SortieExpected = false; // Reset
                state.Prestige -= 5;
                return $"🔥 هجوم مباغت! خرجت حامية {siege.TargetName} في الليل وأحرقت خيام قواتك! خسرنا {surpriseLoss} جندي وتراجعت المعنويات.";
            }

            // Enemy Sortie Setup (Spy Network Warning)
            if (rnd.Next(100) < 3 && !siege.SortieExpected && siege.Turns > 5) // 3% chance
            {
                siege.SortieExpected = true;
                bool hasSpyNetwork = state.SpyNetworks.Any(s => s.TargetId == siege.TargetName) || (state.MinisterBudgets != null && state.MinisterBudgets.ContainsKey("spymaster") && state.MinisterBudgets["spymaster"] >= 25);
                
                if (hasSpyNetwork)
                {
                    return $"👁️ تقرير استخباراتي عاجل: الجواسيس في {siege.TargetName} يؤكدون أن الحامية تخطط لهجوم مباغت الليلة القادمة! استعد للدفاع فوراً!";
                }
            }
            
            int baseLoss = rnd.Next(1, 5);
            // Catapults add +5 to daily loss each
            int enemyLoss = baseLoss + (totalBesiegingSoldiers / 200) + (siege.Catapults * 5);
            
            siege.TargetGarrison = Math.Max(0, siege.TargetGarrison - enemyLoss);
            UpdateWarScore(state);

            if (state.ActiveWar.Turns > 5)
            {
                state.Satisfaction = Math.Max(0, state.Satisfaction - 1); 
            }

            string seasonStr = isWinter ? " (شتاء قارس يزيد من استهلاك المؤن)" : "";
            string report = $"تحديث الحصار{seasonStr}: قواتك ({totalBesiegingSoldiers} جندي) تفرض حصاراً، مدعومة بـ {siege.Catapults} منجنيق. الحامية فقدت {enemyLoss} جندي.";

            var dummyRes = new GameActionResult();
            if (CheckSiegeEnd(state, dummyRes))
            {
                report += "\n" + dummyRes.MainMessage;
            }

            return report;
        }

        public static GameActionResult NegotiatePeace(GameState state, string peaceType)
        {
            var res = new GameActionResult { Title = "مفاوضات السلام" };
            if (state.ActiveWar == null)
            {
                res.Success = false;
                res.MainMessage = "لا توجد حرب قائمة للتفاوض حولها.";
                return res;
            }

            UpdateWarScore(state);
            int score = state.ActiveWar.WarScore;
            var enemy = state.Neighbors[state.ActiveWar.NeighborIdx];

            if (peaceType == "EnforceDemands")
            {
                if (score < 75)
                {
                    res.Success = false;
                    res.MainMessage = $"نتيجة الحرب الحالية {score}. تحتاج إلى 75 لفرض المطالب.";
                    return res;
                }

                res.Success = true;
                res.MainMessage = $"قبلت {enemy.Name} فرض مطالبك بسبب تفوقك العسكري. انتهت الحرب لصالحك.";
                ResolveWarVictory(state, res);
                return res;
            }

            if (peaceType == "WhitePeace")
            {
                if (score < -10)
                {
                    res.Success = false;
                    res.MainMessage = $"العدو يرفض الصلح الأبيض لأن نتيجة الحرب ضدك ({score}).";
                    return res;
                }

                EndWarWithoutTerritoryChange(state, enemy);
                state.Prestige = Math.Max(0, state.Prestige - 5);
                res.Success = true;
                res.MainMessage = $"تم توقيع صلح أبيض مع {enemy.Name}. لا أحد يربح أرضاً، لكن الحرب انتهت.";
                return res;
            }

            if (peaceType == "PayReparations")
            {
                int cost = Math.Max(100, 300 - score * 2);
                if (state.Gold < cost)
                {
                    res.Success = false;
                    res.MainMessage = $"تحتاج إلى {cost} ذهب لدفع تعويضات وإنهاء الحرب.";
                    return res;
                }

                state.Gold -= cost;
                state.Prestige = Math.Max(0, state.Prestige - 20);
                EndWarWithoutTerritoryChange(state, enemy);
                res.Success = true;
                res.ResourceChanges.Add("الذهب", -cost);
                res.ResourceChanges.Add("الهيبة", -20);
                res.MainMessage = $"دفعت تعويضات إلى {enemy.Name} وأنهيت الحرب قبل أن تستنزف المملكة أكثر.";
                return res;
            }

            res.Success = false;
            res.MainMessage = "عرض سلام غير معروف.";
            return res;
        }

        public static GameActionResult SuppressRebellion(GameState state, string enemyArmyId)
        {
            var res = new GameActionResult { Title = "قمع التمرد" };
            var rebelArmy = state.EnemyArmies.FirstOrDefault(a => a.Id == enemyArmyId);
            if (rebelArmy == null)
            {
                res.Success = false;
                res.MainMessage = "لا يوجد جيش متمرد بهذا المعرف.";
                return res;
            }

            var royalArmy = state.Armies
                .Where(a => a.CurrentProvince == rebelArmy.CurrentProvince)
                .OrderByDescending(a => a.TotalSoldiers)
                .FirstOrDefault();

            if (royalArmy == null)
            {
                res.Success = false;
                res.MainMessage = $"لا يوجد جيش ملكي في {rebelArmy.CurrentProvince}. حرّك جيشاً إلى المقاطعة قبل محاولة القمع.";
                return res;
            }

            if (royalArmy.TotalSoldiers < Math.Max(100, rebelArmy.TotalSoldiers * 3 / 4))
            {
                int losses = Math.Min(royalArmy.TotalSoldiers / 3, Math.Max(20, rebelArmy.TotalSoldiers / 5));
                royalArmy.TotalSoldiers = Math.Max(0, royalArmy.TotalSoldiers - losses);
                res.Success = false;
                res.MainMessage = $"قوات {royalArmy.Name} غير كافية لقمع تمرد {rebelArmy.CurrentProvince}. خسرنا {losses} جندياً في اشتباك حدودي.";
                res.Warnings.Add("أرسل تعزيزات أو فاوض المتمردين لتجنب اتساع التمرد.");
                return res;
            }

            int royalLoss = Math.Max(20, rebelArmy.TotalSoldiers / 5);
            royalArmy.TotalSoldiers = Math.Max(0, royalArmy.TotalSoldiers - royalLoss);
            state.EnemyArmies.Remove(rebelArmy);
            CompleteRebellion(state, rebelArmy.CurrentProvince, negotiated: false);

            res.Success = true;
            res.MainMessage = $"تم قمع التمرد في {rebelArmy.CurrentProvince}. خسر الجيش الملكي {royalLoss} جندياً، وانتهت السيطرة المتمردة على المقاطعة.";
            res.ResourceChanges.Add("الجنود", -royalLoss);
            res.SoundEffectKey = "sword";
            LivingRealmSystem.AdjustRoyalReputation(state, "Cruel", 4);
            LivingRealmSystem.AdjustRoyalReputation(state, "Warrior", 4);
            return res;
        }

        public static GameActionResult NegotiateRebellion(GameState state, string enemyArmyId)
        {
            var res = new GameActionResult { Title = "عفو وتفاوض مع المتمردين" };
            var rebelArmy = state.EnemyArmies.FirstOrDefault(a => a.Id == enemyArmyId);
            if (rebelArmy == null)
            {
                res.Success = false;
                res.MainMessage = "لا يوجد جيش متمرد بهذا المعرف.";
                return res;
            }

            const int goldCost = 100;
            const int prestigeCost = 25;
            if (state.Gold < goldCost)
            {
                res.Success = false;
                res.MainMessage = $"تحتاج إلى {goldCost} ذهب لتقديم عفو وتعويضات سياسية للمتمردين.";
                return res;
            }

            state.Gold -= goldCost;
            state.Prestige -= prestigeCost;
            state.Satisfaction = Math.Min(100, state.Satisfaction + 3);
            state.EnemyArmies.Remove(rebelArmy);
            CompleteRebellion(state, rebelArmy.CurrentProvince, negotiated: true);

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -goldCost);
            res.ResourceChanges.Add("الهيبة", -prestigeCost);
            res.MainMessage = $"قبل متمردو {rebelArmy.CurrentProvince} العفو والتفاوض. انتهى التمرد، لكن البلاط يرى ذلك تنازلاً سياسياً مكلفاً.";
            LivingRealmSystem.AdjustRoyalReputation(state, "Just", 4);
            return res;
        }

        private static bool CheckSiegeEnd(GameState state, GameActionResult res)
        {
            var siege = state.SiegeData;
            if (siege == null) return false;

            int totalBesiegingSoldiers = state.Armies.Where(a => a.CurrentProvince == siege.TargetName).Sum(a => a.TotalSoldiers);

            if (siege.TargetGarrison <= 0)
            {
                res.MainMessage += $"\n🏰 انتصار! استسلمت حامية {siege.TargetName} وسيطرت قواتك عليها. انتهت الحرب بانتصارك.";
                ResolveWarVictory(state, res);
                return true;
            }
            else if (totalBesiegingSoldiers <= 0)
            {
                res.MainMessage += $"\n💀 هزيمة فادحة! لم يعد لديك قوات تحاصر {siege.TargetName}.";
                // Don't auto-defeat immediately just because armies died, they can send reinforcements
                // but the old logic did auto-defeat. We will keep it but change the text.
                // ResolveWarDefeat(state);
                // return true;
            }
            return false;
        }
        
        private static void ResolveWarVictory(GameState state, GameActionResult res)
        {
            var enemy = state.Neighbors[state.ActiveWar.NeighborIdx];
            var targetProv = enemy.ClaimableProvinces.Find(p => p.Name == state.ActiveWar.TargetProvince);
            
            if (targetProv != null)
            {
                state.Provinces.Add(new Province
                {
                    Name = targetProv.Name,
                    Vassal = "غير معين",
                    VassalReligion = "سُني أشعري",
                    Income = targetProv.Income,
                    Garrison = 20,
                    Satisfaction = 50,
                    Opinion = 0,
                    Religion = targetProv.Religion,
                    Minorities = targetProv.Minorities,
                    HolySite = null,
                    Occupied = false,
                    OccupiedBy = null,
                    HasRevocationReason = false
                });
                
                enemy.ClaimableProvinces.Remove(targetProv);
            }
            if (enemy.ClaimableProvinces.Count == 0)
            {
                res.ShowAnnexationMenu = true;
                res.AnnexedNeighborIdx = state.ActiveWar.NeighborIdx;
            }
            // RANSOM CHANCE
            if (new Random().Next(100) < 30) // 30% chance
            {
                state.Gold += 1000;
                state.Prestige += 20;
                // Since this runs within CheckSiegeEnd which modifies dummyRes, the UI will just see the extra Gold.
                // We don't have a direct way to append to the message here unless we pass 'res', but we can just use the TurnWarnings to notify.
                state.TurnWarnings.Add("🎉 خبر عظيم: أثناء السيطرة على المدينة، تم أسر نبيل من العائلة الحاكمة للعدو، وتم دفع فدية ضخمة قدرها 1000 ذهب لخزينتك!");
            }

            state.Prestige += 50;
            enemy.Relation = "هدنة";
            enemy.IsAtWarWithPlayer = false;
            enemy.HasClaim = false;
            enemy.ClaimedProvince = null;
            
            state.ActiveWar = null;
            state.SiegeData = null;
            state.CurrentWarGoal = null;
            DiplomacySystem.SynchronizeDiplomacyState(state);
        }
        
        private static void ResolveWarDefeat(GameState state)
        {
            var enemy = state.Neighbors[state.ActiveWar.NeighborIdx];
            
            state.Prestige -= 50;
            state.Gold = Math.Max(0, state.Gold - 200); // reparations
            enemy.Relation = "هدنة";
            enemy.IsAtWarWithPlayer = false;
            enemy.HasClaim = false;
            enemy.ClaimedProvince = null;
            
            state.ActiveWar = null;
            state.SiegeData = null;
            state.CurrentWarGoal = null;
            DiplomacySystem.SynchronizeDiplomacyState(state);
        }

        private static void EndWarWithoutTerritoryChange(GameState state, Neighbor enemy)
        {
            enemy.Relation = "هدنة";
            enemy.IsAtWarWithPlayer = false;
            enemy.HasClaim = false;
            enemy.ClaimedProvince = null;
            state.ActiveWar = null;
            state.SiegeData = null;
            state.CurrentWarGoal = null;
            DiplomacySystem.SynchronizeDiplomacyState(state);
        }

        private static void UpdateWarScore(GameState state)
        {
            if (state.ActiveWar == null || state.SiegeData == null)
                return;

            int initialGarrison = Math.Max(1, state.ActiveWar.Garrison);
            int siegeProgress = Math.Clamp((initialGarrison - state.SiegeData.TargetGarrison) * 100 / initialGarrison, 0, 100);
            int attritionPenalty = Math.Min(30, state.ActiveWar.Turns / 2);
            int armyPresence = state.Armies.Any(a => a.CurrentProvince == state.SiegeData.TargetName && a.TotalSoldiers > 0) ? 10 : -25;

            state.ActiveWar.WarScore = Math.Clamp(siegeProgress + armyPresence - attritionPenalty, -100, 100);
            if (state.CurrentWarGoal != null)
                state.CurrentWarGoal.WarScore = state.ActiveWar.WarScore;
        }

        private static void CompleteRebellion(GameState state, string provinceName, bool negotiated)
        {
            var province = state.Provinces.FirstOrDefault(p => p.Name == provinceName);
            if (province != null)
            {
                province.Occupied = false;
                province.OccupiedBy = null;
                province.Satisfaction = negotiated
                    ? Math.Min(100, province.Satisfaction + 10)
                    : Math.Max(0, province.Satisfaction - 8);
            }

            var provinceGovernorIds = state.Governors
                .Where(g => g.ProvinceName == provinceName || (province != null && g.ProvinceId == province.Id))
                .Select(g => g.Id)
                .ToList();

            foreach (var governor in state.Governors.Where(g => provinceGovernorIds.Contains(g.Id)))
            {
                governor.IsRebellious = false;
                governor.Fear = Math.Min(100, governor.Fear + (negotiated ? 5 : 20));
                governor.OpinionOfKing += negotiated ? 10 : -10;
                governor.UpdateMood();
                LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, negotiated ? "RebellionPardon" : "RebellionSuppressed", negotiated ? "أنهى الملك التمرد بالعفو والتفاوض." : "قمع الملك التمرد بالقوة العسكرية.", 0, 0, 0, 3, 900, negotiated);
            }

            foreach (var faction in state.Factions)
            {
                bool touchesProvince = provinceGovernorIds.Contains(faction.LeaderGovernorId) ||
                                       faction.MemberGovernorIds.Any(id => provinceGovernorIds.Contains(id));
                if (touchesProvince)
                {
                    faction.IsActive = false;
                    faction.IsPreparingRebellion = false;
                    faction.IsRebellionStarted = false;
                    faction.Discontent = negotiated ? Math.Max(0, faction.Discontent - 20) : 0;
                }
            }
        }
    }
}
