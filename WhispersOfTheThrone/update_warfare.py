import re
import codecs

# Detect encoding
with open('Systems/WarfareSystem.cs', 'rb') as f:
    raw = f.read(2)
encoding = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('Systems/WarfareSystem.cs', 'r', encoding=encoding) as f:
    content = f.read()

pattern = r'public static GameActionResult DeclareWar\(GameState state, int neighborIndex, bool forceUnjustWar\)[\s\S]*?neighbor\.Relation = "عدو";'
replacement = r'''public static GameActionResult DeclareWar(GameState state, int neighborIndex, string casusBelliType)
        {
            GameMonitorSystem.Log("WARFARE", "DeclareWar: Initiated with CB " + casusBelliType);
            try
            {
                var res = new GameActionResult { Title = "إعلان الحرب" };

                if (neighborIndex < 0 || neighborIndex >= state.Neighbors.Count)
                {
                    res.Success = false; res.MainMessage = "الجار غير صالح."; return res;
                }

                var neighbor = state.Neighbors[neighborIndex];
                var warPermission = DiplomacySystem.CanDeclareWar(state, neighbor.Id);

                if (!warPermission.CanDeclare)
                {
                    res.Success = false;
                    res.MainMessage = warPermission.Reason;
                    return res;
                }

                if (!warPermission.CasusBellis.Contains(casusBelliType))
                {
                    res.Success = false;
                    res.MainMessage = "سبب الحرب غير صالح.";
                    return res;
                }

                int totalArmy = state.Armies.Sum(a => a.TotalSoldiers);

                if (totalArmy < 100)
                {
                    res.Success = false; res.MainMessage = "جيشك صغير جداً لشن هجوم الآن."; return res;
                }

                if (string.IsNullOrEmpty(neighbor.ClaimedProvince))
                {
                    if (neighbor.ClaimableProvinces.Count > 0)
                        neighbor.ClaimedProvince = neighbor.ClaimableProvinces[0].Name;
                    else
                    {
                        res.Success = false; res.MainMessage = "لا يوجد مقاطعة محددة للحرب من أجلها."; return res;
                    }
                }

                if (casusBelliType == "HolyWar")
                {
                    state.Piety -= 100;
                    res.Warnings.Add("الحرب المقدسة كلفتك الكثير من التقى والإيمان.");
                }
                else if (casusBelliType == "Subjugation")
                {
                    state.Prestige -= 1000;
                    res.Warnings.Add("تم استخدام حرب الإخضاع! استهلكت هيبتك بالكامل.");
                }
                else
                {
                    state.Prestige -= 50; // Claim CB cost
                }

                neighbor.Relation = "عدو";'''

content = re.sub(pattern, replacement, content)

with open('Systems/WarfareSystem.cs', 'w', encoding=encoding) as f:
    f.write(content)
