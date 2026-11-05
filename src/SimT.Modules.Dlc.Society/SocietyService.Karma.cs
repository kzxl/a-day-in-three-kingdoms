using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    // ------------------------------------------------------------- Branching Karma & Causality Engine

    private void EnsureKarmaInitialized()
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='so_karma_seed'") > 0;
        if (!hasTable) return;

        var count = _db.ScalarLong("SELECT COUNT(*) FROM so_karma_seed");
        if (count > 0) return;

        var today = _clock.Today.DayIndex;
        var formattedDate = GameDateCodec.Format(_clock.Today);

        _db.Transaction(() =>
        {
            // Seed 1: Thương Đạo - Đại Hạn Mùa Đông (Starting city)
            _db.Execute(
                """
                INSERT OR IGNORE INTO so_karma_seed (
                    chain_code, title_vi, current_stage, max_stage, branch_choice,
                    planted_day, trigger_after_day, citadel_id, target_npc_id, karma_alignment,
                    is_active, is_resolved, last_outcome_vi, created_date)
                VALUES ('CH_GRAIN_FAMINE', 'Đại Hạn Mùa Đông · Khảo Nghiệm Lòng Nhân', 1, 2, 'pending',
                        @today, @today, @citadel, NULL, 'neutral', 1, 0, 'Lưu dân đói khát lánh nạn trước cổng thành...', @date)
                """,
                new DbArgs().Add("@today", today).Add("@citadel", _player.CurrentCityId).Add("@date", formattedDate));

            // Seed 2: Giang Hồ Nghĩa Hiệp - Lão Trượng Thọ Thương (Trường An or neighbor)
            var targetCity2 = _map.Citadels.Count > 1 ? _map.Citadels[1].Id : _player.CurrentCityId;
            _db.Execute(
                """
                INSERT OR IGNORE INTO so_karma_seed (
                    chain_code, title_vi, current_stage, max_stage, branch_choice,
                    planted_day, trigger_after_day, citadel_id, target_npc_id, karma_alignment,
                    is_active, is_resolved, last_outcome_vi, created_date)
                VALUES ('CH_MOHIST_ENGINEER', 'Cổ Đạo Dược Lão · Duyên Kỳ Ngộ Mặc Gia', 1, 2, 'pending',
                        @today, @trigger, @citadel, NULL, 'neutral', 1, 0, 'Phát hiện một lão trượng ngã bên vệ đường hoang sơn...', @date)
                """,
                new DbArgs().Add("@today", today).Add("@trigger", today + 20).Add("@citadel", targetCity2).Add("@date", formattedDate));

            // Seed 3: Đạo Tặc Bắt Quả Tang (Trác Huyện or City 4)
            var targetCity3 = _map.Citadels.Count > 3 ? _map.Citadels[3].Id : _player.CurrentCityId;
            _db.Execute(
                """
                INSERT OR IGNORE INTO so_karma_seed (
                    chain_code, title_vi, current_stage, max_stage, branch_choice,
                    planted_day, trigger_after_day, citadel_id, target_npc_id, karma_alignment,
                    is_active, is_resolved, last_outcome_vi, created_date)
                VALUES ('CH_HORSE_THIEF', 'Tráng Hán Đạo Mã · Tội Nghiệt & Hiếu Đạo', 1, 2, 'pending',
                        @today, @trigger, @citadel, NULL, 'neutral', 1, 0, 'Bắt quả tang kẻ dắt trộm chiến mã trong đêm tối...', @date)
                """,
                new DbArgs().Add("@today", today).Add("@trigger", today + 35).Add("@citadel", targetCity3).Add("@date", formattedDate));
        });
    }

    public long PlantSeed(
        string chainCode,
        string titleVi,
        string branchChoice,
        int triggerDelayDays,
        long citadelId,
        string karmaAlignment,
        long? targetNpcId = null)
    {
        var today = _clock.Today.DayIndex;
        var formattedDate = GameDateCodec.Format(_clock.Today);

        _db.Execute(
            """
            INSERT INTO so_karma_seed (
                chain_code, title_vi, current_stage, max_stage, branch_choice,
                planted_day, trigger_after_day, citadel_id, target_npc_id, karma_alignment,
                is_active, is_resolved, last_outcome_vi, created_date)
            VALUES (@code, @title, 1, 2, @choice, @today, @trigger, @cit, @npc, @align, 1, 0, 'Hạt giống nhân quả mới gieo mầm...', @date)
            """,
            new DbArgs()
                .Add("@code", chainCode)
                .Add("@title", titleVi)
                .Add("@choice", branchChoice)
                .Add("@today", today)
                .Add("@trigger", today + Math.Max(0, triggerDelayDays))
                .Add("@cit", citadelId)
                .Add("@npc", targetNpcId)
                .Add("@align", karmaAlignment)
                .Add("@date", formattedDate));

        return _db.ScalarLong("SELECT last_insert_rowid()");
    }

    public IReadOnlyList<KarmaSeedDef> GetActiveSeeds() =>
        QuerySeeds("WHERE is_active = 1 AND is_resolved = 0 ORDER BY trigger_after_day ASC");

    public IReadOnlyList<KarmaSeedDef> GetAllSeeds() =>
        QuerySeeds("ORDER BY id DESC");

    private IReadOnlyList<KarmaSeedDef> QuerySeeds(string whereAndOrder)
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='so_karma_seed'") > 0;
        if (!hasTable) return Array.Empty<KarmaSeedDef>();

        var sql = $"""
            SELECT id, chain_code, title_vi, current_stage, max_stage, branch_choice,
                   planted_day, trigger_after_day, citadel_id, target_npc_id,
                   karma_alignment, is_active, is_resolved, last_outcome_vi, created_date
            FROM so_karma_seed
            {whereAndOrder}
            """;

        return _db.Query(sql, row =>
        {
            var id = row.GetInt64(0);
            var code = row.GetString(1);
            var title = row.GetString(2);
            var stage = row.GetInt32(3);
            var maxStage = row.GetInt32(4);
            var choice = row.GetString(5);
            var planted = row.GetInt32(6);
            var trigger = row.GetInt32(7);
            var citId = row.GetInt64(8);
            var npcId = row.IsDbNull(9) ? (long?)null : row.GetInt64(9);
            var align = row.GetString(10);
            var active = row.GetInt32(11) == 1;
            var resolved = row.GetInt32(12) == 1;
            var outcome = row.GetString(13);
            var dateStr = row.GetString(14);

            var citName = _map.Find(citId)?.NameVi ?? "Bản địa";
            var npcName = npcId.HasValue ? _registry.Find(npcId.Value)?.NameVi : null;

            return new KarmaSeedDef(
                id,
                code,
                title,
                stage,
                maxStage,
                choice,
                planted,
                trigger,
                citId,
                citName,
                npcId,
                npcName,
                align,
                active,
                resolved,
                outcome,
                GameDateCodec.Parse(dateStr));
        });
    }

    public IReadOnlyList<KarmaHistoryDef> GetHistory(long? seedId = null)
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='so_karma_history'") > 0;
        if (!hasTable) return Array.Empty<KarmaHistoryDef>();

        var sql = "SELECT id, seed_id, chain_code, stage, decision_vi, consequence_vi, stat_delta_summary, recorded_day, recorded_date FROM so_karma_history ";
        DbArgs? args = null;
        if (seedId.HasValue)
        {
            sql += "WHERE seed_id = @sid ";
            args = DbArgs.One("@sid", seedId.Value);
        }
        sql += "ORDER BY id DESC";

        return _db.Query(sql, row => new KarmaHistoryDef(
            row.GetInt64(0),
            row.GetInt64(1),
            row.GetString(2),
            row.GetInt32(3),
            row.GetString(4),
            row.GetString(5),
            row.IsDbNull(6) ? null : row.GetString(6),
            row.GetInt32(7),
            GameDateCodec.Parse(row.GetString(8))),
            args);
    }

    public IReadOnlyList<KarmaPendingEvent> CheckPendingEvents(long currentCitadelId, int currentDay)
    {
        var seeds = GetActiveSeeds();
        var pending = new List<KarmaPendingEvent>();

        foreach (var s in seeds)
        {
            if (s.CitadelId != currentCitadelId || s.TriggerAfterDay > currentDay)
            {
                continue;
            }

            var prompt = BuildKarmaPrompt(s);
            if (prompt != null)
            {
                pending.Add(prompt);
            }
        }

        return pending;
    }

    private KarmaPendingEvent? BuildKarmaPrompt(KarmaSeedDef s)
    {
        return s.ChainCode switch
        {
            "CH_GRAIN_FAMINE" => s.CurrentStage switch
            {
                1 => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Đại Hạn Mùa Đông · Khảo Nghiệm Lòng Nhân",
                    $"Trước cổng thành {s.CitadelNameVi}, hàng trăm lưu dân đói khát co ro trong gió tuyết, tiếng khóc than vang vọng. Các sạp lương thực địa phương đã đóng cửa hoặc đẩy giá lên cao gấp bội. Bạn đứng trước ngã rẽ lương tri:",
                    1,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("generous", "Xuất Lương Phát Chẩn", "Tốn 150 Quan mua lương cứu đói, tích lũy đại đức (+15 Đức, +5 Danh)", "virtue"),
                        new KarmaPromptOption("ruthless", "Đầu Cơ Trục Lợi", "Bắt tay cùng lái buôn đầu cơ kiếm lời (+300 Quan, -20 Đức, -10 Danh)", "ruthless"),
                        new KarmaPromptOption("neutral", "Khoanh Tay Làm Ngơ", "Tránh dính líu phiền phức, bảo toàn nguyên khí (Không biến động)", "neutral")
                    }),
                2 when s.BranchChoice == "generous" => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Thiện Duyên Tương Ngộ · Môn Đồ Mặc Gia",
                    $"Một thanh niên tuấn tú trong đoàn lưu dân năm xưa nay ăn vận chỉnh tề quay lại {s.CitadelNameVi} bái kiến. Chàng vốn là truyền nhân Mạc Gia, nhờ chén cháo cứu mạng năm đó mà vượt qua cơn bạo bệnh. Nay chàng đem 'Thủy Xa Đồ Phổ' bí truyền đến dâng tặng ân công!",
                    2,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("accept_steward", "Thu Nhận Môn Đồ", "Thu nạp làm môn khách, tiếp nhận đồ phổ cơ quan (+1,200 Quan giá trị, +20 Đức)", "virtue"),
                        new KarmaPromptOption("reward_scholar", "Khuyên Vào Triều Đình", "Tặng lộ phí khuyên chàng ra làm quan cứu đời (+35 Danh Vọng, +500 Quan hồi báo)", "virtue")
                    }),
                2 when s.BranchChoice == "ruthless" => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Ác Nghiệp Báo Ứng · Nạn Dân Nổi Dậy",
                    $"Lưu dân năm xưa bị xua đuổi nay cùng đường đã kết thành toán cướp Hắc Sơn, biết được tung tích bạn tại {s.CitadelNameVi} liền kéo đến bao vây, đòi nợ máu năm xưa!",
                    2,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("bribe_settle", "Nộp Bạc Bồi Thường", "Xuất 600 Quan bồi thường để dập tắt cơn giận dữ của dân làng", "neutral"),
                        new KarmaPromptOption("stand_fight", "Quyết Chiến Trừng Trị", "Rút gươm nghênh địch, đánh tan đám phản loạn (-200 Quan tổn thất, +15 Danh)", "ruthless")
                    }),
                _ => null
            },

            "CH_MOHIST_ENGINEER" => s.CurrentStage switch
            {
                1 => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Cổ Đạo Dược Lão · Duyên Kỳ Ngộ",
                    $"Trên đường hoang dã {s.CitadelNameVi}, một lão trượng ngã gục bên vệ đường do trúng kịch độc xà cắn, hơi thở thoi thóp. Túi hành lý bên cạnh rơi ra một khối cơ quan mộc điểu tinh xảo:",
                    1,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("rescue", "Dốc Dược Cứu Mạng", "Dùng dược liệu quý cứu sống lão trượng (-100 Quan dược liệu, +15 Đức, +10 Danh)", "virtue"),
                        new KarmaPromptOption("search_belongings", "Tịch Thu Kỳ Vật", "Lục soát lấy đi cơ quan mộc điểu quý giá (+250 Quan, -25 Đức)", "ruthless"),
                        new KarmaPromptOption("ignore", "Lặng Lẽ Rời Đi", "Không can thiệp nhân duyên giang hồ (Bình an vô sự)", "neutral")
                    }),
                2 when s.BranchChoice == "rescue" => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Mặc Môn Hồi Báo · Thần Điểu Trợ Uy",
                    $"Lão trượng năm đó chính là Trưởng Lão Mặc Gia! Phái môn đồ mang đến tặng bạn 'Cơ Quan Thần Điểu' hoàn chỉnh cùng 1,500 Quan bồi tạ ơn cứu tử hồi sinh!",
                    2,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("accept_bird", "Hân Hoan Tiếp Nhận", "Tiếp nhận bảo vật thần cơ Mặc Gia (+1,500 Quan, +25 Đức, +25 Danh)", "virtue")
                    }),
                2 when s.BranchChoice == "search_belongings" => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Mặc Gia Hạch Tội · Công Đạo Thiên Hạ",
                    $"Đệ tử Mặc Gia lần theo cơ quan ấn ký, chặn đứng bạn tại {s.CitadelNameVi} bắt giao lại vật phẩm và bồi hoàn danh dự môn phái!",
                    2,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("repay_crime", "Đền Tội Bồi Hoàn", "Nộp phạt 800 Quan và trao trả đồ vật (-800 Quan, -15 Đức, -20 Danh)", "neutral")
                    }),
                _ => null
            },

            "CH_HORSE_THIEF" => s.CurrentStage switch
            {
                1 => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Tráng Hán Đạo Mã · Lòng Hiếu & Đạo Tặc",
                    $"Nửa đêm tại chuồng ngựa thành {s.CitadelNameVi}, bạn bắt tại trận một gã lực lưỡng đang dắt trộm chiến mã. Gã quỳ lạy khóc lóc giãi bày mẹ già bạo bệnh cần tiền mua sâm:",
                    1,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("forgive", "Khoan Thứ Ban Lộ Phí", "Tha bổng và cho thêm 50 Quan chữa bệnh cho mẹ (+12 Đức, +5 Danh)", "virtue"),
                        new KarmaPromptOption("arrest", "Áp Giải Bắt Giao Quan", "Giao cho quan phủ lĩnh 120 Quan tiền thưởng (-5 Đức, +10 Danh)", "ruthless")
                    }),
                2 when s.BranchChoice == "forgive" => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Tuấn Mã Đền Ơn · Nghĩa Khí Hảo Hán",
                    $"Gã tráng hán năm xưa dẫn theo một con Tây Lương Hãn Huyết Mã tuyệt luân đến tìm bạn: 'Mẹ con nhờ ơn ngài đã khỏi bệnh, nay con xin dâng tuấn mã ngàn dặm để đáp đền ân công!'",
                    2,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("accept_horse", "Tiếp Nhận Chiến Mã", "Nhận Hãn Huyết Tuấn Mã danh chấn sa trường (+1,000 Quan giá trị, +20 Đức, +20 Danh)", "virtue")
                    }),
                2 when s.BranchChoice == "arrest" => new KarmaPendingEvent(
                    s.Id,
                    s.ChainCode,
                    "Thảo Khấu Báo Thù · Lửa Cháy Đêm Đông",
                    $"Đồng bọn thảo khấu của tên trộm ngựa lẻn vào phóng hỏa chuồng trại và cướp bóc kho hàng để trả thù!",
                    2,
                    s.CitadelId,
                    s.CitadelNameVi,
                    new[]
                    {
                        new KarmaPromptOption("endure_loss", "Cắn Răng Chịu Tổn Thất", "Khắc phục hậu quả hỏa hoạn (-500 Quan, -10 Đức, -10 Danh)", "neutral")
                    }),
                _ => null
            },

            _ => null
        };
    }

    public KarmaResolutionResult ResolveChoice(long seedId, string choiceKey)
    {
        var seed = _db.QueryOne(
            """
            SELECT id, chain_code, title_vi, current_stage, max_stage, branch_choice,
                   planted_day, trigger_after_day, citadel_id, target_npc_id,
                   karma_alignment, is_active, is_resolved, last_outcome_vi, created_date
            FROM so_karma_seed WHERE id = @id
            """,
            row => new KarmaSeedDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetInt32(3),
                row.GetInt32(4),
                row.GetString(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetInt64(8),
                _map.Find(row.GetInt64(8))?.NameVi ?? "Bản địa",
                row.IsDbNull(9) ? null : row.GetInt64(9),
                null,
                row.GetString(10),
                row.GetInt32(11) == 1,
                row.GetInt32(12) == 1,
                row.GetString(13),
                GameDateCodec.Parse(row.GetString(14))),
            DbArgs.One("@id", seedId));

        if (seed == null || !seed.IsActive || seed.IsResolved)
        {
            return new KarmaResolutionResult(false, "Không Thể Thực Hiện", "Sự kiện nhân quả không tồn tại hoặc đã giải quyết xong.", 0, 0, 0, true);
        }

        string headline, outcome;
        long moneyDelta = 0;
        int fameDelta = 0;
        int virtueDelta = 0;
        bool chainCompleted = false;
        int nextDelayDays = 30;
        string alignment = "neutral";

        switch (seed.ChainCode)
        {
            case "CH_GRAIN_FAMINE":
                if (seed.CurrentStage == 1)
                {
                    if (choiceKey == "generous")
                    {
                        moneyDelta = -150;
                        virtueDelta = 15;
                        fameDelta = 5;
                        alignment = "virtue";
                        headline = "Xuất Lương Cứu Đói Bá Tánh";
                        outcome = "Bá tánh nghẹn ngào cảm tạ ân đức, tiếng thơm đồn xa khắp kinh kỳ.";
                        nextDelayDays = 30;
                    }
                    else if (choiceKey == "ruthless")
                    {
                        moneyDelta = 300;
                        virtueDelta = -20;
                        fameDelta = -10;
                        alignment = "ruthless";
                        headline = "Đầu Cơ Trục Lợi Lưu Dân";
                        outcome = "Thu về khoản bạc lớn từ giá lương thực tăng vọt, nhưng bá tánh căm phẫn thấu xương.";
                        nextDelayDays = 30;
                    }
                    else
                    {
                        chainCompleted = true;
                        headline = "Bàng Quan Trước Thời Cuộc";
                        outcome = "Lặng lẽ rời đi, sóng gió qua mau không để lại vết tích.";
                    }
                }
                else // Stage 2
                {
                    chainCompleted = true;
                    if (choiceKey == "accept_steward")
                    {
                        moneyDelta = 1200;
                        virtueDelta = 20;
                        fameDelta = 25;
                        headline = "Đệ Tử Mạc Gia Đền Ơn";
                        outcome = "Tặng Thủy Xa Đồ Phổ gia truyền và thề nguyện phò tá ân nhân trọn đời!";
                    }
                    else if (choiceKey == "reward_scholar")
                    {
                        moneyDelta = 500;
                        virtueDelta = 15;
                        fameDelta = 35;
                        headline = "Thiện Niệm Kết Thiện Quả";
                        outcome = "Sĩ phu truyền tụng ân đức, quan phủ đề bạt danh xưng Đại Thiện Phú!";
                    }
                    else if (choiceKey == "bribe_settle")
                    {
                        moneyDelta = -600;
                        fameDelta = -10;
                        headline = "Hối Lộ Hóa Giải Báo Thù";
                        outcome = "Phải dâng nộp số tiền lớn bồi thường cho nạn dân để tránh tiêu xa bị thiêu rụi.";
                    }
                    else
                    {
                        moneyDelta = -200;
                        virtueDelta = -10;
                        fameDelta = 15;
                        headline = "Huyết Chiến Hắc Sơn Phỉ";
                        outcome = "Đánh tan đám người trả thù nhưng thương đoàn chịu nhiều tổn thất nặng nề.";
                    }
                }
                break;

            case "CH_MOHIST_ENGINEER":
                if (seed.CurrentStage == 1)
                {
                    if (choiceKey == "rescue")
                    {
                        moneyDelta = -100;
                        virtueDelta = 15;
                        fameDelta = 10;
                        alignment = "virtue";
                        headline = "Hiệp Nghĩa Trị Thương Cứu Người";
                        outcome = "Dùng đan dược cứu sống lão trượng thoát khỏi lưỡi hái tử thần.";
                        nextDelayDays = 40;
                    }
                    else if (choiceKey == "search_belongings")
                    {
                        moneyDelta = 250;
                        virtueDelta = -25;
                        fameDelta = -15;
                        alignment = "ruthless";
                        headline = "Đoạt Cổ Vật Của Người Bị Nạn";
                        outcome = "Tịch thu được cơ quan đồ vật quý nhưng vướng phải oán hận khôn nguôi.";
                        nextDelayDays = 40;
                    }
                    else
                    {
                        chainCompleted = true;
                        virtueDelta = -5;
                        headline = "Khoanh Tay Làm Ngơ";
                        outcome = "Không can thiệp chuyện sống chết người qua đường.";
                    }
                }
                else // Stage 2
                {
                    chainCompleted = true;
                    if (choiceKey == "accept_bird")
                    {
                        moneyDelta = 1500;
                        virtueDelta = 25;
                        fameDelta = 25;
                        headline = "Mặc Môn Hồi Báo Trọng Thưởng";
                        outcome = "Mặc Gia Tông Môn dâng tặng Cơ Quan Thần Điểu và 1,500 Quan bồi tạ ơn cứu tử hồi sinh!";
                    }
                    else
                    {
                        moneyDelta = -800;
                        virtueDelta = -15;
                        fameDelta = -20;
                        headline = "Mặc Môn Truy Sát Hạch Tội";
                        outcome = "Bị cao thủ Mặc Môn chặn đường lấy lại di vật và trừng phạt đích đáng.";
                    }
                }
                break;

            case "CH_HORSE_THIEF":
                if (seed.CurrentStage == 1)
                {
                    if (choiceKey == "forgive")
                    {
                        moneyDelta = -50;
                        virtueDelta = 12;
                        fameDelta = 5;
                        alignment = "virtue";
                        headline = "Khoan Thứ Kẻ Trộm Ngựa";
                        outcome = "Cảm động trước lòng hiếu thảo, tha bổng và ban lộ phí về quê chăm mẹ.";
                        nextDelayDays = 35;
                    }
                    else
                    {
                        moneyDelta = 120;
                        virtueDelta = -5;
                        fameDelta = 10;
                        alignment = "ruthless";
                        headline = "Nghiêm Trị Đạo Tặc Bắt Giao Quan";
                        outcome = "Nhận thưởng từ quan phủ nhưng gây thù chuốc oán cùng bang hội thảo khấu.";
                        nextDelayDays = 35;
                    }
                }
                else // Stage 2
                {
                    chainCompleted = true;
                    if (choiceKey == "accept_horse")
                    {
                        moneyDelta = 1000;
                        virtueDelta = 20;
                        fameDelta = 20;
                        headline = "Hảo Hán Đền Ơn Dâng Tuấn Mã";
                        outcome = "Tráng hán quay lại dâng tặng tuyệt phẩm Tây Lương Hãn Huyết Mã trị giá 1,000 Quan!";
                    }
                    else
                    {
                        moneyDelta = -500;
                        virtueDelta = -10;
                        fameDelta = -10;
                        headline = "Sơn Tặc Phóng Hỏa Trả Thù";
                        outcome = "Chuồng ngựa và sạp hàng bị đồng đảng phóng hỏa trả thù trong đêm tối.";
                    }
                }
                break;

            default:
                headline = "Biến Cố Nhân Quả";
                outcome = "Sự việc trôi qua trong thinh lặng.";
                chainCompleted = true;
                break;
        }

        // Apply financial outcome
        if (moneyDelta != 0 && _broker != null && _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet != null)
        {
            if (moneyDelta > 0)
            {
                wallet.Pay(moneyDelta, "KARMA.REWARD", outcome);
            }
            else
            {
                try
                {
                    wallet.Charge(Math.Abs(moneyDelta), "KARMA.LOSS", outcome);
                }
                catch
                {
                    // Fallback if liquid is short
                }
            }
        }

        // Apply reputation and virtue outcomes
        if ((fameDelta != 0 || virtueDelta != 0) && _broker != null && _broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs != null)
        {
            attrs.GainFame(fameDelta, outcome, virtueDelta: virtueDelta);
        }

        var today = _clock.Today.DayIndex;
        var formattedDate = GameDateCodec.Format(_clock.Today);

        // Record history
        var statSummary = $"{(moneyDelta != 0 ? $"{moneyDelta:+0;-0} Quan, " : "")}{(virtueDelta != 0 ? $"{virtueDelta:+0;-0} Đức, " : "")}{(fameDelta != 0 ? $"{fameDelta:+0;-0} Danh" : "")}".TrimEnd(',', ' ');
        _db.Execute(
            """
            INSERT INTO so_karma_history (
                seed_id, chain_code, stage, decision_vi, consequence_vi,
                stat_delta_summary, recorded_day, recorded_date)
            VALUES (@sid, @code, @stage, @dec, @cons, @stats, @day, @date)
            """,
            new DbArgs()
                .Add("@sid", seed.Id)
                .Add("@code", seed.ChainCode)
                .Add("@stage", seed.CurrentStage)
                .Add("@dec", choiceKey)
                .Add("@cons", outcome)
                .Add("@stats", statSummary)
                .Add("@day", today)
                .Add("@date", formattedDate));

        // Log journey chronicle event
        LogEvent(
            formattedDate,
            _player.PlayerId,
            _player.Name,
            seed.CitadelId,
            seed.CitadelNameVi,
            "commerce",
            $"[Nhân Quả] {headline}",
            outcome,
            statSummary,
            today);

        // Update seed progression
        if (chainCompleted || seed.CurrentStage >= seed.MaxStage)
        {
            _db.Execute(
                """
                UPDATE so_karma_seed SET
                    is_active = 0,
                    is_resolved = 1,
                    branch_choice = @choice,
                    karma_alignment = @align,
                    last_outcome_vi = @outcome
                WHERE id = @id
                """,
                new DbArgs()
                    .Add("@choice", choiceKey)
                    .Add("@align", alignment)
                    .Add("@outcome", outcome)
                    .Add("@id", seed.Id));
        }
        else
        {
            _db.Execute(
                """
                UPDATE so_karma_seed SET
                    current_stage = current_stage + 1,
                    branch_choice = @choice,
                    trigger_after_day = @trigger,
                    karma_alignment = @align,
                    last_outcome_vi = @outcome
                WHERE id = @id
                """,
                new DbArgs()
                    .Add("@choice", choiceKey)
                    .Add("@trigger", today + nextDelayDays)
                    .Add("@align", alignment)
                    .Add("@outcome", outcome)
                    .Add("@id", seed.Id));
        }

        return new KarmaResolutionResult(true, headline, outcome, moneyDelta, fameDelta, virtueDelta, chainCompleted);
    }
}
