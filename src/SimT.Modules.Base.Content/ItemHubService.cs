using SimT.Contracts.Playing;

namespace SimT.Modules.Base.Content;

/// <summary>
/// Central item information, lore codex, and upgrade roadmap service owned by <c>base.content</c>.
/// Provides comprehensive specifications, ASCII illustrations, lore, sourcing, usage guides,
/// and future upgrade trajectories for all 24+ historical commodities, supplies, and tools.
/// </summary>
public sealed class ItemHubService : IItemHubService
{
    private readonly IItemCatalog _catalog;
    private readonly Dictionary<long, ItemHubEntry> _entriesById = new();
    private readonly Dictionary<string, ItemHubEntry> _entriesByCode = new(StringComparer.OrdinalIgnoreCase);

    public ItemHubService(IItemCatalog catalog)
    {
        _catalog = catalog;
        BootstrapCodexEntries();
    }

    public IReadOnlyList<ItemHubEntry> Entries => _entriesById.Values.OrderBy(e => e.Item.Id).ToList();

    public ItemHubEntry? FindEntry(long itemId) => _entriesById.GetValueOrDefault(itemId);

    public ItemHubEntry? FindEntryByCode(string code) => _entriesByCode.GetValueOrDefault(code);

    public IReadOnlyList<ItemHubEntry> FilterByCategory(string? categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey) || categoryKey.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Entries;
        }

        return categoryKey.ToLowerInvariant() switch
        {
            "tool" or "nong_cu" => Entries.Where(e => e.Item.Category == "tool").ToList(),
            "seed" or "vat_tu" => Entries.Where(e => e.Item.Category is "seed" or "supply").ToList(),
            "farming" or "nong_nghiep" => Entries.Where(e => e.Item.Category is "tool" or "seed" or "supply").ToList(),
            "grain" or "luong_thuc" => Entries.Where(e => e.Item.Category == "grain").ToList(),
            "food" or "thuc_pham" => Entries.Where(e => e.Item.Category is "food" or "grain").ToList(),
            "medicine" or "duoc_lieu" => Entries.Where(e => e.Item.Category == "medicine").ToList(),
            "material" or "khoang_san" => Entries.Where(e => e.Item.Category == "material").ToList(),
            "luxury" or "xa_xi" => Entries.Where(e => e.Item.Category == "luxury").ToList(),
            var cat => Entries.Where(e => e.Item.Category.Equals(cat, StringComparison.OrdinalIgnoreCase)).ToList()
        };
    }

    public void RegisterCustomEntry(ItemHubEntry entry)
    {
        _entriesById[entry.Item.Id] = entry;
        _entriesByCode[entry.Item.Code] = entry;
    }

    private void BootstrapCodexEntries()
    {
        foreach (var item in _catalog.All)
        {
            var entry = CreateDefaultEntry(item);
            _entriesById[item.Id] = entry;
            _entriesByCode[item.Code] = entry;
        }
    }

    private static ItemHubEntry CreateDefaultEntry(ItemDef item) => item.Code switch
    {
        // 1. Gạo
        "gao" => new ItemHubEntry(
            item,
            "Lương Thực Thiết Yếu",
            "🍚",
            """
            ┌──────────────────────────────────────────────┐
            │  🌾🌾🌾       [ BẠCH MỄ ĐẠI HOÀNG ]      🌾🌾🌾 │
            │      \ | /                                   │
            │    --( 🌾 )--   "Binh mã vị động,            │
            │      / | \       lương thảo tiên hành."      │
            │     [ === ]     Hạt ngọc trời nuôi trăm họ.  │
            └──────────────────────────────────────────────┘
            """,
            "Gạo tẻ trắng ngần, lương thảo căn bản nuôi sống bách tính và quân đội khắp mười ba châu quận. Trữ lượng gạo quyết định sự thịnh suy và sĩ khí của châu thành.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Lương thực khô (Grain)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày (hư hao dần nếu quá hạn)",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Công dụng chế biến"] = "Nấu Cơm Nắm hành quân (-15 mệt mỏi), nấu cháo cứu trợ dân đói",
                ["Yêu cầu kho bãi"] = "Thương khố khô ráo, tránh ẩm mốc và chuột bọ"
            },
            "Gieo cấy và thu hoạch tại các cánh đồng điền trang (nông tang); mua tại chợ lương thực hoặc các đoàn buôn thương nhân.",
            "Dùng làm lương thực tích trữ, nấu nướng chế biến món ăn phục hồi thể lực, giao dịch buôn bán kiếm chênh lệch giá giữa các thành, tiếp tế quân lương khi hành quân xa.",
            "Tương lai: Mở rộng chế tạo rượu nếp tiến vua, bột ngũ cốc cao cấp tăng gấp bội thể lực, nâng cấp hệ thống kho sấy chống mốc."),

        // 2. Muối
        "muoi" => new ItemHubEntry(
            item,
            "Quân Nhu & Gia Vị Trọng Yếu",
            "🧂",
            """
            ┌──────────────────────────────────────────────┐
            │  🧂🧂🧂       [ BẠCH DIÊM QUAN DOANH ]   🧂🧂🧂 │
            │     . : * : .                                │
            │    [  DIÊM  ]   Tài nguyên độc quyền quốc gia│
            │     ' : . : '   Thiếu muối, cơ thể rã rời,   │
            │     ~~~~~~~~~   quân sĩ mất sạch nhuệ khí.   │
            └──────────────────────────────────────────────┘
            """,
            "Bạch diêm hạt tinh khiết kết tinh từ ruộng muối ven biển và mỏ muối mặn. Là huyết mạch kinh tế của các triều đại phong kiến, từng là mặt hàng đánh thuế cao nhất.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Khoáng vị thiết yếu",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày (rất bền, chống ẩm tốt)",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Đặc tính"] = "Bảo quản thực phẩm tươi sống, chống ươn thối cá thịt"
            },
            "Khai thác từ các mỏ muối duyên hải (như Hạ Bì, Quảng Lăng) hoặc mua sỉ từ các tiệm quan diêm.",
            "Làm gia vị nấu nướng, ướp cá khô dự trữ, làm quà bang giao, hoặc kinh doanh liên thành kiếm địa lợi.",
            "Tương lai: Khả năng chế tạo muối tinh cao cấp dùng trong y học giải độc, muối ướp thịt khô hành quân đường trường."),

        // 3. Trà
        "tra" => new ItemHubEntry(
            item,
            "Danh Trà & Xa Xỉ Phẩm",
            "🍵",
            """
            ┌──────────────────────────────────────────────┐
            │  🍃🍃🍃       [ VŨ DI DANH TRÀ ]         🍃🍃🍃 │
            │       🌿                                     │
            │     ( 🍵 )      "Hương thơm thanh khiết,     │
            │      ¯¯¯         khơi mở trí tuệ danh sĩ."   │
            │    ~ ~ ~ ~      Vật phẩm kết giao tao nhã.   │
            └──────────────────────────────────────────────┘
            """,
            "Lá trà búp non hái từ sườn núi mờ sương miền Giang Nam. Thức uống thanh tao của văn nhân mặc khách, tướng lĩnh và hào môn quý tộc.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Hàng phẩm thanh tao (Luxury)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị (nhẹ, tiện mang theo)",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Hiệu ứng dự kiến"] = "Thưởng trà định tâm, giảm căng thẳng (Stress), tăng tư duy mưu lược"
            },
            "Trồng trọt tại điền trang bằng Hạt Giống Trà; hoặc thu mua tại các vùng thổ sản Giang Nam.",
            "Đàm đạo ẩm trà cùng danh sĩ NPC để gia tăng thiện cảm (+Quan hệ hữu hảo), hoặc chất lên xe ngựa của thương đội bán sang vùng Tây Bắc với giá cao.",
            "Tương lai: Xây dựng trà lâu, sao chế trà tiến vua, thưởng trà luận kiếm khai mở đan đạo."),

        // 4. Lụa
        "lua" => new ItemHubEntry(
            item,
            "Tơ Lụa & May Mặc Quý Tộc",
            "🧶",
            """
            ┌──────────────────────────────────────────────┐
            │  ✨✨✨       [ GIANG NAM THIÊN TƠ ]     ✨✨✨ │
            │      ╭───╮                                   │
            │      │ 🧶│      Mịn màng như ngọc,           │
            │      ╰───╯      óng ả như nước mùa thu.      │
            │     =======     Phẩm vật giao thương quốc tế.│
            └──────────────────────────────────────────────┘
            """,
            "Tơ lụa dệt từ kén tằm ăn lá dâu tươi. Chất vải nhẹ tênh, mịn màng, là trang phục của bậc vương hầu khanh tướng và là tiền tệ trao đổi trên Con Đường Tơ Lụa.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Tơ lụa quý phái (Luxury)",
                ["Hạn lưu kho"] = "Vĩnh viễn (không ôi thiu)",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Ứng dụng dệt may"] = "Nguyên liệu may trang phục hộ thân, cẩm bào quý tộc"
            },
            "Trồng cây giống dâu tằm trên ruộng điền trang để thu hoạch Lụa thô; hoặc thu mua tại xưởng dệt.",
            "Dùng may trang phục cao cấp tăng Uy Danh và Ngoại Giao; cống nạp triều đình; hoặc chất lên lạc đà buôn đường dài.",
            "Tương lai: Dệt thành Ngự Y Tơ Tằm kháng tiễn thuật, thêu chiến kỳ ba quân."),

        // 5. Sắt
        "sat" => new ItemHubEntry(
            item,
            "Khoáng Thạch Binh Giáp",
            "⚙️",
            """
            ┌──────────────────────────────────────────────┐
            │  ⚒️⚒️⚒️       [ HUYỀN THIẾT NGUYÊN KHỐI ] ⚒️⚒️⚒️ │
            │      ╭───╮                                   │
            │      │ ⚙️│      Cương thiết luyện từ lò than,│
            │      ╰───╯      đúc nên vạn mũi giáo gươm.   │
            │     =======     Cội nguồn sức mạnh sa trường.│
            └──────────────────────────────────────────────┘
            """,
            "Thỏi sắt tinh luyện từ quặng sắt mỏ núi. Xương sống của nền quân sự Tam Quốc, dùng rèn đao kiếm, giáo mác, giáp trụ và công cụ sản xuất.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Kim loại luyện kim (Material)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị (rất nặng)",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Ứng dụng thợ rèn"] = "Rèn đúc binh khí (Đao, Kiếm, Thương, Cung), đúc nông cụ sắt"
            },
            "Khai thác từ các mỏ sắt lớn (Hà Bắc, Duyện Châu) hoặc tinh luyện qua nghề thợ rèn.",
            "Chế tạo trang bị chiến đấu cho bản thân và gia binh; cung cấp cho các xưởng vũ khí quân giới.",
            "Tương lai: Tinh luyện thành Bách Luyện Cương, Đao Thép Hoa Vân tôi sắc bén tuyệt luân."),

        // 6. Gỗ
        "go" => new ItemHubEntry(
            item,
            "Vật Liệu Kiến Trúc & Xe Cộ",
            "🪵",
            """
            ┌──────────────────────────────────────────────┐
            │  🪵🪵🪵       [ THIẾT MỘC ĐẠI THỤ ]      🪵🪵🪵 │
            │      /───\                                   │
            │     | 🪵  |     Gỗ quý đốn từ rừng thâm sơn, │
            │      \───/      dựng lầu gác, đóng chiến xa. │
            │     =======     Cơ bản của mọi công trình.   │
            └──────────────────────────────────────────────┘
            """,
            "Gỗ sồi, thiết mộc bền bỉ khai thác từ những cánh rừng nguyên sinh. Vật liệu không thể thiếu để dựng dinh thự, xây điền trang, đóng xe ngựa thương nhân và chiến thuyền.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Vật liệu kiến thiết (Material)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Ứng dụng thợ mộc"] = "Chế tạo xe ngựa chở hàng, nâng cấp kho lương, xây cầu cống"
            },
            "Khai thác từ nghề tiều phu đốn củi/lâm nghiệp hoặc thu mua tại các thương điếm Lâm Mộc.",
            "Dùng đóng xe ngựa gia tăng tải trọng thương đội; cung ứng vật liệu xây dựng xưởng thợ.",
            "Tương lai: Đóng thuyền buôn đường sông, xe chiến mã xa bọc thép, cung nỏ tầm xa."),

        // 7. Thuốc
        "thuoc" => new ItemHubEntry(
            item,
            "Thảo Dược Trị Liệu",
            "💊",
            """
            ┌──────────────────────────────────────────────┐
            │  🌿🌿🌿       [ BÁCH THẢO HOÀN ]         🌿🌿🌿 │
            │       [+]                                    │
            │      ┌───┐      "Dược tính ôn hòa,           │
            │      │ 🌿│       cứu tử phò thương."         │
            │      └───┘      Thần dược nơi biên thùy.     │
            └──────────────────────────────────────────────┘
            """,
            "Thang thuốc bào chế từ nhiều vị thảo dược quý theo bài thuốc cổ truyền. Có công hiệu cầm máu, giải độc, trị ngoại thương và nâng cao thể trạng mau chóng.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Dược liệu trị thương (Medicine)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Hiệu quả tức thời"] = "Phục hồi sinh lực (HP), chữa vết thương sau giao tranh tỷ võ"
            },
            "Trồng trọt thu hoạch từ ruộng Dược Thảo; hoặc mua tại các y quán danh y.",
            "Sử dụng khi bị thương tích trong chiến đấu hoặc tỷ võ; cứu trợ dân chúng khi xảy ra dịch bệnh.",
            "Tương lai: Bào chế các loại Linh Đan Diệu Dược tăng vĩnh viễn căn cốt (CON, WAR, INT)."),

        // 8. Cá khô
        "ca_kho" => new ItemHubEntry(
            item,
            "Lương Khô Hành Quân",
            "🐟",
            """
            ┌──────────────────────────────────────────────┐
            │  🐟🐟🐟       [ HẢI VỊ DIÊM NGƯ ]        🐟🐟🐟 │
            │      ><(((('>                                │
            │     [ ỨỚP MUỐI ] Cá phơi khô tẩm muối mặn,   │
            │      <'))))><   lương thực dã ngoại dặm dài. │
            └──────────────────────────────────────────────┘
            """,
            "Cá tươi sông hồ ướp muối kỹ lưỡng rồi phơi nắng trên giàn tre. Dễ dàng mang theo nhiều ngày đường mà không sợ ôi thiu.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Thực phẩm bảo quản (Food)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Hành quân"] = "Cung ứng khẩu phần ăn dự phòng trên đường dài"
            },
            "Đánh bắt chế biến ven biển hoặc mua tại bến cảng sông nước.",
            "Làm lương khô vượt ải đường xa, giảm tiêu hao thể lực trên đường buôn.",
            "Tương lai: Chế biến thành đồ hộp lương thực quân đội quy mô lớn."),

        // 9. Lúa mì
        "lua_mi" => new ItemHubEntry(
            item,
            "Lương Thực Miền Bắc",
            "🌾",
            """
            ┌──────────────────────────────────────────────┐
            │  🌾🌾🌾       [ BẮC ĐỊA MẠCH LƯƠNG ]     🌾🌾🌾 │
            │      ~~~~~~~                                 │
            │     ( 🌾🌾 )    Bông lúa mì trĩu hạt         │
            │      ~~~~~~~    chịu sương gió thảo nguyên.  │
            └──────────────────────────────────────────────┘
            """,
            "Mạch lương cứng cáp sinh trưởng tốt ở vùng đất khô cằn miền Bắc. Xay thành bột làm bánh bao, lương khô cung ứng quân phòng thủ biên thùy.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Lương thực khô (Grain)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan"
            },
            "Mua bán tại các thành trấn Trung Nguyên và phương Bắc.",
            "Dùng làm lương thực dự trữ, xay bột bánh nướng hành quân.",
            "Tương lai: Nướng bánh lương khô chịu hạn cực cao."),

        // 10. Rượu nếp
        "ruou" => new ItemHubEntry(
            item,
            "Mỹ Tửu Ẩm Thực",
            "🍶",
            """
            ┌──────────────────────────────────────────────┐
            │  🍶🍶🍶       [ ĐỖ KHANG MỸ TỬU ]        🍶🍶🍶 │
            │       _==_                                   │
            │      /    \     "Hà dĩ giải ưu?              │
            │     | RƯỢU |     Duy hữu Đỗ Khang."          │
            │      \____/     Ấm huyết, trừ hàn, hào khí.  │
            └──────────────────────────────────────────────┘
            """,
            "Rượu nếp cái hoa vàng ủ trong chum sành chôn dưới lòng đất nhiều năm. Rượu thơm nồng ấm, giúp tăng sĩ khí và kết giao huynh đệ giang hồ.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Thực phẩm đồ uống (Food)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Hiệu ứng ẩm thực"] = "Giảm căng thẳng (Stress), tăng dũng khí tỷ võ"
            },
            "Ủ từ gạo nếp hoặc mua tại tửu điếm các châu thành.",
            "Mời rượu hảo hữu nâng cao độ thân mật; thiết đãi yến tiệc hào kiệt.",
            "Tương lai: Ủ rượu thượng phẩm tăng cường điểm công kích bạo kích."),

        // 11. Vải thô
        "vai" => new ItemHubEntry(
            item,
            "Vải Bố Bình Dân",
            "🧵",
            """
            ┌──────────────────────────────────────────────┐
            │  🧵🧵🧵       [ MA BỐ DÂN GIA ]          🧵🧵🧵 │
            │      ╭───╮                                   │
            │      │ 🧵│      Dệt từ sợi gai mộc mạc,      │
            │      ╰───╯      bền chắc che nắng mưa.       │
            └──────────────────────────────────────────────┘
            """,
            "Vải dệt từ cây gai dầu bền bỉ, giá thành bình dân. Dùng may trang phục cho bá tánh và làm bao bì đóng gói hàng hóa.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Vải sợi thông dụng (Material)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan"
            },
            "Dệt từ sợi gai hoặc mua tại phường thợ may.",
            "May túi bọc hàng hóa, lều bạt dã chiến.",
            "Tương lai: May đồng phục quân lính tân binh."),

        // 12. Gấm Thục
        "gam" => new ItemHubEntry(
            item,
            "Cực Phẩm Dệt Thục Cẩm",
            "✨",
            """
            ┌──────────────────────────────────────────────┐
            │  ✨✨✨       [ THỤC QUẬN CẨM HOA ]      ✨✨✨ │
            │     * * * *                                  │
            │    [ THỤC CẨM ] Hàng ngàn mũi kim thêu hoa,  │
            │     * * * *     quốc bảo xứ Ba Thục.         │
            └──────────────────────────────────────────────┘
            """,
            "Loại gấm trứ danh vùng Thục Quận với hoa văn tinh xảo như mây ngũ sắc. Giá trị sánh ngang vàng bạc, mặt hàng chiến lược làm giàu cho nhà Thục.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Tuyệt phẩm cẩm tú (Luxury)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan (rất cao)",
                ["Cống phẩm"] = "Quà tặng ngoại giao cấp cao nhất cho các quan chức triều đình"
            },
            "Mua tại Thành Đô / Ích Châu hoặc từ các thương buôn lớn.",
            "Làm quà tặng triều đình để nâng cao Tước Vị và Uy Danh chính trị.",
            "Tương lai: May hoàng bào, cẩm y vệ giáp trụ."),

        // 13. Nhân sâm
        "sam" => new ItemHubEntry(
            item,
            "Linh Dược Trường Thọ",
            "🌿",
            """
            ┌──────────────────────────────────────────────┐
            │  🌿🌿🌿       [ TRƯỜNG BẠCH DÃ SÂM ]     🌿🌿🌿 │
            │        *                                     │
            │      \ | /      Hấp thu linh khí trời đất,   │
            │      -(*)-      nghìn năm đúc nên linh căn.  │
            │       /|\       Đại bổ khí huyết, khởi tử.   │
            └──────────────────────────────────────────────┘
            """,
            "Củ sâm rừng lâu năm đào từ chốn thâm sơn Trường Bạch. Dược liệu chí bảo có thể cứu người trong cơn thập tử nhất sinh, bồi bổ nguyên khí thần kỳ.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Thượng phẩm linh dược (Medicine)",
                ["Hạn lưu kho"] = $"{item.ShelfLifeDays} ngày",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Đặc tính"] = "Hồi phục toàn bộ sinh lực, gia tăng tuổi thọ và sinh khí"
            },
            "Tìm thấy ở rừng sâu U Châu / Liêu Đông hoặc mua ở danh y lầu.",
            "Dùng cấp cứu khi trọng thương hoặc biếu tặng bậc lão thành.",
            "Tương lai: Luyện đan Dưỡng Mệnh Đan giúp sống thọ."),

        // 14. Trầm hương
        "tram_huong" => new ItemHubEntry(
            item,
            "Hương Mộc Tế Lễ",
            "🪵",
            """
            ┌──────────────────────────────────────────────┐
            │  ✨✨✨       [ NAM HẢI TRẦM HƯƠNG ]     ✨✨✨ │
            │       ( )                                    │
            │      (   )      Khói hương nghi ngút thấu trời│
            │     [ TRẦM ]    tâm hồn an tịnh, trừ tà khí. │
            └──────────────────────────────────────────────┘
            """,
            "Khối gỗ trầm tích tụ tinh hoa nhựa thơm qua hàng trăm năm bão gió. Mùi hương ngạt ngào thanh tịnh dùng trong đàn tế tế tự hoàng gia.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Hương liệu cúng tế (Luxury)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan"
            },
            "Giao thương với vùng Lĩnh Nam, Giao Châu.",
            "Dâng tế gia miếu tế đàn hoặc dâng tấu thư triều đình.",
            "Tương lai: Thắp trầm tăng tốc độ tu tâm và giác ngộ."),

        // 15. Than đá
        "than_da" => new ItemHubEntry(
            item,
            "Nhiên Liệu Luyện Kim",
            "🪨",
            """
            ┌──────────────────────────────────────────────┐
            │  🪨🪨🪨       [ U MINH THẠCH THÁN ]      🪨🪨🪨 │
            │      ╭───╮                                   │
            │      │ 🪨│      Nhiệt lượng cực đại,         │
            │      ╰───╯      thổi bùng ngọn lửa lò đúc.   │
            └──────────────────────────────────────────────┘
            """,
            "Khoáng thạch đen nhánh cháy với nhiệt lượng cực cao. Nhiên liệu lý tưởng để thổi lò rèn luyện kim khí chất lượng cao.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Nhiên liệu khoáng thạch (Material)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan"
            },
            "Khai thác mỏ than sâu dưới lòng đất.",
            "Đốt lò thợ rèn đúc vũ khí cao cấp.",
            "Tương lai: Luyện thép gió, đúc đại pháo."),

        // 16. Đồng thau
        "dong" => new ItemHubEntry(
            item,
            "Kim Loại Đúc Tiền & Cổ Vật",
            "🪙",
            """
            ┌──────────────────────────────────────────────┐
            │  🪙🪙🪙       [ THANH ĐỒNG TINH LUYỆN ]  🪙🪙🪙 │
            │      ╭───╮                                   │
            │      │ 🪙│      Đúc đỉnh, đúc tiền ngũ thù,  │
            │      ╰───╯      chạm khắc bảo khí vương quyền│
            └──────────────────────────────────────────────┘
            """,
            "Thỏi đồng thanh thau dẻo dai sáng bóng. Nguyên liệu chính đúc tiền kim loại lưu thông và đúc đỉnh ngọc tế lễ thời cổ đại.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Kim loại quý dân dụng (Material)",
                ["Hạn lưu kho"] = "Vĩnh viễn",
                ["Khối lượng"] = $"{item.Weight} cân / đơn vị",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan"
            },
            "Khai thác từ các mỏ đồng phía Nam.",
            "Đúc tiền tệ, đúc gương đồng, đúc tượng bảo vật.",
            "Tương lai: Đúc chuông đồng báo động, đúc ấn tín."),

        // 17. Hạt giống lúa
        "hat_giong_lua" => new ItemHubEntry(
            item,
            "Hạt Giống Nông Tang",
            "🌱",
            """
            ┌──────────────────────────────────────────────┐
            │  🌱🌱🌱       [ HẠT GIỐNG LÚA NƯỚC ]     🌱🌱🌱 │
            │       \ | /                                  │
            │     --( 🌱 )--  Gieo vào mùa xuân,           │
            │       / | \     thu hoạch mùa thu.           │
            │      [ === ]    1 mẫu ruộng = 1 bao giống.   │
            └──────────────────────────────────────────────┘
            """,
            "Hạt thóc mẩy đều được sàng sảy cẩn thận, hạt nào hạt nấy tròn trịa vàng ươm. Mầm sống của những vụ mùa trĩu hạt.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Hạt giống canh tác (Seed)",
                ["Thời gian sinh trưởng"] = "18 - 30 ngày (tùy mùa và mương máng)",
                ["Sản phẩm đầu ra"] = "Gạo (8 đơn vị / mẫu)",
                ["Tiêu hao độ phì"] = "-25 độ phì đất sau khi gặt",
                ["Công cụ bắt buộc"] = "Cuốc nông nghiệp (cuoc_nong) để cày xới",
                ["Giá mua sỉ"] = $"{item.BasePrice} Quan / bao"
            },
            "Mua trực tiếp tại kho Điền Trang, tiệm hạt giống hoặc chợ phiên.",
            "Gieo trồng trên các khu ruộng đã thuê. Mỗi mẫu ruộng tiêu hao đúng 1 bao giống.",
            "Tương lai: Lai tạo giống lúa kháng hạn, năng suất tăng vọt lên 12 Gạo/mẫu."),

        // 18. Hạt giống trà
        "hat_giong_tra" => new ItemHubEntry(
            item,
            "Hạt Giống Cây Trà",
            "🍃",
            """
            ┌──────────────────────────────────────────────┐
            │  🍃🍃🍃       [ MẦM TRÀ SƠN ĐỊA ]        🍃🍃🍃 │
            │        🌿                                    │
            │      ( 🌱 )     Hạt mầm cây trà sương mù,    │
            │       ¯¯¯       thích hợp sườn đồi thoai thoải│
            └──────────────────────────────────────────────┘
            """,
            "Hạt giống thu từ các cây trà cổ thụ sườn núi cao. Thích hợp gieo trồng ở các vùng đất đỏ màu mỡ.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Hạt giống nông sản (Seed)",
                ["Thời gian sinh trưởng"] = "24 ngày",
                ["Sản phẩm đầu ra"] = "Trà (2 đơn vị / mẫu)",
                ["Công cụ bắt buộc"] = "Cuốc nông nghiệp (cuoc_nong)",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan / bao"
            },
            "Mua tại Điền Trang hoặc chợ châu thành vùng Giang Nam.",
            "Gieo trên ruộng điền trang để thu hoạch lá trà búp non.",
            "Tương lai: Trồng vườn trà chuyên canh tăng sản lượng Trà Tuyết."),

        // 19. Cây giống dâu tằm
        "hat_giong_dau_tam" => new ItemHubEntry(
            item,
            "Cây Giống Dâu Tằm",
            "🐛",
            """
            ┌──────────────────────────────────────────────┐
            │  🌱🌱🌱       [ TANG THỤ CHI MIÊU ]      🌱🌱🌱 │
            │       \ | /                                  │
            │     --( 🌿 )--  Dâu xanh tốt nuôi tằm nhả tơ,│
            │       / | \     cơ sở dệt lụa ngàn dặm.      │
            └──────────────────────────────────────────────┘
            """,
            "Cành dâu giống mập mạp chớm nảy chồi xanh. Trồng dâu nuôi tằm dệt lụa là nghề truyền thống mang lại phú quý ngập tràn.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Cây giống nông nghiệp (Seed)",
                ["Thời gian sinh trưởng"] = "30 ngày",
                ["Sản phẩm đầu ra"] = "Lụa (2 đơn vị / mẫu)",
                ["Công cụ bắt buộc"] = "Cuốc nông nghiệp (cuoc_nong)",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan / cây"
            },
            "Mua tại Điền Trang hoặc phường ươm giống.",
            "Gieo trồng để thu hoạch tơ lụa thô.",
            "Tương lai: Nâng cấp lồng tằm tự động hóa trong điền trang."),

        // 20. Hạt giống dược thảo
        "hat_giong_duoc_thao" => new ItemHubEntry(
            item,
            "Hạt Giống Dược Thảo",
            "🧪",
            """
            ┌──────────────────────────────────────────────┐
            │  🌱🌱🌱       [ BÁCH DƯỢC THẢO CHỦNG ]   🌱🌱🌱 │
            │        🌿                                    │
            │      ┌───┐      Mầm thảo dược quý hiếm,      │
            │      │ 🌱│      chắt lọc linh khí cứu người. │
            │      └───┘                                   │
            └──────────────────────────────────────────────┘
            """,
            "Túi hạt thảo dược tuyển chọn từ rễ cây củ quả quý hiếm. Yêu cầu đất đai phì nhiêu và chăm sóc cẩn thận.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Hạt giống y học (Seed)",
                ["Thời gian sinh trưởng"] = "20 ngày",
                ["Sản phẩm đầu ra"] = "Thuốc (2 đơn vị / mẫu)",
                ["Đất yêu cầu"] = "Độ phì cao để cho dược tính tốt",
                ["Giá cơ bản"] = $"{item.BasePrice} Quan / bao"
            },
            "Mua tại Điền Trang hoặc thỉnh từ danh y quán.",
            "Gieo trồng để thu hoạch thuốc cứu thương và bán giá cao.",
            "Tương lai: Trồng linh chi, hà thủ ô ngàn năm."),

        // 21. Phân bón
        "phan_bon" => new ItemHubEntry(
            item,
            "Vật Tư Cải Tạo Đất",
            "🧪",
            """
            ┌──────────────────────────────────────────────┐
            │  🌱🌱🌱       [ HỮU CƠ ĐẠI HOÀNG PHÂN ]  🌱🌱🌱 │
            │      ╭───╮                                   │
            │      │ 🧪│      Bón lót, bón thúc hồi sinh đất│
            │      ╰───╯      đất tốt mùa màng bội thu.    │
            └──────────────────────────────────────────────┘
            """,
            "Phân hữu cơ ủ hoai mục trộn tro trấu và bột xương. Thần dược bồi bổ cho những thửa ruộng bị bạc màu sau mùa gặt.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Vật tư nông tang (Supply)",
                ["Hiệu ứng bón phân"] = "+25 điểm độ phì đất / lần bón",
                ["Trần tối đa"] = "100 điểm độ phì",
                ["Công cụ bắt buộc"] = "Cuốc nông nghiệp (cuoc_nong) để trộn đất",
                ["Tiêu hao"] = "1 bao / lần bón",
                ["Giá mua sỉ"] = $"{item.BasePrice} Quan / bao"
            },
            "Mua tại kho Điền Trang hoặc các cửa hàng vật tư nông thôn.",
            "Sử dụng lệnh bón phân khi ruộng bị bạc màu sau vụ gặt (độ phì < 100).",
            "Tương lai: Ủ phân vi sinh cao cấp tăng 50 độ phì và giữ ẩm."),

        // 22. Cuốc nông nghiệp
        "cuoc_nong" => new ItemHubEntry(
            item,
            "Nông Cụ Thiết Yếu",
            "⛏️",
            """
            ┌──────────────────────────────────────────────┐
            │  ⛏️⛏️⛏️       [ NÔNG GIA THIẾT HOÀNH ]   ⛏️⛏️⛏️ │
            │        _T_                                   │
            │       /|_|\     Lưỡi cuốc sắt bén ngọt,      │
            │        | |      cán gỗ dẻo dai chắc nịch.    │
            │        | |      "Tay cuốc tay cày,           │
            │        |_|       kho đầy bồ lúa."            │
            └──────────────────────────────────────────────┘
            """,
            "Chiếc cuốc sắt rèn thủ công với lưỡi cuốc dày dặn và cán gỗ nghiến. Là báu vật số một của mọi nhà nông, phục vụ từ việc khai hoang xới đất đến gieo hạt, bón phân.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Nông cụ cầm tay (Tool)",
                ["Thao tác hỗ trợ"] = "Gieo hạt (Plant), Bón phân (Fertilise), Đào mương (Irrigate)",
                ["Cấp độ"] = "Cấp 1 (Nông Cụ Sắt Thô)",
                ["Độ bền"] = "Vĩnh cửu (chưa hao mòn)",
                ["Giá mua"] = $"{item.BasePrice} Quan",
                ["Trạng thái"] = "Cất trong hành trang sở tại để kích hoạt thao tác"
            },
            "Mua tại kho Điền Trang sở tại hoặc tiệm rèn nông cụ.",
            "Cần có trong túi đồ để thực hiện các thao tác gieo trồng và bón phân trên mọi mẫu ruộng.",
            "Tương lai: Rèn nâng cấp lên Cuốc Thép Hoa Vân (tăng 20% năng suất gieo hạt) và Cuốc Bách Luyện Thần Nông."),

        // 23. Liềm thu hoạch
        "liem_gat" => new ItemHubEntry(
            item,
            "Nông Cụ Thu Hoạch",
            "🌾",
            """
            ┌──────────────────────────────────────────────┐
            │  🌾🌾🌾       [ BÁN NGUYỆT THU LIỀM ]    🌾🌾🌾 │
            │        _                                     │
            │       / )       Lưỡi liềm cong hình trăng khuyết│
            │      / /        răng cưa sắc lẹm gặt đứt thân.│
            │     (_/         Lúa chín đầy đồng, liềm reo. │
            └──────────────────────────────────────────────┘
            """,
            "Chiếc liềm cong hình bán nguyệt tôi bằng thép tốt, mép trong có răng cưa nhỏ giúp gặt lúa và cắt cành thảo dược nhanh chóng, không làm rụng hạt.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Nông cụ thu hoạch (Tool)",
                ["Thao tác hỗ trợ"] = "Gặt lúa & Thu hái nông sản (Harvest)",
                ["Cấp độ"] = "Cấp 1 (Liềm Thép Tiêu Chuẩn)",
                ["Độ bền"] = "Vĩnh cửu",
                ["Giá mua"] = $"{item.BasePrice} Quan",
                ["Trạng thái"] = "Bắt buộc có trong hành trang khi thực hiện gặt"
            },
            "Mua tại kho Điền Trang sở tại hoặc tiệm thợ rèn.",
            "Bắt buộc có trong túi đồ sở tại khi lúa chín để tiến hành thu hoạch nông sản.",
            "Tương lai: Nâng cấp lên Liềm Trăng Khuyết Thần Nông tăng 15% sản lượng gặt thu hoạch."),

        // 24. Gầu mương thủy lợi
        "binh_tuoi" => new ItemHubEntry(
            item,
            "Dụng Cụ Thủy Lợi",
            "🌊",
            """
            ┌──────────────────────────────────────────────┐
            │  🌊🌊🌊       [ DẪN THỦY TIÊU GẦU ]      🌊🌊🌊 │
            │       |---|                                  │
            │      /     \    Khơi mương dẫn nước về ruộng,│
            │     |  GẦU  |   đất nứt nẻ hóa phì nhiêu.    │
            │      \_____/    Thủy lợi hưng thịnh nông gia.│
            └──────────────────────────────────────────────┘
            """,
            "Gầu mương đan bằng mây tre trét nhựa mít và đóng đai sắt, chuyên dùng để tát nước, đào đắp bờ mương và khơi thông thủy lợi giữa các mẫu ruộng.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = "Dụng cụ công trình thủy lợi (Tool)",
                ["Thao tác hỗ trợ"] = "Đào mương thủy lợi (Irrigate)",
                ["Hiệu quả"] = "Nâng cấp mương máng, tăng +10 độ phì đất",
                ["Cấp độ"] = "Cấp 1 (Gầu Thủy Lợi Tiêu Chuẩn)",
                ["Giá mua"] = $"{item.BasePrice} Quan"
            },
            "Mua tại kho Điền Trang sở tại hoặc tiệm mộc/chợ phiên.",
            "Cần có trong túi đồ để tiến hành đào mương thủy lợi nâng cấp ruộng đất.",
            "Tương lai: Chế tạo guồng nước tự động (Long cốt xa) dẫn nước tưới tiêu toàn bộ điền trang."),

        // Fallback default
        _ => new ItemHubEntry(
            item,
            item.Category.ToUpperInvariant(),
            "📦",
            """
            ┌──────────────────────────────────────────────┐
            │  📦📦📦       [ KỲ VẬT TAM QUỐC ]        📦📦📦 │
            │      ╭───╮                                   │
            │      │ 📦│      Hàng hóa lưu thông thiên hạ. │
            │      ╰───╯                                   │
            └──────────────────────────────────────────────┘
            """,
            $"Kỳ vật '{item.NameVi}' lưu thông trên thị trường mười ba châu quận.",
            new Dictionary<string, string>
            {
                ["Chủng loại"] = item.Category,
                ["Giá cơ bản"] = $"{item.BasePrice} Quan",
                ["Hạn dùng"] = item.ShelfLifeDays > 0 ? $"{item.ShelfLifeDays} ngày" : "Vô hạn",
                ["Khối lượng"] = $"{item.Weight} cân"
            },
            "Mua bán tại các chợ phiên hoặc thương cảng lớn.",
            "Dùng buôn bán giao thương hoặc tích trữ.",
            "Tương lai: Mở rộng công thức chế tác kết hợp.")
    };
}
