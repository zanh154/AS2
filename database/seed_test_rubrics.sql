-- =====================================================================================
-- Rubric cho dữ liệu thử TFLOW — chạy SAU seed_test_flow.sql.
--
-- AI chỉ chấm câu có rubric; không có rubric thì bài được để trống cho giảng viên chấm.
-- seed_test_flow.sql không tạo rubric, và mỗi lần chạy lại nó thì câu hỏi TFLOW bị tạo
-- mới nên rubric cũ bị xoá theo (rubrics.question_id ON DELETE CASCADE). Chạy lại file
-- này sau mỗi lần seed.
--
-- Mỗi câu Chính của TFLOW101/TFLOW202 nhận 3 tiêu chí, tổng 10 điểm. Chạy nhiều lần cũng
-- không sao: câu đã có rubric thì bỏ qua. Chỉ đụng câu hỏi của môn TFLOW*.
-- =====================================================================================

SET NAMES utf8mb4;

INSERT INTO rubrics (question_id, total_score)
SELECT q.question_id, 10
FROM questions q
JOIN courses c ON c.course_id = q.course_id
WHERE c.course_code IN ('TFLOW101', 'TFLOW202')
  AND q.question_type = 'Main'
  AND NOT EXISTS (SELECT 1 FROM rubrics r WHERE r.question_id = q.question_id);

INSERT INTO rubric_criteria (rubric_id, criteria_name, description, max_score, display_order)
SELECT r.rubric_id, x.criteria_name, x.description, x.max_score, x.display_order
FROM rubrics r
JOIN questions q ON q.question_id = r.question_id
JOIN courses c ON c.course_id = q.course_id
CROSS JOIN (
    SELECT 'Đúng khái niệm' AS criteria_name, 'Nêu đúng định nghĩa, bản chất của khái niệm được hỏi' AS description, 5 AS max_score, 1 AS display_order
    UNION ALL SELECT 'Ví dụ minh hoạ', 'Đưa ra ví dụ cụ thể, đúng ngữ cảnh', 3, 2
    UNION ALL SELECT 'Trình bày rõ ràng', 'Trình bày mạch lạc, đúng trọng tâm', 2, 3
) x
WHERE c.course_code IN ('TFLOW101', 'TFLOW202')
  AND NOT EXISTS (SELECT 1 FROM rubric_criteria rc WHERE rc.rubric_id = r.rubric_id);

SELECT COUNT(*) AS so_cau_co_rubric
FROM rubrics r
JOIN questions q ON q.question_id = r.question_id
JOIN courses c ON c.course_id = q.course_id
WHERE c.course_code IN ('TFLOW101', 'TFLOW202');
