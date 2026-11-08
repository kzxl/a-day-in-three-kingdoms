-- Migration 0407: Legendary Recipes Using Exotic Raw Materials & Support Mastery Perks
INSERT OR IGNORE INTO pf_recipe (id, trade_id, name_vi, required_tier, material_item_id, material_quantity, secondary_item_id, secondary_quantity, output_item_id, output_quantity, craft_days, craft_fee) VALUES
    (19, 4,  'Đúc Huyền Thiết Trọng Kiếm', 4, 25, 2, 15, 2, 5,  2, 5, 80),
    (20, 9,  'May Thiên Tằm Bảo Giáp',     4, 28, 2, 27, 2, 12, 1, 4, 75),
    (21, 3,  'Chế Thiết Mộc Hổ Cân Cung',  3, 26, 2, 32, 1, 6,  2, 4, 65),
    (22, 16, 'Tạc Lam Điền Ngọc Ấn',       4, 29, 1, 16, 2, 16, 1, 5, 90);

INSERT OR IGNORE INTO pf_mastery_perk (id, code, name_vi, description_vi, required_fame, is_unlocked) VALUES
    (6, 'eternal_edge',     'Bất Hoại Kim Thân',  'Khí cụ đang mặc giảm 1 điểm hao mòn mỗi lượt chiến đấu/lao tác; chi phí tu sửa giảm 25%.', 80,  0),
    (7, 'earth_vein_sight', 'Địa Mạch Linh Nhãn', 'Khi khai mỏ hoặc tầm dược dã ngoại, luôn thu hoạch thêm +1 khoáng thạch hoặc dược liệu quý hiếm của vùng.', 120, 0),
    (8, 'grandmaster_seal', 'Danh Sư Khắc Ấn',    'Vũ khí tự tay rèn đúc nhận thêm +30 Uy Danh khởi đầu và tăng +25% giá trị khi xuất bán cho thương hội.', 200, 0);
