-- Migration 0401: Sworn Brotherhood & Banquets

CREATE TABLE IF NOT EXISTS kz_sworn_brother (
    npc_id        INTEGER PRIMARY KEY,
    oath_date     TEXT    NOT NULL,
    bond_title    TEXT    NOT NULL,
    assist_count  INTEGER NOT NULL DEFAULT 0 CHECK (assist_count >= 0)
);

CREATE TABLE IF NOT EXISTS kz_banquet (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id     INTEGER NOT NULL,
    date           TEXT    NOT NULL,
    cost_copper    INTEGER NOT NULL CHECK (cost_copper >= 0),
    attendee_count INTEGER NOT NULL CHECK (attendee_count >= 0),
    summary_vi     TEXT    NOT NULL
);
