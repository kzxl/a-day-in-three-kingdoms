-- 0308-society-merchant-cargo.sql: Physical cargo tracking on merchant fleet for conservation of mass

CREATE TABLE IF NOT EXISTS so_merchant_cargo (
    npc_id                INTEGER NOT NULL REFERENCES np_npc(id),
    item_id               INTEGER NOT NULL,
    quantity              INTEGER NOT NULL CHECK(quantity >= 0),
    unit_cost_quan        INTEGER NOT NULL CHECK(unit_cost_quan >= 0),
    origin_citadel_id     INTEGER NOT NULL,
    PRIMARY KEY(npc_id, item_id)
);

CREATE INDEX IF NOT EXISTS ix_so_cargo_npc ON so_merchant_cargo(npc_id);
