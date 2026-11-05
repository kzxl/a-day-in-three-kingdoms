-- 0107-destiny-deeper-nodes.sql — Deep Tier 4 Mastery (Đại Tông Sư) nodes and advanced milestones for Bách Gia Thiên Mệnh.

-- 1. Đào Chu Chi Thuật (Thương Mại & Đầu Cơ) — Deeper Mastery Nodes
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('daochu_mastery_monopoly', 1, 4, 'Lũng Đoạn Cửu Châu', 'Thâu tóm toàn bộ đầu mối buôn bán. Giảm thêm 5% giá mua tại sạp và tăng 10% giá bán sỉ hàng hóa.', 1, 5, 'daochu_keystone_merchant_king', 'price_buy_discount', 5, 5, -1),
('daochu_mastery_silver_vault', 1, 4, 'Vạn Lượng Kim Sơn', 'Mạng lưới tiền trang liên quận vững chắc, kích thích mãi lực thương mại, tăng thêm 12% giá bán hàng hóa.', 1, 5, 'daochu_keystone_merchant_king', 'price_sell_bonus', 12, 5, 1);

-- 2. Bá Đạo Pháp Gia (Đồn Điền & Kỷ Luật) — Deeper Mastery Nodes
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('badao_mastery_granary', 2, 4, 'Thiên Hạ Đại Kho', 'Quân điền trù phú trữ lượng bất tận. Sản lượng nông phẩm gieo trồng trên mọi mảnh đất tăng vọt thêm 25%.', 1, 5, 'badao_keystone_military_farm', 'farm_yield_bonus', 25, -1, 5),
('badao_mastery_iron_discipline', 2, 4, 'Kỷ Luật Nghiêm Minh', 'Thiết luật Thương Ưởng trị điền. Triệt tiêu thêm 35% độ suy hao thổ nhưỡng khi thu hoạch nông sản.', 1, 5, 'badao_keystone_military_farm', 'soil_drain_reduction', 35, 1, 5);

-- 3. Vương Đạo Nho Gia (Nhân Tâm & Quan Trường) — Deeper Mastery Nodes
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('vuongdao_mastery_virtue_sage', 3, 4, 'Thánh Nhân Đức Trị', 'Ân trạch đức độ bao trùm bốn biển. Tăng 30% danh vọng và uy đức nhận được từ mọi sự kiện thiên hạ.', 1, 5, 'vuongdao_keystone_peach_garden', 'fame_gain_bonus', 30, -5, -1),
('vuongdao_mastery_prime_minister', 3, 4, 'Thừa Tướng Cung Khuyết', 'Quyền cao chức trọng nơi triều đình, bổng lộc triều đình và bổng ngân chức vụ hàng tháng tăng thêm 50%.', 1, 5, 'vuongdao_keystone_peach_garden', 'court_stipend_bonus', 50, -5, 1);

-- 4. Thiết Huyết Tiêu Cục (Vũ Lực & Sơn Trại) — Deeper Mastery Nodes
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('binhgia_mastery_iron_strike', 4, 4, 'Phá Quân Thần Kích', 'Tuyệt kỹ thương pháp sát phạt vô song. Sát thương trong các trận đấu kiếm hoặc quyết đấu 1v1 tăng thêm 35.', 1, 5, 'binhgia_keystone_warlord', 'duel_damage_bonus', 35, -1, -5),
('binhgia_mastery_sweeping_conquer', 4, 4, 'Tảo Thanh Bát Hoang', 'Tiễu trừ phản loạn giặc cỏ. Tăng thêm 50% tiền thưởng và chiến lợi phẩm khi phá tan sào huyệt sơn tặc.', 1, 5, 'binhgia_keystone_warlord', 'bandit_bounty_bonus', 50, 1, -5);

-- 5. Bách Công Kỳ Xảo (Kho Vận & Kỹ Nghệ) — Deeper Mastery Nodes
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('macgia_mastery_divine_fleet', 5, 4, 'Cơ Quan Thần Xa', 'Trục bánh xe cơ khí vi diệu của Mặc Gia. Tốc độ di chuyển của mọi chuyến hàng caravan tăng thêm 25%.', 1, 5, 'macgia_keystone_wooden_ox', 'caravan_speed_bonus', 25, 5, 3),
('macgia_mastery_eternal_cellar', 5, 4, 'Vĩnh Hằng Hàn Thất', 'Hầm đá bảo quản nhiệt độ cực thấp. Giảm thêm 30% tốc độ hư hao và biến chất nông sản lưu kho bãi.', 1, 5, 'macgia_keystone_wooden_ox', 'spoilage_rate_reduction', 30, 3, 5);

-- 6. Ẩn Sĩ Dưỡng Sinh (Y Thuật & Thiên Cơ) — Deeper Mastery Nodes
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('daogia_mastery_immortal_breath', 6, 4, 'Huyền Khí Quy Nguyên', 'Quy tức bế khí Lão Trang. Thể lực tối đa tăng thêm 25 điểm và tốc độ hồi phục năng lượng tăng 30%.', 1, 5, 'daogia_keystone_five_animals', 'max_stamina_bonus', 25, -5, -3),
('daogia_mastery_cosmic_insight', 6, 4, 'Thái Cực Thiên Cơ', 'Quan sát tuần hoàn trời đất thấu suốt càn khôn. Sức khỏe tối đa tăng thêm 25 điểm và dự báo biến động sớm.', 1, 5, 'daogia_keystone_five_animals', 'max_health_bonus', 25, -3, -5);

-- 7. Advanced Milestones granting high-tier Universal and Resonance Destiny Points
INSERT OR IGNORE INTO pl_destiny_milestone (code, category, name_vi, description_vi, universal_points, resonance_points, resonance_school, is_completed, completed_day) VALUES
('ms_wealth_3', 'wealth', 'Phú Khả Địch Quốc', 'Tích lũy đạt 100.000 Quan tiền ngân quỹ (Tài phú kinh thế).', 2, 2, 1, 0, NULL),
('ms_explore_3', 'exploration', 'Trung Nguyên Hùng Bộ', 'Đặt chân qua trọn vẹn cả 20 đại thành trì trên toàn cõi Tam Quốc.', 2, 1, 0, 0, NULL),
('ms_martial_2', 'martial', 'Vạn Quân Tảo Đãng', 'Tiễu trừ đại thắng từ 3 sào huyệt sơn tặc trở lên.', 1, 2, 4, 0, NULL),
('ms_court_2', 'court', 'Trụ Thạch Triều Đình', 'Được triều đình phong tước thăng hàm quan chức cấp cao (Bậc 4 trở lên).', 1, 2, 3, 0, NULL),
('ms_life_2', 'life', 'Lục Thập Hoa Giáp', 'Sống sót kiên cường qua 60 ngày phiêu bạt thời loạn lạc.', 1, 2, 6, 0, NULL),
('ms_farm_2', 'farm', 'Thiên Kho Tích Cốc', 'Sở hữu và canh tác nông điền quy mô lớn trên khắp các châu quận.', 1, 2, 2, 0, NULL),
('ms_caravan_2', 'caravan', 'Vạn Lý Thương Đồ', 'Đoàn xe Caravan thực hiện thành công nhiều chuyến vận tải liên thành.', 1, 2, 5, 0, NULL);
