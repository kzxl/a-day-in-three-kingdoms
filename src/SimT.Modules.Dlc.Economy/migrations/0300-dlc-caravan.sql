-- dlc.caravan — goods in transit between settlements. Owns two tables and nothing else.

CREATE TABLE IF NOT EXISTS cv_run (
    id          INTEGER PRIMARY KEY,
    from_city   INTEGER NOT NULL,
    to_city     INTEGER NOT NULL,
    depart_day  INTEGER NOT NULL,
    arrive_day  INTEGER NOT NULL,
    fee_paid    INTEGER NOT NULL CHECK (fee_paid >= 0),
    status      TEXT    NOT NULL CHECK (status IN ('RUNNING', 'ARRIVED')),
    CHECK (arrive_day >= depart_day)
);

CREATE TABLE IF NOT EXISTS cv_cargo (
    run_id   INTEGER NOT NULL REFERENCES cv_run(id),
    item_id  INTEGER NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    PRIMARY KEY (run_id, item_id)
);

CREATE INDEX IF NOT EXISTS ix_cv_run_status ON cv_run(status, arrive_day);
