-- Automated Trade Loops and Cartel Warfare
CREATE TABLE IF NOT EXISTS cv_auto_route (
    id             INTEGER PRIMARY KEY,
    name           TEXT    NOT NULL,
    from_city_id   INTEGER NOT NULL,
    to_city_id     INTEGER NOT NULL,
    buy_item_id    INTEGER NOT NULL,
    max_buy_price  INTEGER NOT NULL,
    sell_min_price INTEGER NOT NULL,
    batch_size     INTEGER NOT NULL DEFAULT 20,
    active         INTEGER NOT NULL DEFAULT 1,
    last_run_day   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS cv_cartel_reputation (
    cartel_code   TEXT    PRIMARY KEY,
    standing      INTEGER NOT NULL DEFAULT 0,
    treaty_status TEXT    NOT NULL DEFAULT 'NEUTRAL'
);
