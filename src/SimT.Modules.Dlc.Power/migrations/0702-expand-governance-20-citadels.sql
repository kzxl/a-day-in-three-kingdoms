-- 0702-expand-governance-20-citadels.sql — expands governance and faction capitals to cover all 20 historical citadels

UPDATE po_faction SET capital_city_id = 17 WHERE id = 5; -- Viên Thuật đóng đô Thọ Xuân
UPDATE po_faction SET capital_city_id = 19 WHERE id = 12; -- Tây Lương Kiêu Kỵ đóng đô Tây Lương

INSERT OR IGNORE INTO po_city_governance (citadel_id, faction_id, governor_npc_id, unrest_level, defense_strength, tax_rate_percent) VALUES
    (16,  8, 15, 20, 75, 12), -- Uyển Thành (Kinh Châu - Lưu Biểu)
    (17,  5, 58, 35, 85, 20), -- Thọ Xuân (Dương Châu / Hoài Nam - Viên Thuật)
    (18,  4, 17, 15, 75, 12), -- Thượng Đảng (Tịnh Châu - Viên Thiệu)
    (19, 12, 28, 25, 80, 15), -- Tây Lương (Lương Châu - Mã Siêu)
    (20,  8, 15, 10, 60, 10); -- Giao Chỉ (Giao Châu - Lưu Biểu / Sĩ gia)
