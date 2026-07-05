using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class FactionWarEngineTests
{
    private static RealmCharacter SeedVassal(GameState state, string name, string sourceId, int baseOpinion = -50)
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
        return vassal;
    }

    private static Province SeedProvince(GameState state, string name, string governorId, string governorName)
    {
        var province = new Province
        {
            Name = name,
            Id = name + "_id",
            GovernorId = governorId,
            GovernorName = governorName,
            Vassal = governorName,
            Income = 50,
            LocalGarrison = 500,
            RecruitableLevy = 500,
            BaseRecruitableLevy = 500,
            Buildings = new System.Collections.Generic.List<LocalBuilding>()
        };
        state.Provinces.Add(province);
        return province;
    }

    [Fact]
    public void TriggerFactionUltimatum_HaltsTimeAndAddsWarning()
    {
        var state = new GameState();
        state.Time.IsPaused = false;
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        FactionWarEngine.TriggerFactionUltimatum(state);

        Assert.True(state.Time.IsPaused);
        Assert.Contains(state.TurnWarnings, w => w.Contains("إنذار عاجل"));
    }

    [Fact]
    public void TriggerFactionUltimatum_IsIdempotentDuringCivilWar()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        FactionWarEngine.TriggerFactionUltimatum(state);

        Assert.Empty(state.TurnWarnings);
    }

    [Fact]
    public void AcceptUltimatumDemands_FailsWithoutEnoughGold()
    {
        var state = new GameState();
        state.Gold = 100;

        var result = FactionWarEngine.AcceptUltimatumDemands(state);

        Assert.False(result.Success);
    }

    [Fact]
    public void AcceptUltimatumDemands_ReducesCrownAuthorityAndGoldAndResetsProgress()
    {
        var state = new GameState();
        state.Gold = 1000;
        state.CrownAuthority = CrownAuthorityLevel.High;
        state.Time.IsPaused = true;
        var vassal = SeedVassal(state, "والي1", "gov_x");
        vassal.FactionProgress = 100;

        var result = FactionWarEngine.AcceptUltimatumDemands(state);

        Assert.True(result.Success);
        Assert.Equal(700, state.Gold);
        Assert.Equal(CrownAuthorityLevel.Limited, state.CrownAuthority);
        Assert.Equal(0, vassal.FactionProgress);
        Assert.False(state.Time.IsPaused);
    }

    [Fact]
    public void AcceptUltimatumDemands_DoesNotDropCrownAuthorityBelowLow()
    {
        var state = new GameState();
        state.Gold = 1000;
        state.CrownAuthority = CrownAuthorityLevel.Low;

        var result = FactionWarEngine.AcceptUltimatumDemands(state);

        Assert.True(result.Success);
        Assert.Equal(CrownAuthorityLevel.Low, state.CrownAuthority);
    }

    [Fact]
    public void RefuseUltimatumAndStartCivilWar_SetsStateAndPopulatesRebels()
    {
        var state = new GameState();
        state.Gold = 1000;
        var vassal = SeedVassal(state, "والي1", "gov_x", baseOpinion: -60);
        SeedProvince(state, "وادي_الفرات", vassal.SourceId, vassal.Name);

        var result = FactionWarEngine.RefuseUltimatumAndStartCivilWar(state);

        Assert.True(result.Success);
        Assert.True(state.IsCivilWarActive);
        Assert.Contains(vassal.Id, state.RebelVassalIds);
        Assert.True(state.EnemyArmies.Any(a => FactionWarEngine.IsRebelArmy(a) && a.CurrentProvince == "وادي_الفرات"));
    }

    [Fact]
    public void RefuseUltimatumAndStartCivilWar_SkipsLoyalVassals()
    {
        var state = new GameState();
        state.Gold = 1000;
        var loyal = SeedVassal(state, "مخلص", "gov_y", baseOpinion: 50);
        SeedProvince(state, "السليمانية", loyal.SourceId, loyal.Name);

        var result = FactionWarEngine.RefuseUltimatumAndStartCivilWar(state);

        Assert.True(result.Success);
        Assert.DoesNotContain(loyal.Id, state.RebelVassalIds);
    }

    [Fact]
    public void RefuseUltimatumAndStartCivilWar_MarksProvinceOccupied()
    {
        var state = new GameState();
        state.Gold = 1000;
        var vassal = SeedVassal(state, "والي1", "gov_x", baseOpinion: -60);
        var province = SeedProvince(state, "وادي_الفرات", vassal.SourceId, vassal.Name);

        FactionWarEngine.RefuseUltimatumAndStartCivilWar(state);

        Assert.True(province.Occupied);
        Assert.Equal("المتمردون", province.OccupiedBy);
    }

    [Fact]
    public void RefuseUltimatumAndStartCivilWar_CallsMobilizeArmyOnRebelProvinces()
    {
        var state = new GameState();
        state.Gold = 1000;
        var vassal = SeedVassal(state, "والي1", "gov_x", baseOpinion: -60);
        SeedProvince(state, "وادي_الفرات", vassal.SourceId, vassal.Name);
        int armiesBefore = state.Armies != null ? state.Armies.Count : 0;

        FactionWarEngine.RefuseUltimatumAndStartCivilWar(state);

        int armiesAfter = state.Armies != null ? state.Armies.Count : 0;
        Assert.True(armiesAfter > armiesBefore);
    }

    [Fact]
    public void CheckCivilWarResolution_RoyalVictoryImprisonsAllRebels()
    {
        var state = new GameState();
        state.Gold = 1000;
        var vassal = SeedVassal(state, "والي1", "gov_x", baseOpinion: -60);
        SeedProvince(state, "وادي_الفرات", vassal.SourceId, vassal.Name);
        FactionWarEngine.RefuseUltimatumAndStartCivilWar(state);
        int rebelPrisonersBefore = state.DungeonPrisoners.Count;
        state.EnemyArmies.RemoveAll(a => FactionWarEngine.IsRebelArmy(a));

        FactionWarEngine.CheckCivilWarResolution(state);

        Assert.False(state.IsCivilWarActive);
        Assert.Empty(state.RebelVassalIds);
        Assert.True(state.DungeonPrisoners.Count > rebelPrisonersBefore);
        Assert.Contains(state.DungeonPrisoners, p => p.Id == vassal.Id);
        Assert.True(vassal.IsPrisoner);
        Assert.Contains(vassal.Traits, t => t == "خائن" || t == "TreasonFlag");
    }

    [Fact]
    public void CheckCivilWarResolution_RebelVictoryTriggersAbdication()
    {
        var state = new GameState();
        state.Gold = 1000;
        var vassal = SeedVassal(state, "والي1", "gov_x", baseOpinion: -60);
        SeedProvince(state, "وادي_الفرات", vassal.SourceId, vassal.Name);
        FactionWarEngine.RefuseUltimatumAndStartCivilWar(state);
        int prestigeBefore = state.Prestige;
        if (state.Armies != null) state.Armies.Clear();
        state.RulerIsDead = false;

        FactionWarEngine.CheckCivilWarResolution(state);

        Assert.False(state.IsCivilWarActive);
        Assert.True(state.RulerIsDead);
        Assert.True(state.Prestige < prestigeBefore);
    }

    [Fact]
    public void GetRebelArmySize_SumsOnlyRebelArmies()
    {
        var state = new GameState();
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "rebel_army_1", TotalSoldiers = 500, Name = "A" },
            new Army { Id = "neighbor_1", TotalSoldiers = 200, Name = "B" }
        };

        Assert.Equal(500, FactionWarEngine.GetRebelArmySize(state));
    }

    [Fact]
    public void GetRoyalArmySize_SumsAllArmies()
    {
        var state = new GameState();
        state.Armies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "army1", TotalSoldiers = 1000, Name = "A" },
            new Army { Id = "army2", TotalSoldiers = 500, Name = "B" }
        };

        Assert.Equal(1500, FactionWarEngine.GetRoyalArmySize(state));
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_TriggersUltimatumOnFactionProgress100()
    {
        var state = new GameState();
        var vassal = SeedVassal(state, "والي1", "gov_x", baseOpinion: -60);
        vassal.FactionProgress = 100;
        state.Time.IsPaused = false;
        state.Time.Month = 6;
        state.Time.Day = 15;
        state.TurnWarnings = new System.Collections.Generic.List<string>();
        state.RealmCharacters = new System.Collections.Generic.List<RealmCharacter>
        {
            new RealmCharacter { Id = "Ruler", Name = "الخليفة", Role = CharacterRoleType.Ruler }
        };
        state.Governors = new System.Collections.Generic.List<Governor>
        {
            new Governor { Id = vassal.Id, Name = vassal.Name, ProvinceId = "gov_x", ProvinceName = "وادي_الفرات" }
        };

        for (int i = 0; i < 200; i++)
        {
            vassal.FactionProgress = 100;
            state.Time.IsPaused = false;
            state.TurnWarnings = new System.Collections.Generic.List<string>();
            CalendarTimeSystem.AdvanceDay(state);
            if (state.Time.IsPaused) break;
        }

        Assert.True(state.Time.IsPaused);
    }

    [Fact]
    public void GameState_Serialization_PreservesCivilWarFields()
    {
        var state = new GameState();
        state.IsCivilWarActive = true;
        state.RebelVassalIds.Add("rebel-1");
        state.RebelVassalIds.Add("rebel-2");
        state.InitialRoyalArmySnapshot = 1500;

        string json = System.Text.Json.JsonSerializer.Serialize(state);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized!.IsCivilWarActive);
        Assert.Equal(2, deserialized.RebelVassalIds.Count);
        Assert.Equal(1500, deserialized.InitialRoyalArmySnapshot);
    }
}
