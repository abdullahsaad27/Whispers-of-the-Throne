using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class IntrigueSchemeSystemTests
{
    private static RealmCharacter SeedSpymaster(GameState state, int intrigueSkill)
    {
        var sm = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = "مدير المخابرات",
            SourceType = "Councilor",
            SourceId = "spymaster",
            Role = CharacterRoleType.Councilor,
            IntrigueSkill = intrigueSkill,
            Skills = new CharacterSkills { Intrigue = intrigueSkill }
        };
        state.RealmCharacters.Add(sm);
        state.SpymasterCharacterId = sm.Id;
        return sm;
    }

    private static RealmCharacter SeedTarget(GameState state, string name, int intrigueSkill, bool isDead = false)
    {
        var target = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = "Target",
            Role = CharacterRoleType.Courtier,
            IntrigueSkill = intrigueSkill,
            IsDead = isDead,
            Skills = new CharacterSkills { Intrigue = intrigueSkill }
        };
        state.RealmCharacters.Add(target);
        return target;
    }

    [Fact]
    public void LaunchScheme_FailsWhenAnotherSchemeActive()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 5);
        state.ActiveSchemeType = IntrigueSchemeSystem.SchemeTypeMurder;

        var result = IntrigueSchemeSystem.LaunchScheme(state, target.Id, IntrigueSchemeSystem.SchemeTypeFabricateSecret);

        Assert.False(result.Success);
    }

    [Fact]
    public void LaunchScheme_FailsOnInvalidType()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 5);

        var result = IntrigueSchemeSystem.LaunchScheme(state, target.Id, "Invalid");

        Assert.False(result.Success);
    }

    [Fact]
    public void LaunchScheme_FailsForMissingTarget()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);

        var result = IntrigueSchemeSystem.LaunchScheme(state, "no-such-id", IntrigueSchemeSystem.SchemeTypeMurder);

        Assert.False(result.Success);
    }

    [Fact]
    public void LaunchScheme_SetsActiveSchemeAndCreatesTask()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 5);

        var result = IntrigueSchemeSystem.LaunchScheme(state, target.Id, IntrigueSchemeSystem.SchemeTypeMurder);

        Assert.True(result.Success);
        Assert.Equal(IntrigueSchemeSystem.SchemeTypeMurder, state.ActiveSchemeType);
        Assert.Equal(target.Id, state.ActiveSchemeTargetId);
        Assert.Contains(state.DelegatedTasks, t => t.TaskType == IntrigueSchemeSystem.DelegatedTaskType && t.TargetId == target.Id && t.DaysRemaining == 90);
    }

    [Fact]
    public void CalculateSuccessChance_SpymasterAndTargetSkillsFormula()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 5);

        int chance = IntrigueSchemeSystem.CalculateSuccessChance(state, target);

        Assert.Equal(IntrigueSchemeSystem.BaseSuccessPercent + (10 * 3) - (5 * 2), chance);
    }

    [Fact]
    public void CalculateSuccessChance_BodyguardPenaltyApplies()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 0);
        state.BodyguardId = "guard-1";

        int chance = IntrigueSchemeSystem.CalculateSuccessChance(state, target);

        Assert.Equal(IntrigueSchemeSystem.BaseSuccessPercent + (10 * 3) - IntrigueSchemeSystem.GuardProtectionPenaltyPercent, chance);
    }

    [Fact]
    public void CalculateSuccessChance_FoodTasterPenaltyApplies()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 0);
        state.FoodTasterId = "taster-1";

        int chance = IntrigueSchemeSystem.CalculateSuccessChance(state, target);

        Assert.Equal(IntrigueSchemeSystem.BaseSuccessPercent + (10 * 3) - IntrigueSchemeSystem.GuardProtectionPenaltyPercent, chance);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_CompletesActiveIntrigueScheme()
    {
        var state = new GameState();
        SeedSpymaster(state, 10);
        var target = SeedTarget(state, "والي1", 0);
        state.Time.Month = 6;
        state.Time.Day = 15;
        IntrigueSchemeSystem.LaunchScheme(state, target.Id, IntrigueSchemeSystem.SchemeTypeFabricateSecret);
        var task = state.DelegatedTasks.First(t => t.TaskType == IntrigueSchemeSystem.DelegatedTaskType);
        task.DaysRemaining = 1;
        state.PoliticalHooks = new System.Collections.Generic.List<PoliticalHook>();
        state.CharacterSecrets = new System.Collections.Generic.List<CharacterSecret>();

        int hits = 0;
        for (int i = 0; i < 60; i++)
        {
            IntrigueSchemeSystem.LaunchScheme(state, target.Id, IntrigueSchemeSystem.SchemeTypeFabricateSecret);
            task = state.DelegatedTasks.First(t => t.TaskType == IntrigueSchemeSystem.DelegatedTaskType);
            task.DaysRemaining = 1;
            int hooksBefore = state.PoliticalHooks.Count;
            CalendarTimeSystem.AdvanceDay(state);
            if (state.PoliticalHooks.Count > hooksBefore) hits++;
            if (hits >= 1) break;
        }

        Assert.True(hits >= 1);
    }

    [Fact]
    public void ResolveSchemeOutcome_FailureAppliesTyrannyAndGrudge()
    {
        var state = new GameState();
        SeedSpymaster(state, 0);
        var target = SeedTarget(state, "والي1", 25);
        state.TurnWarnings = new System.Collections.Generic.List<string>();
        state.Grudges = new System.Collections.Generic.List<Grudge>();
        state.PoliticalHooks = new System.Collections.Generic.List<PoliticalHook>();
        state.CharacterSecrets = new System.Collections.Generic.List<CharacterSecret>();
        int tyrannyBefore = state.RulerTyranny;

        int failures = 0;
        for (int i = 0; i < 60; i++)
        {
            tyrannyBefore = state.RulerTyranny;
            state.Grudges.Clear();
            IntrigueSchemeSystem.ResolveSchemeOutcome(state, target.Id, IntrigueSchemeSystem.SchemeTypeFabricateSecret);
            if (state.RulerTyranny == tyrannyBefore + IntrigueSchemeSystem.FailureTyrannyInjection && state.Grudges.Count > 0) failures++;
            if (failures >= 1) break;
        }

        Assert.True(failures >= 1);
    }

    [Fact]
    public void ResolveSchemeOutcome_MurderSuccessMarksTargetDead()
    {
        var state = new GameState();
        SeedSpymaster(state, 20);
        var target = SeedTarget(state, "والي1", 0);
        state.PoliticalHooks = new System.Collections.Generic.List<PoliticalHook>();
        state.CharacterSecrets = new System.Collections.Generic.List<CharacterSecret>();
        state.Council = new System.Collections.Generic.Dictionary<string, CouncilMember>
        {
            { "marshal", new CouncilMember { Name = target.Name, Task = "قيادة الجيش" } }
        };

        int successCount = 0;
        for (int i = 0; i < 60; i++)
        {
            target.IsDead = false;
            state.Council["marshal"].Task = "قيادة الجيش";
            IntrigueSchemeSystem.ResolveSchemeOutcome(state, target.Id, IntrigueSchemeSystem.SchemeTypeMurder);
            if (target.IsDead) { successCount++; break; }
        }

        Assert.True(successCount >= 1);
    }

    [Fact]
    public void GetPotentialTargets_ExcludesDeadAndSpymaster()
    {
        var state = new GameState();
        var sm = SeedSpymaster(state, 10);
        var alive = SeedTarget(state, "حي", 5);
        var dead = SeedTarget(state, "ميت_فريد", 5, isDead: true);

        var targets = IntrigueSchemeSystem.GetPotentialTargets(state);

        Assert.DoesNotContain(targets, c => c.Id == sm.Id);
        Assert.DoesNotContain(targets, c => c.Id == dead.Id);
        Assert.Contains(targets, c => c.Id == alive.Id);
    }

    [Fact]
    public void HasActiveScheme_ReturnsTrueWhenSchemeIsRunning()
    {
        var state = new GameState();
        Assert.False(IntrigueSchemeSystem.HasActiveScheme(state));
        state.ActiveSchemeType = IntrigueSchemeSystem.SchemeTypeMurder;
        Assert.True(IntrigueSchemeSystem.HasActiveScheme(state));
    }

    [Fact]
    public void GameState_Serialization_PreservesActiveSchemeFields()
    {
        var state = new GameState();
        state.ActiveSchemeType = IntrigueSchemeSystem.SchemeTypeFabricateSecret;
        state.ActiveSchemeTargetId = "target-1";

        string json = System.Text.Json.JsonSerializer.Serialize(state);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(IntrigueSchemeSystem.SchemeTypeFabricateSecret, deserialized!.ActiveSchemeType);
        Assert.Equal("target-1", deserialized.ActiveSchemeTargetId);
    }
}
