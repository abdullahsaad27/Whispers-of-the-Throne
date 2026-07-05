import codecs
import re

# Fix DiplomacySystem.cs
with open('Systems/DiplomacySystem.cs', 'rb') as f:
    raw = f.read(2)
enc1 = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('Systems/DiplomacySystem.cs', 'r', encoding=enc1) as f:
    content = f.read()

content = content.replace('!string.IsNullOrEmpty(state.Religion) && !string.IsNullOrEmpty(neighbor.Religion) && state.Religion != neighbor.Religion', '!string.IsNullOrEmpty(neighbor.Religion) && neighbor.Religion != "الإسلام"')

with open('Systems/DiplomacySystem.cs', 'w', encoding=enc1) as f:
    f.write(content)


# Fix GrandStrategySystem.cs
with open('Systems/GrandStrategySystem.cs', 'rb') as f:
    raw = f.read(2)
enc2 = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('Systems/GrandStrategySystem.cs', 'r', encoding=enc2) as f:
    content2 = f.read()

content2 = content2.replace('c.IsAdult', 'c.Age >= 16')

with open('Systems/GrandStrategySystem.cs', 'w', encoding=enc2) as f:
    f.write(content2)

