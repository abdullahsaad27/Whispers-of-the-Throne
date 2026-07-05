import re
import codecs

# Detect encoding
with open('Systems/DiplomacySystem.cs', 'rb') as f:
    raw = f.read(2)
encoding = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('Systems/DiplomacySystem.cs', 'r', encoding=encoding) as f:
    content = f.read()

pattern = r'public static \(bool CanDeclare, string Reason\) CanDeclareWar\(GameState state, string targetKingdomId\)\s*\{[\s\S]*?return \(true, ""\);\s*\}'
replacement = r'''public static (bool CanDeclare, string Reason, System.Collections.Generic.List<string> CasusBellis) CanDeclareWar(GameState state, string targetKingdomId)
        {
            var emptyCBs = new System.Collections.Generic.List<string>();
            if (state == null)
                return (false, "حالة اللعبة غير صالحة.", emptyCBs);

            SynchronizeDiplomacyState(state);

            if (state.ActiveWar != null)
                return (false, "أنت في حالة حرب بالفعل. لا يمكنك إعلان حرب أخرى.", emptyCBs);

            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == targetKingdomId);
            if (neighbor == null)
                return (false, "الجار غير موجود.", emptyCBs);

            if (neighbor.IsAtWarWithPlayer || neighbor.Relation == "عدو")
                return (false, $"أنت في حالة حرب مع {neighbor.Name}.", emptyCBs);

            if (neighbor.IsAlly || neighbor.Alliance || HasActiveTreaty(state, targetKingdomId, "DefensiveAlliance", "OffensiveAlliance", "MarriageAlliance"))
                return (false, $"لا يمكنك إعلان الحرب على حليفك {neighbor.Name}. قم بإنهاء التحالف أولاً.", emptyCBs);

            if (neighbor.HasNonAggressionPact || HasActiveTreaty(state, targetKingdomId, "NonAggressionPact"))
                return (false, $"يوجد ميثاق عدم اعتداء بينك وبين {neighbor.Name}.", emptyCBs);

            if (HasActiveTreaty(state, targetKingdomId, "PeaceTreaty"))
                return (false, $"يوجد معاهدة سلام سارية مع {neighbor.Name}.", emptyCBs);

            // Determine available Casus Bellis
            var cbs = new System.Collections.Generic.List<string>();
            
            if (neighbor.HasClaim)
                cbs.Add("Claim"); // المطالبة الشرعية أو المفبركة
            
            if (!string.IsNullOrEmpty(state.Religion) && !string.IsNullOrEmpty(neighbor.Religion) && state.Religion != neighbor.Religion)
                cbs.Add("HolyWar"); // الحرب المقدسة

            if (state.Prestige >= 1000)
                cbs.Add("Subjugation"); // حرب الإخضاع (تتطلب هيبة عالية جداً)

            if (cbs.Count == 0)
                return (false, $"لا تملك سبباً شرعياً (Casus Belli) لإعلان الحرب على {neighbor.Name}.", emptyCBs);

            return (true, "", cbs);
        }'''

content = re.sub(pattern, replacement, content)

with open('Systems/DiplomacySystem.cs', 'w', encoding=encoding) as f:
    f.write(content)
