-- dlc.cultivation — rented plots and the crop currently growing on them.

CREATE TABLE IF NOT EXISTS fm_plot (
    id           INTEGER PRIMARY KEY,
    citadel_id   INTEGER NOT NULL,
    area_mu      INTEGER NOT NULL CHECK (area_mu > 0),
    monthly_rent INTEGER NOT NULL CHECK (monthly_rent > 0),
    rented       INTEGER NOT NULL DEFAULT 0 CHECK (rented IN (0, 1))
);

CREATE TABLE IF NOT EXISTS fm_crop (
    id          INTEGER PRIMARY KEY,
    plot_id     INTEGER NOT NULL REFERENCES fm_plot(id),
    item_id     INTEGER NOT NULL,
    planted_day INTEGER NOT NULL,
    ready_day   INTEGER NOT NULL,
    yield_qty   INTEGER NOT NULL CHECK (yield_qty > 0),
    status      TEXT    NOT NULL CHECK (status IN ('GROWING', 'READY', 'HARVESTED')),
    CHECK (ready_day > planted_day)
);

-- One live crop per plot: sowing twice on the same land is not a rule, it is a bug.
CREATE UNIQUE INDEX IF NOT EXISTS ux_fm_crop_live ON fm_crop(plot_id) WHERE status IN ('GROWING', 'READY');

CREATE INDEX IF NOT EXISTS ix_fm_crop_due ON fm_crop(status, ready_day);
