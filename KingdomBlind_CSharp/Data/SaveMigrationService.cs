using System;
using System.Linq;
using KingdomBlind_CSharp.Models;
using KingdomBlind_CSharp.Systems;

namespace KingdomBlind_CSharp.Data
{
    public static class SaveMigrationService
    {
        public static void Migrate(GameState state)
        {
            if (state == null)
                return;

            EnsureGrandStrategyLists(state);
            EnsureFirstMinisterState(state);
            EnsureRealmCharacters(state);
            EnsureAiDelegationState(state);
            EnsureFeudalContracts(state);
            EnsureReignObjectives(state);
            EnsureWarGoal(state);
        }

        private static void EnsureFirstMinisterState(GameState state)
        {
            state.FirstMinister ??= new FirstMinister();
            if (string.IsNullOrWhiteSpace(state.FirstMinister.CurrentTask))
                state.FirstMinister.CurrentTask = "انتظار الأوامر";
            if (state.FirstMinister.TaskDaysRemaining < 0)
                state.FirstMinister.TaskDaysRemaining = 0;
            if (state.FirstMinister.MonthlyBudgetPercent < 0)
                state.FirstMinister.MonthlyBudgetPercent = 0;

            if (state.MinisterBudgets == null)
                state.MinisterBudgets = new System.Collections.Generic.Dictionary<string, int>();

            if (!state.MinisterBudgets.ContainsKey("first_minister"))
                state.MinisterBudgets["first_minister"] = state.FirstMinister.MonthlyBudgetPercent;
            else
                state.FirstMinister.MonthlyBudgetPercent = state.MinisterBudgets["first_minister"];
        }

