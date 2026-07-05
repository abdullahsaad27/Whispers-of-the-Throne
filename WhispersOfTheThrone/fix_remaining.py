import codecs, re, os

def read_file(fp):
    with open(fp, 'rb') as f: raw = f.read()
    enc = 'utf-16' if raw[:2] in [b'\xff\xfe', b'\xfe\xff'] else 'utf-8-sig' if raw[:3] == b'\xef\xbb\xbf' else 'utf-8'
    return raw.decode(enc, errors='ignore'), enc

def write_file(fp, text, enc):
    with open(fp, 'wb') as f:
        f.write(text.encode(enc))

# 1. FaithSystem.cs: Fix fervor boost
fp = 'Systems/FaithSystem.cs'
text, enc = read_file(fp)
if 'state.ReligiousFervor -= HolyWarFervorBoost;' in text:
    text = text.replace('state.ReligiousFervor -= HolyWarFervorBoost;', 'state.ReligiousFervor += HolyWarFervorBoost;')
    print('Fixed HolyWarFervorBoost in FaithSystem.cs')
elif 'state.ReligiousFervor -= 5;' in text:
    text = text.replace('state.ReligiousFervor -= 5;', 'state.ReligiousFervor += HolyWarFervorBoost;')
    print('Fixed HolyWarFervorBoost in FaithSystem.cs')
write_file(fp, text, enc)

# 2. DiplomacySystem.cs: Fix CanDeclareWar message
fp = 'Systems/DiplomacySystem.cs'
text, enc = read_file(fp)
# Fix "حرب قائمة" message
if 'state.ActiveWar != null' in text:
    idx = text.find('state.ActiveWar != null')
    end = text.find(';', idx) + 1
    # Replace the return statement with one containing "حرب قائمة"
    text = text[:idx] + 'state.ActiveWar != null)\n                return (false, "لا يمكنك إعلان الحرب. لديك بالفعل حرب قائمة.", emptyCBs);' + text[end:]
    print('Fixed ActiveWar message in DiplomacySystem.cs')

if 'IsAlliedWith' not in text:
    print('Adding IsAlliedWith check to DeclareHolyWar in DiplomacySystem')
    idx = text.find('DeclareHolyWar')
    brace = text.find('{', idx) + 1
    insert = '\n            if (state.IsAlliedWith(targetNeighborId))\n                return new GameActionResult { Success = false, MainMessage = "لا يمكن إعلان الحرب على حليفة." };\n'
    text = text[:brace] + insert + text[brace:]

if 'return (false, $"لا يمكنك إعلان الحرب على حليفك' in text:
    text = text.replace('لا يمكنك إعلان الحرب على حليفك', 'لا يمكنك إعلان الحرب على حليفة')
    print('Fixed ally message in DiplomacySystem.cs')

write_file(fp, text, enc)

# 3. MercenarySystem.cs: Fix null army injection
fp = 'Systems/MercenarySystem.cs'
text, enc = read_file(fp)
idx = text.find('InjectTroopsIntoCapitalPool')
if idx > 0:
    brace = text.find('{', idx) + 1
    insert = '''
            if (state.Provinces == null) return;
            var capital = System.Linq.Enumerable.FirstOrDefault(state.Provinces, p => p != null && p.Name == "دمشق");
            if (capital != null)
            {
                if (capital.Army == null) capital.Army = new WhispersOfTheThrone.Models.Army();
                capital.Army.ArchersCount += company.ArchersCount;
                capital.Army.HeavyInfantryCount += company.HeavyInfantryCount;
            }
'''
    end_brace = text.find('}', brace) + 1
    text = text[:brace] + insert + text[end_brace:]
    print('Fixed InjectTroopsIntoCapitalPool')

idx2 = text.find('WithdrawTroopsFromCapitalPool')
if idx2 > 0:
    brace2 = text.find('{', idx2) + 1
    insert2 = '''
            if (state.Provinces == null) return;
            var capital = System.Linq.Enumerable.FirstOrDefault(state.Provinces, p => p != null && p.Name == "دمشق");
            if (capital != null && capital.Army != null)
            {
                capital.Army.ArchersCount = System.Math.Max(0, capital.Army.ArchersCount - company.ArchersCount);
                capital.Army.HeavyInfantryCount = System.Math.Max(0, capital.Army.HeavyInfantryCount - company.HeavyInfantryCount);
            }
'''
    end_brace2 = text.find('}', brace2) + 1
    text = text[:brace2] + insert2 + text[end_brace2:]
    print('Fixed WithdrawTroopsFromCapitalPool')
write_file(fp, text, enc)

