-- Migration 0501: Ancestral Shrine & Child Tutoring

CREATE TABLE IF NOT EXISTS ln_shrine (
    id                INTEGER PRIMARY KEY,
    level             INTEGER NOT NULL DEFAULT 1 CHECK (level BETWEEN 1 AND 5),
    clan_prestige     INTEGER NOT NULL DEFAULT 10 CHECK (clan_prestige >= 0),
    last_offering_day INTEGER NOT NULL DEFAULT 0
);

INSERT OR IGNORE INTO ln_shrine (id, level, clan_prestige, last_offering_day)
VALUES (1, 1, 10, 0);

CREATE TABLE IF NOT EXISTS ln_tutoring (
    child_id          INTEGER PRIMARY KEY REFERENCES ln_child(id),
    mentor_name       TEXT    NOT NULL,
    focus_stat        TEXT    NOT NULL CHECK (focus_stat IN ('WAR', 'INT', 'POL', 'CHA')),
    progress          INTEGER NOT NULL DEFAULT 0 CHECK (progress >= 0),
    stat_bonus        INTEGER NOT NULL DEFAULT 0 CHECK (stat_bonus >= 0)
);
