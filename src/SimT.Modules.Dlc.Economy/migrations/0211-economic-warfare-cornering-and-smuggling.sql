-- 0211-economic-warfare-cornering-and-smuggling.sql
-- Strategic Market Cornering & Frontline Contraband Smuggling

CREATE TABLE IF NOT EXISTS mk_cornered_market (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id          INTEGER NOT NULL,
    item_id             INTEGER NOT NULL,
    cornered_day        INTEGER NOT NULL,
    hoarded_quantity    INTEGER NOT NULL,
    severity            INTEGER NOT NULL DEFAULT 1,
    status              TEXT    NOT NULL DEFAULT 'ACTIVE'
);

CREATE INDEX IF NOT EXISTS ix_mk_cornered_citadel ON mk_cornered_market(citadel_id, status);

CREATE TABLE IF NOT EXISTS mk_smuggle_run_log (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    day_index           INTEGER NOT NULL,
    origin_citadel_id   INTEGER NOT NULL,
    dest_citadel_id     INTEGER NOT NULL,
    item_id             INTEGER NOT NULL,
    quantity            INTEGER NOT NULL,
    revenue             INTEGER NOT NULL,
    merits_earned       INTEGER NOT NULL DEFAULT 0,
    is_intercepted      INTEGER NOT NULL DEFAULT 0,
    narrative_vi        TEXT    NOT NULL
);
