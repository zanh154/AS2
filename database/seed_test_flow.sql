-- =====================================================================================
-- Dữ liệu thử cho toàn bộ luồng AIVES: môn học & phiên thi, bộ câu hỏi, câu hỏi đào sâu & kết quả
-- =====================================================================================
-- Chạy SAU khi đã import dump ở database/dump/. Chạy lại bao nhiêu lần cũng được:
-- phần đầu script xoá sạch dữ liệu thử cũ (chỉ dữ liệu gắn mã TFLOW / email
-- tflow.*@test.local), rồi tạo lại với mốc giờ mới.
--
--   mysql -u <user> -p <ten_db> < database/seed_test_flow.sql
--
-- Tài khoản (mật khẩu chung cho mọi tài khoản thử: Test@1234)
--   tflow.gv1@test.local  Giảng viên A  - phụ trách môn TFLOW101 (dùng để test chính)
--   tflow.gv2@test.local  Giảng viên B  - phụ trách môn TFLOW202 (test phân quyền)
--   tflow.sv1@test.local  Sinh viên 1   - lớp TFLOW101-A
--   tflow.sv2@test.local  Sinh viên 2   - lớp TFLOW101-A
--   tflow.sv3@test.local  Sinh viên 3   - lớp TFLOW101-A
--   tflow.sv4@test.local  Sinh viên 4   - lớp TFLOW202-A (không thuộc lớp của GV A)
--
-- Dữ liệu tạo ra
--   Môn TFLOW101 "Lập trình C# (TEST)" - 2 chủ đề (tài liệu), 12 câu Chính, 12 câu Đào sâu
--     Chủ đề 1: tflow-chu-de-1-csharp.pdf   Chủ đề 2: tflow-chu-de-2-linq.pdf
--     Mỗi chủ đề: Chính 3 Dễ / 2 TB / 1 Khó; Đào sâu 2 Dễ / 2 TB / 2 Khó
--   Môn TFLOW202 của GV B, lớp TFLOW202-A có sinh viên 4
--
--   Phiên [TFLOW-1] "Thi ngay"      Đã xếp lịch, 15 phút/SV, 3 câu chính, tối đa 2 câu đào sâu
--       SV1: ca đang MỞ ngay khi chạy script (bắt đầu từ 1 phút trước)
--       SV2: ca tiếp theo (+14 phút), SV3: ca sau nữa (+29 phút)
--   Phiên [TFLOW-2] "Đã thi hôm qua" Đang diễn ra, hôm qua 09:00, 10 phút/SV
--       SV1: Đã thi, có bài làm 2 vòng -> điểm mong đợi 6,3
--       SV2: Đang thi (bỏ ngang, chưa nộp) -> chốt ca sẽ thành Đã thi, 0 điểm
--       SV3: Chờ thi (không đến)           -> chốt ca sẽ thành Vắng
--   Phiên [TFLOW-3] "Tuần sau"       Đã xếp lịch, +7 ngày 09:00 - để sửa / đổi giờ / thêm, xoá SV / xoá phiên
--   Phiên [TFLOW-4] của GV B         Đã xếp lịch, +2 ngày 14:00 - GV A mở phải bị chặn
--
-- Giờ: ứng dụng dùng DateTime.Now (giờ máy chạy app) trong khi MySQL (Aiven) thường chạy
-- giờ UTC, nên mốc giờ được tính theo giờ Việt Nam (UTC+7). Máy chạy app ở múi giờ khác
-- thì sửa @tz_offset bên dưới.
--
-- Tệp PDF: cột file_path trỏ tới /materials/tflow-chu-de-1.pdf và tflow-chu-de-2.pdf.
-- Chép docs/samples/bai-giang-01.pdf vào src/AssignmentPRN.DataAccess/Storage/materials/
-- với 2 tên đó nếu muốn nút "Mở tài liệu" chạy. Phát đề không cần tệp thật.
-- =====================================================================================

SET NAMES utf8mb4;

