import codecs
import re

file_path = 'Models/GrandStrategyModels.cs'

with open(file_path, 'rb') as f:
    raw = f.read(2)
enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path, 'r', encoding=enc) as f:
    content = f.read()

# Add CommanderExperience and CommanderTraits to RealmCharacter
new_properties = '''        public int CommanderExperience { get; set; } = 0;
        public List<string> CommanderTraits { get; set; } = new List<string>();
        public bool IsPrisoner { get; set; } = false;'''

content = content.replace('        public bool IsPrisoner { get; set; } = false;', new_properties)

with open(file_path, 'w', encoding=enc) as f:
    f.write(content)
