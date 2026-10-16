# Sửa các lỗi tích hợp còn lại — 17/10/2026

Nền trước sửa: main ade2745. Phạm vi: F04, F05, F06, F08 từ báo cáo kiểm thử trước.

## Thay đổi

- **F04 — rubric/lịch sử:** kiểm tra trong transaction Serializable; chặn tạo/sửa/xóa rubric khi câu hỏi đã bắt đầu thi, đã có câu trả lời hoặc điểm AI. Bảo vệ cả thao tác xóa toàn bộ rubric và thay thang điểm/tên tiêu chí hiện có.
- **F05 — RAG/ngân hàng:** duyệt Approved tạo câu ngân hàng trong cùng transaction, lưu BankQuestionId duy nhất để retry/backfill không tạo trùng. Giữ nội dung, đáp án đã duyệt, người duyệt và đủ 6 mức Bloom. Câu ngân hàng được giữ sau khi xóa tài liệu gốc. Trang xem câu đã duyệt có link cấu hình rubric.
- **F06 — timer:** không gán AskedAt cho câu tiếp theo trong lúc chờ hỏi xoáy. Model trả lời muộn không được kéo sinh viên về câu trước sau khi lượt kế tiếp đã mở.
- **F08 — điểm:** khóa lượt thi khi lưu batch, tổng hợp từ các đánh giá thực tế mới và cũ trong cùng transaction; trả tổng đã lưu về Business. Khi chưa đủ điểm để tính tổng thì bỏ tổng AI cũ thay vì hiển thị một tổng không khớp chi tiết. Giữ điểm giảng viên chốt.
- Đề có câu vấn đáp không có phương án trắc nghiệm tự chuyển từ đường vào trắc nghiệm sang phòng vấn đáp; có test HTTP cho nhánh này.
- Script AI chờ lâu hơn khi server khởi tạo chậm; không thay driver hoặc cấu hình GPU hệ thống.

## Kiểm thử

- **440/440 .NET đạt**, 0 failed, 0 skipped: 18 ca hồi quy bổ sung so với nền 422.
- **29/29 JavaScript đạt**.
- **7/7 ca quyền/tài khoản bổ sung đạt**: giữ các bản sửa F01/F02/F03/F07 của main.
- SQLite kiểm tra: rollback approval khi insert bank lỗi; retry không tạo trùng; 6 Bloom; xóa source giữ bank; hai request chồng nhau; quyết định AI muộn; xóa/sửa rubric đã chấm; chấm lại một phần; tổng thiếu điểm; giữ điểm giảng viên.
- MySQL/Ollama/web thật: lần chạy lại đạt toàn bộ pipeline, gồm upload DOCX/PDF/PPTX, embedding, sinh 2 câu, duyệt → liên kết ngân hàng → pool MySQL thật → mở trang rubric, từ chối câu khác, file lỗi, xóa và hủy phiên. Cleanup xác nhận số dòng 8 bảng trở về ban đầu. Lượt đầu gặp lỗi cấp phát CUDA; sau khởi động lại AI với CUDA bị tắt, pipeline RAG đạt. Model vấn đáp 7b vẫn phụ thuộc bộ nhớ khả dụng của máy; chưa đo mic thật/tải đồng thời.

## Migration và dữ liệu cũ

Chạy trước khi khởi động phiên bản mới:

```powershell
pwsh scripts/update-rag-bank.ps1 -WhatIf
pwsh scripts/update-rag-bank.ps1
```

Script đọc cấu hình local, không in mật khẩu. Áp dụng database/20261017_rag_question_bank.sql, rồi đưa các câu RAG đã Approved chưa có liên kết vào ngân hàng bằng transaction.

Trên Aiven của máy này:
- Đã thêm liên kết và mở Bloom 6 mức.
- Đã đưa **1 câu Approved cũ** vào ngân hàng.
- Chạy lại tạo **0 câu mới**, không thêm trùng.

Không chạy dump/seed để nâng cấp. Câu RAG được đưa vào ngân hàng vấn đáp; giảng viên cần cấu hình rubric trước khi thi. Các filter theo tài liệu của ngân hàng cũ vẫn dùng CourseMaterial, không coi DocumentId và MaterialId là cùng một ID.

Bước đề xuất cập nhật hàng loạt điểm AI cũ bị cơ chế duyệt tự động từ chối vì có thể sửa điểm thật trên diện rộng. Bước đó đã được bỏ khỏi script; không chạy bulk repair điểm. Bản sửa điểm áp dụng khi hệ thống thực hiện chấm/chấm lại theo luồng bình thường; điểm giảng viên được giữ nguyên.

## Artifacts

- .runtime/test-results/remaining-fixes-full.trx
- .runtime/test-results/remaining-fixes-http.trx
- .runtime/project-audit/remaining-fixes-js.log
- .runtime/project-audit/rag-bank-migration.log
- .runtime/project-audit/rag-bank-migration-repeat.log
- .runtime/project-audit/remaining-fixes-live-retry.log
- .runtime/logs/remaining-fixes-runtime.log

Các test hồi quy dài hạn nằm trong tests/AssignmentPRN.Tests, không phụ thuộc Aiven hay Ollama và được chạy cùng bộ CI bình thường. Các ca vượt quyền và rubric/điểm dùng database tạm; không dùng điểm thật làm mục tiêu thử sửa/xóa.