        private static void EnsureGrandStrategyLists(GameState state)
        {
            state.RealmCharacters ??= new System.Collections.Generic.List<RealmCharacter>();
            state.CharacterSecrets ??= new System.Collections.Generic.List<CharacterSecret>();
            state.PoliticalHooks ??= new System.Collections.Generic.List<PoliticalHook>();
            state.CharacterClaims ??= new System.Collections.Generic.List<CharacterClaim>();
            state.FeudalContracts ??= new System.Collections.Generic.List<FeudalContract>();
            state.ActiveSchemes ??= new System.Collections.Generic.List<ActiveScheme>();
            state.EventChains ??= new System.Collections.Generic.List<EventChain>();
            state.ReignObjectives ??= new System.Collections.Generic.List<ReignObjective>();
            state.CharacterObjectives ??= new System.Collections.Generic.List<CharacterObjective>();
            state.DynastyChronicle ??= new System.Collections.Generic.List<DynastyChronicleEntry>();
            state.AiAgentProfiles ??= new System.Collections.Generic.List<AiAgentProfile>();
            state.DelegatedAuthoritySettings ??= new DelegatedAuthoritySettings();
            state.AiProposalQueue ??= new System.Collections.Generic.List<AiActionRequest>();
            state.AiActionLog ??= new System.Collections.Generic.List<AiActionLogEntry>();
            state.AiConversationSessions ??= new System.Collections.Generic.List<AiConversationSession>();
            state.AiMeetingHistory ??= new System.Collections.Generic.List<AiMeetingRecord>();
            if (state.DynastyGlory < -1000) state.DynastyGlory = -1000;
            if (state.RoyalDirectorCooldownDays < 0) state.RoyalDirectorCooldownDays = 0;
            if (state.LastRoyalDirectorEventKey == null) state.LastRoyalDirectorEventKey = "";
            state.DelegatedAuthoritySettings.RoleAuthorityLevels ??= new System.Collections.Generic.Dictionary<string, AiAuthorityLevel>();
            state.DelegatedAuthoritySettings.DisabledSimilarProposalKeys ??= new System.Collections.Generic.List<string>();
            if (state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget <= 0)
                state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget = 200;

            foreach (var character in state.RealmCharacters)
            {
                if (string.IsNullOrWhiteSpace(character.Id)) character.Id = Guid.NewGuid().ToString();
                character.Skills ??= new CharacterSkills();
                character.Traits ??= new System.Collections.Generic.List<string>();
                character.Ambitions ??= new System.Collections.Generic.List<string>();
                character.ClaimIds ??= new System.Collections.Generic.List<string>();
                character.SecretIds ??= new System.Collections.Generic.List<string>();
                character.OpinionByCharacterId ??= new System.Collections.Generic.Dictionary<string, int>();
            }

            foreach (var secret in state.CharacterSecrets)
                if (string.IsNullOrWhiteSpace(secret.Id)) secret.Id = Guid.NewGuid().ToString();

            foreach (var hook in state.PoliticalHooks)
                if (string.IsNullOrWhiteSpace(hook.Id)) hook.Id = Guid.NewGuid().ToString();

            foreach (var claim in state.CharacterClaims)
                if (string.IsNullOrWhiteSpace(claim.Id)) claim.Id = Guid.NewGuid().ToString();

            foreach (var contract in state.FeudalContracts)
                if (string.IsNullOrWhiteSpace(contract.Id)) contract.Id = Guid.NewGuid().ToString();

            foreach (var scheme in state.ActiveSchemes)
            {
                if (string.IsNullOrWhiteSpace(scheme.Id)) scheme.Id = Guid.NewGuid().ToString();
                scheme.AgentCharacterIds ??= new System.Collections.Generic.List<string>();
            }

            foreach (var chain in state.EventChains)
            {
                if (string.IsNullOrWhiteSpace(chain.Id)) chain.Id = Guid.NewGuid().ToString();
                chain.Steps ??= new System.Collections.Generic.List<EventChainStep>();
            }

            foreach (var objective in state.ReignObjectives)
                if (string.IsNullOrWhiteSpace(objective.Id)) objective.Id = Guid.NewGuid().ToString();

            foreach (var objective in state.CharacterObjectives)
            {
                if (string.IsNullOrWhiteSpace(objective.Id)) objective.Id = Guid.NewGuid().ToString();
                if (objective.Urgency < 0) objective.Urgency = 0;
                if (objective.Urgency > 100) objective.Urgency = 100;
            }

            foreach (var entry in state.DynastyChronicle)
                if (string.IsNullOrWhiteSpace(entry.Id)) entry.Id = Guid.NewGuid().ToString();

            foreach (var profile in state.AiAgentProfiles)
            {
                profile.AllowedActionTypes ??= new System.Collections.Generic.List<AiActionType>();
                profile.Cooldowns ??= new System.Collections.Generic.Dictionary<string, int>();
                profile.LastActions ??= new System.Collections.Generic.List<string>();
                profile.MemoryNotes ??= new System.Collections.Generic.List<string>();
                if (string.IsNullOrWhiteSpace(profile.CharacterId)) profile.CharacterId = Guid.NewGuid().ToString();
                if (profile.Loyalty < 0 || profile.Loyalty > 100) profile.Loyalty = Math.Clamp(profile.Loyalty, 0, 100);
                if (profile.Trust < 0 || profile.Trust > 100) profile.Trust = Math.Clamp(profile.Trust, 0, 100);
                if (profile.Ambition < 0 || profile.Ambition > 100) profile.Ambition = Math.Clamp(profile.Ambition, 0, 100);
                if (profile.RiskTolerance < 0 || profile.RiskTolerance > 100) profile.RiskTolerance = Math.Clamp(profile.RiskTolerance, 0, 100);
            }

            foreach (var proposal in state.AiProposalQueue)
            {
                if (string.IsNullOrWhiteSpace(proposal.Id)) proposal.Id = Guid.NewGuid().ToString();
                if (proposal.CreatedDate == null) proposal.CreatedDate = "";
                if (proposal.SimilarityKey == null) proposal.SimilarityKey = $"{proposal.Role}:{proposal.ActionType}:{proposal.TargetType}:{proposal.TargetId}";
            }

            foreach (var log in state.AiActionLog)
                if (string.IsNullOrWhiteSpace(log.Id)) log.Id = Guid.NewGuid().ToString();

            foreach (var session in state.AiConversationSessions)
            {
                if (string.IsNullOrWhiteSpace(session.Id)) session.Id = Guid.NewGuid().ToString();
                if (session.Messages == null) session.Messages = new System.Collections.Generic.List<AiConversationMessage>();
                if (session.MaxMessages <= 0) session.MaxMessages = 24;
                if (session.CharacterName == null) session.CharacterName = "";
                if (session.Model == null) session.Model = "";
                if (session.CreatedDate == null) session.CreatedDate = "";
                if (session.LastUpdatedDate == null) session.LastUpdatedDate = "";
                foreach (var message in session.Messages)
                {
                    if (string.IsNullOrWhiteSpace(message.Id)) message.Id = Guid.NewGuid().ToString();
                    if (message.SpeakerId == null) message.SpeakerId = "";
                    if (message.SpeakerName == null) message.SpeakerName = "";
                    if (message.SpeakerRole == null) message.SpeakerRole = "";
                    if (message.Text == null) message.Text = "";
                    if (message.DateText == null) message.DateText = "";
                }
                while (session.Messages.Count > session.MaxMessages)
                    session.Messages.RemoveAt(0);
            }

            foreach (var meeting in state.AiMeetingHistory)
            {
                if (string.IsNullOrWhiteSpace(meeting.Id)) meeting.Id = Guid.NewGuid().ToString();
                meeting.ParticipantNames ??= new System.Collections.Generic.List<string>();
                if (meeting.Topic == null) meeting.Topic = "";
                if (meeting.Scope == null) meeting.Scope = "";
                if (meeting.KingStatement == null) meeting.KingStatement = "";
                if (meeting.DateText == null) meeting.DateText = "";
                if (meeting.Transcript == null) meeting.Transcript = "";
                if (meeting.ProviderSummary == null) meeting.ProviderSummary = "";
            }

            while (state.AiMeetingHistory.Count > 30)
                state.AiMeetingHistory.RemoveAt(0);
        }

