using System.Text.Json;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Core.Kernel;
using SimT.Core.Modules;
using SimT.Testing;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class DialogueTests
{
    [Fact]
    public void Selects_dialogue_lines_by_relationship_tier_for_named_and_role_npcs()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var liuBei = npcs.FindByCode("npc_0001")!;

        var strangerLines = dialogue.GetLinesForNpc(liuBei.Id, "stranger");
        var acquaintanceLines = dialogue.GetLinesForNpc(liuBei.Id, "acquaintance");
        var trustedLines = dialogue.GetLinesForNpc(liuBei.Id, "trusted");

        Assert.True(strangerLines.Count >= 3);
        Assert.True(acquaintanceLines.Count >= 3);
        Assert.True(trustedLines.Count >= 3);
        Assert.NotEqual(strangerLines[0], acquaintanceLines[0]);
        Assert.NotEqual(acquaintanceLines[0], trustedLines[0]);
    }

    [Fact]
    public void Every_dialogue_line_is_at_most_90_chars_and_every_choice_is_at_most_40_chars()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var playerState = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        bag.Add(playerState.CurrentCityId, 1, 2);

        foreach (var npc in npcs.All.Take(40))
        {
            foreach (var tier in new[] { "stranger", "acquaintance", "trusted" })
            {
                var lines = dialogue.GetLinesForNpc(npc.Id, tier);
                Assert.NotEmpty(lines);
                Assert.All(lines, line => Assert.True(line.Length <= 90, $"Line > 90 chars ({line.Length}): {line}"));
            }

            var scene = dialogue.BuildTalkScene(npc.Id);
            Assert.All(scene.Lines, line => Assert.True(line.Length <= 90, $"Scene line > 90 chars: {line}"));
            Assert.All(scene.Choices, c => Assert.True(c.LabelVi.Length <= 40, $"Choice > 40 chars: {c.LabelVi}"));
        }
    }

    [Fact]
    public void Gift_choice_is_hidden_when_bag_empty_and_shown_when_bag_has_item()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var playerState = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var localNpc = npcs.FindByCitadel(playerState.CurrentCityId).First();

        var emptyBagScene = dialogue.BuildTalkScene(localNpc.Id);
        Assert.DoesNotContain(emptyBagScene.Choices, c => c.Id == "gift");

        bag.Add(playerState.CurrentCityId, 1, 1);
        var withItemScene = dialogue.BuildTalkScene(localNpc.Id);
        Assert.Contains(withItemScene.Choices, c => c.Id == "gift");

        var giftOutcome = dialogue.HandleChoice(localNpc.Id, "gift");
        Assert.True(giftOutcome.Ok);
        Assert.True(giftOutcome.MessageVi.Length <= 90);
    }

    [Fact]
    public void Disabling_dlc_dialogue_still_boots_kernel_cleanly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "simt-no-dialogue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var modulesWithoutDialogue = ModuleLoader.Discover(AppContext.BaseDirectory)
                .Select(d => d.Manifest.Id)
                .Where(id => !string.Equals(id, "dlc.dialogue", StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            var productPath = Path.Combine(dir, "product.json");
            File.WriteAllText(productPath, JsonSerializer.Serialize(new
            {
                id = "test-no-dialogue",
                name = "No Dialogue",
                modules = modulesWithoutDialogue,
                seed = 1840502,
                startLocation = "lac_duong",
                startingCash = 2000
            }));

            using var kernel = SimKernel.Open(new KernelOptions(
                DatabasePath: Path.Combine(dir, "campaign.db"),
                ModulesDirectory: AppContext.BaseDirectory,
                ProductPath: productPath,
                ConstantsPath: TestHost.ConstantsPath,
                BackupDirectory: Path.Combine(dir, "backups")));

            Assert.False(kernel.Capabilities.IsProvided(CapabilityIds.Dialogue));
            Assert.True(kernel.Capabilities.IsProvided(CapabilityIds.NpcRegistry));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
