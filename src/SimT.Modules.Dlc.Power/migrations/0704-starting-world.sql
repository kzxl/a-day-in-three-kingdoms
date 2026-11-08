-- Existing campaigns retain their ownership. New campaigns begin under the Han.
CREATE TABLE IF NOT EXISTS po_faction_name (
 faction_id INTEGER NOT NULL, from_year INTEGER NOT NULL, name_vi TEXT NOT NULL,
 leader_npc_id INTEGER NOT NULL, PRIMARY KEY(faction_id, from_year)
);
INSERT OR IGNORE INTO po_faction VALUES
 (13, 'han_court', 'Hán triều', 0, 1, 'Giữ gìn Hán thất', 40000, 20000, '#a8a29e'),
 (14, 'yellow_turbans', 'Khăn Vàng', 6, 5, 'Thái Bình đạo', 30000, 5000, '#c9a227'),
 (15, 'shi_xie', 'Sĩ Nhiếp', 0, 20, 'An định Giao Chỉ', 10000, 5000, '#b98e72'),
 (16, 'liu_yan', 'Lưu Yên', 0, 15, 'Trấn giữ Ích Châu', 20000, 10000, '#84cc16'),
 (17, 'han_fu', 'Hàn Phức', 0, 3, 'Trấn giữ Ký Châu', 20000, 10000, '#b5a45d'),
 (18, 'tao_qian', 'Đào Khiêm', 0, 9, 'Trấn giữ Từ Châu', 20000, 10000, '#b97b8c'),
 (19, 'li_jue', 'Lý Thôi', 0, 2, 'Quân Tây Lương', 20000, 10000, '#74696a');
INSERT OR IGNORE INTO po_faction_name VALUES
 (1,0,'Tào Tháo',21),(1,220,'Tào Ngụy',72),
 (2,0,'Lưu Bị',3),(2,221,'Thục Hán',3),
 (3,0,'Tôn Kiên',13),(3,192,'Tôn Sách',47),(3,200,'Tôn Quyền',18),(3,229,'Đông Ngô',18),
 (12,0,'Mã Đằng',138),(12,211,'Mã Siêu',28);
UPDATE po_city_governance SET faction_id=13, governor_npc_id=0
 WHERE NOT EXISTS (SELECT 1 FROM core_state WHERE key='date');
UPDATE po_city_governance SET faction_id=14, governor_npc_id=6
 WHERE citadel_id=5 AND NOT EXISTS (SELECT 1 FROM core_state WHERE key='date');
