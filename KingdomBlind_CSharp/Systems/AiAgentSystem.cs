using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiAgentSystem
    {
        public static void EnsureAgents(GameState state)
        {
            if (state == null) return;

            state.AiAgentProfiles ??= new List<AiAgentProfile>();
            state.DelegatedAuthoritySettings ??= new DelegatedAuthoritySettings();
            state.DelegatedAuthoritySettings.RoleAuthorityLevels ??= new Dictionary<string, AiAuthorityLevel>();
            state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys ??= new List<string>();
            state.AiProposalQueue ??= new List<AiActionRequest>();
            state.AiActionLog ??= new List<AiActionLogEntry>();

            int monthKey = (state.Time.Year * 100) + state.Time.Month;
            if (state.DelegatedAuthoritySettings.BudgetMonthKey != monthKey)
            {
                state.DelegatedAuthoritySettings.BudgetMonthKey = monthKey;
                state.DelegatedAuthoritySettings.AutonomousBudgetSpentThisMonth = 0;
            }

            foreach (AiAgentRole role in Enum.GetValues(typeof(AiAgentRole)))
            {
                string key = role.ToString();
                if (!state.DelegatedAuthoritySettings.RoleAuthorityLevels.ContainsKey(key))
                    state.DelegatedAuthoritySettings.RoleAuthorityLevels[key] = AiAuthorityLevel.Advisor;
            }

            foreach (var character in state.RealmCharacters.Where(c => c != null && !c.IsDead))
            {
                var role = MapRole(character);
                if (role == null)
                    continue;

                var profile = UpsertProfile(state, character.Id, character.Name, character.SourceType, character.SourceId, role.Value);
                ApplyCharacterStats(state, profile, character);
            }

            foreach (var faction in state.Factions.Where(f => f != null && f.IsActive))
            {
                var leader = state.Governors.FirstOrDefault(g => g.Id == faction.LeaderGovernorId);
                string leaderName = leader == null ? faction.Name : leader.Name;
                var profile = UpsertProfile(state, "faction_" + faction.Id, leaderName, "Faction", faction.Id, AiAgentRole.FactionLeader);
                profile.Loyalty = 0;
                profile.Trust = Math.Clamp(25 - faction.Discontent / 4, 0, 100);
                profile.Ambition = Math.Clamp(40 + faction.PowerPercent / 2 + faction.Discontent / 3, 0, 100);
                profile.RiskTolerance = Math.Clamp(25 + faction.Discontent / 2 + faction.PowerPercent / 3, 5, 95);
                profile.PreferredStrategy = faction.Type == "LowerTaxes" ? "PressureForConcessions" : "FactionPressure";
            }

            UpsertProfile(state, "merchant_representative", "ممثل التجار", "Synthetic", "merchants", AiAgentRole.MerchantRepresentative);
            UpsertProfile(state, "royal_narrator", "الراوي الملكي", "Synthetic", "royal_narrator", AiAgentRole.RoyalNarrator);

            foreach (var profile in state.AiAgentProfiles)
            {
                profile.AllowedActionTypes ??= new List<AiActionType>();
                profile.Cooldowns ??= new Dictionary<string, int>();
                profile.LastActions ??= new List<string>();
                profile.MemoryNotes ??= new List<string>();
                profile.AllowedActionTypes = GetDefaultAllowedActions(profile.Role);
                profile.AuthorityLevel = GetAuthorityForRole(state, profile.Role);
                profile.MonthlyBudget = GetDefaultBudget(profile.Role, state);
                profile.IsAutonomous = state.DelegatedAuthoritySettings.AllowAutonomousActions &&
                                       profile.AuthorityLevel >= AiAuthorityLevel.LimitedDelegate;
                profile.RequiresApprovalAboveRisk = GetApprovalThreshold(profile.AuthorityLevel);
            }
        }

        public static void DecrementCooldowns(GameState state)
        {
            EnsureAgents(state);
            foreach (var profile in state.AiAgentProfiles)
            {
                var keys = profile.Cooldowns.Keys.ToList();
                foreach (var key in keys)
                    profile.Cooldowns[key] = Math.Max(0, profile.Cooldowns[key] - 1);
            }
        }

        public static AiAgentProfile GetProfile(GameState state, string characterId)
        {
            EnsureAgents(state);
            return state.AiAgentProfiles.FirstOrDefault(p => p.CharacterId == characterId);
        }

        public static AiAuthorityLevel GetAuthorityForRole(GameState state, AiAgentRole role)
        {
            state.DelegatedAuthoritySettings ??= new DelegatedAuthoritySettings();
            state.DelegatedAuthoritySettings.RoleAuthorityLevels ??= new Dictionary<string, AiAuthorityLevel>();

            string key = role.ToString();
            if (!state.DelegatedAuthoritySettings.RoleAuthorityLevels.TryGetValue(key, out var authority))
            {
                authority = AiAuthorityLevel.Advisor;
                state.DelegatedAuthoritySettings.RoleAuthorityLevels[key] = authority;
            }

            return authority;
        }

        public static void SetAuthorityForRole(GameState state, AiAgentRole role, AiAuthorityLevel authority)
        {
            state.DelegatedAuthoritySettings ??= new DelegatedAuthoritySettings();
            state.DelegatedAuthoritySettings.RoleAuthorityLevels ??= new Dictionary<string, AiAuthorityLevel>();
            state.DelegatedAuthoritySettings.RoleAuthorityLevels[role.ToString()] = authority;

            foreach (var profile in state.AiAgentProfiles.Where(p => p.Role == role))
            {
                profile.AuthorityLevel = authority;
                profile.IsAutonomous = state.DelegatedAuthoritySettings.AllowAutonomousActions &&
                                       authority >= AiAuthorityLevel.LimitedDelegate;
                profile.RequiresApprovalAboveRisk = GetApprovalThreshold(authority);
            }
        }

        public static string GetDelegationReport(GameState state)
        {
            EnsureAgents(state);
            var sb = new StringBuilder();
            sb.AppendLine("التفويض الملكي.");
            sb.AppendLine("ملخص قصير: كل وكيل يقترح أو ينفذ فقط حسب التفويض، وكل فعل يمر عبر التحقق قبل المساس بالمملكة.");
            sb.AppendLine();
            sb.AppendLine($"الأفعال التلقائية: {(state.DelegatedAuthoritySettings.AllowAutonomousActions ? "مسموحة" : "معطلة")}.");
            sb.AppendLine($"ميزانية التفويض الشهرية: {state.DelegatedAuthoritySettings.AutonomousBudgetSpentThisMonth}/{state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget} ذهب.");
            sb.AppendLine();

            foreach (AiAgentRole role in Enum.GetValues(typeof(AiAgentRole)))
                sb.AppendLine($"{GetRoleDisplayName(role)}: {GetAuthorityDisplayName(GetAuthorityForRole(state, role))}.");

            return sb.ToString().Trim();
        }

        public static string GetAgentsReport(GameState state)
        {
            EnsureAgents(state);
            var lines = state.AiAgentProfiles
                .OrderBy(p => p.Role)
                .ThenByDescending(p => p.Trust)
                .Take(30)
                .Select(p =>
                    $"{p.CharacterName}، {GetRoleDisplayName(p.Role)}. الصلاحية: {GetAuthorityDisplayName(p.AuthorityLevel)}. الثقة {p.Trust}، الولاء {p.Loyalty}، الطموح {p.Ambition}. الاستراتيجية: {GetStrategyDisplayName(p.PreferredStrategy)}.");

            return "الشخصيات المفوضة:\n" + string.Join("\n", lines);
        }

        public static string GetRoleTone(AiAgentRole role)
        {
            return role switch
            {
                AiAgentRole.FirstMinister => "متزن وإداري، يبدأ بملخص ثم يوازن الكلفة والاستقرار.",
                AiAgentRole.Spymaster => "غامض وحذر، يتحدث عن الهمسات والاحتمالات ولا يدعي اليقين.",
                AiAgentRole.MilitaryCommander => "مباشر وعسكري، يكره التردد ويركز على الجند والمؤونة.",
                AiAgentRole.Cleric => "شرعي ووعظي، يربط القرار بالعدل ورضا الناس.",
                AiAgentRole.DiplomaticAdvisor => "هادئ ودبلوماسي، يوازن الوجه والهيبة والعهد.",
                AiAgentRole.Governor => "محلي وعملي، يتحدث عن ضرائب مقاطعته وحمايتها وكرامته.",
                AiAgentRole.FactionLeader => "ضاغط وحذر، يطلب تنازلاً قبل الانفجار.",
                AiAgentRole.NeighborRuler => "سياسي محسوب، يلين أو يهدد حسب الرأي والخوف.",
                AiAgentRole.SpouseQueen => "شخصي وسياسي، يربط القصر بالعائلة وصراع الورثة.",
                AiAgentRole.Heir => "طموح أو متردد، يرى العالم من بوابة الخلافة.",
                AiAgentRole.MerchantRepresentative => "عملي، يتكلم بلغة الربح والأمان والثقة.",
                _ => "ملكي سردي، يختصر معنى الحدث دون إطالة."
            };
        }

        public static string GetRoleDisplayName(AiAgentRole role)
        {
            return role switch
            {
                AiAgentRole.FirstMinister => "الوزير الأول",
                AiAgentRole.Spymaster => "مسؤول الجواسيس",
                AiAgentRole.MilitaryCommander => "القائد العسكري",
                AiAgentRole.Cleric => "رجل الدين",
                AiAgentRole.DiplomaticAdvisor => "المستشار الدبلوماسي",
                AiAgentRole.Governor => "الوالي",
                AiAgentRole.FactionLeader => "قائد فصيل",
                AiAgentRole.NeighborRuler => "حاكم دولة مجاورة",
                AiAgentRole.SpouseQueen => "الزوجة أو الملكة",
                AiAgentRole.Heir => "الوريث",
                AiAgentRole.MerchantRepresentative => "ممثل التجار",
                AiAgentRole.RoyalNarrator => "الراوي الملكي",
                _ => role.ToString()
            };
        }

        public static string GetAuthorityDisplayName(AiAuthorityLevel authority)
        {
            return authority switch
            {
                AiAuthorityLevel.None => "لا صلاحية، حوار فقط",
                AiAuthorityLevel.Advisor => "نصائح فقط",
                AiAuthorityLevel.LimitedDelegate => "أفعال بسيطة",
                AiAuthorityLevel.TrustedDelegate => "أفعال متوسطة ضمن الميزانية",
                AiAuthorityLevel.RoyalRightHand => "صلاحية واسعة في المجال",
                AiAuthorityLevel.Rogue => "منفلت وخطر",
                _ => authority.ToString()
            };
        }

        public static string GetActionDisplayName(AiActionType actionType)
        {
            return actionType switch
            {
                AiActionType.BuildSpyNetwork => "بناء شبكة جواسيس",
                AiActionType.ImproveCounterIntelligence => "تحسين مكافحة الاستخبارات",
                AiActionType.InvestigateGovernor => "مراقبة والٍ",
                AiActionType.DisruptFaction => "تفكيك فصيل",
                AiActionType.ProtectHeir => "حماية الوريث",
                AiActionType.ReviewSpymasterReports => "مراجعة تقارير الجواسيس",
                AiActionType.SendReliefToProvince => "إرسال إغاثة لمقاطعة",
                AiActionType.NegotiateMerchantLoan => "تفاوض على قرض تجاري",
                AiActionType.ProtectTradeRoute => "حماية طريق تجاري",
                AiActionType.RecommendConstruction => "اقتراح بناء أو ترقية",
                AiActionType.RequestCouncilMeeting => "طلب اجتماع مجلس",
                AiActionType.WarnAboutSuccessionRisk => "تحذير من خطر الخلافة",
                AiActionType.ProposeMarriageAlliance => "اقتراح زواج سياسي",
                AiActionType.ImproveClergyRelations => "تحسين علاقة رجال الدين",
                AiActionType.PrepareDefense => "تحضير دفاع",
                AiActionType.MoveArmyRecommendation => "توصية بتحريك جيش",
                AiActionType.SendDiplomaticMessage => "إرسال رسالة دبلوماسية",
                AiActionType.OfferPeaceTerms => "عرض شروط صلح",
                AiActionType.SupportHeir => "دعم الوريث",
                AiActionType.CalmAngryGovernor => "تهدئة والٍ غاضب",
                AiActionType.OrganizeSeasonalMarket => "تنظيم سوق موسمي",
                AiActionType.EscortTradeCaravan => "مرافقة قافلة تجارية",
                _ => actionType.ToString()
            };
        }

        private static AiAgentProfile UpsertProfile(GameState state, string characterId, string name, string sourceType, string sourceId, AiAgentRole role)
        {
            var profile = state.AiAgentProfiles.FirstOrDefault(p => p.CharacterId == characterId);
            if (profile == null)
            {
                profile = new AiAgentProfile
                {
                    CharacterId = characterId,
                    Role = role
                };
                state.AiAgentProfiles.Add(profile);
            }

            profile.CharacterName = string.IsNullOrWhiteSpace(name) ? GetRoleDisplayName(role) : name;
            profile.SourceType = sourceType ?? "";
            profile.SourceId = sourceId ?? "";
            profile.Role = role;

            if (string.IsNullOrWhiteSpace(profile.MoralStyle))
                profile.MoralStyle = GetDefaultMoralStyle(role);
            if (string.IsNullOrWhiteSpace(profile.PreferredStrategy))
                profile.PreferredStrategy = GetDefaultStrategy(role);

            return profile;
        }

        private static AiAgentRole? MapRole(RealmCharacter character)
        {
            if (character.SourceType == "Councilor")
            {
                string title = character.CurrentCouncilPosition + " " + character.SourceId + " " + character.Name;
                if (title.Contains("الأول")) return AiAgentRole.FirstMinister;
                if (title.Contains("استخ") || title.Contains("جواسيس")) return AiAgentRole.Spymaster;
                if (title.Contains("جند") || title.Contains("قائد")) return AiAgentRole.MilitaryCommander;
                if (title.Contains("قضا") || title.Contains("دين")) return AiAgentRole.Cleric;
                if (title.Contains("دبلو") || title.Contains("مستشار")) return AiAgentRole.DiplomaticAdvisor;
                return AiAgentRole.FirstMinister;
            }

            return character.Role switch
            {
                CharacterRoleType.Spouse => AiAgentRole.SpouseQueen,
                CharacterRoleType.Governor => AiAgentRole.Governor,
                CharacterRoleType.NeighborRuler => AiAgentRole.NeighborRuler,
                CharacterRoleType.Child => character.Traits.Any(t => t.Contains("ولي")) ? AiAgentRole.Heir : null,
                CharacterRoleType.Commander => AiAgentRole.MilitaryCommander,
                _ => null
            };
        }

        private static void ApplyCharacterStats(GameState state, AiAgentProfile profile, RealmCharacter character)
        {
            if (profile.Role == AiAgentRole.Spymaster)
            {
                var spy = state.Council.Values.FirstOrDefault(c => c.Title.Contains("استخ") || c.Name == character.Name);
                if (spy != null)
                {
                    profile.Loyalty = Math.Clamp(spy.Loyalty, 0, 100);
                    profile.Trust = Math.Clamp(spy.Trust, 0, 100);
                    profile.Ambition = Math.Clamp(spy.Ambition, 0, 100);
                    profile.RiskTolerance = Math.Clamp(30 + spy.IntrigueSkill * 7, 10, 95);
                    if (spy.IsRightHandOfKing && profile.AuthorityLevel < AiAuthorityLevel.RoyalRightHand)
                        profile.MemoryNotes.Add("يثق به الملك كيد يمنى في الظلال، لكن التفويض الرسمي ما زال هو الفيصل.");
                    return;
                }
            }

            if (profile.Role == AiAgentRole.Governor)
            {
                var gov = state.Governors.FirstOrDefault(g => g.Id == character.SourceId);
                if (gov != null)
                {
                    profile.Loyalty = Math.Clamp(gov.Loyalty, 0, 100);
                    profile.Trust = Math.Clamp(50 + gov.OpinionOfKing / 2, 0, 100);
                    profile.Ambition = Math.Clamp(gov.Ambition, 0, 100);
                    profile.RiskTolerance = Math.Clamp(gov.Ambition - gov.Fear + 50, 5, 95);
                    return;
                }
            }

            if (profile.Role == AiAgentRole.NeighborRuler)
            {
                var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == character.SourceId);
                if (neighbor != null)
                {
                    profile.Loyalty = 0;
                    profile.Trust = Math.Clamp(neighbor.Trust, 0, 100);
                    profile.Ambition = Math.Clamp(neighbor.MilitaryAmbition, 0, 100);
                    profile.RiskTolerance = Math.Clamp(neighbor.MilitaryAmbition - neighbor.FearOfPlayer + 50, 5, 95);
                    return;
                }
            }

            if (profile.Role == AiAgentRole.SpouseQueen)
            {
                var wife = state.Wives.FirstOrDefault(w => w.Id == character.SourceId);
                if (wife != null)
                {
                    profile.Loyalty = Math.Clamp(wife.OpinionOfKing + 50, 0, 100);
                    profile.Trust = Math.Clamp(wife.Trust, 0, 100);
                    profile.Ambition = Math.Clamp(wife.Ambition, 0, 100);
                    profile.RiskTolerance = Math.Clamp(25 + wife.IntrigueSkill * 8, 5, 95);
                    return;
                }
            }

            profile.Loyalty = Math.Clamp(profile.Loyalty, 0, 100);
            profile.Trust = Math.Clamp(profile.Trust, 0, 100);
            profile.Ambition = Math.Clamp(profile.Ambition, 0, 100);
        }

        private static List<AiActionType> GetDefaultAllowedActions(AiAgentRole role)
        {
            return role switch
            {
                AiAgentRole.FirstMinister => new List<AiActionType>
                {
                    AiActionType.SendReliefToProvince,
                    AiActionType.NegotiateMerchantLoan,
                    AiActionType.RecommendConstruction,
                    AiActionType.RequestCouncilMeeting,
                    AiActionType.CalmAngryGovernor,
                    AiActionType.OrganizeSeasonalMarket
                },
                AiAgentRole.Spymaster => new List<AiActionType>
                {
                    AiActionType.BuildSpyNetwork,
                    AiActionType.ImproveCounterIntelligence,
                    AiActionType.InvestigateGovernor,
                    AiActionType.DisruptFaction,
                    AiActionType.ProtectHeir,
                    AiActionType.ReviewSpymasterReports
                },
                AiAgentRole.MilitaryCommander => new List<AiActionType>
                {
                    AiActionType.PrepareDefense,
                    AiActionType.MoveArmyRecommendation,
                    AiActionType.ProtectTradeRoute,
                    AiActionType.EscortTradeCaravan
                },
                AiAgentRole.Cleric => new List<AiActionType>
                {
                    AiActionType.ImproveClergyRelations,
                    AiActionType.SupportHeir,
                    AiActionType.WarnAboutSuccessionRisk
                },
                AiAgentRole.DiplomaticAdvisor => new List<AiActionType>
                {
                    AiActionType.SendDiplomaticMessage,
                    AiActionType.ProposeMarriageAlliance,
                    AiActionType.OfferPeaceTerms
                },
                AiAgentRole.Governor => new List<AiActionType>
                {
                    AiActionType.SendReliefToProvince,
                    AiActionType.PrepareDefense,
                    AiActionType.CalmAngryGovernor,
                    AiActionType.WarnAboutSuccessionRisk
                },
                AiAgentRole.FactionLeader => new List<AiActionType>
                {
                    AiActionType.RequestCouncilMeeting,
                    AiActionType.CalmAngryGovernor
                },
                AiAgentRole.NeighborRuler => new List<AiActionType>
                {
                    AiActionType.SendDiplomaticMessage,
                    AiActionType.ProposeMarriageAlliance,
                    AiActionType.NegotiateMerchantLoan,
                    AiActionType.OfferPeaceTerms
                },
                AiAgentRole.SpouseQueen => new List<AiActionType>
                {
                    AiActionType.WarnAboutSuccessionRisk,
                    AiActionType.SupportHeir,
                    AiActionType.CalmAngryGovernor,
                    AiActionType.RequestCouncilMeeting
                },
                AiAgentRole.Heir => new List<AiActionType>
                {
                    AiActionType.WarnAboutSuccessionRisk,
                    AiActionType.RequestCouncilMeeting,
                    AiActionType.SupportHeir
                },
                AiAgentRole.MerchantRepresentative => new List<AiActionType>
                {
                    AiActionType.OrganizeSeasonalMarket,
                    AiActionType.EscortTradeCaravan,
                    AiActionType.NegotiateMerchantLoan,
                    AiActionType.ProtectTradeRoute
                },
                _ => new List<AiActionType> { AiActionType.RequestCouncilMeeting }
            };
        }

        private static int GetDefaultBudget(AiAgentRole role, GameState state)
        {
            return role switch
            {
                AiAgentRole.FirstMinister => Math.Max(120, state.FirstMinister?.MonthlyBudgetPercent * 20 ?? 120),
                AiAgentRole.Spymaster => 250,
                AiAgentRole.MilitaryCommander => 200,
                AiAgentRole.Cleric => 150,
                AiAgentRole.DiplomaticAdvisor => 150,
                AiAgentRole.MerchantRepresentative => 200,
                AiAgentRole.Governor => 100,
                AiAgentRole.SpouseQueen => 80,
                _ => 50
            };
        }

        private static int GetApprovalThreshold(AiAuthorityLevel authority)
        {
            return authority switch
            {
                AiAuthorityLevel.None => 0,
                AiAuthorityLevel.Advisor => 0,
                AiAuthorityLevel.LimitedDelegate => 15,
                AiAuthorityLevel.TrustedDelegate => 35,
                AiAuthorityLevel.RoyalRightHand => 55,
                AiAuthorityLevel.Rogue => 80,
                _ => 0
            };
        }

        private static string GetDefaultMoralStyle(AiAgentRole role)
        {
            return role switch
            {
                AiAgentRole.Spymaster => "Secretive",
                AiAgentRole.Cleric => "Principled",
                AiAgentRole.MerchantRepresentative => "Transactional",
                AiAgentRole.MilitaryCommander => "Martial",
                AiAgentRole.Governor => "LocalHonor",
                _ => "Pragmatic"
            };
        }

        private static string GetDefaultStrategy(AiAgentRole role)
        {
            return role switch
            {
                AiAgentRole.Spymaster => "Prevention",
                AiAgentRole.FirstMinister => "Stability",
                AiAgentRole.MilitaryCommander => "Security",
                AiAgentRole.DiplomaticAdvisor => "Treaties",
                AiAgentRole.MerchantRepresentative => "ProfitAndRoads",
                AiAgentRole.NeighborRuler => "BalanceOfPower",
                _ => "Balance"
            };
        }

        private static string GetStrategyDisplayName(string strategy)
        {
            return strategy switch
            {
                "Prevention" => "الوقاية وكشف الخطر مبكراً",
                "Stability" => "الاستقرار الإداري",
                "Security" => "الأمن والحشد",
                "Treaties" => "المعاهدات وحفظ الوجه",
                "ProfitAndRoads" => "الربح وأمان الطرق",
                "BalanceOfPower" => "توازن القوى",
                "PressureForConcessions" => "الضغط للحصول على تنازلات",
                "FactionPressure" => "حشد الأنصار والضغط السياسي",
                _ => "التوازن"
            };
        }
    }
}
