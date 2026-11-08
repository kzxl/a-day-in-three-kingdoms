CREATE TABLE IF NOT EXISTS so_rumor (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    heard_city_id INTEGER NOT NULL,
    heard_day INTEGER NOT NULL,
    expires_day INTEGER NOT NULL,
    kind TEXT NOT NULL CHECK(kind IN ('price','event','whereabouts','danger')),
    title_vi TEXT NOT NULL,
    narrative_vi TEXT NOT NULL,
    is_true INTEGER NOT NULL CHECK(is_true IN (0,1)),
    verified INTEGER NOT NULL DEFAULT 0 CHECK(verified IN (0,1)),
    item_id INTEGER,
    target_city_id INTEGER,
    npc_id INTEGER,
    karma_seed_id INTEGER,
    event_key TEXT,
    event_day INTEGER
);
CREATE INDEX IF NOT EXISTS ix_so_rumor_city_day ON so_rumor(heard_city_id, heard_day);
