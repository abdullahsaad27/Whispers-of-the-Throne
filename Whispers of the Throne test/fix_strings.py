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

filepath = 'C:/Users/Abdullah\'s pc/Documents/test/Whispers of the Throne/WhispersOfTheThrone/Systems/WarfareSystem.cs'
content, enc = read_file(filepath)
if content:
    content = content.replace('\"        .\"', '\"حرب قائمة\"')
    with open(filepath, 'w', encoding='utf-16') as f:
        f.write(content)

filepath = 'C:/Users/Abdullah\'s pc/Documents/test/Whispers of the Throne/WhispersOfTheThrone/Systems/FaithSystem.cs'
content, enc = read_file(filepath)
if content:
    content = content.replace('\"      .\"', '\"حليفة\"')
    with open(filepath, 'w', encoding='utf-16') as f:
        f.write(content)
