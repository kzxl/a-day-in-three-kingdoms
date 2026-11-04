-- dlc.politics — the offices a commoner can earn, and the player's standing at court.

CREATE TABLE IF NOT EXISTS po_office (
    id                  INTEGER PRIMARY KEY,
    code                TEXT    NOT NULL UNIQUE,
    name_vi             TEXT    NOT NULL,
    name_en             TEXT    NOT NULL,
    rank                INTEGER NOT NULL CHECK (rank > 0),
    monthly_stipend     INTEGER NOT NULL CHECK (monthly_stipend >= 0),
    reputation_required INTEGER NOT NULL CHECK (reputation_required >= 0)
);

CREATE TABLE IF NOT EXISTS po_state (
    key   TEXT PRIMARY KEY,
    value INTEGER NOT NULL
);

INSERT OR IGNORE INTO po_office (id, code, name_vi, name_en, rank, monthly_stipend, reputation_required) VALUES
    (1, 'huyen_thua', 'Huyện Thừa', 'county deputy', 1,  80,  3),
    (2, 'quan_thua',  'Quận Thừa',  'commandery deputy', 2, 150,  8),
    (3, 'thai_thu',   'Thái Thú',   'governor', 3, 300, 20);

INSERT OR IGNORE INTO po_state (key, value) VALUES ('reputation', 0), ('office_id', 0);