SET @tz_offset = '+07:00';
SET @now = DATE_FORMAT(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', @tz_offset), '%Y-%m-%d %H:%i:00');
SET @today = DATE(@now);
SET @yesterday = @today - INTERVAL 1 DAY;

-- BCrypt của "Test@1234", sinh bằng BCrypt.Net-Next của chính dự án.
SET @pwd = '$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6';

START TRANSACTION;

-- -------------------------------------------------------------------------------------
-- 0. Xoá dữ liệu thử cũ (theo thứ tự khoá ngoại). Không dùng bảng tạm vì Aiven bật
--    sql_require_primary_key.
-- -------------------------------------------------------------------------------------

DELETE FROM answers
WHERE candidate_id IN (
    SELECT c.candidate_id FROM exam_candidates c
    JOIN exam_sessions s ON s.exam_id = c.exam_id
    JOIN courses co ON co.course_id = s.course_id
    JOIN users lu ON lu.user_id = s.lecturer_id
    JOIN users su ON su.user_id = c.student_id
    WHERE co.course_code IN ('TFLOW101', 'TFLOW202')
       OR lu.email LIKE 'tflow.%@test.local'
       OR su.email LIKE 'tflow.%@test.local');

DELETE FROM exam_questions
WHERE parent_exam_question_id IS NOT NULL
  AND candidate_id IN (
    SELECT c.candidate_id FROM exam_candidates c
    JOIN exam_sessions s ON s.exam_id = c.exam_id
    JOIN courses co ON co.course_id = s.course_id
    JOIN users lu ON lu.user_id = s.lecturer_id
    JOIN users su ON su.user_id = c.student_id
    WHERE co.course_code IN ('TFLOW101', 'TFLOW202')
       OR lu.email LIKE 'tflow.%@test.local'
       OR su.email LIKE 'tflow.%@test.local');

DELETE FROM exam_questions
WHERE candidate_id IN (
    SELECT c.candidate_id FROM exam_candidates c
    JOIN exam_sessions s ON s.exam_id = c.exam_id
    JOIN courses co ON co.course_id = s.course_id
    JOIN users lu ON lu.user_id = s.lecturer_id
    JOIN users su ON su.user_id = c.student_id
    WHERE co.course_code IN ('TFLOW101', 'TFLOW202')
       OR lu.email LIKE 'tflow.%@test.local'
       OR su.email LIKE 'tflow.%@test.local');

DELETE FROM exam_candidates
WHERE student_id IN (SELECT user_id FROM users WHERE email LIKE 'tflow.%@test.local')
   OR exam_id IN (
    SELECT s.exam_id FROM exam_sessions s
    JOIN courses co ON co.course_id = s.course_id
    JOIN users lu ON lu.user_id = s.lecturer_id
    WHERE co.course_code IN ('TFLOW101', 'TFLOW202') OR lu.email LIKE 'tflow.%@test.local');

DELETE FROM exam_sessions
WHERE course_id IN (SELECT course_id FROM courses WHERE course_code IN ('TFLOW101', 'TFLOW202'))
   OR lecturer_id IN (SELECT user_id FROM users WHERE email LIKE 'tflow.%@test.local');

DELETE FROM question_options
WHERE question_id IN (
    SELECT q.question_id FROM questions q
    JOIN courses co ON co.course_id = q.course_id
    WHERE co.course_code IN ('TFLOW101', 'TFLOW202'));
DELETE FROM questions
WHERE course_id IN (SELECT course_id FROM courses WHERE course_code IN ('TFLOW101', 'TFLOW202'));
DELETE FROM course_materials
WHERE course_id IN (SELECT course_id FROM courses WHERE course_code IN ('TFLOW101', 'TFLOW202'));

DELETE FROM class_students
WHERE student_id IN (SELECT user_id FROM users WHERE email LIKE 'tflow.%@test.local')
   OR class_id IN (
    SELECT ac.class_id FROM academic_classes ac
    JOIN courses co ON co.course_id = ac.course_id
    WHERE co.course_code IN ('TFLOW101', 'TFLOW202'));
DELETE FROM academic_classes
WHERE course_id IN (SELECT course_id FROM courses WHERE course_code IN ('TFLOW101', 'TFLOW202'))
   OR lecturer_id IN (SELECT user_id FROM users WHERE email LIKE 'tflow.%@test.local');
DELETE FROM courses WHERE course_code IN ('TFLOW101', 'TFLOW202');
DELETE FROM users WHERE email LIKE 'tflow.%@test.local';

-- -------------------------------------------------------------------------------------
-- 1. Tài khoản
-- -------------------------------------------------------------------------------------
SET @role_lecturer = (SELECT role_id FROM roles WHERE role_name = 'Lecturer' LIMIT 1);
SET @role_student  = (SELECT role_id FROM roles WHERE role_name = 'Student'  LIMIT 1);

INSERT INTO users (role_id, full_name, email, password_hash, status, created_at) VALUES
    (@role_lecturer, '[TFLOW] Giảng viên A', 'tflow.gv1@test.local', @pwd, 'Active', @now);
SET @gv1 = LAST_INSERT_ID();
INSERT INTO users (role_id, full_name, email, password_hash, status, created_at) VALUES
    (@role_lecturer, '[TFLOW] Giảng viên B', 'tflow.gv2@test.local', @pwd, 'Active', @now);
SET @gv2 = LAST_INSERT_ID();
INSERT INTO users (role_id, full_name, email, password_hash, status, created_at) VALUES
    (@role_student, '[TFLOW] Sinh viên 1', 'tflow.sv1@test.local', @pwd, 'Active', @now);
SET @sv1 = LAST_INSERT_ID();
INSERT INTO users (role_id, full_name, email, password_hash, status, created_at) VALUES
    (@role_student, '[TFLOW] Sinh viên 2', 'tflow.sv2@test.local', @pwd, 'Active', @now);
SET @sv2 = LAST_INSERT_ID();
INSERT INTO users (role_id, full_name, email, password_hash, status, created_at) VALUES
    (@role_student, '[TFLOW] Sinh viên 3', 'tflow.sv3@test.local', @pwd, 'Active', @now);
SET @sv3 = LAST_INSERT_ID();
INSERT INTO users (role_id, full_name, email, password_hash, status, created_at) VALUES
    (@role_student, '[TFLOW] Sinh viên 4', 'tflow.sv4@test.local', @pwd, 'Active', @now);
SET @sv4 = LAST_INSERT_ID();

-- -------------------------------------------------------------------------------------
-- 2. Môn học, lớp học
-- -------------------------------------------------------------------------------------
INSERT INTO courses (course_code, course_name, description, lecturer_id, is_active, created_at) VALUES
    ('TFLOW101', 'Lập trình C# (TEST)', 'Môn dữ liệu thử cho luồng thi vấn đáp.', @gv1, 1, @now);
SET @c1 = LAST_INSERT_ID();
INSERT INTO courses (course_code, course_name, description, lecturer_id, is_active, created_at) VALUES
    ('TFLOW202', 'Cơ sở dữ liệu (TEST)', 'Môn của giảng viên B, dùng để thử phân quyền.', @gv2, 1, @now);
SET @c2 = LAST_INSERT_ID();

INSERT INTO academic_classes (class_code, class_name, course_id, lecturer_id, is_active, created_at) VALUES
    ('TFLOW101-A', 'Lớp thử C# - nhóm A', @c1, @gv1, 1, @now);
SET @cls1 = LAST_INSERT_ID();
INSERT INTO academic_classes (class_code, class_name, course_id, lecturer_id, is_active, created_at) VALUES
    ('TFLOW202-A', 'Lớp thử CSDL - nhóm A', @c2, @gv2, 1, @now);
SET @cls2 = LAST_INSERT_ID();

INSERT INTO class_students (class_id, student_id, joined_at) VALUES
    (@cls1, @sv1, @now), (@cls1, @sv2, @now), (@cls1, @sv3, @now),
    (@cls2, @sv4, @now);

-- -------------------------------------------------------------------------------------
-- 3. Tài liệu (chủ đề)
-- -------------------------------------------------------------------------------------
INSERT INTO course_materials (course_id, file_name, file_path, file_type, file_size, uploaded_by, processing_status, uploaded_at) VALUES
    (@c1, 'tflow-chu-de-1-csharp.pdf', '/materials/tflow-chu-de-1.pdf', 'PDF', 1024, @gv1, 'Completed', @now);
SET @m1 = LAST_INSERT_ID();
INSERT INTO course_materials (course_id, file_name, file_path, file_type, file_size, uploaded_by, processing_status, uploaded_at) VALUES
    (@c1, 'tflow-chu-de-2-linq.pdf', '/materials/tflow-chu-de-2.pdf', 'PDF', 1024, @gv1, 'Completed', @now);
SET @m2 = LAST_INSERT_ID();

-- -------------------------------------------------------------------------------------
-- 4. Câu hỏi CHÍNH (question_type = Main)
-- -------------------------------------------------------------------------------------
-- Chủ đề 1 - C# cơ bản
INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, 'Trong C#, kiểu nào lưu số nguyên 32 bit có dấu?', 'int (System.Int32).', 'Remember', 'Easy', 'Main', 'Approved', @now);
SET @q_m1 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m1, 'int', 1, 1), (@q_m1, 'long', 0, 2), (@q_m1, 'short', 0, 3), (@q_m1, 'byte', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, 'Từ khoá nào dùng để gọi hàm khởi tạo của lớp cha?', 'base(...) đặt sau khai báo hàm khởi tạo.', 'Remember', 'Easy', 'Main', 'Approved', @now);
SET @q_m2 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m2, 'this', 0, 1), (@q_m2, 'base', 1, 2), (@q_m2, 'super', 0, 3), (@q_m2, 'parent', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, 'Phương thức nào là điểm bắt đầu của một chương trình console C#?', 'static void Main (hoặc top-level statements).', 'Remember', 'Easy', 'Main', 'Approved', @now);
SET @q_m3 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m3, 'Start', 0, 1), (@q_m3, 'Run', 0, 2), (@q_m3, 'Main', 1, 3), (@q_m3, 'Init', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, 'Khác biệt chính giữa class và struct trong C# là gì?', 'class là kiểu tham chiếu, struct là kiểu giá trị (sao chép khi gán).', 'Understand', 'Medium', 'Main', 'Approved', @now);
SET @q_m4 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m4, 'class là kiểu tham chiếu, struct là kiểu giá trị', 1, 1),
    (@q_m4, 'struct không được có phương thức', 0, 2),
    (@q_m4, 'class không có hàm khởi tạo', 0, 3),
    (@q_m4, 'struct luôn được cấp phát trên heap', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, 'Cần những từ khoá nào để lớp con ghi đè phương thức của lớp cha?', 'virtual (hoặc abstract) ở lớp cha, override ở lớp con.', 'Understand', 'Medium', 'Main', 'Approved', @now);
