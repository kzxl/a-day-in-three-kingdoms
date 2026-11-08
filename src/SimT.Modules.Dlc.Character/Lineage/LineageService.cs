using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

public sealed record FamilyMember(long Id, string Name, string Role, int DayIndex, bool IsHeir, long? NpcId = null);

internal sealed record ChildTutorInfo(string Name, string Mentor, string Focus, int Progress, int Bonus);

/// <summary>
/// Household and succession. Marriage and childbirth are paid events, upkeep is a real monthly
/// cashflow, and exactly one heir may be designated — the database enforces the last one.
/// </summary>
internal sealed class LineageService : ILineageHouse
{
    public const long DowryCost = 300;
    public const long ChildCost = 50;
    public const long SpouseUpkeep = 40;
    public const long ChildUpkeep = 20;

    internal static readonly string[] SpouseNames =
        ["Vương Thị", "Lưu Thị", "Tôn Thị", "Mã Thị", "Hà Thị"];

    private static readonly string[] ChildNames =
        ["An", "Bình", "Chi", "Dũng", "Hạnh", "Khang", "Linh", "Minh", "Nguyên", "Phúc"];

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IRng _rng;
    private readonly IIdGenerator _ids;
    private readonly IActionTime _actionTime;
    private readonly ILog _log;
    private readonly INpcRegistry? _registry;
    private readonly ISocialNetwork? _social;

