-- 0801-dlc-debate.sql: Batch 9 (Đợt 9 — Biện luận)
-- Cooldown per (npc_id, topic) (7 days) and active debate modifiers (haggle price, recruit fee, 30-day tax relief).

CREATE TABLE IF NOT EXISTS dg_debate_cooldown (
    npc_id INTEGER NOT NULL,
    topic TEXT NOT NULL,
    last_day_index INTEGER NOT NULL,
    last_outcome TEXT NOT NULL,
    PRIMARY KEY (npc_id, topic)
);

CREATE TABLE IF NOT EXISTS dg_debate_effect (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    topic TEXT NOT NULL,
    city_id INTEGER NOT NULL,
    npc_id INTEGER NOT NULL,
    item_id INTEGER NOT NULL DEFAULT 0,
    modifier_percent INTEGER NOT NULL,
    start_day INTEGER NOT NULL,
    end_day INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_dg_debate_effect_lookup
    ON dg_debate_effect (topic, city_id, npc_id, end_day);
