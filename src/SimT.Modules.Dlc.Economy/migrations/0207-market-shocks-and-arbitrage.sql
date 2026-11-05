-- Geopolitical Shocks and Market Crisis Events
CREATE TABLE IF NOT EXISTS mk_city_shock (
    id                INTEGER PRIMARY KEY,
    citadel_id        INTEGER NOT NULL,
    shock_type        TEXT    NOT NULL,
    start_day         INTEGER NOT NULL,
    end_day           INTEGER NOT NULL,
    severity_permille INTEGER NOT NULL DEFAULT 1000,
    note_vi           TEXT    NOT NULL
);
