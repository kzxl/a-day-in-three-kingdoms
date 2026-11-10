-- Migration 0712: Court Factions Frictions and Imperial Inspections

CREATE TABLE IF NOT EXISTS po_court_faction_standing (
    faction_code     TEXT PRIMARY KEY,
    name_vi          TEXT NOT NULL,
    standing_score   INTEGER NOT NULL DEFAULT 0 CHECK (standing_score >= -100 AND standing_score <= 100),
    description_vi   TEXT NOT NULL
);

INSERT OR IGNORE INTO po_court_faction_standing (faction_code, name_vi, standing_score, description_vi) VALUES
('eunuch', 'Thập Thường Thị (Hoạn Quan Đảng)', 0, 'Cậy sủng lộng quyền, hám lợi tham tài, dung dưỡng gian thương.'),
('scholar', 'Thanh Lưu Sĩ Tộc (Nho Gia Danh Sĩ)', 0, 'Coi trọng chính thống, kỷ cương liêm khiết, ghét cay ghét đắng gian nịnh.'),
('militarist', 'Huân Quý Quân Phái (Tướng Lĩnh Biên Ải)', 0, 'Hiếu chiến trọng võ công, đòi tăng ngân sách quân sự, coi thường văn quan.');

CREATE TABLE IF NOT EXISTS po_imperial_inspection_log (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    day_index         INTEGER NOT NULL,
    citadel_id        INTEGER NOT NULL,
    inspector_name_vi TEXT NOT NULL,
    approach_code     TEXT NOT NULL,
    outcome           TEXT NOT NULL,
    merit_delta       INTEGER NOT NULL DEFAULT 0,
    copper_delta      INTEGER NOT NULL DEFAULT 0,
    narrative_vi      TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_po_inspect_citadel ON po_imperial_inspection_log(citadel_id);
CREATE INDEX IF NOT EXISTS ix_po_inspect_day ON po_imperial_inspection_log(day_index);
