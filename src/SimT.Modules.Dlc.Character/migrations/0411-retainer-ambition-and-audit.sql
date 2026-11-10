-- Migration 0411: Retainer Ambition, Embezzlement and Sworn Brother Assistance

CREATE TABLE IF NOT EXISTS kz_retainer_audit_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    day_index       INTEGER NOT NULL,
    retainer_id     INTEGER NOT NULL,
    npc_id          INTEGER NOT NULL,
    incident_type   TEXT    NOT NULL CHECK (incident_type IN ('EMBEZZLE', 'DEMAND_RAISE', 'DESERTION', 'AUDIT_CLEAR', 'PLACATED', 'SWORN_AID')),
    copper_amount   INTEGER NOT NULL DEFAULT 0,
    loyalty_delta   INTEGER NOT NULL DEFAULT 0,
    narrative_vi    TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_kz_audit_log_npc ON kz_retainer_audit_log(npc_id);
CREATE INDEX IF NOT EXISTS ix_kz_audit_log_day ON kz_retainer_audit_log(day_index);
