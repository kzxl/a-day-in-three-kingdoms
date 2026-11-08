-- 0406-dlc-roster.sql: Võ tướng lục (Met NPCs & Collection Milestones)

CREATE TABLE IF NOT EXISTS kz_met_npc (
    npc_id INTEGER PRIMARY KEY,
    first_met_date TEXT NOT NULL,
    met_citadel_id INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS kz_roster_milestone (
    milestone_key TEXT PRIMARY KEY,
    claimed_date TEXT NOT NULL,
    fame_awarded INTEGER NOT NULL
);