        private static void EnsureAiDelegationState(GameState state)
        {
            AiAgentSystem.EnsureAgents(state);
            state.DelegatedAuthoritySettings.AllowAutonomousActions = state.DelegatedAuthoritySettings.AllowAutonomousActions && state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget > 0;
        }

        private static void EnsureRealmCharacters(GameState state)
        {
            UpsertCharacter(
                state,
                "Ruler",
                "player",
                state.RulerName,
                CharacterRoleType.Ruler,
                state.RulerAge,
                new CharacterSkills
                {
                    Diplomacy = 6 + state.Prestige / 50,
                    Martial = 5,
                    Stewardship = 5,
                    Intrigue = 4,
                    Learning = 5 + state.Piety / 80
                },
                state.RulerTraits);

            foreach (var wife in state.Wives.Where(w => w != null))
            {
                UpsertCharacter(
                    state,
                    "Spouse",
                    wife.Id,
                    wife.Name,
                    CharacterRoleType.Spouse,
                    wife.Age,
                    new CharacterSkills
                    {
                        Diplomacy = wife.DiplomacySkill,
                        Martial = Math.Max(1, wife.PoliticalSkill / 2),
                        Stewardship = wife.PoliticalSkill,
                        Intrigue = wife.IntrigueSkill,
                        Learning = Math.Max(1, wife.Trust / 20)
                    },
                    new[] { wife.CourtGoal });
            }

            foreach (var child in state.Children.Where(c => c != null))
            {
                UpsertCharacter(
                    state,
                    "Child",
                    child.Id,
                    child.Name,
                    CharacterRoleType.Child,
                    child.Age,
                    new CharacterSkills
                    {
                        Diplomacy = child.DiplomaticSkill,
                        Martial = child.MilitarySkill,
                        Stewardship = child.EconomicSkill,
                        Intrigue = child.IntrigueSkill,
                        Learning = Math.Max(1, child.Age / 4)
                    },
                    child.IsHeir ? new[] { "ولي العهد" } : Array.Empty<string>());
            }

            foreach (var governor in state.Governors.Where(g => g != null))
            {
                UpsertCharacter(
                    state,
                    "Governor",
                    governor.Id,
                    governor.Name,
                    CharacterRoleType.Governor,
                    governor.Age,
                    new CharacterSkills
                    {
                        Diplomacy = Math.Clamp(3 + governor.OpinionOfKing / 25, 1, 10),
                        Martial = Math.Clamp(governor.MilitaryPower / 12, 1, 10),
                        Stewardship = Math.Clamp(governor.Wealth / 25, 1, 10),
                        Intrigue = Math.Clamp(governor.Ambition / 12, 1, 10),
                        Learning = 4
                    },
                    governor.Traits);
            }

            foreach (var neighbor in state.Neighbors.Where(n => n != null))
            {
                UpsertCharacter(
                    state,
                    "Neighbor",
                    neighbor.Id,
                    string.IsNullOrWhiteSpace(neighbor.RulerName) ? neighbor.Ruler : neighbor.RulerName,
                    CharacterRoleType.NeighborRuler,
                    45,
                    new CharacterSkills
                    {
                        Diplomacy = Math.Clamp(4 + neighbor.Opinion / 25, 1, 10),
                        Martial = Math.Clamp(neighbor.Army / 100, 1, 10),
                        Stewardship = Math.Clamp(neighbor.EconomicStrength / 12, 1, 10),
                        Intrigue = Math.Clamp(neighbor.MilitaryAmbition / 15, 1, 10),
                        Learning = 4
                    },
                    new[] { neighbor.DiplomaticStance, neighbor.PoliticalGoal });
            }

            foreach (var councilor in state.Council.Values.Where(c => c != null))
            {
                if (councilor.HiddenCorruptionRate < 0)
                    councilor.HiddenCorruptionRate = 0;
                if (councilor.HiddenCorruptionRate == 0 && councilor.IsCorrupt)
                    councilor.HiddenCorruptionRate = 5;

                var character = UpsertCharacter(
                    state,
                    "Councilor",
                    councilor.Title,
                    councilor.Name,
                    CharacterRoleType.Councilor,
                    40,
                    new CharacterSkills
                    {
                        Diplomacy = councilor.Title.Contains("دبلو") ? 7 : 4,
                        Martial = councilor.Title.Contains("جند") ? 7 : 3,
                        Stewardship = councilor.Title.Contains("مال") || councilor.Title.Contains("وزير") ? 7 : 4,
                        Intrigue = councilor.Title.Contains("استخ") ? 7 : 4,
                        Learning = councilor.Title.Contains("قضا") ? 7 : 4
                    },
                    Array.Empty<string>());
                character.CurrentCouncilPosition = councilor.Title;
            }
        }

