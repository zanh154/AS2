-- Approved RAG questions link to the canonical bank. Safe to run again.
SET @ddl = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='generated_questions' AND column_name='bank_question_id')=0,
 'ALTER TABLE generated_questions ADD COLUMN bank_question_id INT NULL', 'SELECT 1');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @ddl = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='generated_questions' AND index_name='ux_generated_bank_question')=0,
 'ALTER TABLE generated_questions ADD UNIQUE INDEX ux_generated_bank_question (bank_question_id)', 'SELECT 1');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @ddl = IF((SELECT COUNT(*) FROM information_schema.table_constraints WHERE constraint_schema=DATABASE() AND table_name='generated_questions' AND constraint_name='fk_generated_bank_question')=0,
 'ALTER TABLE generated_questions ADD CONSTRAINT fk_generated_bank_question FOREIGN KEY (bank_question_id) REFERENCES questions(question_id) ON DELETE RESTRICT', 'SELECT 1');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @ddl = IF((SELECT LOCATE('Evaluate',column_type)>0 AND LOCATE('Create',column_type)>0 FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='questions' AND column_name='bloom_level'), 'SELECT 1',
 'ALTER TABLE questions MODIFY COLUMN bloom_level ENUM(''Remember'',''Understand'',''Apply'',''Analyze'',''Evaluate'',''Create'') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
