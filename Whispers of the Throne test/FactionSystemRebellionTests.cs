using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

/// <summary>
/// اختبارات إصلاح ثغرة Path A: عندما يُطلق FactionSystem.TriggerRebellion
/// يجب أن يضع IsCivilWarActive = true حتى تتحرك الجيوش المتمردة يومياً.
/// </summary>
public class FactionSystemRebellionTests
{
    private static GameState SeedRebellionState(out Governor rebelGov, out Province rebelProvince)
    {
        var state = new GameState();
        state.ReconcileOldSaves();
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        // نجد والياً غاضباً
        rebelGov = state.Governors.First();
        rebelGov.OpinionOfKing = -100;
        rebelGov.Ambition = 100;
        rebelGov.MilitaryPower = 80;
        rebelGov.IsRebellious = false;

        // نتأكد أن مقاطعته موجودة
        string govProvId = rebelGov.ProvinceId;
        rebelProvince = state.Provinces.First(p => p.Id == govProvId);
        rebelProvince.LocalGarrison = 400;
        rebelProvince.RecruitableLevy = 400;

        return state;
    }

    private static Faction SeedFaction(GameState state, Governor leader)
    {
        var faction = new Faction
        {
            Name = $"فصيل اختبار {leader.Name}",
            Type = "LowerTaxes",
            LeaderGovernorId = leader.Id,
            DemandText = "خفض الضرائب فوراً",
            MainReason = "اختبار آلي",
            PowerPercent = 60,
            Discontent = 100,
            DaysUntilUltimatum = 0,
            IsRebellionStarted = false,
            IsActive = true
        };
        faction.MemberGovernorIds.Add(leader.Id);
        state.Factions.Add(faction);
        return faction;
    }

    [Fact]
    public void TriggerRebellion_SetsIsCivilWarActive_True()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);

        Assert.False(state.IsCivilWarActive); // قبل التمرد

        FactionSystem.TriggerRebellion(state, faction);

        Assert.True(state.IsCivilWarActive, "يجب أن يضع IsCivilWarActive = true حتى تُعالَج الجيوش المتمردة يومياً.");
    }

    [Fact]
    public void TriggerRebellion_PopulatesRebelVassalIds()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);

        FactionSystem.TriggerRebellion(state, faction);

        Assert.NotEmpty(state.RebelVassalIds);
        Assert.Contains(gov.Id, state.RebelVassalIds);
    }

    [Fact]
    public void TriggerRebellion_SetsInitialRoyalArmySnapshot()
    {
        var state = SeedRebellionState(out var gov, out _);
        // نفرّغ الجيوش الافتراضية ثم نضيف جيشاً ملكياً بحجم معروف فقط
        state.Armies.Clear();
        state.Armies.Add(new Army { Name = "الجيش الملكي", TotalSoldiers = 2000, CurrentProvince = "بغداد" });

        FactionSystem.TriggerRebellion(state, faction: SeedFaction(state, gov));

        Assert.True(state.InitialRoyalArmySnapshot > 0, "يجب أن يلتقط لقطة الجيش الملكي عند بدء التمرد.");
        Assert.Equal(2000, state.InitialRoyalArmySnapshot);
    }

    [Fact]
    public void TriggerRebellion_SpawnsRebelArmyInEnemyArmies()
    {
        var state = SeedRebellionState(out var gov, out var province);
        var faction = SeedFaction(state, gov);

        int enemiesBefore = state.EnemyArmies.Count;

        FactionSystem.TriggerRebellion(state, faction);

        Assert.True(state.EnemyArmies.Count > enemiesBefore, "يجب أن يُنشئ جيشاً متمرداً في EnemyArmies.");
    }

    [Fact]
    public void TriggerRebellion_MarksProvinceOccupiedByRebels()
    {
        var state = SeedRebellionState(out var gov, out var province);
        var faction = SeedFaction(state, gov);

        FactionSystem.TriggerRebellion(state, faction);

        Assert.True(province.Occupied, "المقاطعة يجب أن تُعلَم كمحتلة.");
        Assert.Equal("المتمردون", province.OccupiedBy);
    }

    [Fact]
    public void TriggerRebellion_MarksGovernorRebellious()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);

        FactionSystem.TriggerRebellion(state, faction);

        Assert.True(gov.IsRebellious, "الوالي يجب أن يُعلَم كمتمرد.");
    }

    [Fact]
    public void TriggerRebellion_DeactivatesFaction()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);

        FactionSystem.TriggerRebellion(state, faction);

        Assert.False(faction.IsActive, "الفصيل يجب أن يُعطَّل بعد بدء التمرد.");
        Assert.True(faction.IsRebellionStarted, "يجب أن يوضع IsRebellionStarted = true.");
    }

    [Fact]
    public void TriggerRebellion_ResumesTime()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);
        state.Time.IsPaused = true;

        FactionSystem.TriggerRebellion(state, faction);

        Assert.False(state.Time.IsPaused, "الزمن يجب أن يستأنف بعد بدء التمرد حتى تتحرك الجيوش يومياً.");
    }

    [Fact]
    public void TriggerRebellion_AddsCivilWarWarning()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);
        state.TurnWarnings.Clear();

        FactionSystem.TriggerRebellion(state, faction);

        Assert.Contains(state.TurnWarnings, w => w.Contains("حرب أهلية"));
    }

    [Fact]
        public void HandleUltimatum_Reject_SetsIsCivilWarActive_True()
        {
            var state = SeedRebellionState(out var gov, out _);
            // نولّد الإنذار أولاً عبر ProcessDailyFactions
            state.DaysSinceGameStart = FactionSystem.FactionGracePeriodDays + 10; // تجاوز فترة السماح
            gov.OpinionOfKing = -100;
            gov.Ambition = 100;
            gov.MilitaryPower = 100;
            var character = state.RealmCharacters.First(c => c.SourceType == "Governor" && c.SourceId == gov.Id);
            character.BaseOpinion = -100;
            character.VassalPower = 100;
            character.FactionProgress = 99;

            FactionSystem.ProcessDailyFactions(state);
            var faction = state.Factions.FirstOrDefault(f => f.Type == "VassalUltimatum" && f.LeaderGovernorId == gov.Id);
            Assert.NotNull(faction);

            FactionSystem.HandleUltimatum(state, faction.Id, "Reject");

            Assert.True(state.IsCivilWarActive, "رفض الإنذار يجب أن يفعل الحرب الأهلية عبر TriggerRebellion المُصلَح.");
        }

    [Fact]
    public void TriggerRebellion_RebelArmyId_StartsWithRebelPrefix()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);

        FactionSystem.TriggerRebellion(state, faction);

        Assert.Contains(state.EnemyArmies, a => a.Id.StartsWith(FactionWarEngine.RebelArmyPrefix));
    }

    [Fact]
    public void TriggerRebellion_SetsCurrentWarGoal_ToRebellion()
    {
        var state = SeedRebellionState(out var gov, out _);
        var faction = SeedFaction(state, gov);

        FactionSystem.TriggerRebellion(state, faction);

        Assert.NotNull(state.CurrentWarGoal);
        Assert.Equal(WarGoalType.Rebellion, state.CurrentWarGoal.Type);
    }
}
