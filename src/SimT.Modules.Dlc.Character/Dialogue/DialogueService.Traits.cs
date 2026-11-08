using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class DialogueService
{
    /// <summary>
    /// Greeting and response lines for each of the 16 NPC character traits (at least 2 lines per trait, all &lt;= 90 chars).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> TraitDialogueLines =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [NpcTraitCodes.ThamTai] = new[]
            {
                "Thiên hạ đều vì chữ lợi, có mối buôn nào lời lớn cứ nói thẳng với ta.",
                "Đồng tiền đi liền khúc ruột, chuyện không ra bạc trắng thì chớ bàn dài."
            },
            [NpcTraitCodes.ThanhLiem] = new[]
            {
                "Làm người cốt ở hai chữ thanh bạch, bổng lộc bất nghĩa ta chẳng màng.",
                "Gió mát trăng thanh là đủ vui, xin chớ đem vàng ngọc làm vẩn đục tình giao hảo."
            },
            [NpcTraitCodes.TrungNghia] = new[]
            {
                "Trượng phu đứng giữa đất trời, một chữ Trung Nghĩa nặng tựa núi Thái Sơn.",
                "Đã nhận lời phó thác của bậc tri ngộ, dẫu thịt nát xương tan cũng chẳng phụ lòng."
            },
            [NpcTraitCodes.DaNghi] = new[]
            {
                "Lòng người hiểm ác khôn lường, lời các hạ nói ta nghe biết vậy chứ chưa tin ngay.",
                "Thời loạn tai vách mạch rừng, việc gì cũng phải phòng người ba bước mới an toàn."
            },
            [NpcTraitCodes.HaoSang] = new[]
            {
                "Bốn bể đều là huynh đệ, tiền tài như vật ngoài thân, hôm nay cứ thỏa sức đàm đạo!",
                "Bằng hữu ghé chơi là quý, chút hàng hóa bạc lẻ có đáng chi mà phải so đo từng đồng."
            },
            [NpcTraitCodes.KeoKiet] = new[]
            {
                "Năm tháng khó khăn, từng hạt gạo đồng tiền đều phải chắt chiu, phung phí là tội.",
                "Hàng trong kho ta giữ kỹ lắm, người ngoài hỏi mua chưa chắc ta đã chịu xuất nhiều."
            },
            [NpcTraitCodes.HieuHoc] = new[]
            {
                "Ba ngày không đọc sách thì vô vị, có cổ thư nào hay xin cùng mang ra bàn luận.",
                "Bên ấm trà thơm nghiền ngẫm kinh sử cổ kim, ấy mới là thú vui tao nhã nhất đời."
            },
            [NpcTraitCodes.NongNay] = new[]
            {
                "Có chuyện gì cứ nói mau cho thống khoái, vòng vo chỉ làm ta ngứa ngáy tay chân!",
                "Ra trận là phải vung đao chém thẳng về phía trước, rụt rè sao làm nên đại sự!"
            },
            [NpcTraitCodes.DiemTinh] = new[]
            {
                "Núi Thái Sơn sập trước mắt mà sắc mặt không đổi, ấy mới là bản lĩnh trí giả.",
                "Việc đời gấp gáp đến mấy cũng phải tĩnh tâm xét thế cục, nóng vội ắt hỏng việc."
            },
            [NpcTraitCodes.KieuNgao] = new[]
            {
                "Bọn giá áo túi cơm ngoài kia đông như kiến cỏ, mấy ai đủ tư cách luận bàn cùng ta.",
                "Tài nghệ bậc này vốn chẳng phải thứ kẻ tầm thường trong thiên hạ có thể vọng bàn."
            },
            [NpcTraitCodes.KhiemCung] = new[]
            {
                "Tại hạ tài mọn sức hèn, hôm nay được diện kiến cao nhân thực là may mắn ba sinh.",
                "Biển học mênh mông, mỗi người ta gặp đều có chỗ hơn mình đáng để cúi đầu học hỏi."
            },
            [NpcTraitCodes.MeRuou] = new[]
            {
                "Đời người mấy gang tay, có vò rượu ngon trước mặt thì chuyện gì cũng hóa nhẹ tênh!",
                "Ngửi mùi men nồng là sảng khoái, ai mang mỹ tửu tới đây chính là tri kỷ của ta!"
            },
            [NpcTraitCodes.MeVo] = new[]
            {
                "Ngày ngày mài đao luyện võ, chỉ mong gặp bậc cao thủ xứng tầm để phân cao thấp!",
                "Văn chương chữ nghĩa không bằng một đường thương dũng mãnh nơi chiến trường sinh tử."
            },
            [NpcTraitCodes.TrongChuTin] = new[]
            {
                "Một lời nói ra tứ mã nan truy, buôn bán hay kết giao đều lấy chữ Tín làm gốc.",
                "Giá cả ta đã định thì dù chợ ngoài kia biến động thế nào cũng quyết không nuốt lời."
            },
            [NpcTraitCodes.XaoQuyet] = new[]
            {
                "Binh bất yếm trá, thương trường hay chiến trường đều thuộc về kẻ tính trước một nước.",
                "Lời thật giả lẫn lộn mới là đạo sinh tồn thời loạn, các hạ đoán xem ta nghĩ gì?"
            },
            [NpcTraitCodes.NhanHau] = new[]
            {
                "Nhìn bá tánh lầm than vì khói lửa can qua, lòng ta đêm ngày chẳng thể yên giấc.",
                "Lấy sức ép người chỉ được cái xác, lấy nhân đức cảm hóa mới thu phục được lòng dân."
            }
        };

    public static IReadOnlyList<string> GetTraitLines(string traitCode) =>
        TraitDialogueLines.TryGetValue(traitCode, out var lines) ? lines : Array.Empty<string>();

    private void AppendTraitLinesForNpc(long npcId, string tierKey, List<string> targetLines)
    {
        if (!_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) || social is null)
            return;

        var traits = social.GetTraits(npcId);
        if (traits.Count == 0)
            return;

        var lineIdx = string.Equals(tierKey, "stranger", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        var primaryLines = GetTraitLines(traits[0].Code);
        if (primaryLines.Count > 0)
        {
            var chosen = ClampLine90(primaryLines[Math.Min(lineIdx, primaryLines.Count - 1)]);
            if (!targetLines.Contains(chosen, StringComparer.Ordinal))
                targetLines.Add(chosen);
        }

        if (!string.Equals(tierKey, "stranger", StringComparison.OrdinalIgnoreCase) && traits.Count > 1)
        {
            var secondaryLines = GetTraitLines(traits[1].Code);
            if (secondaryLines.Count > 0)
            {
                var chosenSecondary = ClampLine90(secondaryLines[0]);
                if (!targetLines.Contains(chosenSecondary, StringComparer.Ordinal))
                    targetLines.Add(chosenSecondary);
            }
        }
    }
}
