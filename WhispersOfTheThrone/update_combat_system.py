import codecs
import re

file_path = 'Systems/CombatSystem.cs'

with open(file_path, 'rb') as f:
    raw = f.read(2)
enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path, 'r', encoding=enc) as f:
    content = f.read()

# Replace ResolveVictor to also call AwardCommanderExperience
pattern = r'''            report\.PhaseLogs\.Add\(\$"\[[^\]]+\]: [^ ]+ [^ ]+ \{report\.Victor\}\."\);
        \}'''

replacement = r'''            report.PhaseLogs.Add($"[النهاية]: المنتصر هو {report.Victor}.");
            AwardCommanderExperience(report);
        }

        private static void AwardCommanderExperience(CombatReport report)
        {
            if (report.AttackerContextState != null && !string.IsNullOrEmpty(report.Attacker.CommanderName))
            {
                var cmd = report.AttackerContextState.RealmCharacters?.FirstOrDefault(c => c.Name == report.Attacker.CommanderName);
                if (cmd != null)
                {
                    cmd.CommanderExperience += report.Defender.InitialTotal / 10;
                    CheckCommanderLevelUp(report.AttackerContextState, cmd);
                }
            }

            if (report.DefenderContextState != null && !string.IsNullOrEmpty(report.Defender.CommanderName))
            {
                var cmd = report.DefenderContextState.RealmCharacters?.FirstOrDefault(c => c.Name == report.Defender.CommanderName);
                if (cmd != null)
                {
                    cmd.CommanderExperience += report.Attacker.InitialTotal / 10;
                    CheckCommanderLevelUp(report.DefenderContextState, cmd);
                }
            }
        }

        private static void CheckCommanderLevelUp(GameState state, RealmCharacter cmd)
        {
            if (cmd.CommanderExperience >= 100)
            {
                cmd.CommanderExperience -= 100;
                cmd.MartialSkill += 1;
                GameMonitorSystem.Log("COMBAT", $"Commander {cmd.Name} leveled up Martial Skill to {cmd.MartialSkill}!");
                
                string[] possibleTraits = { "Aggressive Attacker", "Defensive Specialist", "Holy Warrior", "Logistician", "Organizer" };
                var missingTraits = possibleTraits.Where(t => !cmd.CommanderTraits.Contains(t)).ToList();
                if (missingTraits.Count > 0 && Rand.Next(100) < 30) // 30% chance to also get a trait
                {
                    string newTrait = missingTraits[Rand.Next(missingTraits.Count)];
                    cmd.CommanderTraits.Add(newTrait);
                    GameMonitorSystem.Log("COMBAT", $"Commander {cmd.Name} gained trait: {newTrait}!");
                }
            }
        }'''

# Since we don't know the exact arabic string in the original regex pattern, I will just do a string replace instead of regex for safety, using the last brace
lines = content.split('\n')
for i, line in enumerate(lines):
    if "report.PhaseLogs.Add" in line and "report.Victor" in line and "[النهاية]" not in line:
        # found the line
        lines[i] = line
        lines.insert(i+2, '''
        private static void AwardCommanderExperience(CombatReport report)
        {
            if (report.AttackerContextState != null && !string.IsNullOrEmpty(report.Attacker.CommanderName))
            {
                var cmd = report.AttackerContextState.RealmCharacters?.FirstOrDefault(c => c.Name == report.Attacker.CommanderName);
                if (cmd != null)
                {
                    cmd.CommanderExperience += Math.Max(10, report.Defender.InitialTotal / 10);
                    CheckCommanderLevelUp(report.AttackerContextState, cmd);
                }
            }

            if (report.DefenderContextState != null && !string.IsNullOrEmpty(report.Defender.CommanderName))
            {
                var cmd = report.DefenderContextState.RealmCharacters?.FirstOrDefault(c => c.Name == report.Defender.CommanderName);
                if (cmd != null)
                {
                    cmd.CommanderExperience += Math.Max(10, report.Attacker.InitialTotal / 10);
                    CheckCommanderLevelUp(report.DefenderContextState, cmd);
                }
            }
        }

        private static void CheckCommanderLevelUp(GameState state, RealmCharacter cmd)
        {
            if (cmd.CommanderExperience >= 100)
            {
                cmd.CommanderExperience -= 100;
                cmd.MartialSkill += 1;
                GameMonitorSystem.Log("COMBAT", $"Commander {cmd.Name} leveled up Martial Skill to {cmd.MartialSkill}!");
                
                string[] possibleTraits = { "Aggressive Attacker", "Defensive Specialist", "Holy Warrior", "Logistician", "Organizer" };
                var missingTraits = possibleTraits.Where(t => !cmd.CommanderTraits.Contains(t)).ToList();
                if (missingTraits.Count > 0 && Rand.Next(100) < 30) // 30% chance to also get a trait
                {
                    string newTrait = missingTraits[Rand.Next(missingTraits.Count)];
                    cmd.CommanderTraits.Add(newTrait);
                    GameMonitorSystem.Log("COMBAT", $"Commander {cmd.Name} gained trait: {newTrait}!");
                }
            }
        }''')
        lines.insert(i+1, "            AwardCommanderExperience(report);")
        break

with open(file_path, 'w', encoding=enc) as f:
    f.write('\n'.join(lines))
