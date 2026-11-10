using SimT.Contracts.Ledger;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.Player;

/// <summary>
/// Evaluates progress towards the 5 Endgame Ambitions in the Three Kingdoms sandbox.
/// </summary>
public sealed class EndgameAmbitionService : IEndgameAmbitions
{
    private readonly ICapabilityBroker _capabilities;
    private readonly ILedger? _ledger;

    public EndgameAmbitionService(ICapabilityBroker capabilities, ILedger? ledger = null)
    {
        _capabilities = capabilities;
        _ledger = ledger;
    }

    public EndgameAmbitionState Evaluate()
    {
        var branches = new List<AmbitionBranch>
        {
            EvaluateKingmaker(),
            EvaluateMerchantSovereign(),
            EvaluateDynastyPatriarch(),
            EvaluateGrandMartialMaster(),
            EvaluateRenownedSage()
        };

        var completedCount = branches.Count(b => b.IsFulfilled);
        return new EndgameAmbitionState(branches, completedCount, completedCount > 0);
    }

    public IReadOnlyList<AchievementBadgeDef> EvaluateAchievements()
    {
        var hasWallet = _capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet is not null;
        var hasAttrs = _capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null;
        var hasRep = _capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null;
        var hasLineage = _capabilities.TryGet<ILineageHouse>(CapabilityIds.LineageHouse, out var lineage) && lineage is not null;
        var hasMilitary = _capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military is not null;
        var hasSocial = _capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null;
        var hasCourt = _capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null;
        var hasEspionage = _capabilities.TryGet<IEspionageNetwork>(CapabilityIds.EspionageNetwork, out var espionage) && espionage is not null;

        var balance = hasWallet ? wallet!.Balance : (_ledger?.Balance ?? 0);
        var fame = hasRep ? rep!.Standing.Fame : 0;
        var war = hasAttrs ? attrs!.Attributes.War : 0;
        var childCount = hasLineage ? lineage!.ChildCount : 0;
        var generation = hasLineage ? lineage!.CurrentGeneration : 1;
        var alliances = hasLineage ? lineage!.PoliticalMarriages.Count : 0;
        var clearedStrongholds = hasMilitary ? military!.AllStrongholds().Sum(s => s.ClearedCount) : 0;
        var swornBrothers = hasSocial ? social!.AllRelationships().Count(r => r.Affinity >= 80) : 0;
        var officeRank = hasCourt && court!.OfficeId > 0 ? (court.Offices.FirstOrDefault(o => o.Id == court.OfficeId)?.Rank ?? (int)court.OfficeId) : 0;
        var acquiredTomes = hasEspionage ? espionage!.GetHermitTomes().Count(t => t.IsAcquired) : 0;
        var spyCells = hasEspionage ? espionage!.GetSpyNodes().Count(n => n.IntelLevel > 0) : 0;

        return
        [
            new("ach_first_fortune", "Thương Nhân Khởi Nghiệp", "Rising Merchant", "Thương Nghiệp", "🪙", "Tích lũy 5.000 Quan ngân lượng trong thương quỹ", balance, 5000, balance >= 5000),
            new("ach_grand_tycoon", "Phú Giả Nhất Phương", "Regional Tycoon", "Thương Nghiệp", "💎", "Tích lũy 25.000 Quan ngân lượng", balance, 25000, balance >= 25000),
            new("ach_bandit_slayer", "Bình Tặc Tiên Phong", "Bandit Suppressor", "Võ Nghiệp", "⚔️", "Tiễu trừ thành công ít nhất 1 sào huyệt sơn tặc", clearedStrongholds, 1, clearedStrongholds >= 1),
            new("ach_peerless_warrior", "Vạn Phu Mạc Địch", "Peerless Warrior", "Võ Nghiệp", "🐯", "Rèn luyện Võ Lực đạt mốc 80 điểm trở lên", war, 80, war >= 80),
            new("ach_sworn_oath", "Tri Kỷ Tâm Giao", "Sworn Confidant", "Giao Hảo", "🍑", "Kết giao sinh tử (Hảo cảm ≥ 80) với ít nhất 1 danh sĩ", swornBrothers, 1, swornBrothers >= 1),
            new("ach_imperial_mandarin", "Bước Chân Quan Trường", "Imperial Mandarin", "Chính Trị", "🏛️", "Được sắc phong quan chức triều đình hoặc chư hầu", officeRank, 1, officeRank >= 1),
            new("ach_dynasty_heir", "Khai Chi Tán Diệp", "Dynasty Scion", "Gia Tộc", "🌿", "Sinh con nối dõi hoặc truyền thừa sang Đời thứ 2", Math.Max(childCount, generation - 1), 1, childCount >= 1 || generation >= 2),
            new("ach_royal_inlaw", "Quốc Thích Liên Hôn", "Royal In-Law", "Gia Tộc", "🤝", "Kết thông gia liên hôn với ít nhất 1 thế lực chư hầu", alliances, 1, alliances >= 1),
            new("ach_shadow_master", "Thiên La Địa Võng", "Spymaster", "Mật Vụ", "🕵️", "Thiết lập mạng lưới Tế Tác tại ít nhất 2 thành trì", spyCells, 2, spyCells >= 2),
            new("ach_hermit_scholar", "Chân Truyền Ẩn Sĩ", "Hermit's Disciple", "Kỳ Ngộ", "📘", "Tầm sư học đạo và lĩnh hội ít nhất 1 bộ Kỳ Thư Tam Quốc", acquiredTomes, 1, acquiredTomes >= 1),
            new("ach_realm_renowned", "Danh Chấn Cửu Châu", "Realm Renowned", "Danh Vọng", "🌟", "Uy danh giang hồ & triều dã đạt 150 điểm", fame, 150, fame >= 150)
        ];
    }

