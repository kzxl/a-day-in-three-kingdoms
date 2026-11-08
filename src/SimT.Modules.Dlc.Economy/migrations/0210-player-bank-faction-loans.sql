-- 0210-player-bank-faction-loans.sql — Sovereign War Loans & Faction Credit Financing
CREATE TABLE IF NOT EXISTS bk_faction_loan (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    bank_id INTEGER NOT NULL,
    faction_id INTEGER NOT NULL,
    faction_name_vi TEXT NOT NULL,
    principal_amount INTEGER NOT NULL,
    monthly_rate_pct REAL NOT NULL DEFAULT 6.0,
    privilege_code TEXT NOT NULL,
    privilege_desc_vi TEXT NOT NULL,
    issued_day INTEGER NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE'
);

CREATE INDEX IF NOT EXISTS ix_bk_faction_loan_bank ON bk_faction_loan(bank_id, status);
