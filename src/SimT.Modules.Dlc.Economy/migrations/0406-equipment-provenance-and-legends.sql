-- Migration 0406: Equipment Provenance, Craftsman Signatures and Combat Legends
ALTER TABLE pf_equipment ADD COLUMN creator_id INTEGER DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN creator_name_vi TEXT DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN forged_citadel_id INTEGER DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN forged_day_index INTEGER DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN fame_score INTEGER NOT NULL DEFAULT 0;
ALTER TABLE pf_equipment ADD COLUMN kill_count INTEGER NOT NULL DEFAULT 0;
ALTER TABLE pf_equipment ADD COLUMN legend_narrative TEXT DEFAULT NULL;