SET @q_m5 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m5, 'virtual ở lớp cha và override ở lớp con', 1, 1),
    (@q_m5, 'static ở lớp cha và new ở lớp con', 0, 2),
    (@q_m5, 'sealed ở lớp cha và override ở lớp con', 0, 3),
    (@q_m5, 'abstract ở lớp con', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, 'Khi boxing một giá trị int, điều gì xảy ra?', 'Giá trị được sao chép vào một object mới cấp phát trên heap.', 'Analyze', 'Hard', 'Main', 'Approved', @now);
SET @q_m6 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m6, 'Biến int trở thành tham chiếu tới stack', 0, 1),
    (@q_m6, 'Không có cấp phát bộ nhớ nào', 0, 2),
    (@q_m6, 'Giá trị được sao chép vào một object trên heap', 1, 3),
    (@q_m6, 'int bị chuyển thành string', 0, 4);

-- Chủ đề 2 - LINQ và collection
INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, 'Phương thức LINQ nào lọc phần tử theo điều kiện?', 'Where.', 'Remember', 'Easy', 'Main', 'Approved', @now);
SET @q_m7 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m7, 'Where', 1, 1), (@q_m7, 'Select', 0, 2), (@q_m7, 'OrderBy', 0, 3), (@q_m7, 'GroupBy', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, 'Collection nào lưu các cặp khoá - giá trị?', 'Dictionary<TKey, TValue>.', 'Remember', 'Easy', 'Main', 'Approved', @now);
SET @q_m8 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m8, 'List<T>', 0, 1), (@q_m8, 'Queue<T>', 0, 2), (@q_m8, 'Stack<T>', 0, 3), (@q_m8, 'Dictionary<TKey, TValue>', 1, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, 'Phương thức LINQ nào biến đổi mỗi phần tử sang một dạng mới?', 'Select.', 'Remember', 'Easy', 'Main', 'Approved', @now);
SET @q_m9 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m9, 'Where', 0, 1), (@q_m9, 'Select', 1, 2), (@q_m9, 'Any', 0, 3), (@q_m9, 'Count', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, 'Truy vấn LINQ trên IEnumerable được thực thi khi nào?', 'Khi được duyệt (deferred execution), không phải lúc khai báo.', 'Understand', 'Medium', 'Main', 'Approved', @now);
SET @q_m10 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m10, 'Ngay khi khai báo truy vấn', 0, 1),
    (@q_m10, 'Khi truy vấn được duyệt (deferred execution)', 1, 2),
    (@q_m10, 'Lúc biên dịch', 0, 3),
    (@q_m10, 'Khi bộ thu gom rác chạy', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, 'Khi chuỗi rỗng, First() khác FirstOrDefault() ở điểm nào?', 'First() ném InvalidOperationException, FirstOrDefault() trả về default.', 'Understand', 'Medium', 'Main', 'Approved', @now);
