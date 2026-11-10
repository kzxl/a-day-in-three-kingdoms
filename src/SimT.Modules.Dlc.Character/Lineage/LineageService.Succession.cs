using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class LineageService
{
    public const long PoliticalMarriageDowry = 250;

    public int CurrentGeneration =>
        1 + (int)_db.ScalarLong("SELECT COUNT(*) FROM ln_generation_log");

    public int TotalTariffDiscountPermille =>
        (int)Math.Min(250, _db.ScalarLong("SELECT COALESCE(SUM(tariff_discount_permille), 0) FROM ln_political_marriage"));

    public IReadOnlyList<PoliticalMarriageDef> PoliticalMarriages =>
        _db.Query(
            """
            SELECT m.child_id, COALESCE(c.name_vi, 'Tử tức'), m.faction_id, m.faction_name_vi,
                   m.spouse_title_vi, m.married_day_index, m.tariff_discount_permille, m.prestige_bonus
            FROM ln_political_marriage m
            LEFT JOIN ln_child c ON c.id = m.child_id
            ORDER BY m.married_day_index, m.child_id
            """,
            r => new PoliticalMarriageDef(
                r.GetInt64(0),
                r.GetString(1),
                r.GetInt64(2),
                r.GetString(3),
                r.GetString(4),
                r.GetInt32(5),
                r.GetInt32(6),
                r.GetInt32(7)));

    public IReadOnlyList<GenerationRecordDef> GenerationHistory =>
        _db.Query(
            """
            SELECT generation_number, patriarch_name_vi, heir_child_id, ascended_day_index, inherited_prestige, chronicle_vi
            FROM ln_generation_log
            ORDER BY generation_number
            """,
            r => new GenerationRecordDef(
                r.GetInt32(0),
                r.GetString(1),
                r.GetInt64(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetString(5)));

    public LineageResult ArrangePoliticalMarriage(long childId, long factionId)
    {
        var child = Children.FirstOrDefault(c => c.Id == childId);
        if (child is null)
        {
            return LineageResult.Failure("Không tìm thấy người con này trong gia phả.");
        }

        var existing = _db.ScalarLong("SELECT COUNT(*) FROM ln_political_marriage WHERE child_id = @cid", DbArgs.One("@cid", childId));
        if (existing > 0)
        {
            return LineageResult.Failure($"{child.NameVi} đã kết thân liên hôn với chư hầu trước đó rồi.");
        }

        if (_wallet.Balance < PoliticalMarriageDowry)
        {
            return LineageResult.Failure(
                $"Không đủ sính lễ liên hôn chư hầu (cần {PoliticalMarriageDowry} Quan, hiện có {_wallet.Balance} Quan).");
        }

        var (factionNameVi, spouseTitleVi, discountPermille, prestigeBonus) = ResolveFactionAlliance(factionId);
        var today = _clock.Today;

        _db.Transaction(() =>
        {
            EnsureShrineRow();
            _wallet.Charge(
                PoliticalMarriageDowry,
                JournalCodes.FamilyUpkeep,
                $"Sính lễ liên hôn thế gia cho {child.NameVi} với {factionNameVi}",
                $"alliance:{childId}:{factionId}:{today.DayIndex}");

            _db.Execute(
                """
                INSERT INTO ln_political_marriage (child_id, faction_id, faction_name_vi, spouse_title_vi, married_day_index, tariff_discount_permille, prestige_bonus)
                VALUES (@cid, @fid, @fname, @stitle, @day, @disc, @pres)
                """,
                new DbArgs()
                    .Add("@cid", childId)
                    .Add("@fid", factionId)
                    .Add("@fname", factionNameVi)
                    .Add("@stitle", spouseTitleVi)
                    .Add("@day", today.DayIndex)
                    .Add("@disc", discountPermille)
                    .Add("@pres", prestigeBonus));

            _db.Execute(
                "UPDATE ln_shrine SET clan_prestige = clan_prestige + @pres WHERE id = 1",
                DbArgs.One("@pres", prestigeBonus));

            if (_capabilities?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs != null)
            {
                attrs.GainFame(25, $"Kết thân liên hôn giữa {child.NameVi} và {spouseTitleVi} ({factionNameVi}).", virtueDelta: 5);
            }
        });

        _log.Info($"Arranged political marriage for child {child.NameVi} with faction {factionNameVi}");
        return LineageResult.Success($"[Liên Hôn Chư Hầu] {child.NameVi} đã thành thân cùng {spouseTitleVi} thuộc {factionNameVi} (-{PoliticalMarriageDowry} Quan). Gia uy +{prestigeBonus}, miễn giảm thuế quan thương mại +{discountPermille / 10.0:0.#}%!");
    }

    public LineageResult PassTorchToHeir()
    {
        var heir = Children.FirstOrDefault(c => c.IsHeir);
        if (heir is null)
        {
            return LineageResult.Failure("Gia tộc chưa lập Đích Tử Kế Vị. Hãy chọn một người con làm người kế vị trước khi truyền ngôi.");
        }

        var nextGen = CurrentGeneration + 1;
        var today = _clock.Today;
        var inheritedPrestige = ClanPrestige + 50;
        var focusStat = string.IsNullOrWhiteSpace(heir.FocusStat) ? "LDR" : heir.FocusStat!;
        var statBonus = Math.Max(3, heir.StatBonus + 2);
        var chronicleVi = $"Đời thứ {nextGen}: Tân Gia Chủ {heir.NameVi} kế thừa đại nghiệp vào {GameDateCodec.Format(today)}, tiếp nhận Gia Uy {inheritedPrestige} điểm và truyền thống {focusStat} (+{statBonus}).";

        _db.Transaction(() =>
        {
            EnsureShrineRow();

            _db.Execute(
                """
                INSERT INTO ln_generation_log (generation_number, patriarch_name_vi, heir_child_id, ascended_day_index, inherited_prestige, chronicle_vi)
                VALUES (@gen, @name, @cid, @day, @pres, @chronicle)
                """,
                new DbArgs()
                    .Add("@gen", nextGen)
                    .Add("@name", heir.NameVi)
                    .Add("@cid", heir.Id)
                    .Add("@day", today.DayIndex)
                    .Add("@pres", inheritedPrestige)
                    .Add("@chronicle", chronicleVi));

            _db.Execute("UPDATE ln_shrine SET clan_prestige = clan_prestige + 50 WHERE id = 1");

            if (_capabilities?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs != null)
            {
                attrs.AddExp(focusStat, statBonus * 40);
                attrs.AddExp("LDR", 60);
                attrs.RecoverVitals(100, 100, 100);
                attrs.GainFame(40, $"Đại lễ truyền ngôi Đời thứ {nextGen}: Tân Gia Chủ {heir.NameVi} chính thức chấp chưởng tông môn!", virtueDelta: 10);
            }
        });

        _log.Info($"Passed family torch to heir {heir.NameVi}: generation={nextGen}, prestige={inheritedPrestige}");
        return LineageResult.Success($"[Truyền Thừa Thế Gia — Đời Thứ {nextGen}] Tân Gia Chủ '{heir.NameVi}' đã bái tế Từ Đường, chính thức tiếp nhận ấn tín Gia Tộc! Khí huyết & thể lực hồi phục viên mãn, Gia Uy +50 (đạt {inheritedPrestige}), kế thừa tinh hoa {focusStat}!");
    }

    private void EnsureShrineRow()
    {
        _db.Execute(
            """
            INSERT INTO ln_shrine (id, level, clan_prestige, last_offering_day)
            VALUES (1, 1, 10, 0)
            ON CONFLICT(id) DO NOTHING
            """);
    }

    private static (string FactionNameVi, string SpouseTitleVi, int DiscountPermille, int PrestigeBonus) ResolveFactionAlliance(long factionId) =>
        factionId switch
        {
            1 => ("Thế Lực Tào Ngụy (Hứa Xương)", "Quận Chúa Họ Tào", 50, 40),
            2 => ("Thế Lực Thục Hán (Thành Đô)", "Khuê Nữ Hoàng Tộc Họ Lưu", 50, 45),
            3 => ("Thế Lực Đông Ngô (Kiến Nghiệp)", "Quận Chúa Giang Đông Họ Tôn", 60, 40),
            _ => ("Danh Môn Vọng Tộc Trung Nguyên", "Thiên Kim Thế Gia", 40, 35)
        };
}
