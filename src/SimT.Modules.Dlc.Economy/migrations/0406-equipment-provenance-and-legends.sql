-- Migration 0406: Equipment Provenance, ERP Lot Tracking, Craftsman Signatures and Combat Legends
ALTER TABLE pf_equipment ADD COLUMN creator_id INTEGER DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN creator_name_vi TEXT DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN forged_citadel_id INTEGER DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN forged_day_index INTEGER DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN fame_score INTEGER NOT NULL DEFAULT 0;
ALTER TABLE pf_equipment ADD COLUMN kill_count INTEGER NOT NULL DEFAULT 0;
ALTER TABLE pf_equipment ADD COLUMN legend_narrative TEXT DEFAULT NULL;
ALTER TABLE pf_equipment ADD COLUMN lot_code TEXT DEFAULT NULL;

UPDATE pf_equipment
SET lot_code = printf('LOT-18401-C%02d-%04d', citadel_id, id),
    creator_name_vi = COALESCE(creator_name_vi, 'Quan Xưởng Triều Đình'),
    forged_citadel_id = COALESCE(forged_citadel_id, citadel_id),
    forged_day_index = COALESCE(forged_day_index, 1)
WHERE lot_code IS NULL;
