-- 0502-naval-fleet-and-campaigns.sql: Naval shipyards, riverine fleets, and Yangtze patrol records

CREATE TABLE IF NOT EXISTS ml_naval_shipyard (
    citadel_id INTEGER PRIMARY KEY,
    mong_xung_count INTEGER NOT NULL DEFAULT 0,
    dau_ham_count INTEGER NOT NULL DEFAULT 0,
    lau_thuyen_count INTEGER NOT NULL DEFAULT 0,
    naval_power INTEGER NOT NULL DEFAULT 0,
    patrol_victory_count INTEGER NOT NULL DEFAULT 0,
    last_patrol_date TEXT
);

-- Seed major riverine / naval ports (Lạc Dương=1, Tương Dương=9, Giang Lăng=10, Kiến Nghiệp=12, Ngô Quận=13, Hội稽/Sài Tang)
INSERT OR IGNORE INTO ml_naval_shipyard (citadel_id, mong_xung_count, dau_ham_count, lau_thuyen_count, naval_power, patrol_victory_count, last_patrol_date) VALUES
(1,  1, 0, 0, 15, 0, NULL),
(9,  2, 1, 0, 65, 0, NULL),
(10, 2, 1, 0, 65, 0, NULL),
(11, 1, 1, 0, 50, 0, NULL),
(12, 3, 2, 1, 190, 0, NULL),
(13, 2, 1, 0, 65, 0, NULL);
