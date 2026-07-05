using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class InfrastructureSystemTests
{
    [Fact]
    public void StartBuildingUpgrade_FailsWithoutEnoughGold()
    {
        var state = new GameState();
        state.Gold = 100;
        var province = state.Provinces.First();

        var result = InfrastructureSystem.StartBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);

        Assert.False(result.Success);
        Assert.Equal(100, state.Gold);
    }

    [Fact]
    public void StartBuildingUpgrade_DeductsGoldAndCreatesTask()
    {
        var state = new GameState();
        state.Gold = 1000;
        var province = state.Provinces.First();

        var result = InfrastructureSystem.StartBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);

        Assert.True(result.Success);
        Assert.Equal(850, state.Gold);
        Assert.Contains(state.DelegatedTasks, t => t.TaskType == "BuildUpgrade" && t.TargetId == province.Name && t.Tag == InfrastructureSystem.MarketBuildingType && t.DaysRemaining == 45);
    }

    [Fact]
    public void StartBuildingUpgrade_CostScalesWithLevel()
    {
        var state = new GameState();
        state.Gold = 1000;
        var province = state.Provinces.First();
        province.MarketLevel = 2;

        var result = InfrastructureSystem.StartBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);

        Assert.True(result.Success);
        Assert.Equal(550, state.Gold);
    }

    [Fact]
    public void StartBuildingUpgrade_BlocksDuplicateActiveUpgrade()
    {
        var state = new GameState();
        state.Gold = 2000;
        var province = state.Provinces.First();

        InfrastructureSystem.StartBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);
        var result = InfrastructureSystem.StartBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);

        Assert.False(result.Success);
        Assert.Equal(1850, state.Gold);
    }

    [Fact]
    public void CompleteBuildingUpgrade_IncrementsMarketLevel()
    {
        var state = new GameState();
        var province = state.Provinces.First();

        InfrastructureSystem.CompleteBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);

        Assert.Equal(1, province.MarketLevel);
        Assert.Contains(state.TurnWarnings, w => w.Contains(province.Name) && w.Contains("سوق"));
    }

    [Fact]
    public void CompleteBuildingUpgrade_IncrementsBarracksLevelAndCaps()
    {
        var state = new GameState();
        var province = state.Provinces.First();
        int initialGarrison = province.LocalGarrison;
        int initialLevy = province.RecruitableLevy;

        InfrastructureSystem.CompleteBuildingUpgrade(state, province.Name, InfrastructureSystem.BarracksBuildingType);

        Assert.Equal(1, province.BarracksLevel);
        Assert.Equal(initialGarrison + InfrastructureSystem.BarracksRecruitmentBonusPerLevel, province.LocalGarrison);
        Assert.Equal(initialLevy + InfrastructureSystem.BarracksRecruitmentBonusPerLevel, province.RecruitableLevy);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_CompletesBuildUpgrade()
    {
        var state = new GameState();
        state.Gold = 1000;
        var province = state.Provinces.First();
        InfrastructureSystem.StartBuildingUpgrade(state, province.Name, InfrastructureSystem.MarketBuildingType);
        var task = state.DelegatedTasks.First(t => t.TaskType == "BuildUpgrade");
        task.DaysRemaining = 1;

        CalendarTimeSystem.AdvanceDay(state);

        Assert.DoesNotContain(state.DelegatedTasks, t => t.TaskType == "BuildUpgrade");
        Assert.Equal(1, province.MarketLevel);
    }

    [Fact]
    public void EconomySystem_ProcessMonthlyEconomy_AddsMarketLevelBonus()
    {
        var state = new GameState();
        var province = state.Provinces.First();
        province.MarketLevel = 2;
        int expectedBonus = 2 * InfrastructureSystem.MarketGoldBonusPerLevel;

        state.Time.Day = 30;
        int goldBefore = state.Gold;
        EconomySystem.ProcessDailyEconomy(state);

        Assert.True(state.Gold > goldBefore + expectedBonus - 1000);
    }

    [Fact]
    public void GameState_Serialization_PreservesBuildingLevels()
    {
        var state = new GameState();
        var province = state.Provinces.First();
        province.MarketLevel = 3;
        province.BarracksLevel = 2;

        string json = System.Text.Json.JsonSerializer.Serialize(state);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);

        Assert.NotNull(deserialized);
        var deserializedProvince = deserialized.Provinces.First();
        Assert.Equal(3, deserializedProvince.MarketLevel);
        Assert.Equal(2, deserializedProvince.BarracksLevel);
    }
}
