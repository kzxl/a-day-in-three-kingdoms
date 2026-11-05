using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Military;

/// <summary>
/// Tactical combat simulation engine for Outlaw Stronghold Expeditions (Tiễu Phạt Sơn Trại).
/// Simulates multi-round clashes integrating Troop Counts (Binh Lực), Commander Attributes,
/// Troop Counter-Affinities, and Martial Skills.
/// </summary>
public static class BanditExpeditionEngine
{
    private static readonly (string Name, string Title)[] KhanVangChiefs =
    [
        ("Trương Mạn Thành", "Khăn Vàng Thần Thượng Sứ"),
        ("Ba Tài", "Dĩnh Xuyên Cừ Soái"),
        ("Quản Hợi", "Thanh Châu Đại Đầu Mục"),
        ("Bành Thoát", "Nhữ Nam Tướng Quân"),
        ("Trương Bảo", "Địa Công Tướng Quân")
    ];

    private static readonly (string Name, string Title)[] HacSonChiefs =
    [
        ("Trương Yến", "Hắc Sơn Phi Yến"),
        ("Vu Độc", "Thái Hành Kiếp Soái"),
        ("Bạch Tước", "Sơn Doanh Đầu Lĩnh"),
        ("Lôi Công", "Phục Binh Tướng"),
        ("Tôn Trọng", "Trại Chủ Mãnh Hổ")
    ];

    private static readonly (string Name, string Title)[] BachBaChiefs =
    [
        ("Quách Đại", "Bạch Ba Cừ Soái"),
        ("Lý Nhạc", "Thủy Doanh Bá Vương"),
        ("Hàn Xiêm", "Trảm Mã Tướng"),
        ("Hồ Tài", "Bạch Ba Dũng Soái"),
        ("Dương Phụng", "Nghĩa Khấu Thống Lĩnh")
    ];

    private static readonly (string Name, string Title)[] ThoPhiChiefs =
    [
        ("Hắc Hổ", "Đại Trại Chủ Hắc Phong"),
        ("Độc Long", "Thảo Khấu Lãnh Tụ"),
        ("Thiết Đầu Xà", "Hiểm Địa Kiếp Tặc"),
        ("Triệu Báo", "Sơn Lâm Độc Hành Khấu"),
        ("Lục Lâm Hào Kiệt", "Lộ Phỉ Cừ Khôi")
    ];

    /// <summary>
    /// Deterministically resolves the bandit warlord / chief commanding a stronghold.
    /// </summary>
    public static BanditChiefDef ResolveChief(StrongholdDef stronghold)
    {
        var branch = stronghold.Branch;
        var danger = stronghold.DangerLevel;
        var faction = stronghold.BanditFaction;

        var pool = faction switch
        {
            "khan_vang" => KhanVangChiefs,
            "hac_son" => HacSonChiefs,
            "bach_ba" => BachBaChiefs,
            _ => ThoPhiChiefs
        };

        var index = (int)(Math.Abs(stronghold.Id) % pool.Length);
        var (name, title) = pool[index];

        var avatarChar = faction switch
        {
            "khan_vang" => "黃",
            "hac_son" => "黑",
            "bach_ba" => "白",
            _ => "賊"
        };

        var factionVi = faction switch
        {
            "khan_vang" => "Khăn Vàng Tàn Quân",
            "hac_son" => "Hắc Sơn Khởi Nghĩa Quân",
            "bach_ba" => "Bạch Ba Man Tặc",
            _ => "Lộ Phỉ Thảo Khấu"
        };

        var terrainName = branch switch
        {
            TroopBranch.Archers => "Thủy Doanh Ven Sông & Đầm Lầy Sương Mù",
            TroopBranch.Cavalry => "Bình Nguyên Hiểm Địa & Mã Đạo Ngoại Thành",
            _ => "Sơn Cốc Dốc Đứng & Rừng Rậm Mai Phục"
        };

        var war = Math.Clamp(42 + (danger * 5), 45, 95);
        var ldr = Math.Clamp(38 + (danger * 5), 40, 90);
        var intl = Math.Clamp(30 + (danger * 4), 30, 80);
        var troops = 650 + (danger * 350);
        var maxHp = 120 + (danger * 16);

        var description = $"Thủ lĩnh khét tiếng chiếm đóng {stronghold.NameVi}, chỉ huy {troops:N0} thảo khấu thiện chiến dưới trướng. Địa thế {terrainName} vô cùng hiểm trở.";

        return new BanditChiefDef(
            name,
            title,
            avatarChar,
            factionVi,
            branch,
            war,
            ldr,
            intl,
            troops,
            maxHp,
            danger,
            terrainName,
            description);
    }

