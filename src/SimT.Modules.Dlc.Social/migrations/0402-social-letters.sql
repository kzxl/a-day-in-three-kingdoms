-- 0402-social-letters.sql — Letters and correspondence from high affinity NPCs

CREATE TABLE IF NOT EXISTS kz_letter (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    sent_date           TEXT    NOT NULL,
    sender_npc_id       INTEGER NOT NULL REFERENCES np_npc(id),
    sender_name_vi      TEXT    NOT NULL,
    sender_citadel_id   INTEGER NOT NULL,
    title_vi            TEXT    NOT NULL,
    content_vi          TEXT    NOT NULL,
    gift_item_id        INTEGER,
    gift_item_code      TEXT,
    gift_item_name_vi   TEXT,
    gift_item_quantity  INTEGER NOT NULL DEFAULT 0,
    gift_copper         INTEGER NOT NULL DEFAULT 0,
    is_read             INTEGER NOT NULL DEFAULT 0,
    is_claimed          INTEGER NOT NULL DEFAULT 0,
    created_tick        INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_kz_letter_sender ON kz_letter(sender_npc_id);
CREATE INDEX IF NOT EXISTS ix_kz_letter_unread ON kz_letter(is_read);
