-- 0104-skill-levels-and-slots.sql — Skill levels, active loadout slots, and tier tracking.

ALTER TABLE pl_skill ADD COLUMN level INTEGER NOT NULL DEFAULT 1;
ALTER TABLE pl_skill ADD COLUMN is_equipped INTEGER NOT NULL DEFAULT 0;
ALTER TABLE pl_skill ADD COLUMN equipped_slot INTEGER;
ALTER TABLE pl_skill ADD COLUMN tier INTEGER NOT NULL DEFAULT 1;
