using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.World;

/// <summary>
/// City Stroll & Urban Random Encounters implementation (<c>world.stroll</c>).
/// Allows the player to roam urban sublocations, relieving stress while triggering
/// historical Tam Quốc flavor encounters, interactive NPC social discussions,
/// wandering merchant bargains, and epiphanies.
/// </summary>
internal sealed class CityStrollService : ICityStroll
{
    private sealed class PendingEncounter
    {
        public StrollEventType EventType { get; set; }
        public string LocationName { get; set; } = "";
        public NpcDef? Npc { get; set; }
        public List<StrollMerchantOffer> Offers { get; set; } = new();
    }

    private readonly IWorldMap _map;
    private readonly ICapabilityBroker _broker;
    private readonly IGameClock _clock;
    private readonly ILog _log;

    private readonly Dictionary<long, PendingEncounter> _pendingEncounters = new();

    public CityStrollService(IWorldMap map, ICapabilityBroker broker, IGameClock clock, ILog log)
    {
        _map = map;
        _broker = broker;
        _clock = clock;
        _log = log;
    }

    public StrollResult Stroll(long cityId)
    {
        var city = _map.Find(cityId) ?? (_map.Citadels.Count > 0 ? _map.Citadels[0] : null);
        if (city is null)
        {
            return new StrollResult(false, "Lỗi Thành Trì", "Vô Định", "Không tìm thấy thành trì sở tại.", "", StrollEventType.Peaceful, 0, 0);
        }

        var sublocations = _map.Sublocations(city.Id);
        var subloc = sublocations.Count > 0
            ? sublocations[Random.Shared.Next(sublocations.Count)]
            : new SublocationDef(0, city.Id, "center", $"Nội Thị {city.NameVi}", "urban");

        // Vitals check
        _broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
        if (attrs is not null && attrs.Vitals.Fatigue >= 95)
        {
            return new StrollResult(
                false,
                "Kiệt Sức",
                subloc.NameVi,
                "Bạn đang quá kiệt sức (Mệt mỏi 95%+). Hãy nghỉ ngơi trước khi dạo phố du ngoạn!",
                "",
                StrollEventType.Peaceful,
                0,
                0);
        }

        // Apply baseline stamina consumption & stress relief
        attrs?.ApplyFatigue(5);
        attrs?.RecoverVitals(fatigueRelief: 0, stressRelief: 12);

        // 0. Check for pending Karma fruition / causality encounters first
        if (_broker.TryGet<IKarmaEngine>(CapabilityIds.KarmaEngine, out var karma) && karma is not null)
        {
            var pendingEvents = karma.CheckPendingEvents(city.Id, (int)_clock.Today.DayIndex);
            if (pendingEvents.Count > 0)
            {
                var evt = pendingEvents[0];
                if (evt.Options.Count > 0)
                {
                    var chosenOpt = evt.Options[0];
                    var resolution = karma.ResolveChoice(evt.SeedId, chosenOpt.ChoiceKey);

                    var deltaSummary = $"{(resolution.MoneyChange != 0 ? (resolution.MoneyChange > 0 ? $"+{resolution.MoneyChange} Quan, " : $"{resolution.MoneyChange} Quan, ") : "")}{(resolution.VirtueChange != 0 ? (resolution.VirtueChange > 0 ? $"+{resolution.VirtueChange} Đức, " : $"{resolution.VirtueChange} Đức, ") : "")}{(resolution.FameChange != 0 ? (resolution.FameChange > 0 ? $"+{resolution.FameChange} Danh, " : $"{resolution.FameChange} Danh, ") : "")}-12 Căng thẳng";

                    _log.Info($"[Stroll] Triggered Karma fruition event {evt.SeedId} at {city.NameVi}: {resolution.OutcomeVi}");
                    _pendingEncounters.Remove(city.Id);
                    return new StrollResult(
                        true,
                        evt.Stage == 2 ? $"⚖️ Quả Báo Hiện Tiền: {evt.TitleVi}" : $"⚖️ Nhân Duyên Khởi Điểm: {evt.TitleVi}",
                        subloc.NameVi,
                        $"{evt.NarrativeVi}\n\n[Lựa chọn: {chosenOpt.LabelVi}]\n{resolution.OutcomeVi}",
                        deltaSummary,
                        StrollEventType.KarmaFruition,
                        5,
                        12);
                }
            }
        }

        var roll = Random.Shared.Next(100);
        var seed = (int)(_clock.Today.DayIndex * 37 + cityId * 19 + Random.Shared.Next(10000));

        // 1. Peaceful Walk (60% probability: roll 0..59) - Highest frequency
        if (roll < 60)
        {
            _pendingEncounters.Remove(city.Id);
            _log.Info($"[Stroll] Peaceful walk at {subloc.NameVi}");
            return new StrollResult(
                true,
                "Phố Phường Thanh Bình",
                subloc.NameVi,
                $"Dạo bước trên phiến đá rêu phong tại {subloc.NameVi}, lắng nghe tiếng vó ngựa xa xa và tiếng rao hàng của tiểu thương. Phố phường thái bình, một ngày dạo chơi êm ả không gặp biến cố nào đặc biệt.",
                "-12 Căng thẳng, Phục hồi tinh thần",
                StrollEventType.Peaceful,
                5,
                12);
        }

        // 2. Wandering Merchant Encounter (15% probability: roll 60..74) - Multi-item interactive choice
        if (roll < 75)
        {
            var offers = GenerateMerchantOffers(seed);
            _pendingEncounters[city.Id] = new PendingEncounter
            {
                EventType = StrollEventType.MarketBargain,
                LocationName = subloc.NameVi,
                Offers = offers
            };

            _log.Info($"[Stroll] Wandering merchant encountered at {subloc.NameVi}: {offers.Count} offers");
            return new StrollResult(
                true,
                "Kỳ Thương Du Phương",
                subloc.NameVi,
                $"Tại {subloc.NameVi}, bạn bắt gặp một thương nhân du phương phong trần đang trải tấm thảm nhung, bày ra vài món kỳ trân bảo vật hiếm thấy với giá hữu nghị. Vị khách buôn tươi cười mời bạn chọn lấy một món bảo vật tâm đắc.",
                "Chờ bạn chọn món muốn mua...",
                StrollEventType.MarketBargain,
                5,
                12,
                RequiresChoice: true,
                MerchantOffers: offers);
        }

        // 3. General / Scholar Encounter (12% probability: roll 75..86) - Interactive social choices
        if (roll < 87)
        {
            _broker.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcs);
            var localNpcs = npcs?.FindByCitadel(city.Id) ?? Array.Empty<NpcDef>();

            if (localNpcs.Count > 0)
            {
                var npc = localNpcs[Random.Shared.Next(localNpcs.Count)];
                _pendingEncounters[city.Id] = new PendingEncounter
                {
                    EventType = StrollEventType.NpcEncounter,
                    LocationName = subloc.NameVi,
                    Npc = npc
                };

                var choices = new List<StrollChoiceOption>
                {
                    new("dialogue:discourse", "💬 Đàm Luận Thế Cuộc", $"Luận bàn thế chân vạc của các lộ chư hầu cùng {npc.NameVi} (Đòi hỏi Trí Lực cao)."),
                    new("dialogue:spar", "⚔️ Thỉnh Giáo Võ Nghệ", $"Thỉnh giáo chiêu thức quyền cước binh khí (Đòi hỏi Võ Lực cao, tiêu hao thể lực)."),
                    new("dialogue:wine", "🍶 Mời Rượu Giao Hảo", $"Chi tiêu 30 Quan mời một vò rượu Đỗ Khang kết nghĩa tri âm (Chắc chắn thân mật tăng cao, giải tỏa áp lực).", "Tốn 30 Quan"),
                    new("dialogue:leave", "👋 Lễ Độ Cáo Từ", "Chắp tay thi lễ rồi dạo bước nơi khác, tránh làm phiền đối phương.")
                };

                _log.Info($"[Stroll] Encountered NPC {npc.NameVi} at {subloc.NameVi} awaiting interaction");
                return new StrollResult(
                    true,
                    "Tao Ngộ Danh Sĩ",
                    subloc.NameVi,
                    $"Tại {subloc.NameVi}, bạn tình cờ tao ngộ {npc.NameVi} ({npc.Role}). Nhìn thần thái bất phàm cùng phong cốt uy nghiêm của đối phương, bạn cân nhắc nên tiếp cận giao thiệp theo cách nào.",
                    "Chờ bạn chọn cách giao tiếp...",
                    StrollEventType.NpcEncounter,
                    5,
                    12,
                    RequiresChoice: true,
                    Choices: choices,
                    TargetNpcId: npc.Id);
            }
        }

