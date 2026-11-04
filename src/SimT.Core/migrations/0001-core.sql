-- Kernel schema. Owned by SimT.Core. No business table may be created here.

CREATE TABLE IF NOT EXISTS core_migration (
    module_id    TEXT NOT NULL,
    migration_id TEXT NOT NULL,
    applied_at   TEXT NOT NULL,
    PRIMARY KEY (module_id, migration_id)
);

CREATE TABLE IF NOT EXISTS core_state (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS core_ledger (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    day_index     INTEGER NOT NULL,
    game_date     TEXT    NOT NULL,
    account       TEXT    NOT NULL,
    journal_code  TEXT    NOT NULL,
    direction     INTEGER NOT NULL,
    amount        INTEGER NOT NULL,
    balance_after INTEGER NOT NULL,
    description   TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_core_ledger_day ON core_ledger(day_index);
CREATE INDEX IF NOT EXISTS ix_core_ledger_account ON core_ledger(account, id);

-- A ledger line with a non-positive amount or an unknown direction is unrepresentable.
CREATE TRIGGER IF NOT EXISTS trg_core_ledger_guard
BEFORE INSERT ON core_ledger
WHEN NEW.amount <= 0 OR NEW.direction NOT IN (-1, 1)
BEGIN
    SELECT RAISE(ABORT, 'core_ledger: amount must be positive and direction must be -1 or 1');
END;

CREATE TABLE IF NOT EXISTS core_sequence (
    scope TEXT PRIMARY KEY,
    value INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS core_module_state (
    module_id TEXT PRIMARY KEY,
    version   TEXT NOT NULL,
    enabled   INTEGER NOT NULL
);
