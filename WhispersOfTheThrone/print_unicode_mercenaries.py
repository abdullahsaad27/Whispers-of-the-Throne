import codecs

file_path = r'Systems\MercenarySystem.cs'

with open(file_path, 'rb') as f:
    raw = f.read(2)
enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path, 'r', encoding=enc) as f:
    lines = f.readlines()

for i in range(16, 28):
    print(f"Line {i+1}: {repr(lines[i])}")
