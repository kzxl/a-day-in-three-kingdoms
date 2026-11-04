-- 0105-destiny-talent-tree.sql — Bách Gia Thiên Mệnh passive talent web, node definitions, and milestones.

CREATE TABLE IF NOT EXISTS pl_destiny_node (
    id TEXT PRIMARY KEY,
    school INTEGER NOT NULL,
    tier INTEGER NOT NULL,
    name_vi TEXT NOT NULL,
    summary_vi TEXT NOT NULL,
    point_cost INTEGER NOT NULL DEFAULT 1,
    required_school_points INTEGER NOT NULL DEFAULT 0,
    prerequisites TEXT NOT NULL DEFAULT '',
    mod_code TEXT NOT NULL,
    mod_value INTEGER NOT NULL DEFAULT 0,
    grid_x INTEGER NOT NULL DEFAULT 0,
    grid_y INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS pl_destiny_talent (
    player_id INTEGER NOT NULL,
    node_id TEXT NOT NULL,
    learned_day INTEGER NOT NULL,
    PRIMARY KEY (player_id, node_id)
);

CREATE TABLE IF NOT EXISTS pl_destiny_milestone (
    code TEXT PRIMARY KEY,
    category TEXT NOT NULL,
    name_vi TEXT NOT NULL,
    description_vi TEXT NOT NULL,
    universal_points INTEGER NOT NULL DEFAULT 0,
    resonance_points INTEGER NOT NULL DEFAULT 0,
    resonance_school INTEGER NOT NULL DEFAULT 0,
    is_completed INTEGER NOT NULL DEFAULT 0,
    completed_day INTEGER
);

-- Seed Root Node
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y)
VALUES ('destiny_root', 0, 0, 'Hư Tâm Nhập Thế', 'Tâm cảnh ban sơ, vạn pháp quy nguyên. Mở lối dẫn tới 6 đại học phái Bách Gia.', 0, 0, '', 'universal_open', 1, 0, 0);

-- 1. Đào Chu Chi Thuật (Thương Mại & Đầu Cơ)
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('daochu_root', 1, 0, 'Đào Chu Nhập Môn', 'Bước đầu lĩnh hội thuật kinh thương của Phạm Lãi. Giá mua hàng tại sạp giảm 3%.', 1, 0, 'destiny_root', 'price_buy_discount', 3, 1, 0),
('daochu_minor_bargain', 1, 1, 'Mặc Cả Tinh Tường', 'Am hiểu tâm lý con buôn, giá mua hàng tại mọi sạp giảm thêm 3%.', 1, 1, 'daochu_root', 'price_buy_discount', 3, 2, -1),
('daochu_minor_margin', 1, 1, 'Lợi Nhuận Tối Ưu', 'Kỹ thuật định giá bán sỉ khéo léo, giá bán hàng hóa tăng thêm 4%.', 1, 1, 'daochu_root', 'price_sell_bonus', 4, 2, 1),
('daochu_notable_rumor', 1, 2, 'Thám Mã Phong Thanh', 'Mạng lưới nghe ngóng nhạy bén, nhận trước tin đồn thị trường sớm 3 ngày.', 1, 2, 'daochu_minor_bargain,daochu_minor_margin', 'rumor_intel_days', 3, 3, 0),
('daochu_keystone_merchant_king', 1, 3, 'Kỳ Hóa Khả Cư', 'Keystone Lữ Bất Vi: Toàn bộ lợi nhuận giao thương tăng 25%, bổng lộc thương đoàn dồi dào.', 1, 4, 'daochu_notable_rumor', 'merchant_king_keystone', 25, 4, 0);

-- 2. Bá Đạo Pháp Gia (Đồn Điền & Kỷ Luật)
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('badao_root', 2, 0, 'Pháp Trị Đồn Điền', 'Kỷ luật thép của Thương Ưởng. Năng suất thu hoạch trên mọi mẫu đất tăng 5%.', 1, 0, 'destiny_root', 'farm_yield_bonus', 5, 0, 1),
('badao_minor_soil', 2, 1, 'Luân Canh Giữ Đất', 'Bảo dưỡng đất đai nghiêm cẩn, giảm 15% độ suy kiệt màu mỡ khi thu hoạch.', 1, 1, 'badao_root', 'soil_drain_reduction', 15, -1, 2),
('badao_minor_yield', 2, 1, 'Khẩn Hoang Tăng Vụ', 'Khai hoang mở rộng bờ cõi, tăng thêm 10% sản lượng nông phẩm.', 1, 1, 'badao_root', 'farm_yield_bonus', 10, 1, 2),
('badao_notable_steward', 2, 2, 'Thiết Kỷ Giám Công', 'Quản lý điền trang nghiêm minh, giảm 25% chi phí thuê đất canh tác.', 1, 2, 'badao_minor_soil,badao_minor_yield', 'farm_rent_discount', 25, 0, 3),
('badao_keystone_military_farm', 2, 3, 'Đồn Điền Thiết Kỷ', 'Keystone Tào Tháo: Năng suất gieo trồng trên mọi cánh đồng tăng vọt 40%.', 1, 4, 'badao_notable_steward', 'military_farm_keystone', 40, 0, 4);

