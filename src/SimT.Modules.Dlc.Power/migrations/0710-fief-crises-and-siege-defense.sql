-- 0710-fief-crises-and-siege-defense.sql
-- Emergent Fief Crises and Citadel Siege Defense Simulation

CREATE TABLE IF NOT EXISTS po_fief_crisis (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id        INTEGER NOT NULL,
    crisis_code       TEXT    NOT NULL,
    title_vi          TEXT    NOT NULL,
    description_vi    TEXT    NOT NULL,
    severity          INTEGER NOT NULL DEFAULT 1,
    status            TEXT    NOT NULL DEFAULT 'PENDING',
    created_day       INTEGER NOT NULL,
    resolved_day      INTEGER NOT NULL DEFAULT 0,
    resolution_choice TEXT    NOT NULL DEFAULT '',
    outcome_vi        TEXT    NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS po_fief_siege_log (
    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id            INTEGER NOT NULL,
    day_index             INTEGER NOT NULL,
    attacker_faction_name TEXT    NOT NULL,
    attacker_troops       INTEGER NOT NULL,
    defense_troops        INTEGER NOT NULL,
    tactical_stance       TEXT    NOT NULL,
    outcome               TEXT    NOT NULL,
    defense_casualties    INTEGER NOT NULL DEFAULT 0,
    attacker_casualties   INTEGER NOT NULL DEFAULT 0,
    spoils_copper         INTEGER NOT NULL DEFAULT 0,
    merits_earned         INTEGER NOT NULL DEFAULT 0,
    battle_phases_vi      TEXT    NOT NULL,
    narrative_vi          TEXT    NOT NULL
);
