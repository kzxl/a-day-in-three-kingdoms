using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Character;

/// <summary>
/// Single source of truth for the 16 NPC character traits (Đợt 5 — Đặc tính nhân vật):
/// definitions, mutual exclusion rules, psyche eligibility conditions, canonical historical
/// assignments (~30 famous Three Kingdoms figures), deterministic seed-based assignment,
/// and trait-specific dialogue lines.
///
/// Rationale for deterministic on-the-fly computation (with in-memory caching) instead of a DB table:
/// 1. Works immediately on existing saves/snapshots without extra schema or backfill.
/// 2. Does not consume kernel IRng state during boot, preserving golden replay ledger hashes.
/// 3. Guarantees by construction that no NPC can ever hold mutually exclusive traits.
/// </summary>
public static class NpcTraitCatalog
{
    public static readonly IReadOnlyList<NpcTraitDef> AllTraits = new[]
    {
        new NpcTraitDef(
            NpcTraitCodes.ThamTai,
            "Tham tài",
            "Mắt sáng lên khi thấy vàng bạc, buôn bán luôn tính thiệt hơn từng đồng.",
            NpcTraitCodes.ThanhLiem),
        new NpcTraitDef(
            NpcTraitCodes.ThanhLiem,
            "Thanh liêm",
            "Giữ mình trong sạch, ghét thói đút lót và chẳng màng của cải phi nghĩa.",
            NpcTraitCodes.ThamTai),
        new NpcTraitDef(
            NpcTraitCodes.TrungNghia,
            "Trung nghĩa",
            "Một lòng son sắt với nghĩa khí, dù đầu rơi máu chảy cũng không hai lòng.",
            NpcTraitCodes.XaoQuyet),
        new NpcTraitDef(
            NpcTraitCodes.DaNghi,
            "Đa nghi",
            "Luôn phòng bị người xung quanh, nghe lời nói phải xét nét ba phần thực hư.",
            null),
        new NpcTraitDef(
            NpcTraitCodes.HaoSang,
            "Hào sảng",
            "Rộng rãi phóng khoáng, trọng tình bạn hơn tiền bạc, mua bán rất thoáng tay.",
            NpcTraitCodes.KeoKiet),
        new NpcTraitDef(
            NpcTraitCodes.KeoKiet,
            "Keo kiệt",
            "Giữ của như giữ mạng, hàng hóa trong kho luôn nhỏ giọt từng chút một.",
            NpcTraitCodes.HaoSang),
        new NpcTraitDef(
            NpcTraitCodes.HieuHoc,
            "Hiếu học",
            "Say mê kinh sử và trà đạo, gặp sách quý hay người tài đều hết mực trân trọng.",
            null),
        new NpcTraitDef(
            NpcTraitCodes.NongNay,
            "Nóng nảy",
            "Tính như lửa đốt, ra đòn cực mạnh nhưng dễ lộ sơ hở khi giao chiến.",
            NpcTraitCodes.DiemTinh),
        new NpcTraitDef(
            NpcTraitCodes.DiemTinh,
            "Điềm tĩnh",
            "Gặp biến không loạn, thủ thế vững vàng và suy xét kỹ lưỡng trước mọi nước đi.",
            NpcTraitCodes.NongNay),
        new NpcTraitDef(
            NpcTraitCodes.KieuNgao,
            "Kiêu ngạo",
            "Tự phụ tài năng hơn người, thường nhìn kẻ tầm thường bằng nửa con mắt.",
            NpcTraitCodes.KhiemCung),
        new NpcTraitDef(
            NpcTraitCodes.KhiemCung,
            "Khiêm cung",
            "Ăn nói nhún nhường lễ độ, luôn kính trọng người hiền tài không phân sang hèn.",
            NpcTraitCodes.KieuNgao),
        new NpcTraitDef(
            NpcTraitCodes.MeRuou,
            "Mê rượu",
            "Không ngày nào thiếu vò rượu ngon, ai biếu mỹ tửu ắt kết làm tri kỷ.",
            null),
        new NpcTraitDef(
            NpcTraitCodes.MeVo,
            "Mê võ",
            "Cả đời luyện võ nghệ, quý chuộng binh khí tốt và nể phục bậc cao thủ.",
            null),
        new NpcTraitDef(
            NpcTraitCodes.TrongChuTin,
            "Trọng chữ tín",
            "Lời nói tựa ngàn vàng, buôn bán hay giao ước đều giữ giá và đúng hẹn.",
            null),
        new NpcTraitDef(
            NpcTraitCodes.XaoQuyet,
            "Xảo quyệt",
            "Khéo dùng mưu sâu kế hiểm, ngoài miệng cười nói nhưng trong bụng tính toán.",
            NpcTraitCodes.TrungNghia),
        new NpcTraitDef(
            NpcTraitCodes.NhanHau,
            "Nhân hậu",
            "Thương xót bá tánh lầm than, lấy đức thu phục lòng người hơn dùng vũ lực.",
            null)
    };

