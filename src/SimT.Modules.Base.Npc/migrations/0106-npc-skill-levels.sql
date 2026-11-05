-- 0106-npc-skill-levels.sql — Add level column to np_skill and populate canonical levels for historical characters.

ALTER TABLE np_skill ADD COLUMN level INTEGER NOT NULL DEFAULT 1 CHECK (level BETWEEN 1 AND 10);

-- Baseline scaling based on tier
UPDATE np_skill SET level = 5 WHERE tier = 5;
UPDATE np_skill SET level = 4 WHERE tier = 4;
UPDATE np_skill SET level = 3 WHERE tier = 3;
UPDATE np_skill SET level = 2 WHERE tier = 2;
UPDATE np_skill SET level = 1 WHERE tier = 1;

-- Canonical mastery levels for legendary Three Kingdoms heroes
-- Quan Vũ (Võ Thánh Lv.10, Uy Chấn Lv.8)
UPDATE np_skill SET level = 10 WHERE npc_id = 5 AND code = 'vo_thanh';
UPDATE np_skill SET level = 8 WHERE npc_id = 5 AND code = 'uy_chan';

-- Trương Phi (Đấu Thần Lv.10, Vạn Nhân Địch Lv.8)
UPDATE np_skill SET level = 10 WHERE npc_id = 4 AND code = 'dau_than';
UPDATE np_skill SET level = 8 WHERE npc_id = 4 AND code = 'van_nhan_dich';

-- Lưu Bị (Nhân Đức Lv.10, Độn Tẩu Lv.9)
UPDATE np_skill SET level = 10 WHERE npc_id = 3 AND code = 'nhan_duc';
UPDATE np_skill SET level = 9 WHERE npc_id = 3 AND code = 'don_tau';

-- Chu Du (Hỏa Thần Lv.10, Anh Tư Lv.9)
UPDATE np_skill SET level = 10 WHERE npc_id = 16 AND code = 'hoa_than';
UPDATE np_skill SET level = 9 WHERE npc_id = 16 AND code = 'anh_tu';

-- Trương Giác (Thiên Công Lv.10, Hoàng Thiên Lv.8)
UPDATE np_skill SET level = 10 WHERE npc_id = 6 AND code = 'thien_cong';
UPDATE np_skill SET level = 8 WHERE npc_id = 6 AND code = 'hoang_thien';

-- Tôn Sách (Mãnh Hổ Lv.9, Phá Lũy Lv.7)
UPDATE np_skill SET level = 9 WHERE npc_id = 13 AND code = 'manh_ho';
UPDATE np_skill SET level = 7 WHERE npc_id = 13 AND code = 'pha_luoi';

-- Tôn Quyền (Tọa Giang Lv.9, Dung Nhân Lv.8)
UPDATE np_skill SET level = 9 WHERE npc_id = 18 AND code = 'toa_giang';
UPDATE np_skill SET level = 8 WHERE npc_id = 18 AND code = 'dung_nhan';

-- Viên Thiệu (Minh Chủ Lv.7)
UPDATE np_skill SET level = 7 WHERE npc_id = 17 AND code = 'minh_chu';

-- Lưu Biểu (An Dân Lv.7)
UPDATE np_skill SET level = 7 WHERE npc_id = 15 AND code = 'an_dan';

-- Vương Doãn (Liên Hoàn Kế Lv.8)
UPDATE np_skill SET level = 8 WHERE npc_id = 14 AND code = 'lien_hoan_ke';

-- Lư Thực (Danh Sĩ Lv.8, Đốc Quân Lv.7)
UPDATE np_skill SET level = 8 WHERE npc_id = 11 AND code = 'danh_su';
UPDATE np_skill SET level = 7 WHERE npc_id = 11 AND code = 'doc_quan';

-- Trương Thế Bình (Phú Hào Lv.8, Tiếp Tế Lv.7)
UPDATE np_skill SET level = 8 WHERE npc_id = 7 AND code = 'phu_hao';
UPDATE np_skill SET level = 7 WHERE npc_id = 7 AND code = 'tiep_te';
