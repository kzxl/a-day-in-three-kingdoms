-- dlc.social — Relationship matrix, episodic memory, and retainer system.

CREATE TABLE IF NOT EXISTS kz_relationship (
    npc_id                 INTEGER PRIMARY KEY,
    affinity               INTEGER NOT NULL DEFAULT 0 CHECK (affinity >= -100 AND affinity <= 100),
    trust                  INTEGER NOT NULL DEFAULT 50 CHECK (trust >= 0 AND trust <= 100),
    impression             TEXT    NOT NULL DEFAULT 'Sơ giao',
    gift_count             INTEGER NOT NULL DEFAULT 0 CHECK (gift_count >= 0),
    last_interaction_date  TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS kz_memory (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    npc_id         INTEGER NOT NULL,
    date           TEXT    NOT NULL,
    topic          TEXT    NOT NULL,
    sentiment      TEXT    NOT NULL CHECK (sentiment IN ('positive', 'neutral', 'negative')),
    affinity_delta INTEGER NOT NULL,
    summary_vi     TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_kz_memory_npc ON kz_memory(npc_id);
CREATE INDEX IF NOT EXISTS ix_kz_memory_date ON kz_memory(date);

CREATE TABLE IF NOT EXISTS kz_retainer (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    npc_id          INTEGER NOT NULL UNIQUE,
    role_assigned   TEXT    NOT NULL CHECK (role_assigned IN ('bodyguard', 'accountant', 'advisor', 'manager')),
    monthly_stipend INTEGER NOT NULL CHECK (monthly_stipend >= 0),
    loyalty         INTEGER NOT NULL DEFAULT 70 CHECK (loyalty >= 0 AND loyalty <= 100),
    recruited_date  TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_kz_retainer_npc ON kz_retainer(npc_id);