    private static readonly Dictionary<string, NpcTraitDef> TraitByCode =
        AllTraits.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly (string A, string B)[] ExclusivePairs =
    {
        (NpcTraitCodes.ThamTai, NpcTraitCodes.ThanhLiem),
        (NpcTraitCodes.HaoSang, NpcTraitCodes.KeoKiet),
        (NpcTraitCodes.NongNay, NpcTraitCodes.DiemTinh),
        (NpcTraitCodes.KieuNgao, NpcTraitCodes.KhiemCung),
        (NpcTraitCodes.TrungNghia, NpcTraitCodes.XaoQuyet),
        (NpcTraitCodes.TrongChuTin, NpcTraitCodes.XaoQuyet),
        (NpcTraitCodes.ThamTai, NpcTraitCodes.HaoSang)
    };

    /// <summary>
    /// Fixed canonical traits for ~30 famous historical characters in Romance of the Three Kingdoms
    /// (plus base merchant thuong_nhan_a so baseline Luoyang medicine stall remains stable).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> FixedTraitsByNpcCode =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Thục Hán
            ["npc_0001"] = new[] { NpcTraitCodes.NhanHau, NpcTraitCodes.KhiemCung },                           // Lưu Bị
            ["npc_0002"] = new[] { NpcTraitCodes.NongNay, NpcTraitCodes.MeRuou, NpcTraitCodes.MeVo },          // Trương Phi
            ["npc_0003"] = new[] { NpcTraitCodes.TrungNghia, NpcTraitCodes.KieuNgao, NpcTraitCodes.MeVo },     // Quan Vũ
            ["npc_0024"] = new[] { NpcTraitCodes.TrungNghia, NpcTraitCodes.DiemTinh, NpcTraitCodes.KhiemCung },// Triệu Vân
            ["npc_0025"] = new[] { NpcTraitCodes.DiemTinh, NpcTraitCodes.HieuHoc, NpcTraitCodes.ThanhLiem },   // Gia Cát Lượng
            ["npc_0026"] = new[] { NpcTraitCodes.HieuHoc, NpcTraitCodes.MeRuou, NpcTraitCodes.HaoSang },       // Bàng Thống
            ["npc_0028"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.TrungNghia },                             // Hoàng Trung
            ["npc_0029"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.NongNay, NpcTraitCodes.KieuNgao },        // Mã Siêu
            ["npc_0030"] = new[] { NpcTraitCodes.KieuNgao, NpcTraitCodes.MeVo, NpcTraitCodes.NongNay },        // Ngụy Diên
            ["npc_0031"] = new[] { NpcTraitCodes.TrungNghia, NpcTraitCodes.HieuHoc, NpcTraitCodes.MeVo },      // Khương Duy
            ["npc_0032"] = new[] { NpcTraitCodes.HaoSang, NpcTraitCodes.TrongChuTin, NpcTraitCodes.TrungNghia },// Mi Trúc

            // Tào Ngụy
            ["npc_0022"] = new[] { NpcTraitCodes.DaNghi, NpcTraitCodes.XaoQuyet },                             // Tào Tháo
            ["npc_0035"] = new[] { NpcTraitCodes.ThanhLiem, NpcTraitCodes.TrungNghia, NpcTraitCodes.KhiemCung },// Tuân Úc
            ["npc_0037"] = new[] { NpcTraitCodes.MeRuou, NpcTraitCodes.HieuHoc, NpcTraitCodes.HaoSang },       // Quách Gia
            ["npc_0038"] = new[] { NpcTraitCodes.DiemTinh, NpcTraitCodes.XaoQuyet },                           // Giả Hủ
            ["npc_0039"] = new[] { NpcTraitCodes.DiemTinh, NpcTraitCodes.DaNghi, NpcTraitCodes.XaoQuyet },     // Tư Mã Ý
            ["npc_0040"] = new[] { NpcTraitCodes.DiemTinh, NpcTraitCodes.MeVo, NpcTraitCodes.TrungNghia },     // Trương Liêu
            ["npc_0041"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.NongNay, NpcTraitCodes.TrungNghia },      // Hứa Chử
            ["npc_0042"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.MeRuou, NpcTraitCodes.TrungNghia },       // Điển Vi

