using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class ActivityManagerTests
{
    [Fact]
    public void HostGrandFeast_FailsWithoutEnoughGold()
    {
        var state = new GameState();
        state.Gold = 100;

        var result = ActivityManagerSystem.HostGrandFeast(state);

        Assert.False(result.Success);
        Assert.Equal(100, state.Gold);
    }

    [Fact]
    public void HostGrandFeast_SubtractsGoldAndStressAndAppliesOpinion()
    {
        var state = new GameState();
        state.Gold = 1000;
        state.RulerStress = 80;
        var vassal = new RealmCharacter { Id = "v1", Name = "أمير البلاط", Role = CharacterRoleType.Governor, BaseOpinion = 10 };
        state.RealmCharacters.Add(vassal);

        var result = ActivityManagerSystem.HostGrandFeast(state);

        Assert.True(result.Success);
        Assert.Equal(750, state.Gold);
        Assert.Equal(50, state.RulerStress);
        Assert.Contains(vassal.OpinionModifiers, m => m.Key == ActivityManagerSystem.GrandFeastOpinionKey && m.Value == ActivityManagerSystem.GrandFeastOpinionValue);
    }

    [Fact]
    public void HostGrandFeast_DoesNotAffectRulerCharacter()
    {
        var state = new GameState();
        state.Gold = 1000;
        var ruler = new RealmCharacter { Id = "ruler", Name = "الخليفة", Role = CharacterRoleType.Ruler, BaseOpinion = 0 };
        state.RealmCharacters.Add(ruler);

        ActivityManagerSystem.HostGrandFeast(state);

        Assert.DoesNotContain(ruler.OpinionModifiers, m => m.Key == ActivityManagerSystem.GrandFeastOpinionKey);
    }

    [Fact]
    public void StartHolyPilgrimage_FailsWithoutEnoughGold()
    {
        var state = new GameState();
        state.Gold = 100;

        var result = ActivityManagerSystem.StartHolyPilgrimage(state);

        Assert.False(result.Success);
        Assert.Equal(100, state.Gold);
        Assert.DoesNotContain(state.DelegatedTasks, t => t.TaskType == "HolyPilgrimage");
    }

    [Fact]
    public void StartHolyPilgrimage_SubtractsGoldAndCreatesTask()
    {
        var state = new GameState();
        state.Gold = 1000;

        var result = ActivityManagerSystem.StartHolyPilgrimage(state);

        Assert.True(result.Success);
        Assert.Equal(650, state.Gold);
        Assert.Contains(state.DelegatedTasks, t => t.TaskType == "HolyPilgrimage" && t.DaysRemaining == ActivityManagerSystem.HolyPilgrimageDurationDays);
    }

    [Fact]
    public void StartHolyPilgrimage_BlocksDuplicateActivePilgrimage()
    {
        var state = new GameState();
        state.Gold = 1000;
        state.DelegatedTasks.Add(new DelegatedTask { TaskType = "HolyPilgrimage", DaysRemaining = 30 });

        var result = ActivityManagerSystem.StartHolyPilgrimage(state);

        Assert.False(result.Success);
        Assert.Equal(1000, state.Gold);
    }

    [Fact]
    public void CompleteHolyPilgrimage_AddsPietyLegitimacyAndTrait()
    {
        var state = new GameState();
        state.Piety = 100;
        state.ReligiousLegitimacy = 30;

        ActivityManagerSystem.CompleteHolyPilgrimage(state);

        Assert.Equal(250, state.Piety);
        Assert.Equal(55, state.ReligiousLegitimacy);
        Assert.Contains(ActivityManagerSystem.HajjiTrait, state.EarnedPilgrimTraits);
        Assert.Contains(state.TurnWarnings, w => w.Contains(ActivityManagerSystem.HajjiTrait));
    }

    [Fact]
    public void CompleteHolyPilgrimage_DoesNotDuplicateHajjiTrait()
    {
        var state = new GameState();
        state.EarnedPilgrimTraits.Add(ActivityManagerSystem.HajjiTrait);

        ActivityManagerSystem.CompleteHolyPilgrimage(state);

        Assert.Single(state.EarnedPilgrimTraits);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_CompletesHolyPilgrimageAtZeroDays()
    {
        var state = new GameState();
        state.Gold = 1000;
        ActivityManagerSystem.StartHolyPilgrimage(state);
        var task = state.DelegatedTasks.First(t => t.TaskType == "HolyPilgrimage");
        task.DaysRemaining = 1;
        state.Piety = 0;
        state.ReligiousLegitimacy = 0;

        CalendarTimeSystem.AdvanceDay(state);

        Assert.DoesNotContain(state.DelegatedTasks, t => t.TaskType == "HolyPilgrimage");
        Assert.Equal(ActivityManagerSystem.HolyPilgrimagePietyReward, state.Piety);
        Assert.Equal(ActivityManagerSystem.HolyPilgrimageLegitimacyReward, state.ReligiousLegitimacy);
        Assert.Contains(ActivityManagerSystem.HajjiTrait, state.EarnedPilgrimTraits);
    }

    [Fact]
    public void GameState_Serialization_PreservesEarnedPilgrimTraits()
    {
        var state = new GameState();
        state.EarnedPilgrimTraits.Add(ActivityManagerSystem.HajjiTrait);
        state.Gold = 500;

        string json = System.Text.Json.JsonSerializer.Serialize(state);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);

        Assert.NotNull(deserialized);
        Assert.Contains(ActivityManagerSystem.HajjiTrait, deserialized.EarnedPilgrimTraits);
        Assert.Equal(500, deserialized.Gold);
    }
}
