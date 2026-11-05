CREATE TABLE IF NOT EXISTS dg_dialogue_line (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    target_kind TEXT NOT NULL,
    target_code TEXT NOT NULL,
    tier_key    TEXT NOT NULL,
    seq_order   INTEGER NOT NULL,
    line_vi     TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_dg_dialogue_lookup
    ON dg_dialogue_line (target_kind, target_code, tier_key, seq_order);

CREATE TABLE IF NOT EXISTS dg_dialogue_visit (
    npc_id        INTEGER PRIMARY KEY,
    visit_count   INTEGER NOT NULL DEFAULT 0,
    last_date_str TEXT NOT NULL DEFAULT ''
);

-- ============================================================
-- 1. LƯU BỊ (npc_0001)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('npc', 'npc_0001', 'stranger', 1, 'Bị này vốn dòng dõi Trung Sơn Tĩnh Vương, nay chỉ mong góp sức cứu muôn dân.'),
('npc', 'npc_0001', 'stranger', 2, 'Thiên hạ tao loạn, bách tính lầm than, người có chí há lại ngồi yên nhìn đời?'),
('npc', 'npc_0001', 'stranger', 3, 'Tiên sinh ghé qua tệ xá, xin mời dùng chén trà nhạt bàn chuyện thiên hạ.'),
('npc', 'npc_0001', 'acquaintance', 1, 'Nhiều lần gặp gỡ, Bị rất kính trọng tấm lòng trọng nghĩa của các hạ.'),
('npc', 'npc_0001', 'acquaintance', 2, 'Thành trì muốn yên thì chợ búa phải thông, lòng dân phải ấm no trước đã.'),
('npc', 'npc_0001', 'acquaintance', 3, 'Nếu có dịp đi qua các quận, xin giúp Bị hỏi thăm đời sống bá tánh.'),
('npc', 'npc_0001', 'trusted', 1, 'Giữa thời loạn thế mà gặp được tri kỷ như hiền đệ, thật là phúc của Lưu Bị!'),
('npc', 'npc_0001', 'trusted', 2, 'Đại nghiệp hưng phục Hán thất còn dài, mong hiền đệ cùng ta đồng tâm hiệp lực.'),
('npc', 'npc_0001', 'trusted', 3, 'Dù thành bại thế nào, chữ nhân nghĩa trong lòng ta quyết không bao giờ đổi.');

-- ============================================================
-- 2. QUAN VŨ (npc_0003)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('npc', 'npc_0003', 'stranger', 1, 'Quan mỗ bình sinh chỉ trọng người có nghĩa khí, lời nói phải đi đôi với việc làm.'),
('npc', 'npc_0003', 'stranger', 2, 'Thanh Long Yển Nguyệt Đao này chưa từng chém kẻ vô danh hay người bất nghĩa.'),
('npc', 'npc_0003', 'stranger', 3, 'Các hạ đến đây có việc gì cần bàn, xin cứ nói thẳng chớ vòng vo.'),
('npc', 'npc_0003', 'acquaintance', 1, 'Qua mấy bận giao thiệp, Quan mỗ biết các hạ là người giữ chữ tín.'),
('npc', 'npc_0003', 'acquaintance', 2, 'Việc quân cần nghiêm minh, việc thương cần sòng phẳng, đôi bên đều trọng đạo nghĩa.'),
('npc', 'npc_0003', 'acquaintance', 3, 'Khi nào rảnh rỗi, mời các hạ ra giáo trường cùng Quan mỗ luận bàn võ nghệ.'),
('npc', 'npc_0003', 'trusted', 1, 'Hiền hữu đối đãi chí tình, Quan Vân Trường này xin ghi tạc trong lòng!'),
('npc', 'npc_0003', 'trusted', 2, 'Ngọc có thể nát chứ chẳng đổi sắc trắng, trúc có thể cháy chứ chẳng hủy đốt ngay.'),
('npc', 'npc_0003', 'trusted', 3, 'Nếu trên đường bôn ba có kẻ cậy thế ức hiếp, cứ báo tên Quan mỗ!');

-- ============================================================
-- 3. TRƯƠNG PHI (npc_0002)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('npc', 'npc_0002', 'stranger', 1, 'Ta là Trương Dực Đức người Trác Quận! Ai dám uống cạn vò rượu này với ta?'),
('npc', 'npc_0002', 'stranger', 2, 'Nói chuyện với Trương gia thì cứ thẳng ruột ngựa, ta ghét nhất hạng quanh co!'),
('npc', 'npc_0002', 'stranger', 3, 'Bát Xà Mâu của ta lâu ngày chưa gặp đối thủ xứng tầm trên lôi đài!'),
('npc', 'npc_0002', 'acquaintance', 1, 'Ha ha! Lâu ngày không gặp, hôm nay nhất định phải uống với Lão Trương vài chén!'),
('npc', 'npc_0002', 'acquaintance', 2, 'Ngươi tuy đi buôn nhưng tính tình hào sảng, rất hợp ý Trương Dực Đức này!'),
('npc', 'npc_0002', 'acquaintance', 3, 'Hôm nào rảnh thì ra lôi đài so vài đường thương cho giãn gân cốt nhé!'),
('npc', 'npc_0002', 'trusted', 1, 'Huynh đệ tốt! Việc của ngươi cũng chính là việc của Trương Dực Đức này!'),
('npc', 'npc_0002', 'trusted', 2, 'Rượu ngon phải uống cùng tri kỷ, ngựa tốt phải tặng cho anh hùng!'),
('npc', 'npc_0002', 'trusted', 3, 'Ai dám động đến một sợi tóc của huynh đệ ta, Bát Xà Mâu quyết không tha!');

-- ============================================================
-- 4. GIA CÁT LƯỢNG (npc_0025)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('npc', 'npc_0025', 'stranger', 1, 'Chưa bước ra khỏi lều tranh đã biết thế chân vạc của thiên hạ.'),
('npc', 'npc_0025', 'stranger', 2, 'Khổng Minh chỉ là kẻ cày ruộng đất Nam Dương, dám đâu bàn chuyện cao xa.'),
('npc', 'npc_0025', 'stranger', 3, 'Nhìn sắc trời và giá lương thảo trong thành cũng đủ đoán vận nước thịnh suy.'),
('npc', 'npc_0025', 'acquaintance', 1, 'Tiên sinh đi nhiều nơi, hẳn đã thấy rõ mối liên hệ giữa thương đạo và binh cơ.'),
('npc', 'npc_0025', 'acquaintance', 2, 'Binh chưa động thì lương phải đi trước, người hiểu giá cả tức là hiểu thời thế.'),
('npc', 'npc_0025', 'acquaintance', 3, 'Gió đông sắp nổi, người biết nắm thời cơ ắt sẽ lập nên cơ nghiệp lớn.'),
('npc', 'npc_0025', 'trusted', 1, 'Cúc cung tận tụy, đến chết mới thôi — đó là tâm nguyện cả đời của Lượng.'),
('npc', 'npc_0025', 'trusted', 2, 'Có tri kỷ hiểu lòng như các hạ, mỗi ván cờ thiên hạ đều thêm phần sáng tỏ.'),
('npc', 'npc_0025', 'trusted', 3, 'Khi vận chuyển lương thảo qua vùng hiểm yếu, hãy nhớ xem kỹ hướng gió và mùa mưa.');

-- ============================================================
-- 5. TÀO THÁO (npc_0022)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('npc', 'npc_0022', 'stranger', 1, 'Thời loạn chuộng người tài, Tào Mạnh Đức chỉ hỏi năng lực chứ không hỏi xuất thân.'),
('npc', 'npc_0022', 'stranger', 2, 'Anh hùng trong thiên hạ ngày nay, thử hỏi có mấy người nhìn thấu đại cục?'),
('npc', 'npc_0022', 'stranger', 3, 'Các hạ đến gặp ta, hẳn trong ngực cũng có mưu kế hoặc bản lĩnh hơn người.'),
('npc', 'npc_0022', 'acquaintance', 1, 'Đồn điền và thương mại đều là gốc của phú quốc cường binh, ta rất coi trọng.'),
('npc', 'npc_0022', 'acquaintance', 2, 'Người biết tính toán đường vận lương ngàn dặm còn quý hơn vạn quân giáp sắt.'),
('npc', 'npc_0022', 'acquaintance', 3, 'Uống rượu làm thơ, ngắm trăng sáng mà bàn chuyện anh hùng, há chẳng sảng khoái sao?'),
('npc', 'npc_0022', 'trusted', 1, 'Núi không ngại cao, biển không ngại sâu; Chu Công nhả cơm, thiên hạ quy tâm!'),
('npc', 'npc_0022', 'trusted', 2, 'Được người tâm giao tài trí như khanh, việc lớn phương Bắc ắt sớm thành công.'),
('npc', 'npc_0022', 'trusted', 3, 'Cứ mạnh dạn mở rộng thương hội, phủ thừa tướng luôn hậu thuẫn cho khanh.');

-- ============================================================
-- 6. LỮ BỐ (npc_0057)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('npc', 'npc_0057', 'stranger', 1, 'Nhân trung Lữ Bố, mã trung Xích Thố! Dưới gầm trời này ai địch nổi Phương Thiên Kích?'),
('npc', 'npc_0057', 'stranger', 2, 'Kẻ yếu thì bàn mưu kế, bậc vô địch chỉ dùng một đường kích định giang sơn!'),
('npc', 'npc_0057', 'stranger', 3, 'Nếu không có bảo vật hay bản lĩnh cao cường thì chớ làm mất thời giờ của bản hầu.'),
('npc', 'npc_0057', 'acquaintance', 1, 'Ánh mắt của ngươi khá lắm, đứng trước sát khí của bản hầu mà không hề nao núng.'),
('npc', 'npc_0057', 'acquaintance', 2, 'Chiến mã tốt và thần binh sắc bén mới xứng với bậc dũng tướng cái thế.'),
('npc', 'npc_0057', 'acquaintance', 3, 'Muốn thử sức với Phương Thiên Họa Kích thì bước lên lôi đài bất cứ lúc nào!'),
('npc', 'npc_0057', 'trusted', 1, 'Trong thiên hạ này, số người khiến Lữ Phụng Tiên nể phục đếm trên đầu ngón tay!'),
('npc', 'npc_0057', 'trusted', 2, 'Có ngươi kết giao, chiến mã của ta chẳng bao giờ thiếu cỏ tốt và giáp bền.'),
('npc', 'npc_0057', 'trusted', 3, 'Hãy nhớ kỹ: kẻ nào dám cản đường bằng hữu của Lữ Bố sẽ phải nếm mũi kích này!');

-- ============================================================
-- 7. VAI TRÒ: VÕ TƯỚNG (general)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('role', 'general', 'stranger', 1, 'Thao trường đổ mồ hôi thì chiến trường mới bớt đổ máu, ngày nào ta cũng luyện binh.'),
('role', 'general', 'stranger', 2, 'Thời buổi loạn lạc, ra khỏi cổng thành một bước là phải đề phòng đạo tặc.'),
('role', 'general', 'stranger', 3, 'Các hạ có việc gì muốn bàn cùng võ biền như tại hạ?'),
('role', 'general', 'acquaintance', 1, 'Quân lương và binh khí trong thành dạo này đều nhờ các thương đoàn cung ứng.'),
('role', 'general', 'acquaintance', 2, 'Người có gan đi qua các tuyến đường biên ải quả thực đáng mặt hào kiệt.'),
('role', 'general', 'acquaintance', 3, 'Nếu muốn rèn luyện thân thủ, cứ ra giáo trường tỉ thí cùng tại hạ vài hiệp.'),
('role', 'general', 'trusted', 1, 'Huynh đệ đồng lòng, dù vạn quân vây thành cũng chẳng mảy may sờn lòng!'),
('role', 'general', 'trusted', 2, 'Đội tuần phòng của ta luôn để mắt bảo vệ kho hàng và đoàn xe của huynh đệ.'),
('role', 'general', 'trusted', 3, 'Thanh đao này luôn sẵn sàng vì bằng hữu mà vung lên giữa thời loạn!');

-- ============================================================
-- 8. VAI TRÒ: VĂN SĨ (scholar)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('role', 'scholar', 'stranger', 1, 'Đọc vạn quyển sách không bằng đi vạn dặm đường để thấu hiểu nhân tình thế thái.'),
('role', 'scholar', 'stranger', 2, 'Lễ nhạc tuy suy vi nhưng đạo lý làm người vẫn còn sáng trong từng trang cổ thư.'),
('role', 'scholar', 'stranger', 3, 'Mời tiên sinh ngồi đàm đạo đôi câu về thế cuộc các châu quận hiện nay.'),
('role', 'scholar', 'acquaintance', 1, 'Người làm thương nghiệp mà biết trọng chữ nghĩa thì đi tới đâu cũng được nể vì.'),
('role', 'scholar', 'acquaintance', 2, 'Quan phủ gần đây đang chỉnh đốn sổ sách thuế khóa, tiên sinh nên lưu tâm.'),
('role', 'scholar', 'acquaintance', 3, 'Mỗi bức thư tiến cử đúng lúc có thể mở ra cả một con đường công danh.'),
('role', 'scholar', 'trusted', 1, 'Tri âm khó tìm như khúc Cao Sơn Lưu Thủy, gặp được hiền hữu thật đáng quý!'),
('role', 'scholar', 'trusted', 2, 'Mọi biến động chốn quan trường và thị trường trong quận ta đều ghi chép rõ cho đệ.'),
('role', 'scholar', 'trusted', 3, 'Cầm kỳ thi họa hay mưu lược kinh bang, ta đều sẵn lòng sẻ chia cùng tri kỷ.');

-- ============================================================
-- 9. VAI TRÒ: THƯƠNG NHÂN (merchant)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('role', 'merchant', 'stranger', 1, 'Buôn tài không bằng dài vốn, nhưng quý nhất trên thương trường vẫn là chữ tín.'),
('role', 'merchant', 'stranger', 2, 'Hàng hóa mỗi vùng một giá, biết mua nơi thừa bán nơi thiếu mới là bậc cao tay.'),
('role', 'merchant', 'stranger', 3, 'Khách quan muốn hỏi thăm giá cả mặt hàng nào trong chợ hôm nay?'),
('role', 'merchant', 'acquaintance', 1, 'Đồng nghiệp lâu năm gặp lại, hôm nay tại hạ xin tiết lộ vài tin giá cả hữu ích.'),
('role', 'merchant', 'acquaintance', 2, 'Mùa màng và thời tiết đổi thay từng tháng, tích trữ đúng lúc lợi nhuận tăng gấp bội.'),
('role', 'merchant', 'acquaintance', 3, 'Kho bãi trong thành có hạn, giữ hàng lâu ngày phải chú ý hao hụt bảo quản.'),
('role', 'merchant', 'trusted', 1, 'Chỗ giao tình sâu nặng như huynh đệ mình, có mối hàng tốt tất nhiên phải chia sẻ!'),
('role', 'merchant', 'trusted', 2, 'Các thương hội trong thành đều nể uy tín của hiền hữu, mua bán vô cùng thuận lợi.'),
('role', 'merchant', 'trusted', 3, 'Khi nào lập đoàn lữ hành đi xa, nhớ ghé sạp của ta lấy hàng giá ưu đãi nhé!');

-- ============================================================
-- 10. VAI TRÒ: NÔNG DÂN / BÁCH TÍNH (commoner)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('role', 'commoner', 'stranger', 1, 'Dân đen chúng tôi chỉ mong mưa thuận gió hòa, không bị binh đao cướp bóc.'),
('role', 'commoner', 'stranger', 2, 'Bán mặt cho đất bán lưng cho trời, hạt thóc làm ra thấm đẫm mồ hôi.'),
('role', 'commoner', 'stranger', 3, 'Khách đường xa ghé qua, mời uống bát nước mát dưới bóng cây đầu làng.'),
('role', 'commoner', 'acquaintance', 1, 'Nhờ các thương khách thu mua công bằng mà bà con trong vùng đỡ phần túng thiếu.'),
('role', 'commoner', 'acquaintance', 2, 'Vụ mùa năm nay tùy thuộc vào nước tưới, giá lúa gạo trong chợ cũng theo đó mà lên.'),
('role', 'commoner', 'acquaintance', 3, 'Đường tắt qua sườn đồi tuy gần nhưng dạo này có thú dữ, người đi lại nhớ cẩn thận.'),
('role', 'commoner', 'trusted', 1, 'Ân nhân luôn đối xử tử tế với dân nghèo, cả xóm ai cũng nhắc tên người!'),
('role', 'commoner', 'trusted', 2, 'Có chút nông sản mới thu hoạch còn tươi ngon, mong ân nhân đừng chê lễ mọn.'),
('role', 'commoner', 'trusted', 3, 'Hễ trong vùng có tin tức gì lạ, bà con sẽ lập tức báo cho người hay biết.');

-- ============================================================
-- 11. VAI TRÒ: PHỤ NỮ / GIAI NHÂN (lady)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('role', 'lady', 'stranger', 1, 'Tiếng đàn tỳ bà vang bên hiên vắng, mấy ai thấu hiểu nỗi niềm người thời loạn.'),
('role', 'lady', 'stranger', 2, 'Lụa tốt phải dệt từ tơ tằm thượng hạng, lòng người quý ở sự chân thành.'),
('role', 'lady', 'stranger', 3, 'Công tử dừng bước nơi đây, chẳng hay có điều chi muốn chỉ giáo?'),
('role', 'lady', 'acquaintance', 1, 'Phong thái ung dung lễ độ của công tử khiến tiểu nữ rất đỗi kính mến.'),
('role', 'lady', 'acquaintance', 2, 'Gấm vóc và hương liệu từ phương Nam chuyển về luôn được các phủ đệ săn đón.'),
('role', 'lady', 'acquaintance', 3, 'Mời công tử thưởng thức chén trà hoa cúc mới hái sáng nay cho thư thái tinh thần.'),
('role', 'lady', 'trusted', 1, 'Giữa chốn phong trần gặp được người tri kỷ ấm áp, lòng thiếp cảm kích vô cùng.'),
('role', 'lady', 'trusted', 2, 'Mỗi khi công tử lên đường viễn hành, thiếp luôn cầu chúc bình an thuận lợi.'),
('role', 'lady', 'trusted', 3, 'Chiếc túi hương này xin gửi tặng người để giữ tâm thần thanh tịnh suốt dặm dài.');

-- ============================================================
-- 12. VAI TRÒ: LÃO NHÂN / TRƯỞNG LÃO (elder)
-- ============================================================
INSERT INTO dg_dialogue_line (target_kind, target_code, tier_key, seq_order, line_vi) VALUES
('role', 'elder', 'stranger', 1, 'Lão sống qua bao mùa loạn lạc, nhìn ánh mắt cũng biết ai là người chính trực.'),
('role', 'elder', 'stranger', 2, 'Cây có cội, nước có nguồn; làm việc lớn trước hết phải giữ chữ đức làm đầu.'),
('role', 'elder', 'stranger', 3, 'Người trẻ tuổi đi đứng vội vàng, hãy ngồi xuống nghe lão kể chuyện xưa của thành.'),
('role', 'elder', 'acquaintance', 1, 'Hiền chất biết kính trên nhường dưới, tương lai ắt sẽ làm nên nghiệp lớn.'),
('role', 'elder', 'acquaintance', 2, 'Thành trì này từng trải qua nhiều phen hưng phế, mỗi con phố đều có tích xưa.'),
('role', 'elder', 'acquaintance', 3, 'Tích đức cho đời sau còn bền vững hơn tích vàng tích bạc trong kho.'),
('role', 'elder', 'trusted', 1, 'Hiền chất đối với bậc già cả thật chu đáo, lão coi cháu chẳng khác người một nhà.'),
('role', 'elder', 'trusted', 2, 'Bí quyết giữ lương thực và xem thời tiết của tiền nhân, lão xin truyền cho cháu.'),
('role', 'elder', 'trusted', 3, 'Trời cao không phụ người có tâm, cứ vững bước trên con đường cháu đã chọn!');
