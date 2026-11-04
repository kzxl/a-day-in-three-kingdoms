-- 0102-player-skills.sql — Player learned skills and mentorship tracking.

CREATE TABLE IF NOT EXISTS pl_skill (
    player_id    INTEGER NOT NULL REFERENCES pl_player(id),
    skill_code   TEXT    NOT NULL,
    learned_date TEXT    NOT NULL,
    proficiency  INTEGER NOT NULL DEFAULT 1 CHECK (proficiency >= 1),
    PRIMARY KEY(player_id, skill_code)
);
