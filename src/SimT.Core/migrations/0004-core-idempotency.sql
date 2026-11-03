-- A journal entry may carry an idempotency key: posting the same key twice is a no-op, so a
-- retried command or a replayed month cannot charge the player twice.

ALTER TABLE core_ledger ADD COLUMN idempotency_key TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_ledger_idempotency
    ON core_ledger(idempotency_key) WHERE idempotency_key IS NOT NULL;
