using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyChronicleScreen
{
    private ScreenModel RenderEspionage(long currentCityId, string currentCitadelName)
    {
        var sections = new List<ScreenSection>();
        var espionage = _chronicle as IEspionageNetwork;

        if (espionage is not null)
        {
            var localNode = espionage.GetSpyNode(currentCityId);
            var allNodes = espionage.GetSpyNodes()
                .OrderByDescending(n => n.CitadelId == currentCityId)
                .ThenByDescending(n => n.IntelLevel)
                .ThenBy(n => n.CitadelId)
                .ToList();

            var spyCols = new[] { "Thành Trì", "Cấp Độ Tế Tác", "Chiến Dịch Ngầm", "Số Lần Mật Vụ", "Tình Báo", "Thao Tác Mật Thám" };
            var spyRows = allNodes.Select(n =>
            {
                var levelText = n.IntelLevel switch
                {
                    0 => "Chưa cài cắm (Cấp 0)",
                    1 => "★ Nhãn Tuyến Sơ Cấp (Cấp 1)",
                    2 => "★★ Mật Điệp Trung Cấp (Cấp 2)",
                    _ => "★★★ Thiên La Địa Võng (Cấp 3)"
                };

                var rowCmds = new List<ScreenCommand>();
                if (n.IntelLevel < 3)
                {
                    var nextCost = (n.IntelLevel + 1) switch { 1 => 150, 2 => 250, _ => 400 };
                    rowCmds.Add(new ScreenCommand($"spy:deploy:{n.CitadelId}", $"Cài Tế Tác Cấp {n.IntelLevel + 1} ({nextCost}Q) 🕵️", CommandKind.Primary));
                }

                if (n.IntelLevel > 0)
                {
                    rowCmds.Add(new ScreenCommand($"spy:whisper:{n.CitadelId}:price_whisper", "Tung Tin Đồn Giá (90Q) 📢", CommandKind.Secondary));
                    rowCmds.Add(new ScreenCommand($"spy:whisper:{n.CitadelId}:counter_intel", "Phản Gián (90Q) 🛡️", CommandKind.Secondary));
                }

                return new ScreenRow(
                    [
                        n.CitadelId == currentCityId ? $"📍 {n.CitadelNameVi} (Sở tại)" : n.CitadelNameVi,
                        levelText,
                        n.ActiveOperation == "none" ? "Tĩnh lặng" : n.ActiveOperation,
                        $"{n.SabotageCount} vụ",
                        n.IntelLevel > 0 ? "Đã thông suốt" : "Mù thông tin",
                        n.IntelLevel > 0 ? "Sẵn sàng mật lệnh" : "Cần cài mật thám"
                    ],
                    rowCmds,
                    Title: $"Mật Thám Ty · {n.CitadelNameVi}",
                    Details:
                    [
                        new ScreenDetail("Thành Trì", n.CitadelNameVi),
                        new ScreenDetail("Cấp Độ Tế Tác", levelText),
                        new ScreenDetail("Chiến Dịch Đang Chạy", n.ActiveOperation),
                        new ScreenDetail("Tổng Số Vụ Thao Túng", $"{n.SabotageCount} lần")
                    ]);
            }).ToList();

            sections.Add(new ScreenSection($"🕵️ Mạng Lưới Tế Tác & Tình Báo Châu Quận ({allNodes.Count(n => n.IntelLevel > 0)}/{allNodes.Count} thành đã cài cắm)", spyCols, spyRows, SectionViewMode.DossierGrid));

            var tomes = espionage.GetHermitTomes();
            var tomeCols = new[] { "Kỳ Thư Truyền Thế", "Danh Sĩ Ẩn Cư", "Nơi Ẩn Dật", "Thuộc Tính & Tuyệt Kỹ", "Trạng Thái", "Cầu Đạo" };
            var tomeRows = tomes.Select(t =>
            {
                var statusText = t.IsAcquired ? "✅ Đã Lĩnh Hội Chân Truyền" : "🔒 Chưa Tầm Sư (200 Quan)";
                var rowCmds = t.IsAcquired
                    ? Array.Empty<ScreenCommand>()
                    : new[] { new ScreenCommand($"tome:seek:{t.Code}", "Tầm Sư Cầu Đạo (200Q) 📖", CommandKind.Primary) };

                return new ScreenRow(
                    [
                        $"📘 {t.NameVi}",
                        t.HermitNameVi,
                        t.CitadelNameVi,
                        $"+{t.StatBonus} {t.StatCode} · Kỹ năng {t.SkillCode}",
                        statusText,
                        t.IsAcquired ? "Đã khắc cốt ghi tâm" : "Đích thân tới hoặc có Tế Tác Cấp 1"
                    ],
                    rowCmds,
                    Title: t.NameVi,
                    Details:
                    [
                        new ScreenDetail("Kỳ Thư", t.NameVi),
                        new ScreenDetail("Bậc Kỳ Tài Giữ Sách", t.HermitNameVi),
                        new ScreenDetail("Châu Quận Ẩn Cư", t.CitadelNameVi),
                        new ScreenDetail("Công Hiệu Lĩnh Hội", $"+{t.StatBonus} tư chất {t.StatCode} & +60 EXP kỹ năng {t.SkillCode}"),
                        new ScreenDetail("Tình Trạng", statusText)
                    ]);
            }).ToList();

            sections.Add(new ScreenSection($"📚 Ngũ Đại Kỳ Thư Ẩn Sĩ Tam Quốc ({tomes.Count(t => t.IsAcquired)}/{tomes.Count} quyển)", tomeCols, tomeRows, SectionViewMode.DossierGrid));
        }

        var commands = new List<ScreenCommand>
        {
            new("filter:all", "📜 Tất Cả", CommandKind.Secondary),
            new("filter:espionage", "🕵️ Mật Thám & Kỳ Thư", CommandKind.Primary),
            new("filter:rivals", "🏆 Phú Hào", CommandKind.Secondary),
            new("filter:karma", "⚖️ Nhân Quả", CommandKind.Secondary),
            new("filter:rumors", "Tin Đồn", CommandKind.Secondary)
        };

        var activeCells = espionage?.GetSpyNodes().Count(n => n.IntelLevel > 0) ?? 0;
        var acquiredTomes = espionage?.GetHermitTomes().Count(t => t.IsAcquired) ?? 0;

        var metrics = new List<ScreenMetric>
        {
            new("Mạng Lưới Tế Tác", $"{activeCells} thành trì", "Đài tình báo ngầm", MetricTone.Positive),
            new("Kỳ Thư Đã Lĩnh Hội", $"{acquiredTomes} / 5 bộ", "Tuyệt học danh sĩ", MetricTone.Positive),
            new("Thành Sở Tại", currentCitadelName, "Địa bàn hoạt động", MetricTone.Neutral)
        };

        return new ScreenModel(
            ScreenId,
            Title,
            $"Mật Thám Ty & Kỳ Thư Các · Đã thiết lập {activeCells} lưới tình báo và thu thập {acquiredTomes}/5 bộ Kỳ Thư.",
            sections,
            commands,
            metrics,
            Recommendation: "Cài cắm Tế Tác tại các đô thị lớn để tung tin đồn thao túng giá cả và tầm訪 các bậc ẩn sĩ cầu Kỳ Thư.");
    }
}
