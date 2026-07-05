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
    lines = content.split('\n')
    # Find DeclareWar method
    for i in range(len(lines)):
        if 'public static GameActionResult DeclareWar' in lines[i]:
            # Insert check right after 'if (state == null)'
            for j in range(i, len(lines)):
                if 'return' in lines[j] and 'result;' in lines[j]:
                    insert_idx = j + 1
                    check = '''
            if (state.ActiveWar != null)
            {
                result.Success = false;
                result.MainMessage = "        .";
                return result;
            }
'''
                    lines.insert(insert_idx, check)
                    break
            break
            
    with open(filepath, 'w', encoding=enc) as f:
        f.write('\n'.join(lines))
