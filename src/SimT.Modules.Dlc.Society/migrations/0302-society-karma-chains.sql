-- 0302-society-karma-chains.sql: Branching Karma & Causality Event Chains

CREATE TABLE IF NOT EXISTS so_karma_seed (
    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    chain_code            TEXT    NOT NULL,
    title_vi              TEXT    NOT NULL,
    current_stage         INTEGER NOT NULL DEFAULT 1,
    max_stage             INTEGER NOT NULL DEFAULT 3,
    branch_choice         TEXT    NOT NULL,
    planted_day           INTEGER NOT NULL,
    trigger_after_day     INTEGER NOT NULL,
    citadel_id            INTEGER NOT NULL,
    target_npc_id         INTEGER,
    karma_alignment       TEXT    NOT NULL CHECK (karma_alignment IN ('virtue', 'ruthless', 'neutral')),
    is_active             INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    is_resolved           INTEGER NOT NULL DEFAULT 0 CHECK (is_resolved IN (0, 1)),
    last_outcome_vi       TEXT    NOT NULL DEFAULT '',
    created_date          TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_so_karma_active ON so_karma_seed(is_active, trigger_after_day);
CREATE INDEX IF NOT EXISTS ix_so_karma_citadel ON so_karma_seed(citadel_id);

CREATE TABLE IF NOT EXISTS so_karma_history (
    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    seed_id               INTEGER NOT NULL REFERENCES so_karma_seed(id),
    chain_code            TEXT    NOT NULL,
    stage                 INTEGER NOT NULL,
    decision_vi           TEXT    NOT NULL,
    consequence_vi        TEXT    NOT NULL,
    stat_delta_summary    TEXT,
    recorded_day          INTEGER NOT NULL,
    recorded_date         TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_so_karma_hist_seed ON so_karma_history(seed_id);
