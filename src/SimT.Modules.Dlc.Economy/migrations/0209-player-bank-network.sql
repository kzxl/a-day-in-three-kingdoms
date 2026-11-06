-- 0209-player-bank-network.sql — Player-Owned Bank Headquarters, Branch Network & Vault Logistics

CREATE TABLE IF NOT EXISTS bk_player_bank (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id INTEGER NOT NULL DEFAULT 1,
    bank_name TEXT NOT NULL,
    headquarters_citadel_id INTEGER NOT NULL,
    reputation INTEGER NOT NULL DEFAULT 50,
    reserve_ratio_pct INTEGER NOT NULL DEFAULT 40,
    total_issued_drafts INTEGER NOT NULL DEFAULT 0,
    created_date TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS bk_player_branch (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    bank_id INTEGER NOT NULL,
    citadel_id INTEGER NOT NULL,
    branch_name TEXT NOT NULL,
    vault_cash INTEGER NOT NULL DEFAULT 0,
    npc_deposits INTEGER NOT NULL DEFAULT 0,
    npc_loans INTEGER NOT NULL DEFAULT 0,
    manager_npc_id INTEGER,
    deposit_rate_pct REAL NOT NULL DEFAULT 1.5,
    loan_rate_pct REAL NOT NULL DEFAULT 4.0,
    status TEXT NOT NULL DEFAULT 'ACTIVE',
    opened_date TEXT NOT NULL,
    UNIQUE(bank_id, citadel_id)
);

CREATE TABLE IF NOT EXISTS bk_player_convoy (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    bank_id INTEGER NOT NULL,
    origin_citadel_id INTEGER NOT NULL,
    dest_citadel_id INTEGER NOT NULL,
    bullion_amount INTEGER NOT NULL,
    depart_day INTEGER NOT NULL,
    eta_day INTEGER NOT NULL,
    escort_strength INTEGER NOT NULL DEFAULT 50,
    status TEXT NOT NULL DEFAULT 'IN_TRANSIT',
    created_date TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_bk_player_branch_citadel ON bk_player_branch(citadel_id);
CREATE INDEX IF NOT EXISTS ix_bk_player_convoy_bank ON bk_player_convoy(bank_id, status);