-- 3. Vương Đạo Nho Gia (Nhân Tâm & Quan Trường)
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('vuongdao_root', 3, 0, 'Thi Lễ Gia Phong', 'Đức trị lấy dân làm gốc. Khai mở uy tín danh vọng +15 điểm.', 1, 0, 'destiny_root', 'fame_gain_bonus', 15, -1, 0),
('vuongdao_minor_fame', 3, 1, 'Danh Thần Sĩ Phổ', 'Tiếng lành đồn xa khắp châu quận, tăng 15% uy danh nhận được từ mọi sự kiện.', 1, 1, 'vuongdao_root', 'fame_gain_bonus', 15, -2, -1),
('vuongdao_minor_retainer', 3, 1, 'Môn Đồ Quy Phục', 'Thu phục kẻ sĩ bằng đức độ, giảm 20% chi phí nuôi dưỡng bổng lộc môn khách.', 1, 1, 'vuongdao_root', 'retainer_cost_discount', 20, -2, 1),
('vuongdao_notable_court', 3, 2, 'Thanh Nghị Triều Đình', 'Tiếng nói có trọng lượng nơi cung khuyết, tăng 25% bổng lộc quan trường hàng tháng.', 1, 2, 'vuongdao_minor_fame,vuongdao_minor_retainer', 'court_stipend_bonus', 25, -3, 0),
('vuongdao_keystone_peach_garden', 3, 3, 'Đào Viên Thệ Ước', 'Keystone Lưu Bị: Tăng giới hạn huynh đệ kết nghĩa, toàn bộ môn khách tăng 10 điểm trung thành.', 1, 4, 'vuongdao_notable_court', 'peach_garden_keystone', 10, -4, 0);

-- 4. Thiết Huyết Tiêu Cục (Vũ Lực & Sơn Trại)
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('binhgia_root', 4, 0, 'Thiêu Phong Tiêu Cục', 'Binh pháp Tôn Tử áp dụng vào tiêu cục. Sát thương đối kháng lôi đài tăng 10.', 1, 0, 'destiny_root', 'duel_damage_bonus', 10, 0, -1),
('binhgia_minor_duel', 4, 1, 'Trường Thương Trảm Địch', 'Kỹ thuật đâm kích mãnh liệt, sát thương tuyệt kỹ võ công tăng thêm 15.', 1, 1, 'binhgia_root', 'duel_damage_bonus', 15, -1, -2),
('binhgia_minor_bandit', 4, 1, 'Tiêu Đầu Nhạy Cảm', 'Kinh nghiệm hộ tống giang hồ dày dạn, giảm 30% tỷ lệ bị sơn tặc chặn đường cướp bóc.', 1, 1, 'binhgia_root', 'bandit_ambush_reduction', 30, 1, -2),
('binhgia_notable_spoils', 4, 2, 'Dĩ Chiến Dưỡng Chiến', 'Lấy chiến tranh nuôi chiến tranh, tăng 40% ngân lượng thưởng khi công phá sào huyệt sơn trại.', 1, 2, 'binhgia_minor_duel,binhgia_minor_bandit', 'bandit_bounty_bonus', 40, 0, -3),
('binhgia_keystone_warlord', 4, 3, 'Bá Vương Đơn Thương', 'Keystone Hạng Vũ / Lữ Bố: Sát thương quyết đấu 1v1 tăng vọt 50%.', 1, 4, 'binhgia_notable_spoils', 'warlord_keystone', 50, 0, -4);

-- 5. Bách Công Kỳ Xảo (Kho Vận & Kỹ Nghệ)
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('macgia_root', 5, 0, 'Mặc Tử Bách Công', 'Cải tiến phương tiện di chuyển. Tốc độ vận tải thương đoàn tăng 10%.', 1, 0, 'destiny_root', 'caravan_speed_bonus', 10, 1, 1),
('macgia_minor_speed', 5, 1, 'Xa Luân Trục Sắt', 'Trục bánh bọc sắt kiên cố, tốc độ thương đoàn tăng thêm 15%.', 1, 1, 'macgia_root', 'caravan_speed_bonus', 15, 2, 2),
('macgia_minor_preserve', 5, 1, 'Hầm Đá Chống Ẩm', 'Kho trữ cách nhiệt tối ưu, giảm 25% tốc độ thối rữa nông sản lưu kho.', 1, 1, 'macgia_root', 'spoilage_rate_reduction', 25, 2, 0),
('macgia_notable_ice_cellar', 5, 2, 'Băng Thất Ngàn Năm', 'Kỹ thuật ướp lạnh ngầm, giảm 60% tốc độ hư hao của toàn bộ kho bãi.', 1, 2, 'macgia_minor_speed,macgia_minor_preserve', 'spoilage_rate_reduction', 60, 3, 1),
('macgia_keystone_wooden_ox', 5, 3, 'Mộc Ngưu Lưu Mã', 'Keystone Gia Cát Lượng: Tốc độ caravan tăng vọt 30%, miễn nhiễm hư hao lương thảo trên đường.', 1, 4, 'macgia_notable_ice_cellar', 'wooden_ox_keystone', 30, 4, 2);

