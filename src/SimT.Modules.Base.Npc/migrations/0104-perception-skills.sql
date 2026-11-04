-- Migration 0104: Perception & Insight Special Skills
-- Adds Ba Lac Than Nhan, Nguyet Dan Giam Dinh, and Vong Khi Thien Co to mentors

INSERT OR IGNORE INTO np_skill (npc_id, code, name_vi, category, tier, description, effect_summary) VALUES
    (229, 'ba_lac_than_nhan', 'Bá Lạc Thần Nhãn', 'special', 3, 'Nhìn thấu thiên lý mã trong thiên hạ, hiểu rõ sở trường sở đoản của võ tướng và quân tài.', '+15 Tuệ Nhãn hiệu dụng; giải mã toàn bộ chỉ số võ nghiệp/thống soái của mọi nhân vật đồng thành và phương xa mà không cần bái phỏng; giảm 15% yêu cầu chiêu mộ gia thần.'),
    (34,  'nguyet_dan_giam_dinh', 'Nguyệt Đán Giám Định', 'special', 4, 'Thuật giám định phẩm hạnh và tâm tính danh sĩ từ Nguyệt Đán Bình, nhìn thấu chí hướng của kẻ sĩ.', '+25 Tuệ Nhãn hiệu dụng; nhìn thấu tính cách ẩn và tiểu sử sâu kín; tăng gấp đôi Hảo cảm nhận được khi đàm đạo.'),
    (180, 'nguyet_dan_giam_dinh', 'Nguyệt Đán Giám Định', 'special', 4, 'Thuật giám định phẩm hạnh và tâm tính danh sĩ từ Nguyệt Đán Bình, nhìn thấu chí hướng của kẻ sĩ.', '+25 Tuệ Nhãn hiệu dụng; nhìn thấu tính cách ẩn và tiểu sử sâu kín; tăng gấp đôi Hảo cảm nhận được khi đàm đạo.'),
    (24,  'vong_khi_thien_co', 'Vọng Khí Thiên Cơ', 'special', 5, 'Đỉnh cao thuật số Độn Giáp, ngắm mây nhìn khí tượng thấu suốt thiên cơ và căn cốt mệnh số.', '+40 Tuệ Nhãn hiệu dụng; khai mở Thiên Nhãn toàn tri, hiển thị 100% tất cả 12 chỉ số của mọi NPC trong thiên hạ.'),
    (38,  'vong_khi_thien_co', 'Vọng Khí Thiên Cơ', 'special', 5, 'Đỉnh cao thuật số Độn Giáp, ngắm mây nhìn khí tượng thấu suốt thiên cơ và căn cốt mệnh số.', '+40 Tuệ Nhãn hiệu dụng; khai mở Thiên Nhãn toàn tri, hiển thị 100% tất cả 12 chỉ số của mọi NPC trong thiên hạ.');
