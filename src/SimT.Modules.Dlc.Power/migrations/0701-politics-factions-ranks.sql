-- 0701-politics-factions-ranks.sql: Expands Three Kingdoms warlords factions, 9-rank officialdom, and regional civil governance.

-- 1. Faction table: 12 historical warlords
CREATE TABLE IF NOT EXISTS po_faction (
    id                  INTEGER PRIMARY KEY,
    code                TEXT    NOT NULL UNIQUE,
    name_vi             TEXT    NOT NULL,
    leader_npc_id       INTEGER NOT NULL,
    capital_city_id     INTEGER NOT NULL,
    ideal_vi            TEXT    NOT NULL,
    troop_count         INTEGER NOT NULL CHECK (troop_count >= 0),
    treasury_copper     INTEGER NOT NULL CHECK (treasury_copper >= 0),
    color_hex           TEXT    NOT NULL DEFAULT '#3b82f6'
);

-- 2. City Governance table: security, unrest and defensive stability of 15 citadels
CREATE TABLE IF NOT EXISTS po_city_governance (
    citadel_id          INTEGER PRIMARY KEY,
    faction_id          INTEGER NOT NULL,
    governor_npc_id     INTEGER NOT NULL,
    unrest_level        INTEGER NOT NULL CHECK (unrest_level >= 0 AND unrest_level <= 100),
    defense_strength    INTEGER NOT NULL CHECK (defense_strength >= 0),
    tax_rate_percent    INTEGER NOT NULL CHECK (tax_rate_percent >= 0 AND tax_rate_percent <= 50)
);

-- 3. Civil decrees & policy audit log
CREATE TABLE IF NOT EXISTS po_decree_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    day_index       INTEGER NOT NULL,
    citadel_id      INTEGER NOT NULL,
    policy_code     TEXT    NOT NULL,
    cost_copper     INTEGER NOT NULL,
    reputation_gain INTEGER NOT NULL,
    unrest_delta    INTEGER NOT NULL,
    narrative_vi    TEXT    NOT NULL
);

-- Expand po_office to 9 ranks (Cửu phẩm quan chế)
INSERT OR IGNORE INTO po_office (id, code, name_vi, name_en, rank, monthly_stipend, reputation_required) VALUES
    (4, 'biet_gia',   'Biệt Giá Tòng Sự',     'provincial aide',               4,  500,  40),
    (5, 'thu_su',     'Thứ Sử',               'inspector general',             5,  800,  65),
    (6, 'tran_quan',   'Trấn Quân Tướng Quân', 'garrison general',              6, 1200,  95),
    (7, 'chau_muc',   'Châu Mục',             'provincial governor',           7, 1800, 130),
    (8, 'tam_cong',   'Tam Công (Tư Đồ)',     'minister of the masses',        8, 2600, 175),
    (9, 'thua_tuong', 'Thừa Tướng',           'grand chancellor',              9, 3500, 230);

-- State key for player allegiance faction
INSERT OR IGNORE INTO po_state (key, value) VALUES ('allegiance_faction_id', 0);

-- Seed 12 Historical Warlord Factions
INSERT OR IGNORE INTO po_faction (id, code, name_vi, leader_npc_id, capital_city_id, ideal_vi, troop_count, treasury_copper, color_hex) VALUES
    (1,  'cao_wei',       'Tào Ngụy',          21,  7, 'Thao lược bá đạo, thống nhất thiên hạ', 80000, 35000, '#ef4444'),
    (2,  'shu_han',       'Thục Hán',           3,  4, 'Nhân nghĩa vi bản, phục hưng Hán thất', 35000, 12000, '#22c55e'),
    (3,  'dong_wu',       'Đông Ngô',          13, 13, 'Hiểm trở Trường Giang, bảo cảnh an dân', 60000, 28000, '#3b82f6'),
    (4,  'yuan_shao',     'Viên Thiệu',        17,  3, 'Tứ thế tam công, uy chấn Hà Bắc',       75000, 30000, '#eab308'),
    (5,  'yuan_shu',      'Viên Thuật',        58,  9, 'Xưng đế Trọng Gia, hào hoa phú quý',     40000, 20000, '#a855f7'),
    (6,  'dong_zhuo',     'Đổng Trác',         22,  2, 'Kiêu dũng bạo liệt, khuynh đảo triều đình', 70000, 45000, '#71717a'),
    (7,  'lu_bu',         'Lữ Bố',             56,  9, 'Chiến thần vô song, thiên hạ duy ngã',   25000,  8000, '#f97316'),
    (8,  'liu_biao',      'Lưu Biểu',          15, 10, 'Bát tuấn danh sĩ, thủ hộ Kinh Tương',    50000, 22000, '#14b8a6'),
    (9,  'liu_zhang',     'Lưu Chương',        20, 15, 'Thiên phủ chi quốc, an hưởng thái bình', 45000, 26000, '#84cc16'),
    (10, 'zhang_lu',      'Trương Lỗ',         19, 14, 'Ngũ Đấu Mễ Đạo, chính giáo hợp nhất',    20000, 10000, '#6366f1'),
    (11, 'gongsun_zan',   'Công Tôn Toản',     59,  8, 'Bạch Mã Nghĩa Tòng, bình định biên cương', 30000, 14000, '#06b6d4'),
    (12, 'ma_chao',       'Tây Lương Kiêu Kỵ', 28,  2, 'Thần Uy Thiên Tướng, kỵ binh bách chiến',  32000,  9000, '#d97706');

-- Seed 15 Citadels Governance
INSERT OR IGNORE INTO po_city_governance (citadel_id, faction_id, governor_npc_id, unrest_level, defense_strength, tax_rate_percent) VALUES
    (1,   1, 21, 25, 85, 15),
    (2,   6, 22, 30, 90, 20),
    (3,   4, 17, 15, 80, 12),
    (4,   2,  3, 10, 60, 10),
    (5,   4, 17, 20, 65, 14),
    (6,   2,  3, 12, 55, 10),
    (7,   1, 21, 15, 85, 15),
    (8,  11, 59, 18, 70, 12),
    (9,   7, 56, 28, 75, 18),
    (10,  8, 15, 12, 75, 12),
    (11,  3, 13, 14, 80, 12),
    (12,  8, 15, 16, 70, 14),
    (13,  3, 13, 10, 85, 12),
    (14, 10, 19, 12, 75, 10),
    (15,  9, 20, 10, 80, 10);
