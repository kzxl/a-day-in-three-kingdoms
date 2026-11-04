-- Migration 0602: Farmland Irrigation & Harvest Log

ALTER TABLE fm_plot ADD COLUMN irrigation_level INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS fm_harvest_log (
    id          INTEGER PRIMARY KEY,
    plot_id     INTEGER NOT NULL REFERENCES fm_plot(id),
    citadel_id  INTEGER NOT NULL,
    crop_code   TEXT    NOT NULL,
    yield_qty   INTEGER NOT NULL CHECK (yield_qty > 0),
    harvest_day INTEGER NOT NULL,
    soil_after  INTEGER NOT NULL CHECK (soil_after BETWEEN 0 AND 100)
);

CREATE INDEX IF NOT EXISTS ix_fm_harvest_plot ON fm_harvest_log(plot_id, harvest_day);
