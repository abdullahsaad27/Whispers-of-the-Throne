using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class DynasticGeneticsTests
{
    private static GameState CreateBaseState()
    {
        var state = new GameState();
        state.RealmCharacters ??= new List<RealmCharacter>();
        state.Time.Month = 6;
        state.Time.Day = 15;
        return state;
    }

    private static Spouse AddMother(GameState state, bool isGenius = false)
    {
        var mother = new Spouse { Id = Guid.NewGuid().ToString(), Name = "الملكة", IsGenius = isGenius };
        state.Wives.Add(mother);
        mother.IsPregnant = true;
        mother.PregnancyDaysLeft = 1;
        return mother;
    }

    private static void ForceBirth(GameState state, Spouse mother)
    {
        mother.IsPregnant = true;
        mother.PregnancyDaysLeft = 1;
        state.Time.Month = 6;
        state.Time.Day = 15;
        DynastySystem.ProcessDailyDynasty(state);
    }

    [Fact]
    public void Newborn_CreatesRealmCharacterChild_WithDynastyFields()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);

        var dynChild = state.RealmCharacters.FirstOrDefault(c => c != null && c.SourceType == "Child" && c.Role == CharacterRoleType.Child);
        Assert.NotNull(dynChild);
        Assert.Equal(0, dynChild!.CharacterAge);
        Assert.Equal(mother.Id, dynChild.MotherId);
        Assert.False(dynChild.IsAdult);
    }

    [Fact]
    public void Newborn_MotherGenius_HasInheritanceChance_AndInheritanceIsDeterministic()
    {
        int inherited = 0;
        int trials = 200;
        for (int i = 0; i < trials; i++)
        {
            var state = CreateBaseState();
            var mother = AddMother(state, isGenius: true);
            ForceBirth(state, mother);
            var dynChild = state.RealmCharacters.First(c => c.SourceType == "Child");
            if (dynChild.IsGenius) inherited++;
        }
        Assert.InRange(inherited, 1, trials - 1);
    }

    [Fact]
    public void Newborn_NoGeniusParent_DoesNotInheritGenius()
    {
        var state = CreateBaseState();
        var mother = AddMother(state, isGenius: false);
        ForceBirth(state, mother);
        var dynChild = state.RealmCharacters.First(c => c.SourceType == "Child");
        Assert.False(dynChild.IsGenius);
    }

    [Fact]
    public void Newborn_BothParentsGenius_HasHigherInheritanceRate()
    {
        int inheritedSingle = 0;
        int inheritedBoth = 0;
        const int trials = 400;
        for (int i = 0; i < trials; i++)
        {
            var s1 = CreateBaseState();
            var m1 = AddMother(s1, isGenius: true);
            ForceBirth(s1, m1);
            if (s1.RealmCharacters.First(c => c.SourceType == "Child").IsGenius) inheritedSingle++;

            var s2 = CreateBaseState();
            var m2 = AddMother(s2, isGenius: true);
            var ruler = s2.RealmCharacters.FirstOrDefault(c => c.Role == CharacterRoleType.Ruler);
            if (ruler != null) ruler.IsGenius = true;
            ForceBirth(s2, m2);
            if (s2.RealmCharacters.First(c => c.SourceType == "Child").IsGenius) inheritedBoth++;
        }
        Assert.True(inheritedBoth > inheritedSingle, $"Both-genius ({inheritedBoth}) should exceed single-genius ({inheritedSingle}).");
    }

    [Fact]
    public void SetChildEducationFocus_AssignsFocus()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");

        DynastySystem.SetChildEducationFocus(state, child.Id, "Martial");
        Assert.Equal("Martial", child.CurrentEducationFocus);

        DynastySystem.SetChildEducationFocus(state, child.Id, "Stewardship");
        Assert.Equal("Stewardship", child.CurrentEducationFocus);
    }

    [Fact]
    public void SetChildEducationFocus_DoesNotAffectAdults()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");
        child.IsAdult = true;
        child.CurrentEducationFocus = "";

        DynastySystem.SetChildEducationFocus(state, child.Id, "Martial");
        Assert.Equal("", child.CurrentEducationFocus);
    }

    [Fact]
    public void HandleCharacterAging_AtAge6_TriggersWarning()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");
        child.CharacterAge = 5;
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        DynastySystem.HandleCharacterAging(state);

        Assert.Equal(6, child.CharacterAge);
        Assert.True(state.TurnWarnings.Any(w => w.Contains("[تعليم]") && w.Contains(child.Name)));
    }

    [Fact]
    public void HandleCharacterAging_AtAge16_SetsAdultAndAssignsTrait()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");
        child.CharacterAge = 15;
        child.Skills = new CharacterSkills();
        child.StewardshipSkill = 18;
        child.MartialSkill = 10;
        child.IntrigueSkill = 10;
        child.Traits = new System.Collections.Generic.List<string>();
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        DynastySystem.HandleCharacterAging(state);

        Assert.True(child.IsAdult);
        Assert.Contains("MidasTouched", child.Traits);
        Assert.True(state.TurnWarnings.Any(w => w.Contains("[بلوغ]")));
    }

    [Fact]
    public void HandleCharacterAging_AssignsBrilliantStrategistForHighMartial()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");
        child.CharacterAge = 15;
        child.Skills = new CharacterSkills();
        child.MartialSkill = 20;
        child.StewardshipSkill = 5;
        child.IntrigueSkill = 5;
        child.Traits = new System.Collections.Generic.List<string>();

        DynastySystem.HandleCharacterAging(state);

        Assert.True(child.IsAdult);
        Assert.Contains("BrilliantStrategist", child.Traits);
    }

    [Fact]
    public void ProcessMonthlyChildEducation_IncreasesFocusedSkill()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");
        child.CharacterAge = 10;
        child.Skills = new CharacterSkills();
        child.CurrentEducationFocus = "Martial";
        int initialMartial = child.MartialSkill;

        for (int i = 0; i < 120; i++)
        {
            DynastySystem.ProcessMonthlyChildEducation(state);
            if (child.MartialSkill > initialMartial) break;
        }
        Assert.True(child.MartialSkill > initialMartial);
    }

    [Fact]
    public void GetUnderageDynastyChildren_FiltersByAgeRange()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var c1 = state.RealmCharacters.First(c => c.SourceType == "Child");
        c1.CharacterAge = 5;

        var secondMother = AddMother(state);
        ForceBirth(state, secondMother);
        var c2 = state.RealmCharacters.Where(c => c.SourceType == "Child").Skip(1).First();
        c2.CharacterAge = 10;

        state.RealmCharacters.Add(new RealmCharacter
        {
            Id = "third",
            Name = "ثالث",
            SourceType = "Child",
            Role = CharacterRoleType.Child,
            CharacterAge = 18,
            IsAdult = true
        });

        var underage = DynastySystem.GetUnderageDynastyChildren(state);
        Assert.Single(underage);
        Assert.Equal(10, underage[0].CharacterAge);
    }

    [Fact]
    public void GameState_Serialization_PreservesChildFields()
    {
        var state = CreateBaseState();
        var mother = AddMother(state);
        ForceBirth(state, mother);
        var child = state.RealmCharacters.First(c => c.SourceType == "Child");
        child.MotherId = mother.Id;
        child.FatherId = "Ruler";
        child.CharacterAge = 7;
        child.IsAdult = false;
        child.IsGenius = true;
        child.CurrentEducationFocus = "Stewardship";

        string json = System.Text.Json.JsonSerializer.Serialize(state);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);

        Assert.NotNull(deserialized);
        var deserializedChild = deserialized!.RealmCharacters.FirstOrDefault(c => c != null && c.Id == child.Id);
        Assert.NotNull(deserializedChild);
        Assert.Equal(mother.Id, deserializedChild!.MotherId);
        Assert.Equal("Ruler", deserializedChild.FatherId);
        Assert.Equal(7, deserializedChild.CharacterAge);
        Assert.False(deserializedChild.IsAdult);
        Assert.True(deserializedChild.IsGenius);
        Assert.Equal("Stewardship", deserializedChild.CurrentEducationFocus);
    }
}
