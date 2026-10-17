// <copyright file="ClassicPotionShops.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// Configures the merchant stores of the classic x500 profile:
/// - Potions and Town Portal Scrolls stack to 50 (+0)
/// - Basic pets (Angel, Imp, Uniria) in potion shops and barmaids
/// - Leveling skills (orbs/scrolls for DW, DK, Elf, MG, DL) in Pasi, Elf Lala, Izabel and Marce
/// - Pre-crafted Blood Castle (+1..+8) and Devil Square (+1..+7) tickets + Level 150/220 quest items in Lumen and Caren
/// - DL (Light Plate, Adamantine, Scepters, Cross Shield) and MG (Storm Crow, Rune Blade) gear in Alex, Rhea, Zienna and Hanzo, removing Summoner items.
/// </summary>
internal static class ClassicPotionShops
{
    /// <summary>
    /// The maximum stack size of potions and town portal scrolls.
    /// </summary>
    internal const byte StackSize = 50;

    /// <summary>
    /// The item group of potions, scrolls and other consumables.
    /// </summary>
    internal const byte PotionGroup = 14;

    /// <summary>
    /// The numbers of the general-goods merchants which sell the standard potion assortment.
    /// </summary>
    internal static readonly IReadOnlyCollection<short> GeneralPotionMerchantNumbers = new short[]
    {
        231, // Thompson the Merchant
        253, // Potion Girl Amy
        259, // Oracle Layla
        376, // Pamela the Supplier
        377, // Angela the Supplier
        415, // Silvia
        545, // Christine the General Goods Merchant
        577, // Leina the General Goods Merchant
    };

    /// <summary>
    /// All merchant numbers managed by the classic shop configuration.
    /// </summary>
    internal static readonly IReadOnlyCollection<short> AllManagedMerchantNumbers = new short[]
    {
        230, // Alex (Lorencia)
        231, // Thompson the Merchant
        242, // Elf Lala (Noria)
        244, // Caren the Barmaid (Devias)
        245, // Izabel the Wizard (Devias)
        246, // Zienna the Weapons Merchant (Devias)
        251, // Hanzo the Blacksmith (Lorencia)
        253, // Potion Girl Amy (Lorencia)
        254, // Pasi the Mage (Lorencia)
        255, // Lumen the Barmaid (Lorencia)
        259, // Oracle Layla (Kalima)
        376, // Pamela the Supplier
        377, // Angela the Supplier
        415, // Silvia (Elbeland)
        416, // Rhea (Elbeland)
        417, // Marce (Elbeland)
        545, // Christine (Loren Market)
        577, // Leina (Karutan)
    };

    private const int StoreRows = 15;
    private const int StoreColumns = 8;

    /// <summary>
    /// The standard potions which are sold in stacks of 50.
    /// </summary>
    private static readonly byte[] StandardPotionNumbers =
    {
        0,  // Apple
        1,  // Small Healing Potion
        2,  // Medium Healing Potion
        3,  // Large Healing Potion
        4,  // Small Mana Potion
        5,  // Medium Mana Potion
        6,  // Large Mana Potion
        8,  // Antidote
        10, // Town Portal Scroll
    };

    /// <summary>
    /// Determines whether the item is one of the stackable standard potions or the town portal scroll.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><c>true</c>, if it's a standard potion or a town portal scroll.</returns>
    internal static bool IsStandardPotion(ItemDefinition? definition)
        => definition is { Group: PotionGroup, Number: 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 10 };

    /// <summary>
    /// Applies the classic x500 potion stack sizes and rebuilds all managed merchant stores.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The items which were removed from the stores and need to be deleted by the caller.</returns>
    internal static IReadOnlyList<Item> Apply(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var definition in gameConfiguration.Items.Where(IsStandardPotion))
        {
            definition.Durability = StackSize;
            definition.MaximumItemLevel = 0;
        }

        var removedItems = new List<Item>();

