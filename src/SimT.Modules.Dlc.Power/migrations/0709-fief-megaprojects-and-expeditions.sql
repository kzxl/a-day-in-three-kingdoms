-- 0709-fief-megaprojects-and-expeditions.sql
-- Fief Megaprojects and Citadel Military Expeditions

CREATE TABLE IF NOT EXISTS po_fief_megaproject (
    citadel_id       INTEGER NOT NULL,
    project_code     TEXT    NOT NULL,
    status           TEXT    NOT NULL DEFAULT 'NOT_STARTED',
    invested_copper  INTEGER NOT NULL DEFAULT 0,
    required_copper  INTEGER NOT NULL,
    completed_day    INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (citadel_id, project_code)
);

CREATE TABLE IF NOT EXISTS po_fief_expedition_log (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id       INTEGER NOT NULL,
    day_index        INTEGER NOT NULL,
    expedition_type  TEXT    NOT NULL,
    troops_dispatched INTEGER NOT NULL,
    casualties       INTEGER NOT NULL DEFAULT 0,
    spoils_copper    INTEGER NOT NULL DEFAULT 0,
    merits_earned    INTEGER NOT NULL DEFAULT 0,
    narrative_vi     TEXT    NOT NULL
);
