-- base.npc is a stub in the slice: the table exists so the boundary is real, and the module
-- can grow without touching Core or other modules.

CREATE TABLE IF NOT EXISTS np_npc (
    id         INTEGER PRIMARY KEY,
    code       TEXT    NOT NULL UNIQUE,
    name_vi    TEXT    NOT NULL,
    role       TEXT    NOT NULL,
    citadel_id INTEGER NOT NULL
);

INSERT OR IGNORE INTO np_npc (id, code, name_vi, role, citadel_id) VALUES
    (1, 'thuong_nhan_a', 'Trương Lão Bản', 'merchant', 1),
    (2, 'chu_quan_b',    'Lý Chủ Quán',    'tavern',   2);