SET @q_m11 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m11, 'Cả hai đều trả về null', 0, 1),
    (@q_m11, 'Cả hai đều ném ngoại lệ', 0, 2),
    (@q_m11, 'First() ném ngoại lệ, FirstOrDefault() trả về giá trị mặc định', 1, 3),
    (@q_m11, 'First() trả về giá trị mặc định, FirstOrDefault() ném ngoại lệ', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, 'Vì sao nên giữ IQueryable thay vì IEnumerable khi truy vấn EF Core?', 'Điều kiện được dịch sang SQL, lọc ngay tại database.', 'Analyze', 'Hard', 'Main', 'Approved', @now);
SET @q_m12 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_m12, 'Điều kiện được dịch sang SQL và lọc ở database', 1, 1),
    (@q_m12, 'IQueryable chạy trong bộ nhớ nhanh hơn', 0, 2),
    (@q_m12, 'IEnumerable không hỗ trợ Where', 0, 3),
    (@q_m12, 'IQueryable không cần kết nối database', 0, 4);

-- -------------------------------------------------------------------------------------
-- 5. Câu hỏi ĐÀO SÂU (question_type = FollowUp), bắt buộc có chủ đề
-- -------------------------------------------------------------------------------------
-- Chủ đề 1
INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, '[Đào sâu] Kiểu bool nhận những giá trị nào?', 'true và false.', 'Remember', 'Easy', 'FollowUp', 'Approved', @now);
SET @q_f1 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f1, 'true và false', 1, 1), (@q_f1, '0, 1 và 2', 0, 2), (@q_f1, 'yes và no', 0, 3), (@q_f1, 'on và off', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, '[Đào sâu] Toán tử nào so sánh bằng trong C#?', '==', 'Remember', 'Easy', 'FollowUp', 'Approved', @now);
SET @q_f2 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f2, '=', 0, 1), (@q_f2, '==', 1, 2), (@q_f2, '===', 0, 3), (@q_f2, ':=', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, '[Đào sâu] Có thể dùng new để tạo thể hiện trực tiếp của lớp abstract không?', 'Không, chỉ tạo được từ lớp con cụ thể.', 'Understand', 'Medium', 'FollowUp', 'Approved', @now);
SET @q_f3 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f3, 'Có, như lớp thường', 0, 1),
    (@q_f3, 'Không, chỉ lớp con cụ thể mới tạo được', 1, 2),
    (@q_f3, 'Chỉ khi có hàm khởi tạo public', 0, 3),
    (@q_f3, 'Chỉ trong cùng namespace', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, '[Đào sâu] interface trong C# dùng để làm gì?', 'Định nghĩa hợp đồng các thành viên mà lớp cài đặt phải có.', 'Understand', 'Medium', 'FollowUp', 'Approved', @now);
