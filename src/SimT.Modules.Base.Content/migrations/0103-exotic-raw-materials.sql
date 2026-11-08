-- Migration 0103: Exotic Raw Materials for High-Tier Crafting & Alchemy (English codes)
INSERT OR IGNORE INTO ct_item (id, code, name_vi, name_en, category, base_price, shelf_life_days, weight) VALUES
    (25, 'black_iron',      'Huyền Thiết Khoáng', 'black iron ore',   'material', 120, 0, 8),
    (26, 'ironwood',        'Gỗ Thiết Mộc',       'ironwood timber',  'material',  95, 0, 6),
    (27, 'warhorse_hide',   'Da Chiến Mã',        'warhorse hide',    'material',  85, 0, 4),
    (28, 'celestial_silk',  'Tơ Thiên Tằm',       'celestial silk',   'luxury',   160, 0, 2),
    (29, 'lantian_jade',    'Ngọc Bích Lam Điền', 'lantian jade',     'luxury',   220, 0, 3),
    (30, 'south_sea_pearl', 'Nam Hải Minh Châu',  'south sea pearl',  'luxury',   240, 0, 1),
    (31, 'cinnabar_ore',    'Chu Sa Đan Thạch',   'cinnabar ore',     'medicine', 130, 0, 3),
    (32, 'tiger_tendon',    'Gân Mãnh Hổ',        'tiger tendon',     'medicine', 150, 0, 2);
