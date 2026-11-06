-- Daily price samples, so the stall can show a trend instead of a single number.
-- Kept short on purpose: sixty days is enough for a player to read a trend.

CREATE TABLE IF NOT EXISTS mk_price_sample (
    citadel_id INTEGER NOT NULL,
    item_id    INTEGER NOT NULL,
    day_index  INTEGER NOT NULL,
    buy_price  INTEGER NOT NULL CHECK (buy_price > 0),
    PRIMARY KEY (citadel_id, item_id, day_index)
);

CREATE INDEX IF NOT EXISTS ix_mk_price_day ON mk_price_sample(day_index);
