-- Migration 0408: Two-Way Commission Board (Player-Issued Bounties & Retainer Dispatches)
ALTER TABLE pf_order ADD COLUMN direction TEXT NOT NULL DEFAULT 'INBOUND';
ALTER TABLE pf_order ADD COLUMN assignee_npc_id INTEGER DEFAULT NULL;
ALTER TABLE pf_order ADD COLUMN assignee_name_vi TEXT DEFAULT NULL;
ALTER TABLE pf_order ADD COLUMN completion_day INTEGER DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_pf_order_direction_status ON pf_order(direction, status, citadel_id);
