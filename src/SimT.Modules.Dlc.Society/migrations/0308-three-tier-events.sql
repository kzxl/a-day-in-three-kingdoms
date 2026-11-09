-- 0308-three-tier-events.sql — 3-Tier Sandbox Events: Historical Divergence, Dynamic World Events, Personal NPC Events

ALTER TABLE so_historical_event ADD COLUMN preconditions_json TEXT;
ALTER TABLE so_historical_event ADD COLUMN is_diverged INTEGER NOT NULL DEFAULT 0;
ALTER TABLE so_historical_event ADD COLUMN divergence_code TEXT;
ALTER TABLE so_historical_event ADD COLUMN divergence_title_vi TEXT;
ALTER TABLE so_historical_event ADD COLUMN divergence_narrative_vi TEXT;

CREATE TABLE IF NOT EXISTS so_dynamic_event (
    code               TEXT PRIMARY KEY,
    category           TEXT NOT NULL,
    title_vi           TEXT NOT NULL,
    narrative_vi       TEXT NOT NULL,
    citadel_id         INTEGER NOT NULL,
    occurred_date      TEXT NOT NULL,
    duration_days      INTEGER NOT NULL DEFAULT 30,
    impacts_json       TEXT,
    status             TEXT NOT NULL DEFAULT 'active'
);


CREATE TABLE IF NOT EXISTS so_npc_bond (
    npc_a              INTEGER NOT NULL,
    npc_b              INTEGER NOT NULL,
    affinity           INTEGER NOT NULL DEFAULT 0,
    trust              INTEGER NOT NULL DEFAULT 50,
    PRIMARY KEY (npc_a, npc_b)
);
CREATE INDEX IF NOT EXISTS ix_so_npc_bond_b ON so_npc_bond(npc_b);

CREATE TABLE IF NOT EXISTS so_personal_event (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    code               TEXT NOT NULL,
    kind               TEXT NOT NULL,
    npc_id             INTEGER NOT NULL,
    target_npc_id      INTEGER,
    citadel_id         INTEGER NOT NULL,
    event_date         TEXT NOT NULL,
    title_vi           TEXT NOT NULL,
    narrative_vi       TEXT NOT NULL,
    impact_note_vi     TEXT
);

CREATE INDEX IF NOT EXISTS ix_so_dynamic_event_citadel ON so_dynamic_event(citadel_id, status);
CREATE INDEX IF NOT EXISTS ix_so_personal_event_npc ON so_personal_event(npc_id, event_date);

-- Seed preconditions and divergence branches for key historical milestones

UPDATE so_historical_event
SET preconditions_json = '[{"Type":"npc_alive","NpcCode":"npc_0023","ReasonVi":"Đổng Trác phải còn sống để tiến kinh"}]',
    divergence_code = 'evt_189_dong_trac_dead_alt',
    divergence_title_vi = 'Kinh Đô Bình Định — Đổng Trác Đã Đền Tội Sớm',
    divergence_narrative_vi = 'Đổng Trác đã bỏ mạng trước khi kịp tiến kinh tác oai tác quái. Triều đình Lạc Dương tạm thoát kiếp nạn Tây Lương, quần thần tôn phò hoàng thất.'
WHERE code = 'evt_189_dong_trac_lac_duong';

UPDATE so_historical_event
SET preconditions_json = '[{"Type":"npc_alive","NpcCode":"npc_0023","ReasonVi":"Đổng Trác phải còn sống làm bia cho chư hầu phạt Đổng"}]',
    divergence_code = 'evt_190_ho_lao_quan_alt',
    divergence_title_vi = 'Hổ Lao Bất Chiến — Tây Lương Đã Sớm Tan Biến',
    divergence_narrative_vi = 'Thế lực Đổng Trác đã sụp đổ trước khi mười tám lộ chư hầu hội minh, cửa ải Hổ Lao không còn khói lửa gươm đao của Lữ Bố và Hoa Hùng.'
WHERE code = 'evt_190_ho_lao_quan';

UPDATE so_historical_event
SET preconditions_json = '[{"Type":"npc_alive","NpcCode":"npc_0023","ReasonVi":"Đổng Trác phải còn sống"},{"Type":"citadel_owner","CitadelId":1,"FactionId":6,"ReasonVi":"Lạc Dương phải còn do quân Đổng Trác kiểm soát mới bị phóng hỏa dời đô"}]',
    divergence_code = 'evt_190_doi_do_alt',
    divergence_title_vi = 'Lạc Dương Nguyên Vẹn — Thoát Họa Hỏa Thiêu',
    divergence_narrative_vi = 'Kế hoạch thiêu rụi Lạc Dương và cưỡng bức thiên đô Trường An phá sản. Kinh đô phồn hoa không bị hóa thành tro tàn.'
WHERE code = 'evt_190_doi_do_truong_an';

UPDATE so_historical_event
SET preconditions_json = '[{"Type":"npc_alive","NpcCode":"npc_0003","ReasonVi":"Tào Tháo phải còn sống"},{"Type":"npc_alive","NpcCode":"npc_0018","ReasonVi":"Viên Thiệu phải còn sống"}]',
    divergence_code = 'evt_200_quan_do_alt',
    divergence_title_vi = 'Phương Bắc Đổi Dòng — Không Có Đại Chiến Quan Độ',
    divergence_narrative_vi = 'Trật tự phương Bắc đã bị người chơi và thời cuộc làm đảo lộn hoàn toàn. Đại chiến Quan Độ không còn diễn ra theo kịch bản cũ.'
WHERE code = 'evt_200_quan_do';

UPDATE so_historical_event
SET preconditions_json = '[{"Type":"npc_alive","NpcCode":"npc_0003","ReasonVi":"Tào Tháo phải còn sống và thống nhất phương Bắc"}]',
    divergence_code = 'evt_208_xich_bich_alt',
    divergence_title_vi = 'Xích Bích Lặng Sóng — Đại Quân Tào Không Xuống Nam',
    divergence_narrative_vi = 'Tào Tháo không còn đủ binh lực hoặc đã bị ngăn chặn từ Trung Nguyên, Giang Đông và Kinh Châu thoát khỏi hỏa trận Xích Bích.'
WHERE code = 'evt_208_xich_bich';


UPDATE so_historical_event
SET preconditions_json = '[{"Type":"npc_alive","NpcCode":"npc_0004","ReasonVi":"Trương Giác phải còn sống khởi binh"}]',
    divergence_code = 'evt_184_khan_vang_alt',
    divergence_title_vi = 'Khăn Vàng Tan Tác Sớm — Giáo Chủ Đã Quy Tiên',
    divergence_narrative_vi = 'Trương Giác đã bỏ mạng trước ngày định mệnh, nghĩa quân Thái Bình Đạo như rắn mất đầu, tan tác thành các toán thảo khấu địa phương.'
WHERE code = 'evt_184_khan_vang';
