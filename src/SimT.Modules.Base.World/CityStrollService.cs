using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.World;

/// <summary>
/// City Stroll & Urban Random Encounters implementation (<c>world.stroll</c>).
/// Allows the player to roam urban sublocations, relieving stress while triggering
/// historical Tam Quốc flavor encounters, NPC social interactions, and epiphanies.
/// </summary>
internal sealed class CityStrollService : ICityStroll
{
    private readonly IWorldMap _map;
    private readonly ICapabilityBroker _broker;
    private readonly IGameClock _clock;
    private readonly ILog _log;

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
            ? sublocations[(int)((_clock.Today.DayIndex + Math.Abs(Environment.TickCount64)) % sublocations.Count)]
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

        var seed = (int)(_clock.Today.DayIndex * 37 + cityId * 19 + Math.Abs(Environment.TickCount64) % 1000);
        var roll = Math.Abs(seed % 100);

        // 1. NPC Encounter (30%)
        if (roll < 30)
        {
            _broker.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcs);
            var localNpcs = npcs?.FindByCitadel(city.Id) ?? Array.Empty<NpcDef>();

            if (localNpcs.Count > 0)
            {
                var npc = localNpcs[Math.Abs((seed / 7) % localNpcs.Count)];
                _broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social);
                var chatNote = social?.ChatWithNpc(npc.Id);

                var primaryAttr = npc.Attributes.War >= 75 ? "WAR" : npc.Attributes.Int >= 75 ? "INT" : npc.Attributes.Pol >= 75 ? "POL" : "LDR";
                attrs?.AddExp(primaryAttr, 20);

                _log.Info($"[Stroll] Encountered NPC {npc.NameVi} at {subloc.NameVi}");
                return new StrollResult(
                    true,
                    "Tao Ngộ Danh Sĩ",
                    subloc.NameVi,
                    $"Tại {subloc.NameVi}, bạn tình cờ tao ngộ {npc.NameVi} ({npc.Role}). Hai bên cùng nâng chén trà thơm đàm luận thế sự Tam Quốc, lời lẽ vô cùng tương đắc.",
                    $"+Thân Mật với {npc.NameVi}, +20 EXP {primaryAttr}, -12 Căng thẳng",
                    StrollEventType.NpcEncounter,
                    5,
                    12);
            }
        }

        // 2. Market Bargain / Antique Discovery (20%)
        if (roll < 50)
        {
            _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet);
            var cashGain = 60 + (Math.Abs(seed % 5) * 15);
            wallet?.Pay(cashGain, JournalCodes.StartingCash, "Kỳ ngộ buôn bán vãng lai");

            _log.Info($"[Stroll] Market bargain at {subloc.NameVi}: +{cashGain} Quan");
            return new StrollResult(
                true,
                "Kỳ Thương Cổ Vật",
                subloc.NameVi,
                $"Tại {subloc.NameVi}, bạn bắt gặp một thương nhân ngoại vực đang bày bán kỳ trân dị bảo. Bằng con mắt tinh tường của một thương nhân, bạn đã chọn được món đồ cổ quý giá và kiếm được món lời ngay tại chỗ!",
                $"+{cashGain} Quan ngân lượng, -12 Căng thẳng",
                StrollEventType.MarketBargain,
                5,
                12);
        }

        // 3. Righteous Deed (20%)
        if (roll < 70)
        {
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

        // 4. Epiphany / Awakening Chance (10%)
        if (roll < 80)
        {
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

        // 5. Teahouse Rumor (10%)
        if (roll < 90)
        {
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

        // 6. Peaceful Walk (10%)
        _log.Info($"[Stroll] Peaceful walk at {subloc.NameVi}");
        return new StrollResult(
            true,
            "Phố Phường Thanh Bình",
            subloc.NameVi,
            $"Dạo bước trên phiến đá rêu phong tại {subloc.NameVi}, lắng nghe tiếng vó ngựa xa xa và tiếng rao hàng của tiểu thương. Phố phường thái bình, lòng người yên ả.",
            "-12 Căng thẳng, Phục hồi tinh thần",
            StrollEventType.Peaceful,
            5,
            12);
    }
}
