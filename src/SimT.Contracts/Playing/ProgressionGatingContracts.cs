namespace SimT.Contracts.Playing;

/// <summary>
/// Status of an RPG feature gate requirement.
/// </summary>
public sealed record ProgressionGateStatus(
    bool IsUnlocked,
    string TitleVi,
    string ConditionVi,
    string AdviceVi);

/// <summary>
/// Standard RPG Progression Gating Engine.
/// Decouples feature exposure to prevent choice paralysis on Day 1,
/// guiding new players through natural progression tiers (Lữ Khách -> Phú Thương -> Quyền Quý).
/// </summary>
public static class ProgressionGating
{
    public static ProgressionGateStatus Check(string screenId, long daysPassed, long balanceQuan, int reputationTier = 1)
    {
        switch (screenId)
        {
            // Core starter screens (Always unlocked on Day 1)
            case "city.scene":
                return new(true, "Trong Thành", "Mở khóa", "Dạo phố, quán trọ và gặp gỡ nhân sĩ địa phương.");
            case "city.hub":
                return new(true, "Nhật Trình", "Mở khóa", "Tiêu điểm thời cuộc và cơ hội làm ăn trong ngày.");
            case "content.item_hub":
                return new(true, "Đồ Giám", "Mở khóa", "Tra cứu danh mục kỳ trân dị bảo toàn cõi Tam Quốc.");
            case "core.ledger":
                return new(true, "Sổ Cái", "Mở khóa", "Theo dõi dòng tiền thu chi minh bạch.");
            case "market.stall":
                return new(true, "Chợ Thành", "Mở khóa", "Giao thương mua bán hàng hóa theo cung cầu.");
            case "professions.trades":
                return new(true, "Nghề Nghiệp", "Mở khóa", "Làm công nhật (nông, tiều, ngư, canh) kiếm tiền sống qua ngày.");
            case "player.destiny":
                return new(true, "Mệnh Cách", "Mở khóa", "Cộng điểm thiên mệnh Bách Gia Chư Tử.");
            case "player.reputation":
                return new(true, "Danh Vọng", "Mở khóa", "Uy tín và thanh thế ở từng châu quận.");
            case "social.network":
                return new(true, "Nhân Duyên", "Mở khóa", "Mạng lưới quan hệ hào kiệt và thư từ.");
            case "npc.directory":
                return new(true, "Danh Sĩ", "Mở khóa", "Danh bạ nhân vật và anh hùng thời loạn.");
            case "society.chronicle":
                return new(true, "Thiên Hạ Niên Giám", "Mở khóa", "Theo dõi biến động và sự kiện toàn cõi.");

            // Advanced Gated Screens
            case "world.map":
                if (daysPassed >= 2 || balanceQuan >= 1200)
                    return new(true, "Thiên Hạ", "Mở khóa", "Bản đồ 20 thành trì và quan đạo.");
                return new(false, "Thiên Hạ", "Cần quen thuộc thành thị (qua 2 ngày) hoặc tích lũy ≥ 1.200 Quan lộ phí", "Hãy dạo phố hoặc buôn bán nhỏ trước khi dấn thân vào quan đạo hiểm trở.");

            case "market.player_stall":
                if (balanceQuan >= 1500)
                    return new(true, "Sạp Hàng", "Mở khóa", "Bày bán hàng hóa tư nhân tại chợ.");
                return new(false, "Sạp Hàng", "Cần Vốn ≥ 1.500 Quan để lập sạp riêng", "Kiếm thêm tiền từ chợ hoặc làm thuê công nhật.");

            case "market.warehouse":
                if (balanceQuan >= 2500)
                    return new(true, "Kho Bãi", "Mở khóa", "Thuê kho lưu trữ và bảo quản hàng hóa dài ngày.");
                return new(false, "Kho Bãi", "Cần Vốn ≥ 2.500 Quan để đóng phí thế chân và tiền thuê kho", "Tích lũy vốn từ các thương vụ buôn bán có lãi.");

            case "cultivation.farm":
                if (balanceQuan >= 2000)
                    return new(true, "Điền Trang", "Mở khóa", "Thuê đất đồn điền, mua nông cụ gieo hạt.");
                return new(false, "Điền Trang", "Cần Vốn ≥ 2.000 Quan để thuê điền mẫu và sắm nông cụ", "Gom đủ ngân lượng để bắt đầu nghiệp canh tác điền trang.");

            case "market.bank":
                if (balanceQuan >= 5000)
                    return new(true, "Tiền Trang", "Mở khóa", "Gửi tiết kiệm lấy lãi hoặc vay vốn làm ăn.");
                return new(false, "Tiền Trang", "Cần Vốn ≥ 5.000 Quan để mở sổ giao dịch Tiền Trang", "Chỉ thương gia có tài sản bảo chứng mới được giao dịch tại Tiền Trang.");

            case "caravan.routes":
                if (balanceQuan >= 4000)
                    return new(true, "Thương Đoàn", "Mở khóa", "Tổ chức đội ngũ vận chuyển hàng hóa đường xa.");
                return new(false, "Thương Đoàn", "Cần Vốn ≥ 4.000 Quan để thuê tiêu cục áp tải", "Cần quy mô vốn đủ lớn để chịu rủi ro sơn tặc và chi phí xa giá.");

            case "market.player_bank":
                if (balanceQuan >= 20000 && reputationTier >= 3)
                    return new(true, "Ngân Trang", "Mở khóa", "Mở ngân hiệu tư nhân, phát hành ngân phiếu.");
                return new(false, "Ngân Trang", "Cần Vốn ≥ 20.000 Quan và Danh vọng Cấp 3", "Đỉnh cao tài chính chỉ dành cho cự phú vang danh thiên hạ.");

            case "military.arena":
                if (daysPassed >= 5 || balanceQuan >= 2500)
                    return new(true, "Binh Nghiệp", "Mở khóa", "Đấu trường lôi đài và xuất quân tiễu phỉ.");
                return new(false, "Binh Nghiệp", "Cần trải qua 5 ngày rèn luyện hoặc Vốn ≥ 2.500 Quan trang bị", "Tập trung sinh tồn những ngày đầu trước khi bước lên võ đài sinh tử.");

            case "politics.court":
                if (daysPassed >= 10 || (balanceQuan >= 4000 && reputationTier >= 2))
                    return new(true, "Quan Trường", "Mở khóa", "Ứng thí quan phẩm và cống hiến triều đình.");
                return new(false, "Quan Trường", "Cần qua 10 ngày tích lũy hoặc Vốn ≥ 4.000 Quan & Danh vọng Cấp 2", "Triều đình không tiếp thứ dân vô danh.");

            case "lineage.family":
                if (balanceQuan >= 3000 && reputationTier >= 2)
                    return new(true, "Gia Tộc", "Mở khóa", "Cưới hỏi thê thiếp, sinh con kế tục cơ nghiệp.");
                return new(false, "Gia Tộc", "Cần Vốn ≥ 3.000 Quan và Danh vọng Cấp 2", "An cư mới có thể lập gia đình. Cần nền tảng vững vàng.");

            default:
                return new(true, screenId, "Mở khóa", string.Empty);
        }
    }
}
