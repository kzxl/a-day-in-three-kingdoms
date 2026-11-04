using System.Globalization;
using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.Player;

/// <summary>
/// Domain engine for Three Kingdoms Fame, Regional Influence, Moral Alignment &amp; Deeds Chronicle.
/// Owned by <c>base.player</c>.
/// </summary>
public sealed class PlayerReputationService : IPlayerReputation
{
    private static readonly (string Code, string NameVi)[] CanonicalRegions =
    [
        ("trung_nguyen", "Trung Nguyên"),
        ("ha_bac", "Hà Bắc"),
        ("giang_dong", "Giang Đông"),
        ("kinh_so", "Kinh Sở"),
        ("ba_thuc", "Ba Thục")
    ];

    private readonly IDbSession _db;
    private readonly long _playerId;
    private readonly ICapabilityBroker _broker;
    private readonly IGameClock _clock;
    private readonly ILog _log;

    public PlayerReputationService(
        IDbSession db,
        long playerId,
        ICapabilityBroker broker,
        IGameClock clock,
        ILog log)
    {
        _db = db;
        _playerId = playerId;
        _broker = broker;
        _clock = clock;
        _log = log;
    }

    public ReputationStanding Standing
    {
        get
        {
            var row = _db.QueryOne<CharacterSocial>(
                "SELECT fame, virtue, credibility FROM pl_social WHERE player_id = @id",
                r => new CharacterSocial(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)),
                DbArgs.One("@id", _playerId)) ?? new CharacterSocial(100, 50, 50);

            var fame = row.Fame;
            var (tier, title, rank, nextTierFame, perk) = ComputeTier(fame);

            var prevTierFame = tier switch
            {
                0 => 0,
                1 => 100,
                2 => 300,
                3 => 600,
                4 => 1200,
                5 => 2500,
                _ => 5000
            };

            var range = Math.Max(1, nextTierFame - prevTierFame);
            var progress = tier >= 6 ? 100 : Math.Clamp((int)((fame - prevTierFame) * 100.0 / range), 0, 100);

            return new ReputationStanding(
                Fame: fame,
                Virtue: row.Virtue,
                Credibility: row.Credibility,
                TitleVi: title,
                RankVi: rank,
                CurrentTier: tier,
                NextTierFame: nextTierFame,
                ProgressToNextTierPercent: progress,
                PerkSummaryVi: perk);
        }
    }

    public IReadOnlyList<ReputationDeed> RecentDeeds(int limit = 20)
    {
        return _db.Query(
            @"SELECT id, day_index, date_text, category, fame_delta, virtue_delta, credibility_delta, title_vi, narrative_vi
              FROM pl_reputation_deed
              WHERE player_id = @id
              ORDER BY id DESC
              LIMIT @limit",
            row => new ReputationDeed(
                Id: row.GetInt64(0),
                DayIndex: row.GetInt32(1),
                DateText: row.GetString(2),
                Category: row.GetString(3),
                FameDelta: row.GetInt32(4),
                VirtueDelta: row.GetInt32(5),
                CredibilityDelta: row.GetInt32(6),
                TitleVi: row.GetString(7),
                NarrativeVi: row.GetString(8)),
            new DbArgs().Add("@id", _playerId).Add("@limit", limit));
    }

    public IReadOnlyList<RegionalReputation> RegionalStandings
    {
        get
        {
            var stored = _db.Query(
                "SELECT region_code, reputation FROM pl_reputation_region WHERE player_id = @id",
                row => (Code: row.GetString(0), Rep: row.GetInt32(1)),
                DbArgs.One("@id", _playerId)).ToDictionary(x => x.Code, x => x.Rep);

            var results = new List<RegionalReputation>(CanonicalRegions.Length);
            foreach (var (code, name) in CanonicalRegions)
            {
                var rep = stored.TryGetValue(code, out var val) ? val : 20;
                var (tier, desc) = rep switch
                {
                    >= 300 => ("Vạn Dân Kính Ngưỡng", "Giảm 15% thuế chợ & phí bến bãi; NPC bản địa tự tìm đến kết giao."),
                    >= 150 => ("Danh Chấn Châu Huyện", "Giảm 10% thuế chợ & chi phí giao thương trong khu vực."),
                    >= 50 => ("Tiếng Thơm Lan Xa", "Giảm 5% phí giao dịch buôn bán tại các thành thuộc châu hạt."),
                    _ => ("Khách Lạ Mới Đến", "Chưa tạo được dấu ấn; buôn bán theo giá niêm yết chuẩn.")
                };

                results.Add(new RegionalReputation(code, name, rep, tier, desc));
            }

            return results;
        }
    }

    public IReadOnlyList<FameLeaderboardEntry> GetLeaderboard(int top = 10)
    {
        var playerStanding = Standing;
        var list = new List<FameLeaderboardEntry>();

        var playerCityId = 1L;
        var playerName = "Lữ Khách";
        if (_broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) && ps is not null)
        {
            playerCityId = ps.CurrentCityId;
            playerName = ps.Name;
        }

        var cityName = "Lạc Dương";
        if (_broker.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var wm) && wm is not null)
        {
            var cit = wm.Find(playerCityId);
            if (cit is not null)
            {
                cityName = cit.NameVi;
            }
        }

        // Add player entry
        list.Add(new FameLeaderboardEntry(
            Rank: 0,
            CharacterId: 0,
            NameVi: playerName,
            RoleOrTitle: playerStanding.TitleVi,
            LocationName: cityName,
            Fame: playerStanding.Fame,
            RenownTierVi: playerStanding.RankVi,
            IsPlayer: true));

        // Attempt to fetch from NPC Registry & Society
        var npcsAdded = false;
        if (_broker.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry) && registry is not null)
        {
            _broker.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);

            foreach (var npc in registry.All)
            {
                var activity = society?.GetNpcActivity(npc.Id);
                var fame = activity?.Reputation ?? ComputeNpcInnateFame(npc);
                var npcCityName = cityName;
                if (activity is not null && wm is not null)
                {
                    var c = wm.Find(activity.CurrentCitadelId);
                    if (c is not null) npcCityName = c.NameVi;
                }
                else if (wm is not null)
                {
                    var c = wm.Find(npc.CitadelId);
                    if (c is not null) npcCityName = c.NameVi;
                }

                var (_, tierTitle, rankTitle, _, _) = ComputeTier(fame);

                list.Add(new FameLeaderboardEntry(
                    Rank: 0,
                    CharacterId: npc.Id,
                    NameVi: npc.NameVi,
                    RoleOrTitle: npc.StyleNameVi ?? npc.Role,
                    LocationName: npcCityName,
                    Fame: fame,
                    RenownTierVi: rankTitle,
                    IsPlayer: false));
            }

            npcsAdded = true;
        }

        if (!npcsAdded)
        {
            // Seeded canonical Three Kingdoms historical luminaries fallback
            var seeds = new (long Id, string Name, string Title, string City, int Fame)[]
            {
                (1001, "Đổng Trác", "Tướng Quốc", "Lạc Dương", 980),
                (1002, "Viên Thiệu", "Minh Chủ Minh Ước", "Nam Bì", 850),
                (1003, "Tào Tháo", "Tiêu Kỵ Hiệu Úy", "Hứa Xương", 720),
                (1004, "Viên Thuật", "Hậu Tướng Quân", "Lạc Dương", 680),
                (1005, "Lưu Biểu", "Kinh Châu Mục", "Tương Dương", 650),
                (1006, "Tôn Kiên", "Trường Sa Thái Thú", "Trường Sa", 620),
                (1007, "Lữ Bố", "Trung Lang Tướng", "Lạc Dương", 580),
                (1008, "Lưu Yên", "Ích Châu Mục", "Thành Đô", 560),
                (1009, "Mã Đằng", "Tây Lương Thái Thú", "Hán Trung", 520),
                (1010, "Lưu Bị", "Huyện Lệnh Bình Nguyên", "Khai Phong", 450),
                (1011, "Đinh Nguyên", "Kinh Triệu Doãn", "Lạc Dương", 410),
                (1012, "Khổng Dung", "Bắc Hải Tướng", "Nghiệp Thành", 390)
            };

            foreach (var s in seeds)
            {
                var (_, _, rankTitle, _, _) = ComputeTier(s.Fame);
                list.Add(new FameLeaderboardEntry(0, s.Id, s.Name, s.Title, s.City, s.Fame, rankTitle, false));
            }
        }

        // Sort descending by Fame
        list.Sort((a, b) => b.Fame.CompareTo(a.Fame));

        // Assign ordinal ranks
        var ranked = new List<FameLeaderboardEntry>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            ranked.Add(item with { Rank = i + 1 });
        }

        // Return top N
        return ranked.Take(top).ToList();
    }

    public void GainFame(
        int amount,
        string category,
        string titleVi,
        string narrativeVi,
        string? regionCode = null,
        int virtueDelta = 0,
        int credDelta = 0)
    {
        _db.Execute(
            @"UPDATE pl_social
              SET fame = MAX(0, fame + @fame),
                  virtue = MIN(100, MAX(0, virtue + @virtue)),
                  credibility = MIN(100, MAX(0, credibility + @cred))
              WHERE player_id = @id",
            new DbArgs()
                .Add("@fame", amount)
                .Add("@virtue", virtueDelta)
                .Add("@cred", credDelta)
                .Add("@id", _playerId));

        var today = _clock.Today;
        var dayIndex = today.Day;
        var dateText = today.ToString();

        _db.Execute(
            @"INSERT INTO pl_reputation_deed
              (player_id, day_index, date_text, category, fame_delta, virtue_delta, credibility_delta, title_vi, narrative_vi)
              VALUES (@id, @day, @date, @cat, @fame, @virtue, @cred, @title, @narrative)",
            new DbArgs()
                .Add("@id", _playerId)
                .Add("@day", dayIndex)
                .Add("@date", dateText)
                .Add("@cat", category)
                .Add("@fame", amount)
                .Add("@virtue", virtueDelta)
                .Add("@cred", credDelta)
                .Add("@title", titleVi)
                .Add("@narrative", narrativeVi));

        if (!string.IsNullOrWhiteSpace(regionCode))
        {
            _db.Execute(
                @"INSERT INTO pl_reputation_region (player_id, region_code, reputation)
                  VALUES (@id, @region, MAX(0, @fame))
                  ON CONFLICT(player_id, region_code) DO UPDATE SET reputation = MAX(0, reputation + @fame)",
                new DbArgs()
                    .Add("@id", _playerId)
                    .Add("@region", regionCode)
                    .Add("@fame", amount));
        }

        _log.Info($"reputation deed [{category}] +{amount} fame, +{virtueDelta} virtue, +{credDelta} cred: '{titleVi}'");
    }

    public ReputationActionResult PerformCharity(int donationCopper)
    {
        if (donationCopper < 20)
        {
            return new ReputationActionResult(false, "Số tiền phát chẩn tối thiểu là 20 Quan.", 0, 0, 0);
        }

        if (!_broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) || wallet is null)
        {
            return new ReputationActionResult(false, "Không thể truy cập ngân khố người chơi.", 0, 0, 0);
        }

        if (wallet.Balance < donationCopper)
        {
            return new ReputationActionResult(false, $"Ngân lượng không đủ (cần {donationCopper:N0} Quan, hiện có {wallet.Balance:N0} Quan).", 0, 0, 0);
        }

        var (cityName, regionCode) = GetPlayerLocation();

        wallet.Charge(donationCopper, JournalCodes.ReputationCharity, $"Phát chẩn tế bần cứu trợ bá tánh tại {cityName}");

        var fameGain = Math.Max(5, donationCopper / 3);
        var virtueGain = Math.Max(2, donationCopper / 15);
        var credGain = Math.Max(1, donationCopper / 25);

        GainFame(
            fameGain,
            "charity",
            "Phát Chẩn Cứu Bần",
            $"Bố thí {donationCopper:N0} Quan cứu trợ bá tánh cơ cực tại {cityName}, tấm lòng nhân từ được thiên hạ ca tụng.",
            regionCode,
            virtueGain,
            credGain);

        return new ReputationActionResult(
            true,
            $"Đã phát chẩn {donationCopper:N0} Quan tại {cityName}: +{fameGain} Uy Danh, +{virtueGain} Nhân Đức, +{credGain} Tín Nghĩa.",
            fameGain,
            virtueGain,
            donationCopper);
    }

    public ReputationActionResult HostBanquet(int costCopper)
    {
        if (costCopper < 50)
        {
            return new ReputationActionResult(false, "Chi phí mở yến tiệc hào kiệt tối thiểu là 50 Quan.", 0, 0, 0);
        }

        if (!_broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) || wallet is null)
        {
            return new ReputationActionResult(false, "Không thể truy cập ngân khố người chơi.", 0, 0, 0);
        }

        if (wallet.Balance < costCopper)
        {
            return new ReputationActionResult(false, $"Ngân lượng không đủ (cần {costCopper:N0} Quan, hiện có {wallet.Balance:N0} Quan).", 0, 0, 0);
        }

        var (cityName, regionCode) = GetPlayerLocation();

        wallet.Charge(costCopper, JournalCodes.ReputationBanquet, $"Khoản đãi yến tiệc danh sĩ tại {cityName}");

        var fameGain = Math.Max(10, costCopper / 4);
        var credGain = Math.Max(3, costCopper / 20);

        GainFame(
            fameGain,
            "social",
            "Yến Tiệc Hào Kiệt",
            $"Khoản đãi tiệc rượu linh đình chiêu đãi danh sĩ và tân khách tại {cityName}, tiếng tăm lừng lẫy.",
            regionCode,
            0,
            credGain);

        return new ReputationActionResult(
            true,
            $"Đã tổ chức yến tiệc tiêu tốn {costCopper:N0} Quan tại {cityName}: +{fameGain} Uy Danh, +{credGain} Tín Nghĩa.",
            fameGain,
            0,
            costCopper);
    }

    public ReputationActionResult ProclaimDeeds()
    {
        if (!_broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) || attrs is null)
        {
            return new ReputationActionResult(false, "Không thể kiểm tra thể lực người chơi.", 0, 0, 0);
        }

        if (attrs.Vitals.CurrentStamina < 20)
        {
            return new ReputationActionResult(false, "Thể lực không đủ (cần tối thiểu 20 Thể lực) để đăng đàn diễn thuyết.", 0, 0, 0);
        }

        var (cityName, regionCode) = GetPlayerLocation();

        attrs.ApplyFatigue(20);

        const int fameGain = 15;
        const int virtueGain = 2;
        const int credGain = 5;

        GainFame(
            fameGain,
            "speech",
            "Tuyên Dương Đại Nghĩa",
            $"Đăng đàn trước quảng trường thành {cityName}, tuyên bố chí hướng an bang tế thế, gây chấn động nhân tâm.",
            regionCode,
            virtueGain,
            credGain);

        return new ReputationActionResult(
            true,
            $"Đã đăng đàn diễn thuyết tại {cityName} (-20 Thể lực): +{fameGain} Uy Danh, +{virtueGain} Nhân Đức, +{credGain} Tín Nghĩa.",
            fameGain,
            virtueGain,
            0);
    }

    private (string CityName, string RegionCode) GetPlayerLocation()
    {
        var cityId = 1L;
        if (_broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) && ps is not null)
        {
            cityId = ps.CurrentCityId;
        }

        if (_broker.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(cityId);
            if (cit is not null)
            {
                return (cit.NameVi, cit.Region);
            }
        }

        return ("Lạc Dương", "trung_nguyen");
    }

    private static (int Tier, string Title, string Rank, int NextTierFame, string Perk) ComputeTier(int fame) => fame switch
    {
        < 100 => (0, "Vô Danh Tiểu Tốt", "Hạ Sĩ", 100, "Chưa có danh tiếng, chỉ có thể buôn nhỏ hoặc làm thuê nhật công."),
        < 300 => (1, "Hào Kiệt Mới Nổi", "Bản Huyện Hào Kiệt", 300, "Mở sạp hàng riêng ('Tiệm Của Bạn'), giảm 2% chi phí thị trường."),
        < 600 => (2, "Châu Huyện Chi Danh", "Quận Huyện Danh Sĩ", 600, "Tiếp cận danh sĩ Phẩm B, giảm 5% phí thương dịch vùng bản địa."),
        < 1200 => (3, "Danh Chấn Một Vùng", "Châu Mục Hiền Sĩ", 1200, "Gia nhập thế lực chư hầu, chiêu mộ môn khách phẩm A, giảm 8% cước đoàn buôn."),
        < 2500 => (4, "Danh Chấn Hoa Hạ", "Triều Đình Trọng Thần", 2500, "Yết kiến Thái Thú & Thứ Sử, nhận chức quan Tam Phẩm trở lên, giảm 12% thuế chợ."),
        < 5000 => (5, "Thiên Hạ Tri Danh", "Cột Trụ Giang Sơn", 5000, "Danh tướng phẩm S (Quan Vũ, Triệu Vân, Chu Du...) tôn kính tiếp đãi; mở quyền xin viện binh."),
        _ => (6, "Thiên Hạ Vô Song", "Tam Quốc Chí Tôn", 10000, "Đỉnh cao danh vọng thiên hạ, vạn dân ngưỡng vọng, lưu danh thiên cổ sử sách.")
    };

    private static int ComputeNpcInnateFame(NpcDef npc)
    {
        var a = npc.Attributes;
        var total = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com;
        return Math.Clamp((int)(total * 0.8), 50, 950);
    }
}
