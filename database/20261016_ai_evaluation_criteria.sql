-- ASM2 phần 6: điểm AI theo từng tiêu chí rubric.
-- Additive, chạy một lần, an toàn khi chạy lại. Không sửa hay xoá bảng nào có sẵn.
--
-- `ai_evaluations` đã có trong dump nhưng chỉ giữ một điểm tổng cho mỗi câu trả lời.
-- Đề bài yêu cầu chấm theo rubric và lưu điểm từng tiêu chí, nên mỗi tiêu chí được
-- AI chấm là một dòng ở đây, trỏ về `rubric_criteria` của câu hỏi.
CREATE TABLE IF NOT EXISTS `ai_evaluation_criteria` (
    `evaluation_criteria_id` int NOT NULL AUTO_INCREMENT,
    `evaluation_id` int NOT NULL,
    `criteria_id` int NOT NULL,
    `score` decimal(5,2) NOT NULL,
    `comment` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    PRIMARY KEY (`evaluation_criteria_id`),
    -- Một tiêu chí chỉ được chấm một lần trong một lần đánh giá: chấm lại thì ghi đè.
    UNIQUE KEY `ux_eval_criteria` (`evaluation_id`, `criteria_id`),
    KEY `idx_eval_criteria_evaluation` (`evaluation_id`),
    KEY `idx_eval_criteria_criteria` (`criteria_id`),
    CONSTRAINT `fk_eval_criteria_evaluation` FOREIGN KEY (`evaluation_id`)
        REFERENCES `ai_evaluations` (`evaluation_id`) ON DELETE CASCADE,
    CONSTRAINT `fk_eval_criteria_criteria` FOREIGN KEY (`criteria_id`)
        REFERENCES `rubric_criteria` (`criteria_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
