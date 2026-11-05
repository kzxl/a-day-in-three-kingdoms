-- Perishables decay in storage. The carry column keeps the sub-unit remainder so a slow
-- decay (one unit every few days) does not round to zero forever; age_days guarantees the
-- last spoiled unit eventually disappears instead of lingering forever.

ALTER TABLE mk_warehouse_stock ADD COLUMN spoilage_carry INTEGER NOT NULL DEFAULT 0;
ALTER TABLE mk_warehouse_stock ADD COLUMN age_days INTEGER NOT NULL DEFAULT 0;
