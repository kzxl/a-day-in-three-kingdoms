-- Migration 0706: Citadel Demographics, Physical Food Loop, and Famine Escalation

CREATE TABLE IF NOT EXISTS po_city_demographics (
    citadel_id           INTEGER PRIMARY KEY,
    population           INTEGER NOT NULL DEFAULT 50000 CHECK (population >= 1000),
    prosperity_score     INTEGER NOT NULL DEFAULT 60 CHECK (prosperity_score BETWEEN 0 AND 100),
    starvation_months    INTEGER NOT NULL DEFAULT 0 CHECK (starvation_months >= 0),
    monthly_food_need    INTEGER NOT NULL DEFAULT 15 CHECK (monthly_food_need > 0),
    monthly_food_got     INTEGER NOT NULL DEFAULT 0,
    famine_status_vi     TEXT NOT NULL DEFAULT 'Bách tính an cư lạc nghiệp',
    updated_day_index    INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_po_demographics_citadel ON po_city_demographics(citadel_id);

-- Initialize 20 historical citadels with realistic population scale and food requirements
INSERT OR IGNORE INTO po_city_demographics (citadel_id, population, prosperity_score, starvation_months, monthly_food_need, monthly_food_got, famine_status_vi, updated_day_index) VALUES
    (1,  120000, 75, 0, 22, 22, 'Lạc Dương đô thành phồn hoa', 1),
    (2,  110000, 70, 0, 20, 20, 'Trường An Quan Trung trù phú', 1),
    (3,   95000, 65, 0, 18, 18, 'Nghiệp Thành Hà Bắc đô ấp', 1),
    (4,   45000, 50, 0, 12, 12, 'Trác Huyện U Châu thanh bình', 1),
    (5,   50000, 45, 0, 14, 14, 'Quảng Tông Ký Châu bình ổn', 1),
    (6,   40000, 55, 0, 12, 12, 'Bình Nguyên ven sông Hoàng Hà', 1),
    (7,   85000, 70, 0, 18, 18, 'Hứa Xương đồn điền bội thu', 1),
    (8,   45000, 50, 0, 13, 13, 'Bắc Bình U Yên tráng chí', 1),
    (9,   70000, 60, 0, 16, 16, 'Hạ Phì Từ Châu thịnh vượng', 1),
    (10,  80000, 65, 0, 17, 17, 'Tương Dương Kinh Sở thắng cảnh', 1),
    (11,  35000, 50, 0, 11, 11, 'Xích Bích thủy trại ngư cảng', 1),
    (12,  75000, 60, 0, 16, 16, 'Giang Lăng đại dịch trạm', 1),
    (13, 100000, 75, 0, 20, 20, 'Kiến Nghiệp Giang Đông phú túc', 1),
    (14,  50000, 55, 0, 13, 13, 'Hán Trung Tần Xuyên hiểm trở', 1),
    (15,  90000, 75, 0, 19, 19, 'Thành Đô Thiên Phủ chi quốc', 1),
    (16,  60000, 55, 0, 14, 14, 'Uyển Thành Nam Dương sầm uất', 1),
    (17,  65000, 50, 0, 15, 15, 'Thọ Xuân Hoài Nam vựa lúa', 1),
    (18,  40000, 45, 0, 12, 12, 'Thượng Đảng Thái Hành hiểm sơn', 1),
    (19,  45000, 45, 0, 13, 13, 'Tây Lương biên thùy kiêu dũng', 1),
    (20,  30000, 40, 0, 10, 10, 'Giao Chỉ Lĩnh Nam viễn xứ', 1);
