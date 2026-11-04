-- 0105-life-skills.sql — RPG Life skills for crafting, cooking, gathering, fishing, and hunting.

INSERT OR IGNORE INTO np_skill (npc_id, code, name_vi, category, tier, description, effect_summary) VALUES
    -- NPC 2: Chưởng Quỹ Trường An Tửu Lầu (Tửu Quán / Ẩm thực)
    (2, 'am_thuc', 'Ẩm Thực', 'special', 3, 'Kỹ nghệ mỹ vị tửu quán, kết hợp ngũ vị bồi bổ khí huyết giải trừ mệt mỏi.', 'Tăng hiệu quả hồi phục của món ăn và mở rộng công thức'),
    -- NPC 8: Lư Chi Vinh (Mộc Nghệ / Tầm Dược)
    (8, 'tam_duoc', 'Tầm Dược', 'special', 3, 'Am hiểu cây cỏ hoa lá vùng hoang dã, nhận biết thảo mộc và dược liệu quý.', 'Tăng sản lượng và độ hiếm thảo dược khi hái thuốc dã ngoại'),
    -- NPC 16: Chu Du (Đại Đô Đốc Thủy Quân / Điếu Ngư)
    (16, 'dieu_ngu', 'Điếu Ngư', 'special', 3, 'Kỹ nghệ buông cần sông hồ Trường Giang, kiên nhẫn đợi cá lớn cắn câu.', 'Tăng tỉ lệ câu được cá quý và cơ duyên vớt cổ vật'),
    -- NPC 13: Tôn Kiên (Giang Đông Mãnh Hổ / Thú Liệp)
    (13, 'thu_liep', 'Thú Liệp', 'special', 4, 'Bậc thầy bắn cung săn thú, theo dấu mãnh thú trên thảo nguyên và rừng rậm.', 'Tăng sản lượng thịt da thú và khả năng đả bại mãnh thú'),
    -- NPC 61: Hoa Đà (Thần Y / Luyện Đan)
    (61, 'luyen_dan', 'Luyện Đan', 'special', 5, 'Thuật luyện đan diệu thủ hồi xuân, điều hòa âm dương cứu mạng người trong gang tấc.', 'Gia tăng hiệu quả dược lý đan dược và tỉ lệ đan thành');
