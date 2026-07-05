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

filepath = 'C:/Users/Abdullah\'s pc/Documents/test/Whispers of the Throne/Whispers of the Throne test/StabilizationTests.cs'
content, enc = read_file(filepath)
if content:
    content = content.replace('WarfareSystem.DeclareWar(state, 1, true)', 'WarfareSystem.DeclareWar(state, 1, "Claim")')
    content = content.replace('WarfareSystem.DeclareWar(state, 3, true)', 'WarfareSystem.DeclareWar(state, 3, "Claim")')
    content = content.replace('WarfareSystem.DeclareWar(state, ally.Id, true)', 'WarfareSystem.DeclareWar(state, 0, "Claim")')
    content = content.replace('WarfareSystem.DeclareWar(state, 3, false)', 'WarfareSystem.DeclareWar(state, 3, "Claim")')
    content = content.replace('AiProviderType.Ollama', 'AiProviderType.OpenRouter')
    with open(filepath, 'w', encoding=enc) as f:
        f.write(content)
