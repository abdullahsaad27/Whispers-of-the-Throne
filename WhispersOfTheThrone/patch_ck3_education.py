import codecs

# 1. Patch Systems/DynastySystem.cs
file_path_dynasty = r'Systems\DynastySystem.cs'
with open(file_path_dynasty, 'rb') as f:
    raw = f.read(2)
enc_dynasty = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path_dynasty, 'r', encoding=enc_dynasty) as f:
    content_dynasty = f.read()

# Make GetTraitArabicName public static and add childhood + genetic + all professional traits
target_gettraitname = """        private static string GetAdultTraitArabicName(string trait)
        {
            return trait switch
            {
                "MidasTouched" => "ظ„ظ…ط³ط© ظ…ظٹط¯ط§ط³ ظپظٹ ط§ظ„ط§ظ‚طھطµط§ط¯",
                "BrilliantStrategist" => "ط§ط³طھط±ط§طھظٹط¬ظٹ ط¨ظ„ظٹط¯",
                "MasterSchemer" => "ط¯ط³ط§ط³ ظ…ط§ظ‡ط±",
                "SilverTongued" => "ظ„ط³ط§ظ† ظپط¶ظيت",
                "Erudite" => "ط¹ظ„ظٹظ…",
                _ => trait
            };
        }"""

replacement_gettraitname = """        public static string GetTraitArabicName(string trait)
        {
            if (string.IsNullOrWhiteSpace(trait)) return "";
            return trait switch
            {
                "MidasTouched" => "لمسة ميداس في الاقتصاد (4 نجوم)",
                "FortuneBuilder" => "صانع ثروة (3 نجوم)",
                "ThriftyClerk" => "كاتب مقتصد (2 نجمتان)",
                "IndulgentWastrel" => "مسرف متساهل (1 نجمة واحدة)",
                
                "BrilliantStrategist" => "مخطط عبقري عسكري (4 نجوم)",
                "SkilledTactician" => "تكتيكي بارع (3 نجوم)",
                "ToughSoldier" => "جندي صلب (2 نجمتان)",
                "MisguidedWarrior" => "محارب ضال (1 نجمة واحدة)",
                
                "MasterSchemer" => "سيد المؤامرات (4 نجوم)",
                "IntriguingShadow" => "ظل غامض (3 نجوم)",
                "FlamboyantSchemer" => "دسّاس متكلف (2 نجمتان)",
                "AmateurPlotter" => "متآمر هاوٍ (1 نجمة واحدة)",
                
                "GreyEminence" => "الستار الرمادي في الدبلوماسية (4 نجوم)",
                "CharismaticNegotiator" => "مفاوض كاريزمي (3 نجوم)",
                "FlamboyantTrickster" => "محاور مبهر (2 نجمتان)",
                "NaiveAppeaser" => "مسترضٍ ساذج (1 نجمة واحدة)",
                
                "Erudite" => "عليم حكيم في المعرفة (4 نجوم)",
                "AstuteIntellectual" => "مثقف فطن (3 نجوم)",
                "InsightfulThinker" => "مفكر ثاقب (2 نجمتان)",
                "ConscientiousScribe" => "كاتب ورع (1 نجمة واحدة)",

                "Brave" => "شجاع",
                "Just" => "عادل",
                "Ambitious" => "طموح",
                "Genius" => "عبقري (وراثية)",
                "Quick" => "فطن (وراثية)",
                "Intelligent" => "ذكي (وراثية)",
                "Herculean" => "بنية هرقلية (وراثية)",
                "Comely" => "حسن المظهر (وراثية)",
                "Fecund" => "خصب (وراثية)",
                "PureBlooded" => "نقي الدم (وراثية)",
                
                _ => trait
            };
        }"""

if target_gettraitname in content_dynasty:
    content_dynasty = content_dynasty.replace(target_gettraitname, replacement_gettraitname)
    print("GetTraitArabicName patched.")
