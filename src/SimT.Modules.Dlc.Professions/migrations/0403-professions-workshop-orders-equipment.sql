-- Migration 0403: Workshop, Crafting Orders, and Player Equipment Slots

-- 1. Player Equipped Slots (WEAPON, ARMOR, TOOL)
CREATE TABLE IF NOT EXISTS pf_equipped_slot (
    slot_code       TEXT PRIMARY KEY,
    equipment_id    INTEGER REFERENCES pf_equipment(id) ON DELETE SET NULL,
    equipped_at_day INTEGER NOT NULL
);

-- Seed default initial empty equipped slots
INSERT OR IGNORE INTO pf_equipped_slot (slot_code, equipment_id, equipped_at_day) VALUES
    ('WEAPON', NULL, 1),
    ('ARMOR', NULL, 1),
    ('TOOL', NULL, 1);

-- 2. Player Workshops in citadels
CREATE TABLE IF NOT EXISTS pf_workshop (
    id                INTEGER PRIMARY KEY,
    citadel_id        INTEGER NOT NULL UNIQUE,
    trade_id          INTEGER NOT NULL REFERENCES pf_trade(id),
    name_vi           TEXT NOT NULL,
    monthly_rent      INTEGER NOT NULL CHECK (monthly_rent >= 0),
    furnace_level     INTEGER NOT NULL DEFAULT 1 CHECK (furnace_level BETWEEN 1 AND 5),
    hired_artisans    INTEGER NOT NULL DEFAULT 0 CHECK (hired_artisans >= 0),
    is_active         INTEGER NOT NULL DEFAULT 1,
    created_day_index INTEGER NOT NULL
);

-- 3. Crafting commissions & orders
CREATE TABLE IF NOT EXISTS pf_order (
    id                INTEGER PRIMARY KEY,
    client_name_vi    TEXT NOT NULL,
    client_type       TEXT NOT NULL,
    citadel_id        INTEGER NOT NULL,
    target_item_id    INTEGER NOT NULL,
    quantity_needed   INTEGER NOT NULL CHECK (quantity_needed > 0),
    reward_copper     INTEGER NOT NULL CHECK (reward_copper > 0),
    fame_reward       INTEGER NOT NULL DEFAULT 5,
    deadline_day      INTEGER NOT NULL,
    status            TEXT NOT NULL DEFAULT 'AVAILABLE',
    accepted_day      INTEGER
);

-- Seed initial orders at major citadels
INSERT OR IGNORE INTO pf_order (id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status) VALUES
    (1, 'Quân Đồn Trú Lạc Dương', 'MILITARY', 1, 5, 10, 350, 15, 60, 'AVAILABLE'),
    (2, 'Thương Hội Nam Dương',   'COMMERCE', 1, 6, 8,  280, 10, 45, 'AVAILABLE'),
    (3, 'Thái Y Viện Hứa Xương',  'COURT',    2, 7, 6,  400, 20, 50, 'AVAILABLE'),
    (4, 'Đồn Điền Sứ Lạc Dương',  'NOBLE',    1, 1, 15, 180, 8,  40, 'AVAILABLE'),
    (5, 'Thủy Quân Giang Đông',   'MILITARY', 3, 8, 5,  500, 25, 70, 'AVAILABLE');

-- 4. Guild fame tracking
CREATE TABLE IF NOT EXISTS pf_guild_fame (
    id         INTEGER PRIMARY KEY,
    total_fame INTEGER NOT NULL DEFAULT 0
);

INSERT OR IGNORE INTO pf_guild_fame (id, total_fame) VALUES (1, 10);
