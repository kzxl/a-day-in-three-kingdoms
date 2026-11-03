-- base.player owns the protagonist and the per-city inventory. Cash is NOT stored here:
-- the wallet is a read of the kernel ledger, which keeps one writer.

CREATE TABLE IF NOT EXISTS pl_player (
    id      INTEGER PRIMARY KEY,
    name    TEXT    NOT NULL,
    city_id INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS pl_bag (
    city_id  INTEGER NOT NULL,
    item_id  INTEGER NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    PRIMARY KEY (city_id, item_id)
);