    private AmbitionBranch EvaluateKingmaker()
    {
        var hasCourt = _capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null;
        var hasRep = _capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null;

        var allegianceId = hasCourt ? court!.AllegianceFactionId : 0;
        var inFaction = allegianceId > 0;
        var officeRank = 0;
        if (hasCourt && court!.OfficeId > 0)
        {
            officeRank = court.Offices.FirstOrDefault(o => o.Id == court.OfficeId)?.Rank ?? (int)court.OfficeId;
        }

        var controlledCitadels = 0;
        if (hasCourt && inFaction)
        {
            controlledCitadels = court!.Governances.Count(g => g.FactionId == allegianceId);
        }

        var fame = hasRep ? rep!.Standing.Fame : 0;

        var reqs = new List<AmbitionRequirement>
        {
            new("km_faction", "Quy phục thế lực minh chúa", "Pledged allegiance to a faction", inFaction ? 1 : 0, 1, "Minh Chúa", inFaction),
            new("km_office", "Quan hàm triều đình (Tứ phẩm trở lên)", "Court office rank (Rank 4+)", officeRank, 4, "Phẩm", officeRank >= 4),
            new("km_citadels", "Thành trì thế lực kiểm soát (≥ 3 thành)", "Faction controlled citadels (≥ 3)", controlledCitadels, 3, "Thành", controlledCitadels >= 3),
            new("km_fame", "Uy danh chấn động triều dã (≥ 250)", "Imperial renown (≥ 250)", fame, 250, "Điểm", fame >= 250)
        };

        var percent = CalculatePercent(reqs);
        var isFulfilled = reqs.All(r => r.IsMet);

        return new AmbitionBranch(
            AmbitionCategory.Kingmaker,
            "Phò Tá Đế Vương",
            "Kingmaker",
            "Phò tá một thế lực chư hầu lập nên vương triều, chấp chính triều đình",
            "👑",
            percent,
            isFulfilled,
            reqs,
            "Khai Quốc Công Thần · Nhất Đại Thừa Tướng",
            "Giang sơn trải qua muôn vàn khói lửa loạn lạc, dưới tài mưu lược kinh bang tế thế và tầm nhìn của bạn, chủ công đã đánh dẹp quần hùng, đăng cơ hoàng đế thống nhất giang sơn. Bạn được sắc phong Thừa Tướng tước Vương, quyền khuynh triều dã nhưng hết lòng vì muôn dân trăm họ. Tên tuổi của bạn được tạc trên cửu đỉnh, lưu danh muôn đời như bậc tể tướng mẫu mực nghìn năm có một.");
    }

