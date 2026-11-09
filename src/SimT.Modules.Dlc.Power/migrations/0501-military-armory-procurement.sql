-- Migration 0501: Citadel Military Armory, Autonomous Forges, and Garrison Procurement Loop

CREATE TABLE IF NOT EXISTS ml_citadel_armory (
    citadel_id           INTEGER PRIMARY KEY,
    weapons_stock        INTEGER NOT NULL DEFAULT 40 CHECK (weapons_stock >= 0),
    weapons_capacity     INTEGER NOT NULL DEFAULT 100 CHECK (weapons_capacity > 0),
    monthly_iron_in      INTEGER NOT NULL DEFAULT 0,
    monthly_arms_out     INTEGER NOT NULL DEFAULT 0,
    last_status_vi       TEXT NOT NULL DEFAULT 'Binh giới sung túc',
    updated_day_index    INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_ml_armory_citadel ON ml_citadel_armory(citadel_id);

-- Initialize all 20 historical citadels with armories
INSERT OR IGNORE INTO ml_citadel_armory (citadel_id, weapons_stock, weapons_capacity, monthly_iron_in, monthly_arms_out, last_status_vi, updated_day_index) VALUES
    (1,  45, 120, 0, 0, 'Lạc Dương võ khố sung túc', 1),
    (2,  50, 150, 0, 0, 'Trường An đại quân giới túc bị', 1),
    (3,  40, 100, 0, 0, 'Nghiệp Thành quân giới hoàn bị', 1),
    (4,  35,  90, 0, 0, 'Trác Huyện biên thùy võ trang', 1),
    (5,  40, 100, 0, 0, 'Quảng Tông võ bị sung túc', 1),
    (6,  30,  80, 0, 0, 'Bình Nguyên phòng thủ ổn định', 1),
    (7,  45, 120, 0, 0, 'Hứa Xương đồn điền võ khố', 1),
    (8,  35, 100, 0, 0, 'Bắc Bình thiết kỵ võ trang', 1),
    (9,  40, 100, 0, 0, 'Hạ Phì võ bị sung túc', 1),
    (10, 45, 110, 0, 0, 'Tương Dương thủy lục quân giới', 1),
    (11, 30,  80, 0, 0, 'Xích Bích thủy trại võ bị', 1),
    (12, 40, 100, 0, 0, 'Giang Lăng đại giáp khố', 1),
    (13, 50, 130, 0, 0, 'Kiến Nghiệp thủy quân võ trang', 1),
    (14, 35,  90, 0, 0, 'Hán Trung hiểm yếu thiết khố', 1),
    (15, 45, 120, 0, 0, 'Thành Đô Thục cẩm giáp binh', 1),
    (16, 35,  90, 0, 0, 'Uyển Thành Nam Dương võ khố', 1),
    (17, 40, 100, 0, 0, 'Thọ Xuân Hoài Nam võ bị', 1),
    (18, 45, 110, 0, 0, 'Thượng Đảng thiết quáng quân giới', 1),
    (19, 40, 100, 0, 0, 'Tây Lương thiết kỵ trọng giáp', 1),
    (20, 30,  80, 0, 0, 'Giao Chỉ Nam Hải phòng bị', 1);
