import codecs
import re

with open('MainForm.cs', 'rb') as f:
    raw = f.read(2)
enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('MainForm.cs', 'r', encoding=enc) as f:
    content = f.read()

# Replace first occurrence in ShowPoliticalMap
pattern1 = r'''            if \(n\.HasClaim && n\.Relation != "[^"]+"\)
            \{
                AddActionButton\([^,]+, \(s, e\) => \{
                    var res = WarfareSystem\.DeclareWar\(state, neighborIdx, false\);
                    HandleActionResult\(res, \(\) => ShowPoliticalMap\(null, null\)\);
                \}\);
            \}
            else if \(n\.Relation != "[^"]+"\)
            \{
                AddActionButton\([^,]+, \(s, e\) => \{
                    var res = WarfareSystem\.DeclareWar\(state, neighborIdx, true\);
                    HandleActionResult\(res, \(\) => ShowPoliticalMap\(null, null\)\);
                \}\);
            \}'''

replacement1 = '''            if (n.Relation != "عدو")
            {
                AddActionButton("إعلان الحرب (اختيار السبب)", (s, e) => {
                    ShowDeclareWarMenu(n, neighborIdx);
                });
            }'''

# Replace second occurrence in ShowNeighborReport
pattern2 = r'''            if \(neighborIndex >= 0 && neighbor\.HasClaim && neighbor\.Relation != "[^"]+"\)
            \{
                AddActionButton\([^,]+, \(s, evt\) => \{
                    var res = WarfareSystem\.DeclareWar\(state, neighborIndex, false\);
                    HandleActionResult\(res, \(\) => ShowNeighborReport\(neighbor\)\);
                \}\);
            \}
            else if \(neighborIndex >= 0 && neighbor\.Relation != "[^"]+"\)
            \{
                AddActionButton\([^,]+, \(s, evt\) => \{
                    var res = WarfareSystem\.DeclareWar\(state, neighborIndex, true\);
                    HandleActionResult\(res, \(\) => ShowNeighborReport\(neighbor\)\);
                \}\);
            \}'''

replacement2 = '''            if (neighborIndex >= 0 && neighbor.Relation != "عدو")
            {
                AddActionButton("إعلان الحرب (اختيار السبب)", (s, evt) => {
                    ShowDeclareWarMenu(neighbor, neighborIndex);
                });
            }'''


new_content = re.sub(pattern1, replacement1, content)
new_content = re.sub(pattern2, replacement2, new_content)

with open('MainForm.cs', 'w', encoding=enc) as f:
    f.write(new_content)
