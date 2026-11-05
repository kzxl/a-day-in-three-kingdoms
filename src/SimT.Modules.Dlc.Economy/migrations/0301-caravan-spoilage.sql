-- Freight loss is recorded per cargo line, so "shipped = received + lost" is checkable.

ALTER TABLE cv_cargo ADD COLUMN lost INTEGER NOT NULL DEFAULT 0;
