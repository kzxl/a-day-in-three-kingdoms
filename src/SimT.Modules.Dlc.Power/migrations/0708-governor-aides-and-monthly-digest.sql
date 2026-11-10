-- 0708-governor-aides-and-monthly-digest.sql
-- Autonomous Aide Delegation, Fief Policy Directives, and Monthly Governance Digest

ALTER TABLE po_governor_fief ADD COLUMN civil_aide_npc_id INTEGER NOT NULL DEFAULT 0;
ALTER TABLE po_governor_fief ADD COLUMN military_aide_npc_id INTEGER NOT NULL DEFAULT 0;
ALTER TABLE po_governor_fief ADD COLUMN policy_directive TEXT NOT NULL DEFAULT 'BALANCE';
ALTER TABLE po_governor_fief ADD COLUMN last_monthly_digest_text TEXT NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS po_governance_digest (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id INTEGER NOT NULL,
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    directive TEXT NOT NULL,
    population_delta INTEGER NOT NULL DEFAULT 0,
    treasury_delta INTEGER NOT NULL DEFAULT 0,
    food_consumed INTEGER NOT NULL DEFAULT 0,
    garrison_delta INTEGER NOT NULL DEFAULT 0,
    merits_earned INTEGER NOT NULL DEFAULT 0,
    civil_action_vi TEXT NOT NULL,
    military_action_vi TEXT NOT NULL,
    summary_vi TEXT NOT NULL
);
