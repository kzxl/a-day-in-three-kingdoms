ALTER TABLE kz_npc_wish RENAME TO kz_npc_wish_old;
CREATE TABLE kz_npc_wish (
 id INTEGER PRIMARY KEY AUTOINCREMENT, npc_id INTEGER NOT NULL,
 template TEXT NOT NULL CHECK(template IN ('shop','rare','revenge','mentor','home')),
 item_id INTEGER NOT NULL DEFAULT 0, quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity>=0),
 city_id INTEGER NOT NULL, target_npc_id INTEGER NOT NULL DEFAULT 0,
 opened TEXT NOT NULL, deadline TEXT NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('hidden','revealed','accepted','completed','expired','self_completed')),
 evidence INTEGER NOT NULL DEFAULT 0, skill_code TEXT NOT NULL DEFAULT ''
);
INSERT INTO kz_npc_wish SELECT *, '' FROM kz_npc_wish_old;
DROP TABLE kz_npc_wish_old;
CREATE UNIQUE INDEX ix_kz_wish_open_npc ON kz_npc_wish(npc_id) WHERE state IN ('hidden','revealed','accepted');
