import re
import codecs

# Detect encoding
with open('MainForm.cs', 'rb') as f:
    raw = f.read(2)
encoding = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('MainForm.cs', 'r', encoding=encoding) as f:
    content = f.read()

# First replace in ShowPoliticalMap
pattern1 = r'            if \(n\.HasClaim\)\n            \{\n                AddActionButton\(\$"\S+ \S+ \S+ \{n\.ClaimedProvince\} \?\?", \(s, e\) => \{\n                    var res = WarfareSystem\.DeclareWar\(state, neighborIdx, false\);\n                    HandleActionResult\(res, \(\) => ShowPoliticalMap\(null, null\)\);\n                \}\);\n            \}\n            else if \(n\.Relation != "عدو"\)\n            \{\n                AddActionButton\(\$"\S+ \S+ \S+ \(\S+ \S+ \S+ \S+\) \?\?", \(s, e\) => \{\n                    var res = WarfareSystem\.DeclareWar\(state, neighborIdx, true\);\n                    HandleActionResult\(res, \(\) => ShowPoliticalMap\(null, null\)\);\n                \}\);\n            \}'

replacement1 = r'''            var warPermission = DiplomacySystem.CanDeclareWar(state, n.Id);
            if (n.Relation != "عدو" && warPermission.CanDeclare && warPermission.CasusBellis.Count > 0)
            {
                AddActionButton($"إعلان الحرب على {n.Name} (اختيار السبب)", (s, e) => ShowDeclareWarMenu(n, neighborIdx));
            }'''

# Replace in ShowNeighborReport
pattern2 = r'            if \(neighbor\.HasClaim\)\n            \{\n                AddActionButton\(\$"\S+ \S+ \S+ \{neighbor\.ClaimedProvince\}", \(s, evt\) => \{\n                    var res = WarfareSystem\.DeclareWar\(state, neighborIndex, false\);\n                    HandleActionResult\(res, \(\) => ShowNeighborReport\(neighbor\)\);\n                \}\);\n            \}\n            else if \(neighborIndex >= 0 && neighbor\.Relation != "عدو"\)\n            \{\n                AddActionButton\("إعلان حرب غير مبررة", \(s, evt\) => \{\n                    var res = WarfareSystem\.DeclareWar\(state, neighborIndex, true\);\n                    HandleActionResult\(res, \(\) => ShowNeighborReport\(neighbor\)\);\n                \}\);\n            \}'

replacement2 = r'''            var warPermission = DiplomacySystem.CanDeclareWar(state, neighbor.Id);
            if (neighborIndex >= 0 && neighbor.Relation != "عدو" && warPermission.CanDeclare && warPermission.CasusBellis.Count > 0)
            {
                AddActionButton($"إعلان الحرب على {neighbor.Name} (اختيار السبب)", (s, evt) => ShowDeclareWarMenu(neighbor, neighborIndex));
            }'''

content = re.sub(pattern1, replacement1, content)
content = re.sub(pattern2, replacement2, content)

# Add the new method ShowDeclareWarMenu before private void ShowFactionsMenu
insert_pattern = r'        private void ShowFactionsMenu\(object sender, EventArgs e\)'
insert_replacement = r'''        private void ShowDeclareWarMenu(Neighbor neighbor, int neighborIndex)
        {
            ClearDynamicPanel();
            SetScreenTitle("إعلان الحرب (Casus Belli)");
            SetNarrativeText($"أنت على وشك إعلان الحرب على {neighbor.Name}.\nيرجى اختيار سبب الحرب (Casus Belli) المناسب:");

            var warPermission = DiplomacySystem.CanDeclareWar(state, neighbor.Id);
            
            if (warPermission.CasusBellis.Contains("Claim"))
            {
                AddActionButton($"شن حرب مطالبة (الهدف: {neighbor.ClaimedProvince})", (s, evt) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIndex, "Claim");
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }
            if (warPermission.CasusBellis.Contains("HolyWar"))
            {
                AddActionButton("شن حرب مقدسة (تتطلب 100 تقى)", (s, evt) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIndex, "HolyWar");
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }
            if (warPermission.CasusBellis.Contains("Subjugation"))
            {
                AddActionButton("شن حرب إخضاع (تتطلب 1000 هيبة)", (s, evt) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIndex, "Subjugation");
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }

            AddActionButton("تراجع (العودة للتقرير)", (s, evt) => ShowNeighborReport(neighbor));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowFactionsMenu(object sender, EventArgs e)'''

content = re.sub(insert_pattern, insert_replacement, content)

with open('MainForm.cs', 'w', encoding=encoding) as f:
    f.write(content)
