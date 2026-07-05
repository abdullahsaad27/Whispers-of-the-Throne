using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class AiContextBuilderTests
{
    private static AiAgentProfile MakeProfile(AiAgentRole role, string name = "Test")
    {
        return new AiAgentProfile
        {
            CharacterName = name,
            Role = role,
            CharacterId = Guid.NewGuid().ToString()
        };
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsActiveCivilWar()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1", "r2" };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = FactionWarEngine.RebelArmyPrefix + "a", TotalSoldiers = 600, Name = "RebelA" }
        };
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("نشطة") && f.Contains("المتمردين"));
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsNoCivilWar()
    {
        var state = new GameState();
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("لا توجد حرب أهلية"));
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsEpidemic()
    {
        var state = new GameState();
        state.Provinces.Add(new Province
        {
            Name = "P",
            Id = "p1",
            ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
            {
                new ActiveDisease { Type = "طاعون", InfectionRate = 30, MortalityRate = 15, DaysRemaining = 5 }
            },
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        });
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("وبائي") || f.Contains("وباء"));
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsTournament()
    {
        var state = new GameState();
        state.IsTournamentActive = true;
        state.TournamentStage = 1;
        state.TournamentDaysRemaining = 3;
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("البطولة الملكية"));
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsCaravan()
    {
        var state = new GameState();
        state.IsCaravanActive = true;
        state.ActiveCaravanLeaderId = "leader1";
        state.CaravanHazardPenalty = 5;
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("قافلة"));
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsIntrigueScheme()
    {
        var state = new GameState();
        state.ActiveSchemeType = "Seduction";
        state.ActiveSchemeTargetId = "target1";
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("دسيسة"));
    }

    [Fact]
    public void AppendGlobalAwareness_ReportsGreatJihad()
    {
        var state = new GameState();
        state.IsGreatJihadActive = true;
        state.GreatJihadDaysRemaining = 10;
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(state, ctx);

        Assert.Contains(ctx.KnownFacts, f => f.Contains("الجهاد الكبير"));
    }

    [Fact]
    public void GetRoleFilteredFacts_ForCommander_IncludesCivilWarRebelSize()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = FactionWarEngine.RebelArmyPrefix + "a", TotalSoldiers = 800, Name = "RebelA", CurrentProvince = "home" }
        };

        var facts = AiContextBuilder.GetRoleFilteredFacts(state, AiAgentRole.MilitaryCommander);

        Assert.NotEmpty(facts);
        Assert.Contains(facts, f => f.Contains("800") || f.Contains("حرب أهلية"));
    }

    [Fact]
    public void GetRoleFilteredFacts_ForSpymaster_IncludesRebelLeaders()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        state.RealmCharacters.Add(new RealmCharacter { Id = "r1", Name = "الخائن الكبير", IsDead = false });

        var facts = AiContextBuilder.GetRoleFilteredFacts(state, AiAgentRole.Spymaster);

        Assert.Contains(facts, f => f.Contains("الخائن الكبير") || f.Contains("المتمردين"));
    }

    [Fact]
    public void GetRoleFilteredFacts_ForFirstMinister_ReportsEpidemic()
    {
        var state = new GameState();
        state.IsCapitalIsolated = true;
        state.Provinces.Add(new Province
        {
            Name = "P",
            Id = "p1",
            ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
            {
                new ActiveDisease { Type = "طاعون", InfectionRate = 30, MortalityRate = 15, DaysRemaining = 5 }
            },
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        });

        var facts = AiContextBuilder.GetRoleFilteredFacts(state, AiAgentRole.FirstMinister);

        Assert.Contains(facts, f => f.Contains("وباء") || f.Contains("معزولة"));
    }

    [Fact]
    public void GetRoleFilteredFacts_ForCleric_IncludesGreatJihad()
    {
        var state = new GameState();
        state.IsGreatJihadActive = true;
        state.GreatJihadDaysRemaining = 7;

        var facts = AiContextBuilder.GetRoleFilteredFacts(state, AiAgentRole.Cleric);

        Assert.Contains(facts, f => f.Contains("جهاد"));
    }

    [Fact]
    public void GetRoleFilteredFacts_ForSpouseQueen_ReferencesDynasty()
    {
        var state = new GameState();
        state.DynastyRenown = 50;
        state.IsTournamentActive = true;

        var facts = AiContextBuilder.GetRoleFilteredFacts(state, AiAgentRole.SpouseQueen);

        Assert.NotEmpty(facts);
    }

    [Fact]
    public void GetContextForRole_BuildsContextForProfile()
    {
        var state = new GameState();
        var profile = MakeProfile(AiAgentRole.MilitaryCommander);

        var ctx = AiContextBuilder.GetContextForRole(state, profile);

        Assert.NotNull(ctx);
        Assert.Equal(AiAgentRole.MilitaryCommander, ctx.Role);
        Assert.NotEmpty(ctx.KnownFacts);
    }

    [Fact]
    public void BuildContext_AppendsGlobalAwarenessBeforeRoleFacts()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        var profile = MakeProfile(AiAgentRole.MilitaryCommander);

        var ctx = AiContextBuilder.BuildContext(state, profile);

        Assert.NotNull(ctx);
        Assert.NotEmpty(ctx.KnownFacts);
        Assert.Contains(ctx.KnownFacts, f => f.Contains("التاريخ"));
        Assert.Contains(ctx.KnownFacts, f => f.Contains("الذهب"));
    }

    [Fact]
    public void GoalOrientedActionSystem_GetRoleAwareRecommendations_ForCommanderDuringCivilWar()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        state.Armies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "royal", Name = "Royal", TotalSoldiers = 100, CurrentProvince = "دمشق" }
        };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "a",
                Name = "Rebel",
                TotalSoldiers = 1000,
                CurrentProvince = "حلب"
            }
        };

        var recs = GoalOrientedActionSystem.GetRoleAwareRecommendations(state, "MilitaryCommander");

        Assert.NotEmpty(recs);
    }

    [Fact]
    public void GoalOrientedActionSystem_GetRoleAwareRecommendations_ForSpymasterIncludesScheme()
    {
        var state = new GameState();
        state.ActiveSchemeType = "Seduction";
        state.ActiveSchemeTargetId = "target1";

        var recs = GoalOrientedActionSystem.GetRoleAwareRecommendations(state, "Spymaster");

        Assert.NotEmpty(recs);
    }

    [Fact]
    public void GoalOrientedActionSystem_GetRoleAwareRecommendations_ForClericIncludesJihad()
    {
        var state = new GameState();
        state.IsGreatJihadActive = true;
        state.ReligiousFervor = 80;

        var recs = GoalOrientedActionSystem.GetRoleAwareRecommendations(state, "Cleric");

        Assert.NotEmpty(recs);
        Assert.Contains(recs, r => r.Contains("الجهاد") || r.Contains("الحماس") || r.Contains("الصلوات"));
    }

    [Fact]
    public void GoalOrientedActionSystem_GetRoleAwareRecommendations_ForVizierIncludesEpidemic()
    {
        var state = new GameState();
        state.Provinces.Add(new Province
        {
            Name = "P",
            Id = "p1",
            ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
            {
                new ActiveDisease { Type = "طاعون", InfectionRate = 30, MortalityRate = 15, DaysRemaining = 5 }
            },
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        });

        var recs = GoalOrientedActionSystem.GetRoleAwareRecommendations(state, "FirstMinister");

        Assert.NotEmpty(recs);
        Assert.Contains(recs, r => r.Contains("الأوبئة") || r.Contains("وباء") || r.Contains("الحجر"));
    }

    [Fact]
    public void AppendGlobalAwareness_HandlesNullStateGracefully()
    {
        var ctx = new AiAgentContext();
        AiContextBuilder.AppendGlobalAwareness(null, ctx);

        Assert.Empty(ctx.KnownFacts);
    }
}
