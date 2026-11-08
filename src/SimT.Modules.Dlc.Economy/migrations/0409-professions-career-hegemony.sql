-- Migration 0409: Professions Career Hegemony & Geopolitical Levers
CREATE TABLE IF NOT EXISTS pf_hegemony_enactment (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id        INTEGER NOT NULL,
    trade_id          INTEGER NOT NULL REFERENCES pf_trade(id),
    hegemony_code     TEXT NOT NULL,
    enacted_day_index INTEGER NOT NULL,
    summary_vi        TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_pf_hegemony_citadel ON pf_hegemony_enactment(citadel_id);
