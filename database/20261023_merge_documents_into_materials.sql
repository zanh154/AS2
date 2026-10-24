-- Gộp "Tài liệu & AI" (documents) vào "Tài liệu môn" (course_materials).
-- Mỗi tài liệu môn có tối đa một bản chỉ mục AI (documents + document_chunks +
-- generated_questions). Xoá tài liệu môn thì chỉ mục AI của nó bị xoá theo.
-- Chạy lại nhiều lần vẫn an toàn.
--
-- Bản ghi documents cũ (chưa có material_id):
--   1. Nếu môn đó đã có tài liệu môn trùng tên file -> nối vào tài liệu môn đó.
--   2. Nếu chưa -> tạo tài liệu môn mới, file_path = /materials/legacy-rag/<storage_path>.
--      Sau khi chạy script, chép thư mục "documents" trong DocumentStorage:BasePath vào
--      <MaterialStorage:RootPath>/materials/legacy-rag/ để tải xuống được file cũ.
--      Chunk đã có sẵn nên AI vẫn sinh câu hỏi được dù chưa chép file.
--   File không phải PDF/DOCX/PPTX (course_materials.file_type không chứa) giữ nguyên, không nối.

-- 1. Cột, chỉ mục unique và khoá ngoại.
SET @ddl = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='documents' AND column_name='material_id')=0,
 'ALTER TABLE documents ADD COLUMN material_id INT NULL', 'SELECT 1');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @ddl = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='documents' AND index_name='ux_documents_material')=0,
 'ALTER TABLE documents ADD UNIQUE INDEX ux_documents_material (material_id)', 'SELECT 1');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @ddl = IF((SELECT COUNT(*) FROM information_schema.table_constraints WHERE constraint_schema=DATABASE() AND table_name='documents' AND constraint_name='fk_documents_material')=0,
 'ALTER TABLE documents ADD CONSTRAINT fk_documents_material FOREIGN KEY (material_id) REFERENCES course_materials(material_id) ON DELETE CASCADE', 'SELECT 1');
PREPARE migration_stmt FROM @ddl; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;

START TRANSACTION;

-- Chỉ một document đầu tiên của mỗi (môn, tên file) được nối vào tài liệu môn trùng tên.
DROP TEMPORARY TABLE IF EXISTS merge_first_documents;
CREATE TEMPORARY TABLE merge_first_documents (document_id INT PRIMARY KEY)
SELECT MIN(document_id) AS document_id FROM documents WHERE material_id IS NULL GROUP BY course_id, file_name;

-- Tài liệu môn trùng tên, chưa được document nào khác dùng.
DROP TEMPORARY TABLE IF EXISTS merge_name_matches;
CREATE TEMPORARY TABLE merge_name_matches (document_id INT PRIMARY KEY, material_id INT NOT NULL)
SELECT d.document_id, MIN(m.material_id) AS material_id
FROM documents d
JOIN merge_first_documents f ON f.document_id = d.document_id
JOIN course_materials m ON m.course_id = d.course_id AND m.file_name = d.file_name
WHERE NOT EXISTS (SELECT 1 FROM documents taken WHERE taken.material_id = m.material_id)
GROUP BY d.document_id;

UPDATE documents d JOIN merge_name_matches x ON x.document_id = d.document_id
SET d.material_id = x.material_id
WHERE d.material_id IS NULL;

-- Phần còn lại: tạo tài liệu môn mới. Tên trùng thì thêm tiền tố mã document để không đụng tên.
INSERT INTO course_materials (course_id, file_name, file_path, file_type, file_size, uploaded_by, processing_status, uploaded_at)
SELECT d.course_id,
       IF(f.document_id IS NULL
            OR EXISTS (SELECT 1 FROM course_materials m WHERE m.course_id = d.course_id AND m.file_name = d.file_name),
          LEFT(CONCAT(d.document_id, '-', d.file_name), 255),
          d.file_name),
       CONCAT('/materials/legacy-rag/', REPLACE(d.storage_path, '\\', '/')),
       CASE LOWER(SUBSTRING_INDEX(d.file_name, '.', -1)) WHEN 'pdf' THEN 'PDF' WHEN 'docx' THEN 'DOCX' ELSE 'PPTX' END,
       d.file_size, d.uploaded_by_id, 'Completed', d.created_at
FROM documents d
LEFT JOIN merge_first_documents f ON f.document_id = d.document_id
WHERE d.material_id IS NULL
  AND LOWER(SUBSTRING_INDEX(d.file_name, '.', -1)) IN ('pdf', 'docx', 'pptx')
  AND NOT EXISTS (SELECT 1 FROM course_materials m
                  WHERE m.file_path = CONCAT('/materials/legacy-rag/', REPLACE(d.storage_path, '\\', '/')));

UPDATE documents d
JOIN course_materials m ON m.course_id = d.course_id
  AND m.file_path = CONCAT('/materials/legacy-rag/', REPLACE(d.storage_path, '\\', '/'))
SET d.material_id = m.material_id
WHERE d.material_id IS NULL;

DROP TEMPORARY TABLE IF EXISTS merge_name_matches;
DROP TEMPORARY TABLE IF EXISTS merge_first_documents;

COMMIT;

-- Câu hỏi AI đã duyệt trước đây vào ngân hàng mà chưa có chủ đề: lấy tài liệu môn của document làm chủ đề.
UPDATE questions q
JOIN generated_questions g ON g.bank_question_id = q.question_id
JOIN documents d ON d.document_id = g.document_id
SET q.source_material_id = d.material_id
WHERE q.source_material_id IS NULL AND d.material_id IS NOT NULL;
