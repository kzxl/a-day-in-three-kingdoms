-- 0206-npc-economic-demands.sql: Realistic NPC economic demands and trade preferences
-- Maps characters and roles to willingness to buy goods at markups (500 to 2000 permille).

INSERT OR IGNORE INTO mk_npc_preference (npc_id, category, bid_markup_permille) VALUES
    -- 1. Trương Lão Bản (ID 1) - Dược điếm Lạc Dương
    (1, 'material', 1100),

    -- 2. Lý Chủ Quán (ID 2) - Tửu quán Trường An
    (2, 'grain', 1150),

    -- 3. Triệu Vân (ID 3) - Danh tướng, cần quân nhu, ngựa, thuốc men
    (3, 'food',     1250),
    (3, 'medicine', 1300),
    (3, 'material', 1200),

    -- 4. Trương Phi (ID 4) - Thích danh tửu, thịt khô, binh khí
    (4, 'food',     1350),
    (4, 'material', 1250),

    -- 5. Quan Vũ (ID 5) - Cần lương thảo, chiến giáp, sách sử
    (5, 'food',     1200),
    (5, 'luxury',   1250),
    (5, 'material', 1200),

    -- 6. Lưu Bị (ID 6) - Cần lòng dân lương thực, tơ lụa kết giao
    (6, 'grain',    1250),
    (6, 'luxury',   1300),
    (6, 'food',     1150),

    -- 7. Tào Tháo (ID 7) - Đại quân chủ, chuộng danh tửu, trầm hương, binh bị
    (7, 'luxury',   1400),
    (7, 'food',     1300),
    (7, 'material', 1350),

    -- 8. Tôn Quyền (ID 8) - Giang Đông phú hào, lụa là trà thơm thuyền bè
    (8, 'luxury',   1350),
    (8, 'material', 1250),

    -- 9. Gia Cát Lượng (ID 9) - Quân sư, chuộng trà đạo, dược liệu, cơ quan mộc khí
    (9, 'luxury',   1300),
    (9, 'medicine', 1350),
    (9, 'material', 1200),

    -- 10. Mễ Trúc (ID 10) - Phú thương Cẩm Tú Phô
    (10, 'grain',    1200),
    (10, 'material', 1150),

    -- 11. Hoa Đà (ID 11) - Thần y, tầm dược liệu, nhân sâm, rượu thuốc
    (11, 'medicine', 1500),
    (11, 'food',     1200),

    -- 12. Trương Trọng Cảnh (ID 12) - Y thánh, thu mua dược liệu
    (12, 'medicine', 1450),

    -- 13. Điển Vi (ID 13) - Dũng tướng, chuộng thịt tươi và song kích
    (13, 'food',     1400),
    (13, 'material', 1250),

    -- 14. Hứa Chử (ID 14) - Hổ si, thích ăn no đánh giặc
    (14, 'food',     1400),
    (14, 'grain',    1250),

    -- 15. Tuân Úc (ID 15) - Vương tá chi tài, chuộng trầm hương, thư tịch
    (15, 'luxury',   1400),
    (15, 'food',     1100),

    -- 16. Chu Du (ID 16) - Đô đốc Giang Đông, chuộng đàn trà, lụa là
    (16, 'luxury',   1450),
    (16, 'material', 1200);
