-- Migration 0401: Equipment Crafting, Recipes & Durability

CREATE TABLE IF NOT EXISTS pf_recipe (
    id                INTEGER PRIMARY KEY,
    trade_id          INTEGER NOT NULL REFERENCES pf_trade(id),
    name_vi           TEXT    NOT NULL,
    required_tier     INTEGER NOT NULL DEFAULT 0,
    material_item_id  INTEGER NOT NULL,
    material_quantity INTEGER NOT NULL CHECK (material_quantity > 0),
    secondary_item_id INTEGER DEFAULT NULL,
    secondary_quantity INTEGER DEFAULT 0,
    output_item_id    INTEGER NOT NULL,
    output_quantity   INTEGER NOT NULL DEFAULT 1 CHECK (output_quantity > 0),
    craft_days        INTEGER NOT NULL CHECK (craft_days > 0),
    craft_fee         INTEGER NOT NULL CHECK (craft_fee >= 0)
);

CREATE TABLE IF NOT EXISTS pf_equipment (
    id               INTEGER PRIMARY KEY,
    name_vi          TEXT    NOT NULL,
    item_id          INTEGER NOT NULL,
    citadel_id       INTEGER NOT NULL,
    durability       INTEGER NOT NULL CHECK (durability >= 0),
    max_durability   INTEGER NOT NULL CHECK (max_durability > 0),
    quality_rank     TEXT    NOT NULL CHECK (quality_rank IN ('COMMON', 'FINE', 'MASTER')),
    bonus_stat       TEXT    NOT NULL,
    bonus_value      INTEGER NOT NULL DEFAULT 0,
    CHECK (durability <= max_durability)
);

CREATE INDEX IF NOT EXISTS ix_pf_equipment_citadel ON pf_equipment(citadel_id);

-- Seed recipes:
-- 1. Nông Cụ Gỗ (Thợ mộc + Gỗ -> Nông cụ)
-- 2. Thuyền Đánh Cá Gỗ (Thợ mộc + Gỗ + Sắt -> Thuyền cá)
-- 3. Đao Kiếm Bằng Sắt (Luyện kim + Sắt -> Binh đao)
-- 4. Thiết Giáp Thượng Hạng (Luyện kim + Sắt + Lụa -> Thiết giáp)
-- 5. Cỗ Xe Vận Tải (Thợ mộc + Gỗ + Sắt -> Xe kéo)
INSERT OR IGNORE INTO pf_recipe (id, trade_id, name_vi, required_tier, material_item_id, material_quantity, secondary_item_id, secondary_quantity, output_item_id, output_quantity, craft_days, craft_fee) VALUES
    (1, 3, 'Chế Tác Nông Cụ Gỗ',       0, 6, 2, NULL, 0, 1, 1, 2, 10),
    (2, 3, 'Đóng Thuyền Đánh Cá Gỗ',   1, 6, 4, 5, 1,    8, 2, 3, 25),
    (3, 4, 'Đúc Binh Đao Bằng Sắt',    0, 5, 2, NULL, 0, 5, 1, 3, 30),
    (4, 4, 'Rèn Thiết Giáp Thượng Hạng', 1, 5, 4, 4, 1, 5, 1, 4, 50),
    (5, 3, 'Chế Tạo Cỗ Xe Vận Tải',    2, 6, 5, 5, 2, 6, 1, 5, 60);

-- Seed baseline player starting equipment at citadel 1 (Lạc Dương)
INSERT OR IGNORE INTO pf_equipment (id, name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value) VALUES
    (1, 'Đao Sắt Thao Diễn', 5, 1, 100, 100, 'COMMON', 'WAR', 3),
    (2, 'Cuốc Sắt Khai Điền', 5, 1, 80, 100, 'COMMON', 'POL', 2),
    (3, 'Cân Tiểu Ly Gỗ', 6, 1, 95, 100, 'COMMON', 'COM', 2);
