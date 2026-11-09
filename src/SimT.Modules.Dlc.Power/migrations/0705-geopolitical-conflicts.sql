-- 0705-geopolitical-conflicts.sql — Dynamic Faction Conflicts & Contested Frontlines
CREATE TABLE IF NOT EXISTS po_faction_conflict (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    attacker_faction_id INTEGER NOT NULL,
    defender_faction_id INTEGER NOT NULL,
    target_citadel_id INTEGER NOT NULL,
    intensity INTEGER NOT NULL DEFAULT 1,      -- 1: Skirmish, 2: Clashing, 3: Full Siege
    war_stage TEXT NOT NULL DEFAULT 'SKIRMISH',-- 'SKIRMISH', 'SIEGE', 'BREACH', 'PACIFIED'
    started_day_index INTEGER NOT NULL,
    siege_progress INTEGER NOT NULL DEFAULT 0, -- 0..100
    supply_attrition INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'ACTIVE'      -- 'ACTIVE', 'RESOLVED_VICTORY', 'RESOLVED_REPULSED', 'TRUCE'
);

CREATE INDEX IF NOT EXISTS ix_po_conflict_citadel ON po_faction_conflict(target_citadel_id, status);
CREATE INDEX IF NOT EXISTS ix_po_conflict_factions ON po_faction_conflict(attacker_faction_id, defender_faction_id, status);
