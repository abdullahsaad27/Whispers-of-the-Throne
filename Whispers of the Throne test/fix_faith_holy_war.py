import sys

def read_file(filepath):
    encodings = ['utf-16', 'utf-8-sig', 'utf-8']
    for enc in encodings:
        try:
            with open(filepath, 'r', encoding=enc) as f:
                content = f.read()
                return content, enc
        except UnicodeDecodeError:
            pass
    return None, None

filepath = 'C:/Users/Abdullah\'s pc/Documents/test/Whispers of the Throne/WhispersOfTheThrone/Systems/FaithSystem.cs'
content, enc = read_file(filepath)
if content:
    lines = content.split('\n')
    for i in range(len(lines)):
        if 'DeclareHolyWar' in lines[i]:
            for j in range(i, min(i+40, len(lines))):
                print(f'{j+1}: {lines[j]}')
            break
