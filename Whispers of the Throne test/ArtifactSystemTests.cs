using System;
using System.Linq;
using WhispersOfTheThrone.Models;
using WhispersOfTheThrone.Systems;
using Xunit;

namespace WhispersOfTheThrone.Tests;

public class ArtifactSystemTests
{
    private static Artifact SeedArtifact(string name, string slot, string buff, int value)
    {
        return new Artifact
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Type = slot,
            SlotType = slot,
            BuffType = buff,
            BuffValue = value,
            Value = 300,
            Durability = 100,
            IsHeirloom = true
        };
    }

    private static RealmCharacter SeedVassal(GameState state, string name)
    {
        var vassal = new RealmCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SourceType = "Governor",
            Role = CharacterRoleType.Governor,
            BaseOpinion = 0,
            OpinionModifiers = new System.Collections.Generic.List<OpinionModifier>()
        };
        state.RealmCharacters.Add(vassal);
        return vassal;
    }

    [Fact]
    public void EquipArtifact_AssignsToCorrectSlot()
    {
        var state = new GameState();
        var weapon = SeedArtifact("سيف", ArtifactSystem.SlotWeapon, ArtifactSystem.BuffMartial, 10);
        var robe = SeedArtifact("رداء", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 5);
        var book = SeedArtifact("كتاب", ArtifactSystem.SlotBook, ArtifactSystem.BuffPiety, 8);

        ArtifactSystem.EquipArtifact(state, weapon);
        ArtifactSystem.EquipArtifact(state, robe);
        ArtifactSystem.EquipArtifact(state, book);

        Assert.Same(weapon, state.EquippedWeapon);
        Assert.Same(robe, state.EquippedRobe);
        Assert.Same(book, state.EquippedBook);
    }

    [Fact]
    public void GetWeaponAdvantageBonus_ReturnsBuffValueForMartialWeapon()
    {
        var state = new GameState();
        state.EquippedWeapon = SeedArtifact("سيف", ArtifactSystem.SlotWeapon, ArtifactSystem.BuffMartial, 12);
        Assert.Equal(12, ArtifactSystem.GetWeaponAdvantageBonus(state));
    }

    [Fact]
    public void GetWeaponAdvantageBonus_ReturnsZeroForNonMartial()
    {
        var state = new GameState();
        state.EquippedWeapon = SeedArtifact("سيف", ArtifactSystem.SlotWeapon, ArtifactSystem.BuffPiety, 12);
        Assert.Equal(0, ArtifactSystem.GetWeaponAdvantageBonus(state));
    }

    [Fact]
    public void GetBookPietyBonus_ReturnsBuffValueForPietyBook()
    {
        var state = new GameState();
        state.EquippedBook = SeedArtifact("كتاب", ArtifactSystem.SlotBook, ArtifactSystem.BuffPiety, 7);
        Assert.Equal(7, ArtifactSystem.GetBookPietyBonus(state));
    }

    [Fact]
    public void GetRobeOpinionBonus_ReturnsBuffValueForOpinionRobe()
    {
        var state = new GameState();
        state.EquippedRobe = SeedArtifact("رداء", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 9);
        Assert.Equal(9, ArtifactSystem.GetRobeOpinionBonus(state));
    }

    [Fact]
    public void EquipArtifact_RobeAppliesPermanentOpinionModifierToAllCharacters()
    {
        var state = new GameState();
        state.RealmCharacters = new System.Collections.Generic.List<RealmCharacter>();
        var vassal = SeedVassal(state, "والي1");
        state.EquippedRobe = SeedArtifact("رداء", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 15);

        var newRobe = SeedArtifact("رداء2", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 20);
        state.TreasuryInventory.Add(newRobe);
        ArtifactSystem.EquipArtifact(state, newRobe);

        Assert.Contains(vassal.OpinionModifiers, m => m.Key == ArtifactSystem.OpinionRobeModifierKey && m.Value == 20 && m.IsPermanent);
    }

    [Fact]
    public void EquipArtifact_ReplacingRobeUpdatesOpinionModifier()
    {
        var state = new GameState();
        state.RealmCharacters = new System.Collections.Generic.List<RealmCharacter>();
        var vassal = SeedVassal(state, "والي1");
        var first = SeedArtifact("رداء1", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 10);
        var second = SeedArtifact("رداء2", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 20);
        state.TreasuryInventory.Add(first);
        state.TreasuryInventory.Add(second);
        ArtifactSystem.EquipArtifact(state, first);
        ArtifactSystem.EquipArtifact(state, second);
        var mod = vassal.OpinionModifiers.Find(m => m.Key == ArtifactSystem.OpinionRobeModifierKey);
        Assert.NotNull(mod);
        Assert.Equal(20, mod.Value);
    }

    [Fact]
    public void FundArtifactExpedition_FailsWithoutGold()
    {
        var state = new GameState();
        state.Gold = 100;

        var result = ArtifactSystem.FundArtifactExpedition(state);

        Assert.False(result.Success);
    }

    [Fact]
    public void FundArtifactExpedition_DeductsGoldAndCreatesTask()
    {
        var state = new GameState();
        state.Gold = 1000;

        var result = ArtifactSystem.FundArtifactExpedition(state);

        Assert.True(result.Success);
        Assert.Equal(700, state.Gold);
        Assert.Contains(state.DelegatedTasks, t => t.TaskType == ArtifactSystem.ExpeditedTaskType && t.DaysRemaining == 45);
    }

    [Fact]
    public void CompleteArtifactExpedition_AddsRandomArtifactToTreasury()
    {
        var state = new GameState();
        state.TreasuryInventory = new System.Collections.Generic.List<Artifact>();
        state.TurnWarnings = new System.Collections.Generic.List<string>();

        ArtifactSystem.CompleteArtifactExpedition(state);

        Assert.Single(state.TreasuryInventory);
        var item = state.TreasuryInventory[0];
        Assert.False(string.IsNullOrEmpty(item.SlotType));
        Assert.False(string.IsNullOrEmpty(item.BuffType));
        Assert.True(item.BuffValue > 0);
    }

    [Fact]
    public void CalendarTimeSystem_AdvanceDay_CompletesArtifactExpedition()
    {
        var state = new GameState();
        state.Gold = 1000;
        state.TreasuryInventory = new System.Collections.Generic.List<Artifact>();
        state.Time.Month = 6;
        state.Time.Day = 15;
        ArtifactSystem.FundArtifactExpedition(state);
        var task = state.DelegatedTasks.First(t => t.TaskType == ArtifactSystem.ExpeditedTaskType);
        task.DaysRemaining = 1;

        CalendarTimeSystem.AdvanceDay(state);

        Assert.DoesNotContain(state.DelegatedTasks, t => t.TaskType == ArtifactSystem.ExpeditedTaskType);
        Assert.NotEmpty(state.TreasuryInventory);
    }

    [Fact]
    public void CalendarTimeSystem_MonthlyCollection_AddsBookPiety()
    {
        var state = new GameState();
        state.Time.Month = 1;
        state.Time.Day = 30;
        state.Time.Year = 1071;
        state.EquippedBook = SeedArtifact("مصحف", ArtifactSystem.SlotBook, ArtifactSystem.BuffPiety, 25);
        state.Piety = 100;
        state.TurnWarnings = new System.Collections.Generic.List<string>();
        state.ActiveWar = null;

        CalendarTimeSystem.AdvanceDay(state);

        Assert.True(state.Piety >= 100);
        Assert.Contains(state.TurnWarnings, w => w.Contains("الكتاب المجهز"));
    }

    [Fact]
    public void GetUnequippedTreasuryItems_ExcludesEquipped()
    {
        var state = new GameState();
        state.TreasuryInventory = new System.Collections.Generic.List<Artifact>();
        var equipped = SeedArtifact("مُجهَّز", ArtifactSystem.SlotWeapon, ArtifactSystem.BuffMartial, 5);
        var unequipped1 = SeedArtifact("غير مُجهَّز1", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 5);
        var unequipped2 = SeedArtifact("غير مُجهَّز2", ArtifactSystem.SlotBook, ArtifactSystem.BuffPiety, 5);
        state.TreasuryInventory.Add(equipped);
        state.TreasuryInventory.Add(unequipped1);
        state.TreasuryInventory.Add(unequipped2);
        state.EquippedWeapon = equipped;

        var list = ArtifactSystem.GetUnequippedTreasuryItems(state);

        Assert.Equal(2, list.Count);
        Assert.DoesNotContain(list, a => a.Id == equipped.Id);
    }

    [Fact]
    public void GetArtifactSlotArabic_TranslatesSlots()
    {
        Assert.Equal("سلاح", ArtifactSystem.GetArtifactSlotArabic(ArtifactSystem.SlotWeapon));
        Assert.Equal("رداء", ArtifactSystem.GetArtifactSlotArabic(ArtifactSystem.SlotRobe));
        Assert.Equal("كتاب", ArtifactSystem.GetArtifactSlotArabic(ArtifactSystem.SlotBook));
    }

    [Fact]
    public void GameState_Serialization_PreservesEquippedArtifacts()
    {
        var state = new GameState();
        state.EquippedWeapon = SeedArtifact("سيف", ArtifactSystem.SlotWeapon, ArtifactSystem.BuffMartial, 10);
        state.EquippedRobe = SeedArtifact("رداء", ArtifactSystem.SlotRobe, ArtifactSystem.BuffOpinion, 5);
        state.EquippedBook = SeedArtifact("كتاب", ArtifactSystem.SlotBook, ArtifactSystem.BuffPiety, 8);
        state.TreasuryInventory.Add(state.EquippedWeapon);
        state.TreasuryInventory.Add(state.EquippedRobe);
        state.TreasuryInventory.Add(state.EquippedBook);

        string json = System.Text.Json.JsonSerializer.Serialize(state);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.EquippedWeapon);
        Assert.NotNull(deserialized.EquippedRobe);
        Assert.NotNull(deserialized.EquippedBook);
        Assert.Equal(10, deserialized.EquippedWeapon.BuffValue);
        Assert.Equal(5, deserialized.EquippedRobe.BuffValue);
        Assert.Equal(8, deserialized.EquippedBook.BuffValue);
        Assert.Equal(3, deserialized.TreasuryInventory.Count);
    }

    [Fact]
    public void CombatSystem_AppliesWeaponAdvantageToAttacker()
    {
        var state = new GameState();
        state.EquippedWeapon = SeedArtifact("سيف", ArtifactSystem.SlotWeapon, ArtifactSystem.BuffMartial, 15);
        state.Time.Month = 6;
        state.Time.Day = 15;
        state.Armies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "r1", Name = "الجيش", TotalSoldiers = 1000, CurrentProvince = "وادي_الفرات", LeviesCount = 600, ArchersCount = 200, HeavyInfantryCount = 200, Morale = 100 }
        };
        state.EnemyArmies = new System.Collections.Generic.List<Army>
        {
            new Army { Id = "e1", Name = "العدو", TotalSoldiers = 500, CurrentProvince = "وادي_الفرات", LeviesCount = 300, ArchersCount = 100, HeavyInfantryCount = 100, Morale = 100 }
        };
        int found = 0;
        for (int i = 0; i < 60; i++)
        {
            state.EnemyArmies[0].TotalSoldiers = 500;
            var report = CombatSystem.ResolveMultiPhaseBattle(
                new CombatSystem.ArmyComposition { Levies = 600, Archers = 200, HeavyInfantry = 200, CommanderMartial = 8 },
                new CombatSystem.ArmyComposition { Levies = 300, Archers = 100, HeavyInfantry = 100, CommanderMartial = 5, TerrainDefenseBonus = 0 },
                attackerContext: state, defenderContext: null, attackerName: "الجيش", defenderName: "العدو");
            if (report != null && report.PhaseLogs.Any(l => l != null && l.Contains("أثر السلاح")))
            {
                found++;
                break;
            }
        }
        Assert.True(found >= 1);
    }
}
