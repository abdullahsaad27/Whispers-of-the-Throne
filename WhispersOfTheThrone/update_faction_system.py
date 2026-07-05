import re

with open('Systems/FactionSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

pattern = r'            switch \(actionType\)\n            \{'
replacement = r'''            faction.IsUltimatumPending = false;
            switch (actionType)
            {'''

content = re.sub(pattern, replacement, content)

with open('Systems/FactionSystem.cs', 'w', encoding='utf-8') as f:
    f.write(content)