        foreach (var npcNumber in GeneralPotionMerchantNumbers)
        {
            RebuildStore(context, gameConfiguration, npcNumber, removedItems, BuildGeneralPotionStoreItems);
        }

        RebuildStore(context, gameConfiguration, 242, removedItems, BuildElfLalaStoreItems);
        RebuildStore(context, gameConfiguration, 245, removedItems, BuildIzabelStoreItems);
        RebuildStore(context, gameConfiguration, 254, removedItems, BuildPasiStoreItems);
        RebuildStore(context, gameConfiguration, 255, removedItems, BuildBarmaidStoreItems);
        RebuildStore(context, gameConfiguration, 244, removedItems, BuildBarmaidStoreItems);
        RebuildStore(context, gameConfiguration, 251, removedItems, BuildHanzoStoreItems);
        RebuildStore(context, gameConfiguration, 246, removedItems, BuildZiennaStoreItems);
        RebuildStore(context, gameConfiguration, 230, removedItems, BuildDlMgArmorAndWeaponStoreItems);
        RebuildStore(context, gameConfiguration, 416, removedItems, BuildDlMgArmorAndWeaponStoreItems);
        RebuildStore(context, gameConfiguration, 417, removedItems, BuildMarceStoreItems);

        return removedItems;
    }

    private static void RebuildStore(
        IContext context,
        GameConfiguration gameConfiguration,
        short npcNumber,
        List<Item> removedItems,
        Func<IContext, GameConfiguration, IEnumerable<Item>> itemFactory)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == npcNumber)?.MerchantStore is not { } store)
        {
            return;
        }

        foreach (var existing in store.Items.ToList())
        {
            store.Items.Remove(existing);
            removedItems.Add(existing);
        }

        var grid = new bool[StoreRows, StoreColumns];
        foreach (var item in itemFactory(context, gameConfiguration))
        {
            PlaceInFirstFreeSlot(store, grid, item, npcNumber);
        }
    }

    private static IEnumerable<Item> BuildGeneralPotionStoreItems(IContext context, GameConfiguration config)
    {
        foreach (var potion in CreateStandardPotionPackItems(context, config))
        {
            yield return potion;
        }

        foreach (var pet in CreateBasicPets(context, config))
        {
            yield return pet;
        }

        foreach (var ammo in CreateArrowsAndBolts(context, config))
        {
            yield return ammo;
        }

        if (CreateMiscItem(context, config, 13, 29, 1, 0) is { } guardsman)
        {
            yield return guardsman;
        }
    }

    private static IEnumerable<Item> BuildElfLalaStoreItems(IContext context, GameConfiguration config)
    {
        foreach (var potion in CreateStandardPotionPackItems(context, config))
        {
            yield return potion;
        }

        foreach (var pet in CreateBasicPets(context, config))
        {
            yield return pet;
        }

        foreach (var ammo in CreateArrowsAndBolts(context, config))
        {
            yield return ammo;
        }

        // Elf Orbs: Heal (8), Greater Defense (9), Greater Damage (10), Penetration (17), Ice Arrow (18)
        byte[] elfOrbNumbers = [8, 9, 10, 17, 18];
        foreach (var orbNum in elfOrbNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Orbs, orbNum, 1, 0) is { } orb)
            {
                yield return orb;
            }
        }

        // Summon Orbs (Group 12, Number 11, Levels 0..4: Goblin, Stone Golem, Assassin, Elite Yeti, Dark Knight)
        for (byte level = 0; level <= 4; level++)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Orbs, 11, 1, level) is { } summonOrb)
            {
                yield return summonOrb;
            }
        }

        // Elf Sets: Vine (10, +2+4+L), Silk (11, +2+4+L), Wind (12, +3+4+L)
        foreach (var item in CreateFullArmorSet(context, config, 10, 2))
        {
            yield return item;
        }

        foreach (var item in CreateFullArmorSet(context, config, 11, 2))
        {
            yield return item;
        }

        foreach (var item in CreateFullArmorSet(context, config, 12, 3))
        {
            yield return item;
        }

        if (CreateMiscItem(context, config, 13, 29, 1, 0) is { } guardsman)
        {
            yield return guardsman;
        }
    }

    private static IEnumerable<Item> BuildIzabelStoreItems(IContext context, GameConfiguration config)
    {
        foreach (var potion in CreateStandardPotionPackItems(context, config))
        {
            yield return potion;
        }

        foreach (var pet in CreateBasicPets(context, config))
        {
            yield return pet;
        }

        foreach (var ammo in CreateArrowsAndBolts(context, config))
        {
            yield return ammo;
        }

        // DW/MG Advanced Leveling Scrolls: Flame (4), Twister (7), Evil Spirit (8), Hellfire (9), Aqua Beam (11), Cometfall (12), Inferno (13), Soul Barrier (15)
        byte[] scrollNumbers = [4, 7, 8, 9, 11, 12, 13, 15];
        foreach (var scrollNum in scrollNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Scrolls, scrollNum, 1, 0) is { } scroll)
            {
                yield return scroll;
            }
        }

        // DL Scrolls: FireBurst (21), Critical Damage (23)
        byte[] dlScrollNumbers = [21, 23];
        foreach (var dlScrollNum in dlScrollNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Orbs, dlScrollNum, 1, 0) is { } dlScroll)
            {
                yield return dlScroll;
            }
        }

        // Legendary Set (3, +3+4+L) + Staffs + Legendary Shield
        foreach (var item in CreateFullArmorSet(context, config, 3, 3))
        {
            yield return item;
        }

        if (CreateEquippable(context, config, ItemGroups.Staff, 3, 3, 1, true, false) is { } thunderStaff)
        {
            yield return thunderStaff;
        }

        if (CreateEquippable(context, config, ItemGroups.Staff, 4, 3, 1, true, false) is { } gorgonStaff)
        {
            yield return gorgonStaff;
        }

        if (CreateEquippable(context, config, ItemGroups.Staff, 5, 3, 1, true, false) is { } legendaryStaff)
        {
            yield return legendaryStaff;
        }

        if (CreateEquippable(context, config, ItemGroups.Shields, 14, 3, 2, true, false) is { } legendaryShield)
        {
            yield return legendaryShield;
        }

        if (CreateMiscItem(context, config, 13, 29, 1, 0) is { } guardsman)
        {
            yield return guardsman;
        }
    }

    private static IEnumerable<Item> BuildPasiStoreItems(IContext context, GameConfiguration config)
    {
        // DW/MG Core & Leveling Scrolls in Lorencia: Fire Ball (3), Power Wave (10), Lightning (2), Meteorite (1), Teleport (5), Ice (6), Poison (0), Evil Spirit (8)
        // (Higher-tier scrolls Flame, Twister, Hellfire, Aqua Beam, Cometfall, Inferno, Soul Barrier are in Izabel in Devias and Marce in Elbeland)
        byte[] scrollNumbers = [3, 10, 2, 1, 5, 6, 0, 8];
        foreach (var scrollNum in scrollNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Scrolls, scrollNum, 1, 0) is { } scroll)
            {
                yield return scroll;
            }
        }

        // DL Scrolls: FireBurst (21), Critical Damage (23) (2 scrolls + 8 DW scrolls = 10 1x2 scrolls -> 2.5 clean rows)
        byte[] dlScrollNumbers = [21, 23];
        foreach (var dlScrollNum in dlScrollNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Orbs, dlScrollNum, 1, 0) is { } dlScroll)
            {
                yield return dlScroll;
            }
        }

        // DK/MG Orbs: Twisting Slash (7), Impale (13), Greater Fortitude (14), Rageful Blow (12), Fire Slash (16)
        byte[] orbNumbers = [7, 13, 14, 12, 16];
        foreach (var orbNum in orbNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Orbs, orbNum, 1, 0) is { } orb)
            {
                yield return orb;
            }
        }

        // DW Sets: Pad (2, +0+4+L), Bone (4, +2+4+L), Sphinx (7, +3+4+L)
        foreach (var item in CreateFullArmorSet(context, config, 2, 0))
        {
            yield return item;
        }

        foreach (var item in CreateFullArmorSet(context, config, 4, 2))
        {
            yield return item;
        }

        foreach (var item in CreateFullArmorSet(context, config, 7, 3))
        {
            yield return item;
        }

        // DW Starter Staffs: Skull (0, +0+4+L), Angelic (1, +2+4+L), Serpent (2, +3+4+L)
        if (CreateEquippable(context, config, ItemGroups.Staff, 0, 0, 1, true, false) is { } skullStaff)
        {
            yield return skullStaff;
        }

        if (CreateEquippable(context, config, ItemGroups.Staff, 1, 2, 1, true, false) is { } angelicStaff)
        {
            yield return angelicStaff;
        }

        if (CreateEquippable(context, config, ItemGroups.Staff, 2, 3, 1, true, false) is { } serpentStaff)
        {
            yield return serpentStaff;
        }
    }

    private static IEnumerable<Item> BuildBarmaidStoreItems(IContext context, GameConfiguration config)
    {
        // Ale + Town Portal Scroll (x50) + Armor of Guardsman + Basic Pets
        if (CreateMiscItem(context, config, PotionGroup, 9, 1, 0) is { } ale)
        {
            yield return ale;
        }

        if (CreateMiscItem(context, config, PotionGroup, 10, StackSize, 0) is { } townPortal)
        {
            yield return townPortal;
        }

        foreach (var pet in CreateBasicPets(context, config))
        {
            yield return pet;
        }

        if (CreateMiscItem(context, config, 13, 29, 1, 0) is { } guardsman)
        {
            yield return guardsman;
        }

        // Level 150 Quest Items (Sebina): Scroll of Emperor (23, +0), Broken Sword (24, +0), Tear of Elf (25, +0), Soul Shard of Wizard (26, +0)
        // Level 220 Quest Items (Marlon): Ring of Honor (23, +1), Dark Stone (24, +1)
        if (CreateMiscItem(context, config, PotionGroup, 23, 1, 0) is { } scrollOfEmperor)
        {
            yield return scrollOfEmperor;
        }

        if (CreateMiscItem(context, config, PotionGroup, 24, 1, 0) is { } brokenSword)
        {
            yield return brokenSword;
        }

        if (CreateMiscItem(context, config, PotionGroup, 25, 1, 0) is { } tearOfElf)
        {
            yield return tearOfElf;
        }

        if (CreateMiscItem(context, config, PotionGroup, 26, 1, 0) is { } soulOfWizard)
        {
            yield return soulOfWizard;
        }

        if (CreateMiscItem(context, config, PotionGroup, 23, 1, 1) is { } ringOfHonor)
        {
            yield return ringOfHonor;
        }

        if (CreateMiscItem(context, config, PotionGroup, 24, 1, 1) is { } darkStone)
        {
            yield return darkStone;
        }

        // Pre-crafted Devil's Invitation +1..+7 (Group 14, Number 19)
        for (byte level = 1; level <= 7; level++)
        {
            if (CreateMiscItem(context, config, PotionGroup, 19, 1, level) is { } devilsInvitation)
            {
                yield return devilsInvitation;
            }
        }

        // Pre-crafted Invisibility Cloak +1..+8 (Group 13, Number 18)
        for (byte level = 1; level <= 8; level++)
        {
            if (CreateMiscItem(context, config, 13, 18, 1, level) is { } cloak)
            {
                yield return cloak;
            }
        }
    }

    private static IEnumerable<Item> BuildHanzoStoreItems(IContext context, GameConfiguration config)
    {
        // Shields
        (byte Number, bool Skill, byte Level)[] shields =
        [
            (0, false, 0),  // Small Shield
            (4, true, 1),   // Buckler
            (1, false, 2),  // Horn Shield
            (2, false, 3),  // Kite Shield
            (6, true, 3),   // Skull Shield
            (10, true, 3),  // Big Round Shield
            (9, true, 3),   // Plate Shield
            (7, true, 3),   // Spiked Shield
            (5, true, 3),   // Dragon Slayer Shield
            (8, true, 3),   // Tower Shield
            (11, true, 3),  // Serpent Shield
            (12, true, 3),  // Bronze Shield
        ];
        foreach (var (num, skill, lvl) in shields)
        {
            if (CreateEquippable(context, config, ItemGroups.Shields, num, lvl, 1, true, skill) is { } shield)
            {
                yield return shield;
            }
        }

        // Weapons (fixing the Gladius/Falchion slot 73 collision and adding Battle Scepter for DL)
        (ItemGroups Group, byte Number, byte Level, bool Skill)[] weapons =
        [
            (ItemGroups.Swords, 1, 0, false),   // Short Sword
            (ItemGroups.Axes, 1, 1, false),     // Hand Axe
            (ItemGroups.Scepters, 0, 2, false), // Mace
            (ItemGroups.Swords, 2, 2, false),   // Rapier
            (ItemGroups.Axes, 2, 2, false),     // Double Axe
            (ItemGroups.Swords, 4, 3, true),    // Sword of Assassin
            (ItemGroups.Scepters, 1, 3, true),  // Morning Star
            (ItemGroups.Axes, 3, 3, true),      // Tomahawk
            (ItemGroups.Swords, 0, 2, false),   // Kris
            (ItemGroups.Swords, 6, 3, true),    // Gladius
            (ItemGroups.Swords, 7, 3, true),    // Falchion
            (ItemGroups.Swords, 8, 3, false),   // Serpent Sword
            (ItemGroups.Swords, 5, 3, true),    // Blade
            (ItemGroups.Scepters, 8, 2, true),  // Battle Scepter (DL)
        ];
        foreach (var (group, num, lvl, skill) in weapons)
        {
            if (CreateEquippable(context, config, group, num, lvl, 1, true, skill) is { } weapon)
            {
                yield return weapon;
            }
        }
    }

    private static IEnumerable<Item> BuildZiennaStoreItems(IContext context, GameConfiguration config)
    {
        // Dragon Set (1, +3+4+L)
        foreach (var item in CreateFullArmorSet(context, config, 1, 3))
        {
            yield return item;
        }

        // Place 2-wide weapons first so they align cleanly on even columns, then 1-wide weapons
        (ItemGroups Group, byte Number, bool Skill)[] weapons =
        [
            (ItemGroups.Swords, 9, true),    // Sword of Salamander (2x3)
            (ItemGroups.Swords, 11, true),   // Legendary Sword (2x3)
            (ItemGroups.Swords, 15, true),   // Giant Sword (2x3)
            (ItemGroups.Swords, 12, true),   // Heliacal Sword (2x3)
            (ItemGroups.Bows, 12, true),     // Serpent Crossbow (2x3)
            (ItemGroups.Bows, 13, true),     // Bluewing Crossbow (2x3)
            (ItemGroups.Swords, 31, true),   // Rune Blade (MG, 2x4)
            (ItemGroups.Spears, 9, true),    // Bill of Balrog (2x4)
            (ItemGroups.Spears, 8, true),    // Great Scythe (2x4)
            (ItemGroups.Bows, 5, true),      // Silver Bow (2x4)
            (ItemGroups.Scepters, 10, true), // Great Scepter (DL, 1x4)
            (ItemGroups.Scepters, 8, true),  // Battle Scepter (DL, 1x3)
            (ItemGroups.Scepters, 9, true),  // Master Scepter (DL, 1x3)
            (ItemGroups.Swords, 13, true),   // Double Blade (1x3)
            (ItemGroups.Swords, 14, true),   // Lightning Sword (1x3)
        ];
        foreach (var (group, num, skill) in weapons)
        {
            if (CreateEquippable(context, config, group, num, 3, 1, true, skill) is { } weapon)
            {
                yield return weapon;
            }
        }
    }

    private static IEnumerable<Item> BuildDlMgArmorAndWeaponStoreItems(IContext context, GameConfiguration config)
    {
        // DL Sets: Light Plate (25, +2+4+L) & Adamantine (26, +3+4+L)
        foreach (var item in CreateFullArmorSet(context, config, 25, 2))
        {
            yield return item;
        }

        foreach (var item in CreateFullArmorSet(context, config, 26, 3))
        {
            yield return item;
        }

        // MG Set: Storm Crow (15, +3+4+L - Armor, Pants, Gloves, Boots)
        foreach (var item in CreateFullArmorSet(context, config, 15, 3))
        {
            yield return item;
        }

        // DL Shield: Cross Shield (21, +3+4+L+S)
        if (CreateEquippable(context, config, ItemGroups.Shields, 21, 3, 1, true, true) is { } crossShield)
        {
            yield return crossShield;
        }

        // DL / MG / DK Weapons (+3+4+L+S) - 2-wide first, then 1x4, then 1x3
        (ItemGroups Group, byte Number, bool Skill)[] weapons =
        [
            (ItemGroups.Swords, 31, true),   // Rune Blade (MG, 2x4)
            (ItemGroups.Swords, 10, true),   // Light Saber (2x4)
            (ItemGroups.Scepters, 3, true),  // Great Hammer (2x3)
            (ItemGroups.Scepters, 10, true), // Great Scepter (DL, 1x4)
            (ItemGroups.Scepters, 8, true),  // Battle Scepter (DL, 1x3)
            (ItemGroups.Scepters, 9, true),  // Master Scepter (DL, 1x3)
        ];
        foreach (var (group, num, skill) in weapons)
        {
            if (CreateEquippable(context, config, group, num, 3, 1, true, skill) is { } weapon)
            {
                yield return weapon;
            }
        }
    }

    private static IEnumerable<Item> BuildMarceStoreItems(IContext context, GameConfiguration config)
    {
        // Classic Scrolls + DL Scrolls + DK/Elf Orbs (replaces Summoner Drain Life Parchment)
        byte[] scrollNumbers = [3, 10, 2, 1, 5, 6, 0, 4, 7, 8, 9, 11, 12, 13, 15];
        foreach (var scrollNum in scrollNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Scrolls, scrollNum, 1, 0) is { } scroll)
            {
                yield return scroll;
            }
        }

        byte[] orbNumbers = [7, 13, 14, 12, 16, 8, 9, 10, 17, 18, 21, 23];
        foreach (var orbNum in orbNumbers)
        {
            if (CreateMiscItem(context, config, (byte)ItemGroups.Orbs, orbNum, 1, 0) is { } orb)
            {
                yield return orb;
            }
        }
    }

    private static IEnumerable<Item> CreateStandardPotionPackItems(IContext context, GameConfiguration config)
    {
        foreach (var number in StandardPotionNumbers)
        {
            if (CreateMiscItem(context, config, PotionGroup, number, StackSize, 0) is { } potion)
            {
                yield return potion;
            }
        }
    }

    private static IEnumerable<Item> CreateBasicPets(IContext context, GameConfiguration config)
    {
        // Guardian Angel (0), Imp (1), Horn of Uniria (2) in Group 13
        for (byte number = 0; number <= 2; number++)
        {
            if (config.Items.FirstOrDefault(i => i.Group == 13 && i.Number == number) is { } definition)
            {
                var pet = context.CreateNew<Item>();
                pet.Definition = definition;
                pet.Durability = definition.Durability > 0 ? definition.Durability : (byte)255;
                pet.Level = 0;
                yield return pet;
            }
        }
    }

    private static IEnumerable<Item> CreateArrowsAndBolts(IContext context, GameConfiguration config)
    {
        for (byte level = 0; level <= 2; level++)
        {
            if (CreateEquippable(context, config, ItemGroups.Bows, 7, level, 0, false, false) is { } bolt)
            {
                yield return bolt;
            }
        }

        for (byte level = 0; level <= 2; level++)
        {
            if (CreateEquippable(context, config, ItemGroups.Bows, 15, level, 0, false, false) is { } arrow)
            {
                yield return arrow;
            }
        }
    }

    private static IEnumerable<Item> CreateFullArmorSet(IContext context, GameConfiguration config, byte setNumber, byte level)
    {
        ItemGroups[] armorGroups = [ItemGroups.Helm, ItemGroups.Armor, ItemGroups.Pants, ItemGroups.Gloves, ItemGroups.Boots];
        foreach (var group in armorGroups)
        {
            if (CreateEquippable(context, config, group, setNumber, level, 1, true, false) is { } piece)
            {
                yield return piece;
            }
        }
    }

    private static Item? CreateMiscItem(IContext context, GameConfiguration config, byte group, byte number, byte durability, byte level)
    {
        if (config.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is not { } definition)
        {
            return null;
        }

        var item = context.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = durability;
        item.Level = level;
        return item;
    }

    private static Item? CreateEquippable(
        IContext context,
        GameConfiguration config,
        ItemGroups group,
        byte number,
        byte level,
        byte optionLevel,
        bool luck,
        bool skill)
    {
        if (config.Items.FirstOrDefault(i => i.Group == (byte)group && i.Number == number) is not { } definition)
        {
            return null;
        }

        var item = context.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = definition.Durability;
        item.Level = level;
        item.HasSkill = skill && definition.Skill is not null;

        if (optionLevel > 0
            && definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).FirstOrDefault(o => o.OptionType == ItemOptionTypes.Option) is { } optionDef)
        {
            var optionLink = context.CreateNew<ItemOptionLink>();
            optionLink.ItemOption = optionDef;
            optionLink.Level = optionLevel;
            item.ItemOptions.Add(optionLink);
        }

        if (luck
            && definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).FirstOrDefault(o => o.OptionType == ItemOptionTypes.Luck) is { } luckDef)
        {
            var optionLink = context.CreateNew<ItemOptionLink>();
            optionLink.ItemOption = luckDef;
            item.ItemOptions.Add(optionLink);
        }

        return item;
    }

    private static void PlaceInFirstFreeSlot(ItemStorage store, bool[,] grid, Item item, short npcNumber)
    {
        var width = Math.Max((byte)1, item.Definition?.Width ?? 1);
        var height = Math.Max((byte)1, item.Definition?.Height ?? 1);

        for (var row = 0; row <= StoreRows - height; row++)
        {
            for (var col = 0; col <= StoreColumns - width; col++)
            {
                if (!CanFit(grid, row, col, width, height))
                {
                    continue;
                }

                for (var r = row; r < row + height; r++)
                {
                    for (var c = col; c < col + width; c++)
                    {
                        grid[r, c] = true;
                    }
                }

                item.ItemSlot = (byte)((row * StoreColumns) + col);
                store.Items.Add(item);
                return;
            }
        }

        throw new InvalidOperationException($"Item '{item.Definition?.Name}' ({width}x{height}) did not fit in merchant #{npcNumber} store.");
    }

    private static bool CanFit(bool[,] grid, int row, int col, int width, int height)
    {
        for (var r = row; r < row + height; r++)
        {
            for (var c = col; c < col + width; c++)
            {
                if (grid[r, c])
                {
                    return false;
                }
            }
        }

        return true;
    }
}
