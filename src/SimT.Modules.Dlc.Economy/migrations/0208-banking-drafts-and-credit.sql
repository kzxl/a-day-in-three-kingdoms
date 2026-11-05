-- Ancient Banking, Silver Drafts and Commercial Credit
CREATE TABLE IF NOT EXISTS mk_bank_account (
    citadel_id      INTEGER PRIMARY KEY,
    deposit_balance INTEGER NOT NULL DEFAULT 0,
    loan_balance    INTEGER NOT NULL DEFAULT 0,
    loan_due_day    INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS mk_bank_draft (
    id                INTEGER PRIMARY KEY,
    serial_number     TEXT    NOT NULL UNIQUE,
    face_value        INTEGER NOT NULL,
    issued_day        INTEGER NOT NULL,
    issued_citadel_id INTEGER NOT NULL,
    redeemed          INTEGER NOT NULL DEFAULT 0
);
