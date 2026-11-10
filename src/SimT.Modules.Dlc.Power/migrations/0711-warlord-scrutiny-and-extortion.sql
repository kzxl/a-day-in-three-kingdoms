-- 0711-warlord-scrutiny-and-extortion.sql
-- Warlord Scrutiny, War Levy Demands, and Philanthropy System

CREATE TABLE IF NOT EXISTS po_warlord_scrutiny (
    faction_id          INTEGER PRIMARY KEY,
    scrutiny_score      INTEGER NOT NULL DEFAULT 0,
    last_levy_day       INTEGER NOT NULL DEFAULT 0,
    embargo_until_day   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS po_scrutiny_demand (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    faction_id          INTEGER NOT NULL,
    demand_type         TEXT    NOT NULL,
    title_vi            TEXT    NOT NULL,
    description_vi      TEXT    NOT NULL,
    demand_copper       INTEGER NOT NULL,
    due_day_index       INTEGER NOT NULL,
    status              TEXT    NOT NULL DEFAULT 'PENDING'
);

CREATE INDEX IF NOT EXISTS ix_po_scrutiny_demand_status ON po_scrutiny_demand(faction_id, status);

CREATE TABLE IF NOT EXISTS po_philanthropy_log (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id          INTEGER NOT NULL,
    day_index           INTEGER NOT NULL,
    copper_donated      INTEGER NOT NULL,
    fame_earned         INTEGER NOT NULL,
    virtue_earned       INTEGER NOT NULL,
    scrutiny_reduced    INTEGER NOT NULL,
    narrative_vi        TEXT    NOT NULL
);