        // 4. Righteous Deed (7% probability: roll 87..93)
        if (roll < 94)
        {
            _pendingEncounters.Remove(city.Id);
            attrs?.GainFame(15, "Trượng nghĩa bắt trộm tại nội thị", virtueDelta: 2, credDelta: 1);
            attrs?.AddExp("WAR", 15);

            _log.Info($"[Stroll] Righteous deed at {subloc.NameVi}: +15 Fame");
            return new StrollResult(
                true,
                "Trượng Nghĩa Hiệp Hành",
                subloc.NameVi,
                $"Tại {subloc.NameVi}, bạn chứng kiến một toán lưu manh đang bức hiếp dân lành. Bạn lập tức xuất thủ can thiệp, bằng khí thế hiên ngang đã đuổi sạch đám côn đồ, bá tánh xung quanh đồng thanh reo hò ca ngợi.",
                "+15 Điểm Danh Tiếng, +2 Đức Độ, +15 EXP Võ Lực, -12 Căng thẳng",
                StrollEventType.RighteousDeed,
                5,
                12);
        }

        // 5. Teahouse Rumor (4% probability: roll 94..97)
        if (roll < 98)
        {
            _pendingEncounters.Remove(city.Id);
            attrs?.RecoverVitals(0, stressRelief: 15);
            attrs?.AddExp("INT", 10);

            _log.Info($"[Stroll] Teahouse rumor at {subloc.NameVi}");
            return new StrollResult(
                true,
                "Trà Quán Luận Thế Cuộc",
                subloc.NameVi,
                $"Ghé chân vào một sạp trà cổ kính tại {subloc.NameVi}, bạn vừa nhấp ngụm trà xuân vừa lắng nghe văn nhân sĩ tử bình phẩm về thế lực các lộ chư hầu. Tinh thần vô cùng thanh thản.",
                "-27 Căng thẳng, +10 EXP Trí Lực",
                StrollEventType.TeahouseRumor,
                5,
                27);
        }

