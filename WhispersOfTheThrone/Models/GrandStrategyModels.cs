using System;
using System.Collections.Generic;

namespace WhispersOfTheThrone.Models
{
    public enum CharacterRoleType
    {
        Ruler,
        Spouse,
        Child,
        Governor,
        Councilor,
        Commander,
        NeighborRuler,
        Courtier
    }

    public enum SecretType
    {
        Corruption,
        Treason,
        Affair,
        Murder,
        Heresy,
        Blackmail,
        FalseReport
    }

    public enum HookStrength
    {
        Weak,
        Strong
    }

    public enum SchemeType
    {
        Sway,
        Murder,
        FabricateHook,
        Discredit,
        SupportHeir,
        SabotageSupplies
    }

    public enum SchemeStage
    {
        Planning,
        RecruitingAgents,
        Preparing,
        Execution,
        Resolved,
        Exposed
    }

    public enum SuccessionLawType
    {
        DesignatedHeir,
        Primogeniture,
        Ultimogeniture,
        CouncilElective,
        Gavelkind // Domain splitting
    }

    public enum CrownAuthorityLevel
    {
        Low,
        Limited,
        High,
        Absolute
    }

    public enum HistoricalEra
    {
        Tribal,
        EarlyMedieval,
        HighMedieval,
        LateMedieval
    }

    public sealed class Innovation
    {
        public string Name { get; set; } = "";
        public int Progress { get; set; }
        public int CostPoints { get; set; }
        public bool IsUnlocked { get; set; }
        public string Era { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public enum WarGoalType
    {
        Conquest,
        Claim,
        Subjugation,
        Defense,
        Rebellion
    }

    public sealed class CharacterSkills
    {
        public int Diplomacy { get; set; } = 1;
        public int Martial { get; set; } = 1;
        public int Stewardship { get; set; } = 1;
        public int Intrigue { get; set; } = 1;
        public int Learning { get; set; } = 1;
    }

    public sealed class RealmCharacter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceType { get; set; } = "";
        public string SourceId { get; set; } = "";
        public string Name { get; set; } = "";
        public CharacterRoleType Role { get; set; } = CharacterRoleType.Courtier;
        public int Age { get; set; } = 20;
        public bool IsDead { get; set; }
        public CharacterSkills Skills { get; set; } = new CharacterSkills();
        public List<string> Traits { get; set; } = new List<string>();
        public List<string> Ambitions { get; set; } = new List<string>();
        public List<string> ClaimIds { get; set; } = new List<string>();
        public List<string> SecretIds { get; set; } = new List<string>();
        public Dictionary<string, int> OpinionByCharacterId { get; set; } = new Dictionary<string, int>();
        public string CurrentCouncilPosition { get; set; } = "";
        public string CurrentCourtPosition { get; set; } = "";
        public string HiddenAgenda { get; set; } = "";

        public int BaseOpinion { get; set; }
        public List<OpinionModifier> OpinionModifiers { get; set; } = new List<OpinionModifier>();

        public int VassalPower { get; set; }
        public int StewardshipSkill { get; set; }
        public int MartialSkill { get; set; }
        public int IntrigueSkill { get; set; }
        public bool IsGenius { get; set; }
        public string CurrentEducationFocus { get; set; } = "";
        public bool IsAwayOnExpedition { get; set; }
        public int FactionProgress { get; set; }
        public int FactionLockDays { get; set; }

        public int CommanderExperience { get; set; } = 0;
        public List<string> CommanderTraits { get; set; } = new List<string>();
        public bool IsPrisoner { get; set; } = false;
        public string PrisonerOfId { get; set; } = "";
        public int CharacterAge { get; set; } = 0;
        public string MotherId { get; set; } = "";
        public string FatherId { get; set; } = "";
        public bool IsAdult { get; set; } = false;
        public int TaxObligationTier { get; set; } = 1;
        public int LevyObligationTier { get; set; } = 1;

        public int GetTotalOpinion()
        {
            int total = BaseOpinion;
            if (OpinionModifiers != null)
            {
                foreach (var m in OpinionModifiers)
                {
                    if (m == null) continue;
                    total += m.Value;
                }
            }
            return Math.Clamp(total, -100, 100);
        }
    }

    public sealed class OpinionModifier
    {
        public string Key { get; set; } = "";
        public int Value { get; set; }
        public int RemainingDays { get; set; }
        public bool IsPermanent { get; set; }
    }

