using System;
using System.Linq;
using System.Collections.Generic;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class EconomySystem
    {
        public static GameActionResult SetTaxLevel(GameState state, string taxLevel)
        {
            var res = new GameActionResult { Title = "تغيير مستوى الضرائب" };
            if (taxLevel != "منخفض" && taxLevel != "متوسط" && taxLevel != "مرتفع")
            {
                res.Success = false;
                res.MainMessage = "مستوى ضريبة غير صالح.";
                return res;
            }

            state.TaxLevel = taxLevel;
            res.Success = true;
            res.MainMessage = $"تم تحديد مستوى الضرائب إلى: {taxLevel}. سينعكس ذلك على الدخل ورضا الشعب بداية من الشهر القادم.";
            return res;
        }

        
        public static GameActionResult UpgradeBuilding(GameState state, int provinceIndex, string buildingType)
        {
            var res = new GameActionResult { Title = "بناء أو ترقية المبنى" };
            if (provinceIndex < 0 || provinceIndex >= state.Provinces.Count)
            {
                res.Success = false; res.MainMessage = "المقاطعة غير صالحة."; return res;
            }

            var province = state.Provinces[provinceIndex];
            var existingBuilding = province.Buildings.FirstOrDefault(b => b.BuildingType == buildingType);
            int currentLevel = existingBuilding != null ? existingBuilding.Level : 0;

            int maxLevel = 1;
            if (buildingType == "سوق") maxLevel = 20;
            else if (buildingType == "ثكنة") maxLevel = 30;
            else if (buildingType == "مزرعة") maxLevel = 20;
            else if (buildingType == "منجم") maxLevel = 10;

            if (currentLevel >= maxLevel)
            {
                res.Success = false;
                res.MainMessage = $"لقد وصل هذا المبنى إلى الحد الأقصى للمستوى ({maxLevel}).";
                return res;
            }

            // Check if already in queue
            if (state.BuildingQueue.Any(q => q.ProvinceName == province.Name && q.BuildingType == buildingType))
            {
                res.Success = false;
                res.MainMessage = $"هناك عملية بناء أو ترقية جارية بالفعل لهذا المبنى في {province.Name}.";
                return res;
            }

            int cost = 0;
            int days = 0;

            // Increasing cost per level
            switch (buildingType)
            {
                case "سوق": cost = 100 + (currentLevel * 50); days = 30 + (currentLevel * 5); break;
                case "ثكنة": cost = 150 + (currentLevel * 50); days = 60 + (currentLevel * 10); break;
                case "مزرعة": cost = 100 + (currentLevel * 30); days = 30 + (currentLevel * 5); break;
                case "منجم": cost = 200 + (currentLevel * 100); days = 60 + (currentLevel * 10); break;
                default: res.Success = false; res.MainMessage = "نوع المبنى غير معروف."; return res;
            }

            string actionName = currentLevel <= 0 ? "بناء" : "ترقية";

            if (state.Gold < cost)
            {
                res.Success = false;
                res.MainMessage = $"الذهب غير كافٍ لـ{actionName} {buildingType} للمستوى {currentLevel + 1} (يحتاج {cost} ذهب).";
                return res;
            }

            state.Gold -= cost;
            state.BuildingQueue.Add(new BuildingTask { ProvinceName = province.Name, BuildingType = buildingType, TurnsRemaining = days });
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -cost);
            res.Title = currentLevel <= 0 ? "بناء المبنى" : "ترقية المبنى";
            res.MainMessage = $"بدأ العمال في {actionName} {buildingType} في {province.Name} للمستوى {currentLevel + 1}. سيستغرق العمل {days} يوماً بتكلفة {cost} ذهب.";
            res.SoundEffectKey = "build";
            return res;
        }

        public static GameActionResult StartBuilding(GameState state, int provinceIndex, string buildingType)
        {
            var res = new GameActionResult { Title = "بدء مشروع بناء" };
            if (provinceIndex < 0 || provinceIndex >= state.Provinces.Count)
            {
                res.Success = false; res.MainMessage = "المقاطعة غير صالحة."; return res;
            }

            var province = state.Provinces[provinceIndex];
            int cost = 0;
            int days = 0;

            switch (buildingType)
            {
                case "سوق": cost = 100; days = 30; break;
                case "ثكنة": cost = 150; days = 60; break;
                case "مزرعة": cost = 100; days = 30; break;
                case "منجم": cost = 200; days = 60; break;
                default: res.Success = false; res.MainMessage = "نوع المبنى غير معروف."; return res;
            }

            if (state.Gold < cost)
            {
                res.Success = false;
                res.MainMessage = $"الذهب غير كافٍ لبناء {buildingType} (يحتاج {cost} ذهب).";
                return res;
            }

            state.Gold -= cost;
            // The model uses TurnsRemaining but we repurpose it to mean Days
            state.BuildingQueue.Add(new BuildingTask { ProvinceName = province.Name, BuildingType = buildingType, TurnsRemaining = days });
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -cost);
            res.MainMessage = $"بدأ العمال في بناء {buildingType} في {province.Name}. سيستغرق البناء {days} يوماً بتكلفة {cost} ذهب.";
            res.SoundEffectKey = "build";
            return res;
        }

        public static GameActionResult ProcessDailyEconomy(GameState state)
        {
            var result = new GameActionResult 
            { 
                Title = "التقرير الاقتصادي", 
                Success = true, 
                ShouldNarrate = false,
                MainMessage = ""
            };

            List<string> dailyReports = new List<string>();

            if (state.SeasonalMarketDaysLeft > 0)
            {
                state.SeasonalMarketDaysLeft--;
                if (state.SeasonalMarketDaysLeft == 0)
                    dailyReports.Add("انتهى السوق الموسمي وعادت حركة السوق إلى وضعها المعتاد.");
            }

            // 1.5 Process Automatic Loans
            for (int i = state.Loans.Count - 1; i >= 0; i--)
            {
                var loan = state.Loans[i];
                if (loan.RepaymentMode == "Automatic")
                {
                    int payment = Math.Min(loan.ScheduledPaymentAmount, loan.RemainingAmount);
                    state.Gold -= payment;
                    loan.RemainingAmount -= payment;
                    
                    if (state.Gold < 0)
                    {
                        loan.IsDefaulted = true;
                        state.MerchantsTrust = Math.Max(0, state.MerchantsTrust - 1);
                    }
                    
                    if (loan.RemainingAmount <= 0)
                    {
                        dailyReports.Add($"تم سداد قرض {loan.LenderName} بالكامل.");
                        state.Loans.RemoveAt(i);
                    }
                }
            }

            // 1. Process Building Queue (Daily)
            for (int i = state.BuildingQueue.Count - 1; i >= 0; i--)
            {
                var task = state.BuildingQueue[i];
                task.TurnsRemaining--;
                
                if (task.TurnsRemaining <= 0)
                {
                    var province = state.Provinces.FirstOrDefault(p => p.Name == task.ProvinceName);
                    if (province != null)
                    {
                        var existingBuilding = province.Buildings.FirstOrDefault(b => b.BuildingType == task.BuildingType);
                        if (existingBuilding != null)
                        {
                            existingBuilding.Level++;
                        }
                        else
                        {
                            province.Buildings.Add(new LocalBuilding { BuildingType = task.BuildingType, Level = 1 });
                        }
                        
                        // Apply grand strategy modifiers
                        if (task.BuildingType == "سوق") province.Income += 20;
                        else if (task.BuildingType == "ثكنة") { province.LocalGarrison += 200; province.RecruitableLevy += 100; }
                        
                        dailyReports.Add($"اكتمل بناء {task.BuildingType} في {task.ProvinceName}");
                    }
                    state.BuildingQueue.RemoveAt(i);
                }
            }

            // 2. Monthly Collection and Consumption (on day 30)
            if (state.Time.Day == 30)
            {
                double taxMultiplier = 1.0;
                int satisfactionModifier = 0;
                
                if (state.TaxLevel == "منخفض") { taxMultiplier = 0.7; satisfactionModifier = +2; }
                else if (state.TaxLevel == "مرتفع") { taxMultiplier = 1.4; satisfactionModifier = -3; }

                int totalIncome = 0;
                int foodProduced = 0;
                int silverProduced = 0;
                DiplomacySystem.SynchronizeDiplomacyState(state);
                int tradeIncome = state.Neighbors.Count(n => n.TradeTreaty) * 30;
                int seasonalMarketIncome = state.SeasonalMarketDaysLeft > 0 ? 150 + (state.MerchantsTrust * 2) : 0;
                int protectedRouteIncome = state.ProtectedTradeRoutes.Count * 20;
                int supplyContractIncome = state.ActiveSupplyContracts * 25;

                
            // Vassal Tribute
            foreach (var neighbor in state.Neighbors)
            {
                if (neighbor.Relation == "تابع" && neighbor.TributePercent > 0)
                {
                    int tributeGold = (int)(150 * (neighbor.TributePercent / 100.0));
                    int tributeFood = (int)(100 * (neighbor.TributePercent / 100.0));
                    state.Gold += tributeGold;
                    state.Food += tributeFood;
                    
                    dailyReports.Add($"وصلت الجزية الشهرية من {neighbor.Name}: +{tributeGold} ذهب، +{tributeFood} مؤن.");
                }
            }
            
            foreach (var province in state.Provinces)
                {
                    if (!province.Occupied)
                    {
                        totalIncome += (int)(province.Income * taxMultiplier);
                        province.Satisfaction = Math.Max(0, Math.Min(100, province.Satisfaction + satisfactionModifier));
                        
                        int farmLevel = province.Buildings.Where(b => b.BuildingType == "مزرعة").Sum(b => b.Level);
                        foodProduced += 100 + (farmLevel * 200);
                        
                        int marketLevel = province.Buildings.Where(b => b.BuildingType == "سوق").Sum(b => b.Level);
                        int mineLevel = province.Buildings.Where(b => b.BuildingType == "منجم").Sum(b => b.Level);
                        
                        silverProduced += (marketLevel * 10);
                        totalIncome += (int)((marketLevel * 30) * taxMultiplier);
                        
                        if (mineLevel > 0)
                        {
                            int requiredSilverForWages = mineLevel * 20;
                            if (state.SilverCoins >= requiredSilverForWages)
                            {
                                state.SilverCoins -= requiredSilverForWages;
                                totalIncome += (int)((mineLevel * 100) * taxMultiplier);
                            }
                            else
                            {
                                dailyReports.Add($"⚠️ تحذير: إضراب عمال المناجم في {province.Name} بسبب عدم توفر الفضة لدفع الرواتب.");
                            }
                        }
                    }
                }
                
                totalIncome += tradeIncome + seasonalMarketIncome + protectedRouteIncome + supplyContractIncome;
                int hiddenCorruptionLoss = ApplyHiddenCorruption(state, totalIncome);
                totalIncome = Math.Max(0, totalIncome - hiddenCorruptionLoss);
                if (hiddenCorruptionLoss > 0 && state.Council.Values.Any(c => c.CorruptionDiscovered))
                    dailyReports.Add($"كشف جهاز الدولة اختلاساً شهرياً مخفياً بقيمة {hiddenCorruptionLoss} ذهب.");
                
                // Deduct Minister Budgets and apply effects
                if (state.MinisterBudgets != null)
                {
                    int totalBudgetPercent = state.MinisterBudgets.Values.Sum();
                    if (totalBudgetPercent > 0 && totalBudgetPercent <= 100)
                    {
                        int originalTotalIncome = totalIncome;
                        int totalBudgetAmount = (int)(originalTotalIncome * (totalBudgetPercent / 100.0));
                        totalIncome -= totalBudgetAmount;
                        dailyReports.Add($"تم تخصيص {totalBudgetAmount} ذهب ({totalBudgetPercent}%) لميزانيات الوزراء.");

                        int fmGold = (int)(originalTotalIncome * (state.MinisterBudgets.ContainsKey("first_minister") ? state.MinisterBudgets["first_minister"] : 0) / 100.0);
                        int stGold = (int)(originalTotalIncome * (state.MinisterBudgets.ContainsKey("steward") ? state.MinisterBudgets["steward"] : 0) / 100.0);
                        int maGold = (int)(originalTotalIncome * (state.MinisterBudgets.ContainsKey("marshal") ? state.MinisterBudgets["marshal"] : 0) / 100.0);
                        int spGold = (int)(originalTotalIncome * (state.MinisterBudgets.ContainsKey("spymaster") ? state.MinisterBudgets["spymaster"] : 0) / 100.0);

                        // 1. First Minister (Prestige/Satisfaction)
                        if (fmGold > 0)
                        {
                            int prestigeGain = fmGold / 50;
                            state.Prestige += prestigeGain;
                            state.Satisfaction = Math.Min(100, state.Satisfaction + (fmGold / 100));
                            if (prestigeGain > 0) dailyReports.Add($"- الوزير الأول استخدم ميزانيته لرفع الهيبة ({prestigeGain}+) ورضا الشعب.");
                        }

                        // 2. Steward (Economy / Markets)
                        if (stGold > 0)
                        {
                            int marketCost = 150;
                            if (stGold >= marketCost && state.Provinces.Count > 0)
                            {
                                int marketsBuilt = stGold / marketCost;
                                for (int i=0; i<marketsBuilt; i++) {
                                    var rndProv = state.Provinces[new Random().Next(state.Provinces.Count)];
                                    var existingMarket = rndProv.Buildings.FirstOrDefault(b => b.BuildingType == "سوق");
                                    if (existingMarket != null) existingMarket.Level++;
                                    else rndProv.Buildings.Add(new LocalBuilding { BuildingType = "سوق", Level = 1 });
                                }
                                dailyReports.Add($"- وزير المالية استثمر الميزانية في بناء/تطوير {marketsBuilt} أسواق جديدة.");
                            }
                            else
                            {
                                int extraIncome = stGold / 2; // Returns 50% ROI
                                totalIncome += extraIncome;
                                dailyReports.Add($"- وزير المالية استثمر الميزانية وعادت بأرباح قدرها {extraIncome} ذهب للخزينة.");
                            }
                        }

                        // 3. Marshal (Army / Garrison)
                        if (maGold > 0)
                        {
                            int soldiersGained = (maGold / 50) * 100;
                            state.Army += soldiersGained;
                            dailyReports.Add($"- قائد الجند استخدم ميزانيته لتدريب {soldiersGained} جندي إضافي.");
                        }

                        // 4. Spymaster (Intelligence / Spy Power)
                        if (spGold > 0)
                        {
                            int confisGold = spGold + (spGold / 2);
                            totalIncome += confisGold; 
                            dailyReports.Add($"- مدير الاستخبارات كشف شبكة فساد وصادر {confisGold} ذهب لخزينة الدولة.");
                        }
                    }
                }

                state.Gold += totalIncome;
                state.SilverCoins += silverProduced;
                state.Food += foodProduced;
                
                // Consume food for state.Armies
                int totalSoldiers = 0;
                if (state.Armies != null)
                {
                    totalSoldiers += state.Armies.Sum(a => a.TotalSoldiers);
                }
                
                int armyFoodConsumption = totalSoldiers / 5;
                state.Food -= armyFoodConsumption;

                string tradeReport = tradeIncome > 0 ? $" دخل التجارة: {tradeIncome} ذهب." : "";
                if (seasonalMarketIncome > 0) tradeReport += $" دخل السوق الموسمي: {seasonalMarketIncome} ذهب.";
                if (protectedRouteIncome > 0) tradeReport += $" دخل الطرق المحمية: {protectedRouteIncome} ذهب.";
                if (supplyContractIncome > 0) tradeReport += $" دخل عقود التجار: {supplyContractIncome} ذهب.";
                dailyReports.Add($"نهاية الشهر: تم جمع {totalIncome} ذهب، وحصاد {foodProduced} مؤونة.{tradeReport} استهلاك الجيش: {armyFoodConsumption} مؤونة.");

                if (state.Food < 0)
                {
                    state.Food = 0;
                    int desertion = totalSoldiers / 10;
                    
                    int overflow = desertion;
                    foreach (var army in state.Armies)
                    {
                        if (army.TotalSoldiers >= overflow)
                        {
                            army.TotalSoldiers -= overflow;
                            overflow = 0;
                            break;
                        }
                        else
                        {
                            overflow -= army.TotalSoldiers;
                            army.TotalSoldiers = 0;
                        }
                    }

                    state.Satisfaction = Math.Max(0, state.Satisfaction - 10);
                    state.Prestige -= 5;
                    dailyReports.Add($"⚠️ تحذير خطير: مجاعة تضرب المملكة! فر {desertion} من الجنود وانخفض رضا الشعب بشدة.");
                }
                
                if (state.Gold < 0)
                {
                    state.Satisfaction = Math.Max(0, state.Satisfaction - 5);
                    state.Prestige -= 5;
                    dailyReports.Add($"⚠️ تحذير خطير: إفلاس الخزينة! تأخر دفع الرواتب يثير غضب الشعب والجنود.");
                }
            }

            if (dailyReports.Count > 0)
            {
                result.ShouldNarrate = true;
                result.MainMessage = string.Join("\n", dailyReports);
            }

            return result;
        }

        private static int ApplyHiddenCorruption(GameState state, int grossIncome)
        {
            if (state.Council == null || grossIncome <= 0)
                return 0;

            int loss = 0;
            foreach (var member in state.Council.Values)
            {
                if (member == null || !member.IsCorrupt)
                    continue;

                int rate = member.HiddenCorruptionRate <= 0 ? 5 : member.HiddenCorruptionRate;
                loss += Math.Max(1, grossIncome * rate / 100);

                if (!member.CorruptionDiscovered && state.CounterIntelligenceLevel + member.Trust > 85)
                {
                    member.CorruptionDiscovered = true;
                    var character = state.RealmCharacters.FirstOrDefault(c => c.SourceType == "Councilor" && c.SourceId == member.Title);
                    if (character != null && !state.CharacterSecrets.Any(s => s.OwnerCharacterId == character.Id && s.Type == SecretType.Corruption))
                    {
                        state.CharacterSecrets.Add(new CharacterSecret
                        {
                            OwnerCharacterId = character.Id,
                            OwnerName = member.Name,
                            Type = SecretType.Corruption,
                            Severity = Math.Clamp(rate, 1, 10),
                            IsKnownToPlayer = true,
                            Summary = $"{member.Name} يختلس جزءاً من دخل المملكة الشهري."
                        });
                    }
                }
            }

            return loss;
        }

        // Keep a shim for TurnResolutionSystem.cs backward compatibility if still used
        public static string ProcessEconomyTurn(GameState state)
        {
            var result = ProcessDailyEconomy(state);
            return result.MainMessage;
        }

        public static GameActionResult MobilizeArmy(GameState state, int provinceIndex)
        {
            var res = new GameActionResult { Title = "تعبئة جيش ميداني" };
            var prov = state.Provinces[provinceIndex];
            if (prov.LocalGarrison < 200)
            {
                res.Success = false;
                res.MainMessage = $"لا يوجد عدد كافٍ من الجنود في حامية {prov.Name} لتشكيل جيش ميداني. يتطلب 200 جندي.";
                return res;
            }

            prov.LocalGarrison -= 200;
            string armyId = "army_" + Guid.NewGuid().ToString().Substring(0, 8);
            
            if (state.Armies == null) state.Armies = new System.Collections.Generic.List<Army>();
            
            state.Armies.Add(new Army { 
                Id = armyId, 
                Name = $"جيش {prov.Name}", 
                CommanderName = "قائد الجيش", 
                CurrentProvince = prov.Name, 
                TotalSoldiers = 200, 
                Supply = 100, 
                Morale = 100 
            });
            
            res.Success = true;
            res.MainMessage = $"تم بنجاح تعبئة جيش ميداني جديد من مقاطعة {prov.Name} بقوة 200 جندي.";
            res.SoundEffectKey = "sword";
            return res;
        }

        public static GameActionResult StartSeasonalMarket(GameState state)
        {
            var res = new GameActionResult { Title = "السوق الموسمي" };
            const int goldCost = 200;
            const int foodCost = 100;

            if (state.SeasonalMarketDaysLeft > 0)
            {
                res.Success = false;
                res.MainMessage = $"هناك سوق موسمي قائم بالفعل. متبقٍ {state.SeasonalMarketDaysLeft} يوم.";
                return res;
            }

            if (state.Gold < goldCost || state.Food < foodCost)
            {
                res.Success = false;
                res.MainMessage = $"تحتاج إلى {goldCost} ذهب و{foodCost} مؤونة لإقامة سوق موسمي كبير.";
                return res;
            }

            state.Gold -= goldCost;
            state.Food -= foodCost;
            state.SeasonalMarketDaysLeft = 30;
            state.MerchantsTrust = Math.Min(100, state.MerchantsTrust + 5);
            state.Satisfaction = Math.Min(100, state.Satisfaction + 2);

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -goldCost);
            res.ResourceChanges.Add("المؤونة", -foodCost);
            res.MainMessage = "أُقيم سوق موسمي في بغداد والطرق الكبرى لمدة 30 يوماً. سيضيف دخلاً عند نهاية الشهر ويرفع ثقة التجار.";
            res.SoundEffectKey = "coins";
            DynastyChronicleSystem.RecordEvent(state, "Trade", "السوق الموسمي في بغداد", "أقامت الخلافة سوقاً موسمياً كبيراً جذب التجار والقوافل إلى طرق بغداد.", 4, 2);
            return res;
        }

        public static GameActionResult GrantMerchantPrivileges(GameState state)
        {
            var res = new GameActionResult { Title = "تسهيلات التجار" };
            const int cost = 150;
            if (state.Gold < cost)
            {
                res.Success = false;
                res.MainMessage = $"تحتاج إلى {cost} ذهب لتنظيم سجلات التجار ومنحهم تسهيلات.";
                return res;
            }

            state.Gold -= cost;
            state.MerchantsTrust = Math.Min(100, state.MerchantsTrust + 12);
            state.ActiveSupplyContracts += 1;

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -cost);
            res.MainMessage = "تم تحديث سجلات التجار ومنح تسهيلات للقوافل. زادت ثقة التجار وبدأ عقد تجاري يدر دخلاً شهرياً.";
            res.SoundEffectKey = "paper";
            DynastyChronicleSystem.RecordEvent(state, "Trade", "تسهيلات التجار", "نظم البلاط سجلات التجار ومنح تسهيلات للقوافل مقابل عقود توريد شهرية.", 3, 1);
            return res;
        }

        public static GameActionResult ProtectTradeRoute(GameState state, string routeName)
        {
            var res = new GameActionResult { Title = "حماية طريق تجاري" };
            if (string.IsNullOrWhiteSpace(routeName))
            {
                res.Success = false;
                res.MainMessage = "اسم الطريق التجاري غير صالح.";
                return res;
            }

            if (state.ProtectedTradeRoutes.Contains(routeName))
            {
                res.Success = false;
                res.MainMessage = $"الطريق {routeName} محمي بالفعل.";
                return res;
            }

            if (state.Army < 300)
            {
                res.Success = false;
                res.MainMessage = "تحتاج إلى قوة عسكرية لا تقل عن 300 جندي قبل إرسال حراسة ثابتة للطرق.";
                return res;
            }

            const int goldCost = 100;
            const int foodCost = 50;
            if (state.Gold < goldCost || state.Food < foodCost)
            {
                res.Success = false;
                res.MainMessage = $"تحتاج إلى {goldCost} ذهب و{foodCost} مؤونة لتجهيز حراسة الطريق.";
                return res;
            }

            state.Gold -= goldCost;
            state.Food -= foodCost;
            state.ProtectedTradeRoutes.Add(routeName);
            state.MerchantsTrust = Math.Min(100, state.MerchantsTrust + 10);
            state.Prestige += 5;

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -goldCost);
            res.ResourceChanges.Add("المؤونة", -foodCost);
            res.MainMessage = $"أرسلت قوات ملكية لحماية {routeName}. زادت ثقة التجار وسيضيف الطريق المحمي دخلاً شهرياً.";
            res.SoundEffectKey = "sword";
            DynastyChronicleSystem.RecordEvent(state, "Trade", "حماية طريق تجاري", $"أرسلت الخلافة قوات لحماية {routeName}، فازدادت ثقة التجار بطرق بغداد.", 5, 2);
            return res;
        }

        public static GameActionResult RequestMerchantLoan(GameState state, int amount)
        {
            var res = new GameActionResult { Title = "طلب قرض من التجار" };
            
            var loan = new Loan
            {
                LenderType = "Merchants",
                LenderName = "نقابة التجار",
                PrincipalAmount = amount,
                RemainingAmount = amount, // No financial interest, based on user request
                StartDateDays = state.Time.Year * 360 + state.Time.Month * 30 + state.Time.Day,
                DueDateDays = state.Time.Year * 360 + state.Time.Month * 30 + state.Time.Day + 360, // 1 year
                RepaymentMode = "Automatic",
                ScheduledPaymentAmount = Math.Max(1, amount / 360), // Daily payment amount
                PoliticalCondition = "منح امتيازات تجارية في العاصمة"
            };
            
            state.Loans.Add(loan);
            state.Gold += amount;
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", amount);
            res.MainMessage = $"تم الحصول على قرض بقيمة {amount} ذهب من نقابة التجار.";
            
            return res;
        }

        public static GameActionResult RequestForeignLoan(GameState state, string kingdomName)
        {
            var res = new GameActionResult { Title = "طلب قرض أجنبي" };
            
            int amount = 2000; // Using a default 2000 value as none was parameterized
            var loan = new Loan
            {
                LenderType = "ForeignKingdom",
                LenderName = kingdomName,
                PrincipalAmount = amount,
                RemainingAmount = amount, // No financial interest
                StartDateDays = state.Time.Year * 360 + state.Time.Month * 30 + state.Time.Day,
                DueDateDays = state.Time.Year * 360 + state.Time.Month * 30 + state.Time.Day + 360,
                RepaymentMode = "Automatic",
                ScheduledPaymentAmount = Math.Max(1, amount / 360),
                PoliticalCondition = "معاهدة سلام ملزمة"
            };
            
            state.Loans.Add(loan);
            state.Gold += amount;
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", amount);
            res.MainMessage = $"تم الحصول على قرض أجنبي بقيمة {amount} ذهب من {kingdomName} بشرط: {loan.PoliticalCondition}.";
            
            return res;
        }

        public static GameActionResult RepayLoan(GameState state, string loanId, int amount)
        {
            var res = new GameActionResult { Title = "سداد قرض" };
            var loan = state.Loans.FirstOrDefault(l => l.Id == loanId);
            
            if (loan == null)
            {
                res.Success = false;
                res.MainMessage = "القرض غير موجود.";
                return res;
            }
            
            if (state.Gold < amount)
            {
                res.Success = false;
                res.MainMessage = "لا تملك الذهب الكافي للسداد.";
                return res;
            }
            
            int actualRepayment = Math.Min(amount, loan.RemainingAmount);
            state.Gold -= actualRepayment;
            loan.RemainingAmount -= actualRepayment;
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -actualRepayment);
            
            if (loan.RemainingAmount <= 0)
            {
                state.Loans.Remove(loan);
                res.MainMessage = $"تم سداد القرض بالكامل. تم دفع {actualRepayment} ذهب.";
            }
            else
            {
                res.MainMessage = $"تم سداد {actualRepayment} ذهب. المتبقي: {loan.RemainingAmount}.";
            }
            
            return res;
        }
    }
}
