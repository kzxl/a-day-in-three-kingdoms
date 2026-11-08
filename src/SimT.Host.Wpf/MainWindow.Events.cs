using System.Text.RegularExpressions;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Host.Wpf.Controls;
using SimT.Host.Wpf.Scenes;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    public void CheckPendingHistoricalEventsAfterAdvance()
    {
        if (!_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) || society is null)
        {
            return;
        }

        var distantStatus = society.ConsumeLastHistoricalStatus();
        if (!string.IsNullOrWhiteSpace(distantStatus))
        {
            StatusText.Text = distantStatus;
            ShowNotification(distantStatus);
        }

        if (!_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) || player is null)
        {
            return;
        }

        var pending = society.DequeuePendingHistoricalEvent(player.CurrentCityId);
        if (pending is not null)
        {
            ShowNotification($"📜 Sử biến bùng nổ ({GameDateCodec.ShortVi(pending.ScheduledDate)}): {pending.TitleVi}");
            TryOpenHistoricalEventScene(pending.Code, isReplay: false);
        }
    }

    public bool TryOpenHistoricalEventScene(string eventCode, bool isReplay = false)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) ||
            society is null)
        {
            return false;
        }

        var evt = society.FindHistoricalEvent(eventCode);
        if (evt is null)
        {
            return false;
        }

        var scene = BuildHistoricalEventScene(evt, isReplay);
        if (!SceneOverlay.ShowEventScene(scene))
        {
            return false;
        }

        StatusText.Text = $"[Sử Biến · {GameDateCodec.ShortVi(evt.ScheduledDate)}] {evt.TitleVi}";
        return true;
    }

    public EventScene BuildHistoricalEventScene(HistoricalEventView evt, bool isReplay = false)
    {
        _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map);
        _kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg);

        var cityName = map?.Find(evt.PrimaryCitadelId)?.NameVi ?? "Trung Nguyên";
        var portraits = new List<EventPortraitView>(2);
        if (reg is not null)
        {
            foreach (var code in evt.RelatedNpcCodes.Take(2))
            {
                if (reg.FindByCode(code) is { } npc)
                {
                    var (fullArt, _, _) = SceneArtCatalog.ResolveNpcArt(npc);
                    portraits.Add(new EventPortraitView(
                        npc.Id,
                        npc.Code,
                        npc.NameVi,
                        RoleTitleVi.Format(npc.Role),
                        fullArt)
                    {
                        ExpressionPaths = SceneArtCatalog.ResolvePortraitExpressions(fullArt),
                        LineExpressions = evt.PortraitExpressions.GetValueOrDefault(npc.Code) ?? Array.Empty<string>()
                    });
                }
            }
        }

        var sentences = SplitNarrativeSentences(evt.NarrativeVi);
        var impacts = evt.Impacts
            .Select(i => i.NoteVi)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var choices = evt.Choices
            .Select(c => SplitChoiceEffect(c.Key, c.LabelVi, c.HintVi))
            .ToList();
        if (choices.Count == 0 || !choices.Any(c => string.Equals(c.Id, "continue", StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new SceneChoice("continue", "Tiếp tục", "Khép lại trang sử"));
        }

        return new EventScene(
            $"event:{evt.Code}",
            evt.Code,
            GameDateCodec.Format(evt.ScheduledDate),
            $"{GameDateCodec.EraVi(evt.ScheduledDate.Year)} · {GameDateCodec.DescribeVi(evt.ScheduledDate)}",
            cityName,
            evt.TitleVi,
            evt.NarrativeVi,
            sentences,
            SceneArtCatalog.BackgroundForKey(evt.BackgroundKey),
            portraits,
            impacts,
            choices,
            isReplay || string.Equals(evt.Status, "viewed", StringComparison.OrdinalIgnoreCase));
    }

    // Event data writes the effect in brackets ("Quyên 80 Quan … (+Uy danh)"); show it as the choice hint instead.
    private static SceneChoice SplitChoiceEffect(string key, string label, string? hint)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(hint) || open <= 0 || !label.EndsWith(')'))
            return new SceneChoice(key, label, hint);
        return new SceneChoice(key, label[..open], label[(open + 2)..^1]);
    }

    private void HandleEventSceneMessage(string type, string id, EventScene scene)
    {
        if (type != "choice")
        {
            return;
        }

        if (_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) && society is not null)
        {
            var outcome = society.ResolveHistoricalEventChoice(scene.EventCode, id);
            if (!outcome.Ok)
            {
                StatusText.Text = outcome.MessageVi;
                ShowNotification(outcome.MessageVi, GameToastKind.Loss);
                return;
            }

            _kernel.Save();
            UpdateHeader();
            InvalidateBadges();
            RefreshBadges();
            StatusText.Text = outcome.MessageVi;
            ShowNotification(outcome.MessageVi);
        }

        SceneOverlay.HideScene();
        if (string.Equals(_screenId, CitySceneScreenId, StringComparison.Ordinal))
        {
            SelectScreen(CitySceneScreenId);
        }
        else
        {
            RenderSelected();
        }
    }

    private static IReadOnlyList<string> SplitNarrativeSentences(string narrative)
    {
        if (string.IsNullOrWhiteSpace(narrative))
        {
            return ["Thiên hạ đại biến, sử quan ghi chép vào biên niên."];
        }

        var parts = Regex.Split(narrative.Trim(), @"(?<=[\.\!\?…])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        return parts.Length > 0 ? parts : [narrative.Trim()];
    }
}