            // Đông Ngô
            ["npc_0014"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.TrungNghia },                             // Tôn Kiên
            ["npc_0048"] = new[] { NpcTraitCodes.HaoSang, NpcTraitCodes.MeVo, NpcTraitCodes.NongNay },         // Tôn Sách
            ["npc_0019"] = new[] { NpcTraitCodes.DiemTinh, NpcTraitCodes.DaNghi },                             // Tôn Quyền
            ["npc_0017"] = new[] { NpcTraitCodes.KieuNgao, NpcTraitCodes.HieuHoc },                            // Chu Du
            ["npc_0049"] = new[] { NpcTraitCodes.HaoSang, NpcTraitCodes.TrongChuTin, NpcTraitCodes.NhanHau },  // Lỗ Túc
            ["npc_0050"] = new[] { NpcTraitCodes.HieuHoc, NpcTraitCodes.MeVo },                                // Lữ Mông
            ["npc_0051"] = new[] { NpcTraitCodes.DiemTinh, NpcTraitCodes.KhiemCung, NpcTraitCodes.HieuHoc },   // Lục Tốn
            ["npc_0053"] = new[] { NpcTraitCodes.HaoSang, NpcTraitCodes.MeVo, NpcTraitCodes.MeRuou },          // Cam Ninh
            ["npc_0054"] = new[] { NpcTraitCodes.TrongChuTin, NpcTraitCodes.TrungNghia, NpcTraitCodes.MeVo },  // Thái Sử Từ

            // Quần Hùng & Khác
            ["npc_0057"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.KieuNgao, NpcTraitCodes.ThamTai },        // Lữ Bố
            ["npc_0023"] = new[] { NpcTraitCodes.ThamTai, NpcTraitCodes.NongNay, NpcTraitCodes.XaoQuyet },     // Đổng Trác
            ["npc_0018"] = new[] { NpcTraitCodes.KieuNgao, NpcTraitCodes.DaNghi },                             // Viên Thiệu
            ["npc_0059"] = new[] { NpcTraitCodes.ThamTai, NpcTraitCodes.KieuNgao, NpcTraitCodes.KeoKiet },     // Viên Thuật
            ["npc_0133"] = new[] { NpcTraitCodes.ThanhLiem, NpcTraitCodes.DiemTinh, NpcTraitCodes.TrungNghia },// Cao Thuận
            ["npc_0013"] = new[] { NpcTraitCodes.ThamTai, NpcTraitCodes.KeoKiet, NpcTraitCodes.XaoQuyet },     // Tả Phong
            ["npc_0095"] = new[] { NpcTraitCodes.MeVo, NpcTraitCodes.NongNay, NpcTraitCodes.KieuNgao },        // Hoa Hùng
            ["npc_0097"] = new[] { NpcTraitCodes.HieuHoc, NpcTraitCodes.DiemTinh, NpcTraitCodes.ThanhLiem },   // Thái Diễm
            ["npc_0224"] = new[] { NpcTraitCodes.ThanhLiem, NpcTraitCodes.DiemTinh, NpcTraitCodes.KhiemCung }, // Hoa Hân
            ["npc_0230"] = new[] { NpcTraitCodes.HaoSang, NpcTraitCodes.MeRuou, NpcTraitCodes.TrongChuTin },   // Tô Song

