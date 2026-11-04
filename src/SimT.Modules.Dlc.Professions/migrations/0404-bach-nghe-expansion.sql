-- Migration 0404: Bách Nghệ Grand Expansion (16 Trades across 4 Sectors, Masterpiece Recipes, Guild Perks)

-- 1. Add sector category to pf_trade
ALTER TABLE pf_trade ADD COLUMN sector TEXT NOT NULL DEFAULT 'CRAFT';

UPDATE pf_trade SET sector = 'AGRI' WHERE code IN ('nong_phu', 'ngu_dan');
UPDATE pf_trade SET sector = 'ALCH' WHERE code = 'duoc_su';
UPDATE pf_trade SET sector = 'CRAFT' WHERE code IN ('tho_moc', 'luyen_kim');

-- 2. Expand trades to 16 canonical professions covering all 16 historical trade goods
INSERT OR IGNORE INTO pf_trade (id, code, name_vi, name_en, output_item_id, days_per_batch, base_quantity, tool_fee, sector) VALUES
    (6,  'canh_nong',    'Canh Nông',      'wheat farmer', 9,  2, 5, 2,  'AGRI'),
    (7,  'nau_ruou',     'Nấu Rượu',       'brewer',       10, 3, 3, 8,  'ALCH'),
    (8,  'tho_det',      'Thợ Dệt Vải',    'weaver',       11, 3, 3, 5,  'TEXTILE'),
    (9,  'det_gam',      'Thợ Dệt Gấm',    'brocade maker',12, 5, 1, 40, 'TEXTILE'),
    (10, 'hai_tra',      'Trà Nông',       'tea plucker',  3,  2, 3, 6,  'AGRI'),
    (11, 'lam_muoi',     'Diêm Dân',       'salt maker',   2,  2, 4, 3,  'AGRI'),
    (12, 'tam_tang',     'Tằm Tang',       'sericulturist',4,  4, 2, 25, 'AGRI'),
    (13, 'hai_thuoc',    'Thái Dược',      'ginseng hunter',13,5, 1, 50, 'AGRI'),
    (14, 'tam_tram',     'Tầm Trầm',       'agarwood woodsman', 14, 5, 1, 45, 'AGRI'),
    (15, 'khai_khoang',  'Khai Khoáng',    'coal miner',   15, 2, 4, 4,  'CRAFT'),
    (16, 'duc_dong',     'Đúc Đồng',       'bronze smith', 16, 3, 2, 12, 'CRAFT');

-- 3. Expanded legendary recipes (weapons, armor, tools, and elixir)
INSERT OR IGNORE INTO pf_recipe (id, trade_id, name_vi, required_tier, material_item_id, material_quantity, secondary_item_id, secondary_quantity, output_item_id, output_quantity, craft_days, craft_fee) VALUES
    (12, 4,  'Rèn Phương Thiên Họa Kích',     4, 5,  8, 16, 4, 5,  1, 7, 300),
    (13, 3,  'Chế Tác Gia Cát Liên Nỗ',       3, 6,  6, 5,  3, 5,  1, 5, 160),
    (14, 4,  'Rèn Minh Quang Hoàng Kim Giáp', 4, 5,  6, 16, 4, 5,  1, 6, 280),
    (15, 9,  'May Thục Cẩm Bách Hoa Bào',     3, 12, 4, 4,  2, 12, 1, 4, 150),
    (16, 5,  'Luyện Cửu Chuyển Hồi Thiên Hoàn',4, 13, 3, 14, 2, 7,  2, 5, 220),
    (17, 7,  'Ủ Đỗ Khang Thần Tiên Mỹ Tửu',   2, 10, 4, 1,  4, 10, 2, 3, 80),
    (18, 16, 'Đúc Thần Bàn Tính Đồng Thau',   2, 16, 4, 6,  2, 16, 1, 3, 70),
    (19, 3,  'Chế Tác Cân Kim Lượng Tử Viện', 1, 6,  3, 16, 1, 6,  1, 2, 40),
    (20, 4,  'Rèn Đoản Thê Thiết Giáp',       2, 5,  4, 11, 2, 5,  1, 3, 80);

-- 4. Expanded guild crafting orders
INSERT OR IGNORE INTO pf_order (id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status) VALUES
    (6,  'Kỵ Binh Hổ Báo Kỵ',         'MILITARY', 2, 5,  12, 420, 18, 55, 'AVAILABLE'),
    (7,  'Bắc Cung Thị Vệ',           'COURT',    1, 16, 10, 500, 20, 60, 'AVAILABLE'),
    (8,  'Huyện Lệnh Thục Quận',      'NOBLE',    3, 12, 3,  600, 25, 45, 'AVAILABLE'),
    (9,  'Hương Dược Quán Lạc Dương', 'COMMERCE', 1, 13, 4,  750, 30, 65, 'AVAILABLE'),
    (10, 'Trà Điếm Tiết Độ Sứ',       'COMMERCE', 2, 3,  12, 360, 15, 40, 'AVAILABLE');

-- 5. Craftsman Guild Mastery Perks
CREATE TABLE IF NOT EXISTS pf_mastery_perk (
    id             INTEGER PRIMARY KEY,
    code           TEXT NOT NULL UNIQUE,
    name_vi        TEXT NOT NULL,
    description_vi TEXT NOT NULL,
    required_fame  INTEGER NOT NULL,
    is_unlocked    INTEGER NOT NULL DEFAULT 0
);

INSERT OR IGNORE INTO pf_mastery_perk (id, code, name_vi, description_vi, required_fame, is_unlocked) VALUES
    (1, 'xao_doat_thien_cong', 'Xảo Đoạt Thiên Công', 'Tay nghề xuất thần, tăng +1 sản lượng cho tất cả nghề lao tác cơ bản.', 15, 0),
    (2, 'thiet_cot_bach_luyen', 'Thiết Cốt Bách Luyện', 'Thép tôi trăm lần, khí cụ rèn đúc ra được cộng thêm +25 độ bền tối đa.', 35, 0),
    (3, 'nhat_tam_nhi_dung',   'Nhất Tâm Nhị Dụng',   'Làm việc thuần thục, các lượt lao tác và rèn đúc giảm 1 ngày thời gian.', 60, 0),
    (4, 'dan_dao_thong_than',   'Đan Đạo Thông Thần',   'Thuốc men tinh túy, tăng tỷ lệ luyện ra trang bị và đan dược Thần Phẩm.', 100, 0),
    (5, 'thuong_than_dieu_toan','Thương Thần Diệu Toán','Danh vọng vang xa, nhận thêm +25% thù lao tiền đồng khi hoàn thành đơn đặt hàng.', 150, 0);
