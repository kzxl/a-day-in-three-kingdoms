-- 0707-deep-officialdom-governance-and-council.sql
-- Deep officialdom mechanics: Governor Fiefdom, Imperial Council Memorials, Promotion & Demotion Logging

CREATE TABLE IF NOT EXISTS po_governor_fief (
    citadel_id INTEGER PRIMARY KEY,
    player_governor INTEGER NOT NULL DEFAULT 0,
    appointed_day INTEGER NOT NULL DEFAULT 0,
    agriculture_score INTEGER NOT NULL DEFAULT 50,
    commerce_score INTEGER NOT NULL DEFAULT 50,
    fortification_score INTEGER NOT NULL DEFAULT 50,
    order_score INTEGER NOT NULL DEFAULT 50,
    local_treasury INTEGER NOT NULL DEFAULT 500,
    last_eval_day INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS po_court_memorial (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    faction_id INTEGER NOT NULL,
    day_index INTEGER NOT NULL,
    category TEXT NOT NULL,
    title_vi TEXT NOT NULL,
    proposal_vi TEXT NOT NULL,
    outcome TEXT NOT NULL,
    merit_awarded INTEGER NOT NULL DEFAULT 0,
    impact_narrative_vi TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS po_career_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    day_index INTEGER NOT NULL,
    old_office_id INTEGER NOT NULL,
    new_office_id INTEGER NOT NULL,
    change_type TEXT NOT NULL,
    reason_vi TEXT NOT NULL,
    merits_delta INTEGER NOT NULL DEFAULT 0
);

INSERT OR IGNORE INTO po_state (key, value) VALUES ('merits', 0);
INSERT OR IGNORE INTO po_state (key, value) VALUES ('governor_city_id', 0);