else:
    target_gettraitname_crlf = target_gettraitname.replace('\n', '\r\n')
    replacement_gettraitname_crlf = replacement_gettraitname.replace('\n', '\r\n')
    if target_gettraitname_crlf in content_dynasty:
        content_dynasty = content_dynasty.replace(target_gettraitname_crlf, replacement_gettraitname_crlf)
        print("GetTraitArabicName CRLF patched.")
    else:
        print("WARNING: GetTraitArabicName pattern not found!")

# Update AssignAdultTrait to support all tiers
target_assignadult = """        private static void AssignAdultTrait(RealmCharacter child)
        {
            if (child == null) return;
            child.Traits ??= new List<string>();
            if (child.StewardshipSkill >= 15) child.Traits.Add("MidasTouched");
            if (child.MartialSkill >= 15) child.Traits.Add("BrilliantStrategist");
            if (child.IntrigueSkill >= 15) child.Traits.Add("MasterSchemer");
            if (child.Skills?.Diplomacy >= 15) child.Traits.Add("SilverTongued");
            if (child.Skills?.Learning >= 15) child.Traits.Add("Erudite");
        }"""

replacement_assignadult = """        private static void AssignAdultTrait(RealmCharacter child)
        {
            if (child == null) return;
            child.Traits ??= new List<string>();
            
            string focus = child.CurrentEducationFocus;
            if (string.IsNullOrWhiteSpace(focus))
            {
                int maxVal = Math.Max(child.StewardshipSkill, Math.Max(child.MartialSkill, Math.Max(child.IntrigueSkill, child.Skills.Diplomacy)));
                if (maxVal == child.StewardshipSkill) focus = "Stewardship";
                else if (maxVal == child.MartialSkill) focus = "Martial";
                else if (maxVal == child.IntrigueSkill) focus = "Intrigue";
                else focus = "Diplomacy";
            }

            if (focus.Equals("Stewardship", StringComparison.OrdinalIgnoreCase))
            {
                int val = child.StewardshipSkill;
                if (val >= 15) { child.Traits.Add("MidasTouched"); child.StewardshipSkill += 8; child.Skills.Stewardship += 8; }
                else if (val >= 11) { child.Traits.Add("FortuneBuilder"); child.StewardshipSkill += 6; child.Skills.Stewardship += 6; }
                else if (val >= 6) { child.Traits.Add("ThriftyClerk"); child.StewardshipSkill += 4; child.Skills.Stewardship += 4; }
                else { child.Traits.Add("IndulgentWastrel"); child.StewardshipSkill += 2; child.Skills.Stewardship += 2; }
            }
            else if (focus.Equals("Martial", StringComparison.OrdinalIgnoreCase))
            {
                int val = child.MartialSkill;
                if (val >= 15) { child.Traits.Add("BrilliantStrategist"); child.MartialSkill += 8; child.Skills.Martial += 8; }
                else if (val >= 11) { child.Traits.Add("SkilledTactician"); child.MartialSkill += 6; child.Skills.Martial += 6; }
                else if (val >= 6) { child.Traits.Add("ToughSoldier"); child.MartialSkill += 4; child.Skills.Martial += 4; }
                else { child.Traits.Add("MisguidedWarrior"); child.MartialSkill += 2; child.Skills.Martial += 2; }
            }
            else if (focus.Equals("Intrigue", StringComparison.OrdinalIgnoreCase))
            {
                int val = child.IntrigueSkill;
                if (val >= 15) { child.Traits.Add("MasterSchemer"); child.IntrigueSkill += 8; child.Skills.Intrigue += 8; }
                else if (val >= 11) { child.Traits.Add("IntriguingShadow"); child.IntrigueSkill += 6; child.Skills.Intrigue += 6; }
                else if (val >= 6) { child.Traits.Add("FlamboyantSchemer"); child.IntrigueSkill += 4; child.Skills.Intrigue += 4; }
                else { child.Traits.Add("AmateurPlotter"); child.IntrigueSkill += 2; child.Skills.Intrigue += 2; }
            }
            else if (focus.Equals("Diplomacy", StringComparison.OrdinalIgnoreCase))
            {
                int val = child.Skills.Diplomacy;
                if (val >= 15) { child.Traits.Add("GreyEminence"); child.Skills.Diplomacy += 8; }
                else if (val >= 11) { child.Traits.Add("CharismaticNegotiator"); child.Skills.Diplomacy += 6; }
                else if (val >= 6) { child.Traits.Add("FlamboyantTrickster"); child.Skills.Diplomacy += 4; }
                else { child.Traits.Add("NaiveAppeaser"); child.Skills.Diplomacy += 2; }
            }
            else // Learning
            {
                int val = child.Skills.Learning;
                if (val >= 15) { child.Traits.Add("Erudite"); child.Skills.Learning += 8; }
                else if (val >= 11) { child.Traits.Add("AstuteIntellectual"); child.Skills.Learning += 6; }
                else if (val >= 6) { child.Traits.Add("InsightfulThinker"); child.Skills.Learning += 4; }
                else { child.Traits.Add("ConscientiousScribe"); child.Skills.Learning += 2; }
            }
        }"""

