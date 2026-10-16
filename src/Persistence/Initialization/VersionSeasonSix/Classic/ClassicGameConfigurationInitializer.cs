// <copyright file="ClassicGameConfigurationInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Initializes the <see cref="GameConfiguration"/> for Season 6 Classic x500.
/// </summary>
public class ClassicGameConfigurationInitializer : GameConfigurationInitializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassicGameConfigurationInitializer"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public ClassicGameConfigurationInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        this.DisableForbiddenClasses();
        this.ApplyClassicItemDropRestrictions();
        this.AdjustBoxOfKundunDrops();
        this.AdjustMerchantStores();
        this.AddClassicTrainingSpots();
    }

    private void DisableForbiddenClasses()
    {
        var forbiddenClassNumbers = new HashSet<byte>
        {
            (byte)CharacterClassNumber.Summoner,
            (byte)CharacterClassNumber.BloodySummoner,
            (byte)CharacterClassNumber.DimensionMaster,
            (byte)CharacterClassNumber.RageFighter,
            (byte)CharacterClassNumber.FistMaster,
        };

        foreach (var characterClass in this.GameConfiguration.CharacterClasses)
        {
            if (forbiddenClassNumbers.Contains(characterClass.Number))
            {
                characterClass.CanGetCreated = false;
            }
        }
    }

    private void ApplyClassicItemDropRestrictions()
    {
        foreach (var item in this.GameConfiguration.Items)
        {
            // Disable drops for armors above Great Dragon / Dark Soul / Hurricane / Red Spirit / Dark Master (number >= 29)
            if (item.Group is >= 7 and <= 11 && item.Number >= 29)
            {
                item.DropsFromMonsters = false;
            }

            // Disable Rage Fighter weapons (Group 0, numbers 32-35)
            if (item.Group == 0 && item.Number is >= 32 and <= 35)
            {
                item.DropsFromMonsters = false;
            }

            // Disable Summoner weapons (Group 5, numbers 14-23)
            if (item.Group == 5 && item.Number is >= 14 and <= 23)
            {
                item.DropsFromMonsters = false;
            }

            // Disable Level 3 Wings (Group 12, numbers 36-43)
            if (item.Group == 12 && item.Number is >= 36 and <= 43)
            {
                item.DropsFromMonsters = false;
            }

            // Disable Jewel of Harmony, Gemstone, and Refine Stones (Group 14, numbers 41-44)
            if (item.Group == 14 && item.Number is >= 41 and <= 44)
            {
                item.DropsFromMonsters = false;
            }
        }

        // Adjust default jewel drop group chance for balanced Medium/Fast rate (2%)
        var jewelDropId = GuidHelper.CreateGuid<DropItemGroup>(4);
        if (this.GameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == jewelDropId) is { } jewelGroup)
        {
            jewelGroup.Chance = 0.02;
        }

        // Adjust random excellent drop rate (0.2%)
        var excDropId = GuidHelper.CreateGuid<DropItemGroup>(3);
        if (this.GameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == excDropId) is { } excGroup)
        {
            excGroup.Chance = 0.002;
        }
    }

    private void AdjustBoxOfKundunDrops()
    {
        var boxOfLuck = this.GameConfiguration.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 11);
        if (boxOfLuck is null)
        {
            return;
        }

        foreach (var dropGroup in boxOfLuck.DropItems)
        {
            // Box of Kundun +4 (SourceItemLevel 11)
            if (dropGroup.SourceItemLevel == 11)
            {
                var itemsToRemove = dropGroup.PossibleItems
                    .Where(i => (i.Group is >= 7 and <= 11 && i.Number >= 29) || (i.Group == 0 && i.Number >= 32) || (i.Group == 5 && i.Number >= 14))
                    .ToList();
                foreach (var item in itemsToRemove)
                {
                    dropGroup.PossibleItems.Remove(item);
                }
            }

            // Box of Kundun +5 (SourceItemLevel 12)
            if (dropGroup.SourceItemLevel == 12)
            {
                var itemsToRemove = dropGroup.PossibleItems
                    .Where(i => (i.Group is >= 7 and <= 11 && i.Number >= 29) || (i.Group == 0 && i.Number >= 32) || (i.Group == 5 && i.Number >= 14))
                    .ToList();
                foreach (var item in itemsToRemove)
                {
                    dropGroup.PossibleItems.Remove(item);
                }
            }
        }
    }

    private void AdjustMerchantStores()
    {
        // 1. Set max stack durability to 50 for standard potions and scrolls
        foreach (var potionDef in this.GameConfiguration.Items.Where(i => i.Group == 14 && (i.Number is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 10)))
        {
            potionDef.Durability = 50;
            potionDef.MaximumItemLevel = 0;
        }

        // 2. Clean potion shops (Amy 253, Elf Lala 242, Izabel 245, Thompson 231, Pamela 376, Angela 377, etc.)
        var potionsToSell = new (byte Slot, byte Number)[]
        {
            (0, 0),  // Apple +0 x50
            (1, 1),  // Small Healing Potion +0 x50
            (2, 2),  // Medium Healing Potion +0 x50
            (3, 3),  // Large Healing Potion +0 x50
            (4, 4),  // Small Mana Potion +0 x50
            (5, 5),  // Medium Mana Potion +0 x50
            (6, 6),  // Large Mana Potion +0 x50
            (7, 8),  // Antidote +0 x50
            (8, 10), // Town Portal Scroll +0 x50
        };

        var potionNpcNumbers = new short[] { 253, 242, 245, 231, 259, 376, 377, 541, 543 };

        var targetMonsters = this.GameConfiguration.Monsters
            .Where(m => m.MerchantStore is not null && (potionNpcNumbers.Contains(m.Number) || m.MerchantStore.Items.Any(i => i.Definition?.Group == 14 && (i.Definition.Number is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 10))))
            .ToList();

        var slotsToUse = potionsToSell.Select(p => p.Slot).ToHashSet();

        foreach (var monster in targetMonsters)
        {
            var store = monster.MerchantStore!;
            var oldPotions = store.Items
                .Where(i => i.Definition?.Group == 14 && (i.Definition.Number is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 10))
                .ToList();

            foreach (var item in oldPotions)
            {
                store.Items.Remove(item);
            }

            var conflictingItems = store.Items.Where(i => slotsToUse.Contains(i.ItemSlot)).ToList();
            foreach (var item in conflictingItems)
            {
                store.Items.Remove(item);
            }

            foreach (var (slot, number) in potionsToSell)
            {
                if (this.GameConfiguration.Items.FirstOrDefault(i => i.Group == 14 && i.Number == number) is { } def)
                {
                    var item = this.Context.CreateNew<Item>();
                    item.Definition = def;
                    item.ItemSlot = slot;
                    item.Durability = 50;
                    item.Level = 0;
                    store.Items.Add(item);
                }
            }
        }
    }

    private void AddClassicTrainingSpots()
    {
        var spotDefinitions = new[]
        {
            // Map 0: Lorencia
            new SpotConfig(0, 3, 184, 187, 124, 127, 6),  // Spiders at East Gate
            new SpotConfig(0, 0, 128, 132, 178, 182, 6),  // Bull Fighters at South Gate
            new SpotConfig(0, 2, 174, 178, 163, 167, 6),  // Giants at East Field
            new SpotConfig(0, 14, 165, 170, 200, 205, 6), // Skeletons at North-East

            // Map 2: Devias
            new SpotConfig(2, 19, 22, 26, 22, 26, 6),       // Yetis at Devias 2
            new SpotConfig(2, 20, 220, 224, 220, 224, 6),   // Elite Yetis at Devias 3

            // Map 4: Lost Tower
            new SpotConfig(4, 40, 8, 12, 85, 89, 6),        // Death Knights at Lost Tower 7
            new SpotConfig(4, 38, 40, 44, 90, 94, 6),       // Death Cows at Lost Tower 7

            // Map 8: Tarkan
            new SpotConfig(8, 57, 160, 164, 150, 154, 6),   // Mutants at Tarkan 1
            new SpotConfig(8, 59, 75, 79, 165, 169, 6),     // Iron Wheels at Tarkan 2

            // Map 10: Icarus
            new SpotConfig(10, 71, 40, 44, 50, 54, 6),      // Queen Rainer at Icarus
            new SpotConfig(10, 72, 60, 64, 80, 84, 6),      // Drakan at Icarus
        };

        short spawnCounter = 1000;
        foreach (var spot in spotDefinitions)
        {
            if (this.GameConfiguration.Maps.FirstOrDefault(m => m.Number == spot.MapNumber) is { } map
                && this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == spot.MonsterNumber) is { } monster)
            {
                var area = this.Context.CreateNew<MonsterSpawnArea>();
                area.SetGuid((short)spot.MapNumber, spawnCounter++);
                area.GameMap = map;
                area.MonsterDefinition = monster;
                area.Quantity = spot.Quantity;
                area.X1 = spot.X1;
                area.X2 = spot.X2;
                area.Y1 = spot.Y1;
                area.Y2 = spot.Y2;
                area.SpawnTrigger = SpawnTrigger.Automatic;
                map.MonsterSpawns.Add(area);
            }
        }
    }

    private sealed record SpotConfig(short MapNumber, short MonsterNumber, byte X1, byte X2, byte Y1, byte Y2, short Quantity);
}