            // Thương nhân & chưởng quỹ gốc tại Lạc Dương / Trường An
            ["thuong_nhan_a"] = new[] { NpcTraitCodes.TrongChuTin, NpcTraitCodes.KhiemCung },
            ["quan_vien_a"] = new[] { NpcTraitCodes.HaoSang, NpcTraitCodes.MeRuou }
        };

    public static NpcTraitDef? FindByCode(string code) =>
        TraitByCode.TryGetValue(code, out var def) ? def : null;

    public static bool AreMutuallyExclusive(string codeA, string codeB)
    {
        if (string.Equals(codeA, codeB, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var (a, b) in ExclusivePairs)
        {
            if ((string.Equals(codeA, a, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(codeB, b, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(codeA, b, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(codeB, a, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Evaluates whether a trait is compatible with an NPC's psychological profile (NpcPsycheProfile).
    /// </summary>
    public static bool IsEligibleForPsyche(string traitCode, NpcPsycheProfile psyche, NpcDef? npc = null)
    {
        return traitCode switch
        {
            NpcTraitCodes.ThamTai => psyche.Materialism >= 60 && psyche.Integrity <= 72,
            NpcTraitCodes.ThanhLiem => psyche.Integrity >= 68 && psyche.Materialism <= 62,
            NpcTraitCodes.TrungNghia => psyche.Integrity >= 65,
            NpcTraitCodes.DaNghi => psyche.Ambition >= 55 && psyche.Integrity <= 80,
            NpcTraitCodes.HaoSang => psyche.Integrity >= 55 && psyche.Materialism <= 78,
            NpcTraitCodes.KeoKiet => psyche.Materialism >= 62 && psyche.Ambition <= 85,
            NpcTraitCodes.HieuHoc => psyche.Integrity >= 55 || (npc?.Attributes.Int ?? 50) >= 72,
            NpcTraitCodes.NongNay => psyche.Ambition >= 55 || (npc?.Attributes.War ?? 50) >= 75,
            NpcTraitCodes.DiemTinh => psyche.Integrity >= 55 || (npc?.Attributes.Int ?? 50) >= 70,
            NpcTraitCodes.KieuNgao => psyche.Ambition >= 62,
            NpcTraitCodes.KhiemCung => psyche.Integrity >= 60 && psyche.Ambition <= 82,
            NpcTraitCodes.MeRuou => psyche.Ambition >= 35 || psyche.Materialism >= 35,
            NpcTraitCodes.MeVo => psyche.Ambition >= 55 || (npc?.Attributes.War ?? 50) >= 70,
            NpcTraitCodes.TrongChuTin => psyche.Integrity >= 58,
            NpcTraitCodes.XaoQuyet => psyche.Ambition >= 60 && psyche.Integrity <= 68,
            NpcTraitCodes.NhanHau => psyche.Integrity >= 75 && psyche.Materialism <= 58,
            _ => true
        };
    }

    public static IReadOnlyList<NpcTraitDef> ResolveTraits(
        NpcDef? npc,
        NpcPsycheProfile psyche,
        long campaignSeed,
        long npcId)
    {
        if (npc is not null && FixedTraitsByNpcCode.TryGetValue(npc.Code, out var fixedCodes))
        {
            var fixedList = new List<NpcTraitDef>(fixedCodes.Length);
            foreach (var code in fixedCodes)
            {
                if (FindByCode(code) is { } def &&
                    fixedList.All(existing => !AreMutuallyExclusive(existing.Code, def.Code)))
                {
                    fixedList.Add(def);
                }
            }
            if (fixedList.Count > 0)
                return fixedList;
        }

        // Deterministic pool of eligible traits based on NpcPsycheProfile
        var candidates = AllTraits
            .Where(t => IsEligibleForPsyche(t.Code, psyche, npc))
            .ToList();

        if (candidates.Count == 0)
            candidates.AddRange(AllTraits);

        var state = Mix64((ulong)campaignSeed ^ ((ulong)npcId * 0x9E3779B97F4A7C15UL));
        if (npc is not null)
        {
            foreach (var ch in npc.Code)
                state = Mix64(state ^ ch);
        }

        // Target 2 or 3 traits (or 1-3) so masking ("???") is always meaningful on unmet NPCs
        var targetCount = 2 + (int)(Mix64(state + 17UL) % 2UL); // 2 or 3 traits
        var selected = new List<NpcTraitDef>(targetCount);

        var count = candidates.Count;
        var startIdx = (int)(state % (ulong)count);
        var step = (int)((Mix64(state + 31UL) % (ulong)Math.Max(1, count - 1)) + 1UL);

        for (var i = 0; i < count * 2 && selected.Count < targetCount; i++)
        {
            var idx = (startIdx + (i * step)) % count;
            var candidate = candidates[idx];
            if (selected.All(existing => !AreMutuallyExclusive(existing.Code, candidate.Code)))
            {
                selected.Add(candidate);
            }
        }

        if (selected.Count == 0)
            selected.Add(AllTraits[(int)(state % (ulong)AllTraits.Count)]);

        return selected;
    }

    private static ulong Mix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
