using System;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class IntelligenceSystem
    {
        private static Random rand = new Random();

        // 1. Establish Network
        public static GameActionResult EstablishNetwork(GameState state, string name, string targetType, string targetId)
        {
            var res = new GameActionResult { Success = false };

            if (state.Gold < 150)
            {
                res.MainMessage = "لا تملك الذهب الكافي لبناء شبكة جواسيس (المطلوب 150 ذهب).";
                return res;
            }

            if (state.SpyNetworks.Any(n => n.TargetType == targetType && n.TargetId == targetId))
            {
                res.MainMessage = "لديك شبكة جواسيس بالفعل في هذا الهدف.";
                return res;
            }

            state.Gold -= 150;
            var network = new SpyNetwork
            {
                Name = name,
                TargetType = targetType,
                TargetId = targetId,
                Strength = 10,
                Secrecy = 20,
                Infiltration = 5,
                Analysis = 5
            };
            state.SpyNetworks.Add(network);

            res.Success = true;
            res.MainMessage = $"تم بنجاح تأسيس {name}. الجواسيس بدأوا بالانتشار.";
            res.SoundEffectKey = "coin";
            return res;
        }

        // 2. Upgrade Network
        public static GameActionResult UpgradeNetwork(GameState state, string networkId, string upgradeType)
        {
            var res = new GameActionResult { Success = false };
            var net = state.SpyNetworks.FirstOrDefault(n => n.Id == networkId);
            if (net == null)
            {
                res.MainMessage = "لم يتم العثور على الشبكة.";
                return res;
            }

            int cost = 0;
            string successMsg = "";

            switch (upgradeType)
            {
                case "تجنيد مخبرين محليين": // Local Informants
                    cost = 100;
                    if (state.Gold < cost) { res.MainMessage = $"لا يوجد ذهب كافٍ لهذه الترقية. المطلوب {cost} ذهب."; return res; }
                    state.Gold -= cost;
                    net.Strength = Math.Min(100, net.Strength + 15);
                    net.Secrecy = Math.Max(0, net.Secrecy - 5); // More people = less secret
                    successMsg = $"تم تجنيد مخبرين بنجاح لشبكة {net.Name}. زادت القوة وانخفضت السرية قليلاً.";
                    break;
                case "زرع جاسوس داخل البلاط": // Infiltrate Court
                    cost = 200;
                    if (state.Gold < cost) { res.MainMessage = $"لا يوجد ذهب كافٍ لهذه الترقية. المطلوب {cost} ذهب."; return res; }
                    state.Gold -= cost;
                    net.Infiltration = Math.Min(100, net.Infiltration + 20);
                    net.ExposureRisk += 10;
                    successMsg = $"تم زرع عميل عميق. زاد الاختراق ولكن خطر الانكشاف ارتفع.";
                    break;
                case "شراء ولاء الخدم": // Bribe Servants
                    cost = 150;
                    if (state.Gold < cost) { res.MainMessage = $"لا يوجد ذهب كافٍ لهذه الترقية. المطلوب {cost} ذهب."; return res; }
                    state.Gold -= cost;
                    net.Analysis = Math.Min(100, net.Analysis + 15);
                    net.Infiltration = Math.Min(100, net.Infiltration + 10);
                    successMsg = $"الخدم ينقلون الأسرار الآن. زادت دقة التحليل والاختراق.";
                    break;
                case "بناء بيت رسائل سري": // Safehouse
                    cost = 300;
                    if (state.Gold < cost) { res.MainMessage = $"لا يوجد ذهب كافٍ لهذه الترقية. المطلوب {cost} ذهب."; return res; }
                    state.Gold -= cost;
                    net.Strength = Math.Min(100, net.Strength + 20);
                    net.Secrecy = Math.Min(100, net.Secrecy + 20);
                    successMsg = $"تم بناء بيت رسائل آمن. الشبكة الآن أكثر قوة وسرية.";
                    break;
                default:
                    res.MainMessage = "ترقية غير معروفة.";
                    return res;
            }

            res.Success = true;
            res.MainMessage = successMsg;
            res.SoundEffectKey = "coin";

            return res;
        }

        public static GameActionResult ImproveCounterIntelligence(GameState state, int goldCost = 200, int strengthGain = 5)
        {
            var res = new GameActionResult { Title = "مكافحة الاستخبارات" };
            if (state.Gold < goldCost)
            {
                res.Success = false;
                res.MainMessage = $"الذهب لا يكفي لتحسين مكافحة الاستخبارات. المطلوب {goldCost} ذهب.";
                return res;
            }

            state.Gold -= goldCost;
            state.CounterIntelligenceLevel = Math.Clamp(state.CounterIntelligenceLevel + strengthGain, 0, 100);

            res.Success = true;
            res.ResourceChanges.Add("الذهب", -goldCost);
            res.MainMessage = $"نُشر حرس أوفياء وراجعت العيون السرية سجلات الخدم والرسل. ارتفع مستوى مكافحة الاستخبارات إلى {state.CounterIntelligenceLevel}/100.";
            res.SoundEffectKey = "paper";
            return res;
        }

        // 3. Start Operation
        public static GameActionResult StartOperation(GameState state, string name, string type, string targetType, string targetId, string networkId, int goldCost, int days)
        {
            var res = new GameActionResult { Success = false };

            if (state.Gold < goldCost)
            {
                res.MainMessage = "الذهب لا يكفي لتمويل العملية.";
                return res;
            }

            var net = state.SpyNetworks.FirstOrDefault(n => n.Id == networkId);
            if (net == null)
            {
                res.MainMessage = "الشبكة غير صالحة.";
                return res;
            }

            state.Gold -= goldCost;

            // Base chances calculated from network stats + Spymaster presence
            int spymasterSkill = state.Council.ContainsKey("spymaster") ? 5 : 0;
            int baseSuccess = net.Strength + net.Infiltration + (spymasterSkill * 5);
            int baseExposure = 50 - net.Secrecy + net.ExposureRisk - (spymasterSkill * 2);

            var op = new IntelligenceOperation
            {
                Name = name,
                OperationType = type,
                TargetType = targetType,
                TargetId = targetId,
                AssignedSpyNetworkId = networkId,
                DaysRemaining = days,
                GoldCost = goldCost,
                SuccessChance = Math.Clamp(baseSuccess, 10, 95),
                ExposureChance = Math.Clamp(baseExposure, 5, 80),
                Status = "Active"
            };

            state.IntelligenceOperations.Add(op);
            net.ActiveOperationsCount++;

            res.Success = true;
            res.MainMessage = $"بدأت عملية {name}. ستستغرق {days} أيام. فرصة النجاح المقدرة: {op.SuccessChance}%.";
            return res;
        }

        // 4. Process Daily
        public static GameActionResult ProcessDailyIntelligence(GameState state)
        {
            var res = new GameActionResult { Success = true, MainMessage = "" };
            bool anyFinished = false;

            foreach (var op in state.IntelligenceOperations.Where(o => o.Status == "Active"))
            {
                op.DaysRemaining--;

                if (op.DaysRemaining <= 0)
                {
                    ResolveOperation(state, op);
                    anyFinished = true;
                    if (op.RequiresPause) res.ShouldPauseTime = true;
                    res.Warnings.Add($"[تقرير استخباراتي]: {op.Name} - {op.Status}\n{op.ResultSummary}");
                    state.SecretReports.Add($"[{state.Time.GetDateString()}] {op.Name}: {op.ResultSummary}");
                }
            }

            if (anyFinished)
            {
                res.MainMessage = "وردت تقارير استخباراتية جديدة يا مولاي.";
                res.SoundEffectKey = "tick";
            }

            return res;
        }

        private static void ResolveOperation(GameState state, IntelligenceOperation op)
        {
            var net = state.SpyNetworks.FirstOrDefault(n => n.Id == op.AssignedSpyNetworkId);
            if (net != null) net.ActiveOperationsCount = Math.Max(0, net.ActiveOperationsCount - 1);

            int roll = rand.Next(1, 101);
            int exposureRoll = rand.Next(1, 101);

            bool success = roll <= op.SuccessChance;
            bool exposed = exposureRoll <= op.ExposureChance;

            if (exposed)
            {
                op.Status = "Exposed";
                op.ResultSummary = "لقد انكشف عملاؤنا أثناء تنفيذ العملية! تم إعدام بعضهم، والمملكة تواجه فضيحة.";
                state.Prestige = Math.Max(0, state.Prestige - 15);
                if (net != null) net.ExposureRisk += 20;
                
                // If it targets a governor
                if (op.TargetType == "InternalProvince")
                {
                    var gov = state.Governors.FirstOrDefault(g => g.ProvinceName == op.TargetId);
                    if (gov != null)
                    {
                        gov.OpinionOfKing = Math.Max(-100, gov.OpinionOfKing - 30);
                        op.ResultSummary += $"\nغضب الوالي {gov.Name} بشدة لاكتشافه تجسسك عليه!";
                    }
                }
                return;
            }

            if (!success)
            {
                op.Status = "Failed";
                op.ResultSummary = "فشلت العملية في تحقيق أهدافها، لكن العملاء انسحبوا دون أن ينكشفوا.";
                return;
            }

            op.Status = "Completed";
            string confidence = GetConfidence(state, net);
            int confidenceRank = GetConfidenceRank(state, net);
            
            // Execute specific effects based on operation type
            switch (op.OperationType)
            {
                case "مراقبة والٍ":
                    var gov = state.Governors.FirstOrDefault(g => g.ProvinceName == op.TargetId);
                    if (gov != null)
                    {
                        string mood = PoliticalSystem.GetArabicMood(gov.CurrentMood);
                        if (confidenceRank >= 3)
                        {
                            op.ResultSummary = $"نجحت المراقبة. الوالي {gov.Name} يُظهر ولاءً مقداره {gov.Loyalty} ومزاجه: {mood}.\nدرجة الثقة: {confidence}.";
                        }
                        else if (confidenceRank == 2)
                        {
                            int min = Math.Max(0, gov.Loyalty - 15);
                            int max = Math.Min(100, gov.Loyalty + 15);
                            op.ResultSummary = $"نجحت المراقبة جزئياً. ولاء الوالي {gov.Name} يبدو بين {min} و{max}، ومزاجه أقرب إلى: {mood}.\nدرجة الثقة: {confidence}.";
                        }
                        else
                        {
                            op.ResultSummary = $"وصلت إشارات غير مكتملة عن الوالي {gov.Name}. ولاؤه يبدو {DescribeLevel(gov.Loyalty)}، ومزاجه غير مؤكد.\nدرجة الثقة: {confidence}.";
                        }
                    }
                    break;
                case "تفكيك فصيل":
                    var faction = state.Factions.FirstOrDefault(f => f.Id == op.TargetId);
                    if (faction != null)
                    {
                        faction.PowerPercent = Math.Max(0, faction.PowerPercent - 25);
                        faction.Discontent = Math.Max(0, faction.Discontent - 20);
                        op.ResultSummary = $"تم زرع الشقاق داخل فصيل {faction.Name}. انخفضت قوتهم بمقدار 25% وسخطهم بمقدار 20.\nدرجة الثقة: {confidence}.";
                    }
                    break;
                case "استطلاع مملكة":
                    var neighbor = state.Neighbors.FirstOrDefault(n => n.Name == op.TargetId);
                    if (neighbor != null)
                    {
                        int actualArmy = neighbor.Army;
                        int margin = confidenceRank switch
                        {
                            >= 4 => 50,
                            3 => 120,
                            2 => 300,
                            _ => 700
                        };
                        int estMin = actualArmy - rand.Next(Math.Max(50, margin / 2), margin + 1);
                        int estMax = actualArmy + rand.Next(Math.Max(75, margin / 2), margin + 1);
                        
                        string intent = neighbor.Opinion < 20 ? "عدائية" : (neighbor.Opinion > 60 ? "ودية" : "محايدة");
                        
                        op.ResultSummary = $"تقرير عن {neighbor.Name}.\nقوة الجيش المتوقعة: بين {Math.Max(0, estMin)} و {estMax}.\nالنية السياسية: {intent}.\nدرجة الثقة: {confidence}.";
                    }
                    break;
                
                
                case "اغتيال الحاكم العدو":
                    var targetKing = state.Neighbors.FirstOrDefault(n => n.Name == op.TargetId);
                    if (targetKing != null)
                    {
                        int chance = confidenceRank * 20;
                        if (rand.Next(100) < chance)
                        {
                            targetKing.Opinion = 0;
                            targetKing.Relation = "عداء";
                            targetKing.Army = Math.Max(0, targetKing.Army - 500);
                            state.Prestige += 100;
                            op.ResultSummary = $"نجاح باهر! تم دس السم لحاكم {targetKing.Name}. غرقت المملكة في فوضى الخلافة وتشتت جيشهم.\nدرجة الثقة: {confidence}.";
                        }
                        else
                        {
                            targetKing.Opinion = -100;
                            targetKing.Relation = "حرب";
                            targetKing.IsAtWarWithPlayer = true;
                            state.Prestige -= 50;
                            op.ResultSummary = $"فشل ذريع! تم القبض على قتلتك قبل وصولهم لحاكم {targetKing.Name}. اعتبروا ذلك إعلان حرب وانكشف أمرك للعلن!\nدرجة الثقة: {confidence}.";
                        }
                    }
                    break;
                case "اغتيال والي":
                    var targetGov = state.Governors.FirstOrDefault(g => g.ProvinceName == op.TargetId);
                    if (targetGov != null)
                    {
                        if (rand.Next(100) < 70)
                        {
                            state.Governors.Remove(targetGov);
                            op.ResultSummary = $"تم بنجاح اغتيال الوالي {targetGov.Name} في جنح الظلام. يبدو الحادث كأنه طبيعي.\nدرجة الثقة: {confidence}.";
                        }
                        else
                        {
                            targetGov.Loyalty = 0;
                            targetGov.CurrentMood = "Angry";
                            state.Prestige -= 30;
                            op.ResultSummary = $"فشل الاغتيال! نجا {targetGov.Name} واكتشف أنك وراء المحاولة. ولاؤه انهار تماماً!\nدرجة الثقة: {confidence}.";
                        }
                    }
                    break;
                case "بحث عن فضائح":
                    var scandalGov = state.Governors.FirstOrDefault(g => g.ProvinceName == op.TargetId);
                    if (scandalGov != null)
                    {
                        if (rand.Next(100) < 80)
                        {
                            scandalGov.Loyalty = 100;
                            state.Gold += 200;
                            op.ResultSummary = $"نجاح! وجد جواسيسك وثائق تدين الوالي {scandalGov.Name} باختلاس أموال. دفع لك 200 ذهب لشراء صمتك وأقسم لك بالولاء المطلق خوفاً من الفضيحة.\nدرجة الثقة: {confidence}.";
                        }
                        else
                        {
                            op.ResultSummary = $"لم يجد جواسيسك أي شيء يعيب {scandalGov.Name}. يبدو أنه رجل شريف (أو حذر جداً).\nدرجة الثقة: {confidence}.";
                        }
                    }
                    break;
                case "تخريب مؤونة العدو":
                    op.ResultSummary = $"تم تسميم الآبار وتخريب مخازن الحبوب. إذا اندلعت حرب قريبًا، سيعاني جيشهم من الجوع.\nدرجة الثقة: {confidence}.";
                    // Need to implement actual debuff on neighbor or state in WarfareSystem later.
                    break;
                default:
                    op.ResultSummary = $"نجحت العملية.\nدرجة الثقة: {confidence}.";
                    break;
            }
        }

        private static string GetConfidence(GameState state, SpyNetwork net)
        {
            int rank = GetConfidenceRank(state, net);
            return rank switch
            {
                >= 4 => "مؤكدة",
                3 => "مرتفعة",
                2 => "متوسطة",
                1 => "منخفضة",
                _ => "شائعة غير مؤكدة"
            };
        }

        private static int GetConfidenceRank(GameState state, SpyNetwork net)
        {
            if (net == null) return 0;
            
            int analysisMod = net.Analysis;
            // High trust spymaster increases confidence
            if (state.Council.ContainsKey("spymaster") && state.Council["spymaster"].IsRightHandOfKing)
            {
                analysisMod += 20;
            }

            if (analysisMod >= 80 && net.Infiltration >= 80) return 4;
            if (analysisMod >= 60) return 3;
            if (analysisMod >= 30) return 2;
            return 1;
        }

        private static string DescribeLevel(int value)
        {
            if (value >= 70) return "مرتفعاً";
            if (value >= 40) return "متوسطاً";
            return "منخفضاً";
        }

        // --- SPYMASTER DEEP MECHANICS ---
        public static GameActionResult AppointSecretMonitor(GameState state)
        {
            if (!state.Council.ContainsKey("spymaster")) return new GameActionResult { Success = false, MainMessage = "لا يوجد مسؤول جواسيس لتعيين مراقب عليه." };
            var spy = state.Council["spymaster"];
            if (spy.HasSecretMonitor) return new GameActionResult { Success = false, MainMessage = "تم تعيين مراقب سري مسبقاً." };
            
            if (state.Gold < 100) return new GameActionResult { Success = false, MainMessage = "الذهب لا يكفي لتوظيف مراقب سري." };
            
            state.Gold -= 100;
            spy.HasSecretMonitor = true;
            spy.Trust = Math.Max(0, spy.Trust - 10);
            
            return new GameActionResult { Success = true, MainMessage = "تم تعيين عيون سرية لمراقبة مسؤول الجواسيس. إذا اكتشف ذلك، سينخفض ولاؤه." };
        }

        public static GameActionResult InterrogateSpymaster(GameState state)
        {
            if (!state.Council.ContainsKey("spymaster")) return new GameActionResult { Success = false, MainMessage = "لا يوجد مسؤول جواسيس لاستجوابه." };
            var spy = state.Council["spymaster"];
            
            spy.Loyalty = Math.Max(0, spy.Loyalty - 20);
            spy.Trust = Math.Max(0, spy.Trust - 15);
            spy.Influence = Math.Max(0, spy.Influence - 10);
            
            string msg = "تم استدعاء مسؤول الجواسيس واستجوابه بحدة. انخفض نفوذه في القصر، ولكنه أصبح يضمر لك الاستياء.";
            return new GameActionResult { Success = true, MainMessage = msg };
        }

        public static GameActionResult SupportSpymaster(GameState state)
        {
            if (!state.Council.ContainsKey("spymaster")) return new GameActionResult { Success = false, MainMessage = "لا يوجد مسؤول جواسيس لدعمه." };
            var spy = state.Council["spymaster"];
            
            spy.Loyalty = Math.Min(100, spy.Loyalty + 15);
            spy.Trust = Math.Min(100, spy.Trust + 15);
            spy.Influence = Math.Min(100, spy.Influence + 10);
            
            if (spy.Trust >= 90 && spy.Loyalty >= 90 && !spy.IsRightHandOfKing)
            {
                spy.IsRightHandOfKing = true;
                return new GameActionResult { Success = true, MainMessage = "أعلنت دعمك الكامل لمسؤول الجواسيس. لقد أصبح الآن 'يد الملك اليمنى في الظلال'، ستكون تقاريره أدق وسيحميك بفعالية أكبر." };
            }

            return new GameActionResult { Success = true, MainMessage = "قدمت دعماً صريحاً لمسؤول الجواسيس. زاد ولاؤه وثقتك به، لكن نفوذه في القصر ارتفع." };
        }
    }
}
