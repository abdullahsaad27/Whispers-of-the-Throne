import codecs

file_path = 'Systems/FactionWarEngine.cs'

with open(file_path, 'rb') as f:
    raw = f.read(2)
enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path, 'r', encoding=enc) as f:
    content = f.read()

# We need to find the loop where RebelVassalIds are added:
# foreach (var vassal in state.RealmCharacters.Where(c => c != null && !c.IsDead && c.Role != CharacterRoleType.Ruler && c.GetTotalOpinion() < RebelOpinionThreshold).ToList())
# {
#     state.RebelVassalIds.Add(vassal.Id);

# And add the firing logic:
injection = '''
                    // Fire from council if they are a councilor
                    foreach(var role in state.Council.Keys.ToList())
                    {
                        if (state.Council[role].Name == vassal.Name)
                        {
                            state.Council[role].Name = "شاغر (متمرد)";
                            state.Council[role].Task = "لا يوجد";
                            GameMonitorSystem.Log("FACTION_WAR", $"Fired {vassal.Name} from Council because of rebellion.");
                        }
                    }
                    if (state.FirstMinister != null && state.FirstMinister.Name == vassal.Name)
                    {
                        state.FirstMinister.Name = "شاغر";
                        state.FirstMinister.IsAppointed = false;
                    }
'''

content = content.replace('state.RebelVassalIds.Add(vassal.Id);', 'state.RebelVassalIds.Add(vassal.Id);' + injection)

with open(file_path, 'w', encoding=enc) as f:
    f.write(content)
