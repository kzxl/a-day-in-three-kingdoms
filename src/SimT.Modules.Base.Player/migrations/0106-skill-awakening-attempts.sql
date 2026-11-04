-- 0106-skill-awakening-attempts.sql — Tracks accumulated practice attempts toward skill awakening.

CREATE TABLE IF NOT EXISTS pl_skill_attempt (
    player_id    INTEGER NOT NULL REFERENCES pl_player(id),
    skill_code   TEXT    NOT NULL,
    attempts     INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY(player_id, skill_code)
);
