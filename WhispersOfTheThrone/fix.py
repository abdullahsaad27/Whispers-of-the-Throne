# -*- coding: utf-8 -*-
import codecs
with codecs.open('Systems/DiplomacySystem.cs', 'r', 'utf-8') as f:
    lines = f.readlines()
idx = 502
new_code = '''        public static GameActionResult ProposeVassalOffer(GameState state, string targetKingdomId)
        {
            return new GameActionResult { Success = false, MainMessage = "غير مدعوم حاليا." };
        }

        public static GameActionResult BreakTreaty(GameState state, string treatyId)
        {
            var res = new GameActionResult { Title = "خرق معاهدة" };
            SynchronizeDiplomacyState(state);
            var treaty = state.Treaties.FirstOrDefault(t => t.Id == treatyId);
            if (treaty == null) { res.Success = false; res.MainMessage = "المعاهدة غير موجودة."; return res; }

            state.Treaties.Remove(treaty);
            state.Prestige -= treaty.BreakPenalty;
            state.ReligiousLegitimacy -= (treaty.BreakPenalty / 2);
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
                LivingRealmSystem.AddMemory(state, "Neighbor", neighbor.Id, neighbor.Name, "BrokenTreaty", "خرق الملك معاهدة.", 0, 0, 0, 3, 900, false);
                LivingRealmSystem.AdjustRoyalReputation(state, "OathBreaker", Math.Max(5, treaty.BreakPenalty / 2));
                res.MainMessage = "لقد قمت بخرق المعاهدة!";
            }
            else
            {
                res.MainMessage = "لقد قمت بخرق المعاهدة.";
            }

            res.Success = true;
            SynchronizeDiplomacyState(state);
            return res;
        }

        public static GameActionResult ArrangeMarriageAlliance(GameState state, string targetKingdomId)
        {
            return ArrangeMarriageAlliance(state, targetKingdomId, "Child_1");
        }

        public static GameActionResult DeclareWar(GameState state, string targetKingdomId)
        {
            return WarfareSystem.DeclareWar(state, state.Neighbors.FindIndex(n => n.Id == targetKingdomId), false);
        }

        public static GameActionResult ProposeVassalOffer(GameState state, string governorId, string offerType, int goldAmount)
        {
            return LivingRealmSystem.ProposeVassalOffer(state, governorId, offerType, goldAmount);
        }
    }
}
'''
with codecs.open('Systems/DiplomacySystem.cs', 'w', 'utf-8') as f:
    f.writelines(lines[:idx])
    f.write(new_code)
