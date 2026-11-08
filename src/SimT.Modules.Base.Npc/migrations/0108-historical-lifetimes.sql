-- Audited dates only. See docs/design/starting-scenarios.md for sources and uncertainty policy.
CREATE TABLE IF NOT EXISTS np_lifetime (
 npc_id INTEGER PRIMARY KEY, birth_year INTEGER, death_year INTEGER, home_citadel_id INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS np_lifetime_campaign (id INTEGER PRIMARY KEY, enabled INTEGER NOT NULL);
INSERT OR IGNORE INTO np_lifetime_campaign SELECT 1,
 CASE WHEN EXISTS (SELECT 1 FROM core_state WHERE key='date') THEN 0 ELSE 1 END;
INSERT OR IGNORE INTO np_lifetime VALUES (3,161,223,4); -- Lưu Bị
INSERT OR IGNORE INTO np_lifetime VALUES (4,NULL,221,4); -- Trương Phi
INSERT OR IGNORE INTO np_lifetime VALUES (5,NULL,219,4); -- Quan Vũ
INSERT OR IGNORE INTO np_lifetime VALUES (6,NULL,184,5); -- Trương Giác
INSERT OR IGNORE INTO np_lifetime VALUES (11,NULL,192,5); -- Lư Thực
INSERT OR IGNORE INTO np_lifetime VALUES (14,NULL,192,2); -- Vương Doãn
INSERT OR IGNORE INTO np_lifetime VALUES (15,NULL,208,10); -- Lưu Biểu
INSERT OR IGNORE INTO np_lifetime VALUES (16,175,210,17); -- Chu Du
INSERT OR IGNORE INTO np_lifetime VALUES (17,NULL,202,3); -- Viên Thiệu
INSERT OR IGNORE INTO np_lifetime VALUES (18,182,252,13); -- Tôn Quyền
INSERT OR IGNORE INTO np_lifetime VALUES (20,NULL,219,15); -- Lưu Chương
INSERT OR IGNORE INTO np_lifetime VALUES (21,155,220,7); -- Tào Tháo
INSERT OR IGNORE INTO np_lifetime VALUES (22,NULL,192,2); -- Đổng Trác
INSERT OR IGNORE INTO np_lifetime VALUES (23,NULL,229,8); -- Triệu Vân
INSERT OR IGNORE INTO np_lifetime VALUES (24,181,234,9); -- Gia Cát Lượng
INSERT OR IGNORE INTO np_lifetime VALUES (25,179,214,10); -- Bàng Thống
INSERT OR IGNORE INTO np_lifetime VALUES (26,176,220,2); -- Pháp Chính
INSERT OR IGNORE INTO np_lifetime VALUES (27,NULL,220,10); -- Hoàng Trung
INSERT OR IGNORE INTO np_lifetime VALUES (28,176,222,2); -- Mã Siêu
INSERT OR IGNORE INTO np_lifetime VALUES (29,NULL,234,10); -- Ngụy Diên
INSERT OR IGNORE INTO np_lifetime VALUES (30,202,264,19); -- Khương Duy
INSERT OR IGNORE INTO np_lifetime VALUES (31,NULL,220,9); -- Mi Trúc
INSERT OR IGNORE INTO np_lifetime VALUES (34,163,212,7); -- Tuân Úc
INSERT OR IGNORE INTO np_lifetime VALUES (35,157,214,7); -- Tuân Du
INSERT OR IGNORE INTO np_lifetime VALUES (36,170,207,7); -- Quách Gia
INSERT OR IGNORE INTO np_lifetime VALUES (37,147,223,2); -- Giả Hủ
INSERT OR IGNORE INTO np_lifetime VALUES (38,179,251,1); -- Tư Mã Ý
INSERT OR IGNORE INTO np_lifetime VALUES (39,169,222,3); -- Trương Liêu
INSERT OR IGNORE INTO np_lifetime VALUES (45,NULL,231,3); -- Trương Cáp
INSERT OR IGNORE INTO np_lifetime VALUES (46,NULL,227,2); -- Từ Hoảng
INSERT OR IGNORE INTO np_lifetime VALUES (47,175,200,13); -- Tôn Sách
INSERT OR IGNORE INTO np_lifetime VALUES (48,172,217,17); -- Lỗ Túc
INSERT OR IGNORE INTO np_lifetime VALUES (49,178,220,7); -- Lữ Mông
INSERT OR IGNORE INTO np_lifetime VALUES (50,183,245,13); -- Lục Tốn
INSERT OR IGNORE INTO np_lifetime VALUES (53,NULL,206,6); -- Thái Sử Từ
INSERT OR IGNORE INTO np_lifetime VALUES (56,NULL,199,2); -- Lữ Bố
INSERT OR IGNORE INTO np_lifetime VALUES (57,NULL,199,9); -- Trần Cung
INSERT OR IGNORE INTO np_lifetime VALUES (58,NULL,199,7); -- Viên Thuật
INSERT OR IGNORE INTO np_lifetime VALUES (59,NULL,199,8); -- Công Tôn Toản
INSERT OR IGNORE INTO np_lifetime VALUES (72,187,226,7); -- Tào Phi
INSERT OR IGNORE INTO np_lifetime VALUES (73,197,264,7); -- Đặng Ngải
INSERT OR IGNORE INTO np_lifetime VALUES (74,225,264,7); -- Chung Hội
INSERT OR IGNORE INTO np_lifetime VALUES (96,NULL,221,5); -- Vu Cấm
INSERT OR IGNORE INTO np_lifetime VALUES (97,NULL,218,6); -- Nhạc Tiến
INSERT OR IGNORE INTO np_lifetime VALUES (128,192,232,1); -- Tào Thực
INSERT OR IGNORE INTO np_lifetime VALUES (132,NULL,228,1); -- Vương Lãng
INSERT OR IGNORE INTO np_lifetime VALUES (133,NULL,237,1); -- Trần Quần
INSERT OR IGNORE INTO np_lifetime VALUES (135,208,255,1); -- Tư Mã Sư
INSERT OR IGNORE INTO np_lifetime VALUES (136,211,265,1); -- Tư Mã Chiêu
INSERT OR IGNORE INTO np_lifetime VALUES (138,NULL,212,2); -- Mã Đằng
INSERT OR IGNORE INTO np_lifetime VALUES (139,NULL,215,2); -- Hàn Toại
INSERT OR IGNORE INTO np_lifetime VALUES (246,174,241,9); -- Gia Cát Cẩn
INSERT OR IGNORE INTO np_lifetime VALUES (276,203,253,13); -- Gia Cát Khác
INSERT OR IGNORE INTO np_lifetime VALUES (155,207,271,10); -- Lưu Thiền
INSERT OR IGNORE INTO np_lifetime VALUES (131,157,232,6); -- Hoa Hân
-- Old seed years were not verified: do not expose them as historical facts.
UPDATE np_attribute SET birth_year=(SELECT birth_year FROM np_lifetime WHERE npc_id=np_attribute.npc_id)
 WHERE npc_id IN (SELECT id FROM np_npc WHERE code LIKE 'npc_%');