    /// <summary>
    /// Runs a full multi-round tactical expedition simulation against an outlaw stronghold.
    /// </summary>
    public static BanditExpeditionResult SimulateExpedition(
        IPlayerAttributes playerAttrs,
        StrongholdDef stronghold,
        TroopBranch playerBranch,
        string itemRewardName,
        int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var chief = ResolveChief(stronghold);

        var pAttr = playerAttrs.Attributes;

        // Player initial forces
        var playerInitialTroops = Math.Max(600, (pAttr.Ldr * 22) + (pAttr.Con * 10));
        var playerTroops = playerInitialTroops;
        var playerMaxHp = 100 + (pAttr.Con / 2);
        var playerHp = playerMaxHp;

        // Bandit initial forces
        var banditInitialTroops = chief.TroopCount;
        var banditTroops = banditInitialTroops;
        var banditMaxHp = chief.MaxHp;
        var banditHp = banditMaxHp;

        var roundLogs = new List<BanditBattleRoundLog>();

        // Branch counter multipliers
        var isPlayerAdvantage = TroopBranchHelper.Counters(playerBranch, chief.Branch);
        var isBanditAdvantage = TroopBranchHelper.Counters(chief.Branch, playerBranch);

        var playerDmgMult = isPlayerAdvantage ? 1.25 : (isBanditAdvantage ? 0.80 : 1.0);
        var banditDmgMult = isBanditAdvantage ? 1.25 : (isPlayerAdvantage ? 0.80 : 1.0);

        var won = false;
        var round = 1;
        var maxRounds = 5;

        while (round <= maxRounds)
        {
            var isLastRound = round == maxRounds;

            // Base round damage
            var playerAtkPower = pAttr.War + (pAttr.Ldr / 2) + rng.Next(10, 25);
            var banditAtkPower = chief.War + (chief.Ldr / 2) + rng.Next(8, 20);

            // Skill triggers
            string? skillTriggered = null;
            var skillDmgBonus = 1.0;

            if (rng.NextDouble() < 0.40)
            {
                var skillRoll = rng.Next(4);
                switch (skillRoll)
                {
                    case 0:
                        skillTriggered = "Long Đảm Thấu Trận! Tướng quân tả xung hữu đột, xé toạc chiến tuyến giặc.";
                        skillDmgBonus = 1.35;
                        break;
                    case 1:
                        skillTriggered = "Thiết Bích Thuẫn Trận! Binh sĩ dựng lá chắn vững như bàn thạch, cản phá xung lực.";
                        banditDmgMult *= 0.65;
                        break;
                    case 2:
                        skillTriggered = "Võ Thánh Thần Uy! Một đao trảm vỡ cờ soái, uy chấn tứ phương.";
                        skillDmgBonus = 1.45;
                        break;
                    default:
                        skillTriggered = "Phá Quân Bách Kích! Toàn quân đồng loạt xung phong công phá trại lũy.";
                        skillDmgBonus = 1.30;
                        break;
                }
            }

            // Calculate troop casualties
            var pDamageDealt = (int)((playerAtkPower * 2.8) * playerDmgMult * skillDmgBonus);
            var bDamageDealt = (int)((banditAtkPower * 2.2) * banditDmgMult);

            // Adjust by remaining troop percentage
            pDamageDealt = (int)(pDamageDealt * (0.5 + 0.5 * ((double)playerTroops / playerInitialTroops)));
            bDamageDealt = (int)(bDamageDealt * (0.5 + 0.5 * ((double)banditTroops / banditInitialTroops)));

            banditTroops = Math.Max(0, banditTroops - pDamageDealt);
            playerTroops = Math.Max(0, playerTroops - bDamageDealt);

            var chiefDmg = (int)((pAttr.War * 0.4) * skillDmgBonus);
            var playerWound = (int)(chief.War * 0.25);

            banditHp = Math.Max(0, banditHp - chiefDmg);
            playerHp = Math.Max(10, playerHp - playerWound);

            var isDecisive = banditTroops <= 0 || banditHp <= 0 || playerTroops <= 0;

            var phaseName = round switch
            {
                1 => "Hiệp 1 (Tiền Tiêu Xung Kích)",
                2 => "Hiệp 2 (Trung Quân Áp Đảo)",
                3 => "Hiệp 3 (Công Phá Trại Lũy)",
                4 => "Hiệp 4 (Quyết Chiến Địch Doanh)",
                _ => $"Hiệp {round} (Tổng Lực Đột Phá)"
            };

            var narrative = $"{phaseName}: {TroopBranchHelper.NameVi(playerBranch)} ta giáp chiến cùng {TroopBranchHelper.NameVi(chief.Branch)} của {chief.NameVi}. Ta diệt {pDamageDealt:N0} giặc, tổn hao {bDamageDealt:N0} binh sĩ.";
            if (!string.IsNullOrEmpty(skillTriggered))
            {
                narrative = $"⚡ {skillTriggered}\n{narrative}";
            }

            roundLogs.Add(new BanditBattleRoundLog(
                round,
                pDamageDealt,
                bDamageDealt,
                playerTroops,
                banditTroops,
                playerHp,
                banditHp,
                skillTriggered,
                narrative,
                isDecisive));

            if (isDecisive)
            {
                break;
            }

            round++;
        }

        // Final victory resolution
        won = banditTroops <= 0 || banditHp <= 0 || (playerTroops > banditTroops && playerTroops > playerInitialTroops * 0.25);

        if (won)
        {
            banditTroops = 0;
            banditHp = 0;
        }

        var fameGained = won ? stronghold.DangerLevel * 15 : 0;
        var virtueGained = won ? 5 : 0;
        var lootGold = won ? stronghold.LootGold : 0;

        var summary = won
            ? $"Đại Thắng! Tiễu trừ {stronghold.NameVi}, đả bại Trại Chủ {chief.NameVi}. Thu hồi {lootGold} Quan và 2 {itemRewardName}, bảo an bờ cõi."
            : $"Thất bại! Thế lực {stronghold.NameVi} quá đông đảo phòng ngự kiên cố (Cấp {stronghold.DangerLevel}). Quân ta lực bất tòng tâm đành tạm lui binh.";

        return new BanditExpeditionResult(
            won,
            roundLogs.Count,
            lootGold,
            stronghold.ItemRewardCode,
            itemRewardName,
            fameGained,
            virtueGained,
            playerInitialTroops,
            playerTroops,
            banditInitialTroops,
            banditTroops,
            chief,
            stronghold,
            roundLogs,
            summary);
    }
}
