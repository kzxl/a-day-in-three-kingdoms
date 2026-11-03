-- Market depth: stall supply now has a baseline to compare against, so price can react to how
-- much is left on the shelf and the stall can restock itself at month boundaries.

ALTER TABLE mk_stall_listing ADD COLUMN baseline_stock INTEGER NOT NULL DEFAULT 40;

UPDATE mk_stall_listing SET baseline_stock = 40 WHERE baseline_stock <= 0;
