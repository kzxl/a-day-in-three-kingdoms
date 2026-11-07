using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf.Controls;

public enum LoadingStepStatus
{
    Pending,
    Active,
    Done
}

/// <summary>
/// Shared loading state between the initial loading window (steps 1–4) and MainWindow's
/// overlay (steps 5–6), ensuring the traveler position, walk distance, tip timer, and
/// step checkmarks continue without any visual jump across the window handoff.
/// </summary>
public sealed class LoadingProgressState
{
    public static readonly IReadOnlyList<string> StepLabels =
    [
        "Mở sổ sách ván chơi",
        "Nạp 300 danh sĩ thiên hạ",
        "Dựng 20 thành trì và thương lộ",
        "Tính giá chợ 24 mặt hàng",
        "Khởi động khung cảnh",
        "Vẽ cảnh Trong thành"
    ];

    public static readonly IReadOnlyList<string> Tips =
    [
        "Đi bộ mang theo được 40 đơn vị hàng. Muốn chở nhiều hơn, hãy thuê Thương Đoàn.",
        "Mệt từ 95 trở lên thì không thể lên đường hay giao đấu. Nghỉ ngơi trước đã.",
        "Bộ khắc Cung, Cung khắc Kỵ, Kỵ khắc Bộ. Chọn binh chủng trước khi lên lôi đài.",
        "Lễ vật càng quý, danh sĩ bậc cao càng dễ kết giao.",
        "Hàng để lại ở thành nào vẫn nằm ở thành đó. Mở Bản đồ để xem đồ của mình ở đâu."
    ];

    private readonly object _sync = new();
    private readonly bool[] _done = new bool[6];
    private readonly bool[] _loggedBySink = new bool[4];

    public string CampaignName { get; private set; } = "Lữ Khách";
    public int Turn { get; private set; } = 1;
    public string CityNameVi { get; private set; } = "Lạc Dương";
    public string CityNameHan { get; private set; } = "洛陽";
    public string SublineText { get; private set; } = "Trung Nguyên · 02/05/184 · Mùa Hạ";

    public double Pos { get; set; }
    public double Dist { get; set; }
    public double IdleTime { get; set; }
    public bool IsMoving { get; set; }
    public bool Ready { get; set; }

    public static LoadingProgressState ForNewSave(string campaignName)
    {
        var state = new LoadingProgressState();
        var startDate = new GameDate(184, 5, 2);
        state.SetHeader(
            string.IsNullOrWhiteSpace(campaignName) ? "Lữ Khách" : campaignName.Trim(),
            turn: 1,
            cityNameVi: "Lạc Dương",
            cityCode: "lac_duong",
            regionNameVi: "Trung Nguyên",
            date: startDate);
        return state;
    }

    public static LoadingProgressState ForSaveItem(TitleWindow.SaveItem item)
    {
        var state = new LoadingProgressState();
        var startDate = new GameDate(184, 5, 2);
        var date = GameDateCodec.TryParse(item.GameDate, out var parsed) ? parsed : startDate;
        var turn = Math.Max(1, (int)(date.DayIndex - startDate.DayIndex + 1));
        state.SetHeader(
            item.Name,
            turn,
            item.CityName,
            item.CityCode,
            item.RegionName,
            date);
        return state;
    }

    public void SetHeader(
        string campaignName,
        int turn,
        string cityNameVi,
        string? cityCode,
        string regionNameVi,
        GameDate date)
    {
        lock (_sync)
        {
            CampaignName = string.IsNullOrWhiteSpace(campaignName) ? "Lữ Khách" : campaignName;
            Turn = Math.Max(1, turn);
            CityNameVi = string.IsNullOrWhiteSpace(cityNameVi) ? "Lạc Dương" : cityNameVi;
            CityNameHan = CitadelDef.ResolveHanzi(cityCode);
            var region = RoleTitleVi.FormatRegion(regionNameVi);
            SublineText = $"{region} · {GameDateCodec.ShortVi(date)} · Mùa {GameDateCodec.SeasonVi(date.Month)}";
        }
    }

    public void UpdateFromKernel(SimKernel kernel, string fallbackCampaignName)
    {
        var player = kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) ? ps : null;
        var map = kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var wm) ? wm : null;
        var citadel = player is not null && map is not null ? map.Find(player.CurrentCityId) : null;

        var date = kernel.Today;
        var startDate = kernel.Product.StartDate;
        var turn = Math.Max(1, (int)(date.DayIndex - startDate.DayIndex + 1));

        lock (_sync)
        {
            CampaignName = !string.IsNullOrWhiteSpace(player?.Name) ? player.Name : fallbackCampaignName;
            Turn = turn;
            CityNameVi = citadel?.NameVi ?? CityNameVi;
            CityNameHan = citadel is not null ? citadel.NameHan : CityNameHan;
            var region = citadel is not null ? RoleTitleVi.FormatRegion(citadel.Region) : "Trung Nguyên";
            SublineText = $"{region} · {GameDateCodec.ShortVi(date)} · Mùa {GameDateCodec.SeasonVi(date.Month)}";
        }
    }

    public void MarkStepDone(int index, bool fromLogSink = false)
    {
        if (index is < 0 or >= 6)
            return;

        lock (_sync)
        {
            _done[index] = true;
            if (fromLogSink && index < _loggedBySink.Length)
                _loggedBySink[index] = true;
        }
    }

    public void OnKernelLog(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;

        // Step 0: Opening save database / kernel startup has begun
        MarkStepDone(0, fromLogSink: true);

        if (line.Contains("base.npc:", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("npc registry ready", StringComparison.OrdinalIgnoreCase))
        {
            MarkStepDone(1, fromLogSink: true);
        }
        else if (line.Contains("base.world:", StringComparison.OrdinalIgnoreCase) &&
                 line.Contains("map ready", StringComparison.OrdinalIgnoreCase))
        {
            MarkStepDone(2, fromLogSink: true);
        }
        else if (line.Contains("dlc.economy:", StringComparison.OrdinalIgnoreCase) &&
                 line.Contains("market(", StringComparison.OrdinalIgnoreCase))
        {
            MarkStepDone(3, fromLogSink: true);
        }
    }

    public void MarkKernelOpenCompleted()
    {
        lock (_sync)
        {
            for (var i = 0; i < 4; i++)
                _done[i] = true;
        }
    }

    public void MarkAllDone()
    {
        lock (_sync)
        {
            for (var i = 0; i < 6; i++)
                _done[i] = true;
        }
    }

    public (int CompletedCount, int ActiveIndex, LoadingStepStatus[] Statuses) GetSnapshot()
    {
        lock (_sync)
        {
            var statuses = new LoadingStepStatus[6];
            var completed = 0;
            var activeIdx = -1;

            for (var i = 0; i < 6; i++)
            {
                if (_done[i])
                {
                    statuses[i] = LoadingStepStatus.Done;
                    completed++;
                }
                else if (activeIdx == -1)
                {
                    activeIdx = i;
                    statuses[i] = LoadingStepStatus.Active;
                }
                else
                {
                    statuses[i] = LoadingStepStatus.Pending;
                }
            }

            return (completed, activeIdx, statuses);
        }
    }
}
