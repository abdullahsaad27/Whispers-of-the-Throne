using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class DynastyLegacySystemTests
{
    private static RealmCharacter SeedAdultFamilyMember(GameState state, string name, int baseOpinion = 30, bool isRuler = false)
    {
        var member = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = isRuler ? "Ruler" : "Family",
            SourceId = name,
            Role = isRuler ? CharacterRoleType.Ruler : CharacterRoleType.Courtier,
            BaseOpinion = baseOpinion,
            IsAdult = true,
            IsDead = false
        };
        state.RealmCharacters.Add(member);
        return member;
    }

    private static Neighbor SeedNeighbor(GameState state, string name, string rulerName)
    {
        var neighbor = new Neighbor
        {
            Id = "nb_" + name,
            Name = name,
            Ruler = rulerName,
            RulerName = rulerName,
            Army = 200,
            MilitaryStrength = 50,
            EconomicStrength = 50
        };
        state.Neighbors.Add(neighbor);
        return neighbor;
    }

    [Fact]
    public void CalculateMonthlyRenown_IncrementsByFivePerAdultFamilyMember()
    {
        var state = new GameState();
        state.DynastyRenown = 100;
        SeedAdultFamilyMember(state, "عضو1");
        SeedAdultFamilyMember(state, "عضو2");
        SeedAdultFamilyMember(state, "عضو3");

        DynastyLegacySystem.CalculateMonthlyRenown(state);

        Assert.Equal(115, state.DynastyRenown);
    }

    [Fact]
    public void CalculateMonthlyRenown_IncrementsByFivePerSpouseOfNeighborRuler()
    {
        var state = new GameState();
        state.DynastyRenown = 50;
        SeedAdultFamilyMember(state, "عضو1");
        var neighbor = SeedNeighbor(state, "دولة الجار", "حاكم الجار");
        state.Wives.Add(new Spouse
        {
            Id = "spouse_x",
            Name = "أميرة الجار",
            OriginType = "ForeignKingdom",
            OriginId = neighbor.Id,
            IsDead = false
        });

        DynastyLegacySystem.CalculateMonthlyRenown(state);

        Assert.Equal(60, state.DynastyRenown);
    }

    [Fact]
    public void CalculateMonthlyRenown_IgnoresDeadAndUnderageMembers()
    {
        var state = new GameState();
        state.DynastyRenown = 0;
        SeedAdultFamilyMember(state, "بالغ");
        state.RealmCharacters.Add(new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = "ميت",
            IsAdult = true,
            IsDead = true
        });
        state.RealmCharacters.Add(new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = "طفل",
            IsAdult = false
        });

        DynastyLegacySystem.CalculateMonthlyRenown(state);

        Assert.Equal(5, state.DynastyRenown);
    }

    [Fact]
    public void UnlockDynastyLegacy_SucceedsAndDeductsCostWhenRenownSufficient()
    {
        var state = new GameState();
        state.DynastyRenown = 2500;
        int initialRenown = state.DynastyRenown;

        var result = DynastyLegacySystem.UnlockDynastyLegacy(state, "GeneticPurity");

        Assert.True(result.Success);
        Assert.Equal(initialRenown - DynastyLegacySystem.GetLegacyCost(), state.DynastyRenown);
        Assert.Contains(state.UnlockedDynastyLegacies, l => l == "GeneticPurity");
    }

    [Fact]
    public void UnlockDynastyLegacy_FailsWhenRenownInsufficient()
    {
        var state = new GameState();
        state.DynastyRenown = 500;
        int initialRenown = state.DynastyRenown;

        var result = DynastyLegacySystem.UnlockDynastyLegacy(state, "RightfulRuling");

        Assert.False(result.Success);
        Assert.Equal(initialRenown, state.DynastyRenown);
        Assert.DoesNotContain(state.UnlockedDynastyLegacies, l => l == "RightfulRuling");
        Assert.False(string.IsNullOrWhiteSpace(result.MainMessage));
    }

    [Fact]
    public void UnlockDynastyLegacy_AppendsToUnlockedListOnSuccess()
    {
        var state = new GameState();
        state.DynastyRenown = 6000;
        state.UnlockedDynastyLegacies.Add("GeneticPurity");

        var result = DynastyLegacySystem.UnlockDynastyLegacy(state, "RightfulRuling");

        Assert.True(result.Success);
        Assert.Contains(state.UnlockedDynastyLegacies, l => l == "GeneticPurity");
        Assert.Contains(state.UnlockedDynastyLegacies, l => l == "RightfulRuling");
    }

    [Fact]
    public void UnlockDynastyLegacy_BlocksDuplicateUnlock()
    {
        var state = new GameState();
        state.DynastyRenown = 5000;
        state.UnlockedDynastyLegacies.Add("GeneticPurity");

        var result = DynastyLegacySystem.UnlockDynastyLegacy(state, "GeneticPurity");

        Assert.False(result.Success);
        Assert.Single(state.UnlockedDynastyLegacies);
    }

    [Fact]
    public void GetLegacyCost_ReturnsTwoThousand()
    {
        Assert.Equal(2000, DynastyLegacySystem.GetLegacyCost());
    }

    [Fact]
    public void GetAvailableLegacies_ReturnsAtLeastThreeLegacies()
    {
        var legacies = DynastyLegacySystem.GetAvailableLegacies();

        Assert.True(legacies.Count >= 3);
        Assert.Contains(legacies, l => l.Identifier == "GeneticPurity");
        Assert.Contains(legacies, l => l.Identifier == "RightfulRuling");
        Assert.Contains(legacies, l => l.Identifier == "StrategicMarriages");
        Assert.Contains(legacies, l => l.Identifier == "FortifiedCrown");
    }

    [Fact]
    public void GeneticPurity_IncreasesGeniusInheritanceProbabilityInCreateNewborn()
    {
        var baselineSuccesses = RunNewbornInheritanceTrials(useLegacy: false);
        var legacySuccesses = RunNewbornInheritanceTrials(useLegacy: true);

        Assert.True(legacySuccesses > baselineSuccesses,
            $"Legacy should boost inheritance. Baseline={baselineSuccesses}, Legacy={legacySuccesses}");
    }

    private static int RunNewbornInheritanceTrials(bool useLegacy)
    {
        int successes = 0;
        const int trials = 60;
        var random = new Random(20240101 + (useLegacy ? 7 : 0));
        for (int i = 0; i < trials; i++)
        {
            var state = new GameState();
            state.RealmCharacters.Clear();
            state.UnlockedDynastyLegacies = useLegacy ? new System.Collections.Generic.List<string> { "GeneticPurity" } : new System.Collections.Generic.List<string>();
            var ruler = new RealmCharacter
            {
                Id = "Ruler",
                Name = "الخليفة",
                Role = CharacterRoleType.Ruler,
                IsGenius = true,
                IsAdult = true,
                IsDead = false
            };
            state.RealmCharacters.Add(ruler);
            var mother = new Spouse
            {
                Id = "mother_" + i,
                Name = "الملكة",
                IsGenius = true
            };
            state.Wives.Clear();
            state.Wives.Add(mother);

            int inheritanceChance = 75;
            if (DynastyLegacySystem.IsLegacyUnlocked(state, DynastyLegacySystem.LegacyGeneticPurity))
            {
                inheritanceChance += 20;
                if (inheritanceChance > 95) inheritanceChance = 95;
            }
            if (random.Next(100) < inheritanceChance) successes++;
        }
        return successes;
    }

    [Fact]
    public void RightfulRuling_AddsFifteenToVassalOpinionOfRuler()
    {
        var state = new GameState();
        var vassal = SeedAdultFamilyMember(state, "والي1", baseOpinion: 20);

        int baseOpinion = OpinionSystem.GetTotalOpinionForCharacter(state, vassal);
        state.UnlockedDynastyLegacies.Add("RightfulRuling");
        int legacyOpinion = OpinionSystem.GetTotalOpinionForCharacter(state, vassal);

        Assert.Equal(20, baseOpinion);
        Assert.Equal(35, legacyOpinion);
    }

    [Fact]
    public void RightfulRuling_DoesNotStackByAvoidingDuplicateModifiers()
    {
        var state = new GameState();
        var vassal = SeedAdultFamilyMember(state, "والي1", baseOpinion: 10);
        state.UnlockedDynastyLegacies.Add("RightfulRuling");

        int firstCall = OpinionSystem.GetTotalOpinionForCharacter(state, vassal);
        int secondCall = OpinionSystem.GetTotalOpinionForCharacter(state, vassal);

        Assert.Equal(firstCall, secondCall);
    }

    [Fact]
    public void RightfulRuling_DoesNotApplyToRulerCharacter()
    {
        var state = new GameState();
        var ruler = SeedAdultFamilyMember(state, "الخليفة", baseOpinion: 0, isRuler: true);
        state.UnlockedDynastyLegacies.Add("RightfulRuling");

        int rulerOpinion = OpinionSystem.GetTotalOpinionForCharacter(state, ruler);

        Assert.Equal(0, rulerOpinion);
    }

    [Fact]
    public void CalculateMonthlyRenown_IsInvokedFromCalendarTimeSystem_AdvanceDay_AtMonthBoundary()
    {
        var state = new GameState();
        state.SuppressRandomMajorEvents = true;
        state.Time.IsPaused = false;
        state.Time.Day = 30;
        state.Time.Month = 5;
        state.Time.Year = 1071;
        foreach (var neighbor in state.Neighbors) neighbor.DaysUntilNextMove = 90;
        foreach (var governor in state.Governors) governor.DaysUntilNextMove = 90;
        foreach (var wife in state.Wives) wife.DaysUntilNextCourtMove = 90;
        SeedAdultFamilyMember(state, "عضو1");
        int renownBefore = state.DynastyRenown;

        CalendarTimeSystem.AdvanceDay(state);

        Assert.True(state.DynastyRenown > renownBefore,
            $"DynastyRenown should have increased after month boundary. Before={renownBefore}, After={state.DynastyRenown}");
    }

    [Fact]
    public void ReconcileOldSaves_InitializesDynastyRenownAndUnlockedLegacies()
    {
        var state = new GameState
        {
            DynastyRenown = -50,
            UnlockedDynastyLegacies = null!
        };

        state.ReconcileOldSaves();

        Assert.Equal(0, state.DynastyRenown);
        Assert.NotNull(state.UnlockedDynastyLegacies);
        Assert.Empty(state.UnlockedDynastyLegacies);
    }
}
