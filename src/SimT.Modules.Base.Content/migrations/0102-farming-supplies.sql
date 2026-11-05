-- Farming supplies: seeds, fertilizer, and agricultural tools
INSERT OR IGNORE INTO ct_item (id, code, name_vi, name_en, category, base_price, shelf_life_days, weight) VALUES
    (17, 'hat_giong_lua',       'Hạt giống lúa',         'rice seed',          'seed',     5, 120, 1),
    (18, 'hat_giong_tra',       'Hạt giống trà',         'tea seed',           'seed',    15, 120, 1),
    (19, 'hat_giong_dau_tam',   'Cây giống dâu tằm',     'mulberry seedling',  'seed',    20, 120, 2),
    (20, 'hat_giong_duoc_thao', 'Hạt giống dược thảo',   'herb seed',          'seed',    35, 120, 1),
    (21, 'phan_bon',            'Phân bón nông nghiệp',  'fertilizer',         'farming', 15,   0, 5),
    (22, 'cuoc_nong',           'Cuốc nông nghiệp',      'farming hoe',        'tool',    60,   0, 3),
    (23, 'liem_gat',            'Liềm thu hoạch',        'harvest sickle',     'tool',    50,   0, 2),
    (24, 'binh_tuoi',           'Gầu mương thủy lợi',    'irrigation scoop',   'tool',    70,   0, 4);