        // 6. Epiphany / Awakening Chance (2% probability: roll 98..99)
        _pendingEncounters.Remove(city.Id);
        var practice = attrs?.PracticeSkill("dieu_ke_dyn", 40);
        attrs?.AddExp("INT", 25);
        var practiceText = practice?.Awakened == true
            ? "Kỹ năng [Diệu Kế] ĐỘT PHÁ GIÁC NGỘ!"
            : practice?.LeveledUp == true
                ? "Kỹ năng [Diệu Kế] thăng cấp!"
                : "+40 EXP Kỹ Năng";

        _log.Info($"[Stroll] Epiphany at {subloc.NameVi}: {practiceText}");
        return new StrollResult(
            true,
            "Đốn Ngộ Binh Pháp",
            subloc.NameVi,
            $"Đứng trước cảnh sắc uy nghi tráng lệ tại {subloc.NameVi}, tâm cảnh bạn bỗng chốc đốn ngộ! Tinh hoa võ học mưu lược như ngưng đọng lại trong tâm khảm.",
            $"{practiceText}, +25 EXP Trí Lực, -12 Căng thẳng",
            StrollEventType.Epiphany,
            5,
            12);
    }

    public StrollResult ResolveChoice(long cityId, string choiceKey, string? param = null)
    {
        if (!_pendingEncounters.TryGetValue(cityId, out var pending))
        {
            return new StrollResult(
                false,
                "Không Thể Thao Tác",
                "Phố Phường",
                "Không có sự kiện tương tác nào đang chờ xử lý tại nơi này.",
                "",
                StrollEventType.Peaceful,
                0,
                0);
        }

        _broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);

        // A. Resolve Wandering Merchant Choice
        if (pending.EventType == StrollEventType.MarketBargain)
        {
            if (choiceKey == "buy")
            {
                var offer = pending.Offers.FirstOrDefault(o =>
                    o.ItemCode.Equals(param, StringComparison.OrdinalIgnoreCase) ||
                    o.ItemId.ToString() == param);

                if (offer is null)
                {
                    return new StrollResult(
                        false,
                        "Hàng Hóa Không Khả Dụng",
                        pending.LocationName,
                        "Vật phẩm bạn chọn hiện không có trong quầy của thương nhân.",
                        "",
                        StrollEventType.MarketBargain,
                        0,
                        0,
                        RequiresChoice: true,
                        MerchantOffers: pending.Offers);
                }

                _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet);
                if (wallet is null || wallet.Liquid < offer.UnitPrice)
                {
                    return new StrollResult(
                        false,
                        "Ngân Lượng Không Đủ",
                        pending.LocationName,
                        $"Bạn không đủ {offer.UnitPrice} Quan ngân lượng trong túi để mua [{offer.ItemNameVi}]. Thương nhân ái ngại nhìn bạn.",
                        "Giao dịch không thành do thiếu tiền",
                        StrollEventType.MarketBargain,
                        0,
                        0,
                        RequiresChoice: true,
                        MerchantOffers: pending.Offers);
                }

                // Charge wallet
                wallet.Charge(offer.UnitPrice, JournalCodes.MarketBuy, $"Mua {offer.ItemNameVi} từ thương nhân du ngoạn");

                // Add item to player bag
                if (_broker.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag is not null)
                {
                    bag.Add(cityId, offer.ItemId, offer.Quantity);
                }

                _pendingEncounters.Remove(cityId);
                attrs?.RecoverVitals(0, stressRelief: 10);
                _log.Info($"[Stroll] Bought {offer.ItemNameVi} for {offer.UnitPrice} Quan");

                return new StrollResult(
                    true,
                    "Giao Dịch Thành Công",
                    pending.LocationName,
                    $"Bạn chi trả {offer.UnitPrice} Quan để mua [{offer.ItemNameVi}] từ tay thương nhân du phương. Vị khách buôn tươi cười gói ghém cẩn thận trao cho bạn, miệng không ngớt chúc phúc cho hành trình phiêu bạt của bạn rồi thu xếp quầy hàng rời đi.",
                    $"+{offer.Quantity} {offer.ItemNameVi}, -{offer.UnitPrice} Quan, -10 Căng thẳng",
                    StrollEventType.MarketBargain,
                    0,
                    10,
                    RequiresChoice: false);
            }
            else // decline or leave
            {
                _pendingEncounters.Remove(cityId);
                _log.Info("[Stroll] Declined merchant offer");
                return new StrollResult(
                    true,
                    "Cáo Biệt Kỳ Thương",
                    pending.LocationName,
                    "Bạn mỉm cười chắp tay từ chối khéo. Vị thương nhân du phương gật đầu tán thưởng: 'Kỳ duyên vạn dặm, xin hẹn ngày tái ngộ!', rồi cất gánh dạo bước sang phố khác.",
                    "Không có biến động tài sản",
                    StrollEventType.MarketBargain,
                    0,
                    0,
                    RequiresChoice: false);
            }
        }

        // B. Resolve General / Scholar Social Choice
        if (pending.EventType == StrollEventType.NpcEncounter)
        {
            var npc = pending.Npc;
            if (npc is null)
            {
                _pendingEncounters.Remove(cityId);
                return new StrollResult(false, "Lỗi Tương Tác", pending.LocationName, "Không tìm thấy thông tin nhân vật.", "", StrollEventType.Peaceful, 0, 0);
            }

            _broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social);
            var playerAttrs = attrs?.Attributes ?? new CharacterAttributes(50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50);

            if (choiceKey == "dialogue:discourse")
            {
                _pendingEncounters.Remove(cityId);
                var d20 = Random.Shared.Next(1, 21);
                var playerScore = playerAttrs.Int + (playerAttrs.Pol / 2) + d20;
                var npcThreshold = npc.Attributes.Int + 5;

                if (playerScore >= npcThreshold)
                {
                    social?.ChatWithNpc(npc.Id);
                    attrs?.AddExp("INT", 25);
                    attrs?.RecoverVitals(0, stressRelief: 15);

                    return new StrollResult(
                        true,
                        "Luận Bàn Thế Cuộc — Tri Kỷ Tương Đắc",
                        pending.LocationName,
                        $"Trước ánh mắt uy nghiêm của {npc.NameVi}, bạn tự tin phân tích cục diện thiên hạ phân tranh và sách lược kinh bang tế thế. {npc.NameVi} vuốt râu tán thưởng: 'Kiến giải của các hạ thật khoáng đạt sâu sắc, như vén mây mù thấy trời xanh!'. Cuộc tương đàm vô cùng tâm đắc.",
                        $"+Thân Mật với {npc.NameVi}, +25 EXP Trí Lực, -15 Căng thẳng",
                        StrollEventType.NpcEncounter,
                        0,
                        15,
                        RequiresChoice: false,
                        TargetNpcId: npc.Id);
                }
                else
                {
                    attrs?.AddExp("INT", 10);
                    attrs?.RecoverVitals(0, stressRelief: 5);

                    return new StrollResult(
                        true,
                        "Luận Bàn Thế Cuộc — Lắng Nghe Chỉ Điểm",
                        pending.LocationName,
                        $"Bạn bày tỏ kiến giải của mình, song {npc.NameVi} lắng nghe rồi từ tốn chỉ ra vài chỗ sơ hở trong đại cuộc. Tuy vậy đối phương vẫn ghi nhận lòng cầu tiến và sự nhiệt thành của bạn.",
                        $"+Ít Thân Mật với {npc.NameVi}, +10 EXP Trí Lực, -5 Căng thẳng",
                        StrollEventType.NpcEncounter,
                        0,
                        5,
                        RequiresChoice: false,
                        TargetNpcId: npc.Id);
                }
            }

            if (choiceKey == "dialogue:spar")
            {
                _pendingEncounters.Remove(cityId);
                var d20 = Random.Shared.Next(1, 21);
                var playerScore = playerAttrs.War + (playerAttrs.Ldr / 2) + d20;
                var npcThreshold = npc.Attributes.War;

                attrs?.ApplyFatigue(5);

                if (playerScore >= npcThreshold)
                {
                    social?.ChatWithNpc(npc.Id);
                    attrs?.AddExp("WAR", 30);
                    attrs?.RecoverVitals(0, stressRelief: 10);

                    return new StrollResult(
                        true,
                        "Thỉnh Giáo Võ Nghệ — Khí Phách Hào Sảng",
                        pending.LocationName,
                        $"Hai bên mượn mộc côn thử sức mười mấy hiệp. Chiêu pháp quyền cước của bạn kín kẽ dũng mãnh, khiến {npc.NameVi} phải trầm trồ khen ngợi khí khái trượng phu, hào phóng chỉ điểm thêm vài bí quyết thực chiến độc môn.",
                        $"+Thân Mật với {npc.NameVi}, +30 EXP Võ Lực, -10 Căng thẳng, +5 Mệt mỏi",
                        StrollEventType.NpcEncounter,
                        5,
                        10,
                        RequiresChoice: false,
                        TargetNpcId: npc.Id);
                }
                else
                {
                    attrs?.AddExp("WAR", 15);

                    return new StrollResult(
                        true,
                        "Thỉnh Giáo Võ Nghệ — Lĩnh Hội Yếu Quyết",
                        pending.LocationName,
                        $"Bản lĩnh võ nghệ của {npc.NameVi} quả nhiên danh bất hư truyền. Trải qua vài chục chiêu giao phong, bạn tuy bị đối phương áp chế nhưng đã kịp thời lĩnh hội được nhiều kinh nghiệm quyền cước đắt giá.",
                        $"+Ít Thân Mật với {npc.NameVi}, +15 EXP Võ Lực, +5 Mệt mỏi",
                        StrollEventType.NpcEncounter,
                        5,
                        0,
                        RequiresChoice: false,
                        TargetNpcId: npc.Id);
                }
            }

            if (choiceKey == "dialogue:wine")
            {
                _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet);
                if (wallet is null || wallet.Liquid < 30)
                {
                    return new StrollResult(
                        false,
                        "Không Đủ Ngân Lượng",
                        pending.LocationName,
                        $"Trong đãy túi của bạn không đủ 30 Quan ngân lượng để mua vò rượu ngon thiết đãi {npc.NameVi}!",
                        "Cần có ít nhất 30 Quan",
                        StrollEventType.NpcEncounter,
                        0,
                        0,
                        RequiresChoice: true,
                        Choices: pending.Npc is null ? null : new List<StrollChoiceOption>
                        {
                            new("dialogue:discourse", "💬 Đàm Luận Thế Cuộc", $"Luận bàn thế chân vạc của các lộ chư hầu cùng {npc.NameVi} (Đòi hỏi Trí Lực cao)."),
                            new("dialogue:spar", "⚔️ Thỉnh Giáo Võ Nghệ", $"Thỉnh giáo chiêu thức quyền cước binh khí (Đòi hỏi Võ Lực cao, tiêu hao thể lực)."),
                            new("dialogue:wine", "🍶 Mời Rượu Giao Hảo", "Chi tiêu 30 Quan mời một vò rượu Đỗ Khang kết nghĩa tri âm.", "Tốn 30 Quan"),
                            new("dialogue:leave", "👋 Lễ Độ Cáo Từ", "Chắp tay thi lễ rồi dạo bước nơi khác, tránh làm phiền đối phương.")
                        },
                        TargetNpcId: npc.Id);
                }

                // Charge wallet 30 copper
                wallet.Charge(30, JournalCodes.ReputationBanquet, $"Mời rượu {npc.NameVi}");

                _pendingEncounters.Remove(cityId);
                social?.ChatWithNpc(npc.Id);
                attrs?.RecoverVitals(0, stressRelief: 25);

                return new StrollResult(
                    true,
                    "Nâng Chén Tiêu Dao — Kết Giao Tri Âm",
                    pending.LocationName,
                    $"Bạn hào sảng chi 30 Quan mua vò rượu hoa điêu thơm nức thiết đãi {npc.NameVi}. Cả hai cùng nâng chén dưới bóng liễu râm mát, đối ẩm say sưa, mọi âu lo căng thẳng đều tan biến như mây khói.",
                    $"+Thân Mật Lớn với {npc.NameVi}, -30 Quan, -25 Căng thẳng",
                    StrollEventType.NpcEncounter,
                    0,
                    25,
                    RequiresChoice: false,
                    TargetNpcId: npc.Id);
            }

            // dialogue:leave
            _pendingEncounters.Remove(cityId);
            return new StrollResult(
                true,
                "Lễ Độ Cáo Biệt",
                pending.LocationName,
                $"Bạn giữ phong thái nho nhã chắp tay thi lễ rồi cáo từ {npc.NameVi}. Đối phương mỉm cười gật đầu đáp lễ, nhìn theo bạn dạo bước trên phố cổ.",
                "Không có biến động",
                StrollEventType.NpcEncounter,
                0,
                0,
                RequiresChoice: false);
        }

        return new StrollResult(false, "Thao Tác Không Hợp Lệ", pending.LocationName, "Lựa chọn không phù hợp với kỳ ngộ này.", "", StrollEventType.Peaceful, 0, 0);
    }

    private List<StrollMerchantOffer> GenerateMerchantOffers(int seed)
    {
        _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog);
        var allItems = catalog?.All ?? Array.Empty<ItemDef>();
        var offers = new List<StrollMerchantOffer>();

        if (allItems.Count > 0)
        {
            var step = Math.Max(1, allItems.Count / 3);
            var offset = Random.Shared.Next(allItems.Count);

            for (int i = 0; i < 3; i++)
            {
                var idx = (offset + i * step) % allItems.Count;
                var item = allItems[idx];
                var discountPermille = 750 + Random.Shared.Next(150); // 75% to 90%
                var discountedPrice = Math.Max(15, (long)(item.BasePrice * discountPermille / 1000));
                var icon = GetItemIcon(item.Category);

                offers.Add(new StrollMerchantOffer(
                    item.Id,
                    item.Code,
                    item.NameVi,
                    item.Category,
                    icon,
                    discountedPrice,
                    1,
                    $"Hàng quý du thương, ưu đãi {discountedPrice} Quan (Gốc: {item.BasePrice} Quan)."));
            }
        }
        else
        {
            // Fallback default offers if catalog not loaded yet
            offers.Add(new StrollMerchantOffer(1, "ruou_do_khang", "Rượu Đỗ Khang", "Bảo Vật", "🍶", 35, 1, "Rượu quý giải sầu khai tâm trí."));
            offers.Add(new StrollMerchantOffer(2, "kim_sang_duoc", "Kim Sáng Dược", "Dược Phẩm", "💊", 25, 1, "Dược phẩm trị thương hiệu nghiệm."));
            offers.Add(new StrollMerchantOffer(3, "binh_thu_tan_quyen", "Binh Thư Tàn Quyển", "Sách", "📜", 60, 1, "Binh thư lưu truyền nâng cao tài trí."));
        }

        return offers;
    }

    private static string GetItemIcon(string category) => category.ToLowerInvariant() switch
    {
        "weapon" or "vũ khí" => "⚔️",
        "armor" or "giáp trụ" => "🛡️",
        "horse" or "chiến mã" => "🐎",
        "book" or "sách" or "binh thư" => "📜",
        "medicine" or "dược phẩm" => "💊",
        "treasure" or "bảo vật" or "ngọc" => "💎",
        "food" or "lương thảo" => "🌾",
        _ => "📦"
    };
}
