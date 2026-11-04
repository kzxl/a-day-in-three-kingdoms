-- Migration 0402: Masterpiece tiers and legendary recipes

-- Expand pf_equipment quality_rank to include LEGENDARY
CREATE TABLE pf_equipment_new (
    id               INTEGER PRIMARY KEY,
    name_vi          TEXT    NOT NULL,
    item_id          INTEGER NOT NULL,
    citadel_id       INTEGER NOT NULL,
    durability       INTEGER NOT NULL CHECK (durability >= 0),
    max_durability   INTEGER NOT NULL CHECK (max_durability > 0),
    quality_rank     TEXT    NOT NULL CHECK (quality_rank IN ('COMMON', 'FINE', 'MASTER', 'LEGENDARY')),
    bonus_stat       TEXT    NOT NULL,
    bonus_value      INTEGER NOT NULL DEFAULT 0,
    CHECK (durability <= max_durability)
);

INSERT INTO pf_equipment_new SELECT * FROM pf_equipment;
DROP TABLE pf_equipment;
ALTER TABLE pf_equipment_new RENAME TO pf_equipment;
CREATE INDEX IF NOT EXISTS ix_pf_equipment_citadel ON pf_equipment(citadel_id);

-- Add 5th trade: Dược Sư (Herbalist / Alchemist)
INSERT OR IGNORE INTO pf_trade (id, code, name_vi, name_en, output_item_id, days_per_batch, base_quantity, tool_fee) VALUES
    (5, 'duoc_su', 'Dược Sư', 'herbalist', 7, 3, 2, 8);

-- Seed new advanced Masterpiece recipes
INSERT OR IGNORE INTO pf_recipe (id, trade_id, name_vi, required_tier, material_item_id, material_quantity, secondary_item_id, secondary_quantity, output_item_id, output_quantity, craft_days, craft_fee) VALUES
    (6,  4, 'Rèn Thanh Long Yển Nguyệt Đao', 3, 5, 6, 6, 2, 5, 1, 5, 120),
    (7,  4, 'Rèn Bát Xà Mâu',               3, 5, 5, 4, 2, 5, 1, 5, 100),
    (8,  3, 'Chế Tác Thần Tý Cung',         2, 6, 4, 5, 2, 5, 1, 4, 80),
    (9,  5, 'Bào Chế Bách Thảo Cao',        1, 7, 3, 13, 1, 7, 2, 3, 60),
    (10, 5, 'Luyện Chế Kim Sáng Dược',      2, 7, 4, 10, 2, 7, 2, 4, 90),
    (11, 5, 'Luyện Cửu Chuyển Hoàn Đan',    4, 13, 3, 14, 2, 7, 1, 7, 250);
