// <copyright file="ClassicSeasonSixTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Classic;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests for the Season 6 Classic x500 initialization.
/// </summary>
[TestFixture]
internal class ClassicSeasonSixTests
{
    /// <summary>
    /// Tests that the classic season 6 initialization initializes the expected configuration.
    /// </summary>
    [Test]
    public async Task TestClassicSeason6DataAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new ClassicSeasonSixDataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var serverDefinitions = await context.GetAsync<GameServerDefinition>().ConfigureAwait(false);

        // 1. Verify Experience Rate is 500x
        var server = serverDefinitions.First();
        Assert.That(server.ExperienceRate, Is.EqualTo(500.0f));

        // 2. Verify Summoner and Rage Fighter are disabled
        var summoner = gameConfiguration.CharacterClasses.First(c => c.Number == (byte)CharacterClassNumber.Summoner);
        var rageFighter = gameConfiguration.CharacterClasses.First(c => c.Number == (byte)CharacterClassNumber.RageFighter);
        Assert.That(summoner.CanGetCreated, Is.False);
        Assert.That(rageFighter.CanGetCreated, Is.False);

        // 3. Verify classic classes are enabled
        var darkKnight = gameConfiguration.CharacterClasses.First(c => c.Number == (byte)CharacterClassNumber.DarkKnight);
        var darkWizard = gameConfiguration.CharacterClasses.First(c => c.Number == (byte)CharacterClassNumber.DarkWizard);
        var fairyElf = gameConfiguration.CharacterClasses.First(c => c.Number == (byte)CharacterClassNumber.FairyElf);
        Assert.That(darkKnight.CanGetCreated, Is.True);
        Assert.That(darkWizard.CanGetCreated, Is.True);
        Assert.That(fairyElf.CanGetCreated, Is.True);

        // 4. Verify Reset Plugin is active
        var resetPluginConfig = gameConfiguration.PlugInConfigurations
            .FirstOrDefault(p => p.TypeId == typeof(ResetFeaturePlugIn).GUID);
        Assert.That(resetPluginConfig, Is.Not.Null);
        Assert.That(resetPluginConfig!.IsActive, Is.True);

        // 5. Verify armor items above Great Dragon and equivalents are not dropping
        var highTierArmors = gameConfiguration.Items
            .Where(i => i.Group is >= 7 and <= 11 && i.Number >= 29)
            .ToList();
        Assert.That(highTierArmors, Is.Not.Empty);
        Assert.That(highTierArmors.All(i => !i.DropsFromMonsters), Is.True);

        // 6. Verify Great Dragon armor (Group 8, Number 21) drops
        var greatDragonArmor = gameConfiguration.Items.First(i => i.Group == 8 && i.Number == 21);
        Assert.That(greatDragonArmor.DropsFromMonsters, Is.True);

        // 7. Verify training spots exist
        var lorencia = gameConfiguration.Maps.First(m => m.Number == 0);
        var lorenciaSpots = lorencia.MonsterSpawns.Where(s => s.Quantity >= 5).ToList();
        Assert.That(lorenciaSpots, Is.Not.Empty);

        var devias = gameConfiguration.Maps.First(m => m.Number == 2);
        var deviasSpots = devias.MonsterSpawns.Where(s => s.Quantity >= 5).ToList();
        Assert.That(deviasSpots, Is.Not.Empty);
    }
}
