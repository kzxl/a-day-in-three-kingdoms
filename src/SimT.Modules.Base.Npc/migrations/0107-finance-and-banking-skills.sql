-- 0107-finance-and-banking-skills.sql — Commercial, Banking, and Financial Hegemony Skills.

INSERT OR IGNORE INTO np_skill (npc_id, code, name_vi, category, tier, description, effect_summary, level) VALUES
    -- 1. Thấu Thị Kim Khố (Insight into Cash Flows & Vault Reserves)
    (31, 'thau_thi_kim_kho', 'Thấu Thị Kim Khố', 'special', 3, 'Nhãn lực soi tỏ kim khố, nắm rõ lượng ngân lượng dự trữ và dòng tiền biến động của chư hầu và phú gia.', 'Hiển thị chính xác thực lực tài chính và nhu cầu vay nợ của NPC', 4),
    -- 2. Ngân Phiếu Tín Khế (Sovereign Bank Draft Master)
    (31, 'ngan_phieu_tin_khe', 'Ngân Phiếu Tín Khế', 'special', 4, 'Bí thuật khắc bản in ngân phiếu bảo mật cao, xây dựng niềm tin sắt đá trong giới thương đoàn.', 'Tăng 25% uy tín ngân hiệu và tăng hạn mức lưu thông ngân phiếu tự phát hành', 5),
    -- 3. Đòn Bẩy Ngân Khố (Fractional Reserve Mastery)
    (7, 'phan_doan_du_tru', 'Đòn Bẩy Ngân Khố', 'special', 4, 'Nghệ thuật vận dụng đòn bẩy dự trữ phân đoạn, quay vòng vốn tối đa mà không gây hoảng loạn rút tiền.', 'Cho phép duy trì tỷ lệ dự trữ an toàn ở mức 30%, tối ưu hóa dòng vốn cho vay', 4),
    -- 4. Định Giá Tín Dụng (Sovereign Credit Pricing)
    (7, 'dinh_gia_tin_dung', 'Định Giá Tín Dụng', 'special', 3, 'Tài đàm phán khế ước vay mượn với các tướng lĩnh và lãnh chúa, tối ưu hóa lợi nhuận kỳ phiếu.', 'Tăng biên lợi nhuận lãi suất cho vay thêm +3% mà không làm giảm hảo cảm', 4),
    -- 5. Tiêu Cục Thiết Giáp (Armored Bullion Convoy)
    (9, 'tieu_cuc_thiet_giap', 'Tiêu Cục Thiết Giáp', 'special', 4, 'Huấn luyện tiêu sư võ nghệ cao cường áp tải ngân lượng, thiết lập tuyến đường an toàn tuyệt đối.', 'Giảm 85% nguy cơ đoàn tiêu cục chở bạc bị sơn tặc phục kích cướp bóc', 4),
    -- 6. Thiết Diện Truy Trưng (Inevitable Collateral Seizure)
    (2, 'thiet_dien_truy_trung', 'Thiết Diện Truy Trưng', 'special', 4, 'Bản lĩnh siết nợ lạnh lùng theo đúng khế ước, thu hồi ruộng vườn quầy hàng của con nợ quá hạn.', 'Cho phép tịch thu thế chấp tài sản của người nợ quá hạn mà không bùng phát bạo loạn', 3),
    -- 7. Diêm Thiết Độc Quyền (Salt & Iron Monopoly)
    (31, 'kiem_soat_diem_thiet', 'Diêm Thiết Độc Quyền', 'special', 5, 'Nắm giữ môn bài quan thương độc quyền phân phối muối và sắt, thao túng huyết mạch kinh tế thiên hạ.', 'Mở khóa đặc quyền kinh doanh độc quyền Diêm Thiết và giảm thuế quan liên vùng', 5);