        private static RealmCharacter UpsertCharacter(
            GameState state,
            string sourceType,
            string sourceId,
            string name,
            CharacterRoleType role,
            int age,
            CharacterSkills skills,
            System.Collections.Generic.IEnumerable<string> traits)
        {
            sourceId ??= "";
            var character = state.RealmCharacters.FirstOrDefault(c => c.SourceType == sourceType && c.SourceId == sourceId);
            if (character == null)
            {
                character = new RealmCharacter
                {
                    SourceType = sourceType,
                    SourceId = sourceId
                };
                state.RealmCharacters.Add(character);
            }

            character.Name = string.IsNullOrWhiteSpace(name) ? role.ToString() : name;
            character.Role = role;
            character.Age = Math.Max(0, age);
            character.IsDead = role == CharacterRoleType.Ruler ? state.RulerIsDead : character.IsDead;
            character.Skills = skills ?? new CharacterSkills();

            foreach (var trait in traits ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(trait) && !character.Traits.Contains(trait))
                    character.Traits.Add(trait);
            }

            if (character.Ambitions.Count == 0)
                character.Ambitions.Add(GetDefaultAmbition(role));

            if (string.IsNullOrWhiteSpace(character.HiddenAgenda))
                character.HiddenAgenda = GetDefaultAgenda(role, character);

            return character;
        }

        private static string GetDefaultAmbition(CharacterRoleType role)
        {
            return role switch
            {
                CharacterRoleType.Spouse => "حماية نفوذ جناحها داخل القصر",
                CharacterRoleType.Child => "تثبيت موقعه في الخلافة",
                CharacterRoleType.Governor => "توسيع نفوذ مقاطعته",
                CharacterRoleType.Councilor => "إثبات كفاءته أمام الملك",
                CharacterRoleType.NeighborRuler => "تأمين حدوده ومصالحه",
                _ => "حفظ السلالة واستقرار العرش"
            };
        }

