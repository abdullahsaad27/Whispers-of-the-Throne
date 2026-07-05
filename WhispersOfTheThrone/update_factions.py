import re

with open('MainForm.cs', 'r', encoding='utf-8') as f:
    content = f.read()

pattern = r'                    if \(faction\.DaysUntilUltimatum > 0\)\n                    \{\n                        AddActionButton'
replacement = r'''                    if (faction.DaysUntilUltimatum > 0 || faction.IsUltimatumPending)
                    {
                        AddActionButton'''

content = re.sub(pattern, replacement, content)

with open('MainForm.cs', 'w', encoding='utf-8') as f:
    f.write(content)
