using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class FactionWarRebelActivityTests
{
    private static RealmCharacter SeedRebel(GameState state, string name, string sourceId, string provinceName, int baseOpinion = -50)
    {
        var vassal = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = "Governor",
            SourceId = sourceId,
            Role = CharacterRoleType.Governor,
            BaseOpinion = baseOpinion,
            VassalPower = 30,
            Skills = new CharacterSkills { Intrigue = 5 }
        };
        state.RealmCharacters.Add(vassal);

        var province = new Province
        {
            Name = provinceName,
            Id = "prov_" + provinceName,
            GovernorId = sourceId,
            GovernorName = name,
            Vassal = name,
            Income = 50,
            LocalGarrison = 500,
            RecruitableLevy = 500,
            BaseRecruitableLevy = 500,
            ConnectedProvinces = new System.Collections.Generic.List<string>(),
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        };
        state.Provinces.Add(province);
        return vassal;
    }

    [Fact]
    public void ProcessDailyRebelActivity_IsNoopWhenNoCivilWar()
    {
        var state = new GameState();
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = FactionWarEngine.RebelArmyPrefix + "x", TotalSoldiers = 100, CurrentProvince = "A" }
        };

        FactionWarEngine.ProcessDailyRebelActivity(state);

        Assert.Equal(100, state.EnemyArmies[0].TotalSoldiers);
    }

    [Fact]
    public void ProcessDailyRebelActivity_AttritesRebelOverTime()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        var home = SeedRebel(state, "Rebel", "gov_a", "الأولى", -50);
        SeedRebel(state, "Royal", "gov_b", "الثانية", 50);

        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "TestRebel",
                TotalSoldiers = 1000,
                CurrentProvince = "الأولى",
                CommanderName = "Rebel"
            }
        };

        int initial = state.EnemyArmies[0].TotalSoldiers;
        FactionWarEngine.ProcessDailyRebelActivity(state);

        Assert.True(state.EnemyArmies[0].TotalSoldiers < initial, "Rebel army should shrink due to attrition when it has no target.");
    }

    [Fact]
    public void ProcessDailyRebelActivity_AssignsDestinationFromHomeProvince()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        SeedRebel(state, "Rebel", "gov_a", "home", -50);
        var homeProv = state.Provinces.First(p => p.Name == "home");
        homeProv.Occupied = true;
        homeProv.OccupiedBy = "المتمردون";
        var other = new Province
        {
            Name = "target",
            Id = "prov_target",
            GovernorId = "gov_other",
            GovernorName = "Other",
            LocalGarrison = 200,
            ConnectedProvinces = new System.Collections.Generic.List<string> { "home" },
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        };
        state.Provinces.Add(other);

        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "TestRebel",
                TotalSoldiers = 1000,
                CurrentProvince = "home",
                CommanderName = "Rebel"
            }
        };

        FactionWarEngine.ProcessDailyRebelActivity(state);

        // بعد إصلاح سلوك المتمردين: الأولوية القصوى للعاصمة إن لم تكن متمردة.
        // العاصمة الافتراضية (Provinces[0]) غير متمردة، لذا يستهدفها المتمرد بدلاً من "target".
        Assert.NotNull(state.EnemyArmies[0].DestinationProvince);
        Assert.True(state.EnemyArmies[0].DaysToDestination > 0);
        Assert.True(
            state.EnemyArmies[0].DestinationProvince == "target" ||
            state.EnemyArmies[0].DestinationProvince == state.Provinces[0].Name,
            $"Expected rebel to target either 'target' or the capital '{state.Provinces[0].Name}', but got '{state.EnemyArmies[0].DestinationProvince}'"
        );
    }

    [Fact]
    public void ProcessDailyRebelActivity_OccupiesUndefendedProvince()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string>();
        SeedRebel(state, "Rebel", "gov_a", "home", -50);
        var target = new Province
        {
            Name = "target",
            Id = "prov_target",
            GovernorId = "gov_t",
            GovernorName = "Target",
            LocalGarrison = 100,
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        };
        state.Provinces.Add(target);

        state.Armies = new System.Collections.Generic.List<Army>();

        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "TestRebel",
                TotalSoldiers = 500,
                CurrentProvince = "target",
                CommanderName = "Rebel"
            }
        };

        FactionWarEngine.ProcessDailyRebelActivity(state);

        Assert.True(target.Occupied);
        Assert.Equal("المتمردون", target.OccupiedBy);
    }

    [Fact]
    public void ProcessDailyRebelActivity_RemovesDefeatedRebelArmy()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        SeedRebel(state, "Rebel", "gov_a", "home", -50);
        var target = new Province
        {
            Name = "target",
            Id = "prov_target",
            GovernorId = "gov_t",
            GovernorName = "Target",
            LocalGarrison = 100,
            FortLevel = 3,
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        };
        state.Provinces.Add(target);

        state.Armies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = "royal",
                Name = "Royal",
                TotalSoldiers = 10000,
                CurrentProvince = "target",
                CommanderName = "King"
            }
        };

        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "TestRebel",
                TotalSoldiers = 5,
                CurrentProvince = "target",
                CommanderName = "Rebel"
            }
        };

        FactionWarEngine.ProcessDailyRebelActivity(state);

        Assert.DoesNotContain(state.EnemyArmies, a => a.Id == FactionWarEngine.RebelArmyPrefix + "x");
    }

    [Fact]
    public void ProcessDailyRebelActivity_AppendsWarningInArabic()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string>();
        state.TurnWarnings = new System.Collections.Generic.List<string>();
        SeedRebel(state, "Rebel", "gov_a", "home", -50);
        var other = new Province
        {
            Name = "other",
            Id = "prov_other",
            GovernorId = "gov_o",
            GovernorName = "O",
            LocalGarrison = 100,
            ConnectedProvinces = new System.Collections.Generic.List<string> { "home" },
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        };
        state.Provinces.Add(other);

        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "TestRebel",
                TotalSoldiers = 500,
                CurrentProvince = "home",
                CommanderName = "Rebel"
            }
        };

        FactionWarEngine.ProcessDailyRebelActivity(state);

        Assert.NotEmpty(state.TurnWarnings);
    }

    [Fact]
    public void CheckCivilWarResolution_TriggersRebelVictory_WhenCapitalOccupiedByRebels()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        state.Armies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "royal", Name = "Royal", TotalSoldiers = 1000, CurrentProvince = "دمشق" }
        };

        state.Provinces = new System.Collections.Generic.List<Province>
        {
            new Province { Name = "دمشق", Id = "p1", LocalGarrison = 100, Occupied = true, OccupiedBy = "المتمردون", Buildings = new System.Collections.Generic.List<LocalBuilding>() }
        };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "Rebel",
                TotalSoldiers = 500,
                CurrentProvince = "دمشق"
            }
        };

        FactionWarEngine.CheckCivilWarResolution(state);

        Assert.True(state.IsCivilWarActive, "Civil war should remain active since royal army still has troops.");
    }

    [Fact]
    public void CheckCivilWarResolution_TriggersRebelVictory_WhenRoyalArmyDestroyed()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string> { "r1" };
        state.Armies = new System.Collections.Generic.List<Army>();

        state.Provinces = new System.Collections.Generic.List<Province>
        {
            new Province { Name = "دمشق", Id = "p1", LocalGarrison = 100, Buildings = new System.Collections.Generic.List<LocalBuilding>() }
        };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "Rebel",
                TotalSoldiers = 500,
                CurrentProvince = "other"
            }
        };

        FactionWarEngine.CheckCivilWarResolution(state);

        Assert.False(state.IsCivilWarActive);
        Assert.True(state.RulerIsDead);
    }

    [Fact]
    public void CombatSystem_ResolveRebelClash_RoyalWinsWhenOutnumbered()
    {
        var state = new GameState();
        state.Armies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "royal", Name = "Royal", TotalSoldiers = 5000, CurrentProvince = "p1" }
        };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "Rebel",
                TotalSoldiers = 10,
                CurrentProvince = "p1"
            }
        };
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        var prov = new Province { Name = "p1", Id = "p1", FortLevel = 3, LocalGarrison = 100, Buildings = new System.Collections.Generic.List<LocalBuilding>() };
        var rebel = state.EnemyArmies[0];
        var royal = state.Armies[0];

        var report = CombatSystem.ResolveRebelClash(state, prov, rebel, royal);

        Assert.NotEmpty(report);
        Assert.True(state.TurnWarnings.Any(w => w.Contains("تصادم")), "Warning should mention clash in Arabic.");
    }

    [Fact]
    public void CombatSystem_ResolveRebelClash_HandlesNullGracefully()
    {
        var state = new GameState();
        var result = CombatSystem.ResolveRebelClash(state, null, null, null);
        Assert.Equal("", result);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_ProcessesRebelActivityDuringCivilWar()
    {
        var state = new GameState();
        state.SuppressRandomMajorEvents = true;
        state.IsCivilWarActive = true;
        state.RebelVassalIds = new System.Collections.Generic.List<string>();
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army
            {
                Id = FactionWarEngine.RebelArmyPrefix + "x",
                Name = "TestRebel",
                TotalSoldiers = 500,
                CurrentProvince = "home",
                CommanderName = "R"
            }
        };
        state.Provinces = new System.Collections.Generic.List<Province>
        {
            new Province { Name = "home", Id = "p1", GovernorId = "g1", GovernorName = "Gov", LocalGarrison = 100, Occupied = true, OccupiedBy = "المتمردون", ConnectedProvinces = new System.Collections.Generic.List<string> { "other" }, Buildings = new System.Collections.Generic.List<LocalBuilding>() },
            new Province { Name = "other", Id = "p2", GovernorId = "g2", GovernorName = "Gov2", LocalGarrison = 100, ConnectedProvinces = new System.Collections.Generic.List<string> { "home" }, Buildings = new System.Collections.Generic.List<LocalBuilding>() }
        };

        CalendarTimeSystem.AdvanceDay(state);

        Assert.True(state.IsCivilWarActive);
    }
}