if target_assignadult in content_dynasty:
    content_dynasty = content_dynasty.replace(target_assignadult, replacement_assignadult)
    print("AssignAdultTrait patched.")
else:
    target_assignadult_crlf = target_assignadult.replace('\n', '\r\n')
    replacement_assignadult_crlf = replacement_assignadult.replace('\n', '\r\n')
    if target_assignadult_crlf in content_dynasty:
        content_dynasty = content_dynasty.replace(target_assignadult_crlf, replacement_assignadult_crlf)
        print("AssignAdultTrait CRLF patched.")
    else:
        print("WARNING: AssignAdultTrait pattern not found!")

# Update GetAdultTraitAnnouncement
target_announcement = """        private static string GetAdultTraitAnnouncement(RealmCharacter child)
        {
            if (child == null || child.Traits == null) return "";
            var relevant = child.Traits.Where(t => t == "MidasTouched" || t == "BrilliantStrategist" || t == "MasterSchemer" || t == "SilverTongued" || t == "Erudite").ToList();
            if (relevant.Count == 0) return "ظ„ظ… ظٹط­طµظ„ ط¹ظ„ظ‰ ط³ظ…ط© ظ…ظ‡ظ†ظٹط© ط¨ط§ط±ط²ط©.";
            return "ط³ظ…ط§طھظ‡ ط§ظ„ظ…ظ‡ظ†ظٹط©: " + string.Join("طŒ ", relevant.Select(GetAdultTraitArabicName));
        }"""

replacement_announcement = """        private static string GetAdultTraitAnnouncement(RealmCharacter child)
        {
            if (child == null || child.Traits == null) return "";
            var relevant = child.Traits.Where(t => 
                t == "MidasTouched" || t == "FortuneBuilder" || t == "ThriftyClerk" || t == "IndulgentWastrel" ||
                t == "BrilliantStrategist" || t == "SkilledTactician" || t == "ToughSoldier" || t == "MisguidedWarrior" ||
                t == "MasterSchemer" || t == "IntriguingShadow" || t == "FlamboyantSchemer" || t == "AmateurPlotter" ||
                t == "GreyEminence" || t == "CharismaticNegotiator" || t == "FlamboyantTrickster" || t == "NaiveAppeaser" ||
                t == "Erudite" || t == "AstuteIntellectual" || t == "InsightfulThinker" || t == "ConscientiousScribe"
            ).ToList();
            if (relevant.Count == 0) return "لم يحصل على سمة مهنية بارزة.";
            return "سماته المهنية: " + string.Join("، ", relevant.Select(GetTraitArabicName));
        }"""

if target_announcement in content_dynasty:
    content_dynasty = content_dynasty.replace(target_announcement, replacement_announcement)
    print("GetAdultTraitAnnouncement patched.")
else:
    target_announcement_crlf = target_announcement.replace('\n', '\r\n')
    replacement_announcement_crlf = replacement_announcement.replace('\n', '\r\n')
    if target_announcement_crlf in content_dynasty:
        content_dynasty = content_dynasty.replace(target_announcement_crlf, replacement_announcement_crlf)
        print("GetAdultTraitAnnouncement CRLF patched.")
    else:
        print("WARNING: GetAdultTraitAnnouncement pattern not found!")

