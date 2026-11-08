CREATE TABLE IF NOT EXISTS kz_npc_wish (
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 npc_id INTEGER NOT NULL,
 template TEXT NOT NULL CHECK(template IN ('shop','rare','revenge','mentor')),
 item_id INTEGER NOT NULL DEFAULT 0,
 quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity>=0),
 city_id INTEGER NOT NULL,
 target_npc_id INTEGER NOT NULL DEFAULT 0,
 opened TEXT NOT NULL,
 deadline TEXT NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('hidden','revealed','accepted','completed','expired','self_completed')),
 evidence INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_kz_wish_open_npc ON kz_npc_wish(npc_id)
 WHERE state IN ('hidden','revealed','accepted');
CREATE TABLE IF NOT EXISTS kz_wish_state (key TEXT PRIMARY KEY);
