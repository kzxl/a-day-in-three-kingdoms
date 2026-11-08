CREATE TABLE IF NOT EXISTS so_journey_log_bonds (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    date              TEXT    NOT NULL,
    npc_id            INTEGER NOT NULL,
    npc_name_vi       TEXT    NOT NULL,
    citadel_id        INTEGER NOT NULL,
    citadel_name_vi   TEXT    NOT NULL,
    event_kind        TEXT    NOT NULL CHECK (event_kind IN ('martial', 'commerce', 'governance', 'scholarly', 'travel', 'relationship')),
    headline_vi       TEXT    NOT NULL,
    detail_vi         TEXT    NOT NULL,
    stat_impact       TEXT,
    created_tick      INTEGER NOT NULL
);

INSERT INTO so_journey_log_bonds SELECT * FROM so_journey_log;
DROP TABLE so_journey_log;
ALTER TABLE so_journey_log_bonds RENAME TO so_journey_log;
CREATE INDEX IF NOT EXISTS ix_so_journey_log_date ON so_journey_log(date);
CREATE INDEX IF NOT EXISTS ix_so_journey_log_npc ON so_journey_log(npc_id);
CREATE INDEX IF NOT EXISTS ix_so_journey_log_citadel ON so_journey_log(citadel_id);
