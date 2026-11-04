-- 0105-life-skills.sql — RPG Life skills for crafting, cooking, gathering, fishing, hunting, and wilderness survival.

INSERT OR IGNORE INTO np_skill (npc_id, code, name_vi, category, tier, description, effect_summary) VALUES
    -- 1. Ẩm Thực (Culinary / Cooking)
    (2, 'am_thuc', 'Ẩm Thực', 'special', 3, 'Kỹ nghệ mỹ vị tửu quán, kết hợp ngũ vị bồi bổ khí huyết giải trừ mệt mỏi.', 'Tăng hiệu quả hồi phục của món ăn và mở rộng công thức'),
    -- 2. Tầm Dược (Herbalism)
    (8, 'tam_duoc', 'Tầm Dược', 'special', 3, 'Am hiểu cây cỏ hoa lá vùng hoang dã, nhận biết thảo mộc và dược liệu quý.', 'Tăng sản lượng và độ hiếm thảo dược khi hái thuốc dã ngoại'),
    -- 3. Điếu Ngư (Fishing)
    (16, 'dieu_ngu', 'Điếu Ngư', 'special', 3, 'Kỹ nghệ buông cần sông hồ Trường Giang, kiên nhẫn đợi cá lớn cắn câu.', 'Tăng tỉ lệ câu được cá quý và cơ duyên vớt cổ vật'),
    -- 4. Thú Liệp (Hunting)
    (13, 'thu_liep', 'Thú Liệp', 'special', 4, 'Bậc thầy bắn cung săn thú, theo dấu mãnh thú trên thảo nguyên và rừng rậm.', 'Tăng sản lượng thịt da thú và khả năng đả bại mãnh thú'),
    -- 5. Luyện Đan (Alchemy)
    (61, 'luyen_dan', 'Luyện Đan', 'special', 5, 'Thuật luyện đan diệu thủ hồi xuân, điều hòa âm dương cứu mạng người trong gang tấc.', 'Gia tăng hiệu quả dược lý đan dược và tỉ lệ đan thành'),
    -- 6. Khai Khoáng (Mining)
    (1, 'khai_khoang', 'Khai Khoáng', 'special', 3, 'Nhãn lực phân biệt quặng mỏ trong hang sâu, đào bới than đá và thỏi sắt.', 'Gia tăng sản lượng và cơ hội tìm thấy khoáng thạch quý'),
    -- 7. Dệt May (Tailoring)
    (9, 'det_may', 'Dệt May', 'special', 3, 'Canh cửi dệt lụa gấm, gia công áo giáp da và chiến bào hoa mỹ.', 'Nâng cao phẩm chất y phục và độ bền của giáp nhẹ'),
    -- 8. Hóa Men Nấu Rượu (Brewing)
    (4, 'u_men', 'Hóa Men Ủ Rượu', 'special', 3, 'Bí thuật lên men nấu rượu nếp nồng đậm, uống vào tráng chí ngút ngàn.', 'Chế biến các loại danh tửu thượng hạng và giảm căng thẳng'),
    -- 9. Nông Canh (Agronomy)
    (7, 'nong_canh', 'Nông Canh', 'special', 3, 'Thấu triệt nông vụ mùa màng, tuyển chọn lúa giống và bồi dưỡng thổ nhưỡng.', 'Tăng năng suất cây trồng và sản lượng thu hoạch ngũ cốc'),
    -- 10. Dã Ngoại Sinh Tồn (Survival)
    (3, 'sinh_ton', 'Dã Ngoại Sinh Tồn', 'special', 4, 'Kinh nghiệm bôn ba khắp thiên hạ, dựng trại dã ngoại và thích ứng khí hậu.', 'Giảm hao tổn thể lực khi du hành và chống chịu thời tiết xấu'),
    -- 11. Ngự Mã (Horsemanship)
    (7, 'ngu_ma', 'Ngự Mã Thuần Hóa', 'special', 4, 'Hiểu rõ tập tính ngựa chiến, kỵ thuật thuần thục trên thảo nguyên.', 'Gia tăng tốc độ thương đội và né tránh nguy hiểm trên đường');
