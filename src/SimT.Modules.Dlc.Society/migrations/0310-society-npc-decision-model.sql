-- 0310-society-npc-decision-model.sql: NPC Decision Model (Needs, Planning, and Multi-Step Goals)

CREATE TABLE IF NOT EXISTS so_npc_needs (
    npc_id           INTEGER PRIMARY KEY REFERENCES np_npc(id),
    safety_need      INTEGER NOT NULL DEFAULT 50 CHECK (safety_need BETWEEN 0 AND 100),
    wealth_need      INTEGER NOT NULL DEFAULT 50 CHECK (wealth_need BETWEEN 0 AND 100),
    fame_need        INTEGER NOT NULL DEFAULT 50 CHECK (fame_need BETWEEN 0 AND 100),
    power_need       INTEGER NOT NULL DEFAULT 50 CHECK (power_need BETWEEN 0 AND 100),
    social_need      INTEGER NOT NULL DEFAULT 50 CHECK (social_need BETWEEN 0 AND 100),
    updated_date     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS so_npc_goal (
    npc_id               INTEGER PRIMARY KEY REFERENCES np_npc(id),
    goal_type            TEXT    NOT NULL,
    stage                TEXT    NOT NULL,
    current_step         INTEGER NOT NULL DEFAULT 1,
    total_steps          INTEGER NOT NULL DEFAULT 3,
    target_citadel_id    INTEGER,
    target_npc_id        INTEGER,
    target_item_id       INTEGER,
    capital_allocated    INTEGER NOT NULL DEFAULT 0,
    progress             INTEGER NOT NULL DEFAULT 0,
    plan_desc            TEXT    NOT NULL,
    started_day_index    INTEGER NOT NULL,
    updated_date         TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_so_npc_goal_type ON so_npc_goal(goal_type);