    private AmbitionBranch EvaluateMerchantSovereign()
    {
        var hasWallet = _capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet is not null;
        var hasCaravan = _capabilities.TryGet<ICaravanGuild>(CapabilityIds.CaravanGuild, out var caravan) && caravan is not null;
        var hasAttrs = _capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null;
        var hasRep = _capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null;

        var balance = hasWallet ? wallet!.Balance : (_ledger?.Balance ?? 0);
        var fleetTier = hasCaravan ? caravan!.Fleet.Tier : 0;
        var comAttr = hasAttrs ? attrs!.Attributes.Com : 0;
        var credibility = hasRep ? rep!.Standing.Credibility : 0;

        var reqs = new List<AmbitionRequirement>
        {
            new("ms_wealth", "Tích lũy ngân quỹ đại phú (≥ 50,000 quan)", "Accumulated treasury (≥ 50,000 quan)", balance, 50000, "Quan", balance >= 50000),
            new("ms_fleet", "Quy mô hạm đội tiêu cục (Cấp 2 trở lên)", "Caravan fleet tier (Tier 2+)", fleetTier, 2, "Cấp", fleetTier >= 2),
            new("ms_com", "Thương tài kinh thương cái thế (Com ≥ 70)", "Commerce attribute (Com ≥ 70)", comAttr, 70, "Điểm", comAttr >= 70),
            new("ms_credibility", "Uy tín thương giới thiên hạ (≥ 80)", "Commercial credibility (≥ 80)", credibility, 80, "Điểm", credibility >= 80)
        };

        var percent = CalculatePercent(reqs);
        var isFulfilled = reqs.All(r => r.IsMet);

        return new AmbitionBranch(
            AmbitionCategory.MerchantSovereign,
            "Phú Khả Địch Quốc",
            "Merchant Sovereign",
            "Thao túng huyết mạch kinh tế cửu châu, tích lũy kim ngân tài phú vô tận",
            "💰",
            percent,
            isFulfilled,
            reqs,
            "Đệ Nhất Phú Hào · Thiên Hạ Tài Thần",
            "Từ hai bàn tay trắng và chút vốn liếng ban đầu, bạn đã kiến thiết mạng lưới thương nhân, tiêu cục và thương điếm phủ khắp mười ba châu bốn biển. Vàng bạc châu báu, lụa là gấm vóc trong kho lẫm của bạn còn dồi dào hơn cả quốc khố chư hầu. Bất kỳ chư hầu nào muốn động binh hay xây thành lập ấp đều phải kính cẩn cúi mình trước quyền lực kinh tế tối thượng của bạn.");
    }

    private AmbitionBranch EvaluateDynastyPatriarch()
    {
        var hasLineage = _capabilities.TryGet<ILineageHouse>(CapabilityIds.LineageHouse, out var lineage) && lineage is not null;
        var hasEstate = _capabilities.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) && estate is not null;

        var isMarried = hasLineage && lineage!.IsMarried;
        var childCount = hasLineage ? lineage!.ChildCount : 0;
        var farmsCount = hasEstate ? estate!.Plots.Count(p => p.Rented) + estate.Facilities.Count : 0;
        var clanPrestige = hasLineage ? lineage!.ClanPrestige : 0;

