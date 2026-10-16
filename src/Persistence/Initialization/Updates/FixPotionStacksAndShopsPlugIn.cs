// <copyright file="FixPotionStacksAndShopsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates the maximum durability of potions and portal scrolls to 50 and cleans up potion shops.
/// </summary>
public abstract class FixPotionStacksAndShopsPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Fix Potion Stacks to 50 & Clean Potion Shops";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Updates the maximum durability of potions and portal scrolls to 50, and organizes neat 50-stack shops in Lorencia, Devias, and Noria.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override int Version => 2;

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 10, 16, 12, 45, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        // 1. Set max stack durability to 50 for standard potions and scrolls
        foreach (var potionDef in gameConfiguration.Items.Where(i => i.Group == 14 && (i.Number is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 10)))
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

        var targetMonsters = gameConfiguration.Monsters
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
                await context.DeleteAsync(item).ConfigureAwait(false);
            }

            var conflictingItems = store.Items.Where(i => slotsToUse.Contains(i.ItemSlot)).ToList();
            foreach (var item in conflictingItems)
            {
                store.Items.Remove(item);
                await context.DeleteAsync(item).ConfigureAwait(false);
            }

            foreach (var (slot, number) in potionsToSell)
            {
                if (gameConfiguration.Items.FirstOrDefault(i => i.Group == 14 && i.Number == number) is { } def)
                {
                    var item = context.CreateNew<Item>();
                    item.Definition = def;
                    item.ItemSlot = slot;
                    item.Durability = 50;
                    item.Level = 0;
                    store.Items.Add(item);
                }
            }
        }
    }

    /// <summary>
    /// The update plugin for Season 6 Classic.
    /// </summary>
    [PlugIn]
    [Guid("8180D2A9-C3FF-4490-80B9-489382CB7247")]
    public class Season6Classic : FixPotionStacksAndShopsPlugIn
    {
        /// <inheritdoc />
        public override string DataInitializationKey => ClassicSeasonSixDataInitialization.Id;
    }

    /// <summary>
    /// The update plugin for standard Season 6.
    /// </summary>
    [PlugIn]
    [Guid("34C06EF6-3C75-40AE-B58F-24289D1D7B9E")]
    public class Season6 : FixPotionStacksAndShopsPlugIn
    {
        /// <inheritdoc />
        public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
    }
}
