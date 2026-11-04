-- 0205-market-real-stalls.sql: Real specialized industry stalls and NPC market offers

CREATE TABLE IF NOT EXISTS mk_stall (
    id          INTEGER PRIMARY KEY,
    citadel_id  INTEGER NOT NULL,
    sector      TEXT    NOT NULL,
    name_vi     TEXT    NOT NULL,
    owner_kind  TEXT    NOT NULL CHECK (owner_kind IN ('official', 'npc', 'player')),
    npc_id      INTEGER,
    reputation  INTEGER NOT NULL DEFAULT 100 CHECK (reputation >= 0)
);

CREATE TABLE IF NOT EXISTS mk_npc_offer (
    id                INTEGER PRIMARY KEY,
    stall_id          INTEGER NOT NULL REFERENCES mk_stall(id),
    npc_id            INTEGER NOT NULL,
    item_id           INTEGER NOT NULL,
    quantity          INTEGER NOT NULL CHECK (quantity >= 0),
    max_quantity      INTEGER NOT NULL CHECK (max_quantity > 0),
    unit_price        INTEGER NOT NULL CHECK (unit_price > 0),
    discount_permille INTEGER NOT NULL DEFAULT 0 CHECK (discount_permille BETWEEN 0 AND 500)
);

CREATE TABLE IF NOT EXISTS mk_npc_preference (
    npc_id              INTEGER NOT NULL,
    category            TEXT    NOT NULL,
    bid_markup_permille INTEGER NOT NULL DEFAULT 1000 CHECK (bid_markup_permille BETWEEN 500 AND 2000),
    PRIMARY KEY (npc_id, category)
);

-- Seed specialized industry stalls across key citadels
INSERT OR IGNORE INTO mk_stall (id, citadel_id, sector, name_vi, owner_kind, npc_id, reputation) VALUES
    -- 1. Lạc Dương (Citadel 1)
    (1,  1, 'grain',    'Quan Lương Thương Lạc Dương', 'official', NULL, 120),
    (2,  1, 'textile',  'Tơ Lụa Cẩm Tú Đường',         'official', NULL, 110),
    (3,  1, 'medicine', 'Hiệu Thuốc Tế Thế',           'npc',      1,    150),
    (4,  1, 'metal',    'Thiết Khí Phô Lạc Dương',     'official', NULL, 100),
    (5,  1, 'luxury',   'Lạc Dương Trân Bảo Các',      'official', NULL, 130),

    -- 2. Trường An (Citadel 2)
    (6,  2, 'grain',    'Tây Kinh Thường Bình Kho',    'official', NULL, 120),
    (7,  2, 'textile',  'Trường An Thục Lụa Phô',      'official', NULL, 105),
    (8,  2, 'metal',    'Tây Vực Khoáng Thương',       'npc',      2,    115),
    (9,  2, 'luxury',   'Trân Bảo Tơ Lụa Cổ Đạo',      'official', NULL, 140),

    -- 3. Nghiệp Thành (Citadel 3)
    (10, 3, 'grain',    'Ký Châu Đại Lương Phô',       'official', NULL, 110),
    (11, 3, 'luxury',   'Hà Bắc Ngọc Bảo Trai',        'official', NULL, 125),
    (12, 3, 'metal',    'Nghiệp Thành Binh Khí Phô',   'official', NULL, 110),

    -- 4. Trác Huyện (Citadel 4)
    (13, 4, 'grain',    'U Châu Nông Sản Phô',         'official', NULL,  90),
    (14, 4, 'metal',    'Thiết Tượng Thần Phố',        'official', NULL,  95),

    -- 9. Hạ Bì / Từ Châu (Citadel 9)
    (15, 9, 'textile',  'Mễ Gia Cẩm Tú Phô',           'npc',      10,   160),
    (16, 9, 'grain',    'Từ Châu Lương Thực Hành',     'official', NULL, 110),

    -- 13. Kiến Nghiệp / Giang Nam (Citadel 13)
    (17, 13, 'textile', 'Giang Nam Thêu Dệt Quán',     'official', NULL, 130),
    (18, 13, 'grain',   'Ngô Việt Ngư Lương Hành',     'official', NULL, 115),

    -- 15. Thành Đô / Thục (Citadel 15)
    (19, 15, 'textile', 'Thục Cẩm Đệ Nhất Phô',        'official', NULL, 170),
    (20, 15, 'medicine','Tây Thục Thảo Dược Đường',    'official', NULL, 140);

-- Seed NPC Surplus Offers
INSERT OR IGNORE INTO mk_npc_offer (id, stall_id, npc_id, item_id, quantity, max_quantity, unit_price, discount_permille) VALUES
    -- Trương Lão Bản tại Hiệu Thuốc Tế Thế (Lạc Dương)
    (1, 3, 1, 7,  20, 20, 110, 50),  -- Thuốc: 110 Quan (thay vì 120), chiết khấu 5%
    (2, 3, 1, 13,  5,  5, 185, 20),  -- Nhân Sâm: 185 Quan (thay vì 200), chiết khấu 2%

    -- Lý Chủ Quán tại Tây Vực Khoáng Thương (Trường An)
    (3, 8, 2, 5,  15, 15,  55, 30),  -- Sắt: 55 Quan (thay vì 60), chiết khấu 3%
    (4, 8, 2, 16, 10, 10,  42, 20),  -- Đồng thau: 42 Quan (thay vì 45), chiết khấu 2%

    -- Mễ Trúc tại Mễ Gia Cẩm Tú Phô (Hạ Bì)
    (5, 15, 10, 4,  30, 30,  82, 60), -- Lụa: 82 Quan (thay vì 90), chiết khấu 6%
    (6, 15, 10, 12, 12, 12, 138, 40); -- Gấm Thục: 138 Quan (thay vì 150), chiết khấu 4%

-- Seed NPC Trade Preferences
INSERT OR IGNORE INTO mk_npc_preference (npc_id, category, bid_markup_permille) VALUES
    (1,  'medicine', 1200), -- Trương Lão Bản chuộng Dược liệu (+20%)
    (2,  'food',     1100), -- Lý Chủ Quán chuộng Rượu/Thực phẩm (+10%)
    (10, 'luxury',   1150); -- Mễ Trúc chuộng Kỳ trân dị bảo (+15%)
