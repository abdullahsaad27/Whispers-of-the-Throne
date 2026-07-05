using System;
using System.Collections.Generic;

namespace WhispersOfTheThrone.Models
{
    public sealed class CharacterGoal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public string GoalType { get; set; } = ""; // Survive, GatherWealth, SeizePower, ProtectFamily, ExpandTerritory, SeekRevenge
        public int Priority { get; set; } = 1; // 1-100
        public int DaysToAchieve { get; set; } = 90;
        public bool IsActive { get; set; } = true;
        public int CreatedDay { get; set; }
    }

    public sealed class GoalAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GoalId { get; set; } = "";
        public string ActionType { get; set; } = ""; // BribeOfficial, MarryIntoPower, PoisonRival, FundFaction, RequestForeignAid, BuildArmy, SpreadRumors, BetrayAlly, SabotageEconomy
        public string TargetId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public int Progress { get; set; } // 0-100
        public int DaysRemaining { get; set; } = 14;
        public bool IsExecuted { get; set; }
    }

    public sealed class GoalPriorityRule
    {
        public string Precondition { get; set; } = "";
        public string GoalType { get; set; } = "";
        public int PriorityBoost { get; set; } = 0;
    }

    public sealed class Grudge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HolderCharacterId { get; set; } = "";
        public string TargetCharacterId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public string GrudgeType { get; set; } = ""; // Insult, BrokenPromise, TitleRevoked, FamilyKilled, TerritoryLost, Betrayed, Humiliated
        public int Severity { get; set; } = 0; // 0-100
        public int CreatedDay { get; set; }
        public int LastActedUponDay { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> ActionsTaken { get; set; } = new List<string>();
    }

    public sealed class SecretAlliance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GovernorId { get; set; } = "";
        public string GovernorName { get; set; } = "";
        public string NeighborId { get; set; } = "";
        public string NeighborName { get; set; } = "";
        public string ProvinceId { get; set; } = "";
        public string ProvinceName { get; set; } = "";
        public int NegotiatedDay { get; set; }
        public int Secrecy { get; set; } = 70; // 0-100, chance of detection
        public bool IsExposed { get; set; }
        public bool IsActivated { get; set; } // betrayal war started
        public int GoldBribe { get; set; }
        public string Terms { get; set; } = "";
    }

    public sealed class DefensiveCoalition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public List<string> MemberNeighborIds { get; set; } = new List<string>();
        public string TargetKingdomId { get; set; } = "";
        public int FormationDay { get; set; }
        public int ThreatLevel { get; set; } = 0; // 0-100
        public bool IsActive { get; set; } = true;
        public int CoalitionStrength { get; set; }
    }
}