SET @q_f4 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f4, 'Lưu trữ dữ liệu dùng chung', 0, 1),
    (@q_f4, 'Thay thế cho struct', 0, 2),
    (@q_f4, 'Định nghĩa hợp đồng các thành viên mà lớp phải cài đặt', 1, 3),
    (@q_f4, 'Tạo luồng xử lý mới', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, '[Đào sâu] Equals mặc định của struct so sánh điều gì?', 'So sánh giá trị từng trường.', 'Analyze', 'Hard', 'FollowUp', 'Approved', @now);
SET @q_f5 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f5, 'Giá trị từng trường', 1, 1),
    (@q_f5, 'Địa chỉ bộ nhớ', 0, 2),
    (@q_f5, 'Luôn trả về false', 0, 3),
    (@q_f5, 'Chỉ so sánh tên kiểu', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m1, @gv1, '[Đào sâu] Đặt sealed lên một lớp có tác dụng gì?', 'Ngăn không cho lớp khác kế thừa.', 'Analyze', 'Hard', 'FollowUp', 'Approved', @now);
SET @q_f6 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f6, 'Ngăn tạo thể hiện', 0, 1),
    (@q_f6, 'Biến lớp thành static', 0, 2),
    (@q_f6, 'Ẩn lớp khỏi assembly khác', 0, 3),
    (@q_f6, 'Ngăn lớp khác kế thừa', 1, 4);

