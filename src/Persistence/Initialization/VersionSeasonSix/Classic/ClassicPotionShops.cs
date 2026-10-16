// <copyright file="ClassicPotionShops.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Configures the potion merchants of the classic profile, so that they sell clean (+0) stacks of 50.
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
    /// The numbers of the merchants which sell the standard potion assortment
    /// (all of them use the potion girl store of the default initialization).
    /// </summary>
    internal static readonly IReadOnlyCollection<short> PotionMerchantNumbers = new short[]
    {
        231, // Thompson the Merchant
        242, // Elf Lala
        245, // Izabel the Wizard
        253, // Potion Girl Amy
        259, // Oracle Layla
        376, // Pamela the Supplier
        377, // Angela the Supplier
        415, // Silvia
        545, // Christine the General Goods Merchant
        577, // Leina the General Goods Merchant
    };

    /// <summary>
    /// The potions which are sold, by store slot.
    /// </summary>
    private static readonly (byte Slot, byte Number)[] PotionsToSell =
    {
        (0, 0),  // Apple
        (1, 1),  // Small Healing Potion
        (2, 2),  // Medium Healing Potion
        (3, 3),  // Large Healing Potion
        (4, 4),  // Small Mana Potion
        (5, 5),  // Medium Mana Potion
        (6, 6),  // Large Mana Potion
        (7, 8),  // Antidote
        (8, 10), // Town Portal Scroll
    };

    /// <summary>
    /// Determines whether the item is one of the stackable standard potions or the town portal scroll.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><c>true</c>, if it's a standard potion or a town portal scroll.</returns>
    internal static bool IsStandardPotion(ItemDefinition? definition)
        => definition is { Group: PotionGroup, Number: 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 10 };

    /// <summary>
    /// Sets the stack size of the standard potion definitions and rebuilds the potion slots of all potion merchants.
    /// </summary>
    /// <param name="context">The context.</param>
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
        var potionSlots = PotionsToSell.Select(p => p.Slot).ToHashSet();
        var stores = gameConfiguration.Monsters
            .Where(m => PotionMerchantNumbers.Contains(m.Number))
            .Select(m => m.MerchantStore)
            .OfType<ItemStorage>();
        foreach (var store in stores)
        {
            // The original assortment has the potions (+0, +1, x1, x3) in the first rows. They're all replaced.
            var obsoleteItems = store.Items
                .Where(i => IsStandardPotion(i.Definition) || potionSlots.Contains(i.ItemSlot))
                .ToList();
            foreach (var item in obsoleteItems)
            {
                store.Items.Remove(item);
                removedItems.Add(item);
            }

            foreach (var (slot, number) in PotionsToSell)
            {
                if (gameConfiguration.Items.FirstOrDefault(i => i.Group == PotionGroup && i.Number == number) is { } definition)
                {
                    var item = context.CreateNew<Item>();
                    item.Definition = definition;
                    item.ItemSlot = slot;
                    item.Durability = StackSize;
                    item.Level = 0;
                    store.Items.Add(item);
                }
            }
        }

        return removedItems;
    }
}
