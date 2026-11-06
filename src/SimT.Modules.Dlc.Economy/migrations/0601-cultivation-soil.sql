-- Land wears out. Soil starts at 100, falls with every harvest and recovers slowly.

ALTER TABLE fm_plot ADD COLUMN soil INTEGER NOT NULL DEFAULT 100;
