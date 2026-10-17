// <copyright file="FixPotionStacksAndShopsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates the maximum durability of potions and portal scrolls to 50 and configures all Classic x500 merchant stores.
/// </summary>
/// <remarks>
/// The merchant stores are cached by a running server and their items are not covered by the
/// configuration change propagation, so the server process needs to be restarted afterwards.
/// </remarks>
public abstract class FixPotionStacksAndShopsPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plugin name.
    /// </summary>
    internal const string PlugInName = "Fix Potion Stacks to 50 & Classic x500 Merchant Shops";

    /// <summary>
    /// The plugin description.
    /// </summary>
    internal const string PlugInDescription = "Configures all Classic x500 merchant shops: 50-stack potions, basic pets (Angel, Imp, Uniria), leveling skills, pre-crafted BC/DS event invitations, lvl 150/220 quest items, and DL/MG sets and weapons. Requires a server restart.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override int Version => 4;

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 10, 17, 1, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var removedItems = ClassicPotionShops.Apply(context, gameConfiguration);
        foreach (var item in removedItems)
        {
            await context.DeleteAsync(item).ConfigureAwait(false);
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
