-- Migration 0407: Legendary Recipes Using Exotic Raw Materials
INSERT OR IGNORE INTO pf_recipe (id, trade_id, name_vi, required_tier, material_item_id, material_quantity, secondary_item_id, secondary_quantity, output_item_id, output_quantity, craft_days, craft_fee) VALUES
    (19, 4,  'Đúc Huyền Thiết Trọng Kiếm', 4, 25, 2, 15, 2, 5,  2, 5, 80),
    (20, 9,  'May Thiên Tằm Bảo Giáp',     4, 28, 2, 27, 2, 12, 1, 4, 75),
    (21, 3,  'Chế Thiết Mộc Hổ Cân Cung',  3, 26, 2, 32, 1, 6,  2, 4, 65),
    (22, 16, 'Tạc Lam Điền Ngọc Ấn',       4, 29, 1, 16, 2, 16, 1, 5, 90);
