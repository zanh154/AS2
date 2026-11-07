-- ASM2: run once per database; safe to re-run when the table already exists.
CREATE TABLE IF NOT EXISTS exam_viva_settings (
    exam_id INT NOT NULL PRIMARY KEY,
    seconds_per_question INT NOT NULL DEFAULT 120,
    max_follow_ups_per_question INT NOT NULL DEFAULT 1,
    CONSTRAINT fk_viva_settings_exam FOREIGN KEY (exam_id) REFERENCES exam_sessions(exam_id) ON DELETE CASCADE,
    CONSTRAINT ck_viva_seconds CHECK (seconds_per_question BETWEEN 15 AND 1800),
    CONSTRAINT ck_viva_followups CHECK (max_follow_ups_per_question BETWEEN 0 AND 5)
);
