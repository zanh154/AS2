-- ASM2 document ingestion / embeddings / generated question review.
-- Additive and re-runnable; existing exam data is not modified.
CREATE TABLE IF NOT EXISTS documents (
  document_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  course_id INT NOT NULL,
  uploaded_by_id INT NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  content_type VARCHAR(100) NOT NULL,
  file_size BIGINT NOT NULL,
  storage_path VARCHAR(500) NOT NULL,
  status VARCHAR(20) NOT NULL,
  error_message VARCHAR(1000) NULL,
  created_at DATETIME(6) NOT NULL,
  processed_at DATETIME(6) NULL,
  KEY IX_documents_course_id (course_id),
  KEY IX_documents_uploaded_by_id (uploaded_by_id),
  KEY IX_documents_status (status),
  CONSTRAINT FK_documents_courses FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE RESTRICT,
  CONSTRAINT FK_documents_users FOREIGN KEY (uploaded_by_id) REFERENCES users(user_id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS document_chunks (
  chunk_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  document_id INT NOT NULL,
  chunk_index INT NOT NULL,
  content LONGTEXT NOT NULL,
  token_count INT NOT NULL,
  embedding_json JSON NOT NULL,
  KEY IX_document_chunks_document_id (document_id),
  CONSTRAINT FK_document_chunks_documents FOREIGN KEY (document_id) REFERENCES documents(document_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS generated_questions (
  question_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  document_id INT NOT NULL,
  course_id INT NULL,
  question_text LONGTEXT NOT NULL,
  answer_key VARCHAR(2000) NULL,
  explanation VARCHAR(2000) NULL,
  bloom_level INT NOT NULL,
  topic VARCHAR(200) NULL,
  status VARCHAR(20) NOT NULL,
  reviewed_by_id INT NULL,
  reviewed_at DATETIME(6) NULL,
  review_notes VARCHAR(1000) NULL,
  created_at DATETIME(6) NOT NULL,
  KEY IX_generated_questions_document_id (document_id),
  KEY IX_generated_questions_course_id (course_id),
  KEY IX_generated_questions_reviewed_by_id (reviewed_by_id),
  KEY IX_generated_questions_status (status),
  CONSTRAINT FK_generated_questions_documents FOREIGN KEY (document_id) REFERENCES documents(document_id) ON DELETE CASCADE,
  CONSTRAINT FK_generated_questions_courses FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE SET NULL,
  CONSTRAINT FK_generated_questions_users FOREIGN KEY (reviewed_by_id) REFERENCES users(user_id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
