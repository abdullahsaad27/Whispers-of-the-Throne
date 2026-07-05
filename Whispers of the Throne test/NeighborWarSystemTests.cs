using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

/// <summary>
/// اختبارات نظام NeighborWarSystem: جيوش الدول المجاورة تتحرك في الحروب الخارجية.
/// </summary>
public class NeighborWarSystemTests
{
    private static GameState SeedWarState(out Neighbor enemy)
    {
        var state = new GameState();
        state.ReconcileOldSaves();
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        // نجهّز حرباً خارجية
        enemy = state.Neighbors[0];
        enemy.Army = 2000;
        enemy.Name = "الدولة_العدوة";
        enemy.Ruler = "السلطان المعادي";
        enemy.IsAtWarWithPlayer = true;
        enemy.Relation = "حرب";
        enemy.HasClaim = true;
        enemy.ClaimedProvince = enemy.ClaimableProvinces.Count > 0
            ? enemy.ClaimableProvinces[0].Name
            : "الموصل";

        state.ActiveWar = new ActiveWar
        {
            Type = "conquest",
            NeighborIdx = 0,
            TargetProvince = enemy.ClaimedProvince,
            Garrison = 200,
            Turns = 0
        };

        state.SiegeData = new SiegeData
        {
            TargetName = enemy.ClaimedProvince,
            TargetGarrison = 200,
            PlayerArmy = 1000,
            Turns = 0,
            SiegeProgress = 0
        };

        // نتأكد أن العاصمة موجودة
        if (state.Provinces.Count > 0)
        {
            state.Provinces[0].LocalGarrison = 200;
            state.Provinces[0].Garrison = 100;
            state.Provinces[0].Occupied = false;
            state.Provinces[0].OccupiedBy = null;
        }

        return state;
    }

    // ─── SpawnEnemyFieldArmy ───

    [Fact]
    public void SpawnEnemyFieldArmy_CreatesArmyInEnemyArmies()
    {
        var state = SeedWarState(out var enemy);
        int before = state.EnemyArmies.Count;

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);

