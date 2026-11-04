-- 0103-player-reputation.sql — Historical deeds and regional prestige tracking for protagonist.

CREATE TABLE IF NOT EXISTS pl_reputation_deed (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id         INTEGER NOT NULL REFERENCES pl_player(id),
    day_index         INTEGER NOT NULL,
    date_text         TEXT    NOT NULL,
    category          TEXT    NOT NULL,
    fame_delta        INTEGER NOT NULL,
    virtue_delta      INTEGER NOT NULL DEFAULT 0,
    credibility_delta INTEGER NOT NULL DEFAULT 0,
    title_vi          TEXT    NOT NULL,
    narrative_vi      TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS pl_reputation_region (
    player_id         INTEGER NOT NULL REFERENCES pl_player(id),
    region_code       TEXT    NOT NULL,
    reputation        INTEGER NOT NULL DEFAULT 20 CHECK (reputation >= 0),
    PRIMARY KEY(player_id, region_code)
);
