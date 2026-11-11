using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    /// <summary>
    /// Routes explicit row/item inspection directly to the dedicated Item or NPC detail modal
    /// without opening a redundant side drawer.
    /// </summary>
    private void ShowInspector(ScreenSection section, ScreenRow row)
    {
        var itemModalCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("item:modal:", StringComparison.Ordinal));
        if (itemModalCmd is not null)
        {
            var uri = CommandUri.Parse(itemModalCmd.Id);
            if (uri.TryGetParamLong(out var modalItemId) || uri.TryGetTargetLong(out modalItemId))
            {
                TryOpenItemDetailModal(modalItemId);
                return;
            }
        }

        var npcModalCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("npc:modal:", StringComparison.Ordinal));
        if (npcModalCmd is not null && long.TryParse(npcModalCmd.Id["npc:modal:".Length..], out var npcId))
        {
            TryOpenNpcModal(npcId);
            return;
        }

        foreach (var cmd in row.Commands)
        {
            var uri = CommandUri.Parse(cmd.Id);
            if ((uri.Action is "buy" or "sell" or "store" or "take") &&
                (uri.TryGetTargetLong(out var tradeItemId) || uri.TryGetParamLong(out tradeItemId)))
            {
                TryOpenItemDetailModal(tradeItemId);
                return;
            }

            if (cmd.Id.StartsWith("talk:npc:", StringComparison.Ordinal) &&
                long.TryParse(cmd.Id["talk:npc:".Length..], out var talkNpcId))
            {
                TryOpenNpcModal(talkNpcId);
                return;
            }
        }

        if (_kernel.Capabilities.TryGet<IItemHubService>(CapabilityIds.ItemHub, out var hub) && hub is not null)
        {
            var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : section.Heading);
            var matchedEntry = hub.Entries.FirstOrDefault(e =>
                string.Equals(e.Item.NameVi, title, StringComparison.OrdinalIgnoreCase)
                || title.Contains(e.Item.NameVi, StringComparison.OrdinalIgnoreCase)
                || (row.Cells.Count > 1 && string.Equals(e.Item.NameVi, row.Cells[1], StringComparison.OrdinalIgnoreCase))
                || (row.Cells.Count > 0 && string.Equals(e.Item.NameVi, row.Cells[0], StringComparison.OrdinalIgnoreCase)));

            if (matchedEntry is not null)
            {
                TryOpenItemDetailModal(matchedEntry.Item.Id);
            }
        }
    }
}
