-- Migration 0405: Deep RPG Life Skills (Cooking, Wilderness Foraging, Fishing, Blacksmithing Tempering)

ALTER TABLE pf_equipment ADD COLUMN temper_level INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS pf_dish (
    id                INTEGER PRIMARY KEY,
    code              TEXT    NOT NULL UNIQUE,
    name_vi           TEXT    NOT NULL,
    description_vi    TEXT    NOT NULL,
    primary_item_id   INTEGER NOT NULL REFERENCES ct_item(id),
    primary_qty       INTEGER NOT NULL CHECK (primary_qty > 0),
    secondary_item_id INTEGER DEFAULT NULL REFERENCES ct_item(id),
    secondary_qty     INTEGER DEFAULT 0,
    fatigue_relief    INTEGER NOT NULL DEFAULT 0,
    stress_relief     INTEGER NOT NULL DEFAULT 0,
    health_gain       INTEGER NOT NULL DEFAULT 0,
    cook_fee          INTEGER NOT NULL DEFAULT 5
);

CREATE TABLE IF NOT EXISTS pf_dish_inventory (
    dish_id           INTEGER PRIMARY KEY REFERENCES pf_dish(id),
    quantity          INTEGER NOT NULL DEFAULT 0 CHECK (quantity >= 0)
);

INSERT OR IGNORE INTO pf_dish (id, code, name_vi, description_vi, primary_item_id, primary_qty, secondary_item_id, secondary_qty, fatigue_relief, stress_relief, health_gain, cook_fee) VALUES
    (1, 'com_nam',        'Cơm Nắm Hành Quân',     'Cơm nắm rắc muối biển, lương khô tiện lợi cho quân sĩ hành quân đường xa.', 1, 2, 2, 1, 15, 5, 0, 5),
    (2, 'canh_ca',        'Canh Cá Hoa Tiêu',      'Cá khô ninh nhừ cùng muối biển, giải trừ mỏi mệt sau ngày dài lao tác.', 8, 2, 2, 1, 20, 10, 5, 8),
    (3, 'banh_man',       'Bánh Bao Bột Mì',       'Bánh bao hấp nóng hổi từ bột mì, chắc dạ ấm lòng.', 9, 2, 2, 1, 15, 5, 2, 6),
    (4, 'tra_dieu_tam',   'Danh Trà Điều Tâm',     'Trà danh tiếng pha rượu nếp hâm nóng, thư thái thần trí khai thông kinh mạch.', 3, 1, 10, 1, 10, 25, 0, 10),
    (5, 'duoc_thien',     'Bát Bảo Dược Thiện',    'Dược liệu quý hầm cá khô, bồi bổ khí huyết tăng cường sinh lực.', 7, 1, 8, 1, 35, 20, 15, 20),
    (6, 'sam_quy_thang',  'Nhân Sâm Đại Bổ Thang', 'Nhân sâm nghìn năm sắc cùng rượu nếp, đại bổ nguyên khí.', 13, 1, 10, 1, 50, 40, 30, 40);
