-- Every ledger line now records which module posted it, so a report can attribute cashflow and
-- an invariant can spot a module spending outside its own rules.

ALTER TABLE core_ledger ADD COLUMN module_id TEXT NOT NULL DEFAULT 'core';

CREATE INDEX IF NOT EXISTS ix_core_ledger_module ON core_ledger(module_id, day_index);
CREATE INDEX IF NOT EXISTS ix_core_ledger_journal ON core_ledger(journal_code, day_index);
