-- 0311-espionage-and-hermit-tomes.sql: Covert spy network cells (Te Tac) and 5 Legendary Hermit Tomes (Ky Thu Danh Si)

CREATE TABLE IF NOT EXISTS so_spy_network (
    citadel_id INTEGER PRIMARY KEY,
    intel_level INTEGER NOT NULL DEFAULT 0,
    infiltrated_day_index INTEGER NOT NULL DEFAULT 0,
    active_operation TEXT NOT NULL DEFAULT 'none',
    sabotage_count INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS so_hermit_tome (
    code TEXT PRIMARY KEY,
    name_vi TEXT NOT NULL,
    hermit_name_vi TEXT NOT NULL,
    citadel_id INTEGER NOT NULL,
    stat_code TEXT NOT NULL,
    stat_bonus INTEGER NOT NULL,
    skill_code TEXT NOT NULL,
    is_acquired INTEGER NOT NULL DEFAULT 0,
    acquired_day_index INTEGER NOT NULL DEFAULT 0
);

INSERT OR IGNORE INTO so_hermit_tome (code, name_vi, hermit_name_vi, citadel_id, stat_code, stat_bonus, skill_code, is_acquired, acquired_day_index) VALUES
('thai_binh_yeu_thuat', 'Thái Bình Yêu Thuật (Nam Hoa Lão Tiên)', 'Nam Hoa Lão Tiên', 5,  'INT', 6, 'strategy_plotting', 0, 0),
('thanh_nang_thu',      'Thanh Nang Thư (Thần Y Hoa Đà)',          'Hoa Đà Tiên Sinh', 9,  'INT', 5, 'craft_alchemy',     0, 0),
('ton_tu_binh_phap',    'Tôn Tử Binh Pháp Thập Tam Thiên',         'Ẩn Sĩ Giang Đông', 13, 'LDR', 6, 'martial_tactics',   0, 0),
('don_giap_thien_thu',  'Độn Giáp Thiên Thư (Tả Từ Đạo Nhân)',     'Tả Từ Chân Nhân',  15, 'INT', 7, 'strategy_debating', 0, 0),
('dao_chu_thuong_huan', 'Đào Chu Công Thương Huấn Bí Điển',        'Hậu Duệ Phạm Lãi', 7,  'COM', 6, 'commerce_appraisal',0, 0);