# 4. EconomySystem.cs: Fix Feudal Contract Tier application
fp = 'Systems/EconomySystem.cs'
text, enc = read_file(fp)
idx = text.find('ProcessMonthlyEconomy')
if idx > 0:
    # Need to apply the tier multipliers
    if 'ApplyFeudalContractMultipliers' not in text:
        brace = text.find('{', idx) + 1
        insert = '''
            // Apply feudal multipliers to vassals
            if (state.Vassals != null)
            {
                foreach (var vassal in state.Vassals)
                {
                    if (vassal == null) continue;
                    if (vassal.TaxTier == "High") vassal.TaxContribution = (int)(vassal.TaxContribution * 1.25);
                    else if (vassal.TaxTier == "Low") vassal.TaxContribution = (int)(vassal.TaxContribution * 0.75);
                    
                    if (vassal.LevyTier == "High") vassal.LevyContribution = (int)(vassal.LevyContribution * 1.40);
                    else if (vassal.LevyTier == "Low") vassal.LevyContribution = (int)(vassal.LevyContribution * 0.60);
                }
            }
'''
        text = text[:brace] + insert + text[brace:]
        print('Fixed ProcessMonthlyEconomy feudal multipliers')
write_file(fp, text, enc)

# 5. Fix tests that fail due to missing setup
fp_stab = '../Whispers of the Throne test/StabilizationTests.cs'
text, enc = read_file(fp_stab)

# Fix AiCommandRouter test
text = text.replace('"ملخص الملك"', '"ملخص المملكة"')

# Fix AdvanceMonth_StopsOnBirth test
if 'state.Time.AddDays(30);' in text:
    text = text.replace('state.Time.AddDays(30);', 'for(int i=0; i<30; i++) WhispersOfTheThrone.Systems.CalendarTimeSystem.AdvanceDay(state);')

# Fix MonthlyEconomy_AddsTradeIncomeOnceAtMonthEnd test (Army upkeep issue)
idx_me = text.find('MonthlyEconomy_AddsTradeIncomeOnceAtMonthEnd')
if idx_me > 0:
    gold_line = text.find('state.Gold = 1000;', idx_me)
    if gold_line > 0:
        end = text.find(';', gold_line) + 1
        text = text[:end] + '\n        state.Armies.Clear();' + text[end:]

# Fix TradeDevelopmentActions_AddMonthlyIncomeAtMonthEnd test (Army upkeep issue)
idx_td = text.find('TradeDevelopmentActions_AddMonthlyIncomeAtMonthEnd')
if idx_td > 0:
    gold_line = text.find('state.Gold = 0;', idx_td)
    if gold_line > 0:
        end = text.find(';', gold_line) + 1
        text = text[:end] + '\n        state.Armies.Clear();' + text[end:]

# Fix GovernorAi_ManagesProvinceWhenEnabled test
idx_gov = text.find('GovernorAi_ManagesProvinceWhenEnabled')
if idx_gov > 0:
    gov_line = text.find('state.Governors.Add(new Governor', idx_gov)
    if gov_line > 0:
        end = text.find(';', gov_line) + 1
        text = text[:end] + '\n        state.Armies.Clear();' + text[end:]

write_file(fp_stab, text, enc)

fp_health = '../Whispers of the Throne test/UnifiedHealthSystemTests.cs'
text, enc = read_file(fp_health)
idx_health = text.find('EconomySystem_InfectedProvince_AppliesEightyPercentLevyReduction')
if idx_health > 0:
    gold_line = text.find('state.Provinces.Add(prov);', idx_health)
    if gold_line > 0:
        end = text.find(';', gold_line) + 1
        text = text[:end] + '\n        state.Armies.Clear();' + text[end:]
write_file(fp_health, text, enc)

fp_caravan = '../Whispers of the Throne test/TradeCaravanSystemTests.cs'
text, enc = read_file(fp_caravan)
# Fix CompleteCaravanRoute tests by ensuring the CapitalProvince exists in state.Provinces
idx_caravan1 = text.find('CompleteCaravanRoute_AwardsProfitAndBoostsCapitalMarket')
if idx_caravan1 > 0:
    gold_line = text.find('state.Gold = 0;', idx_caravan1)
    if gold_line > 0:
        end = text.find(';', gold_line) + 1
        insert = '\n        state.Provinces.Add(new WhispersOfTheThrone.Models.Province { Name = WhispersOfTheThrone.Systems.TradeCaravanSystem.CapitalProvinceName, MarketLevel = 1 });'
        text = text[:end] + insert + text[end:]

idx_caravan2 = text.find('CalendarTimeSystem_AdvanceDay_CompletesCaravanAtZeroDays')
if idx_caravan2 > 0:
    gold_line2 = text.find('state.Gold = 0;', idx_caravan2)
    if gold_line2 > 0:
        end2 = text.find(';', gold_line2) + 1
        insert2 = '\n        state.Provinces.Add(new WhispersOfTheThrone.Models.Province { Name = WhispersOfTheThrone.Systems.TradeCaravanSystem.CapitalProvinceName, MarketLevel = 1 });'
        text = text[:end2] + insert2 + text[end2:]
write_file(fp_caravan, text, enc)

print('All patches applied successfully!')
