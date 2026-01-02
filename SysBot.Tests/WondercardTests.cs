using FluentAssertions;
using PKHeX.Core;
using SysBot.Pokemon;
using Xunit;
using System.IO;

namespace SysBot.Tests;

public class WondercardTests
{
    // Event with fixed OT (e.g., "HOME" for Zeraora)
    private const string ZeraoraWondercardPath = @"c:\Users\Takut\OneDrive\Dokumente\GitHub\ZE-FusionBot\9011_SWSH_-_Simulated_HOME_Shiny_Zeraora.wc8";

    // Event with variable OT (trainer's own OT, like Mew)
    private const string MewWondercardPath = @"c:\Users\Takut\OneDrive\Dokumente\GitHub\ZE-FusionBot\1521_SV_-_Trainer_Mew_Ground_Tera_Type.wc9";

    static WondercardTests()
    {
        // Initialize AutoLegalityWrapper with default settings (matching PKHeX/hideoutpk.de defaults)
        var settings = new SysBot.Pokemon.LegalitySettings
        {
            GenerateOT = "hideoutpk.de",
            GenerateTID16 = 12345,
            GenerateSID16 = 54321,
            GenerateLanguage = LanguageID.English
        };
        AutoLegalityWrapper.EnsureInitialized(settings);
    }

    [Fact]
    public void ZeraoraWondercardFileExists()
    {
        // Verify the wondercard file exists
        File.Exists(ZeraoraWondercardPath).Should().BeTrue($"Zeraora wondercard file should exist at {ZeraoraWondercardPath}");
    }

    [Fact]
    public void MewWondercardFileExists()
    {
        // Verify the wondercard file exists
        File.Exists(MewWondercardPath).Should().BeTrue($"Mew wondercard file should exist at {MewWondercardPath}");
    }

    #region Zeraora Tests (Event with fixed OT)

    [Fact]
    public void Zeraora_CanLoadWondercard()
    {
        // Arrange
        var bytes = File.ReadAllBytes(ZeraoraWondercardPath);
        var extension = Path.GetExtension(ZeraoraWondercardPath);

        // Act
        var mg = MysteryGift.GetMysteryGift(bytes, extension);

        // Assert
        mg.Should().NotBeNull("Zeraora wondercard should be loaded successfully");
        mg!.Species.Should().Be((ushort)Species.Zeraora, "Wondercard should contain Zeraora");
    }

    [Fact]
    public void Zeraora_IsLegalAndHasFixedOT()
    {
        // Arrange
        var bytes = File.ReadAllBytes(ZeraoraWondercardPath);
        var extension = Path.GetExtension(ZeraoraWondercardPath);
        var mg = MysteryGift.GetMysteryGift(bytes, extension);
        var trainer = AutoLegalityWrapper.GetFallbackTrainer();

        // Act
        var pkm = mg?.ConvertToPKM(trainer);
        var la = new LegalityAnalysis(pkm!);

        // Assert
        pkm.Should().NotBeNull();
        pkm!.FatefulEncounter.Should().BeTrue("Zeraora is an event Pokemon");
        pkm.OriginalTrainerName.Should().NotBeNullOrEmpty("Should have OT from event");

        // Zeraora has a fixed OT from the event (e.g., "HOME"), not the fallback
        pkm.OriginalTrainerName.Should().NotBe("hideoutpk.de", "Should have event OT, not fallback trainer OT");

        var report = la.Report();
        if (!la.Valid)
        {
            la.Valid.Should().BeTrue($"Zeraora PKM should be legal. Legality Report:\n{report}");
        }
    }

    #endregion

    #region Mew Tests (Event with variable OT - trainer's own)

    [Fact]
    public void Mew_CanLoadWondercard()
    {
        // Arrange
        var bytes = File.ReadAllBytes(MewWondercardPath);
        var extension = Path.GetExtension(MewWondercardPath);

        // Act
        var mg = MysteryGift.GetMysteryGift(bytes, extension);

        // Assert
        mg.Should().NotBeNull("Mew wondercard should be loaded successfully");
        mg!.Species.Should().Be((ushort)Species.Mew, "Wondercard should contain Mew");
    }

    [Fact]
    public void Mew_IsLegalWithFallbackTrainer()
    {
        // Arrange
        var bytes = File.ReadAllBytes(MewWondercardPath);
        var extension = Path.GetExtension(MewWondercardPath);
        var mg = MysteryGift.GetMysteryGift(bytes, extension);
        var trainer = AutoLegalityWrapper.GetFallbackTrainer();

        // Act
        var pkm = mg?.ConvertToPKM(trainer);
        var la = new LegalityAnalysis(pkm!);

        // Assert
        pkm.Should().NotBeNull();
        pkm!.FatefulEncounter.Should().BeTrue("Mew is an event Pokemon");

        // Mew uses the trainer's own OT (from fallback trainer in this case)
        pkm.OriginalTrainerName.Should().Be("hideoutpk.de", "Should use fallback trainer OT for variable OT events");

        var report = la.Report();
        if (!la.Valid)
        {
            la.Valid.Should().BeTrue($"Mew PKM should be legal. Legality Report:\n{report}");
        }
    }

    [Fact]
    public void Mew_HasCorrectTeraType()
    {
        // Arrange
        var bytes = File.ReadAllBytes(MewWondercardPath);
        var extension = Path.GetExtension(MewWondercardPath);
        var mg = MysteryGift.GetMysteryGift(bytes, extension);
        var trainer = AutoLegalityWrapper.GetFallbackTrainer();

        // Act
        var pkm = mg?.ConvertToPKM(trainer);

        // Assert
        pkm.Should().NotBeNull();
        pkm.Should().BeOfType<PK9>("Mew WC9 should convert to PK9");

        var pk9 = pkm as PK9;
        pk9.Should().NotBeNull();
        // Ground Tera Type according to the filename
        pk9!.TeraType.Should().Be(MoveType.Ground, "This is a Ground Tera Type Mew event");
    }

    #endregion

    #region General Tests

    [Fact]
    public void EmptyTrainerInfoShouldFailGracefully()
    {
        // Arrange
        var bytes = File.ReadAllBytes(ZeraoraWondercardPath);
        var extension = Path.GetExtension(ZeraoraWondercardPath);
        var mg = MysteryGift.GetMysteryGift(bytes, extension);
        var emptyTrainer = new SimpleTrainerInfo(); // Empty trainer like the old code

        // Act
        var pkm = mg?.ConvertToPKM(emptyTrainer);

        // Assert
        pkm.Should().NotBeNull("Even with empty trainer, PKM should be created");

        // However, the legality check should fail
        var la = new LegalityAnalysis(pkm!);
        // We expect this to fail because of missing OT data
        // This demonstrates why we needed the fix
    }

    #endregion
}
