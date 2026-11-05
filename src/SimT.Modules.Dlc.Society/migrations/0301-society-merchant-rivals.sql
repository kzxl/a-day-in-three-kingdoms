-- 0301-society-merchant-rivals.sql: Autonomous AI Merchant Rivals and Enterprise Growth

CREATE TABLE IF NOT EXISTS so_merchant_rival (
    npc_id                    INTEGER PRIMARY KEY REFERENCES np_npc(id),
    enterprise_name           TEXT    NOT NULL,
    merchant_tier             INTEGER NOT NULL DEFAULT 1 CHECK (merchant_tier BETWEEN 1 AND 5),
    capital_quan              INTEGER NOT NULL CHECK (capital_quan >= 0),
    fleet_carts               INTEGER NOT NULL DEFAULT 1 CHECK (fleet_carts >= 1),
    primary_good_id           INTEGER NOT NULL DEFAULT 1,
    headquarters_citadel_id   INTEGER NOT NULL,
    target_citadel_id         INTEGER NOT NULL,
    reputation                INTEGER NOT NULL DEFAULT 50,
    market_influence_permille INTEGER NOT NULL DEFAULT 100,
    monthly_profit            INTEGER NOT NULL DEFAULT 0,
    total_trade_volume        INTEGER NOT NULL DEFAULT 0,
    last_action_desc          TEXT    NOT NULL DEFAULT '',
    updated_date              TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_so_merchant_capital ON so_merchant_rival(capital_quan DESC);
CREATE INDEX IF NOT EXISTS ix_so_merchant_hq ON so_merchant_rival(headquarters_citadel_id);
