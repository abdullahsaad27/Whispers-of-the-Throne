using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class CourtSessionTests
{
    private static RealmCharacter SeedGovernor(GameState state, string name, int baseOpinion = 0, string sourceId = null)
    {
        var rc = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = "Governor",
            SourceId = sourceId ?? ("gov_" + Guid.NewGuid().ToString("N").Substring(0, 6)),
            Role = CharacterRoleType.Governor,
            BaseOpinion = baseOpinion,
            IsAdult = true
        };
        state.RealmCharacters.Add(rc);
        return rc;
    }

    private static RealmCharacter SeedAdultCourtier(GameState state, string name)
    {
        var rc = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = "Courtier",
            SourceId = "court_" + Guid.NewGuid().ToString("N").Substring(0, 6),
            Role = CharacterRoleType.Courtier,
            IsAdult = true
        };
        state.RealmCharacters.Add(rc);
        return rc;
    }

    private static GameState CreateReadyState()
    {
        var state = new GameState();
        state.Gold = 1000;
        state.RealmCharacters = new System.Collections.Generic.List<RealmCharacter>();
        state.Wives = new System.Collections.Generic.List<Spouse>();
        state.Grudges = new System.Collections.Generic.List<Grudge>();
        state.DaysSinceLastCourt = GameState.HoldCourtCooldownDays;
        return state;
    }

    [Fact]
    public void ExecuteHoldCourtSession_FailsIfCooldownNotMet()
    {
        var state = CreateReadyState();
        state.DaysSinceLastCourt = GameState.HoldCourtCooldownDays - 1;

        var res = CourtEventSystem.ExecuteHoldCourtSession(state);

        Assert.False(res.Success);
        Assert.Contains("5 سنوات", res.MainMessage);
    }

    [Fact]
    public void ExecuteHoldCourtSession_SucceedsAndQueues3Petitioners()
    {
        var state = CreateReadyState();
        SeedGovernor(state, "والي_أ", sourceId: "g1");
        SeedGovernor(state, "والي_ب", sourceId: "g2");
        SeedGovernor(state, "والي_ج", sourceId: "g3");

        var res = CourtEventSystem.ExecuteHoldCourtSession(state);

        Assert.True(res.Success);
        Assert.Equal(CourtEventSystem.SessionPetitionCount, CourtEventSystem.GetRemainingPetitionCount());
    }

    [Fact]
    public void GetNextPetitionInSession_ReturnsFirstSecondThirdInOrder()
    {
        var state = CreateReadyState();
        SeedGovernor(state, "والي_أ", sourceId: "g1");
        SeedGovernor(state, "والي_ب", sourceId: "g2");

        var res = CourtEventSystem.ExecuteHoldCourtSession(state);
        Assert.True(res.Success);

        var first = CourtEventSystem.GetNextPetitionInSession(state);
        var second = CourtEventSystem.GetNextPetitionInSession(state);
        var third = CourtEventSystem.GetNextPetitionInSession(state);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, third.Id);
        Assert.NotEqual(first.Id, third.Id);
    }

    [Fact]
    public void GetNextPetitionInSession_ReturnsNullAfterThree()
    {
        var state = CreateReadyState();
        SeedGovernor(state, "والي_أ", sourceId: "g1");
        SeedGovernor(state, "والي_ب", sourceId: "g2");

        CourtEventSystem.ExecuteHoldCourtSession(state);
        CourtEventSystem.GetNextPetitionInSession(state);
        CourtEventSystem.GetNextPetitionInSession(state);
        CourtEventSystem.GetNextPetitionInSession(state);

        var next = CourtEventSystem.GetNextPetitionInSession(state);
        Assert.Null(next);
    }

    [Fact]
    public void EndCourtSession_Adds200PrestigeAndResetsCooldown()
    {
        var state = CreateReadyState();
        SeedGovernor(state, "والي_أ", sourceId: "g1");
        SeedGovernor(state, "والي_ب", sourceId: "g2");
        int prestigeBefore = state.Prestige;

        CourtEventSystem.ExecuteHoldCourtSession(state);
        var res = CourtEventSystem.EndCourtSession(state);

        Assert.True(res.Success);
        Assert.Equal(prestigeBefore + CourtEventSystem.SessionPrestigeReward, state.Prestige);
        Assert.Equal(0, state.DaysSinceLastCourt);
    }

    [Fact]
    public void EndCourtSession_ClearsQueue()
    {
        var state = CreateReadyState();
        SeedGovernor(state, "والي_أ", sourceId: "g1");
        SeedGovernor(state, "والي_ب", sourceId: "g2");

        CourtEventSystem.ExecuteHoldCourtSession(state);
        CourtEventSystem.EndCourtSession(state);

        Assert.Equal(0, CourtEventSystem.GetRemainingPetitionCount());
        Assert.False(CourtEventSystem.HasPendingSession());
    }

    [Fact]
    public void ExecuteOptionA_LandDispute_StillWorks()
    {
        var state = CreateReadyState();
        var vassalA = SeedGovernor(state, "والي_أ", sourceId: "govA");
        var vassalB = SeedGovernor(state, "والي_ب", sourceId: "govB");
        var petition = new CourtPetition
        {
            Id = "p1",
            Title = "اختبار",
            Description = "اختبار",
            OptionATitle = "أ",
            OptionADescription = "أ",
            OptionBTitle = "ب",
            OptionBDescription = "ب",
            TargetVassalAId = vassalA.Id,
            TargetVassalBId = vassalB.Id,
            PetitionerId = vassalA.Id,
            ScenarioType = "LandDispute"
        };

        var res = CourtEventSystem.ExecuteOptionA(state, petition);

        Assert.True(res.Success);
        Assert.Contains(vassalA.OpinionModifiers, m => m.Key == "CourtPetition_FavoredInLandDispute" && m.Value == 40 && m.IsPermanent);
    }

    [Fact]
    public void ExecuteOptionB_FinancialRequest_AddsStress()
    {
        var state = CreateReadyState();
        state.RulerStress = 0;
        var petition = new CourtPetition { ScenarioType = "FinancialRequest" };

        var res = CourtEventSystem.ExecuteOptionB(state, petition);

        Assert.True(res.Success);
        Assert.Equal(15, state.RulerStress);
    }

    [Fact]
    public void ReconcileOldSaves_InitializesDaysSinceLastCourt()
    {
        var state = new GameState { DaysSinceLastCourt = -5 };
        state.ReconcileOldSaves();
        Assert.Equal(0, state.DaysSinceLastCourt);
    }

    [Fact]
    public void AdvanceDay_IncrementsDaysSinceLastCourt()
    {
        var state = CreateReadyState();
        state.SuppressRandomMajorEvents = true;
        foreach (var neighbor in state.Neighbors) neighbor.DaysUntilNextMove = 9999;
        foreach (var governor in state.Governors) governor.DaysUntilNextMove = 9999;
        state.Time.IsPaused = false;

        int before = state.DaysSinceLastCourt;
        CalendarTimeSystem.AdvanceDay(state);

        Assert.True(state.DaysSinceLastCourt > before);
    }

    [Fact]
    public void GenerateRandomPetition_StillWorks_LandDispute()
    {
        var state = CreateReadyState();
        state.Gold = 500;
        SeedGovernor(state, "والي_أ", baseOpinion: 20, sourceId: "gov1");
        SeedGovernor(state, "والي_ب", baseOpinion: 30, sourceId: "gov2");
        SeedGovernor(state, "والي_ج", baseOpinion: 10, sourceId: "gov3");

        CourtPetition found = null;
        for (int i = 0; i < 60 && found == null; i++)
        {
            found = CourtEventSystem.GenerateRandomPetition(state);
        }

        Assert.NotNull(found);
        Assert.False(string.IsNullOrEmpty(found.ScenarioType));
    }

    [Fact]
    public void GenerateRandomPetition_ReturnsNull_WhenNoGovernors()
    {
        var state = CreateReadyState();
        state.Gold = 500;
        SeedAdultCourtier(state, "قاض");

        CourtPetition result = null;
        for (int i = 0; i < 40; i++)
        {
            var trial = CourtEventSystem.GenerateRandomPetition(state);
            if (trial != null && trial.ScenarioType == "LandDispute") { result = trial; break; }
        }
        Assert.Null(result);
    }

    [Fact]
    public void FinancialRequest_OptionA_DeductsGoldAndSetsTradeBonus()
    {
        var state = CreateReadyState();
        state.Gold = 1000;
        state.PendingTradeProfitBonus = 0;

        var petition = new CourtPetition
        {
            Id = "p3",
            Title = "اختبار",
            Description = "اختبار",
            OptionATitle = "أ",
            OptionADescription = "أ",
            OptionBTitle = "ب",
            OptionBDescription = "ب",
            TargetVassalAId = "",
            TargetVassalBId = "",
            PetitionerId = "",
            ScenarioType = "FinancialRequest"
        };

        var res = CourtEventSystem.ExecuteOptionA(state, petition);

        Assert.True(res.Success);
        Assert.Equal(850, state.Gold);
        Assert.Equal(75, state.PendingTradeProfitBonus);
    }

    [Fact]
    public void PendingTradeProfitBonus_IsConsumedByMonthlyEconomy()
    {
        var state = CreateReadyState();
        state.Gold = 1000;
        state.PendingTradeProfitBonus = 75;
        state.Loans = new System.Collections.Generic.List<Loan>();
        state.BuildingQueue = new System.Collections.Generic.List<BuildingTask>();
        state.TaxLevel = "متوسط";
        state.Time.Month = 5;
        state.Time.Day = TimeState.GetDaysInMonth(5, 1071);
        state.Time.Year = 1071;

        EconomySystem.ProcessDailyEconomy(state);

        Assert.Equal(0, state.PendingTradeProfitBonus);
    }

    [Fact]
    public void ExecuteHoldCourtSession_FailsIfCooldownReachedButNoGovernors()
    {
        var state = CreateReadyState();
        state.DaysSinceLastCourt = GameState.HoldCourtCooldownDays;
        state.Gold = 0;
        state.RealmCharacters = new System.Collections.Generic.List<RealmCharacter>();

        var res = CourtEventSystem.ExecuteHoldCourtSession(state);

        Assert.True(res.Success);
        Assert.Equal(CourtEventSystem.SessionPetitionCount, CourtEventSystem.GetRemainingPetitionCount());
    }

    [Fact]
    public void ExecuteHoldCourtSession_ResetsCooldownToZero_AfterCompletion()
    {
        var state = CreateReadyState();
        SeedGovernor(state, "والي_أ", sourceId: "g1");
        SeedGovernor(state, "والي_ب", sourceId: "g2");
        state.DaysSinceLastCourt = GameState.HoldCourtCooldownDays + 50;

        CourtEventSystem.ExecuteHoldCourtSession(state);
        CourtEventSystem.EndCourtSession(state);

        Assert.Equal(0, state.DaysSinceLastCourt);
    }
}
