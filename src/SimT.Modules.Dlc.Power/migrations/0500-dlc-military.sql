-- dlc.military — Martial arena, bandit expeditions, and military commissary requisitions.

CREATE TABLE IF NOT EXISTS ml_stronghold (
    id                INTEGER PRIMARY KEY,
    citadel_id        INTEGER NOT NULL,
    name_vi           TEXT    NOT NULL,
    bandit_faction    TEXT    NOT NULL CHECK (bandit_faction IN ('khan_vang', 'hac_son', 'bach_ba', 'tho_phi')),
    danger_level      INTEGER NOT NULL CHECK (danger_level >= 1 AND danger_level <= 10),
    loot_gold         INTEGER NOT NULL CHECK (loot_gold >= 0),
    item_reward_code  TEXT    NOT NULL,
    cleared_count     INTEGER NOT NULL DEFAULT 0 CHECK (cleared_count >= 0),
    last_cleared_date TEXT
);

CREATE INDEX IF NOT EXISTS ix_ml_stronghold_citadel ON ml_stronghold(citadel_id);

CREATE TABLE IF NOT EXISTS ml_duel_log (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    date             TEXT    NOT NULL,
    challenger_name  TEXT    NOT NULL,
    opponent_npc_id  INTEGER NOT NULL,
    opponent_name    TEXT    NOT NULL,
    citadel_id       INTEGER NOT NULL,
    winner           TEXT    NOT NULL CHECK (winner IN ('player', 'npc', 'draw')),
    rounds           INTEGER NOT NULL CHECK (rounds >= 1),
    log_details      TEXT    NOT NULL,
    reward_gold      INTEGER NOT NULL DEFAULT 0 CHECK (reward_gold >= 0)
);

CREATE INDEX IF NOT EXISTS ix_ml_duel_log_date ON ml_duel_log(date);

CREATE TABLE IF NOT EXISTS ml_supply_requisition (
    id                INTEGER PRIMARY KEY,
    citadel_id        INTEGER NOT NULL,
    target_item_code  TEXT    NOT NULL,
    target_item_name  TEXT    NOT NULL,
    quantity_needed   INTEGER NOT NULL CHECK (quantity_needed > 0),
    reward_per_unit   INTEGER NOT NULL CHECK (reward_per_unit > 0),
    deadline_date     TEXT    NOT NULL,
    fulfilled         INTEGER NOT NULL DEFAULT 0 CHECK (fulfilled IN (0, 1))
);

CREATE INDEX IF NOT EXISTS ix_ml_requisition_citadel ON ml_supply_requisition(citadel_id);

-- Initial bandit strongholds surrounding the 15 citadels
INSERT OR IGNORE INTO ml_stronghold (id, citadel_id, name_vi, bandit_faction, danger_level, loot_gold, item_reward_code) VALUES
    (1,  1,  'Mang Sơn Sào Huyệt',         'hac_son',   4, 450, 'dong'),
    (2,  1,  'Lục Lâm Thảo Khấu',          'tho_phi',   2, 250, 'ruou'),
    (3,  2,  'Bạch Ba Tàn Doanh',          'bach_ba',   5, 550, 'sat'),
    (4,  2,  'Vị Thủy Thủy Phỉ',           'tho_phi',   3, 320, 'muoi'),
    (5,  3,  'Dĩnh Xuyên Khăn Vàng',       'khan_vang', 3, 350, 'gao'),
    (6,  4,  'Thái Hành Sơn Trại',         'hac_son',   6, 680, 'sat'),
    (7,  5,  'Thanh Thành Sơn Phỉ',        'tho_phi',   4, 420, 'tra'),
    (8,  6,  'Đông Hải Thủy Tặc',          'tho_phi',   5, 500, 'lua'),
    (9,  7,  'Kinh Sơn Man Trại',          'tho_phi',   4, 400, 'sam'),
    (10, 8,  'Hải Lăng Thổ Phỉ',           'tho_phi',   3, 320, 'vai'),
    (11, 9,  'Tần Lĩnh Sào Huyệt',         'tho_phi',   4, 380, 'go'),
    (12, 10, 'Liêu Đông Mã Tặc',           'tho_phi',   6, 650, 'da'),
    (13, 11, 'Thái Hồ Thủy Tặc',           'tho_phi',   4, 450, 'lua'),
    (14, 12, 'Bành Trạch Thủy Khấu',       'tho_phi',   5, 520, 'dong'),
    (15, 13, 'Hoài Hà Lộ Phỉ',             'khan_vang', 3, 340, 'gao'),
    (16, 14, 'Mạc Phụ Sơn Trại',           'tho_phi',   4, 410, 'tra'),
    (17, 15, 'Tây Vực Mã Tặc',             'tho_phi',   7, 750, 'da');

-- Initial military supply requisitions
INSERT OR IGNORE INTO ml_supply_requisition (id, citadel_id, target_item_code, target_item_name, quantity_needed, reward_per_unit, deadline_date) VALUES
    (1,  1,  'gao',  'Lương Thảo Tiếp Tế',    10, 20, '185-06-01'),
    (2,  1,  'sat',  'Thiết Liệu Quân Giới',   5, 60, '185-06-01'),
    (3,  2,  'dong', 'Đồng Thỏi Đúc Binh Khí', 5, 80, '185-06-01'),
    (4,  3,  'gao',  'Lương Thảo Cung Ứng',   15, 22, '185-06-01'),
    (5,  4,  'sat',  'Quân Giới Trọng Thiết', 10, 65, '185-06-01'),
    (6,  6,  'lua',  'Chiến Bào Kỵ Binh',      4, 130, '185-06-01');
