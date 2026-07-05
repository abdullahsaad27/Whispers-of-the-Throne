using System;
using System.Collections.Generic;
using System.Linq;
using WhispersOfTheThrone.Audio;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class TournamentSystemTests
{
    private static RealmCharacter Seed(GameState state, string name, int martialSkill = 0, bool isAdult = true, bool isDead = false, params string[] traits)
    {
        var rc = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = "Courtier",
            SourceId = name,
            Role = CharacterRoleType.Courtier,
            MartialSkill = martialSkill,
            IsAdult = isAdult,
            IsDead = isDead
        };
        if (traits != null)
        {
            foreach (var t in traits) rc.Traits.Add(t);
        }
        state.RealmCharacters.Add(rc);
        return rc;
    }

    private static GameState MakeReadyState(int gold = 1000)
    {
        var state = new GameState();
        state.Gold = gold;
        state.RulerStress = 20;
        state.RealmCharacters ??= new List<RealmCharacter>();
        state.RealmCharacters.Clear();
        Seed(state, "القائد الفاتح", martialSkill: 12);
        Seed(state, "الفارس خالد", martialSkill: 9);
        Seed(state, "الفارس سليم", martialSkill: 7);
        Seed(state, "الفارس منصور", martialSkill: 6);
        Seed(state, "الفارس هشام", martialSkill: 5);
        Seed(state, "الفارس عامر", martialSkill: 3);
        return state;
    }

    [Fact]
    public void InitiateGrandTournament_SucceedsWhenGoldIsSufficient()
    {
        var state = MakeReadyState(gold: 1000);

        var result = TournamentSystem.InitiateGrandTournament(state);

        Assert.True(result.Success);
        Assert.Equal(600, state.Gold);
        Assert.True(state.IsTournamentActive);
        Assert.Equal(1, state.TournamentStage);
        Assert.Equal(3, state.TournamentDaysRemaining);
    }

    [Fact]
    public void InitiateGrandTournament_FailsWhenGoldIsInsufficient()
    {
        var state = MakeReadyState(gold: 200);

        var result = TournamentSystem.InitiateGrandTournament(state);

        Assert.False(result.Success);
        Assert.Equal(200, state.Gold);
        Assert.False(state.IsTournamentActive);
        Assert.Equal(0, state.TournamentStage);
    }

    [Fact]
    public void InitiateGrandTournament_FailsWhenTournamentAlreadyActive()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);
        int goldAfterFirst = state.Gold;

        var result = TournamentSystem.InitiateGrandTournament(state);

        Assert.False(result.Success);
        Assert.Equal(goldAfterFirst, state.Gold);
    }

    [Fact]
    public void InitiateGrandTournament_Deducts400GoldAndAdds40RulerStress()
    {
        var state = MakeReadyState(gold: 1000);
        state.RulerStress = 30;

        TournamentSystem.InitiateGrandTournament(state);

        Assert.Equal(600, state.Gold);
        Assert.Equal(70, state.RulerStress);
    }

    [Fact]
    public void InitiateGrandTournament_RulerStressIsClampedAt100()
    {
        var state = MakeReadyState(gold: 1000);
        state.RulerStress = 95;

        TournamentSystem.InitiateGrandTournament(state);

        Assert.Equal(100, state.RulerStress);
    }

    [Fact]
    public void InitiateGrandTournament_AddsPermanentOpinionToAllNonRulerCharacters()
    {
        var state = MakeReadyState(gold: 1000);

        TournamentSystem.InitiateGrandTournament(state);

        foreach (var c in state.RealmCharacters)
        {
            Assert.Contains(c.OpinionModifiers, m =>
                m.Key == TournamentSystem.AttendedRoyalTournamentOpinionKey &&
                m.Value == TournamentSystem.AttendedRoyalTournamentOpinionValue &&
                m.IsPermanent);
        }
    }

    [Fact]
    public void InitiateGrandTournament_PopulatesParticipantsUpToSixMartialCharacters()
    {
        var state = MakeReadyState(gold: 1000);
        for (int i = 0; i < 8; i++) Seed(state, $"فارس إضافي {i}", martialSkill: 10);

        TournamentSystem.InitiateGrandTournament(state);

        Assert.NotEmpty(state.TournamentParticipants);
        Assert.True(state.TournamentParticipants.Count <= TournamentSystem.MaxTournamentParticipants);

        foreach (var id in state.TournamentParticipants)
        {
            var c = state.RealmCharacters.First(rc => rc.Id == id);
            Assert.False(c.IsDead);
        }
    }

    [Fact]
    public void SimulateJoustingMatches_AdvancesStageToTwoAndAwardsPrestige()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);
        int prestigeBefore = state.Prestige;

        TournamentSystem.SimulateJoustingMatches(state);

        Assert.Equal(2, state.TournamentStage);
        Assert.Equal(prestigeBefore + TournamentSystem.JoustingPrestigeReward, state.Prestige);
    }

    [Fact]
    public void SimulateJoustingMatches_SetsTournamentChampionId()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);

        TournamentSystem.SimulateJoustingMatches(state);

        Assert.False(string.IsNullOrWhiteSpace(state.TournamentChampionId));
        var champion = state.RealmCharacters.First(c => c.Id == state.TournamentChampionId);
        Assert.Contains(TournamentSystem.TournamentChampionTrait, champion.Traits);
    }

    [Fact]
    public void ProcessTournamentAccidents_AddsGrudgeWhenHazardTriggers()
    {
        int accidentsFound = 0;
        for (int attempt = 0; attempt < 200 && accidentsFound == 0; attempt++)
        {
            var state = MakeReadyState(gold: 1000);
            state.Grudges ??= new List<Grudge>();
            state.Grudges.Clear();
            TournamentSystem.InitiateGrandTournament(state);
            var beforeCount = state.Grudges.Count;

            TournamentSystem.ProcessTournamentAccidents(state);

            if (state.Grudges.Count > beforeCount) accidentsFound++;
        }
        Assert.True(accidentsFound > 0, "Expected at least one accident to be triggered in 200 attempts");
    }

    [Fact]
    public void ProcessTournamentAccidents_AddsMutilatedTraitToVictim()
    {
        int victimFound = 0;
        for (int attempt = 0; attempt < 200 && victimFound == 0; attempt++)
        {
            var state = MakeReadyState(gold: 1000);
            TournamentSystem.InitiateGrandTournament(state);

            TournamentSystem.ProcessTournamentAccidents(state);

            if (state.TournamentAccidentLog != null && state.TournamentAccidentLog.Count > 0)
            {
                var anyMutilated = state.RealmCharacters.Any(c => c.Traits.Contains(TournamentSystem.TournamentMutilatedTrait));
                if (anyMutilated) victimFound++;
            }
        }
        Assert.True(victimFound > 0, "Expected at least one attempt to add Mutilated trait to a victim");
    }

    [Fact]
    public void CompleteTournamentFeast_AddsRenownAndResetsState()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);
        TournamentSystem.SimulateJoustingMatches(state);
        TournamentSystem.ProcessTournamentAccidents(state);
        int renownBefore = state.DynastyRenown;

        var result = TournamentSystem.CompleteTournamentFeast(state);

        Assert.True(result.Success);
        Assert.Equal(renownBefore + TournamentSystem.FeastRenownReward, state.DynastyRenown);
        Assert.False(state.IsTournamentActive);
        Assert.Equal(0, state.TournamentStage);
        Assert.Equal(0, state.TournamentDaysRemaining);
        Assert.Empty(state.TournamentParticipants);
        Assert.Empty(state.TournamentAccidentLog);
    }

    [Fact]
    public void CompleteTournamentFeast_SetsIsTournamentActiveToFalse()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);
        Assert.True(state.IsTournamentActive);

        TournamentSystem.CompleteTournamentFeast(state);

        Assert.False(state.IsTournamentActive);
    }

    [Fact]
    public void GetTournamentReport_ReturnsNonEmptyString()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);

        string report = TournamentSystem.GetTournamentReport(state);

        Assert.False(string.IsNullOrWhiteSpace(report));
        Assert.Contains("ديوان البطولات", report);
    }

    [Fact]
    public void GetTournamentReport_IncludesChampionNameAfterJousting()
    {
        var state = MakeReadyState(gold: 1000);
        TournamentSystem.InitiateGrandTournament(state);
        TournamentSystem.SimulateJoustingMatches(state);

        string report = TournamentSystem.GetTournamentReport(state);

        var champion = state.RealmCharacters.First(c => c.Id == state.TournamentChampionId);
        Assert.Contains(champion.Name, report);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_TriggersMultiStageProgression()
    {
        var state = MakeReadyState(gold: 1000);
        state.SuppressRandomMajorEvents = true;
        TournamentSystem.InitiateGrandTournament(state);
        Assert.True(state.IsTournamentActive);
        Assert.Equal(3, state.TournamentDaysRemaining);
        Assert.Equal(1, state.TournamentStage);

        CalendarTimeSystem.AdvanceDay(state);
        Assert.Equal(2, state.TournamentStage);
        Assert.Equal(2, state.TournamentDaysRemaining);

        CalendarTimeSystem.AdvanceDay(state);
        Assert.Equal(2, state.TournamentStage);

        CalendarTimeSystem.AdvanceDay(state);
        Assert.False(state.IsTournamentActive);
        Assert.Equal(0, state.TournamentStage);
        Assert.Equal(0, state.TournamentDaysRemaining);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_AwardsPrestigeOnJoustingDay()
    {
        var state = MakeReadyState(gold: 1000);
        state.SuppressRandomMajorEvents = true;
        int prestigeBefore = state.Prestige;
        TournamentSystem.InitiateGrandTournament(state);

        CalendarTimeSystem.AdvanceDay(state);

        Assert.True(state.Prestige >= prestigeBefore + TournamentSystem.JoustingPrestigeReward);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_AwardsRenownOnFinalDay()
    {
        var state = MakeReadyState(gold: 1000);
        state.SuppressRandomMajorEvents = true;
        int renownBefore = state.DynastyRenown;
        TournamentSystem.InitiateGrandTournament(state);

        for (int i = 0; i < 3; i++)
        {
            CalendarTimeSystem.AdvanceDay(state);
        }

        Assert.Equal(renownBefore + TournamentSystem.FeastRenownReward, state.DynastyRenown);
    }

    [Fact]
    public void ReconcileOldSaves_InitializesTournamentFieldsSafely()
    {
        var state = new GameState
        {
            TournamentStage = -50,
            TournamentDaysRemaining = -10,
            TournamentParticipants = null,
            TournamentAccidentLog = null,
            TournamentChampionId = null
        };

        state.ReconcileOldSaves();

        Assert.NotNull(state.TournamentParticipants);
        Assert.NotNull(state.TournamentAccidentLog);
        Assert.Equal(0, state.TournamentStage);
        Assert.Equal(0, state.TournamentDaysRemaining);
        Assert.Equal("", state.TournamentChampionId);
        Assert.False(state.IsTournamentActive);
    }
}
