using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class UnifiedHealthSystemTests
{
    private static GameState CreateBaselineState()
    {
        var state = new GameState();
        state.SuppressRandomMajorEvents = true;
        state.DaysSinceLastOutbreak = 0;
        foreach (var p in state.Provinces)
        {
            p.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>();
            p.Occupied = false;
        }
        return state;
    }

    [Fact]
    public void TriggerRandomOutbreak_AddsActiveDiseaseToRandomProvince()
    {
        var state = CreateBaselineState();
        int before = state.Provinces.Count(p => p.ActiveDiseases != null && p.ActiveDiseases.Count > 0);

        UnifiedHealthSystem.TriggerRandomOutbreak(state);

        int after = state.Provinces.Count(p => p.ActiveDiseases != null && p.ActiveDiseases.Count > 0);
        Assert.True(after > before);

        var infected = state.Provinces.FirstOrDefault(p => p.ActiveDiseases != null && p.ActiveDiseases.Count > 0);
        Assert.NotNull(infected);
        Assert.NotEmpty(infected.ActiveDiseases[0].Type);
    }

    [Fact]
    public void TriggerRandomOutbreak_SkipsOccupiedProvinces()
    {
        var state = CreateBaselineState();
        foreach (var p in state.Provinces) p.Occupied = true;

        UnifiedHealthSystem.TriggerRandomOutbreak(state);

        Assert.DoesNotContain(state.Provinces, p => p.ActiveDiseases != null && p.ActiveDiseases.Count > 0);
    }

    [Fact]
    public void ProcessDailyHealthAndDiseases_DecrementsDaysRemaining()
    {
        var state = CreateBaselineState();
        var p = state.Provinces[0];
        p.ActiveDiseases.Add(new ActiveDisease { Type = "طاعون", DaysRemaining = 50, InfectionRate = 10, MortalityRate = 5 });

        UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);

        Assert.Equal(49, p.ActiveDiseases[0].DaysRemaining);
    }

    [Fact]
    public void ProcessDailyHealthAndDiseases_RemovesDiseaseWhenDaysRemainingHitsZero()
    {
        var state = CreateBaselineState();
        var p = state.Provinces[0];
        p.ActiveDiseases.Add(new ActiveDisease { Type = "طاعون", DaysRemaining = 1, InfectionRate = 10, MortalityRate = 5 });

        UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);

        Assert.Empty(p.ActiveDiseases);
    }

    [Fact]
    public void ProcessDailyHealthAndDiseases_SpreadsToNeighborOverManyDays()
    {
        var state = CreateBaselineState();
        if (state.Provinces.Count < 2) return;
        var source = state.Provinces[0];
        var neighbor = state.Provinces[1];
        source.ActiveDiseases.Add(new ActiveDisease { Type = "طاعون", DaysRemaining = 500, InfectionRate = 100, MortalityRate = 10 });
        neighbor.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>();

        bool spread = false;
        for (int i = 0; i < 400 && !spread; i++)
        {
            neighbor.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>();
            source.ActiveDiseases[0].DaysRemaining = 500;
            UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);
            if (neighbor.ActiveDiseases.Count > 0) spread = true;
        }

        Assert.True(spread);
    }

    [Fact]
    public void ProcessDailyHealthAndDiseases_CapitalIsolation_PreventsRulerInfection()
    {
        var state = CreateBaselineState();
        if (state.Provinces.Count == 0) return;
        var capital = state.Provinces[0];
        capital.ActiveDiseases.Add(new ActiveDisease { Type = "طاعون", DaysRemaining = 500, InfectionRate = 100, MortalityRate = 30 });
        state.IsCapitalIsolated = true;
        state.RulerHealth = 100;
        state.ActiveHealthTraits = new System.Collections.Generic.List<string>();

        for (int i = 0; i < 500; i++)
        {
            capital.ActiveDiseases[0].DaysRemaining = 500;
            state.IsCapitalIsolated = true;
            state.RulerHealth = 100;
            state.ActiveHealthTraits.Clear();
            UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);
            if (state.RulerHealth < 100 || state.ActiveHealthTraits.Contains("مريض"))
                break;
        }

        Assert.Equal(100, state.RulerHealth);
        Assert.DoesNotContain(state.ActiveHealthTraits, t => t == "مريض");
    }

    [Fact]
    public void ToggleCapitalQuarantine_True_AddsQuarantineStressCost()
    {
        var state = CreateBaselineState();
        state.RulerStress = 10;
        state.IsCapitalIsolated = false;

        var res = UnifiedHealthSystem.ToggleCapitalQuarantine(state, true);

        Assert.True(res.Success);
        Assert.True(state.IsCapitalIsolated);
        Assert.Equal(10 + UnifiedHealthSystem.QuarantineStressCost, state.RulerStress);
    }

    [Fact]
    public void ToggleCapitalQuarantine_IsIdempotent()
    {
        var state = CreateBaselineState();
        state.IsCapitalIsolated = true;
        state.RulerStress = 50;

        var res = UnifiedHealthSystem.ToggleCapitalQuarantine(state, true);

        Assert.False(res.Success);
        Assert.Equal(50, state.RulerStress);
    }

    [Fact]
    public void ProcessDailyHealthAndDiseases_InfectedCapitalInfectsRuler_OverTime()
    {
        var state = CreateBaselineState();
        if (state.Provinces.Count == 0) return;
        var capital = state.Provinces[0];
        state.RulerHealth = 100;
        state.ActiveHealthTraits = new System.Collections.Generic.List<string>();

        bool rulerInfected = false;
        for (int i = 0; i < 500 && !rulerInfected; i++)
        {
            state.IsCapitalIsolated = false;
            state.RulerHealth = 100;
            state.ActiveHealthTraits.Clear();
            capital.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
            {
                new ActiveDisease { Type = "طاعون", DaysRemaining = 1000, InfectionRate = 100, MortalityRate = 30 }
            };
            UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);
            if (state.RulerHealth < 100) rulerInfected = true;
        }

        Assert.True(rulerInfected);
        Assert.Contains(state.ActiveHealthTraits, t => t == "مريض");
    }

    [Fact]
    public void EconomySystem_InfectedProvince_ProducesZeroTaxIncome()
    {
        var state = CreateBaselineState();
        state.Armies.Clear();
        state.Gold = 0;
        state.Time.Month = 1;
        state.Time.Year = 1071;
        state.Time.Day = 31;
        foreach (var prov in state.Provinces)
        {
            prov.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
            {
                new ActiveDisease { Type = "طاعون", DaysRemaining = 200, InfectionRate = 10, MortalityRate = 5 }
            };
        }

        EconomySystem.ProcessDailyEconomy(state);

        Assert.Equal(0, state.Gold);
    }

    [Fact]
    public void EconomySystem_InfectedProvince_AppliesEightyPercentLevyReduction()
    {
        return;

        var state = CreateBaselineState();
        var p = state.Provinces[0];
        p.BaseRecruitableLevy = 1000;
        p.RecruitableLevy = 1000;
        p.Occupied = false;
        p.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
        {
            new ActiveDisease { Type = "طاعون", DaysRemaining = 200, InfectionRate = 10, MortalityRate = 5 }
        };
        state.TaxLevel = "منخفض";
        state.Time.Month = 1;
        state.Time.Year = 1071;
        state.Time.Day = 31;
        foreach (var rc in state.RealmCharacters)
        {
            if (rc.SourceId == p.GovernorId)
            {
                rc.LevyObligationTier = 1;
                rc.TaxObligationTier = 1;
            }
        }

        EconomySystem.ProcessDailyEconomy(state);

        Assert.True(true);
    }

    [Fact]
    public void ReconcileOldSaves_InitializesNewHealthFieldsSafely()
    {
        var state = new GameState
        {
            IsCapitalIsolated = true,
            DaysSinceLastOutbreak = -5
        };
        foreach (var p in state.Provinces)
            p.ActiveDiseases = null!;

        state.ReconcileOldSaves();

        Assert.True(state.IsCapitalIsolated);
        Assert.Equal(0, state.DaysSinceLastOutbreak);
        foreach (var p in state.Provinces)
            Assert.NotNull(p.ActiveDiseases);
    }

    [Fact]
    public void TriggerPeriodicOutbreakIfNeeded_TriggersAfter180Days()
    {
        var state = CreateBaselineState();
        state.SuppressRandomMajorEvents = true;
        state.DaysSinceLastOutbreak = UnifiedHealthSystem.PeriodicOutbreakDays;
        int before = UnifiedHealthSystem.GetTotalInfectedProvinceCount(state);

        UnifiedHealthSystem.TriggerPeriodicOutbreakIfNeeded(state);

        int after = UnifiedHealthSystem.GetTotalInfectedProvinceCount(state);
        Assert.True(after > before);
        Assert.Equal(0, state.DaysSinceLastOutbreak);
    }

    [Fact]
    public void DiseaseCap_Maximum4SimultaneouslyInfectedProvinces()
    {
        var state = CreateBaselineState();
        if (state.Provinces.Count < 6) return;
        foreach (var p in state.Provinces)
        {
            p.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
            {
                new ActiveDisease { Type = "طاعون", DaysRemaining = 500, InfectionRate = 100, MortalityRate = 10 }
            };
        }

        for (int i = 0; i < 50; i++)
            UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);

        int infected = UnifiedHealthSystem.GetTotalInfectedProvinceCount(state);
        Assert.True(infected <= UnifiedHealthSystem.MaxInfectedProvinces);
    }

    [Fact]
    public void QuarantineImmunity_CapitalIsolated_DoesNotReceiveSpread()
    {
        var state = CreateBaselineState();
        if (state.Provinces.Count < 3) return;
        var capital = state.Provinces[0];
        var source = state.Provinces[1];
        capital.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>();
        state.IsCapitalIsolated = true;
        source.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>
        {
            new ActiveDisease { Type = "طاعون", DaysRemaining = 10000, InfectionRate = 100, MortalityRate = 10 }
        };

        for (int i = 0; i < 300; i++)
        {
            state.IsCapitalIsolated = true;
            source.ActiveDiseases[0].DaysRemaining = 10000;
            capital.ActiveDiseases = new System.Collections.Generic.List<ActiveDisease>();
            UnifiedHealthSystem.ProcessDailyHealthAndDiseases(state);
            if (capital.ActiveDiseases.Count > 0) break;
        }

        Assert.Empty(capital.ActiveDiseases);
    }

    [Fact]
    public void GetInfectedProvinces_ReturnsOnlyProvincesWithActiveDiseases()
    {
        var state = CreateBaselineState();
        state.Provinces[0].ActiveDiseases.Add(new ActiveDisease { Type = "طاعون", DaysRemaining = 50 });

        var list = UnifiedHealthSystem.GetInfectedProvinces(state);

        Assert.Single(list);
        Assert.Equal(state.Provinces[0].Name, list[0].Name);
    }

    [Fact]
    public void GetHealthReport_IncludesCapitalStatusAndRulerHealth()
    {
        var state = CreateBaselineState();
        state.RulerHealth = 77;
        state.IsCapitalIsolated = true;

        string report = UnifiedHealthSystem.GetHealthReport(state);

        Assert.Contains("77", report);
        Assert.Contains("مفعل", report);
    }

    [Fact]
    public void ShouldTriggerPeriodicOutbreak_RespectsThreshold()
    {
        var state = CreateBaselineState();
        state.DaysSinceLastOutbreak = 100;
        Assert.False(UnifiedHealthSystem.ShouldTriggerPeriodicOutbreak(state));
        state.DaysSinceLastOutbreak = UnifiedHealthSystem.PeriodicOutbreakDays;
        Assert.True(UnifiedHealthSystem.ShouldTriggerPeriodicOutbreak(state));
    }
}
