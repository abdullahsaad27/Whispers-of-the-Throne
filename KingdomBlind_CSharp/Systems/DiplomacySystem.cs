using System;
using System.Collections.Generic;
using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class DiplomacySystem
    {
        private static Random rand = new Random();

        public static int GetCurrentDayNumber(GameState state)
        {
            if (state?.Time == null) return 0;
            return ((state.Time.Year - 1) * 360) + ((state.Time.Month - 1) * 30) + state.Time.Day;
        }

        public static void SynchronizeDiplomacyState(GameState state)
        {
            if (state == null) return;
            state.ReconcileOldSaves();
            int today = GetCurrentDayNumber(state);

            foreach (var treaty in state.Treaties)
            {
                if (string.IsNullOrWhiteSpace(treaty.Id))
                    treaty.Id = Guid.NewGuid().ToString();

                if (treaty.StartDateDays <= 0)
                    treaty.StartDateDays = today;

                if (treaty.DurationDays > 0 && treaty.EndDateDays <= 0)
                    treaty.EndDateDays = treaty.StartDateDays + treaty.DurationDays;

                if (treaty.EndDateDays > 0 && treaty.EndDateDays < today)
                    treaty.IsActive = false;
            }

            foreach (var neighbor in state.Neighbors)
            {
                if (string.IsNullOrWhiteSpace(neighbor.Id))
                    neighbor.Id = Guid.NewGuid().ToString();

                if (neighbor.ActiveTreaties == null)
                    neighbor.ActiveTreaties = new List<string>();

                if ((neighbor.Alliance || neighbor.IsAlly) && !HasActiveTreaty(state, neighbor.Id, "DefensiveAlliance", "OffensiveAlliance", "MarriageAlliance"))
                    state.Treaties.Add(CreateLegacyTreaty(state, neighbor, neighbor.Alliance ? "MarriageAlliance" : "DefensiveAlliance", 360 * 5));

                if (neighbor.HasNonAggressionPact && !HasActiveTreaty(state, neighbor.Id, "NonAggressionPact"))
                    state.Treaties.Add(CreateLegacyTreaty(state, neighbor, "NonAggressionPact", 360 * 3));

                if (neighbor.TradeTreaty && !HasActiveTreaty(state, neighbor.Id, "TradeAgreement"))
                    state.Treaties.Add(CreateLegacyTreaty(state, neighbor, "TradeAgreement", 360 * 3));
            }

            foreach (var neighbor in state.Neighbors)
            {
                var activeTreaties = state.Treaties
                    .Where(t => t.IsActive && t.KingdomBId == neighbor.Id)
                    .ToList();

                bool hasAlliance = activeTreaties.Any(t => IsAllianceTreaty(t.TreatyType));
                bool hasNonAggression = activeTreaties.Any(t => t.TreatyType == "NonAggressionPact");
                bool hasTrade = activeTreaties.Any(t => t.TreatyType == "TradeAgreement");

                neighbor.Alliance = hasAlliance;
                neighbor.IsAlly = hasAlliance;
                neighbor.HasNonAggressionPact = hasNonAggression;
                neighbor.TradeTreaty = hasTrade;
                neighbor.ActiveTreaties = activeTreaties.Select(t => t.Id).ToList();

                if (neighbor.IsAtWarWithPlayer || neighbor.Relation == "حرب")
                {
                    neighbor.Relation = "حرب";
                    neighbor.DiplomaticStance = "Hostile";
                }
                else if (hasAlliance)
                {
                    neighbor.Relation = "تحالف";
                    neighbor.DiplomaticStance = "Allied";
                }
                else if (hasTrade)
                {
                    neighbor.Relation = "تحالف تجاري";
                }
                else if (string.IsNullOrWhiteSpace(neighbor.Relation))
                {
                    neighbor.Relation = neighbor.Opinion < -40 ? "عدائية" : "حياد";
                }
            }
        }

        public static bool HasActiveTreaty(GameState state, string targetKingdomId, params string[] treatyTypes)
        {
            if (state?.Treaties == null) return false;
            int today = GetCurrentDayNumber(state);
            var typeSet = treatyTypes == null || treatyTypes.Length == 0 ? null : new HashSet<string>(treatyTypes);

            return state.Treaties.Any(t =>
                t.IsActive &&
                t.KingdomBId == targetKingdomId &&
                (typeSet == null || typeSet.Contains(t.TreatyType)) &&
                (t.EndDateDays <= 0 || t.EndDateDays >= today));
        }

        public static (bool CanDeclare, string Reason) CanDeclareWar(GameState state, string targetKingdomId)
        {
            if (state == null)
                return (false, "حالة اللعبة غير جاهزة.");

            SynchronizeDiplomacyState(state);

            if (state.ActiveWar != null)
                return (false, "لا يمكن إعلان حرب جديدة بينما هناك حرب قائمة بالفعل. أنهِ الحرب الحالية أولاً.");

            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            if (neighbor == null)
                return (false, "المملكة المستهدفة غير موجودة.");

            if (neighbor.IsAtWarWithPlayer || neighbor.Relation == "حرب")
                return (false, $"أنت بالفعل في حالة حرب مع {neighbor.Name}.");

            if (neighbor.IsAlly || neighbor.Alliance || HasActiveTreaty(state, targetKingdomId, "DefensiveAlliance", "OffensiveAlliance", "MarriageAlliance"))
                return (false, $"لا يمكن إعلان الحرب على {neighbor.Name} لأنها حليفة لك. اكسر التحالف أولاً من شاشة الدبلوماسية المتقدمة.");

            if (neighbor.HasNonAggressionPact || HasActiveTreaty(state, targetKingdomId, "NonAggressionPact"))
                return (false, $"لا يمكن إعلان الحرب على {neighbor.Name} بسبب معاهدة عدم اعتداء نشطة.");

            if (HasActiveTreaty(state, targetKingdomId, "PeaceTreaty"))
                return (false, $"لا يمكن إعلان الحرب على {neighbor.Name} أثناء سريان معاهدة السلام.");

            return (true, "");
        }

        public static string ProcessDailyTreaties(GameState state)
        {
            if (state == null) return "";

            int today = GetCurrentDayNumber(state);
            var expired = new List<string>();
            foreach (var treaty in state.Treaties)
            {
                if (treaty.IsActive && treaty.EndDateDays > 0 && treaty.EndDateDays < today)
                {
                    treaty.IsActive = false;
                    var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == treaty.KingdomBId);
                    expired.Add(neighbor == null
                        ? $"انتهت معاهدة من نوع {treaty.TreatyType}."
                        : $"انتهت معاهدة {GetTreatyDisplayName(treaty.TreatyType)} مع {neighbor.Name}.");
                }
            }

            SynchronizeDiplomacyState(state);
            return expired.Count == 0 ? "" : string.Join("\n", expired);
        }

        private static DiplomaticTreaty CreateLegacyTreaty(GameState state, Neighbor neighbor, string treatyType, int durationDays)
        {
            int today = GetCurrentDayNumber(state);
            return new DiplomaticTreaty
            {
                TreatyType = treatyType,
                KingdomAId = "Player",
                KingdomBId = neighbor.Id,
                StartDateDays = today,
                DurationDays = durationDays,
                EndDateDays = today + durationDays,
                IsActive = true,
                BreakPenalty = IsAllianceTreaty(treatyType) ? 50 : 25,
                Notes = "معاهدة تمت مزامنتها من حفظ قديم."
            };
        }

        private static bool IsAllianceTreaty(string treatyType)
        {
            return treatyType == "DefensiveAlliance" || treatyType == "OffensiveAlliance" || treatyType == "MarriageAlliance";
        }

        private static string GetTreatyDisplayName(string treatyType)
        {
            return treatyType switch
            {
                "NonAggressionPact" => "عدم الاعتداء",
                "DefensiveAlliance" => "التحالف الدفاعي",
                "OffensiveAlliance" => "التحالف الهجومي",
                "MarriageAlliance" => "تحالف الزواج",
                "TradeAgreement" => "التجارة",
                "PeaceTreaty" => "السلام",
                _ => treatyType
            };
        }

        public static string GetNeighborInfo(GameState state, int neighborIdx)
        {
            var n = state.Neighbors[neighborIdx];
            
            var occupiedByThem = new List<string>();
            foreach (var p in state.Provinces)
            {
                if (p.OccupiedBy == n.Name)
                {
                    occupiedByThem.Add(p.Name);
                }
            }
            
            string occupiedStr = "";
            if (occupiedByThem.Count > 0)
            {
                occupiedStr = $"\n⚠️ مقاطعات احتلها هذا العدو: {string.Join(", ", occupiedByThem)}";
            }
            
            string claimStr = "";
            if (n.HasClaim && !string.IsNullOrEmpty(n.ClaimedProvince))
            {
                claimStr = $"\n⚔️ مطالبة شرعية نشطة على مقاطعة: {n.ClaimedProvince}";
            }
            else if (n.HasClaim)
            {
                claimStr = "\n⚔️ مطالبة شرعية نشطة";
            }
            
            string allianceStr = n.Alliance ? "نعم (حليف عسكري)" : "لا";
            string tradeStr = n.TradeTreaty ? "نعم" : "لا";
            
            return $"الدبلوماسية والسياسة مع: {n.Name}\n\n" +
                   $"العاصمة: {n.Capital}\n" +
                   $"الحاكم: {n.Ruler}\n" +
                   $"الديانة الطائفية المعتمدة: {n.Religion}\n" +
                   $"القوة العسكرية النشطة: {n.Army} مقاتلاً\n" +
                   $"الحالة الدبلوماسية: {n.Relation}\n" +
                   $"رأيه بجلالتكم: {n.Opinion}\n" +
                   $"معاهدة تجارية نشطة: {tradeStr}\n" +
                   $"تحالف عسكري (زواج): {allianceStr}" +
                   $"{claimStr}{occupiedStr}\n\n" +
                   "ما هي أوامرك للدبلوماسية الخارجية؟";
        }

        public static string SendEnvoy(GameState state, int neighborIdx)
        {
            var n = state.Neighbors[neighborIdx];
            if (state.Gold < 50)
            {
                return "لا تملك الذهب الكافي (50 ذهب) لإرسال سفير!";
            }
            state.Gold -= 50;
            n.Opinion = Math.Min(100, n.Opinion + 15);
            return $"أرسلت سفيراً ملكياً محملاً بأثمن الهدايا لبلاط {n.Name}. تحسن رأي حاكمهم تجاهك بمقدار 15 نقطة بتكلفة 50 ذهباً.";
        }

        public static string OfferTrade(GameState state, int neighborIdx)
        {
            SynchronizeDiplomacyState(state);
            var n = state.Neighbors[neighborIdx];
            if (n.Opinion < 25)
            {
                return $"رفض حاكم {n.Name} توقيع المعاهدة التجارية لأن رأيه بك سلبي أو منخفض! ({n.Opinion} رأي، ويتطلب 25).";
            }

            n.TradeTreaty = true;
            n.Relation = "تحالف تجاري";
            if (!HasActiveTreaty(state, n.Id, "TradeAgreement"))
            {
                int today = GetCurrentDayNumber(state);
                state.Treaties.Add(new DiplomaticTreaty
                {
                    TreatyType = "TradeAgreement",
                    KingdomAId = "Player",
                    KingdomBId = n.Id,
                    StartDateDays = today,
                    DurationDays = 360 * 3,
                    EndDateDays = today + 360 * 3,
                    BreakPenalty = 10,
                    Notes = $"تجارة شهرية مع {n.Name}"
                });
            }

            SynchronizeDiplomacyState(state);
            return $"قبل حاكم {n.Name} عرضك بكل سرور! تم توقيع المعاهدة التجارية وتدفق القوافل التجارية (+30 ذهب شهرياً).";
        }

        public static string CancelTrade(GameState state, int neighborIdx)
        {
            SynchronizeDiplomacyState(state);
            var n = state.Neighbors[neighborIdx];
            n.TradeTreaty = false;
            n.Relation = "حياد";
            foreach (var treaty in state.Treaties.Where(t => t.KingdomBId == n.Id && t.TreatyType == "TradeAgreement" && t.IsActive))
                treaty.IsActive = false;

            SynchronizeDiplomacyState(state);
            return $"ألغيت معاهدة التجارة مع {n.Name}. انقطع الدخل الإضافي وعادت العلاقة للحياد الطبيعي.";
        }

        public static string ForgeClaim(GameState state, int neighborIdx, string provinceName)
        {
            var n = state.Neighbors[neighborIdx];
            if (state.Gold < 100)
            {
                return "عذراً، لا تملك الذهب الكافي (100 ذهب) لتمويل عملية تزوير المطالبة!";
            }

            state.Gold -= 100;
            state.Prestige -= 15;
            n.HasClaim = true;
            n.ClaimedProvince = provinceName;

            return $"أرسلت جواسيسك لتزوير صكوك ملكية قديمة بأحقيتك في مقاطعة {provinceName} التابعة لـ {n.Name}. نجحت العملية! حصلت على مطالبة شرعية (Casus Belli) لغزو {provinceName} بتكلفة 100 ذهب و15 هيبة.";
        }

        public static string ExecuteReplaceVassal(GameState state, int provinceIdx, string religion, int opinionMod, int satisfactionCost)
        {
            var p = state.Provinces[provinceIdx];
            string oldVassal = p.Vassal;

            string[] christianNames = { "الوالي ميخائيل", "الوالي يوحنا", "الأمير جرجس", "الوالي أندراوس", "الوالي بطرس" };
            string[] muslimNames = { "الأمير خالد", "الوالي حمزة", "الأمير جعفر", "الوالي أسامة", "الأمير صهيب" };

            string[] vassalNames = religion.Contains("مسيحي") ? christianNames : muslimNames;

            string newVassal = vassalNames[rand.Next(vassalNames.Length)];
            while (newVassal == oldVassal)
            {
                newVassal = vassalNames[rand.Next(vassalNames.Length)];
            }

            p.Vassal = newVassal;
            p.VassalReligion = religion;
            p.Opinion = rand.Next(-10, 31) + opinionMod;
            p.Satisfaction = Math.Max(10, p.Satisfaction - satisfactionCost);

            string satisfactionMsg = $"تراجع رضا الرعية المحلية بمقدار -{satisfactionCost}%.";
            if (satisfactionCost <= 15)
            {
                satisfactionMsg += " (الرعية راضية نسبياً بتعيين والٍ من نفس طائفتهم مما خفف توتر العزل القسري)";
            }

            return $"تم عزل {oldVassal} وتعيين {newVassal} ({religion}) والياً جديداً على {p.Name}. {satisfactionMsg} حصل الوالي الجديد على ولاء أولي قدره {p.Opinion}.";
        }

        public static GameActionResult SendGift(GameState state, string targetKingdomId, int goldAmount)
        {
            var res = new GameActionResult { Title = "إرسال هدية دبلوماسية" };
            SynchronizeDiplomacyState(state);
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            if (neighbor == null) { res.Success = false; res.MainMessage = "المملكة غير موجودة."; return res; }
            if (state.Gold < goldAmount) { res.Success = false; res.MainMessage = "لا تملك الذهب الكافي."; return res; }

            state.Gold -= goldAmount;
            int opinionBoost = goldAmount / 100;
            neighbor.Opinion += opinionBoost;
            neighbor.OpinionOfKing += opinionBoost;
            neighbor.Trust += Math.Max(0, opinionBoost / 2);
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", -goldAmount);
            res.MainMessage = $"أرسلت هدية بقيمة {goldAmount} ذهب إلى {neighbor.Name}. زاد رأيهم بك بمقدار {opinionBoost}.";
            return res;
        }

        public static GameActionResult SignNonAggressionPact(GameState state, string targetKingdomId)
        {
            var res = new GameActionResult { Title = "توقيع معاهدة عدم اعتداء" };
            SynchronizeDiplomacyState(state);
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            
            if (neighbor == null) { res.Success = false; res.MainMessage = "المملكة غير موجودة."; return res; }
            if (neighbor.HasNonAggressionPact) { res.Success = false; res.MainMessage = "يوجد بالفعل معاهدة عدم اعتداء."; return res; }
            
            if (neighbor.Opinion < 0 || neighbor.Trust < 20)
            {
                res.Success = false;
                res.MainMessage = $"رفض حاكم {neighbor.Name} المعاهدة بسبب ضعف الثقة أو العداء.";
                return res;
            }

            var treaty = new DiplomaticTreaty
            {
                TreatyType = "NonAggressionPact",
                KingdomAId = "Player",
                KingdomBId = neighbor.Id,
                StartDateDays = GetCurrentDayNumber(state),
                DurationDays = 360 * 3, // 3 years
                EndDateDays = GetCurrentDayNumber(state) + 360 * 3,
                TrustEffect = 10,
                OpinionEffect = 10,
                BreakPenalty = 30,
                Notes = $"تمنع إعلان الحرب لمدة 3 سنوات بينك وبين {neighbor.Name}"
            };

            state.Treaties.Add(treaty);
            neighbor.HasNonAggressionPact = true;
            neighbor.Trust += 10;
            neighbor.Opinion += 10;
            neighbor.OpinionOfKing += 10;
            SynchronizeDiplomacyState(state);
            
            res.Success = true;
            res.MainMessage = $"نجحت الدبلوماسية! تم توقيع معاهدة عدم اعتداء مع {neighbor.Name} لمدة 3 سنوات.";
            return res;
        }

        public static GameActionResult SignAlliance(GameState state, string targetKingdomId, bool isOffensive)
        {
            var res = new GameActionResult { Title = isOffensive ? "توقيع تحالف هجومي" : "توقيع تحالف دفاعي" };
            SynchronizeDiplomacyState(state);
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            if (neighbor == null) { res.Success = false; res.MainMessage = "المملكة غير موجودة."; return res; }
            if (neighbor.IsAlly) { res.Success = false; res.MainMessage = "هذه المملكة حليفة لك مسبقاً."; return res; }

            int requiredTrust = isOffensive ? 80 : 50;
            int requiredOpinion = isOffensive ? 60 : 30;

            if (neighbor.Trust < requiredTrust || neighbor.Opinion < requiredOpinion)
            {
                res.Success = false;
                res.MainMessage = $"رفضت مملكة {neighbor.Name} التحالف. ثقتهم ورأيهم بك لا يكفيان لمثل هذه المعاهدة العسكرية.";
                return res;
            }

            var treaty = new DiplomaticTreaty
            {
                TreatyType = isOffensive ? "OffensiveAlliance" : "DefensiveAlliance",
                KingdomAId = "Player",
                KingdomBId = neighbor.Id,
                StartDateDays = GetCurrentDayNumber(state),
                DurationDays = 360 * 5, // 5 years
                EndDateDays = GetCurrentDayNumber(state) + 360 * 5,
                BreakPenalty = 50,
                Notes = isOffensive ? "يسمح بطلب الدعم في الحروب الهجومية والدفاعية" : "يسمح بطلب الدعم في الحروب الدفاعية فقط"
            };

            state.Treaties.Add(treaty);
            neighbor.IsAlly = true;
            neighbor.Alliance = true;
            neighbor.Trust += 15;
            neighbor.Opinion += 20;
            SynchronizeDiplomacyState(state);

            res.Success = true;
            res.MainMessage = $"تم توقيع التحالف بنجاح مع {neighbor.Name}. قواتهم ستدعمك عند الحاجة.";
            return res;
        }

        public static GameActionResult RequestPoliticalLoan(GameState state, string targetKingdomId, int amount)
        {
            var res = new GameActionResult { Title = "طلب قرض سياسي" };
            SynchronizeDiplomacyState(state);
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            if (neighbor == null) { res.Success = false; res.MainMessage = "المملكة غير موجودة."; return res; }
            
            if (neighbor.EconomicStrength < 40 || neighbor.Opinion < 20)
            {
                res.Success = false;
                res.MainMessage = $"رفضت {neighbor.Name} منحك قرضاً سياسياً بسبب ضعف اقتصادهم أو علاقتهم بك.";
                return res;
            }

            var treaty = new DiplomaticTreaty
            {
                TreatyType = "PoliticalLoanAgreement",
                KingdomAId = "Player",
                KingdomBId = neighbor.Id,
                StartDateDays = GetCurrentDayNumber(state),
                DurationDays = 360 * 2,
                EndDateDays = GetCurrentDayNumber(state) + 360 * 2,
                BreakPenalty = 40,
                Notes = "قرض بدون فوائد مالية مقابل التزام سياسي بدعمهم أو عدم الاعتداء"
            };

            var loan = new Loan
            {
                LenderType = "ForeignKingdom",
                LenderName = neighbor.Name,
                PrincipalAmount = amount,
                RemainingAmount = amount,
                StartDateDays = GetCurrentDayNumber(state),
                DueDateDays = GetCurrentDayNumber(state) + 360 * 2,
                RepaymentMode = "Automatic",
                ScheduledPaymentAmount = Math.Max(1, amount / (360 * 2)),
                PoliticalCondition = "دعم سياسي وتنازلات"
            };

            state.Treaties.Add(treaty);
            state.Loans.Add(loan);
            state.Gold += amount;
            neighbor.Trust += 5; // they trust you to pay back
            SynchronizeDiplomacyState(state);
            
            res.Success = true;
            res.ResourceChanges.Add("الذهب", amount);
            res.MainMessage = $"وافقت {neighbor.Name} على منحك قرضاً بقيمة {amount} ذهب بدون فوائد، مقابل التزامات سياسية موثقة في معاهدة.";
            return res;
        }

        public static GameActionResult AccuseOfEspionage(GameState state, string targetKingdomId)
        {
            var res = new GameActionResult { Title = "اتهام بدعم فصيل" };
            SynchronizeDiplomacyState(state);
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            if (neighbor == null) { res.Success = false; res.MainMessage = "المملكة غير موجودة."; return res; }

            if (neighbor.IsSuspectedOfEspionage)
            {
                res.Success = true;
                neighbor.Opinion -= 30;
                neighbor.Trust -= 40;
                neighbor.IsSuspectedOfEspionage = false; // Resolved
                neighbor.Relation = "متوتر";
                res.MainMessage = $"قمت بتوجيه اتهام رسمي لـ {neighbor.Name} بدعم فصائل متمردة. تدهورت العلاقات بشدة، لكنهم أوقفوا دعمهم المؤقت خشية الفضيحة.";
                return res;
            }
            else
            {
                res.Success = true;
                neighbor.Opinion -= 50;
                neighbor.Trust -= 50;
                state.Prestige -= 20;
                res.MainMessage = $"قمت باتهام {neighbor.Name} بدون دليل قاطع! لقد اعتبروا هذا إهانة دبلوماسية خطيرة، وفقدت هيبتك أمام الممالك الأخرى.";
                return res;
            }
        }
        
        public static GameActionResult BreakTreaty(GameState state, string treatyId)
        {
            var res = new GameActionResult { Title = "خرق معاهدة" };
            SynchronizeDiplomacyState(state);
            var treaty = state.Treaties.FirstOrDefault(t => t.Id == treatyId);
            if (treaty == null) { res.Success = false; res.MainMessage = "المعاهدة غير موجودة."; return res; }

            state.Treaties.Remove(treaty);
            state.Prestige -= treaty.BreakPenalty;
            state.ReligiousLegitimacy -= (treaty.BreakPenalty / 2); // Clergy hates breaking oaths
            
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == treaty.KingdomBId);
            if (neighbor != null)
            {
                neighbor.Opinion -= treaty.BreakPenalty;
                neighbor.Trust -= treaty.BreakPenalty;
                if (treaty.TreatyType == "NonAggressionPact") neighbor.HasNonAggressionPact = false;
                if (treaty.TreatyType == "DefensiveAlliance" || treaty.TreatyType == "OffensiveAlliance" || treaty.TreatyType == "MarriageAlliance")
                {
                    neighbor.IsAlly = false;
                    neighbor.Alliance = false;
                }
                if (treaty.TreatyType == "TradeAgreement") neighbor.TradeTreaty = false;
                LivingRealmSystem.AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "BrokenTreaty", $"خرق الملك معاهدة من نوع {treaty.TreatyType}.", 0, 0, 0, 3, 900, false);
                LivingRealmSystem.AdjustRoyalReputation(state, "OathBreaker", Math.Max(5, treaty.BreakPenalty / 2));
                
                res.MainMessage = $"لقد قمت بخرق المعاهدة مع {neighbor.Name}! فقدت {treaty.BreakPenalty} من الهيبة، وتراجعت شرعيتك الدينية، وغضبوا بشدة.";
            }
            else
            {
                res.MainMessage = $"لقد قمت بخرق المعاهدة. فقدت {treaty.BreakPenalty} هيبة.";
            }

            res.Success = true;
            SynchronizeDiplomacyState(state);
            return res;
        }
    }
}
