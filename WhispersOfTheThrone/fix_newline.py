import codecs
import re

with open('MainForm.cs', 'rb') as f:
    raw = f.read(2)
encoding = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open('MainForm.cs', 'r', encoding=encoding) as f:
    content = f.read()

# Fix newline in string literal
content = re.sub(r'SetNarrativeText\(\$"(.*?)\n(.*?)"\);', r'SetNarrativeText($"\1\\n\2");', content)

with open('MainForm.cs', 'w', encoding=encoding) as f:
    f.write(content)
