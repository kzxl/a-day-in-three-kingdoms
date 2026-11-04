-- Migration 0304: Market Rumors, Commercial Arbitrage & Price Shocks
CREATE TABLE IF NOT EXISTS cv_market_rumor (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    citadel_id          INTEGER NOT NULL,
    item_id             INTEGER NOT NULL,
    event_code          TEXT    NOT NULL,
    headline_vi         TEXT    NOT NULL,
    detail_vi           TEXT    NOT NULL,
    price_mult_permille INTEGER NOT NULL DEFAULT 1000,
    start_day           INTEGER NOT NULL,
    end_day             INTEGER NOT NULL,
    is_active           INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_cv_market_rumor_city ON cv_market_rumor(citadel_id, is_active);

-- Seed initial geopolitical commercial shocks (distant citadels to encourage long-distance trading)
INSERT OR IGNORE INTO cv_market_rumor (id, citadel_id, item_id, event_code, headline_vi, detail_vi, price_mult_permille, start_day, end_day, is_active)
VALUES
    (1, 3, 1, 'nan_doi_ky_chau', 'Nạn Đói Ký Châu', 'Hạn hán kéo dài tại Hà Bắc khiến giá gạo tại Nghiệp Thành tăng vọt, thương nhân khắp nơi đổ về cứu đói trục lợi.', 2200, 66001, 66070, 1),
    (2, 7, 5, 'chuan_bi_quan_dung', 'Tào Tháo Rèn Binh Hứa Đô', 'Tào Mạnh Đức thu gom sắt tinh luyện để tái vũ trang quân Hứa Xương, giá sắt tăng mạnh.', 1800, 66001, 66060, 1),
    (3, 10, 3, 'dich_le_kinh_chau', 'Dịch Lệ Kinh Tương', 'Kinh Châu bùng phát chướng khí mùa hè, danh y khuyên dùng trà mạn giải độc khiến giá trà tại Tương Dương tăng cao.', 2000, 66001, 66065, 1),
    (4, 13, 4, 'khanh_dien_giang_dong', 'Đại Điển Giang Đông', 'Tôn gia mở đại thọ khao quân tại Kiến Nghiệp, thu mua lụa là may lễ phục số lượng cực lớn.', 1900, 66001, 66055, 1),
    (5, 15, 7, 'thuc_dao_chuong_khi', 'Hiểm Đạo Ba Thục', 'Chướng khí Thục Đạo hoành hành, phủ Thành Đô chi tiền vạn nhập dược liệu trị hàn phong dịch.', 2100, 66001, 66080, 1);
