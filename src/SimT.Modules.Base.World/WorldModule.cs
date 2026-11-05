using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.World;

/// <summary>base.world — settlements, roads, and sublocations, exposed as the <c>world.map</c> capability.</summary>
public sealed class WorldModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = new WorldMap(context.Db);
        context.Capabilities.Provide(CapabilityIds.WorldMap, map);
        var stroll = new CityStrollService(map, context.Capabilities, context.Clock, context.Log);
        context.Capabilities.Provide(CapabilityIds.CityStroll, stroll);
        context.Ui.AddScreen(new WorldMapScreen(map, context.Capabilities, context.Clock));
        context.Ui.AddCommandHandler(new WorldMapCommandHandler(map, context.Capabilities, context.Clock));
        context.Log.Info($"map ready with {map.Citadels.Count} citadels, CityStroll registered");
    }

    private sealed class WorldMap : IWorldMap
    {
        private readonly CitadelDef[] _citadels;
        private readonly Dictionary<long, CitadelDef> _byId;
        private readonly Dictionary<long, RouteStep[]> _routes;
        private readonly Dictionary<long, long[]> _neighbours;
        private readonly int[,] _travelMatrix;
        private readonly Dictionary<long, SublocationDef[]> _sublocations;
        private readonly List<RouteLinkDef> _allRoutesList = new();
        private readonly Dictionary<long, CitadelEcosystemDef> _ecosystems = new();

        public WorldMap(IDbSession db)
        {
            _citadels = db.Query(
                    "SELECT id, code, name_vi, name_en, region, price_factor_permille FROM wd_citadel ORDER BY id",
                    row => new CitadelDef(
                        row.GetInt64(0),
                        row.GetString(1),
                        row.GetString(2),
                        row.GetString(3),
                        row.GetString(4),
                        row.GetInt32(5)))
                .ToArray();

            _byId = new Dictionary<long, CitadelDef>(_citadels.Length);
            foreach (var citadel in _citadels)
            {
                _byId[citadel.Id] = citadel;
            }

            var routes = new Dictionary<long, List<RouteStep>>();
            foreach (var step in db.Query(
                         "SELECT citadel_id, other_id, travel_days FROM wd_route ORDER BY citadel_id, other_id",
                         row => (From: row.GetInt64(0), To: row.GetInt64(1), Days: row.GetInt32(2))))
            {
                if (!routes.TryGetValue(step.From, out var list))
                {
                    list = new List<RouteStep>();
                    routes[step.From] = list;
                }

                list.Add(new RouteStep(step.To, step.Days));

                // Add to flat undirected route list without duplicate visual edges
                if (step.From < step.To)
                {
                    _allRoutesList.Add(new RouteLinkDef(step.From, step.To, step.Days));
                }
            }

            _routes = routes.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
            _neighbours = _routes.ToDictionary(pair => pair.Key, pair => pair.Value.Select(s => s.To).ToArray());

            var maxId = _citadels.Length > 0 ? (int)_citadels.Max(c => c.Id) : 0;
            _travelMatrix = new int[maxId + 1, maxId + 1];
            foreach (var (from, steps) in _routes)
            {
                if (from <= maxId)
                {
                    foreach (var step in steps)
                    {
                        if (step.To <= maxId)
                        {
                            _travelMatrix[from, step.To] = step.Days;
                        }
                    }
                }
            }

            var sublocations = new Dictionary<long, List<SublocationDef>>();
            foreach (var sub in db.Query(
                         "SELECT id, citadel_id, code, name_vi, kind FROM wd_sublocation ORDER BY citadel_id, sort_order",
                         row => new SublocationDef(
                              row.GetInt64(0),
                              row.GetInt64(1),
                              row.GetString(2),
                              row.GetString(3),
                              row.GetString(4))))
            {
                if (!sublocations.TryGetValue(sub.CitadelId, out var list))
                {
                    list = new List<SublocationDef>();
                    sublocations[sub.CitadelId] = list;
                }

                list.Add(sub);
            }

            _sublocations = sublocations.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

            InitEcosystemProfiles();
        }

        private void InitEcosystemProfiles()
        {
            var seedEcosystems = new[]
            {
                new CitadelEcosystemDef(1, "Trung Nguyên Đế Đô", "Ôn đới bốn mùa rõ rệt", "Đồng bằng châu thổ sông Lạc Thủy trù phú", "Cổ Vật Hoàng Cung", "Thiết Giáp Binh Khí", 1, 410, 250),
                new CitadelEcosystemDef(2, "Quan Trung Cửa Ngõ Tây Vực", "Khô lạnh gió cát Tây Lương", "Thung lũng sông Vị Thủy & Đèo Đồng Quan", "Chiến Mã Tây Lương", "Thảm Lông Dê & Đao Kiếm", 2, 290, 250),
                new CitadelEcosystemDef(3, "Hà Bắc Phì Nhiêu", "Gió bấc khô ráo mát mẻ", "Đồng bằng Chương Thủy đại kho lương thực", "Lương Thảo Ngũ Cốc", "Thiết Đột Kỵ Binh", 2, 450, 180),
                new CitadelEcosystemDef(4, "U Châu Biên Cương", "Lạnh giá sương muối quanh năm", "Vườn đào thôn dã & gò đồi bắc bộ", "Lụa Dệt Dân Gian", "Rượu Trác Huyện Thơm Nồng", 2, 490, 110),
                new CitadelEcosystemDef(5, "Cự Lộc Sa Trường", "Khắc nghiệt sương mù hoàng thổ", "Bình nguyên đầm lầy hiểm trở Cự Lộc", "Bùa Chú Khăn Vàng", "Dược Thảo Trị Thương", 3, 470, 145),
                new CitadelEcosystemDef(6, "Thanh Châu Duyên Hải", "Gió biển mát mẻ ôn hòa", "Vùng đất châu thổ hạ lưu Hoàng Hà", "Muối Biển Tinh Khiết", "Lương Thực Lưu Dân", 2, 540, 160),
                new CitadelEcosystemDef(7, "Dự Châu Tâm Điểm", "Thuận hòa mưa nắng điều độ", "Đồng bằng đồn điền Tào Ngụy quân quản", "Quân Nhu Đồn Điền", "Binh Thư Chiến Lược", 1, 450, 295),
                new CitadelEcosystemDef(8, "Biên Ải Tuyết Sơn", "Hàn đới tuyết phủ cực bắc", "Thảo nguyên biên cương lạnh buốt", "Bạch Mã Tuấn Mã", "Da Hồ Ly & Tuyết Sâm", 3, 570, 80),
                new CitadelEcosystemDef(9, "Từ Châu Thủy Trấn", "Mưa nhiều ấm áp ẩm ướt", "Đất đỏ trù phú nằm giữa Tứ Thủy & Hoài Hà", "Cá Hoài Hà & Cung Tiễn", "Gạo Nếp Thượng Hạng", 2, 580, 280),
                new CitadelEcosystemDef(10, "Kinh Tương Yết Hầu", "Ôn nhuận gió sông mát lành", "Giao lộ Hán Thủy & Đồi Lộc Môn", "Chiến Thuyền Hán Thủy", "Cá Tươi & Văn Sách", 2, 410, 370),
                new CitadelEcosystemDef(11, "Trường Giang Hiểm Giới", "Gió đông nam ẩm ướt", "Vách đá sừng sững bên dòng Trường Giang", "Dầu Hỏa Chiến Hạm", "Thảo Dược Ven Sông", 2, 490, 430),
                new CitadelEcosystemDef(12, "Nam Quận Thành Lũy", "Mưa phùn sương khói mờ ảo", "Thành trì kiên cố đầm lầy nam Kinh Châu", "Gỗ Đóng Thuyền Chiến", "Lương Thảo Thủy Lộ", 2, 390, 440),
                new CitadelEcosystemDef(13, "Giang Đông Thủy Quốc", "Nhiệt đới gió mùa mát rượi", "Kinh đô Đông Ngô bên bờ Thái Hồ", "Trà Long Tỉnh", "Tơ Lụa & Gốm Sứ Tinh Xảo", 1, 620, 390),
                new CitadelEcosystemDef(14, "Tần Lĩnh Hiểm Đạo", "Sương giá mù sương núi cao", "Hiểm đạo Kiếm Các vách đá cheo leo", "Linh Chi & Trầm Hương", "Quặng Sắt Hán Trung", 3, 260, 340),
                new CitadelEcosystemDef(15, "Thiên Phủ Ba Thục", "Ấm áp bốn mùa không tuyết", "Bình nguyên Ba Thục màu mỡ trù phú", "Thục Cẩm Danh Trấn", "Mật Gấu & Trà Thục Sơn", 1, 190, 430),
                new CitadelEcosystemDef(16, "Nam Dương Yết Hầu", "Ôn hòa mưa thuận ven sông", "Bình nguyên Dục Thủy nối liền Trung Nguyên và Kinh Tương", "Thảo Dược Nam Dương", "Binh Giáp Tây Lương", 2, 425, 330),
                new CitadelEcosystemDef(17, "Hoài Nam Trọng Trấn", "Ẩm ướt mưa nhiều bốn mùa", "Đồng bằng phì nhiêu ven bờ sông Hoài Thủy", "Lúa Nước Hoài Nam", "Binh Khí Hoài Thủy", 2, 530, 335),
                new CitadelEcosystemDef(18, "Thái Hành Hiểm Trấn", "Khô lạnh gió núi Thái Hành", "Cao nguyên hiểm trở nhìn xuống Trung Nguyên", "Quặng Sắt Thượng Đảng", "Chiến Mã Tịnh Châu", 2, 370, 190),
                new CitadelEcosystemDef(19, "Lương Châu Sa Mạc", "Gió cát khô lạnh sa mạc cằn cỗi", "Thảo nguyên sa mạc cửa ngõ Con Đường Tơ Lụa", "Hãn Huyết Chiến Mã", "Tơ Lụa Tây Vực & Đao Sắt", 3, 150, 190),
                new CitadelEcosystemDef(20, "Lĩnh Nam Hải Lộ", "Nhiệt đới ẩm ấm gió biển quanh năm", "Đồng bằng duyên hải rừng rậm cận kề Biển Đông", "Nam Hải Dạ Minh Châu", "Trầm Hương Lĩnh Nam & Gỗ Quý", 2, 430, 475)
            };

            foreach (var eco in seedEcosystems)
            {
                _ecosystems[eco.CitadelId] = eco;
            }
        }

        public IReadOnlyList<CitadelDef> Citadels => _citadels;

        public CitadelDef? Find(long citadelId) => _byId.GetValueOrDefault(citadelId);

        public IReadOnlyList<long> RouteNeighbours(long citadelId) =>
            _neighbours.TryGetValue(citadelId, out var list)
                ? list
                : Array.Empty<long>();

        public int TravelDays(long fromCitadelId, long toCitadelId)
        {
            if (fromCitadelId >= 0 && fromCitadelId < _travelMatrix.GetLength(0) &&
                toCitadelId >= 0 && toCitadelId < _travelMatrix.GetLength(1))
            {
                return _travelMatrix[fromCitadelId, toCitadelId];
            }

            return 0;
        }

        public IReadOnlyList<SublocationDef> Sublocations(long citadelId) =>
            _sublocations.TryGetValue(citadelId, out var list)
                ? list
                : Array.Empty<SublocationDef>();

        public IReadOnlyList<RouteLinkDef> Routes => _allRoutesList;

        public CitadelEcosystemDef GetEcosystem(long citadelId)
        {
            if (_ecosystems.TryGetValue(citadelId, out var eco))
                return eco;

            var cit = Find(citadelId);
            return new CitadelEcosystemDef(
                citadelId,
                cit?.Region ?? "Đồng Bằng",
                "Ôn đới",
                "Vùng đồng bằng trù phú",
                "Nông sản địa phương",
                "Vật liệu chế tác",
                1,
                400,
                300);
        }

        public TravelQuote QuoteTravel(long fromCitadelId, long toCitadelId, TravelMode mode)
        {
            if (fromCitadelId == toCitadelId)
            {
                return new TravelQuote(fromCitadelId, toCitadelId, 0, 0, 0, 0, 0, new[] { fromCitadelId }, "Tại Chỗ");
            }

            var path = FindShortestPathNodes(fromCitadelId, toCitadelId);
            var directDays = TravelDays(fromCitadelId, toCitadelId);
            var baseDays = directDays > 0 ? directDays : CalculatePathDays(path);
            if (baseDays <= 0) baseDays = 4;

            int actualDays;
            long fee;
            int rations;
            int fatigue;
            string modeName;

            switch (mode)
            {
                case TravelMode.Horse:
                    actualDays = Math.Max(1, (int)Math.Ceiling(baseDays * 0.65));
                    fee = baseDays * 15;
                    rations = actualDays;
                    fatigue = actualDays * 4;
                    modeName = "Dịch Mã (Cưỡi Ngựa)";
                    break;

                case TravelMode.Caravan:
                    actualDays = baseDays;
                    fee = baseDays * 25;
                    rations = baseDays;
                    fatigue = baseDays * 3;
                    modeName = "Tiêu Cục (Hộ Tiêu)";
                    break;

                case TravelMode.Walk:
                default:
                    actualDays = baseDays;
                    fee = 0;
                    rations = baseDays;
                    fatigue = baseDays * 7;
                    modeName = "Bộ Hành (Đi Bộ)";
                    break;
            }

            return new TravelQuote(fromCitadelId, toCitadelId, baseDays, actualDays, fee, rations, fatigue, path, modeName);
        }

        private int CalculatePathDays(IReadOnlyList<long> path)
        {
            if (path.Count <= 1) return 0;
            var total = 0;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var d = TravelDays(path[i], path[i + 1]);
                total += (d > 0 ? d : 2);
            }
            return total;
        }

        private IReadOnlyList<long> FindShortestPathNodes(long fromId, long toId)
        {
            var queue = new Queue<long>();
            var visited = new HashSet<long>();
            var prev = new Dictionary<long, long>();

            queue.Enqueue(fromId);
            visited.Add(fromId);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (curr == toId) break;

                foreach (var next in RouteNeighbours(curr))
                {
                    if (visited.Add(next))
                    {
                        prev[next] = curr;
                        queue.Enqueue(next);
                    }
                }
            }

            if (!prev.ContainsKey(toId))
            {
                return new[] { fromId, toId };
            }

            var path = new List<long>();
            var step = toId;
            while (step != fromId)
            {
                path.Add(step);
                step = prev[step];
            }
            path.Add(fromId);
            path.Reverse();
            return path;
        }

        private readonly record struct RouteStep(long To, int Days);
    }
}
