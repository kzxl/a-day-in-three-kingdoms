using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyChronicleScreen
{
    private ScreenModel RenderRumors()
    {
        var news = (_chronicle as ITavernRumors)?.History() ?? [];
        var rows = news.Select(r => new ScreenRow(
            [GameDateCodec.ShortVi(GameDate.FromDayIndex(r.HeardDay)),
                _map.Find(r.HeardCityId)?.NameVi ?? "", r.TitleVi, r.NarrativeVi, r.StatusVi,
                r.IsExpired(_clock.Today.DayIndex) ? "Hết hạn" : "Còn hiệu lực"],
            [], Details: [new("Lời kể", r.NarrativeVi), new("Kiểm chứng", r.StatusVi)],
            IsMuted: r.IsExpired(_clock.Today.DayIndex))).ToArray();
        return new(ScreenId, "Sổ tin đồn", "Tin đồn · Chưa rõ / Đúng / Sai",
            [new("Tin đồn đã nghe", ["Ngày", "Thành nghe", "Chuyện", "Lời kể", "Kiểm chứng", "Thời hạn"], rows)],
            [new("filter:all", "Tất Cả", CommandKind.Secondary),
                new("filter:rumors", "Tin đồn", CommandKind.Primary),
                new("filter:migration", "Di Cư", CommandKind.Secondary)]);
    }
}