-- 6. Ẩn Sĩ Dưỡng Sinh (Y Thuật & Thiên Cơ)
INSERT OR IGNORE INTO pl_destiny_node (id, school, tier, name_vi, summary_vi, point_cost, required_school_points, prerequisites, mod_code, mod_value, grid_x, grid_y) VALUES
('daogia_root', 6, 0, 'Đạo Khí Dưỡng Sinh', 'Thở theo phép quy chân của Lão Tử. Thể lực tối đa tăng 10 điểm.', 1, 0, 'destiny_root', 'max_stamina_bonus', 10, -1, -1),
('daogia_minor_vitality', 6, 1, 'Bách Thảo Điều Tễ', 'Thuốc bổ hằng ngày tráng kiện khí huyết, tăng 20% tốc độ hồi phục thể lực.', 1, 1, 'daogia_root', 'stamina_regen_bonus', 20, -2, -2),
('daogia_minor_longevity', 6, 1, 'Trường Thọ Đan Quyết', 'Khai thông kinh mạch, sức khỏe tối đa tăng thêm 15 điểm.', 1, 1, 'daogia_root', 'max_health_bonus', 15, -2, 0),
('daogia_notable_astronomy', 6, 2, 'Quan Tinh Đoán Mệnh', 'Xem thiên tượng thấu suốt cơ trời, cảnh báo sớm biến động lũ lụt và hạn hán.', 1, 2, 'daogia_minor_vitality,daogia_minor_longevity', 'disaster_warning_days', 7, -3, -1),
('daogia_keystone_five_animals', 6, 3, 'Ngũ Cầm Dưỡng Mệnh', 'Keystone Hoa Đà: Thể lực hồi phục gấp đôi khi nghỉ ngơi, tăng cường sinh mệnh trường cửu.', 1, 4, 'daogia_notable_astronomy', 'five_animals_keystone', 100, -4, -2);

-- Seed Milestones
INSERT OR IGNORE INTO pl_destiny_milestone (code, category, name_vi, description_vi, universal_points, resonance_points, resonance_school, is_completed, completed_day) VALUES
('ms_wealth_1', 'wealth', 'Vạn Quán Gia Tài', 'Tích lũy đạt 10.000 Quan tiền trong két sắt hoặc túi tiền.', 1, 1, 1, 0, NULL),
('ms_wealth_2', 'wealth', 'Phú Giáp Nhất Phương', 'Tích lũy đạt 50.000 Quan tiền tổng tài sản.', 1, 2, 1, 0, NULL),
('ms_explore_1', 'exploration', 'Năm Châu Đặt Chân', 'Đã chu du qua ít nhất 5 đại thành trì khác nhau của Trung Nguyên.', 1, 0, 0, 0, NULL),
('ms_explore_2', 'exploration', 'Giang Sơn Toàn Lãm', 'Đã đặt chân qua trọn vẹn 15 đại thành trì thiên hạ.', 2, 0, 0, 0, NULL),
('ms_court_1', 'court', 'Đăng Khoa Nhập Sĩ', 'Được triều đình bổ nhiệm chức quan đầu tiên (Huyện Thừa trở lên).', 1, 1, 3, 0, NULL),
('ms_martial_1', 'martial', 'Huyết Chiến Sơn Trại', 'Tự thân tiêu diệt thành công sào huyệt sơn tặc đầu tiên.', 1, 1, 4, 0, NULL),
('ms_farm_1', 'farm', 'Phong Thu Mùa Màng', 'Thu hoạch lúa gạo bội thu trên các khu điền trang.', 1, 1, 2, 0, NULL),
('ms_caravan_1', 'caravan', 'Thiên Lý Thương Lộ', 'Đoàn xe Caravan hoàn thành chuyến hàng viễn chinh liên tỉnh đầu tiên.', 1, 1, 5, 0, NULL),
('ms_social_1', 'social', 'Kim Lan Kết Nghĩa', 'Cùng một danh sĩ anh kiệt kết bái huynh đệ sinh tử.', 1, 1, 3, 0, NULL),
('ms_life_1', 'life', 'Tam Thập Nhi Lập', 'Sống sót qua 30 ngày phiêu bạt giữa thời loạn lạc Tam Quốc.', 1, 1, 6, 0, NULL);
