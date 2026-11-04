-- Migration 0303: Caravan Fleet Upgrades & Logistics Infrastructure

CREATE TABLE IF NOT EXISTS cv_fleet (
    id                 INTEGER PRIMARY KEY,
    tier               INTEGER NOT NULL DEFAULT 1,
    name_vi            TEXT    NOT NULL,
    capacity_mult      REAL    NOT NULL DEFAULT 1.0,
    cost_discount      REAL    NOT NULL DEFAULT 0.0,
    defense_bonus      REAL    NOT NULL DEFAULT 0.0,
    spoilage_reduction REAL    NOT NULL DEFAULT 0.0
);

INSERT OR IGNORE INTO cv_fleet (id, tier, name_vi, capacity_mult, cost_discount, defense_bonus, spoilage_reduction) VALUES
    (1, 1, 'Xe Thồ Mộc Cơ Bản', 1.0, 0.0, 0.0, 0.0);
