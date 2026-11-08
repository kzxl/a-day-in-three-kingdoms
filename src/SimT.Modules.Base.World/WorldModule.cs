using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.World;

/// <summary>base.world — settlements, roads, and sublocations, exposed as the <c>world.map</c> capability.</summary>
public sealed partial class WorldModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = new WorldMap(context.Db, context.Clock, context.Capabilities);
        context.Capabilities.Provide(CapabilityIds.WorldMap, map);
        var stroll = new CityStrollService(map, context.Capabilities, context.Clock, context.Log);
        context.Capabilities.Provide(CapabilityIds.CityStroll, stroll);
        context.Ui.AddScreen(new WorldMapScreen(map, context.Capabilities, context.Clock));
        context.Ui.AddCommandHandler(new WorldMapCommandHandler(context.Db, map, context.Capabilities, context.Clock));
        context.Log.Info($"map ready with {map.Citadels.Count} citadels, CityStroll registered");
    }

    private sealed partial class WorldMap : IWorldMap
    {
        private readonly IGameClock? _clock;
        private readonly CitadelDef[] _citadels;
        private readonly Dictionary<long, CitadelDef> _byId;
        private readonly Dictionary<long, RouteStep[]> _routes;
        private readonly Dictionary<long, long[]> _neighbours;
        private readonly int[,] _travelMatrix;
        private readonly Dictionary<long, SublocationDef[]> _sublocations;
        private readonly List<RouteLinkDef> _allRoutesList = new();
        private readonly Dictionary<long, CitadelEcosystemDef> _ecosystems = new();
        private readonly Dictionary<(long A, long B), RouteHazardEntry> _routeHazards = new();
        private readonly Dictionary<long, List<RouteHazardEntry>> _cityHazards = new();

        public WorldMap(IDbSession db, IGameClock? clock = null, ICapabilityBroker? capabilities = null)
        {
            _clock = clock;
            _capabilities = capabilities;
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
                new CitadelEcosystemDef(1, "Trung Nguyên Đế Đô", "Ôn đới bốn mùa rõ rệt", "Đồng bằng châu thổ sông Lạc Thủy trù phú", "Cổ Vật Hoàng Cung", "Thiết Giáp Binh Khí", 1, 446.4, 236.0),
                new CitadelEcosystemDef(2, "Quan Trung Cửa Ngõ Tây Vực", "Khô lạnh gió cát Tây Lương", "Thung lũng sông Vị Thủy & Đèo Đồng Quan", "Chiến Mã Tây Lương", "Thảm Lông Dê & Đao Kiếm", 2, 332.8, 247.2),
                new CitadelEcosystemDef(3, "Hà Bắc Phì Nhiêu", "Gió bấc khô ráo mát mẻ", "Đồng bằng Chương Thủy đại kho lương thực", "Lương Thảo Ngũ Cốc", "Thiết Đột Kỵ Binh", 2, 507.2, 181.6),
                new CitadelEcosystemDef(4, "U Châu Biên Cương", "Lạnh giá sương muối quanh năm", "Vườn đào thôn dã & gò đồi bắc bộ", "Lụa Dệt Dân Gian", "Rượu Trác Huyện Thơm Nồng", 2, 559.2, 80.0),
                new CitadelEcosystemDef(5, "Cự Lộc Sa Trường", "Khắc nghiệt sương mù hoàng thổ", "Bình nguyên đầm lầy hiểm trở Cự Lộc", "Bùa Chú Khăn Vàng", "Dược Thảo Trị Thương", 3, 532.8, 157.6),
                new CitadelEcosystemDef(6, "Thanh Châu Duyên Hải", "Gió biển mát mẻ ôn hòa", "Vùng đất châu thổ hạ lưu Hoàng Hà", "Muối Biển Tinh Khiết", "Lương Thực Lưu Dân", 2, 573.6, 155.2),
                new CitadelEcosystemDef(7, "Dự Châu Tâm Điểm", "Thuận hòa mưa nắng điều độ", "Đồng bằng đồn điền Tào Ngụy quân quản", "Quân Nhu Đồn Điền", "Binh Thư Chiến Lược", 1, 491.2, 255.2),
                new CitadelEcosystemDef(8, "Biên Ải Tuyết Sơn", "Hàn đới tuyết phủ cực bắc", "Thảo nguyên biên cương lạnh buốt", "Bạch Mã Tuấn Mã", "Da Hồ Ly & Tuyết Sâm", 3, 624.0, 67.2),
                new CitadelEcosystemDef(9, "Từ Châu Thủy Trấn", "Mưa nhiều ấm áp ẩm ướt", "Đất đỏ trù phú nằm giữa Tứ Thủy & Hoài Hà", "Cá Hoài Hà & Cung Tiễn", "Gạo Nếp Thượng Hạng", 2, 623.2, 246.4),
                new CitadelEcosystemDef(10, "Kinh Tương Yết Hầu", "Ôn nhuận gió sông mát lành", "Giao lộ Hán Thủy & Đồi Lộc Môn", "Chiến Thuyền Hán Thủy", "Cá Tươi & Văn Sách", 2, 436.8, 318.4),
                new CitadelEcosystemDef(11, "Trường Giang Hiểm Giới", "Gió đông nam ẩm ướt", "Vách đá sừng sững bên dòng Trường Giang", "Dầu Hỏa Chiến Hạm", "Thảo Dược Ven Sông", 2, 492.8, 388.8),
                new CitadelEcosystemDef(12, "Nam Quận Thành Lũy", "Mưa phùn sương khói mờ ảo", "Thành trì kiên cố đầm lầy nam Kinh Châu", "Gỗ Đóng Thuyền Chiến", "Lương Thảo Thủy Lộ", 2, 438.4, 372.8),
                new CitadelEcosystemDef(13, "Giang Đông Thủy Quốc", "Nhiệt đới gió mùa mát rượi", "Kinh đô Đông Ngô bên bờ Thái Hồ", "Trà Long Tỉnh", "Tơ Lụa & Gốm Sứ Tinh Xảo", 1, 648.8, 318.4),
                new CitadelEcosystemDef(14, "Tần Lĩnh Hiểm Đạo", "Sương giá mù sương núi cao", "Hiểm đạo Kiếm Các vách đá cheo leo", "Linh Chi & Trầm Hương", "Quặng Sắt Hán Trung", 3, 272.8, 285.6),
                new CitadelEcosystemDef(15, "Thiên Phủ Ba Thục", "Ấm áp bốn mùa không tuyết", "Bình nguyên Ba Thục màu mỡ trù phú", "Thục Cẩm Danh Trấn", "Mật Gấu & Trà Thục Sơn", 1, 178.4, 362.4),
                new CitadelEcosystemDef(16, "Nam Dương Yết Hầu", "Ôn hòa mưa thuận ven sông", "Bình nguyên Dục Thủy nối liền Trung Nguyên và Kinh Tương", "Thảo Dược Nam Dương", "Binh Giáp Tây Lương", 2, 448.8, 288.0),
                new CitadelEcosystemDef(17, "Hoài Nam Trọng Trấn", "Ẩm ướt mưa nhiều bốn mùa", "Đồng bằng phì nhiêu ven bờ sông Hoài Thủy", "Lúa Nước Hoài Nam", "Binh Khí Hoài Thủy", 2, 585.6, 301.6),
                new CitadelEcosystemDef(18, "Thái Hành Hiểm Trấn", "Khô lạnh gió núi Thái Hành", "Cao nguyên hiểm trở nhìn xuống Trung Nguyên", "Quặng Sắt Thượng Đảng", "Chiến Mã Tịnh Châu", 2, 467.2, 185.6),
                new CitadelEcosystemDef(19, "Lương Châu Sa Mạc", "Gió cát khô lạnh sa mạc cằn cỗi", "Thảo nguyên sa mạc cửa ngõ Con Đường Tơ Lụa", "Hãn Huyết Chiến Mã", "Tơ Lụa Tây Vực & Đao Sắt", 3, 132.8, 130.4),
                new CitadelEcosystemDef(20, "Lĩnh Nam Hải Lộ", "Nhiệt đới ẩm ấm gió biển quanh năm", "Đồng bằng duyên hải rừng rậm cận kề Biển Đông", "Nam Hải Dạ Minh Châu", "Trầm Hương Lĩnh Nam & Gỗ Quý", 2, 240.0, 592.0)
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
            if (IsRouteBlocked(fromCitadelId, toCitadelId))
            {
                return 0;
            }

            if (fromCitadelId >= 0 && fromCitadelId < _travelMatrix.GetLength(0) &&
                toCitadelId >= 0 && toCitadelId < _travelMatrix.GetLength(1))
            {
                var baseDays = _travelMatrix[fromCitadelId, toCitadelId];
                if (baseDays <= 0)
                {
                    return 0;
                }

                if (TryGetActiveRouteHazard(fromCitadelId, toCitadelId, out var hazard) &&
                    string.Equals(hazard.HazardType, "route_danger", StringComparison.OrdinalIgnoreCase))
                {
                    var extra = Math.Max(1, baseDays * Math.Max(1000, hazard.SeverityPermille) / 1000 - baseDays);
                    return baseDays + extra;
                }

                return baseDays;
            }

            return 0;
        }

        public void SetRouteHazard(
            long fromCitadelId,
            long toCitadelId,
            string hazardType,
            int startDay,
            int durationDays,
            int severityPermille,
            string noteVi)
        {
            var entry = new RouteHazardEntry(hazardType, startDay, startDay + Math.Max(1, durationDays), severityPermille, noteVi);
            if (fromCitadelId > 0 && toCitadelId > 0 && fromCitadelId != toCitadelId)
            {
                var key = (Math.Min(fromCitadelId, toCitadelId), Math.Max(fromCitadelId, toCitadelId));
                _routeHazards[key] = entry;
            }

            foreach (var citId in new[] { fromCitadelId, toCitadelId })
            {
                if (citId <= 0) continue;
                if (!_cityHazards.TryGetValue(citId, out var list))
                {
                    list = new List<RouteHazardEntry>();
                    _cityHazards[citId] = list;
                }
                list.Add(entry);
            }
        }

        public bool IsRouteBlocked(long fromCitadelId, long toCitadelId) =>
            TryGetActiveRouteHazard(fromCitadelId, toCitadelId, out var hazard) &&
            string.Equals(hazard.HazardType, "route_block", StringComparison.OrdinalIgnoreCase);

        private bool TryGetActiveRouteHazard(long fromCitadelId, long toCitadelId, out RouteHazardEntry hazard)
        {
            var key = (Math.Min(fromCitadelId, toCitadelId), Math.Max(fromCitadelId, toCitadelId));
            if (_routeHazards.TryGetValue(key, out hazard))
            {
                var today = (int)(_clock?.Today.DayIndex ?? 0);
                if (today >= hazard.StartDay && today <= hazard.EndDay)
                {
                    return true;
                }
            }

            hazard = default;
            return false;
        }

        public IReadOnlyList<SublocationDef> Sublocations(long citadelId) =>
            _sublocations.TryGetValue(citadelId, out var list)
                ? list
                : Array.Empty<SublocationDef>();

        public IReadOnlyList<RouteLinkDef> Routes => _allRoutesList;

        public CitadelEcosystemDef GetEcosystem(long citadelId)
        {
            if (!_ecosystems.TryGetValue(citadelId, out var eco))
            {
                var cit = Find(citadelId);
                eco = new CitadelEcosystemDef(
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

            if (_cityHazards.TryGetValue(citadelId, out var hazards))
            {
                var today = (int)(_clock?.Today.DayIndex ?? 0);
                var maxBoost = 0;
                foreach (var h in hazards)
                {
                    if (today >= h.StartDay && today <= h.EndDay)
                    {
                        var boost = string.Equals(h.HazardType, "route_block", StringComparison.OrdinalIgnoreCase)
                            ? 2
                            : Math.Max(1, h.SeverityPermille / 600);
                        if (boost > maxBoost) maxBoost = boost;
                    }
                }

                if (maxBoost > 0)
                {
                    return eco with { DangerLevel = Math.Min(5, eco.DangerLevel + maxBoost) };
                }
            }

            return eco;
        }

        private readonly record struct RouteStep(long To, int Days);
        private readonly record struct RouteHazardEntry(string HazardType, int StartDay, int EndDay, int SeverityPermille, string NoteVi);
    }
}
