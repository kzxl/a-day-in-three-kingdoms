using System.Windows;
using System.Windows.Controls;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameChronicleView
{
    public void SetFilter(string filter)
    {
        _currentFilter = filter;
        BuildFilterButtons();
        RenderList();
    }

    private void LoadRumorEvents()
    {
        if (_kernel is null || !_kernel.Capabilities.TryGet<ITavernRumors>(CapabilityIds.TavernRumors, out var rumors)
            || rumors is null) return;
        _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map);
        foreach (var rumor in rumors.History())
        {
            var expired = rumor.IsExpired(_kernel.Today.DayIndex);
            _allEvents.Add(new ChronicleEventModel
            {
                Id = -rumor.Id,
                Date = GameDate.FromDayIndex(rumor.HeardDay),
                CitadelName = map?.Find(rumor.HeardCityId)?.NameVi ?? "",
                CategoryKey = "rumors",
                CategoryNameVi = "Tin đồn",
                Headline = $"{rumor.TitleVi} · {rumor.StatusVi}",
                Narrative = rumor.NarrativeVi + $"\n\nKiểm chứng: {rumor.StatusVi}. " +
                    (expired ? "Tin đã hết hạn." : "Tin còn hiệu lực."),
                IsMuted = expired
            });
        }
    }

    private void LoadHistoricalEvents(ISocietyChronicle society)
    {
        if (_kernel is null) return;
        _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map);
        var existingCodes = _allEvents
            .Select(e => e.HistoricalEventCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        long idx = -500000;
        foreach (var evt in society.GetHistoricalEvents(occurredOnly: true))
        {
            if (existingCodes.Contains(evt.Code))
            {
                continue;
            }

            var impactSummary = string.Join(" · ", evt.Impacts.Select(i => i.NoteVi));
            _allEvents.Add(new ChronicleEventModel
            {
                Id = idx--,
                Date = evt.ScheduledDate,
                CitadelName = map?.Find(evt.PrimaryCitadelId)?.NameVi ?? "Trung Nguyên",
                CategoryKey = "history",
                CategoryNameVi = "Sử biến",
                Headline = $"[Sử Biến] {evt.TitleVi}",
                Narrative = evt.NarrativeVi,
                StatImpact = impactSummary,
                CanIntervene = false,
                IsResolved = true,
                HistoricalEventCode = evt.Code
            });
        }
    }

    private static string? ExtractHistoricalCode(string? statImpact)
    {
        if (string.IsNullOrWhiteSpace(statImpact) || !statImpact.StartsWith("evt:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = statImpact["evt:".Length..];
        var sep = raw.IndexOf('·');
        return (sep >= 0 ? raw[..sep] : raw).Trim();
    }

    private UIElement BuildHistoricalReplayPad(string eventCode)
    {
        var box = new Border
        {
            Background = ParchmentSelectedBg,
            BorderBrush = GoldAccent,
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var dock = new DockPanel();
        var replayBtn = new GameButton
        {
            Content = "📜 Mở Cảnh Sử Biến",
            Variant = GameButtonVariant.Primary,
            MinHeight = 30,
            Padding = new Thickness(12, 4, 12, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        replayBtn.Click += (_, _) => HistoricalEventReplayRequested?.Invoke(eventCode);
        DockPanel.SetDock(replayBtn, Dock.Right);
        dock.Children.Add(replayBtn);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        info.Children.Add(new TextBlock
        {
            Text = "🐉 BƯỚC NGOẶT LỊCH SỬ TAM QUỐC",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = GoldAccent,
            Margin = new Thickness(0, 0, 0, 2)
        });
        info.Children.Add(new TextBlock
        {
            Text = "Bạn có thể mở lại cảnh sử biến toàn màn hình để xem chân dung danh sĩ và diễn biến lịch sử.",
            FontSize = 11,
            Foreground = TextParchmentMuted,
            TextWrapping = TextWrapping.Wrap
        });
        dock.Children.Add(info);

        box.Child = dock;
        return box;
    }
}