-- Chủ đề 2
INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, '[Đào sâu] Phương thức LINQ nào đếm số phần tử?', 'Count.', 'Remember', 'Easy', 'FollowUp', 'Approved', @now);
SET @q_f7 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f7, 'Sum', 0, 1), (@q_f7, 'Count', 1, 2), (@q_f7, 'Max', 0, 3), (@q_f7, 'Length', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, '[Đào sâu] Thêm một phần tử vào List<T> bằng phương thức nào?', 'Add.', 'Remember', 'Easy', 'FollowUp', 'Approved', @now);
SET @q_f8 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f8, 'Add', 1, 1), (@q_f8, 'Push', 0, 2), (@q_f8, 'Enqueue', 0, 3), (@q_f8, 'Put', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, '[Đào sâu] Any(điều kiện) trả về gì?', 'true nếu có ít nhất một phần tử thoả điều kiện.', 'Understand', 'Medium', 'FollowUp', 'Approved', @now);
SET @q_f9 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f9, 'true nếu có ít nhất một phần tử thoả điều kiện', 1, 1),
    (@q_f9, 'Phần tử đầu tiên thoả điều kiện', 0, 2),
    (@q_f9, 'Số phần tử thoả điều kiện', 0, 3),
    (@q_f9, 'Một danh sách rỗng', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, '[Đào sâu] GroupBy trả về kiểu gì?', 'Một chuỗi IGrouping<TKey, TElement>.', 'Understand', 'Medium', 'FollowUp', 'Approved', @now);
SET @q_f10 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f10, 'Dictionary<TKey, TElement>', 0, 1),
    (@q_f10, 'List<TKey>', 0, 2),
    (@q_f10, 'Tập các IGrouping<TKey, TElement>', 1, 3),
    (@q_f10, 'Một phần tử duy nhất', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, '[Đào sâu] Gọi ToList() ở giữa truy vấn EF Core gây ra điều gì?', 'Truy vấn chạy ngay; phần phía sau xử lý trong bộ nhớ.', 'Analyze', 'Hard', 'FollowUp', 'Approved', @now);
SET @q_f11 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f11, 'Không có ảnh hưởng gì', 0, 1),
    (@q_f11, 'Truy vấn chạy ngay, phần sau xử lý trong bộ nhớ', 1, 2),
    (@q_f11, 'Truy vấn chạy trên server nhanh hơn', 0, 3),
    (@q_f11, 'Database tự thêm index', 0, 4);

INSERT INTO questions (course_id, source_material_id, created_by, question_text, expected_answer, bloom_level, difficulty, question_type, status, created_at) VALUES
    (@c1, @m2, @gv1, '[Đào sâu] Vì sao duyệt cùng một IQueryable hai lần có thể gọi database hai lần?', 'Deferred execution: mỗi lần duyệt thực thi lại truy vấn.', 'Analyze', 'Hard', 'FollowUp', 'Approved', @now);
SET @q_f12 = LAST_INSERT_ID();
INSERT INTO question_options (question_id, option_text, is_correct, display_order) VALUES
    (@q_f12, 'Do cache của EF bị tắt', 0, 1),
    (@q_f12, 'Do bộ thu gom rác', 0, 2),
    (@q_f12, 'Không bao giờ xảy ra', 0, 3),
    (@q_f12, 'Mỗi lần duyệt thực thi lại truy vấn (deferred execution)', 1, 4);

-- -------------------------------------------------------------------------------------
-- 6. Phiên [TFLOW-1] - thi ngay: ca của SV1 đang mở
-- -------------------------------------------------------------------------------------
SET @s1_start = @now - INTERVAL 1 MINUTE;
INSERT INTO exam_sessions (course_id, lecturer_id, exam_name, description, start_time, end_time, time_per_student, main_question_count, max_follow_up_count, status, created_at) VALUES
    (@c1, @gv1, '[TFLOW-1] Thi ngay', 'SV1 vào thi được ngay; SV2, SV3 đợi tới ca của mình.',
     @s1_start, @s1_start + INTERVAL 45 MINUTE, 15, 3, 2, 'Scheduled', @now);
SET @s1 = LAST_INSERT_ID();
INSERT INTO exam_candidates (exam_id, student_id, scheduled_time, status) VALUES
    (@s1, @sv1, @s1_start, 'Waiting'),
    (@s1, @sv2, @s1_start + INTERVAL 15 MINUTE, 'Waiting'),
    (@s1, @sv3, @s1_start + INTERVAL 30 MINUTE, 'Waiting');

