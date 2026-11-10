using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class SocialService
{
    public void OnMonthClosed(GameDate date)
    {
        ProcessRetainerStipends(date);
        ProcessMonthlyLetters(date);
    }

    private void ProcessRetainerStipends(GameDate date)
    {
        var retainers = GetRetainers();
        if (retainers.Count == 0)
        {
            return;
        }

        var formattedDate = GameDateCodec.Format(date);

        _db.Transaction(() =>
        {
            foreach (var ret in retainers)
            {
                var npc = _registry.Find(ret.NpcId);
                var name = npc?.NameVi ?? $"Gia thần #{ret.NpcId}";

                // Charge monthly stipend with idempotency key
                var key = $"stipend:{ret.Id}:{formattedDate}";
                try
                {
                    _wallet.Charge(ret.MonthlyStipend, "social.stipend", $"Bổng lộc gia thần {name}", key);
                    _db.Execute(
                        "UPDATE kz_retainer SET loyalty = MIN(100, loyalty + 1) WHERE id = @id",
                        DbArgs.One("@id", ret.Id));
                }
                catch (SimException)
                {
                    // If short on cash, loyalty drops!
                    _db.Execute(
                        "UPDATE kz_retainer SET loyalty = MAX(0, loyalty - 15) WHERE id = @id",
                        DbArgs.One("@id", ret.Id));
                    _log.Warn($"Failed to pay stipend for retainer {name} on {formattedDate}; loyalty reduced.");
                }

                ProcessRetainerAmbitionFriction(ret, name, formattedDate, date.DayIndex);
            }
        });
    }

    public void ProcessMonthlyLetters(GameDate date)
    {
        var hasLetterTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        if (!hasLetterTable)
        {
            return;
        }

        var formattedDate = GameDateCodec.Format(date);
        var tick = date.DayIndex;

        // Candidate NPCs: affinity >= 50, sworn brothers, or retainers
        var relationships = _db.Query(
            "SELECT npc_id, affinity, impression FROM kz_relationship WHERE affinity >= 50",
            r => (NpcId: r.GetInt64(0), Affinity: r.GetInt32(1), Impression: r.GetString(2)));

        var candidateMap = new Dictionary<long, (int Affinity, string Impression)>();
        foreach (var r in relationships)
        {
            candidateMap[r.NpcId] = (r.Affinity, r.Impression);
        }

        foreach (var sw in GetSwornBrothers())
        {
            if (!candidateMap.ContainsKey(sw.NpcId))
            {
                var rel = GetRelationship(sw.NpcId);
                candidateMap[sw.NpcId] = (Math.Max(rel.Affinity, 80), "Huynh đệ kim lan");
            }
        }

        foreach (var ret in GetRetainers())
        {
            if (!candidateMap.ContainsKey(ret.NpcId))
            {
                var rel = GetRelationship(ret.NpcId);
                candidateMap[ret.NpcId] = (Math.Max(rel.Affinity, 50), "Gia thần tâm phúc");
            }
        }

        if (candidateMap.Count == 0)
        {
            return;
        }

        var swornIds = GetSwornBrothers().Select(s => s.NpcId).ToHashSet();
        var retainerIds = GetRetainers().Select(r => r.NpcId).ToHashSet();

        _db.Transaction(() =>
        {
            foreach (var (npcId, (affinity, impression)) in candidateMap)
            {
                // Max 1 letter per NPC per month
                var alreadySent = _db.ScalarLong(
                    "SELECT COUNT(*) FROM kz_letter WHERE sender_npc_id = @npcId AND sent_date = @date",
                    new DbArgs().Add("@npcId", npcId).Add("@date", formattedDate)) > 0;
                if (alreadySent)
                {
                    continue;
                }

                var isSworn = swornIds.Contains(npcId);
                var isRetainer = retainerIds.Contains(npcId);

                // Chance calculation: sworn brothers stay in close touch, close friends write regularly
                var chance = isSworn ? 70 : (affinity >= 85 ? 50 : (affinity >= 70 ? 35 : 20));
                var rng = new SimpleRng((uint)(tick * 433 ^ (int)npcId));

                if (rng.Next(100) >= chance)
                {
                    continue;
                }

                var npc = _registry.Find(npcId);
                if (npc is null)
                {
                    continue;
                }

                var cit = _map.Find(npc.CitadelId);
                var citName = cit?.NameVi ?? "Phương xa";

                var (title, content, giftItemCode, giftCopper) = GenerateLetterContent(npc, citName, isSworn, isRetainer, affinity, rng);

                long? gItemId = null;
                string? gItemCode = null;
                string? gItemName = null;
                int gItemQty = 0;

                if (!string.IsNullOrEmpty(giftItemCode))
                {
                    var item = _items.FindByCode(giftItemCode);
                    if (item is not null)
                    {
                        gItemId = item.Id;
                        gItemCode = item.Code;
                        gItemName = item.NameVi;
                        gItemQty = 1;
                    }
                }

                _db.Execute(
                    "INSERT INTO kz_letter (sent_date, sender_npc_id, sender_name_vi, sender_citadel_id, title_vi, content_vi, " +
                    "gift_item_id, gift_item_code, gift_item_name_vi, gift_item_quantity, gift_copper, is_read, is_claimed, created_tick) " +
                    "VALUES (@date, @npcId, @name, @citId, @title, @content, @gId, @gCode, @gName, @gQty, @copper, 0, 0, @tick)",
                    new DbArgs()
                        .Add("@date", formattedDate)
                        .Add("@npcId", npc.Id)
                        .Add("@name", npc.NameVi)
                        .Add("@citId", npc.CitadelId)
                        .Add("@title", title)
                        .Add("@content", content)
                        .Add("@gId", gItemId)
                        .Add("@gCode", gItemCode)
                        .Add("@gName", gItemName)
                        .Add("@gQty", gItemQty)
                        .Add("@copper", giftCopper)
                        .Add("@tick", tick));

                // Bonus affinity (+2) for correspondence
                _db.Execute(
                    "UPDATE kz_relationship SET affinity = MIN(100, affinity + 2), last_interaction_date = @date WHERE npc_id = @id",
                    new DbArgs().Add("@date", formattedDate).Add("@id", npc.Id));

                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                    "VALUES (@npcId, @date, 'letter', 'positive', 2, @summary)",
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@date", formattedDate)
                        .Add("@summary", $"{npc.NameVi} gửi phong thư vấn an từ {citName}."));
            }
        });
    }

    private static (string Title, string Content, string? GiftItemCode, int GiftCopper) GenerateLetterContent(
        NpcDef npc,
        string citName,
        bool isSworn,
        bool isRetainer,
        int affinity,
        SimpleRng rng)
    {
        var a = npc.Attributes;

        if (isSworn)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "🌸 Tâm Thư Kim Lan: Vấn An Hiền Đệ / Huynh",
                    $"Bái biệt đã lâu, nơi chiến địa gió lạnh thấu xương, ta vẫn luôn nhớ tới chén rượu ăn thề dưới bóng đào viên thuở trước. " +
                    $"Dạo này lưu lại {citName}, quân mã tề chỉnh, việc lớn từng bước tiến hành. " +
                    $"Ta đặc biệt nhờ trạm dịch gửi tặng chút ngân lượng và rượu thơm hảo hạng, mong hiền đệ bảo trọng thân thể, sớm ngày tái ngộ cùng nâng chén!",
                    "ruou",
                    150);
            }

            return (
                "🤝 Đào Viên Huyết Thệ: Phong Vân Thiên Hạ",
                $"Thời cuộc thiên hạ biến ảo khôn lường, chư hầu khắp nơi rục rịch binh mã. " +
                $"Ta tọa trấn tại {citName}, trong lòng vẫn luôn khắc cốt ghi tâm chí lớn của huynh đệ chúng ta. " +
                $"Nay gửi chút quân tư tiếp tế, chúc hiền huynh vạn sự hanh thông, danh vang bốn bể!",
                null,
                200);
        }

        if (isRetainer)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "🛡️ Bẩm Báo Gia Thần: Bổn Phận & Vấn An Minh Chủ",
                    $"Thuộc hạ phụng mệnh minh chủ, ngày đêm không dám lơ là chức trách tại {citName}. " +
                    $"Gần đây võ nghệ tinh tiến, việc tuần phòng và thu chi đều thuận lợi, xin kính báo để minh chủ an lòng. " +
                    $"Thuộc hạ gom góp được chút ngân lượng dâng lên minh chủ làm lộ phí hành tẩu!",
                    null,
                    80);
            }

            return (
                "📜 Thư Bẩm Báo: Tình Hình Trị An & Dược Liệu Tiếp Tế",
                $"Thuộc hạ tại {citName} quan sát thấy khách buôn qua lại tấp nập, thương lộ bình yên. " +
                $"Thuộc hạ may mắn thu thập được vị thuốc bổ dưỡng khí huyết, xin sai người hỏa tốc chuyển gửi dâng lên minh chủ bồi bổ thân thể!",
                "thuoc",
                0);
        }

        // Archetype based on attributes
        if (a.War >= 75)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "⚔️ Luận Kiếm Chi Thư: Hào Khí Ngút Trời",
                    $"Từ dạo cùng các hạ đàm đạo võ học, ta vẫn luôn khắc ghi nhã ý. " +
                    $"Gần đây tại {citName} ta thường mài kiếm dưới trăng, ngẫm lại lời bàn năm xưa của các hạ thật thấu đáo. " +
                    $"Nay gửi tặng vò rượu nếp ủ kỹ, mong ngày tương phùng cùng cạn chén luận anh hùng!",
                    "ruou",
                    100);
            }

            return (
                "🏹 Phong Thư Quân Trại: Tiễu Phỉ An Dân",
                $"Ta vừa dẫn binh tuần phòng tiễu trừ thảo khấu quanh vùng {citName}, thu được một số dược liệu và quân nhu phòng thân. " +
                $"Nhớ tới bằng hữu bôn ba giang hồ gió bụi, xin gửi tặng để hộ thân khi hữu sự.",
                "thuoc",
                0);
        }

        if (a.Com >= 70)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "💰 Phi Thư Thương Gia: Hồi Báo Lợi Nhuận Buôn Bán",
                    $"Thương đoàn của ta vừa hoàn tất chuyến buôn đường dài về {citName}, buôn may bán đắt, thu lời không nhỏ. " +
                    $"Nhớ đến ân tình và sự tương trợ của các hạ thuở trước, ta trích phần hoa hồng này xin gửi biếu để tỏ chút lòng thành!",
                    null,
                    180);
            }

            return (
                "📦 Tơ Lụa & Danh Trà: Quà Tặng Tri Kỷ Thương Mại",
                $"Tại chợ phiên {citName} vừa nhập về một lô tơ lụa và trà mạn cực phẩm. " +
                $"Ta lập tức chọn lấy phẩm vị tinh xảo nhất gửi ngựa trạm chuyển giao đến tay các hạ, kính chúc buôn may bán đắt, tài lộc dồi dào!",
                "tra",
                50);
        }

        if (a.Int >= 75)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "📜 Thư Đàm Đạo Thời Cuộc: Luận Bàn Đại Thế Chư Hầu",
                    $"Phương bắc phong vân khởi sắc, thiên hạ biến loạn khó lường. " +
                    $"Ta tọa quan thiên văn và thời cuộc tại {citName}, thường nhớ tới những lời đàm luận sáng suốt của các hạ. " +
                    $"Nay gửi phong thư này kèm chút danh trà để các hạ thưởng thức lúc rảnh rỗi nghiền ngẫm việc lớn.",
                    "tra",
                    50);
            }

            return (
                "🌿 Trầm Tư Tu Dưỡng: Dược Liệu Dưỡng Sinh",
                $"Hành tẩu thiên hạ vất vả nhọc nhằn, cần phải bảo trọng khí huyết và tinh thần. " +
                $"Ta tình cờ có được vị danh dược quý ngàn năm, đặc biệt sai người mang tới chúc các hạ thân thể tráng kiện, tâm trí minh mẫn.",
                "sam",
                0);
        }

        return (
            "✉️ Thư Vấn An Bằng Hữu Phương Xa",
            $"Biệt ly mấy độ trăng tròn, ngụ tại {citName} vẫn luôn hướng về cố nhân. " +
            $"Gửi chút lộ phí phong bao, chúc các hạ hành trình bình an, sớm ngày hội ngộ đàm đạo bên bếp lửa hồng.",
            null,
            80);
    }

    public IReadOnlyList<LetterDef> GetLetters(bool unreadOnly = false, int limit = 30)
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        if (!hasTable)
        {
            return Array.Empty<LetterDef>();
        }

        var sql = "SELECT id, sent_date, sender_npc_id, sender_name_vi, sender_citadel_id, title_vi, content_vi, " +
                  "gift_item_id, gift_item_code, gift_item_name_vi, gift_item_quantity, gift_copper, is_read, is_claimed " +
                  "FROM kz_letter ";

        if (unreadOnly)
        {
            sql += "WHERE is_read = 0 ";
        }

        sql += "ORDER BY id DESC LIMIT " + Math.Clamp(limit, 1, 100);

        return _db.Query(sql, r => new LetterDef(
            r.GetInt64(0),
            GameDateCodec.Parse(r.GetString(1)),
            r.GetInt64(2),
            r.GetString(3),
            r.GetInt64(4),
            r.GetString(5),
            r.GetString(6),
            r.IsDbNull(7) ? (long?)null : r.GetInt64(7),
            r.GetStringOrNull(8),
            r.GetStringOrNull(9),
            r.GetInt32(10),
            r.GetInt32(11),
            r.GetInt32(12) != 0,
            r.GetInt32(13) != 0));
    }

    public LetterDef? GetLetter(long letterId)
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        if (!hasTable)
        {
            return null;
        }

        return _db.QueryOne(
            "SELECT id, sent_date, sender_npc_id, sender_name_vi, sender_citadel_id, title_vi, content_vi, " +
            "gift_item_id, gift_item_code, gift_item_name_vi, gift_item_quantity, gift_copper, is_read, is_claimed " +
            "FROM kz_letter WHERE id = @id",
            r => new LetterDef(
                r.GetInt64(0),
                GameDateCodec.Parse(r.GetString(1)),
                r.GetInt64(2),
                r.GetString(3),
                r.GetInt64(4),
                r.GetString(5),
                r.GetString(6),
                r.IsDbNull(7) ? (long?)null : r.GetInt64(7),
                r.GetStringOrNull(8),
                r.GetStringOrNull(9),
                r.GetInt32(10),
                r.GetInt32(11),
                r.GetInt32(12) != 0,
                r.GetInt32(13) != 0),
            DbArgs.One("@id", letterId));
    }

    public string ReadLetter(long letterId)
    {
        var letter = GetLetter(letterId);
        if (letter is null)
        {
            return "Không tìm thấy phong thư này.";
        }

        _db.Execute("UPDATE kz_letter SET is_read = 1 WHERE id = @id", DbArgs.One("@id", letterId));

        var giftText = letter.GiftCopper > 0 && letter.GiftItemQuantity > 0
            ? $"{letter.GiftCopper} Quan tiền & {letter.GiftItemQuantity} {letter.GiftItemNameVi}"
            : letter.GiftCopper > 0
                ? $"{letter.GiftCopper} Quan tiền"
                : letter.GiftItemQuantity > 0
                    ? $"{letter.GiftItemQuantity} {letter.GiftItemNameVi}"
                    : "Không có lễ vật đính kèm";

        var claimStatus = (letter.GiftCopper > 0 || letter.GiftItemQuantity > 0)
            ? (letter.IsClaimed ? "[Đã nhận quà]" : "[Chưa nhận quà - Nhấn nút 'Nhận Quà 🎁']")
            : "";

        var cit = _map.Find(letter.SenderCitadelId);
        var citName = cit?.NameVi ?? "Phương xa";

        return $"══════════════════════════════════════════════════\n" +
               $"📜 {letter.TitleVi}\n" +
               $"Người gửi: {letter.SenderNameVi} (Đang ở {citName})\n" +
               $"Ngày gửi: {GameDateCodec.Format(letter.SentDate)}\n" +
               $"──────────────────────────────────────────────────\n" +
               $"{letter.ContentVi}\n" +
               $"──────────────────────────────────────────────────\n" +
               $"🎁 Lễ vật: {giftText} {claimStatus}\n" +
               $"══════════════════════════════════════════════════";
    }

    public string ClaimLetterGift(long letterId)
    {
        var letter = GetLetter(letterId);
        if (letter is null)
        {
            return "Không tìm thấy phong thư này.";
        }

        if (letter.IsClaimed)
        {
            return "Lễ vật đính kèm trong thư này đã được nhận trước đó rồi.";
        }

        if (letter.GiftCopper <= 0 && letter.GiftItemQuantity <= 0)
        {
            return "Bức thư này không có lễ vật đính kèm.";
        }

        var details = new List<string>();

        if (letter.GiftCopper > 0)
        {
            _wallet.Pay(letter.GiftCopper, "social.letter_gift", $"Lễ vật đính kèm từ thư của {letter.SenderNameVi}");
            details.Add($"+{letter.GiftCopper} Quan tiền");
        }

        if (letter.GiftItemId.HasValue && letter.GiftItemQuantity > 0)
        {
            _bag.Add(_player.CurrentCityId, letter.GiftItemId.Value, letter.GiftItemQuantity);
            details.Add($"+{letter.GiftItemQuantity} {letter.GiftItemNameVi}");
        }

        _db.Execute("UPDATE kz_letter SET is_claimed = 1, is_read = 1 WHERE id = @id", DbArgs.One("@id", letterId));

        var currentCit = _map.Find(_player.CurrentCityId);
        return $"🎁 Đã nhận lễ vật từ {letter.SenderNameVi}: {string.Join(", ", details)} (đã chuyển vào hành trang tại {currentCit?.NameVi ?? "thành này"}).";
    }

    public int UnreadLetterCount()
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        return hasTable ? (int)_db.ScalarLong("SELECT COUNT(*) FROM kz_letter WHERE is_read = 0") : 0;
    }

    private static string CalculateImpression(int affinity) => affinity switch
    {
        >= 90 => "Sinh tử chi giao",
        >= 70 => "Thâm giao tri kỷ",
        >= 40 => "Hảo hữu thân thiết",
        >= 15 => "Quen biết tương đắc",
        >= -10 => "Sơ giao bằng hữu",
        >= -40 => "Lạnh nhạt xa cách",
        _ => "Tử thù đối địch"
    };

    public IReadOnlyList<SwornBrotherDef> GetSwornBrothers() =>
        _db.Query(
            "SELECT npc_id, oath_date, bond_title, assist_count FROM kz_sworn_brother ORDER BY rowid",
            row => new SwornBrotherDef(
                row.GetInt64(0),
                GameDateCodec.Parse(row.GetString(1)),
                row.GetString(2),
                row.GetInt32(3)));

    public string SwearBrotherhood(long npcId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CurrentCitadelId(_capabilities) != _player.CurrentCityId)
        {
            return $"{npc.NameVi} hiện đang ở thành khác, không thể cùng cắt máu ăn thề tại đây.";
        }

        var existingSworn = GetSwornBrothers();
        if (existingSworn.Any(s => s.NpcId == npcId))
        {
            return $"{npc.NameVi} đã là nghĩa huynh đệ kim lan của bạn rồi!";
        }

        if (existingSworn.Count >= 2)
        {
            return "Bạn đã có đủ 2 nghĩa huynh đệ kim lan (theo điển cố Đào Viên Tam Kết Nghĩa), không thể kết bái thêm.";
        }

        var rel = GetRelationship(npcId);
        if (rel.Affinity < 80 || rel.Trust < 60)
        {
            return $"{npc.NameVi} chưa đủ tin cậy (cần Hảo Cảm ≥ 80 và Tín Nhiệm ≥ 60, hiện tại {rel.Affinity}/100 hảo cảm, {rel.Trust}/100 tín nhiệm) để cùng thề ước sinh tử.";
        }

        const int oathCost = 100;
        if (_wallet.Liquid < oathCost)
        {
            return $"Cần {oathCost} Quan sắm sửa lễ vật trâu rượu tế cáo trời đất, hiện chỉ có {_wallet.Liquid} Quan.";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);
        var bondTitle = existingSworn.Count == 0 ? "Nghĩa Huynh" : "Nghĩa Đệ";

        _db.Transaction(() =>
        {
            _wallet.Charge(oathCost, JournalCodes.CaravanFreight, $"Lễ vật tế cáo trời đất kết nghĩa kim lan cùng {npc.NameVi}");
            _clock.Advance(1);

            _db.Execute(
                """
                INSERT INTO kz_sworn_brother (npc_id, oath_date, bond_title, assist_count)
                VALUES (@npcId, @oathDate, @bondTitle, 0)
                """,
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@oathDate", formattedDate)
                    .Add("@bondTitle", bondTitle));

            // Lock affinity & trust to 100, impression to Sworn Brother
            _db.Execute(
                """
                INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date)
                VALUES (@npcId, 100, 100, 'Kết Nghĩa Huynh Đệ', 0, @date)
                ON CONFLICT(npc_id) DO UPDATE SET
                    affinity = 100,
                    trust = 100,
                    impression = 'Kết Nghĩa Huynh Đệ',
                    last_interaction_date = excluded.last_interaction_date
                """,
                new DbArgs().Add("@npcId", npcId).Add("@date", formattedDate));

            _db.Execute(
                """
                INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi)
                VALUES (@npcId, @date, 'oath', 'positive', 20, @summary)
                """,
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"Cùng {npc.NameVi} cắt máu ăn thề, kết nghĩa huynh đệ kim lan tại Đào Viên, nguyện đồng tâm hiệp lực, hoạn nạn có nhau, sinh tử không rời!"));
        });

        _registry.UpdateCitadel(npcId, _player.CurrentCityId);

        var bondNotice = ApplyBondConsequences(npcId, Math.Max(0, 100 - rel.Affinity), rel.Impression);
        RecordMetNpc(npcId, today, _player.CurrentCityId);
        return $"Trời đất chứng giám! Bạn cùng {npc.NameVi} đã chính thức kết nghĩa huynh đệ kim lan ({bondTitle})! Tình nghĩa keo sơn, hoạn nạn có nhau." + bondNotice;
    }

    public string HostBanquet()
    {
        string bondNotices = "";
        const int banquetCost = 150;
        if (_wallet.Liquid < banquetCost)
        {
            return $"Cần {banquetCost} Quan để mở yến tiệc chiêu đãi quần hiền, hiện chỉ có {_wallet.Liquid} Quan.";
        }

        var cityId = _player.CurrentCityId;
        var localNpcs = _registry.FindByCitadel(cityId)
            .Take(6)
            .ToList();

        if (localNpcs.Count == 0)
        {
            return "Không có danh sĩ nào tại thành này để cùng nâng chén thưởng nguyệt.";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            _wallet.Charge(banquetCost, JournalCodes.CaravanFreight, "Mở đại yến tiệc kết giao hào kiệt");
            _clock.Advance(1);

            foreach (var npc in localNpcs)
            {
                var rel = GetRelationship(npc.Id);
                var newAffinity = Math.Min(100, rel.Affinity + 12);
                var newTrust = Math.Min(100, rel.Trust + 5);
                var impression = CalculateImpression(newAffinity);

                _db.Execute(
                    """
                    INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date)
                    VALUES (@npcId, @aff, @trust, @imp, 0, @date)
                    ON CONFLICT(npc_id) DO UPDATE SET
                        affinity = excluded.affinity,
                        trust = excluded.trust,
                        impression = excluded.impression,
                        last_interaction_date = excluded.last_interaction_date
                    """,
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@aff", newAffinity)
                        .Add("@trust", newTrust)
                        .Add("@imp", impression)
                        .Add("@date", formattedDate));

                _db.Execute(
                    """
                    INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi)
                    VALUES (@npcId, @date, 'banquet', 'positive', 12, @summary)
                    """,
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@date", formattedDate)
                        .Add("@summary", $"Dự đại yến do bạn chủ trì, cùng nâng chén thưởng nguyệt, đàm luận thế sự thiên hạ."));
                bondNotices += ApplyBondConsequences(npc.Id, newAffinity - rel.Affinity, rel.Impression);
            }

            var summary = $"Đại yến quy tụ {localNpcs.Count} danh sĩ ({string.Join(", ", localNpcs.Select(n => n.NameVi))}). Hảo cảm đồng loạt gia tăng!";
            _db.Execute(
                """
                INSERT INTO kz_banquet (citadel_id, date, cost_copper, attendee_count, summary_vi)
                VALUES (@city, @date, @cost, @count, @summary)
                """,
                new DbArgs()
                    .Add("@city", cityId)
                    .Add("@date", formattedDate)
                    .Add("@cost", banquetCost)
                    .Add("@count", localNpcs.Count)
                    .Add("@summary", summary));
        });

        foreach (var npc in localNpcs)
        {
            RecordMetNpc(npc.Id, today, cityId);
        }

        return $"Đại yến thành công rực rỡ! {localNpcs.Count} danh sĩ ({string.Join(", ", localNpcs.Select(n => n.NameVi))}) tề tựu nâng chén, hảo cảm đồng loạt tăng +12 và tín nhiệm +5!" + bondNotices;
    }

    public NpcPsycheProfile GetPsycheProfile(long npcId)
    {
        var npc = _registry.Find(npcId);
        if (npc is null)
        {
            return new NpcPsycheProfile(npcId, CharacterArchetype.Pragmatist, "Thương Nhân", "💰 Thực Dụng", 50, 50, 50);
        }

        var arch = ResolveNpcArchetype(npc);
        var (archVi, badgeVi, ambition, integrity, materialism) = arch switch
        {
            CharacterArchetype.Hegemon => ("Bá Đạo Kiêu Hùng", "⚔️ Bá Đạo", 90, 45, 60),
            CharacterArchetype.Benevolent => ("Vương Đạo Nhân Nghĩa", "🕊️ Vương Đạo", 60, 95, 30),
            CharacterArchetype.Pragmatist => ("Thực Dụng Thương Doanh", "💰 Thực Dụng", 65, 60, 90),
            CharacterArchetype.Hermit => ("Phương Ngoại Ẩn Sĩ", "🍵 Ẩn Sĩ", 20, 85, 25),
            _ => ("Hào Kiệt", "★ Trung Dung", 50, 50, 50)
        };

        return new NpcPsycheProfile(npcId, arch, archVi, badgeVi, ambition, integrity, materialism);
    }

    public static CharacterArchetype ResolveNpcArchetype(NpcDef npc)
    {
        var code = (npc.Code ?? string.Empty).ToLowerInvariant();
        var role = (npc.Role ?? string.Empty).ToLowerInvariant();
        var name = (npc.NameVi ?? string.Empty).ToLowerInvariant();

        if (code is "npc_0001" or "npc_0004" or "npc_0016" or "npc_0018" or "npc_0019" or "npc_0020" or "npc_0021" or "npc_0022" or "npc_0023")
        {
            if (name.Contains("lưu bị") || name.Contains("quan vũ") || name.Contains("triệu vân"))
                return CharacterArchetype.Benevolent;
            return CharacterArchetype.Hegemon;
        }

        if (name.Contains("hoa đà") || name.Contains("tư mã huy") || name.Contains("gia cát") || name.Contains("tả từ") ||
            role.Contains("đạo nhân") || role.Contains("kỳ sĩ") || role.Contains("tiên sinh") || role.Contains("dược"))
        {
            return CharacterArchetype.Hermit;
        }

        if (name.Contains("lưu bị") || name.Contains("quan vũ") || name.Contains("trương phi") || name.Contains("triệu vân") ||
            role.Contains("nhân đức") || role.Contains("nghĩa hiệp") || role.Contains("hoàng gia"))
        {
            return CharacterArchetype.Benevolent;
        }

        if (role.Contains("thương") || role.Contains("chưởng quỹ") || role.Contains("tiền trang") || role.Contains("lão bản") ||
            name.Contains("mi trúc") || name.Contains("lỗ túc"))
        {
            return CharacterArchetype.Pragmatist;
        }

        var attr = npc.Attributes;
        if (attr.War >= 75 || attr.Ldr >= 80)
        {
            return attr.War >= 85 ? CharacterArchetype.Hegemon : CharacterArchetype.Benevolent;
        }

        if (attr.Com >= 65)
        {
            return CharacterArchetype.Pragmatist;
        }

        if (attr.Int >= 75)
        {
            return CharacterArchetype.Hermit;
        }

        return CharacterArchetype.Benevolent;
    }

    private static string AutoAssignRole(CharacterAttributes a)
    {
        if (a.War >= 75) return "bodyguard";
        if (a.Com >= 70) return "manager";
        if (a.Int >= 75) return "advisor";
        return "accountant";
    }

    public static string RoleTitleVi(string role) => role switch
    {
        "bodyguard" => "Hộ Vệ Tướng Quân (Bảo vệ an nguy)",
        "manager" => "Chưởng Quầy Sạp Hàng (Kinh doanh thương mại)",
        "advisor" => "Mưu Thần Quân Sư (Hiến kế mưu lược)",
        "accountant" => "Kế Toán Sổ Sách (Quản lý kho quỹ)",
        "artisan" => "Giám Công Thợ Cả (Ủy thác khai thác & chế tác)",
        _ => "Gia Thần"
    };

    public void RecordCooperation(long npcId, int affinityDelta, int trustDelta, string summaryVi)
    {
        var npc = _registry.Find(npcId);
        if (npc is null) return;

        var formattedDate = GameDateCodec.Format(_clock.Today);
        var rel = GetRelationship(npcId);
        var newAffinity = Math.Clamp(rel.Affinity + affinityDelta, -100, 100);
        var newTrust = Math.Clamp(rel.Trust + trustDelta, 0, 100);
        var impression = CalculateImpression(newAffinity);
        var sentiment = affinityDelta >= 0 ? "positive" : "negative";
        var summary = string.IsNullOrWhiteSpace(summaryVi)
            ? $"Hợp tác thương vụ và hoàn thành ủy thác cùng {npc.NameVi}."
            : summaryVi.Trim();

        _db.Execute(
            """
            INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date)
            VALUES (@npcId, @aff, @trust, @imp, 0, @date)
            ON CONFLICT(npc_id) DO UPDATE SET
                affinity = excluded.affinity,
                trust = excluded.trust,
                impression = excluded.impression,
                last_interaction_date = excluded.last_interaction_date
            """,
            new DbArgs()
                .Add("@npcId", npcId)
                .Add("@aff", newAffinity)
                .Add("@trust", newTrust)
                .Add("@imp", impression)
                .Add("@date", formattedDate));

        _db.Execute(
            """
            INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi)
            VALUES (@npcId, @date, 'commission', @sentiment, @delta, @summary)
            """,
            new DbArgs()
                .Add("@npcId", npcId)
                .Add("@date", formattedDate)
                .Add("@sentiment", sentiment)
                .Add("@delta", affinityDelta)
                .Add("@summary", summary));
    }

    private sealed class SimpleRng
    {
        private uint _state;

        public SimpleRng(uint seed)
        {
            _state = seed == 0 ? 0x12345678 : seed;
        }

        public int Next(int max)
        {
            if (max <= 1) return 0;
            // Xorshift32
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)max);
        }
    }
}
