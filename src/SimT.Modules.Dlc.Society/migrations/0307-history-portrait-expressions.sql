-- Per-character expressions, parallel to the event's narrative sentences (zero based).
CREATE TABLE IF NOT EXISTS so_event_portrait_expression (
    event_code TEXT NOT NULL REFERENCES so_historical_event(code),
    npc_code TEXT NOT NULL,
    line_index INTEGER NOT NULL CHECK (line_index >= 0),
    expression TEXT NOT NULL CHECK (expression IN ('normal', 'happy', 'angry', 'sad')),
    PRIMARY KEY (event_code, npc_code, line_index)
);

INSERT OR IGNORE INTO so_event_portrait_expression VALUES
('evt_184_khan_vang', 'npc_0004', 0, 'angry'),
('evt_184_khan_vang', 'npc_0004', 3, 'sad'),
('evt_189_dong_trac_lac_duong', 'npc_0023', 0, 'angry'),
('evt_189_dong_trac_lac_duong', 'npc_0057', 2, 'happy'),
('evt_190_ho_lao_quan', 'npc_0057', 2, 'angry'),
('evt_190_ho_lao_quan', 'npc_0003', 1, 'angry'),
('evt_190_doi_do_truong_an', 'npc_0023', 0, 'angry'),
('evt_191_ton_kien_tuong_duong', 'npc_0014', 0, 'sad'),
('evt_192_lu_bo_tru_dong_trac', 'npc_0057', 0, 'angry'),
('evt_192_lu_bo_tru_dong_trac', 'npc_0023', 0, 'angry'),
('evt_196_hien_de_hua_xuong', 'npc_0022', 0, 'happy'),
('evt_198_ha_phi_lu_bo', 'npc_0057', 0, 'sad'),
('evt_198_ha_phi_lu_bo', 'npc_0022', 1, 'angry'),
('evt_200_quan_do_dai_chien', 'npc_0022', 0, 'angry'),
('evt_207_tam_co_thao_lu', 'npc_0001', 0, 'happy'),
('evt_207_tam_co_thao_lu', 'npc_0025', 0, 'happy'),
('evt_208_xich_bich_hoa_cong', 'npc_0022', 0, 'angry'),
('evt_208_xich_bich_hoa_cong', 'npc_0022', 2, 'sad'),
('evt_214_luu_bi_ich_chau', 'npc_0001', 0, 'happy'),
('evt_219_quan_vu_kinh_chau', 'npc_0003', 0, 'sad'),
('evt_220_tao_phi_xung_de', 'npc_0001', 0, 'sad');
