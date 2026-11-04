-- 0101-player-attributes.sql — Canonical 12 RPG attributes, dynamic vitals and social standing.

CREATE TABLE IF NOT EXISTS pl_attribute (
    player_id     INTEGER PRIMARY KEY REFERENCES pl_player(id),
    war_attr      INTEGER NOT NULL DEFAULT 50 CHECK (war_attr BETWEEN 1 AND 120),
    ldr_attr      INTEGER NOT NULL DEFAULT 50 CHECK (ldr_attr BETWEEN 1 AND 120),
    int_attr      INTEGER NOT NULL DEFAULT 50 CHECK (int_attr BETWEEN 1 AND 120),
    pol_attr      INTEGER NOT NULL DEFAULT 50 CHECK (pol_attr BETWEEN 1 AND 120),
    dip_attr      INTEGER NOT NULL DEFAULT 50 CHECK (dip_attr BETWEEN 1 AND 120),
    com_attr      INTEGER NOT NULL DEFAULT 50 CHECK (com_attr BETWEEN 1 AND 120),
    log_attr      INTEGER NOT NULL DEFAULT 50 CHECK (log_attr BETWEEN 1 AND 120),
    cra_attr      INTEGER NOT NULL DEFAULT 50 CHECK (cra_attr BETWEEN 1 AND 120),
    con_attr      INTEGER NOT NULL DEFAULT 50 CHECK (con_attr BETWEEN 1 AND 120),
    wil_attr      INTEGER NOT NULL DEFAULT 50 CHECK (wil_attr BETWEEN 1 AND 120),
    prc_attr      INTEGER NOT NULL DEFAULT 50 CHECK (prc_attr BETWEEN 1 AND 120),
    lck_attr      INTEGER NOT NULL DEFAULT 50 CHECK (lck_attr BETWEEN 1 AND 120),
    war_exp       INTEGER NOT NULL DEFAULT 0 CHECK (war_exp >= 0),
    ldr_exp       INTEGER NOT NULL DEFAULT 0 CHECK (ldr_exp >= 0),
    int_exp       INTEGER NOT NULL DEFAULT 0 CHECK (int_exp >= 0),
    pol_exp       INTEGER NOT NULL DEFAULT 0 CHECK (pol_exp >= 0),
    dip_exp       INTEGER NOT NULL DEFAULT 0 CHECK (dip_exp >= 0),
    com_exp       INTEGER NOT NULL DEFAULT 0 CHECK (com_exp >= 0),
    log_exp       INTEGER NOT NULL DEFAULT 0 CHECK (log_exp >= 0),
    cra_exp       INTEGER NOT NULL DEFAULT 0 CHECK (cra_exp >= 0),
    con_exp       INTEGER NOT NULL DEFAULT 0 CHECK (con_exp >= 0),
    wil_exp       INTEGER NOT NULL DEFAULT 0 CHECK (wil_exp >= 0),
    prc_exp       INTEGER NOT NULL DEFAULT 0 CHECK (prc_exp >= 0),
    lck_exp       INTEGER NOT NULL DEFAULT 0 CHECK (lck_exp >= 0)
);

CREATE TABLE IF NOT EXISTS pl_vitals (
    player_id     INTEGER PRIMARY KEY REFERENCES pl_player(id),
    health        INTEGER NOT NULL DEFAULT 100 CHECK (health BETWEEN 0 AND 100),
    fatigue       INTEGER NOT NULL DEFAULT 0 CHECK (fatigue BETWEEN 0 AND 100),
    morale        INTEGER NOT NULL DEFAULT 100 CHECK (morale BETWEEN 0 AND 100),
    stress        INTEGER NOT NULL DEFAULT 0 CHECK (stress BETWEEN 0 AND 100)
);

CREATE TABLE IF NOT EXISTS pl_social (
    player_id     INTEGER PRIMARY KEY REFERENCES pl_player(id),
    fame          INTEGER NOT NULL DEFAULT 100 CHECK (fame >= 0),
    virtue        INTEGER NOT NULL DEFAULT 50 CHECK (virtue BETWEEN 0 AND 100),
    credibility   INTEGER NOT NULL DEFAULT 50 CHECK (credibility BETWEEN 0 AND 100)
);
