-- Migration 0605: Perennial Ratoon Lifecycles & Crop Code Tracking on fm_crop
ALTER TABLE fm_crop ADD COLUMN crop_code TEXT;
ALTER TABLE fm_crop ADD COLUMN remaining_harvests INTEGER NOT NULL DEFAULT 1;
ALTER TABLE fm_crop ADD COLUMN harvest_stage INTEGER NOT NULL DEFAULT 1;
