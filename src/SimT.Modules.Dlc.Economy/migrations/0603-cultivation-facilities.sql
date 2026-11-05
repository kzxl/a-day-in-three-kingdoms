-- Migration 0603: Estate Facilities & Multi-Tier Upgrades

CREATE TABLE IF NOT EXISTS fm_facility (
    id            INTEGER PRIMARY KEY,
    citadel_id    INTEGER NOT NULL,
    facility_type TEXT    NOT NULL,
    tier          INTEGER NOT NULL DEFAULT 1 CHECK (tier BETWEEN 1 AND 5),
    built_day     INTEGER NOT NULL,
    UNIQUE(citadel_id, facility_type)
);

CREATE INDEX IF NOT EXISTS ix_fm_facility_citadel ON fm_facility(citadel_id);
