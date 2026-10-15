-- =====================================================================================
-- Nâng cấp schema: duyệt câu hỏi AI sinh + bảng thông báo
-- Ngày: 2026-10-14
--
-- Chỉ cần chạy khi database đã có dữ liệu (ví dụ DB Aiven dùng chung).
-- Database import MỚI từ database/dump/ thì KHÔNG cần chạy file này — dump đã bao gồm
-- toàn bộ thay đổi dưới đây.
--
-- Script chạy lại nhiều lần cũng được: mỗi thay đổi đều kiểm tra tồn tại trước khi làm.
--
--   mysql -u <user> -p <ten_db> < database/20261014_add_question_review_and_notifications.sql
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 1. questions: nguồn câu hỏi + vết duyệt
-- -------------------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS aives_add_column_if_missing;
DELIMITER //
CREATE PROCEDURE aives_add_column_if_missing(
    IN p_table VARCHAR(64),
    IN p_column VARCHAR(64),
    IN p_definition TEXT)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = p_table
          AND column_name = p_column)
    THEN
        SET @ddl = CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN ', p_definition);
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END //
DELIMITER ;

CALL aives_add_column_if_missing('questions', 'source',
    '`source` enum(''Manual'',''AI'') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''Manual'' AFTER `status`');
CALL aives_add_column_if_missing('questions', 'reviewed_by',
    '`reviewed_by` int DEFAULT NULL AFTER `source`');
CALL aives_add_column_if_missing('questions', 'reviewed_at',
    '`reviewed_at` datetime DEFAULT NULL AFTER `reviewed_by`');

DROP PROCEDURE aives_add_column_if_missing;

-- Index và khoá ngoại: thêm nếu chưa có.
DROP PROCEDURE IF EXISTS aives_add_index_if_missing;
DELIMITER //
CREATE PROCEDURE aives_add_index_if_missing(
    IN p_table VARCHAR(64),
    IN p_index VARCHAR(64),
    IN p_definition TEXT)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = p_table
          AND index_name = p_index)
    AND NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = DATABASE()
          AND table_name = p_table
          AND constraint_name = p_index
          AND constraint_type = 'FOREIGN KEY')
    THEN
        SET @ddl = CONCAT('ALTER TABLE `', p_table, '` ADD ', p_definition);
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END //
DELIMITER ;

CALL aives_add_index_if_missing('questions', 'idx_questions_source',
    'KEY `idx_questions_source` (`source`)');
CALL aives_add_index_if_missing('questions', 'idx_questions_reviewer',
    'KEY `idx_questions_reviewer` (`reviewed_by`)');
CALL aives_add_index_if_missing('questions', 'fk_questions_reviewer',
    'CONSTRAINT `fk_questions_reviewer` FOREIGN KEY (`reviewed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL');

DROP PROCEDURE aives_add_index_if_missing;

-- Câu hỏi cũ là do giảng viên soạn nên giữ nguyên 'Manual' và trạng thái đã có.

-- -------------------------------------------------------------------------------------
-- 2. notifications: thông báo cho giảng viên khi ngân hàng / cấu hình đề thay đổi
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `notifications` (
  `notification_id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `course_id` int DEFAULT NULL,
  `exam_id` int DEFAULT NULL,
  `type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `message` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_read` tinyint(1) NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`notification_id`),
  KEY `idx_notifications_user_read` (`user_id`,`is_read`),
  KEY `idx_notifications_course` (`course_id`),
  KEY `idx_notifications_exam` (`exam_id`),
  CONSTRAINT `fk_notifications_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_notifications_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_notifications_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -------------------------------------------------------------------------------------
-- 3. Kiểm tra nhanh
-- -------------------------------------------------------------------------------------
SELECT column_name, column_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'questions'
  AND column_name IN ('source', 'reviewed_by', 'reviewed_at');

SELECT COUNT(*) AS notifications_table_ready
FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_name = 'notifications';
