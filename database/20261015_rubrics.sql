-- ASM2 rubric tables; additive migration. Run after the ASM1 schema.
CREATE TABLE IF NOT EXISTS `rubrics` (
    `rubric_id` int NOT NULL AUTO_INCREMENT,
    `question_id` int NOT NULL,
    `total_score` decimal(5,2) NOT NULL DEFAULT 0.0,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    CONSTRAINT `PK_rubrics` PRIMARY KEY (`rubric_id`),
    UNIQUE KEY `IX_rubrics_question_id` (`question_id`),
    CONSTRAINT `FK_rubrics_questions_question_id` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;



CREATE TABLE IF NOT EXISTS `rubric_criteria` (
    `criteria_id` int NOT NULL AUTO_INCREMENT,
    `rubric_id` int NOT NULL,
    `criteria_name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `max_score` decimal(5,2) NOT NULL,
    `display_order` int NOT NULL DEFAULT 1,
    CONSTRAINT `PK_rubric_criteria` PRIMARY KEY (`criteria_id`),
    CONSTRAINT `FK_rubric_criteria_rubrics_rubric_id` FOREIGN KEY (`rubric_id`) REFERENCES `rubrics` (`rubric_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;
