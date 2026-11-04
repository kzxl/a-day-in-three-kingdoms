-- Migration 0305: Caravan Crew Specialists (Escort, Accountant, Navigator)
CREATE TABLE IF NOT EXISTS cv_fleet_crew (
    id                INTEGER PRIMARY KEY CHECK (id = 1),
    escort_npc_id     INTEGER,
    accountant_npc_id INTEGER,
    navigator_npc_id  INTEGER
);

INSERT OR IGNORE INTO cv_fleet_crew (id, escort_npc_id, accountant_npc_id, navigator_npc_id)
VALUES (1, NULL, NULL, NULL);
