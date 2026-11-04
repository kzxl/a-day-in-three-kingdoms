-- Expanded historical goods for base.content
INSERT OR IGNORE INTO ct_item (id, code, name_vi, name_en, category, base_price, shelf_life_days, weight) VALUES
    (9,  'lua_mi',     'Lúa mì',     'wheat',     'grain',    10,  60, 10),
    (10, 'ruou',       'Rượu nếp',   'rice wine', 'food',     25, 180,  5),
    (11, 'vai',        'Vải thô',    'linen',     'material', 20,   0,  4),
    (12, 'gam',        'Gấm Thục',   'brocade',   'luxury',  150,   0,  2),
    (13, 'sam',        'Nhân sâm',   'ginseng',   'medicine',200, 120,  1),
    (14, 'tram_huong', 'Trầm hương', 'agarwood',  'luxury',  180,   0,  1),
    (15, 'than_da',    'Than đá',    'coal',      'material', 15,   0, 15),
    (16, 'dong',       'Đồng thau',  'bronze',    'material', 45,   0, 10);
