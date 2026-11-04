-- dlc.society — Living World Engine, autonomous NPC simulation, and realm chronicle.

CREATE TABLE IF NOT EXISTS so_ledger (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    npc_id        INTEGER NOT NULL,
    date          TEXT    NOT NULL,
    direction     TEXT    NOT NULL CHECK (direction IN ('IN', 'OUT')),
    amount        INTEGER NOT NULL CHECK (amount >= 0),
    reason        TEXT    NOT NULL,
    balance_after INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_so_ledger_npc ON so_ledger(npc_id);
CREATE INDEX IF NOT EXISTS ix_so_ledger_date ON so_ledger(date);

CREATE TABLE IF NOT EXISTS so_journey_log (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    date              TEXT    NOT NULL,
    npc_id            INTEGER NOT NULL,
    npc_name_vi       TEXT    NOT NULL,
    citadel_id        INTEGER NOT NULL,
    citadel_name_vi   TEXT    NOT NULL,
    event_kind        TEXT    NOT NULL CHECK (event_kind IN ('martial', 'commerce', 'governance', 'scholarly', 'travel')),
    headline_vi       TEXT    NOT NULL,
    detail_vi         TEXT    NOT NULL,
    stat_impact       TEXT,
    created_tick      INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_so_journey_log_date ON so_journey_log(date);
CREATE INDEX IF NOT EXISTS ix_so_journey_log_npc ON so_journey_log(npc_id);
CREATE INDEX IF NOT EXISTS ix_so_journey_log_citadel ON so_journey_log(citadel_id);

CREATE TABLE IF NOT EXISTS so_npc_activity (
    npc_id             INTEGER PRIMARY KEY,
    current_citadel_id INTEGER NOT NULL,
    current_wealth     INTEGER NOT NULL CHECK (current_wealth >= 0),
    focus_path         TEXT    NOT NULL CHECK (focus_path IN ('martial', 'merchant', 'strategist', 'governor', 'wanderer')),
    current_action     TEXT    NOT NULL,
    stamina            INTEGER NOT NULL DEFAULT 100,
    reputation         INTEGER NOT NULL DEFAULT 50,
    updated_date       TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS so_npc_stat_delta (
    npc_id        INTEGER PRIMARY KEY,
    war_delta     INTEGER NOT NULL DEFAULT 0,
    ldr_delta     INTEGER NOT NULL DEFAULT 0,
    int_delta     INTEGER NOT NULL DEFAULT 0,
    pol_delta     INTEGER NOT NULL DEFAULT 0,
    dip_delta     INTEGER NOT NULL DEFAULT 0,
    com_delta     INTEGER NOT NULL DEFAULT 0,
    log_delta     INTEGER NOT NULL DEFAULT 0,
    cra_delta     INTEGER NOT NULL DEFAULT 0,
    updated_date  TEXT    NOT NULL
);
