-- base.content owns the item catalog. Master data only; no player state here.

CREATE TABLE IF NOT EXISTS ct_item (
    id               INTEGER PRIMARY KEY,
    code             TEXT    NOT NULL UNIQUE,
    name_vi          TEXT    NOT NULL,
    name_en          TEXT    NOT NULL,
    category         TEXT    NOT NULL,
    base_price       INTEGER NOT NULL CHECK (base_price > 0),
    shelf_life_days  INTEGER NOT NULL DEFAULT 0,
    weight           INTEGER NOT NULL DEFAULT 1
);

INSERT OR IGNORE INTO ct_item (id, code, name_vi, name_en, category, base_price, shelf_life_days, weight) VALUES
    (1, 'gao',     'Gạo',      'rice',      'grain',   12,  60, 10),
    (2, 'muoi',    'Muối',     'salt',      'grain',    8, 180, 10),
    (3, 'tra',     'Trà',      'tea',       'luxury',  35, 120,  2),
    (4, 'lua',     'Lụa',      'silk',      'luxury',  90,   0,  3),
    (5, 'sat',     'Sắt',      'iron',      'material', 60,  0, 12),
    (6, 'go',      'Gỗ',       'timber',    'material', 18,   0, 15),
    (7, 'thuoc',   'Thuốc',    'medicine',  'medicine',120,  90,  1),
    (8, 'ca_kho',  'Cá khô',   'dried fish','food',    22,  45,  4);