-- -------------------------------------------------------------------------------------
-- 7. Phiên [TFLOW-2] - đã thi hôm qua, để test chốt ca và kết quả
-- -------------------------------------------------------------------------------------
SET @s2_start = @yesterday + INTERVAL 9 HOUR;
INSERT INTO exam_sessions (course_id, lecturer_id, exam_name, description, start_time, end_time, time_per_student, main_question_count, max_follow_up_count, status, created_at) VALUES
    (@c1, @gv1, '[TFLOW-2] Đã thi hôm qua', 'SV1 đã thi xong, SV2 bỏ ngang, SV3 không đến. Dùng để thử Chốt ca và Kết quả.',
     @s2_start, @s2_start + INTERVAL 30 MINUTE, 10, 3, 2, 'InProgress', @now);
SET @s2 = LAST_INSERT_ID();

-- SV1: đã hoàn thành 2 vòng
SET @s2_sv1_start = @s2_start;
SET @s2_round1_end = @s2_start + INTERVAL 5 MINUTE;
SET @s2_round2_end = @s2_start + INTERVAL 8 MINUTE;
INSERT INTO exam_candidates (exam_id, student_id, scheduled_time, status, started_at, finished_at) VALUES
    (@s2, @sv1, @s2_sv1_start, 'Completed', @s2_sv1_start, @s2_round2_end);
SET @s2_cand1 = LAST_INSERT_ID();

-- SV2: đã vào thi, có đề nhưng bỏ ngang không nộp
INSERT INTO exam_candidates (exam_id, student_id, scheduled_time, status, started_at) VALUES
    (@s2, @sv2, @s2_start + INTERVAL 10 MINUTE, 'InProgress', @s2_start + INTERVAL 10 MINUTE);
SET @s2_cand2 = LAST_INSERT_ID();

-- SV3: không đến
INSERT INTO exam_candidates (exam_id, student_id, scheduled_time, status) VALUES
    (@s2, @sv3, @s2_start + INTERVAL 20 MINUTE, 'Waiting');

-- Đề vòng 1 của SV1: M4 (chủ đề 1, TB), M5 (chủ đề 1, TB), M7 (chủ đề 2, Dễ)
INSERT INTO exam_questions (exam_id, candidate_id, question_id, parent_exam_question_id, order_no, asked_at, is_completed) VALUES
    (@s2, @s2_cand1, @q_m4, NULL, 1, @s2_round1_end, 1);
SET @eq1 = LAST_INSERT_ID();
INSERT INTO exam_questions (exam_id, candidate_id, question_id, parent_exam_question_id, order_no, asked_at, is_completed) VALUES
    (@s2, @s2_cand1, @q_m5, NULL, 2, @s2_round1_end, 1);
SET @eq2 = LAST_INSERT_ID();
INSERT INTO exam_questions (exam_id, candidate_id, question_id, parent_exam_question_id, order_no, asked_at, is_completed) VALUES
    (@s2, @s2_cand1, @q_m7, NULL, 3, @s2_round1_end, 1);
SET @eq3 = LAST_INSERT_ID();

-- Vòng 2 đúng theo luật FollowUpPlanner: câu sai (M5, TB) -> câu Dễ cùng chủ đề 1 (F1);
-- câu đúng (M7, Dễ) -> câu khó hơn cùng chủ đề 2 (F9, TB).
INSERT INTO exam_questions (exam_id, candidate_id, question_id, parent_exam_question_id, order_no, asked_at, is_completed) VALUES
    (@s2, @s2_cand1, @q_f1, @eq2, 4, @s2_round1_end, 1);
SET @eq4 = LAST_INSERT_ID();
INSERT INTO exam_questions (exam_id, candidate_id, question_id, parent_exam_question_id, order_no, asked_at, is_completed) VALUES
    (@s2, @s2_cand1, @q_f9, @eq3, 5, @s2_round1_end, 1);
SET @eq5 = LAST_INSERT_ID();

-- Bài làm SV1: M4 đúng, M5 sai, M7 đúng | F1 đúng, F9 sai
-- Điểm = (1 + 0 + 1 + 0,5 + 0) / (3 + 0,5 + 0,5) x 10 = 6,25 -> 6,3
INSERT INTO answers (exam_question_id, candidate_id, selected_option_id, started_at, finished_at, created_at) VALUES
    (@eq1, @s2_cand1, (SELECT option_id FROM question_options WHERE question_id = @q_m4 AND is_correct = 1 LIMIT 1), @s2_sv1_start, @s2_round1_end, @s2_round1_end),
    (@eq2, @s2_cand1, (SELECT option_id FROM question_options WHERE question_id = @q_m5 AND is_correct = 0 ORDER BY display_order LIMIT 1), @s2_sv1_start, @s2_round1_end, @s2_round1_end),
    (@eq3, @s2_cand1, (SELECT option_id FROM question_options WHERE question_id = @q_m7 AND is_correct = 1 LIMIT 1), @s2_sv1_start, @s2_round1_end, @s2_round1_end),
    (@eq4, @s2_cand1, (SELECT option_id FROM question_options WHERE question_id = @q_f1 AND is_correct = 1 LIMIT 1), @s2_round1_end, @s2_round2_end, @s2_round2_end),
    (@eq5, @s2_cand1, (SELECT option_id FROM question_options WHERE question_id = @q_f9 AND is_correct = 0 ORDER BY display_order LIMIT 1), @s2_round1_end, @s2_round2_end, @s2_round2_end);

