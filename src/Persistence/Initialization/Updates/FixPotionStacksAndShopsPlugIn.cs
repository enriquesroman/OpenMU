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
/// <remarks>
/// Version 1 and 2 of this update used wrong merchant numbers, respectively a heuristic which also matched
/// non-potion merchants. They placed potions over the original items of Pasi the Mage, Lumen the Barmaid
/// and Caren the Barmaid. Version 3 restores these items.
/// The merchant stores are cached by a running server and their items are not covered by the
/// configuration change propagation, so the server process needs to be restarted afterwards.
/// </remarks>
public abstract class FixPotionStacksAndShopsPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Fix Potion Stacks to 50 & Clean Potion Shops";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Potions and portal scrolls stack up to 50 and potion merchants sell clean +0 stacks of 50. Restores the shops of Pasi, Lumen and Caren which were changed by previous versions. Requires a restart of the server process.";

    private const byte ScrollGroup = 15;
    private const byte OrbGroup = 12;
    private const byte MiscGroup = 13;

    /// <summary>
    /// The original items of the merchants which were changed by previous versions of this update, by merchant number.
    /// </summary>
    private static readonly Dictionary<short, (byte Slot, byte Group, byte Number, byte Level)[]> RestoredItems = new()
    {
        [254] = // Pasi the Mage
        [
            (0, ScrollGroup, 3, 0),  // Scroll of Fire Ball
            (1, ScrollGroup, 10, 0), // Scroll of Power Wave
            (2, ScrollGroup, 2, 0),  // Scroll of Lighting
            (3, ScrollGroup, 1, 0),  // Scroll of Meteorite
            (4, ScrollGroup, 5, 0),  // Scroll of Teleport
            (5, ScrollGroup, 6, 0),  // Scroll of Ice
            (6, ScrollGroup, 0, 0),  // Scroll of Poison
            (7, OrbGroup, 13, 0),    // Orb of Impale
        ],
        [255] = // Lumen the Barmaid
        [
            (0, MiscGroup, 17, 1), // Blood Bone +1
            (1, MiscGroup, 17, 2), // Blood Bone +2
            (2, MiscGroup, 17, 3), // Blood Bone +3
            (3, MiscGroup, 17, 4), // Blood Bone +4
            (4, MiscGroup, 17, 5), // Blood Bone +5
            (5, MiscGroup, 17, 6), // Blood Bone +6
            (6, MiscGroup, 17, 7), // Blood Bone +7
            (7, MiscGroup, 17, 8), // Blood Bone +8
            (39, ClassicPotionShops.PotionGroup, 10, 0), // Town Portal Scroll
        ],
        [244] = // Caren the Barmaid
        [
            (0, ClassicPotionShops.PotionGroup, 9, 0),  // Ale
            (1, ClassicPotionShops.PotionGroup, 10, 0), // Town Portal Scroll
            (2, MiscGroup, 16, 1), // Scroll of Archangel +1
            (3, MiscGroup, 16, 2), // Scroll of Archangel +2
            (4, MiscGroup, 17, 1), // Blood Bone +1
            (5, MiscGroup, 17, 2), // Blood Bone +2
            (6, ClassicPotionShops.PotionGroup, 17, 1), // Devils Eye +1
            (7, ClassicPotionShops.PotionGroup, 17, 2), // Devils Eye +2
        ],
    };

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override int Version => 3;

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 10, 16, 16, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var removedItems = new List<Item>(ClassicPotionShops.Apply(context, gameConfiguration));
        removedItems.AddRange(RestoreChangedMerchants(context, gameConfiguration));

        foreach (var item in removedItems)
        {
            await context.DeleteAsync(item).ConfigureAwait(false);
        }
    }

    private static IEnumerable<Item> RestoreChangedMerchants(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (merchantNumber, originalItems) in RestoredItems)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == merchantNumber)?.MerchantStore is not { } store)
            {
                continue;
            }

            // Previous versions placed potions on the first slots or removed the original items. In both cases,
            // all standard potions and everything on the original slots is removed and the original items are added again.
            var originalSlots = originalItems.Select(i => i.Slot).ToHashSet();
            var obsoleteItems = store.Items
                .Where(i => ClassicPotionShops.IsStandardPotion(i.Definition) || originalSlots.Contains(i.ItemSlot))
                .ToList();
            foreach (var item in obsoleteItems)
            {
                store.Items.Remove(item);
                yield return item;
            }

            foreach (var (slot, group, number, level) in originalItems)
            {
                if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is { } definition)
                {
                    var item = context.CreateNew<Item>();
                    item.Definition = definition;
                    item.ItemSlot = slot;
                    item.Durability = 1;
                    item.Level = level;
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
