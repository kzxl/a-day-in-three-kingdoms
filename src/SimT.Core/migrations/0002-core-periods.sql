-- Kernel hardening: an append-only log of published game periods so a repeated or replayed
-- advance cannot charge the same month twice.

CREATE TABLE IF NOT EXISTS core_period_log (
    period_key  TEXT PRIMARY KEY,
    published_at TEXT NOT NULL
);