    public sealed class CharacterSecret
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SecretType Type { get; set; } = WhispersOfTheThrone.Models.SecretType.Corruption;
        public string OwnerCharacterId { get; set; } = "";
        public string OwnerId
        {
            get => OwnerCharacterId;
            set => OwnerCharacterId = value ?? "";
        }
        public string SecretType
        {
            get => Type.ToString();
            set
            {
                if (Enum.TryParse<SecretType>(value ?? "", true, out var parsed))
                    Type = parsed;
            }
        }
        public string OwnerName { get; set; } = "";
        public string Summary { get; set; } = "";
        public bool IsKnownToPlayer { get; set; }
        public bool IsDiscovered
        {
            get => IsKnownToPlayer;
            set => IsKnownToPlayer = value;
        }
        public bool IsExposed { get; set; }
        public int Severity { get; set; } = 1;
    }

    public sealed class PoliticalHook
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HolderCharacterId { get; set; } = "Player";
        public string TargetCharacterId { get; set; } = "";
        public string SourceVassalId
        {
            get => TargetCharacterId;
            set => TargetCharacterId = value ?? "";
        }
        public string TargetName { get; set; } = "";
        public string SecretId { get; set; } = "";
        public HookStrength Strength { get; set; } = HookStrength.Weak;
        public string Type
        {
            get => Strength.ToString();
            set
            {
                if (Enum.TryParse<HookStrength>(value ?? "", true, out var parsed))
                    Strength = parsed;
            }
        }
        public int ExpiresDay { get; set; }
        public int ExpirationDays
        {
            get => ExpiresDay;
            set => ExpiresDay = value;
        }
        public bool IsUsed { get; set; }
    }

    public sealed class CharacterClaim
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HolderCharacterId { get; set; } = "";
        public string HolderName { get; set; } = "";
        public string TargetType { get; set; } = "Province";
        public string TargetId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public bool IsPressed { get; set; }
        public bool IsStrong { get; set; } = true;
    }

    public sealed class FeudalContract
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProvinceId { get; set; } = "";
        public string ProvinceName { get; set; } = "";
        public string GovernorId { get; set; } = "";
        public string GovernorName { get; set; } = "";
        public int TaxPercent { get; set; } = 25;
        public int LevyPercent { get; set; } = 25;
        public int Autonomy { get; set; } = 50;
        public bool HasCouncilRights { get; set; }
        public bool ProtectedFromRevocation { get; set; }
    }

    public sealed class ActiveScheme
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SchemeType Type { get; set; } = SchemeType.Sway;
        public SchemeStage Stage { get; set; } = SchemeStage.Planning;
        public string OwnerCharacterId { get; set; } = "Player";
        public string OwnerName { get; set; } = "";
        public string TargetCharacterId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public int Progress { get; set; }
        public int Secrecy { get; set; } = 50;
        public int SuccessChance { get; set; } = 30;
        public int DaysRemaining { get; set; } = 30;
        public List<string> AgentCharacterIds { get; set; } = new List<string>();
        public bool IsPlayerScheme { get; set; } = true;
        public bool IsResolved { get; set; }
    }

    public sealed class WarGoal
    {
        public WarGoalType Type { get; set; } = WarGoalType.Conquest;
        public string TargetProvince { get; set; } = "";
        public string TargetKingdomId { get; set; } = "";
        public string TargetKingdomName { get; set; } = "";
        public int WarScore { get; set; }
        public bool CanNegotiatePeace { get; set; } = true;
    }

    public sealed class EventChainStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TitleKey { get; set; } = "";
        public string BodyKey { get; set; } = "";
        public bool RequiresDecision { get; set; } = true;
        public bool IsResolved { get; set; }
    }

    public sealed class EventChain
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ChainType { get; set; } = "";
        public string ActorCharacterId { get; set; } = "";
        public string ActorName { get; set; } = "";
        public int CurrentStepIndex { get; set; }
        public List<EventChainStep> Steps { get; set; } = new List<EventChainStep>();
        public bool IsComplete { get; set; }
    }

    public sealed class ReignObjective
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ObjectiveType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int Progress { get; set; }
        public int Target { get; set; } = 1;
        public bool IsCompleted { get; set; }
    }

    public sealed class CharacterObjective
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string ObjectiveType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int Urgency { get; set; } = 30;
        public int Progress { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsRevealedToPlayer { get; set; } = true;
        public int LastAdvancedDay { get; set; }
    }

    public sealed class DynastyChronicleEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int DayNumber { get; set; }
        public string DateText { get; set; } = "";
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int GloryChange { get; set; }
        public int Severity { get; set; } = 1;
    }
}
