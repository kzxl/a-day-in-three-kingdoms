-- dlc.market — first DLC of the product. Owns stall supply and warehouses. It never creates
-- or alters a base table: listings are seeded from the content/map capabilities at start.

CREATE TABLE IF NOT EXISTS mk_stall_listing (
    citadel_id INTEGER NOT NULL,
    item_id    INTEGER NOT NULL,
    stock      INTEGER NOT NULL CHECK (stock >= 0),
    PRIMARY KEY (citadel_id, item_id)
);

CREATE TABLE IF NOT EXISTS mk_warehouse (
    citadel_id   INTEGER PRIMARY KEY,
    capacity     INTEGER NOT NULL CHECK (capacity > 0),
    monthly_rent INTEGER NOT NULL CHECK (monthly_rent > 0)
);

CREATE TABLE IF NOT EXISTS mk_warehouse_stock (
    citadel_id INTEGER NOT NULL,
    item_id    INTEGER NOT NULL,
    quantity   INTEGER NOT NULL CHECK (quantity > 0),
    PRIMARY KEY (citadel_id, item_id)
);

-- A warehouse stock row cannot exist without the warehouse it belongs to.
CREATE TRIGGER IF NOT EXISTS trg_mk_stock_requires_warehouse
BEFORE INSERT ON mk_warehouse_stock
WHEN NOT EXISTS (SELECT 1 FROM mk_warehouse WHERE citadel_id = NEW.citadel_id)
BEGIN
    SELECT RAISE(ABORT, 'mk_warehouse_stock: no warehouse rented in that citadel');
END;
