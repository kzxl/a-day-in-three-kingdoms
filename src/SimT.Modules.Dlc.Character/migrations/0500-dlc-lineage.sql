-- dlc.lineage — marriage, children and succession. One spouse, one heir.

CREATE TABLE IF NOT EXISTS ln_spouse (
    id               INTEGER PRIMARY KEY,
    name_vi          TEXT    NOT NULL,
    dowry_paid       INTEGER NOT NULL CHECK (dowry_paid >= 0),
    married_day_index INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS ln_child (
    id             INTEGER PRIMARY KEY,
    name_vi        TEXT    NOT NULL,
    born_day_index INTEGER NOT NULL,
    is_heir        INTEGER NOT NULL DEFAULT 0 CHECK (is_heir IN (0, 1))
);

-- The database refuses a second heir, so succession cannot be ambiguous even after a bug.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ln_child_single_heir ON ln_child(is_heir) WHERE is_heir = 1;

-- At most one spouse: the slice models a single household, not a harem.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ln_spouse_single ON ln_spouse((1));
