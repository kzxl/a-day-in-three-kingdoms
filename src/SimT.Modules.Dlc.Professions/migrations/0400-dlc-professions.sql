-- dlc.professions — day-labour trades and the player's practice in them.

CREATE TABLE IF NOT EXISTS pf_trade (
    id              INTEGER PRIMARY KEY,
    code            TEXT    NOT NULL UNIQUE,
    name_vi         TEXT    NOT NULL,
    name_en         TEXT    NOT NULL,
    output_item_id  INTEGER NOT NULL,
    days_per_batch  INTEGER NOT NULL CHECK (days_per_batch > 0),
    base_quantity   INTEGER NOT NULL CHECK (base_quantity > 0),
    tool_fee        INTEGER NOT NULL CHECK (tool_fee >= 0)
);

CREATE TABLE IF NOT EXISTS pf_skill (
    trade_id       INTEGER PRIMARY KEY REFERENCES pf_trade(id),
    practice_count INTEGER NOT NULL DEFAULT 0 CHECK (practice_count >= 0),
    tier           INTEGER NOT NULL DEFAULT 0 CHECK (tier BETWEEN 0 AND 5)
);

INSERT OR IGNORE INTO pf_trade (id, code, name_vi, name_en, output_item_id, days_per_batch, base_quantity, tool_fee) VALUES
    (1, 'nong_phu',   'Nông Phu',      'farmer',     1, 2, 3, 2),
    (2, 'ngu_dan',    'Ngư Dân',       'fisher',     8, 2, 2, 2),
    (3, 'tho_moc',    'Thợ Mộc',       'carpenter',  6, 3, 2, 5),
    (4, 'luyen_kim',  'Thợ Luyện Kim', 'smelter',    5, 4, 2, 9);