-- Đề của SV2 (chưa nộp, chưa có câu trả lời): M1, M8, M12
INSERT INTO exam_questions (exam_id, candidate_id, question_id, parent_exam_question_id, order_no, asked_at, is_completed) VALUES
    (@s2, @s2_cand2, @q_m1, NULL, 1, NULL, 0),
    (@s2, @s2_cand2, @q_m8, NULL, 2, NULL, 0),
    (@s2, @s2_cand2, @q_m12, NULL, 3, NULL, 0);

-- -------------------------------------------------------------------------------------
-- 8. Phiên [TFLOW-3] - tuần sau, để sửa / đổi giờ / thêm, xoá SV / xoá phiên
-- -------------------------------------------------------------------------------------
SET @s3_start = @today + INTERVAL 7 DAY + INTERVAL 9 HOUR;
INSERT INTO exam_sessions (course_id, lecturer_id, exam_name, description, start_time, end_time, time_per_student, main_question_count, max_follow_up_count, status, created_at) VALUES
    (@c1, @gv1, '[TFLOW-3] Tuần sau', 'Mọi SV còn Chờ thi: sửa, đổi giờ, thêm/xoá SV, xoá phiên đều được.',
     @s3_start, @s3_start + INTERVAL 60 MINUTE, 20, 4, 2, 'Scheduled', @now);
SET @s3 = LAST_INSERT_ID();
INSERT INTO exam_candidates (exam_id, student_id, scheduled_time, status) VALUES
    (@s3, @sv1, @s3_start, 'Waiting'),
    (@s3, @sv2, @s3_start + INTERVAL 20 MINUTE, 'Waiting'),
    (@s3, @sv3, @s3_start + INTERVAL 40 MINUTE, 'Waiting');

-- -------------------------------------------------------------------------------------
-- 9. Phiên [TFLOW-4] của giảng viên B - để thử phân quyền
-- -------------------------------------------------------------------------------------
SET @s4_start = @today + INTERVAL 2 DAY + INTERVAL 14 HOUR;
INSERT INTO exam_sessions (course_id, lecturer_id, exam_name, description, start_time, end_time, time_per_student, main_question_count, max_follow_up_count, status, created_at) VALUES
    (@c2, @gv2, '[TFLOW-4] Phiên của GV B', 'Giảng viên A mở phiên này phải bị chặn.',
     @s4_start, @s4_start + INTERVAL 15 MINUTE, 15, 3, 0, 'Scheduled', @now);
SET @s4 = LAST_INSERT_ID();
INSERT INTO exam_candidates (exam_id, student_id, scheduled_time, status) VALUES
    (@s4, @sv4, @s4_start, 'Waiting');

COMMIT;


-- -------------------------------------------------------------------------------------
-- Kiểm tra nhanh sau khi chạy
-- -------------------------------------------------------------------------------------
-- Mong đợi: 2 GV, 4 SV
SELECT r.role_name, COUNT(*) AS so_tai_khoan
FROM users u JOIN roles r ON r.role_id = u.role_id
WHERE u.email LIKE 'tflow.%@test.local' GROUP BY r.role_name;

-- Mong đợi: Main 12, FollowUp 12
SELECT question_type, COUNT(*) AS so_cau
FROM questions WHERE course_id = @c1 GROUP BY question_type;

-- Mong đợi: 4 phiên; TFLOW-1 ca SV1 đang mở ở thời điểm @now
SELECT s.exam_name, s.status, c.scheduled_time, u.email, c.status AS trang_thai_sv, @now AS gio_hien_tai
FROM exam_sessions s
JOIN exam_candidates c ON c.exam_id = s.exam_id
JOIN users u ON u.user_id = c.student_id
WHERE s.exam_id IN (@s1, @s2, @s3, @s4)
ORDER BY s.exam_id, c.scheduled_time;
