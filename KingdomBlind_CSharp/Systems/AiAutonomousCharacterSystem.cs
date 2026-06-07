using System.Linq;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiAutonomousCharacterSystem
    {
        public static GameActionResult ProcessMonthlyAiCharacters(GameState state)
        {
            var result = new GameActionResult { Title = "قرارات الشخصيات بالذكاء الاصطناعي", Success = true, ShouldNarrate = false };
            state.ReconcileOldSaves();
            if (state.SuppressRandomMajorEvents)
                return result;

            var config = AppConfig.Load();
            config.AiActors ??= new AiActorSettings();

            if (config.AiProvider == null || config.AiProvider.ProviderType == AiProviderType.Disabled)
                return result;

            LivingRealmEvent realmEvent = null;

            if (config.AiActors.AllowAiMinisterDecisions &&
                !state.LivingRealmLog.Any(e => !e.IsResolved && e.EventType == "AiMinisterCouncilProposal"))
            {
                var minister = state.Council.Values
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name))
                    .OrderByDescending(c => c.Influence + c.Ambition)
                    .FirstOrDefault();
                if (minister != null)
                {
                    string line = SuperTonicAI.GenerateDialogue(minister.Title, state, "اقتراح سياسة شهرية للمجلس");
                    realmEvent = CreateEvent(state, "AiMinisterCouncilProposal", "Councilor", minister.Title, minister.Name,
                        $"اقتراح من {minister.Name}",
                        $"{minister.Name} يتقدم باقتراح أمام المجلس:\n\"{line}\"",
                        "يمكنك قبول الاقتراح كمسار سياسي، أو طلب تفاصيل، أو رفضه حتى لا يكبر نفوذه.");
                }
            }

            if (realmEvent == null &&
                config.AiActors.AllowAiSpouseDecisions &&
                !state.LivingRealmLog.Any(e => !e.IsResolved && e.EventType == "AiSpouseCourtProposal"))
            {
                var wife = state.Wives.Where(w => !w.IsDead).OrderByDescending(w => w.Influence + w.Ambition).FirstOrDefault();
                if (wife != null)
                {
                    string line = SuperTonicAI.GenerateDialogue(wife.Name, state, "طلب سياسي من زوجة داخل القصر");
                    realmEvent = CreateEvent(state, "AiSpouseCourtProposal", "Spouse", wife.Id, wife.Name,
                        $"طلب من {wife.Name}",
                        $"{wife.Name} تطلب لقاءً خاصاً وتقول:\n\"{line}\"",
                        "دعمها يزيد نفوذ جناحها، وتأجيلها يكسب وقتاً، ورفضها قد يضعف العلاقة.");
                }
            }

            if (realmEvent == null &&
                config.AiActors.AllowAiNeighborDecisions &&
                !state.LivingRealmLog.Any(e => !e.IsResolved && e.EventType == "AiNeighborAudienceInvitation"))
            {
                var neighbor = state.Neighbors
                    .Where(n => !n.IsAtWarWithPlayer)
                    .OrderByDescending(n => n.Opinion + n.Trust + n.MilitaryAmbition)
                    .FirstOrDefault();
                if (neighbor != null)
                {
                    string ruler = string.IsNullOrWhiteSpace(neighbor.RulerName) ? neighbor.Ruler : neighbor.RulerName;
                    string line = SuperTonicAI.GenerateDialogue(ruler, state, $"دعوة دبلوماسية من {neighbor.Name}");
                    realmEvent = CreateEvent(state, "AiNeighborAudienceInvitation", "Neighbor", neighbor.Id, neighbor.Name,
                        $"دعوة من {neighbor.Name}",
                        $"{ruler} يرسل إلى بغداد رسالة بصوته السياسي:\n\"{line}\"",
                        "قبول الحوار قد يفتح باب ثقة أو معاهدة. الرفض يحفظ وقتك لكنه قد يبرد العلاقة.");
                }
            }

            if (realmEvent == null)
                return result;

            state.LivingRealmLog.Add(realmEvent);
            result.ShouldNarrate = true;
            result.ShouldPauseTime = true;
            result.MainMessage = $"{realmEvent.Title}\n{realmEvent.Description}";
            result.SoundEffectKey = "paper";
            return result;
        }

        public static GameActionResult ResolveAiCharacterEvent(GameState state, LivingRealmEvent realmEvent, string choice)
        {
            var result = new GameActionResult { Title = "قرار شخصية مدعومة بالذكاء الاصطناعي" };
            state.ReconcileOldSaves();

            switch (realmEvent.EventType)
            {
                case "AiMinisterCouncilProposal":
                    ResolveMinister(state, realmEvent, choice, result);
                    break;
                case "AiSpouseCourtProposal":
                    ResolveSpouse(state, realmEvent, choice, result);
                    break;
                case "AiNeighborAudienceInvitation":
                    ResolveNeighbor(state, realmEvent, choice, result);
                    break;
                default:
                    result.Success = true;
                    result.MainMessage = "تم إغلاق الحدث.";
                    break;
            }

            if (result.Success)
                realmEvent.IsResolved = true;

            return result;
        }

        private static LivingRealmEvent CreateEvent(GameState state, string type, string actorType, string actorId, string actorName, string title, string description, string advice)
        {
            return new LivingRealmEvent
            {
                EventType = type,
                ActorType = actorType,
                ActorId = actorId,
                ActorName = actorName,
                Title = title,
                Description = description,
                CouncilAdvice = advice,
                DateText = state.Time.GetDateString(),
                CreatedDay = DiplomacySystem.GetCurrentDayNumber(state),
                Severity = 2,
                RequiresDecision = true,
                RequiresPause = true
            };
        }

        private static void ResolveMinister(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var minister = state.Council.Values.FirstOrDefault(c => c.Title == realmEvent.ActorId || c.Name == realmEvent.ActorName);
            if (choice == "Accept")
            {
                if (minister != null)
                {
                    minister.Trust = System.Math.Clamp(minister.Trust + 5, 0, 100);
                    minister.Influence = System.Math.Clamp(minister.Influence + 3, 0, 100);
                }
                state.Prestige += 3;
                result.Success = true;
                result.MainMessage = "قبلت اقتراح الوزير. زادت ثقته ونفوذه قليلاً، وظهر المجلس أكثر حيوية.";
            }
            else if (choice == "Details")
            {
                if (minister != null) minister.Trust = System.Math.Clamp(minister.Trust + 2, 0, 100);
                result.Success = true;
                result.MainMessage = "طلبت تفاصيل إضافية. شعر الوزير أن رأيه مسموع دون منحه تفويضاً كاملاً.";
            }
            else
            {
                if (minister != null) minister.Loyalty = System.Math.Clamp(minister.Loyalty - 3, 0, 100);
                result.Success = true;
                result.MainMessage = "رفضت الاقتراح. بقي القرار بيدك، لكن الوزير خرج أقل حماسة.";
            }
        }

        private static void ResolveSpouse(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var wife = state.Wives.FirstOrDefault(w => w.Id == realmEvent.ActorId);
            if (wife == null)
            {
                result.Success = false;
                result.MainMessage = "لم يتم العثور على الزوجة المرتبطة بالحدث.";
                return;
            }

            if (choice == "Support")
            {
                wife.OpinionOfKing = System.Math.Clamp(wife.OpinionOfKing + 8, -100, 100);
                wife.Influence = System.Math.Clamp(wife.Influence + 4, 0, 100);
                result.Success = true;
                result.MainMessage = $"دعمت {wife.Name}. تحسن رأيها بك، وازداد نفوذ جناحها داخل القصر.";
            }
            else if (choice == "Delay")
            {
                wife.Trust = System.Math.Clamp(wife.Trust - 2, 0, 100);
                result.Success = true;
                result.MainMessage = "أجلت الطلب. كسبت وقتاً، لكنها شعرت أن القصر لا يصغي بالكامل.";
            }
            else
            {
                wife.OpinionOfKing = System.Math.Clamp(wife.OpinionOfKing - 8, -100, 100);
                result.Success = true;
                result.MainMessage = "رفضت الطلب. ضعفت العلاقة، وقد تبحث عن حلفاء آخرين.";
            }
        }

        private static void ResolveNeighbor(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == realmEvent.ActorId);
            if (neighbor == null)
            {
                result.Success = false;
                result.MainMessage = "لم يتم العثور على الدولة المرتبطة بالحدث.";
                return;
            }

            if (choice == "Accept")
            {
                neighbor.Opinion = System.Math.Clamp(neighbor.Opinion + 8, -100, 100);
                neighbor.Trust = System.Math.Clamp(neighbor.Trust + 6, 0, 100);
                result.Success = true;
                result.MainMessage = $"قبلت الحوار مع {neighbor.Name}. تحسنت الثقة وهدأ التوتر الدبلوماسي.";
            }
            else if (choice == "Envoy")
            {
                if (state.Gold < 50)
                {
                    result.Success = false;
                    result.MainMessage = "تحتاج إلى 50 ذهب لإرسال مبعوث مناسب.";
                    return;
                }
                state.Gold -= 50;
                neighbor.Opinion = System.Math.Clamp(neighbor.Opinion + 12, -100, 100);
                result.Success = true;
                result.ResourceChanges.Add("الذهب", -50);
                result.MainMessage = $"أرسلت مبعوثاً إلى {neighbor.Name}. تحسنت العلاقة بوضوح.";
            }
            else
            {
                neighbor.Opinion = System.Math.Clamp(neighbor.Opinion - 5, -100, 100);
                result.Success = true;
                result.MainMessage = $"رفضت دعوة {neighbor.Name}. لم تقع أزمة، لكن العلاقة بردت قليلاً.";
            }
        }
    }
}