# Add childhood events trigger to HandleCharacterAging
target_aging = """                    if (child.CharacterAge >= 16)
                    {
                        child.IsAdult = true;
                        AssignAdultTrait(child);
                        state.TurnWarnings ??= new List<string>();
                        state.TurnWarnings.Add($"[ط¨ظ„ظˆط؛]: ط¨ظ„طـ{child.Name} ط§ظ„ط³ط§ط¯ط³ط© ط¹ط´ط±ط© ظˆط£طµط¨ط­ ط±ط¬ظ„ط§ظ‹ ط¨ط§ظ„ط§ظ‹ ظ…ط¤ظ‡ظ„ط§ظ‹ ظ„ظ„ط¨ظ„ط§ط· ظˆط§ظ„ظ…ط¬ظ„ط³.");
                        NvdaEngine.Speak($"ط¨ظ„ط؛ {child.Name} ط§ظ„ط³ط§ط¯ط³ط© ط¹ط´ط±ط© ظˆط£طµط¨ط­ ط¨ط§ظ„ط؛ط§ظ‹. {GetAdultTraitAnnouncement(child)}");
                    }"""

replacement_aging = """                    if (child.CharacterAge == 9 || child.CharacterAge == 12 || child.CharacterAge == 14)
                    {
                        string guardianName = "مؤدبه الخاص";
                        var ward = state.WardAssignments?.FirstOrDefault(w => w.ChildId == child.Id);
                        if (ward != null)
                        {
                            guardianName = ward.GuardianName;
                        }

                        var childEvent = new LivingRealmEvent
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventType = "ChildhoodEducationChoice",
                            ActorType = "Child",
                            ActorId = child.Id,
                            ActorName = child.Name,
                            Title = $"تربية الأمير {child.Name}",
                            Description = $"بلغ الأمير {child.Name} سن {child.CharacterAge}. يقدم مؤدبه {guardianName} تقريراً عن سلوكه؛ الموقف يتطلب توجيهك الأبوي لتشكيل شخصيته.",
                            CouncilAdvice = "اختر السمة التي تريد أن يكتسبها الأمير. ستؤثر هذه الصفة بشكل دائم على مهاراته وطباعه عند البلوغ.",
                            RequiresPause = true,
                            RequiresDecision = true,
                            CreatedDay = DiplomacySystem.GetCurrentDayNumber(state),
                            DateText = state.Time.GetDateString()
                        };
                        state.LivingRealmLog ??= new List<LivingRealmEvent>();
                        state.LivingRealmLog.Add(childEvent);
                        
                        state.TurnWarnings ??= new List<string>();
                        state.TurnWarnings.Add($"[تربية وتعليم]: هناك قرار تربية معلق للأمير {child.Name} في شاشة العالم الحي.");
                    }

                    if (child.CharacterAge >= 16)
                    {
                        child.IsAdult = true;
                        AssignAdultTrait(child);
                        state.TurnWarnings ??= new List<string>();
                        state.TurnWarnings.Add($"[بلوغ]: بلغ {child.Name} السادسة عشرة وأصبح بالغاً مؤهلاً للبلاط والمجلس.");
                        NvdaEngine.Speak($"بلغ {child.Name} السادسة عشرة وأصبح بالغاً. {GetAdultTraitAnnouncement(child)}");
                    }"""

if target_aging in content_dynasty:
    content_dynasty = content_dynasty.replace(target_aging, replacement_aging)
    print("HandleCharacterAging patched.")
else:
    target_aging_crlf = target_aging.replace('\n', '\r\n')
    replacement_aging_crlf = replacement_aging.replace('\n', '\r\n')
    if target_aging_crlf in content_dynasty:
        content_dynasty = content_dynasty.replace(target_aging_crlf, replacement_aging_crlf)
        print("HandleCharacterAging CRLF patched.")
    else:
        print("WARNING: HandleCharacterAging pattern not found!")

with open(file_path_dynasty, 'w', encoding=enc_dynasty) as f:
    f.write(content_dynasty)


