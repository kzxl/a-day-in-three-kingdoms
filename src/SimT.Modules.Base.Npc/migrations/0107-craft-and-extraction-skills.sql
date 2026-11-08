-- 0107-craft-and-extraction-skills.sql — Support skills for master forging, weapon maintenance, vein divining, furnace mastery, jade appraisal, and exotic husbandry.

INSERT OR IGNORE INTO np_skill (npc_id, code, name_vi, category, tier, description, effect_summary, level) VALUES
    -- 1. Thần Chú Đoán Tạo (Master Forging)
    (25, 'master_forging',     'Thần Chú Đoán Tạo',   'special',  4, 'Bí pháp truyền thế của đại tông sư rèn đúc, lắng nghe tiếng vang kim thiết để điểm trúng huyệt mạch phôi thép.', 'Tăng tỷ lệ đúc ra Thần Binh/Cực Phẩm và cộng thêm độ bền tối đa (+3/cấp) cho vũ khí chế tác', 7),
    -- 2. Bảo Khí Dưỡng Hộ (Weapon Maintenance)
    (24, 'weapon_maintenance', 'Bảo Khí Dưỡng Hộ',    'special',  3, 'Pháp môn lau chùi, mài giũa và dưỡng hộ binh khí sau mỗi trận giao phong, giữ lưỡi đao bền bỉ qua trăm trận.', 'Giảm hao mòn độ bền trang bị khi chiến đấu/lao tác và giảm 5% chi phí tu sửa mỗi cấp', 6),
    -- 3. Tầm Long Điểm Khoáng (Vein Divining)
    (18, 'vein_divining',      'Tầm Long Điểm Khoáng', 'special',  4, 'Thuật phong thủy địa mạch dò tìm long mạch khoáng sơn, nhìn sắc đất đá biết dưới lòng núi ẩn giấu Huyền Thiết hay Ngọc Bích.', 'Tăng sản lượng đào mỏ và tăng mạnh cơ hội thu hoạch khoáng sản hiếm (Huyền Thiết, Ngọc Bích, Chu Sa)', 6),
    -- 4. Khống Hỏa Luyện Kim (Furnace Mastery)
    (16, 'furnace_mastery',    'Khống Hỏa Luyện Kim', 'special',  4, 'Tuyệt kỹ điều khiển hỏa hầu lò luyện bằng than đá và gió bễ, giúp kim loại nóng chảy đồng nhất không chút tạp chất.', 'Giảm 20% phí tôi luyện khí cụ, giảm yêu cầu thuộc tính khi Tôi Luyện và tăng +5 độ bền mỗi lần tôi luyện', 7),
    -- 5. Giám Bảo Tinh Nhãn (Jade Appraisal)
    (9,  'jade_appraisal',     'Giám Bảo Tinh Nhãn',  'commerce', 3, 'Đôi mắt tinh tường của đại thương gia thẩm định ngọc quý, bảo khí và kỳ vật thiên hạ, định giá chuẩn xác từng vết vân thép.', 'Tăng +15 Nhãn Lực (PRC) và tăng giá bán khí cụ rèn đúc thêm +5% mỗi cấp', 6),
    -- 6. Thiên Tằm Dưỡng Thực (Exotic Husbandry)
    (7,  'exotic_husbandry',   'Thiên Tằm Dưỡng Thực','special',  3, 'Bí thuật nuôi tằm thiên sơn, thuần dưỡng thú quý và thu hái kỳ trân dị thảo nơi rừng sâu nước độc.', 'Tăng sản lượng Tầm Dược / Săn Bắn và thu hoạch thêm Tơ Thiên Tằm, Gân Mãnh Hổ, Minh Châu', 5);
