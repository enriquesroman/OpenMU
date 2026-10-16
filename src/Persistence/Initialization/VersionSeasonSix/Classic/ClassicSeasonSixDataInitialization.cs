// <copyright file="ClassicSeasonSixDataInitialization.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.TestAccounts;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Provides initial data for Season 6 Episode 3 Classic x500.
/// </summary>
[Guid("6C1CA38B-0839-4E9B-A5A2-F7315455D7A4")]
[PlugIn]
[Display(Name = "1.04d - Season 6 Classic x500", Description = "Season 6 Episode 3 Classic x500 with classic classes, resets, training spots, and equipment limited up to Great Dragon and equivalents.")]
public class ClassicSeasonSixDataInitialization : DataInitializationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassicSeasonSixDataInitialization" /> class.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public ClassicSeasonSixDataInitialization(IPersistenceContextProvider persistenceContextProvider, ILoggerFactory loggerFactory)
        : base(persistenceContextProvider, loggerFactory)
    {
    }

    /// <summary>
    /// Gets the identifier, by which the initialization is selected.
    /// </summary>
    public static string Id => "season6-classic";

    /// <inheritdoc />
    public override string Key => Id;

    /// <inheritdoc />
    public override string Caption => "1.04d - Season 6 Classic x500";

    /// <inheritdoc />
    protected override float ExperienceRate => 500.0f;

    /// <inheritdoc/>
    protected override IInitializer TestAccountsInitializer => new TestAccountsInitialization(this.Context, this.GameConfiguration);

    /// <inheritdoc/>
    protected override IInitializer GameConfigurationInitializer => new ClassicGameConfigurationInitializer(this.Context, this.GameConfiguration);

    /// <inheritdoc/>
    protected override IGameMapsInitializer GameMapsInitializer => new GameMapsInitializer(this.Context, this.GameConfiguration);

    /// <inheritdoc />
    protected override void CreateGameClientDefinition()
    {
        var season6GmoClient = this.Context.CreateNew<GameClientDefinition>();
        season6GmoClient.SetGuid(0x104D);
        season6GmoClient.Season = 6;
        season6GmoClient.Episode = 3;
        season6GmoClient.Language = ClientLanguage.English;
        season6GmoClient.Version = [0x31, 0x30, 0x34, 0x30, 0x34];
        season6GmoClient.Serial = "k1Pk2jcET48mxL3b"u8.ToArray();
        season6GmoClient.Description = "Season 6 Episode 3 GMO Client";

        var season6OpenSource = this.Context.CreateNew<GameClientDefinition>();
        season6OpenSource.SetGuid(0x204D);
        season6OpenSource.Season = 106;
        season6OpenSource.Episode = 3;
        season6OpenSource.Language = ClientLanguage.English;
        season6OpenSource.Version = [0x32, 0x30, 0x34, 0x30, 0x34];
        season6OpenSource.Serial = "k1Pk2jcET48mxL3b"u8.ToArray();
        season6OpenSource.Description = "Season 6 Episode 3 Open Source Client";
    }

    /// <inheritdoc />
    protected override void ApplyCustomPlugInConfigurations(PlugInManager plugInManager, ReferenceHandler referenceHandler)
    {
        base.ApplyCustomPlugInConfigurations(plugInManager, referenceHandler);

        var resetPluginConfig = this.GameConfiguration.PlugInConfigurations
            .FirstOrDefault(p => p.TypeId == typeof(ResetFeaturePlugIn).GUID);
        if (resetPluginConfig is not null)
        {
            resetPluginConfig.IsActive = true;
            var config = new ResetConfiguration
            {
                RequiredLevel = 400,
                LevelAfterReset = 1,
                RequiredMoney = 10_000_000,
                MultiplyRequiredMoneyByResetCount = false,
                ResetStats = true,
                PointsPerReset = 500,
                MultiplyPointsByResetCount = true,
                ReplacePointsPerReset = true,
                MoveHome = true,
                LogOut = false,
            };
            resetPluginConfig.SetConfiguration(config, referenceHandler);
        }
    }
}
