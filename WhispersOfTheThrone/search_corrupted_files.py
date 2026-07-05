import os
import codecs

systems_dir = 'Systems'
corrupted_files = []

for file_name in os.listdir(systems_dir):
    if not file_name.endswith('.cs'):
        continue
    file_path = os.path.join(systems_dir, file_name)
    with open(file_path, 'rb') as f:
        raw = f.read(2)
    enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

    try:
        with open(file_path, 'r', encoding=enc) as f:
            content = f.read()
        if '\ufffd' in content:
            corrupted_files.append(file_name)
    except Exception as e:
        print(f"Error reading {file_name}: {e}")

print("Corrupted files containing \\ufffd:")
for f in corrupted_files:
    print(f"- {f}")
