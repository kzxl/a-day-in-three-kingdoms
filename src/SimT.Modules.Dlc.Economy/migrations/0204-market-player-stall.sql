-- dlc.market: Player-owned retail stall and shelf management.

CREATE TABLE IF NOT EXISTS mk_player_stall (
    citadel_id     INTEGER PRIMARY KEY,
    rent_per_month INTEGER NOT NULL CHECK (rent_per_month > 0),
    hired_clerks   INTEGER NOT NULL DEFAULT 1 CHECK (hired_clerks >= 0),
    is_open        INTEGER NOT NULL DEFAULT 1 CHECK (is_open IN (0, 1)),
    created_day    INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS mk_player_stall_shelf (
    citadel_id    INTEGER NOT NULL,
    item_id       INTEGER NOT NULL,
    quantity      INTEGER NOT NULL CHECK (quantity >= 0),
    selling_price INTEGER NOT NULL CHECK (selling_price > 0),
    PRIMARY KEY (citadel_id, item_id)
);

CREATE TRIGGER IF NOT EXISTS trg_mk_shelf_requires_stall
BEFORE INSERT ON mk_player_stall_shelf
WHEN NOT EXISTS (SELECT 1 FROM mk_player_stall WHERE citadel_id = NEW.citadel_id)
BEGIN
    SELECT RAISE(ABORT, 'mk_player_stall_shelf: no player stall in that citadel');
END;