# 2. Patch Systems/LivingRealmSystem.cs
file_path_lrs = r'Systems\LivingRealmSystem.cs'
with open(file_path_lrs, 'rb') as f:
    raw = f.read(2)
enc_lrs = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path_lrs, 'r', encoding=enc_lrs) as f:
    content_lrs = f.read()

# Add ChildhoodEducationChoice to switch
target_lrs_switch = """                case "TradeRouteCrisis":
                    ResolveTradeRouteCrisis(state, realmEvent, choice, result);
                    break;"""

replacement_lrs_switch = """                case "TradeRouteCrisis":
                    ResolveTradeRouteCrisis(state, realmEvent, choice, result);
                    break;
                case "ChildhoodEducationChoice":
                    ResolveChildhoodChoice(state, realmEvent, choice, result);
                    break;"""

if target_lrs_switch in content_lrs:
    content_lrs = content_lrs.replace(target_lrs_switch, replacement_lrs_switch)
    print("LivingRealmSystem.cs switch case added.")
else:
    target_lrs_switch_crlf = target_lrs_switch.replace('\n', '\r\n')
    replacement_lrs_switch_crlf = replacement_lrs_switch.replace('\n', '\r\n')
    if target_lrs_switch_crlf in content_lrs:
        content_lrs = content_lrs.replace(target_lrs_switch_crlf, replacement_lrs_switch_crlf)
        print("LivingRealmSystem.cs CRLF switch case added.")
    else:
        print("WARNING: LivingRealmSystem.cs switch pattern not found!")

# Add ResolveChildhoodChoice helper method
target_lrs_helper = """        private static void ResolveEconomicAidRequest(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)"""

replacement_lrs_helper = """        private static void ResolveChildhoodChoice(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)
        {
            var child = state.RealmCharacters.FirstOrDefault(c => c != null && c.Id == realmEvent.ActorId);
            if (child == null)
            {
                result.Success = false;
                result.MainMessage = "لم يتم العثور على الأمير المعني.";
                return;
            }

            child.Traits ??= new List<string>();
            
            if (choice == "Brave")
            {
                child.Traits.Add("Brave");
                child.MartialSkill += 2;
                child.Skills.Martial += 2;
                result.MainMessage = $"اكتسب الأمير {child.Name} سمة (شجاع) وازدادت مهارته العسكرية بمقدار 2.";
            }
            else if (choice == "Just")
            {
                child.Traits.Add("Just");
                child.StewardshipSkill += 2;
                child.Skills.Stewardship += 2;
                child.Skills.Diplomacy += 2;
                result.MainMessage = $"اكتسب الأمير {child.Name} سمة (عادل) وازدادت مهاراته في الإشراف والدبلوماسية بمقدار 2.";
            }
            else // Ambitious
            {
                child.Traits.Add("Ambitious");
                child.StewardshipSkill += 1;
                child.Skills.Stewardship += 1;
                child.MartialSkill += 1;
                child.Skills.Martial += 1;
                child.IntrigueSkill += 1;
                child.Skills.Intrigue += 1;
                child.Skills.Diplomacy += 1;
                child.Skills.Learning += 1;
                result.MainMessage = $"اكتسب الأمير {child.Name} سمة (طموح) وازدادت كافة مهاراته بمقدار 1.";
            }

            realmEvent.IsResolved = true;
            result.Success = true;
            result.SoundEffectKey = "paper_scroll";
        }

        private static void ResolveEconomicAidRequest(GameState state, LivingRealmEvent realmEvent, string choice, GameActionResult result)"""

if target_lrs_helper in content_lrs:
    content_lrs = content_lrs.replace(target_lrs_helper, replacement_lrs_helper)
    print("LivingRealmSystem.cs helper added.")
else:
    target_lrs_helper_crlf = target_lrs_helper.replace('\n', '\r\n')
    replacement_lrs_helper_crlf = replacement_lrs_helper.replace('\n', '\r\n')
    if target_lrs_helper_crlf in content_lrs:
        content_lrs = content_lrs.replace(target_lrs_helper_crlf, replacement_lrs_helper_crlf)
        print("LivingRealmSystem.cs CRLF helper added.")
    else:
        print("WARNING: LivingRealmSystem.cs helper pattern not found!")

