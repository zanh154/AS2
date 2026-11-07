# Kiểm thử vận hành ASM2 — 15/10/2026

## Kết quả

- Project test hiện tại chạy thành công: **203 passed, 0 failed, 0 skipped**.
- Đăng nhập giảng viên test, tạo môn/lớp test, tạo phiên thi qua Razor Page, lưu cấu hình thời gian/câu hỏi đào sâu: thành công trên MySQL Aiven thật.
- Upload DOCX qua form có antiforgery; worker trích xuất văn bản; model embedding thật tạo vector 768 chiều và lưu chunks: thành công.
- Model `qwen2.5:3b` sinh câu hỏi thật; lưu chờ duyệt; sửa câu hỏi/đáp án/giải thích/chủ đề/Bloom rồi duyệt qua Razor Page: dữ liệu chỉnh sửa và trạng thái Approved được lưu đúng.
- Sau test, đã xóa dữ liệu tạo riêng, số bản ghi courses/exam_sessions/academic_classes/questions/documents/generated_questions trở về trước test.
- Chạy lại các migration câu hỏi/thông báo, cấu hình phiên và RAG: thành công; số bản ghi cũ courses/users/exam_sessions/exam_candidates/questions/answers không đổi.

## Hồi quy mới

`DocumentOperationalTests` dùng SQLite riêng, không gọi model thật:

- Giảng viên khác, sinh viên và session giả vai trò không được xem/sinh/duyệt/xóa tài liệu.
- Duyệt lưu các trường đã sửa; từ chối lần hai không ghi đè quyết định đã lưu.
- Số câu 0 hoặc 21 bị chặn trước khi gọi AI.
- Retrieval chỉ lấy chunks thuộc tài liệu đã chọn.
- Xử lý lại tài liệu thay chunks cũ, không làm mất chunks tài liệu khác.

`RazorModuleTwoHttpTests` kiểm tra HTTP và WebSocket thật với database riêng:

- Login, quyền sở hữu, antiforgery, tạo môn/phiên, cấu hình và sự kiện phiên thi.
- JoinCourseGroup/JoinDocumentGroup bị chặn cho môn/tài liệu khác.
- Giảng viên vào nhóm tài liệu của mình nhận được DocumentProgress.

## Phạm vi

Test AI thực tế dùng một DOCX ngắn và một câu hỏi. Chưa phải đánh giá chất lượng model trên tài liệu dài, nhiều người đồng thời, PDF scan hay mọi định dạng tài liệu. Không xác nhận các module giọng nói/hỏi đào sâu chưa được triển khai.

Bốn file ASM1 cũ chỉ tồn tại tại máy (`ExamSchedulePlannerTests`, `ExamSessionRepositoryTests`, `ModuleTwoRegressionTests`, `ModuleTwoTests`) dùng các API/controller đã bỏ. Chúng được giữ nguyên và loại khỏi compile; kết quả 203 áp dụng cho bộ test hiện hành.
