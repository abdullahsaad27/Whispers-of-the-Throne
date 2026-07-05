import codecs, re

def read_file(fp):
    with open(fp, 'rb') as f: raw = f.read()
    enc = 'utf-16' if raw[:2] in [b'\xff\xfe', b'\xfe\xff'] else 'utf-8-sig' if raw[:3] == b'\xef\xbb\xbf' else 'utf-8'
    return raw.decode(enc, errors='ignore'), enc

def write_file(fp, text, enc):
    with open(fp, 'wb') as f: f.write(text.encode(enc))

# 1. DiplomacySystem.cs
fp = 'Systems/DiplomacySystem.cs'
text, enc = read_file(fp)
if 'أنت في حالة حرب بالفعل' in text:
    text = re.sub(r'أنت في حالة حرب بالفعل[^"]*?"', 'أنت في حالة حرب بالفعل. لديك حرب قائمة"', text)
if 'لا يمكنك إعلان الحرب على حليفك' in text:
    text = text.replace('لا يمكنك إعلان الحرب على حليفك', 'لا يمكنك إعلان الحرب على حليفة')
write_file(fp, text, enc)
print('Fixed DiplomacySystem.cs')

# 2. FaithSystem.cs
fp = 'Systems/FaithSystem.cs'
text, enc = read_file(fp)
if 'state.ReligiousFervor -= HolyWarFervorBoost;' in text:
    text = text.replace('state.ReligiousFervor -= HolyWarFervorBoost;', 'state.ReligiousFervor += HolyWarFervorBoost;')
if 'state.ReligiousFervor -= 5;' in text:
    text = text.replace('state.ReligiousFervor -= 5;', 'state.ReligiousFervor += 5;')
write_file(fp, text, enc)
print('Fixed FaithSystem.cs')

# 3. EconomySystem.cs
fp = 'Systems/EconomySystem.cs'
text, enc = read_file(fp)
if 'vassal.TaxTier == "High"' not in text:
    match = re.search(r'void ProcessMonthlyEconomy[^{]+\{', text)
    if match:
        brace = match.end()
        insert = '''
            if (state.Vassals != null)
            {
                foreach (var vassal in state.Vassals)
                {
                    if (vassal == null) continue;
                    if (vassal.TaxTier == "High") vassal.TaxContribution = (int)(vassal.TaxContribution * 1.25);
                    if (vassal.TaxTier == "Low") vassal.TaxContribution = (int)(vassal.TaxContribution * 0.75);
                    if (vassal.LevyTier == "High") vassal.LevyContribution = (int)(vassal.LevyContribution * 1.40);
                    if (vassal.LevyTier == "Low") vassal.LevyContribution = (int)(vassal.LevyContribution * 0.60);
                }
            }
'''
        text = text[:brace] + insert + text[brace:]
        write_file(fp, text, enc)
        print('Fixed EconomySystem.cs')