        var reqs = new List<AmbitionRequirement>
        {
            new("dp_married", "Lập gia đình kết tóc se duyên", "Wed a spouse and establish household", isMarried ? 1 : 0, 1, "Gia thất", isMarried),
            new("dp_children", "Khai chi tán diệp sinh con kế nghiệp (≥ 1)", "Lineage heirs born (≥ 1)", childCount, 1, "Hậu duệ", childCount >= 1),
            new("dp_farms", "Điền trang cơ nghiệp sở hữu (≥ 2 điền trang)", "Cultivation estates & facilities (≥ 2)", farmsCount, 2, "Trang trại", farmsCount >= 2),
            new("dp_prestige", "Danh vọng tông môn thế gia (≥ 100)", "Clan prestige and heritage (≥ 100)", clanPrestige, 100, "Điểm", clanPrestige >= 100)
        };

        var percent = CalculatePercent(reqs);
        var isFulfilled = reqs.All(r => r.IsMet);

        return new AmbitionBranch(
            AmbitionCategory.DynastyPatriarch,
            "Vọng Tộc Thiên Cổ",
            "Dynasty Patriarch",
            "Khai chi tán diệp, điền trang bạt ngàn, gia phong đời đời truyền thừa hưng thịnh",
            "🏡",
            percent,
            isFulfilled,
            reqs,
            "Vọng Tộc Đệ Nhất · Thế Gia Trăm Đời",
            "Bạn đã gây dựng một gia tộc hùng mạnh với gia trang rộng lớn ngàn mẫu, gia đạo hòa thuận, con cháu đông đúc hiếu thảo và tài đức vẹn toàn. Qua nhiều thế hệ, con cháu dòng họ bạn liên tục đỗ đạt, xuất tướng nhập tướng, trở thành một trong những danh gia vọng tộc có sức ảnh hưởng sâu rộng bậc nhất lịch sử.");
    }

    private AmbitionBranch EvaluateGrandMartialMaster()
    {
        var hasAttrs = _capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null;
        var hasMilitary = _capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military is not null;
        var hasRep = _capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null;

        var warAttr = hasAttrs ? attrs!.Attributes.War : 0;
        var clearedStrongholds = hasMilitary ? military!.AllStrongholds().Sum(s => s.ClearedCount) : 0;
        var fame = hasRep ? rep!.Standing.Fame : 0;
        var health = hasAttrs ? attrs!.Vitals.Health : 0;

        var reqs = new List<AmbitionRequirement>
        {
            new("gm_war", "Vũ dũng vô địch thiên hạ (War ≥ 85)", "Martial prowess attribute (War ≥ 85)", warAttr, 85, "Điểm", warAttr >= 85),
            new("gm_strongholds", "Bình định sào huyệt sơn tặc (≥ 2 lần dẹp loạn)", "Bandit strongholds suppressed (≥ 2)", clearedStrongholds, 2, "Trại", clearedStrongholds >= 2),
            new("gm_fame", "Chiến danh lừng lẫy cửu châu (≥ 250 Uy Danh)", "Martial renown (≥ 250 Fame)", fame, 250, "Điểm", fame >= 250),
            new("gm_health", "Khí huyết dồi dào tráng kiện (Health ≥ 90)", "Physical vitality (Health ≥ 90)", health, 90, "Điểm", health >= 90)
        };

        var percent = CalculatePercent(reqs);
        var isFulfilled = reqs.All(r => r.IsMet);

        return new AmbitionBranch(
            AmbitionCategory.GrandMartialMaster,
            "Vô Song Chiến Thần",
            "Grand Martial Master",
            "Võ nghệ vô song, dẹp yên giặc cỏ sơn trại, uy chấn võ đài thiên hạ",
            "⚔️",
            percent,
            isFulfilled,
            reqs,
            "Võ Thánh Tái Thế · Thiên Hạ Vô Song",
            "Đao kiếm trong tay bạn đã trở thành huyền thoại. Khắp mười ba châu, sơn tặc thổ phỉ nghe danh phong đã tự động tan rã, võ tướng các phương bước lên võ đài đều phải tâm phục khẩu phục. Dân gian truyền tụng bạn như một vị Chiến Thần bảo quốc an dân, đền miếu phụng thờ khói hương nghi ngút ngàn thu.");
    }

    private AmbitionBranch EvaluateRenownedSage()
    {
        var hasAttrs = _capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null;
        var hasSocial = _capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null;
        var hasDestiny = _capabilities.TryGet<IPlayerDestiny>(CapabilityIds.PlayerDestiny, out var destiny) && destiny is not null;
        var hasRep = _capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null;

        var intAttr = hasAttrs ? attrs!.Attributes.Int : 0;
        var virtue = hasRep ? rep!.Standing.Virtue : 0;
        var sageStat = Math.Max(intAttr, virtue);

        var closeFriends = hasSocial ? social!.AllRelationships().Count(r => r.Affinity >= 60) : 0;
        var learnedNodes = hasDestiny ? destiny!.State.LearnedNodeIds.Count : 0;
        var fame = hasRep ? rep!.Standing.Fame : 0;

        var reqs = new List<AmbitionRequirement>
        {
            new("rs_stat", "Trí tuệ hoặc Đức độ bậc đại sư (Int ≥ 80 hoặc Đức ≥ 60)", "Intellect or Virtue (Int ≥ 80 or Virtue ≥ 60)", sageStat, 80, "Điểm", intAttr >= 80 || virtue >= 60),
            new("rs_friends", "Mối thâm giao tri kỷ khắp thiên hạ (≥ 3 người)", "Close confidants with affinity ≥ 60 (≥ 3)", closeFriends, 3, "Danh sĩ", closeFriends >= 3),
            new("rs_destiny", "Lĩnh hội Bách Gia Thiên Mệnh (≥ 4 thiên phú)", "Learned destiny talents (≥ 4)", learnedNodes, 4, "Bí pháp", learnedNodes >= 4),
            new("rs_fame", "Thanh danh sĩ phu ngưỡng vọng (≥ 200 Uy Danh)", "Scholarly renown (≥ 200 Fame)", fame, 200, "Điểm", fame >= 200)
        };

        var percent = CalculatePercent(reqs);
        var isFulfilled = reqs.All(r => r.IsMet);

        return new AmbitionBranch(
            AmbitionCategory.RenownedSage,
            "Thiên Hạ Danh Sĩ",
            "Renowned Sage",
            "Đức độ quảng đại, tri kỷ khắp bốn bể, thông hiểu huyền cơ Bách Gia",
            "📜",
            percent,
            isFulfilled,
            reqs,
            "Vạn Thế Sư Biểu · Bách Gia Tông Sư",
            "Đức hạnh và trí tuệ uyên thâm của bạn lan tỏa khắp Trung Nguyên. Từ thường dân cho đến bậc vương giả đều tìm đến bạn để cầu lời khuyên trị quốc an dân. Tác phẩm và tư tưởng của bạn trở thành kinh điển cho hậu thế noi theo, được tôn xưng như bậc Thánh hiền tái sinh.");
    }

    private static int CalculatePercent(IReadOnlyList<AmbitionRequirement> reqs)
    {
        if (reqs.Count == 0) return 0;
        if (reqs.All(r => r.IsMet)) return 100;

        double sumRatio = 0.0;
        foreach (var req in reqs)
        {
            if (req.IsMet)
            {
                sumRatio += 1.0;
            }
            else
            {
                var ratio = req.TargetValue <= 0 ? 0 : Math.Clamp((double)req.CurrentValue / req.TargetValue, 0.0, 1.0);
                sumRatio += ratio;
            }
        }

        return (int)Math.Clamp(Math.Round(sumRatio / reqs.Count * 100.0), 0, 99);
    }
}