with open(file_path_lrs, 'w', encoding=enc_lrs) as f:
    f.write(content_lrs)


# 3. Patch MainForm.cs
file_path_mf = 'MainForm.cs'
with open(file_path_mf, 'rb') as f:
    raw = f.read(2)
enc_mf = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path_mf, 'r', encoding=enc_mf) as f:
    content_mf = f.read()

# Add ChildhoodEducationChoice buttons case
target_mf_buttons = """                case "TradeRouteCrisis":
                    Resolve("طھظ…ظˆظٹظ„ ط­ظ…ط§ظٹط© ط§ظ„ط·ط±ظٹظ‚ (60 ط°ظ‡ط¨)", "Protect");
                    Resolve("طھط¬ط§ظ‡ظ„ ط§ظ„ط£ط²ظ…ط© (طھط±ط§ط¬ط¹ ط«ظ‚ط© ط§ظ„طھط¬ط§ط±)", "Ignore");
                    break;"""

replacement_mf_buttons = """                case "TradeRouteCrisis":
                    Resolve("طھظ…ظˆظٹظ„ ط­ظ…ط§ظٹط© ط§ظ„ط·ط±ظٹظ‚ (60 ط°ظ‡ط¨)", "Protect");
                    Resolve("طھط¬ط§ظ‡ظ„ ط§ظ„ط£ط²ظ…ط© (طھط±ط§ط¬ط¹ ط«ظ‚ط© ط§ظ„طھط¬ط§ط±)", "Ignore");
                    break;
                case "ChildhoodEducationChoice":
                    Resolve("علمه الشجاعة والإقدام (سمة شجاع: +2 عسكري)", "Brave");
                    Resolve("علمه العدل والإنصاف (سمة عادل: +2 إشراف، +2 دبلوماسية)", "Just");
                    Resolve("علمه الطموح وعلو الهمة (سمة طموح: +1 لكافة المهارات)", "Ambitious");
                    break;"""

if target_mf_buttons in content_mf:
    content_mf = content_mf.replace(target_mf_buttons, replacement_mf_buttons)
    print("MainForm.cs button case added.")
else:
    target_mf_buttons_crlf = target_mf_buttons.replace('\n', '\r\n')
    replacement_mf_buttons_crlf = replacement_mf_buttons.replace('\n', '\r\n')
    if target_mf_buttons_crlf in content_mf:
        content_mf = content_mf.replace(target_mf_buttons_crlf, replacement_mf_buttons_crlf)
        print("MainForm.cs CRLF button case added.")
    else:
        print("WARNING: MainForm.cs button pattern not found!")

# Update trait displaying with Select(DynastySystem.GetTraitArabicName)
targets_joins = [
    ('ruler.Traits) : "لا يوجد";', 'ruler.Traits.Select(DynastySystem.GetTraitArabicName)) : "لا يوجد";'),
    ('string.Join("، ", rc.Traits)}" : "";', 'string.Join("، ", rc.Traits.Select(DynastySystem.GetTraitArabicName))}" : "";'),
    ('string.Join("، ", rc.Traits)}\\n" : "\\n";', 'string.Join("، ", rc.Traits.Select(DynastySystem.GetTraitArabicName))}\\n" : "\\n";')
]

for t, r in targets_joins:
    if t in content_mf:
        content_mf = content_mf.replace(t, r)
        print(f"Patched: {t}")
    else:
        # Try raw double slash for newline
        t_crlf = t.replace('\\n', '\n')
        r_crlf = r.replace('\\n', '\n')
        if t_crlf in content_mf:
            content_mf = content_mf.replace(t_crlf, r_crlf)
            print(f"Patched CRLF version: {t}")
        else:
            # Let's try direct search
            print(f"WARNING: Join pattern not found: {t}")

with open(file_path_mf, 'w', encoding=enc_mf) as f:
    f.write(content_mf)

print("Childhood Education and Trait translation patches completed.")