        Assert.True(state.EnemyArmies.Count > before, "يجب أن يُضاف جيش معادٍ إلى EnemyArmies.");
    }

    [Fact]
    public void SpawnEnemyFieldArmy_ArmyHasEnemyPrefix()
    {
        var state = SeedWarState(out var enemy);

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);

        Assert.Contains(state.EnemyArmies, a => NeighborWarSystem.IsForeignEnemyArmy(a));
    }

    [Fact]
    public void SpawnEnemyFieldArmy_ArmyHasNonZeroSoldiers()
    {
        var state = SeedWarState(out var enemy);

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);

        var enemyArmy = state.EnemyArmies.FirstOrDefault(NeighborWarSystem.IsForeignEnemyArmy);
        Assert.NotNull(enemyArmy);
        Assert.True(enemyArmy.TotalSoldiers > 0, "الجيش المعادي يجب أن يملك جنوداً.");
    }

    [Fact]
    public void SpawnEnemyFieldArmy_ArmyMovesTowardCapital()
    {
        var state = SeedWarState(out var enemy);
        string capitalName = state.Provinces.Count > 0 ? state.Provinces[0].Name : "";

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);

        var enemyArmy = state.EnemyArmies.FirstOrDefault(NeighborWarSystem.IsForeignEnemyArmy);
        Assert.NotNull(enemyArmy);
        Assert.Equal(capitalName, enemyArmy.DestinationProvince);
        Assert.True(enemyArmy.DaysToDestination > 0, "الجيش المعادي يجب أن يكون في حالة زحف نحو العاصمة.");
    }

    [Fact]
    public void SpawnEnemyFieldArmy_DoesNotExceedTwoArmiesPerNeighbor()
    {
        var state = SeedWarState(out var enemy);

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);
        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);
        int countAfterTwo = state.EnemyArmies.Count(NeighborWarSystem.IsForeignEnemyArmy);

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);
        int countAfterThree = state.EnemyArmies.Count(NeighborWarSystem.IsForeignEnemyArmy);

        Assert.Equal(countAfterTwo, countAfterThree);
    }

    [Fact]
    public void SpawnEnemyFieldArmy_AddsWarning()
    {
        var state = SeedWarState(out var enemy);
        state.TurnWarnings.Clear();

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);

        Assert.Contains(state.TurnWarnings, w => w.Contains("حرب خارجية"));
    }

    // ─── ProcessDailyEnemyArmies ───

    [Fact]
    public void ProcessDailyEnemyArmies_IsNoopWhenNoActiveWar()
    {
        var state = new GameState();
        state.ReconcileOldSaves();
        state.ActiveWar = null;
        state.EnemyArmies.Add(new Army
        {
            Id = NeighborWarSystem.EnemyArmyPrefix + "test",
            Name = "TestEnemy",
            TotalSoldiers = 500,
            CurrentProvince = "A"
        });

        string report = NeighborWarSystem.ProcessDailyEnemyArmies(state);

        // لا حرب نشطة → لا تُعالَج الجيوش
        Assert.Equal("", report);
    }

    [Fact]
    public void ProcessDailyEnemyArmies_MovesTowardDestination()
    {
        var state = SeedWarState(out var enemy);
        state.Armies.Clear(); // لا جيش ملكي
        // نربط المقاطعات اتصالياً
        if (state.Provinces.Count >= 2)
        {
            if (!state.Provinces[1].ConnectedProvinces.Contains(state.Provinces[0].Name))
                state.Provinces[1].ConnectedProvinces.Add(state.Provinces[0].Name);
            if (!state.Provinces[0].ConnectedProvinces.Contains(state.Provinces[1].Name))
                state.Provinces[0].ConnectedProvinces.Add(state.Provinces[1].Name);
        }

        NeighborWarSystem.SpawnEnemyFieldArmy(state, enemy);
        var enemyArmy = state.EnemyArmies.FirstOrDefault(NeighborWarSystem.IsForeignEnemyArmy);
        Assert.NotNull(enemyArmy);
        int daysBefore = enemyArmy.DaysToDestination;

        NeighborWarSystem.ProcessDailyEnemyArmies(state);

        // يجب أن يتقدم الزحف (يوم أو وصل)
        Assert.True(enemyArmy.DaysToDestination < daysBefore || enemyArmy.CurrentProvince == enemyArmy.DestinationProvince);
    }

    [Fact]
    public void ProcessDailyEnemyArmies_OccupiesUndefendedProvinceOnArrival()
    {
        var state = SeedWarState(out var enemy);
        state.Armies.Clear();

        // نضع جيشاً معادياً وصل لتوّه إلى مقاطعة غير محمية
        var targetProvince = state.Provinces.Count > 1 ? state.Provinces[1] : state.Provinces[0];
        targetProvince.Occupied = false;
        targetProvince.OccupiedBy = null;
        targetProvince.LocalGarrison = 0;
        targetProvince.Garrison = 0;
        string enemyName = enemy.Name;

        state.EnemyArmies.Add(new Army
        {
            Id = NeighborWarSystem.EnemyArmyPrefix + "test1",
            Name = $"جيش {enemyName} الغازي",
            TotalSoldiers = 800,
            CurrentProvince = targetProvince.Name,
            DestinationProvince = targetProvince.Name,
            CurrentOrder = "MoveToProvince",
            DaysToDestination = 1,
            CommanderName = "القائد"
        });

        // المعالجة اليومية تنقص DaysToDestination إلى 0 ثم تعالج الوصول
        NeighborWarSystem.ProcessDailyEnemyArmies(state);

        Assert.True(targetProvince.Occupied, "المقاطعة غير المحمية يجب أن تُحتَل.");
        Assert.Equal(enemyName, targetProvince.OccupiedBy);
    }

    [Fact]
    public void ProcessDailyEnemyArmies_ClashesWithRoyalArmyInSameProvince()
    {
        var state = SeedWarState(out var enemy);
        var province = state.Provinces.Count > 1 ? state.Provinces[1] : state.Provinces[0];
        province.LocalGarrison = 0;
        province.Garrison = 0;
        province.Occupied = false;

        // جيش ملكي في نفس المقاطعة
        state.Armies.Clear();
        state.Armies.Add(new Army
        {
            Id = "royal_test",
            Name = "الجيش الملكي",
            TotalSoldiers = 5000,
            CurrentProvince = province.Name
        });

        // جيش معادٍ ضعيف جداً في نفس المقاطعة
        state.EnemyArmies.Add(new Army
        {
            Id = NeighborWarSystem.EnemyArmyPrefix + "test_weak",
            Name = $"جيش {enemy.Name} الغازي",
            TotalSoldiers = 10,
            CurrentProvince = province.Name,
            DestinationProvince = null,
            CurrentOrder = "Idle",
            DaysToDestination = 0,
            CommanderName = "القائد"
        });

        NeighborWarSystem.ProcessDailyEnemyArmies(state);

        // الجيش المعادي الضعيف جداً يجب أن يُسحق (0 جندي ويُزال)
        var weakArmy = state.EnemyArmies.FirstOrDefault(a => a.Id == NeighborWarSystem.EnemyArmyPrefix + "test_weak");
        Assert.True(weakArmy == null || weakArmy.TotalSoldiers <= 0 || weakArmy.TotalSoldiers < 10,
            "الجيش المعادي الضعيف جداً يجب أن يخسر بشدة أمام الجيش الملكي الضخم.");
    }

    [Fact]
    public void ProcessDailyEnemyArmies_CapitalFallsWhenUndefended()
    {
        var state = SeedWarState(out var enemy);
        state.Armies.Clear();
        var capital = state.Provinces[0];
        capital.LocalGarrison = 0;
        capital.Garrison = 0;
        capital.Occupied = false;
        string enemyName = enemy.Name;

        int prestigeBefore = state.Prestige;

        state.EnemyArmies.Add(new Army
        {
            Id = NeighborWarSystem.EnemyArmyPrefix + "test_cap",
            Name = $"جيش {enemyName} الغازي",
            TotalSoldiers = 1500,
            CurrentProvince = capital.Name,
            DestinationProvince = capital.Name,
            CurrentOrder = "MoveToProvince",
            DaysToDestination = 1,
            CommanderName = "القائد"
        });

        NeighborWarSystem.ProcessDailyEnemyArmies(state);

        Assert.True(capital.Occupied, "العاصمة غير المحمية يجب أن تسقط.");
        Assert.Equal(enemyName, capital.OccupiedBy);
        Assert.True(state.Prestige < prestigeBefore, "سقوط العاصمة يجب أن يخفض الهيبة.");
    }

    [Fact]
    public void ProcessDailyEnemyArmies_CapitalGarrisonFightsBack()
    {
        var state = SeedWarState(out var enemy);
        state.Armies.Clear();
        var capital = state.Provinces[0];
        capital.LocalGarrison = 1000;
        capital.Garrison = 500;
        capital.Occupied = false;

        state.EnemyArmies.Add(new Army
        {
            Id = NeighborWarSystem.EnemyArmyPrefix + "test_cap2",
            Name = $"جيش {enemy.Name} الغازي",
            TotalSoldiers = 500,
            CurrentProvince = capital.Name,
            DestinationProvince = null,
            CurrentOrder = "Idle",
            DaysToDestination = 0,
            CommanderName = "القائد"
        });

        NeighborWarSystem.ProcessDailyEnemyArmies(state);

        // الحامية يجب أن تدافع (العدو ضعيف نسبياً)
        Assert.True(state.TurnWarnings.Any(w => w.Contains("معركة عاصمة") || w.Contains("حرب خارجية")),
            "يجب أن يكون هناك تقرير عن معركة في العاصمة.");
    }

    // ─── ClearForeignEnemyArmies / LiberateProvincesOccupiedBy ───

    [Fact]
    public void ClearForeignEnemyArmies_RemovesOnlyEnemyArmies()
    {
        var state = SeedWarState(out var enemy);
        // نبني بادئة مطابقة لما يستخدمه SpawnEnemyFieldArmy فعلياً
        string safeId = new string(enemy.Id.Where(char.IsLetterOrDigit).ToArray());
        if (safeId.Length == 0) safeId = "x";
        string enemyPrefix = NeighborWarSystem.EnemyArmyPrefix + safeId;
        // نضيف جيشاً معادياً بالبادئة الصحيحة وآخر متمرداً
        state.EnemyArmies.Add(new Army
        {
            Id = enemyPrefix + "_test_clear",
            Name = "جيش عدو",
            TotalSoldiers = 500,
            CurrentProvince = "A"
        });
        state.EnemyArmies.Add(new Army
        {
            Id = "rebel_keep",
            Name = "جيش متمردين",
            TotalSoldiers = 300,
            CurrentProvince = "B"
        });

        NeighborWarSystem.ClearForeignEnemyArmies(state, enemy);

        Assert.DoesNotContain(state.EnemyArmies, a => a.Id.StartsWith(enemyPrefix));
        Assert.Contains(state.EnemyArmies, a => a.Id == "rebel_keep");
    }

    [Fact]
    public void LiberateProvincesOccupiedBy_FreesOnlyMatchingProvinces()
    {
        var state = SeedWarState(out var enemy);
        if (state.Provinces.Count >= 2)
        {
            state.Provinces[0].Occupied = true;
            state.Provinces[0].OccupiedBy = enemy.Name;
            state.Provinces[1].Occupied = true;
            state.Provinces[1].OccupiedBy = "المتمردون";
        }

        NeighborWarSystem.LiberateProvincesOccupiedBy(state, enemy.Name);

        if (state.Provinces.Count >= 2)
        {
            Assert.False(state.Provinces[0].Occupied);
            Assert.Null(state.Provinces[0].OccupiedBy);
            // المقاطعة المتمردة لا تتأثر
            Assert.True(state.Provinces[1].Occupied);
            Assert.Equal("المتمردون", state.Provinces[1].OccupiedBy);
        }
    }

    [Fact]
    public void GetForeignEnemyArmySize_ReturnsCorrectTotal()
    {
        var state = new GameState();
        state.ReconcileOldSaves();
        state.EnemyArmies.Add(new Army { Id = NeighborWarSystem.EnemyArmyPrefix + "a", TotalSoldiers = 300, Name = "Enemy1" });
        state.EnemyArmies.Add(new Army { Id = NeighborWarSystem.EnemyArmyPrefix + "b", TotalSoldiers = 200, Name = "Enemy2" });
        state.EnemyArmies.Add(new Army { Id = "rebel_army_x", TotalSoldiers = 500, Name = "Rebel" });

        Assert.Equal(500, NeighborWarSystem.GetForeignEnemyArmySize(state));
    }

    [Fact]
    public void IsForeignEnemyArmy_DistinguishesFromRebelArmy()
    {
        var foreignArmy = new Army { Id = NeighborWarSystem.EnemyArmyPrefix + "x", Name = "Enemy" };
        var rebelArmy = new Army { Id = FactionWarEngine.RebelArmyPrefix + "y", Name = "Rebel" };

        Assert.True(NeighborWarSystem.IsForeignEnemyArmy(foreignArmy));
        Assert.False(NeighborWarSystem.IsForeignEnemyArmy(rebelArmy));
        Assert.True(FactionWarEngine.IsRebelArmy(rebelArmy));
        Assert.False(FactionWarEngine.IsRebelArmy(foreignArmy));
    }

    // ─── التكامل مع WarfareSystem ───

    [Fact]
    public void DeclareWar_SpawnsEnemyFieldArmy()
    {
        var state = new GameState();
        state.ReconcileOldSaves();
        var neighbor = state.Neighbors[0];
        neighbor.HasClaim = true;
        neighbor.ClaimedProvince = neighbor.ClaimableProvinces.Count > 0
            ? neighbor.ClaimableProvinces[0].Name : "";
        state.Armies[0].TotalSoldiers = 3000;

        int enemiesBefore = state.EnemyArmies.Count;

        var result = WarfareSystem.DeclareWar(state, 0, "Claim");

        Assert.True(result.Success);
        Assert.True(state.EnemyArmies.Count > enemiesBefore,
            "إعلان الحرب يجب أن يُنشئ جيشاً معادياً ميدانياً عبر NeighborWarSystem.");
    }

    [Fact]
    public void DeclareWar_EnemyArmyHasValidProperties()
    {
        var state = new GameState();
        state.ReconcileOldSaves();
        var neighbor = state.Neighbors[0];
        neighbor.HasClaim = true;
        neighbor.ClaimedProvince = neighbor.ClaimableProvinces.Count > 0
            ? neighbor.ClaimableProvinces[0].Name : "";
        state.Armies[0].TotalSoldiers = 3000;

        WarfareSystem.DeclareWar(state, 0, "Claim");

        var enemyArmy = state.EnemyArmies.FirstOrDefault(NeighborWarSystem.IsForeignEnemyArmy);
        Assert.NotNull(enemyArmy);
        Assert.True(enemyArmy.TotalSoldiers > 0);
        Assert.False(string.IsNullOrEmpty(enemyArmy.CommanderName));
    }
}