    public LineageService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IRng rng,
        IIdGenerator ids,
        IActionTime actionTime,
        ILog log,
        INpcRegistry? registry = null,
        ISocialNetwork? social = null)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _rng = rng;
        _ids = ids;
        _actionTime = actionTime;
        _log = log;
        _registry = registry;
        _social = social;
    }

    public bool IsMarried => _db.ScalarLong("SELECT COUNT(*) FROM ln_spouse") > 0;

    public string? SpouseName =>
        _db.ScalarString("SELECT name_vi FROM ln_spouse ORDER BY id LIMIT 1");

    public long? SpouseNpcId
    {
        get
        {
            var res = _db.Query("SELECT npc_id FROM ln_spouse ORDER BY id LIMIT 1", r => r.IsDbNull(0) ? (long?)null : r.GetInt64(0));
            return res.Count > 0 ? res[0] : null;
        }
    }

    public int ChildCount => (int)_db.ScalarLong("SELECT COUNT(*) FROM ln_child");

    public string? HeirName =>
        _db.ScalarString("SELECT name_vi FROM ln_child WHERE is_heir = 1 LIMIT 1");

    public long MonthlyUpkeep => IsMarried ? SpouseUpkeep + (ChildCount * ChildUpkeep) : 0;

    public int ClanPrestige => (int)_db.ScalarLong("SELECT COALESCE((SELECT clan_prestige FROM ln_shrine WHERE id = 1), 10)");

    public int ShrineLevel => (int)_db.ScalarLong("SELECT COALESCE((SELECT level FROM ln_shrine WHERE id = 1), 1)");

    public IReadOnlyList<ChildDef> Children =>
        _db.Query(
            """
            SELECT c.id, c.name_vi, c.born_day_index, c.is_heir, t.mentor_name, t.focus_stat, COALESCE(t.stat_bonus, 0)
            FROM ln_child c
            LEFT JOIN ln_tutoring t ON t.child_id = c.id
            ORDER BY c.born_day_index, c.id
            """,
            row =>
            {
                var bornDay = row.GetInt32(2);
                var birthDate = GameDate.FromDayIndex(bornDay);
                var age = Math.Max(0, _clock.Today.Year - birthDate.Year);
                var isAdult = age >= 16;
                return new ChildDef(
                    row.GetInt64(0),
                    row.GetString(1),
                    bornDay,
                    age,
                    isAdult,
                    row.GetInt64(3) != 0,
                    row.IsDbNull(4) ? null : row.GetString(4),
                    row.IsDbNull(5) ? null : row.GetString(5),
                    row.GetInt32(6));
            });

    public IReadOnlyList<FamilyMember> Members()
    {
        var members = new List<FamilyMember>();

        var spouse = _db.QueryOne(
            "SELECT id, name_vi, married_day_index, npc_id FROM ln_spouse ORDER BY id LIMIT 1",
            row => new FamilyMember(row.GetInt64(0), row.GetString(1), "Chính thất", row.GetInt32(2), false, row.IsDbNull(3) ? null : row.GetInt64(3)));

        if (spouse is not null)
        {
            members.Add(spouse);
        }

        members.AddRange(_db.Query(
            "SELECT id, name_vi, born_day_index, is_heir FROM ln_child ORDER BY born_day_index, id",
            row => new FamilyMember(
                row.GetInt64(0),
                row.GetString(1),
                "Tử tức",
                row.GetInt32(2),
                row.GetInt64(3) != 0)));

        return members;
    }

    public LineageResult Marry() => Marry(null);

    public LineageResult Marry(long? npcId)
    {
        if (IsMarried)
        {
            return LineageResult.Failure("Đã có chính thất.");
        }

        string name;
        long dowry = DowryCost;
        NpcDef? targetNpc = null;

        if (npcId.HasValue)
        {
            targetNpc = _registry?.Find(npcId.Value);
            if (targetNpc is null)
            {
                return LineageResult.Failure("Không tìm thấy nhân sĩ này.");
            }

            name = targetNpc.NameVi;

            var hasSpouse = _db.ScalarLong(
                "SELECT COUNT(1) FROM kz_npc_bond WHERE (npc_a = @id OR npc_b = @id) AND kind = 'spouse'",
                DbArgs.One("@id", npcId.Value)) > 0;
            if (hasSpouse)
            {
                return LineageResult.Failure($"{name} đã có hôn phối.");
            }

            if (_social is not null)
            {
                var affinity = _social.GetRelationship(npcId.Value).Affinity;
                if (affinity < 80)
                {
                    return LineageResult.Failure($"Hảo cảm với {name} chưa đủ sâu đậm (hiện có {affinity}/80) để nên duyên cầm sắt.");
                }
            }

            var rank = SocialService.EvaluateNpcRank(targetNpc);
            dowry = rank switch
            {
                NpcRank.S => 1000,
                NpcRank.A => 600,
                NpcRank.B => 450,
                _ => 300
            };
        }
        else
        {
            name = SpouseNames[_rng.Next(0, SpouseNames.Length)];
        }

        if (_wallet.Liquid < dowry)
        {
            return LineageResult.Failure($"Cần {dowry} Quan sính lễ, chỉ có {_wallet.Liquid}.");
        }

        var day = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.LineageMarry);
            _wallet.Charge(dowry, JournalCodes.Dowry, $"Sính lễ cưới {name}");
            _db.Execute(
                "INSERT INTO ln_spouse(id, name_vi, dowry_paid, married_day_index, npc_id) VALUES(@id, @name, @dowry, @day, @npcId)",
                new DbArgs()
                    .Add("@id", _ids.Next("lineage.spouse"))
                    .Add("@name", name)
                    .Add("@dowry", dowry)
                    .Add("@day", day)
                    .Add("@npcId", npcId.HasValue ? (object)npcId.Value : DBNull.Value));


        });

        _log.Info($"married {name} for {dowry} (npc_id={npcId})");
        return LineageResult.Success($"Thành hôn với {name}, sính lễ {dowry} Quan.");
    }

    public IReadOnlyList<(NpcDef Npc, int Affinity, long Dowry)> EligibleNpcSuitors()
    {
        if (_registry is null || _social is null || IsMarried)
        {
            return Array.Empty<(NpcDef, int, long)>();
        }

        var highAffinityNpcs = _db.Query(
            "SELECT npc_id, affinity FROM kz_relationship WHERE affinity >= 80",
            r => (NpcId: r.GetInt64(0), Affinity: r.GetInt32(1)));

        if (highAffinityNpcs.Count == 0)
        {
            return Array.Empty<(NpcDef, int, long)>();
        }

        var suitors = new List<(NpcDef Npc, int Affinity, long Dowry)>();
        foreach (var (npcId, affinity) in highAffinityNpcs)
        {
            var npc = _registry.Find(npcId);
            if (npc is null) continue;

            var hasSpouse = _db.ScalarLong(
                "SELECT COUNT(1) FROM kz_npc_bond WHERE (npc_a = @id OR npc_b = @id) AND kind = 'spouse'",
                DbArgs.One("@id", npc.Id)) > 0;
            if (!hasSpouse)
            {
                var rank = SocialService.EvaluateNpcRank(npc);
                long dowry = rank switch
                {
                    NpcRank.S => 1000,
                    NpcRank.A => 600,
                    NpcRank.B => 450,
                    _ => 300
                };
                suitors.Add((npc, affinity, dowry));
            }
        }
        return suitors.OrderByDescending(s => s.Affinity).ThenBy(s => s.Npc.NameVi).ToList();
    }

    public LineageResult Beget()
    {
        if (!IsMarried)
        {
            return LineageResult.Failure("Chưa có chính thất thì chưa tính chuyện con cái.");
        }

        if (_wallet.Liquid < ChildCost)
        {
            return LineageResult.Failure($"Cần {ChildCost} Quan lo sinh nở, chỉ có {_wallet.Liquid}.");
        }

        var name = ChildNames[_rng.Next(0, ChildNames.Length)];
        var day = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _wallet.Charge(ChildCost, JournalCodes.FamilyUpkeep, $"Lo sinh nở cho {NameOfSpouse()}");
            _db.Execute(
                "INSERT INTO ln_child(id, name_vi, born_day_index, is_heir) VALUES(@id, @name, @day, 0)",
                new DbArgs().Add("@id", _ids.Next("lineage.child")).Add("@name", name).Add("@day", day));
        });

        _log.Info($"child {name} born on day {day}");
        return LineageResult.Success($"{name} chào đời, chi phí {ChildCost} Quan.");
    }

    public LineageResult DesignateHeir(long childId)
    {
        var exists = _db.ScalarLong("SELECT COUNT(*) FROM ln_child WHERE id=@id", DbArgs.One("@id", childId));
        if (exists == 0)
        {
            return LineageResult.Failure("Không có người con này trong gia phả.");
        }

        var name = _db.ScalarString("SELECT name_vi FROM ln_child WHERE id=@id", DbArgs.One("@id", childId));

        _db.Transaction(() =>
        {
            _db.Execute("UPDATE ln_child SET is_heir = 0 WHERE is_heir = 1");
            _db.Execute("UPDATE ln_child SET is_heir = 1 WHERE id=@id", DbArgs.One("@id", childId));
        });

        return LineageResult.Success($"{name} được lập làm người kế vị.");
    }

    /// <summary>Monthly household upkeep. Unaffordable months are logged, never silently free.</summary>
    public void ChargeMonthly(GameDate date)
    {
        var upkeep = MonthlyUpkeep;
        if (upkeep <= 0)
        {
            return;
        }

        if (_wallet.Liquid < upkeep)
        {
            _log.Warn($"Chưa trả được phí gia đình {upkeep} Quan ngày {date}.");
            return;
        }

        _wallet.Charge(
            upkeep,
            JournalCodes.FamilyUpkeep,
            $"Phí gia đình tháng {date.Month:00}/{date.Year}",
            $"lineage.upkeep:{date.DayIndex}");
    }

    public LineageResult UpgradeShrine()
    {
        var currentLevel = ShrineLevel;
        if (currentLevel >= 5)
        {
            return LineageResult.Failure("Từ đường gia tộc đã được xây dựng nguy nga tráng lệ, đạt cấp bậc tối cao (Cấp 5).");
        }

        var cost = currentLevel * 500;
        if (_wallet.Liquid < cost)
        {
            return LineageResult.Failure($"Cần {cost} Quan tu bổ mở rộng từ đường, hiện chỉ có {_wallet.Liquid} Quan.");
        }

        var nextLevel = currentLevel + 1;
        _db.Transaction(() =>
        {
            _wallet.Charge(cost, JournalCodes.FamilyUpkeep, $"Tu bổ mở rộng từ đường gia tộc lên Cấp {nextLevel}");
            _db.Execute(
                """
                INSERT INTO ln_shrine (id, level, clan_prestige, last_offering_day)
                VALUES (1, @level, 60, 0)
                ON CONFLICT(id) DO UPDATE SET
                    level = @level,
                    clan_prestige = clan_prestige + 50
                """,
                DbArgs.One("@level", nextLevel));
        });

        return LineageResult.Success($"Đại công cáo thành! Từ đường gia tộc được mở rộng lên Cấp {nextLevel}, gia uy tăng thêm 50 điểm!");
    }

    public LineageResult HoldOffering()
    {
        const int offeringCost = 80;
        if (_wallet.Liquid < offeringCost)
        {
            return LineageResult.Failure($"Cần {offeringCost} Quan sắm sửa hương đăng hoa quả lễ phẩm tế tổ, chỉ có {_wallet.Liquid} Quan.");
        }

        var lastOfferingDay = (int)_db.ScalarLong("SELECT COALESCE((SELECT last_offering_day FROM ln_shrine WHERE id = 1), 0)");
        var currentDay = _clock.Today.DayIndex;
        if (lastOfferingDay > 0 && currentDay - lastOfferingDay < 25)
        {
            return LineageResult.Failure($"Mới tế tổ cách đây {currentDay - lastOfferingDay} ngày. Nghi thức tế lễ gia tộc cần ít nhất 25 ngày cách quãng.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(offeringCost, JournalCodes.FamilyUpkeep, "Hương đăng trà quả đại lễ tế tổ từ đường");
            _clock.Advance(1);
            _db.Execute(
                """
                UPDATE ln_shrine
                SET clan_prestige = clan_prestige + 15,
                    last_offering_day = @day
                WHERE id = 1
                """,
                DbArgs.One("@day", _clock.Today.DayIndex));
        });

        return LineageResult.Success("Nghi lễ tế tổ trang nghiêm thành kính kết thúc tốt đẹp! Toàn gia đoàn tụ, gia uy gia tộc tăng thêm 15 điểm.");
    }

    public LineageResult AssignTutor(long childId, string mentorName, string focusStat)
    {
        var exists = _db.ScalarLong("SELECT COUNT(*) FROM ln_child WHERE id = @id", DbArgs.One("@id", childId));
        if (exists == 0)
        {
            return LineageResult.Failure("Không tìm thấy con nối dõi này trong gia phả.");
        }

        if (string.IsNullOrWhiteSpace(mentorName))
        {
            mentorName = "Danh Nho Ẩn Cư";
        }

        if (focusStat is not ("WAR" or "INT" or "POL" or "CHA"))
        {
            focusStat = "INT";
        }

        _db.Execute(
            """
            INSERT INTO ln_tutoring (child_id, mentor_name, focus_stat, progress, stat_bonus)
            VALUES (@child, @mentor, @focus, 0, 0)
            ON CONFLICT(child_id) DO UPDATE SET
                mentor_name = excluded.mentor_name,
                focus_stat = excluded.focus_stat
            """,
            new DbArgs().Add("@child", childId).Add("@mentor", mentorName).Add("@focus", focusStat));

        return LineageResult.Success($"Đã mời {mentorName} phụ trách giáo dưỡng ({focusStat}) cho con trẻ.");
    }

    public LineageResult TeachChild(long childId)
    {
        var child = _db.QueryOne(
            "SELECT c.name_vi, t.mentor_name, t.focus_stat, t.progress, t.stat_bonus FROM ln_child c JOIN ln_tutoring t ON t.child_id = c.id WHERE c.id = @id",
            row => new ChildTutorInfo(row.GetString(0), row.GetString(1), row.GetString(2), row.GetInt32(3), row.GetInt32(4)),
            DbArgs.One("@id", childId));

        if (child is null)
        {
            return LineageResult.Failure("Chưa cắt cử danh sư phụ đạo cho người con này.");
        }

        const int lessonFee = 25;
        if (_wallet.Liquid < lessonFee)
        {
            return LineageResult.Failure($"Cần {lessonFee} Quan học phí thù lao cho danh sư, chỉ có {_wallet.Liquid} Quan.");
        }

        var newProgress = child.Progress + 1;
        var newBonus = Math.Min(10, newProgress / 3);

        _db.Transaction(() =>
        {
            _wallet.Charge(lessonFee, JournalCodes.FamilyUpkeep, $"Học phí một khóa huấn thị cho {child.Name}");
            _clock.Advance(1);
            _db.Execute(
                """
                UPDATE ln_tutoring
                SET progress = @prog, stat_bonus = @bonus
                WHERE child_id = @id
                """,
                new DbArgs().Add("@prog", newProgress).Add("@bonus", newBonus).Add("@id", childId));
        });

        return LineageResult.Success($"Hài nhi {child.Name} chuyên cần học tập dưới sự chỉ bảo của {child.Mentor} ({child.Focus} +{newBonus})!");
    }

    private string NameOfSpouse() => SpouseName ?? "chính thất";
}

internal readonly record struct OldLineageResult(bool Ok, string Message)
{
    public static OldLineageResult Success(string message) => new(true, message);

    public static OldLineageResult Failure(string message) => new(false, message);
}
