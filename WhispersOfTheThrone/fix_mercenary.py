import codecs, re

fp = 'Systems/MercenarySystem.cs'
with open(fp, 'rb') as f: raw = f.read()
enc = 'utf-16' if raw[:2] in [b'\xff\xfe', b'\xfe\xff'] else 'utf-8-sig' if raw[:3] == b'\xef\xbb\xbf' else 'utf-8'
text = raw.decode(enc, errors='ignore')

# 1. Clean up the broken string injections
# Look for the injected code block inside string interpolation
bad_injection = '''{
            if (state.Provinces == null) return;
            var capital = System.Linq.Enumerable.FirstOrDefault(state.Provinces, p => p != null && p.Name == "دمشق");
            if (capital != null)
            {
                if (capital.Army == null) capital.Army = new WhispersOfTheThrone.Models.Army();
                capital.Army.ArchersCount += company.ArchersCount;
                capital.Army.HeavyInfantryCount += company.HeavyInfantryCount;
            }
'''
if bad_injection in text:
    text = text.replace(bad_injection, '{company.CompanyName}')

bad_injection2 = '''{
            if (state.Provinces == null) return;
            var capital = System.Linq.Enumerable.FirstOrDefault(state.Provinces, p => p != null && p.Name == "دمشق");
            if (capital != null && capital.Army != null)
            {
                capital.Army.ArchersCount = System.Math.Max(0, capital.Army.ArchersCount - company.ArchersCount);
                capital.Army.HeavyInfantryCount = System.Math.Max(0, capital.Army.HeavyInfantryCount - company.HeavyInfantryCount);
            }
'''
if bad_injection2 in text:
    text = text.replace(bad_injection2, '{company.CompanyName}')

# 2. Add to the ACTUAL methods
match1 = re.search(r'void InjectTroopsIntoCapitalPool[^{]+\{', text)
if match1:
    brace1 = match1.end()
    if 'if (state.Provinces == null) return;' not in text[brace1:brace1+200]:
        insert1 = '''
            if (state.Provinces == null) return;
            var capital = System.Linq.Enumerable.FirstOrDefault(state.Provinces, p => p != null && p.Name == "دمشق");
            if (capital != null)
            {
                if (capital.Army == null) capital.Army = new WhispersOfTheThrone.Models.Army();
                capital.Army.ArchersCount += company.ArchersCount;
                capital.Army.HeavyInfantryCount += company.HeavyInfantryCount;
            }
'''
        text = text[:brace1] + insert1 + text[brace1:]

match2 = re.search(r'void WithdrawTroopsFromCapitalPool[^{]+\{', text)
if match2:
    brace2 = match2.end()
    if 'if (state.Provinces == null) return;' not in text[brace2:brace2+200]:
        insert2 = '''
            if (state.Provinces == null) return;
            var capital = System.Linq.Enumerable.FirstOrDefault(state.Provinces, p => p != null && p.Name == "دمشق");
            if (capital != null && capital.Army != null)
            {
                capital.Army.ArchersCount = System.Math.Max(0, capital.Army.ArchersCount - company.ArchersCount);
                capital.Army.HeavyInfantryCount = System.Math.Max(0, capital.Army.HeavyInfantryCount - company.HeavyInfantryCount);
            }
'''
        text = text[:brace2] + insert2 + text[brace2:]

with open(fp, 'wb') as f:
    f.write(text.encode(enc))

print('MercenarySystem.cs fixed successfully.')
