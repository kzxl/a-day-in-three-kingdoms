-- Migration 0302: Freight Contracts & Caravan Escorts

ALTER TABLE cv_run ADD COLUMN escort_npc_id INTEGER DEFAULT NULL;
ALTER TABLE cv_run ADD COLUMN ambush_loss INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS cv_contract (
    id               INTEGER PRIMARY KEY,
    guild_name       TEXT    NOT NULL,
    from_city        INTEGER NOT NULL,
    to_city          INTEGER NOT NULL,
    item_id          INTEGER NOT NULL,
    quantity         INTEGER NOT NULL CHECK (quantity > 0),
    reward_copper    INTEGER NOT NULL CHECK (reward_copper > 0),
    deadline_day     INTEGER NOT NULL,
    status           TEXT    NOT NULL CHECK (status IN ('AVAILABLE', 'IN_TRANSIT', 'DELIVERED', 'EXPIRED')),
    accepted_day     INTEGER,
    completed_day    INTEGER
);

CREATE INDEX IF NOT EXISTS ix_cv_contract_status ON cv_contract(status, from_city, to_city);

-- Seed baseline freight contracts across major trading routes
-- Campaign starts at 184-05-02 (DayIndex = 66001)
INSERT INTO cv_contract (id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status)
VALUES
    (1, 'Lạc Dương Thương Hội', 1, 2, 2, 8, 450, 66020, 'AVAILABLE'),     -- Lạc Dương -> Trường An (Muối, 20 ngày)
    (2, 'Trường An Tiêu Cục', 2, 1, 3, 10, 380, 66025, 'AVAILABLE'),     -- Trường An -> Lạc Dương (Trà, 25 ngày)
    (3, 'Lạc Dương Tiêu Cục', 1, 5, 1, 15, 520, 66025, 'AVAILABLE'),     -- Lạc Dương -> Hứa Xương (Gạo, 25 ngày)
    (4, 'Kinh Châu Trà Thương', 8, 9, 3, 12, 480, 66030, 'AVAILABLE'),    -- Tương Dương -> Giang Lăng (Trà, 30 ngày)
    (5, 'Hứa Xương Lương Thương', 5, 4, 1, 20, 650, 66035, 'AVAILABLE'),  -- Hứa Xương -> Nghiệp Thành (Gạo, 35 ngày)
    (6, 'Từ Châu Tiêu Cục', 6, 11, 4, 8, 700, 66040, 'AVAILABLE'),       -- Hạ Bì -> Kiến Nghiệp (Lụa, 40 ngày)
    (7, 'Tây Xuyên Mã Đội', 14, 13, 7, 6, 580, 66045, 'AVAILABLE'),      -- Thành Đô -> Hán Trung (Dược Liệu, 45 ngày)
    (8, 'U Châu Thương Đoàn', 7, 3, 2, 15, 460, 66050, 'AVAILABLE');      -- Kế Huyện -> Nam Bì (Muối, 50 ngày)