        private static string GetDefaultAgenda(CharacterRoleType role, RealmCharacter character)
        {
            return role switch
            {
                CharacterRoleType.Spouse => character.Skills.Intrigue >= 7 ? "نسج شبكة نفوذ سرية في البلاط" : "تعزيز مكانتها عبر القرابة والولاء",
                CharacterRoleType.Governor => character.Skills.Martial >= 7 ? "رفع استقلال المقاطعة عسكرياً" : "موازنة الولاء بالمصلحة",
                CharacterRoleType.Councilor => "الحفاظ على المنصب وتوسيع التأثير",
                _ => "انتظار فرصة سياسية مناسبة"
            };
        }

        private static void EnsureFeudalContracts(GameState state)
        {
            foreach (var province in state.Provinces)
            {
                if (string.IsNullOrWhiteSpace(province.Id))
                    province.Id = Guid.NewGuid().ToString();

                var governor = state.Governors.FirstOrDefault(g => g.ProvinceId == province.Id || g.ProvinceName == province.Name);
                string governorId = governor?.Id ?? province.GovernorId ?? "";

                if (state.FeudalContracts.Any(c => c.ProvinceId == province.Id))
                    continue;

                state.FeudalContracts.Add(new FeudalContract
                {
                    ProvinceId = province.Id,
                    ProvinceName = province.Name,
                    GovernorId = governorId,
                    GovernorName = governor?.Name ?? province.GovernorName ?? province.Vassal ?? "",
                    TaxPercent = Math.Clamp(20 + province.Opinion / 10, 10, 40),
                    LevyPercent = Math.Clamp(20 + province.LocalGarrison / 200, 10, 50),
                    Autonomy = Math.Clamp(50 - province.Opinion / 3, 20, 80),
                    HasCouncilRights = province.Opinion > 60,
                    ProtectedFromRevocation = province.HasRevocationReason == false && province.Opinion > 70
                });
            }
        }

        private static void EnsureReignObjectives(GameState state)
        {
            if (state.ReignObjectives.Count > 0)
                return;

            state.ReignObjectives.Add(new ReignObjective
            {
                ObjectiveType = "UnifySyria",
                Title = "توحيد الشام",
                Description = "ضم ثلاث مقاطعات جديدة أو إخضاع ثلاث دول تابعة.",
                Target = 3,
                Progress = state.Neighbors.Count(n => n.Relation == "تابع" || n.Relation == "مضمومة")
            });

            state.ReignObjectives.Add(new ReignObjective
            {
                ObjectiveType = "SecureSuccession",
                Title = "تأمين الخلافة",
                Description = "امتلاك وريث حي ورضا سياسي كافٍ لتجنب أزمة خلافة.",
                Target = 1,
                Progress = string.IsNullOrWhiteSpace(state.HeirName) ? 0 : 1
            });

            state.ReignObjectives.Add(new ReignObjective
            {
                ObjectiveType = "RepairTreasury",
                Title = "إصلاح الخزينة",
                Description = "الوصول إلى 2000 ذهب مع ثقة تجار لا تقل عن 60.",
                Target = 2000,
                Progress = Math.Min(state.Gold, 2000)
            });
        }

        private static void EnsureWarGoal(GameState state)
        {
            if (state.ActiveWar == null)
            {
                state.CurrentWarGoal = null;
                return;
            }

            if (state.CurrentWarGoal != null)
                return;

            var neighbor = state.ActiveWar.NeighborIdx >= 0 && state.ActiveWar.NeighborIdx < state.Neighbors.Count
                ? state.Neighbors[state.ActiveWar.NeighborIdx]
                : null;

            state.CurrentWarGoal = new WarGoal
            {
                Type = state.ActiveWar.Type == "conquest" ? WarGoalType.Conquest : WarGoalType.Claim,
                TargetProvince = state.ActiveWar.TargetProvince,
                TargetKingdomId = neighbor?.Id ?? "",
                TargetKingdomName = neighbor?.Name ?? "",
                WarScore = state.ActiveWar.WarScore
            };
        }
    }
}
