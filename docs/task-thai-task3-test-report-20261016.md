# Kiểm thử task Thai và task 3 — 16/10/2026

> Báo cáo lịch sử trước khi sửa. NAV-01 và LIST-01 đã được sửa trong working tree; xem `task3-fix-test-report-20261016.md` để biết kết quả cuối 253/253 và các lỗi bổ sung.

Bản kiểm tra: `e7c4fd9` (main AS2 đã cập nhật về ThaiBranch tại máy).

## Kết luận

- Task Thai: chưa phát hiện lỗi trong các ca đã chạy về Razor Pages môn học/phiên thi, cấu hình, trạng thái, phân quyền và sự kiện SignalR.
- Task 3 (tài liệu/RAG/sinh và duyệt câu hỏi): luồng chính chạy được với MySQL Aiven và Ollama thật, nhưng phát hiện **2 lỗi** cần sửa.
- Bộ test trước khi bổ sung ca mới: **213 passed, 0 failed**.
- Sau khi thêm 3 ca tái hiện 2 lỗi: **216 tổng, 213 passed, 3 failed, 0 skipped**. Không bỏ qua các ca lỗi.

## Task Thai

| Kiểm tra | Kết quả | Cách kiểm tra |
|---|---|---|
| Đăng nhập, trang Razor, chống giả mạo form, phân quyền giảng viên/sinh viên | Đạt | HTTP với database SQLite riêng |
| Giảng viên không gán môn cho giảng viên khác qua dữ liệu POST | Đạt | RazorModuleTwoHttpTests |
| Tạo môn và phiên có lớp/sinh viên/câu hỏi | Đạt | Form HTTP thật trên Aiven |
| Số câu chính và phạm vi phát đề thủ công/tự động | Đạt trong các ca hiện có | SharedQuestionConfigurationTests, QuestionDealingTests, QuestionSupplyTests |
| Lưu 60 giây/câu và 2 lượt hỏi xoáy | Đạt | Form thật và đối chiếu MySQL |
| Chặn 0 hoặc 1801 giây/câu; chặn 6 lượt hỏi xoáy | Đạt | Form thật, dữ liệu đã lưu không bị ghi đè |
| Quyền sửa cấu hình, khóa sau phát đề/bắt đầu | Đạt | VivaSettingsTests, IssuedPaperEditTests |
| Hủy phiên đang xếp lịch, khóa cấu hình phiên đã hủy | Đạt | Form thật và đối chiếu MySQL |
| Đồng bộ trạng thái khi vào thi, nộp bài, hết giờ, vắng thi | Đạt | ExamSessionLifecycleTests, ExamLifecycleRulesTests, OverdueSlotTests |
| Quyền join nhóm phiên/môn; nhận sự kiện cấu hình | Đạt | ExamHubAccessTests; WebSocket thật trong RazorModuleTwoHttpTests |
| Thông báo cập nhật ngân hàng câu hỏi mới từ main | Đạt trong các ca hiện có | QuestionRealtimeNotificationTests |

## Task 3

| Kiểm tra | Kết quả |
|---|---|
| Upload DOCX có văn bản → xử lý nền → chunks/embedding thật | Đạt trên Aiven + Ollama |
| Sinh đúng 2 câu từ tài liệu, lưu chờ duyệt | Đạt với qwen2.5:3b |
| Chỉnh nội dung/đáp án/giải thích/Bloom/chủ đề rồi duyệt | Đạt, dữ liệu đã lưu được kiểm tra |
| Từ chối câu hỏi thứ hai | Đạt |
| Sinh 0 hoặc 21 câu | Bị chặn; ca 0 được chạy trên web thật, cả hai có hồi quy tự động |
| Upload PDF chứa văn bản và PPTX chứa text | Đạt, tạo chunks và embedding thật |
| File .exe và PDF rỗng | Bị chặn trước khi tạo bản ghi |
| File mang đuôi DOCX nhưng không phải DOCX hợp lệ | Nhận trạng thái Failed; không lưu chunks |
| Xóa tài liệu qua form, xóa chunks liên quan | Đạt trên database thật |
| Giảng viên khác/sinh viên/session giả vai trò truy cập dữ liệu | Bị chặn trong hồi quy |
| Retrieval chỉ lấy tài liệu đã chọn; xử lý lại thay chunks | Đạt trong hồi quy |
| Duyệt lại không ghi đè quyết định đã lưu | Bị chặn trong hồi quy |
| Quyền nhóm tài liệu và sự kiện DocumentProgress | Đạt qua WebSocket trong hồi quy HTTP |
| Điều hướng khi mở Xem/Duyệt trực tiếp từ chi tiết tài liệu | **Lỗi NAV-01** |
| Hiển thị tên người tải lên trong danh sách | **Lỗi LIST-01** |

## NAV-01 — Trang duyệt không xác định tài liệu từ câu hỏi (P2)

Tái hiện:

1. Sinh câu hỏi từ một tài liệu đã xử lý xong.
2. Tại trang chi tiết tài liệu, bấm **Xem/Duyệt**. Link hiện chỉ mang ID câu hỏi, không có `documentId`.
3. PageModel dùng `documentId` từ query (mặc định 0) cho nút quay lại và hidden field.
4. Sau lưu quyết định hoặc bấm quay lại, điều hướng tới danh sách của tài liệu 0 và báo không tìm thấy tài liệu. Quyết định duyệt vẫn có thể đã lưu thành công.

Đồng thời, nếu truyền `documentId` khác vào query, trang dùng sai ID đó dù đã đọc được ID tài liệu thật từ câu hỏi.

Bằng chứng:

- `Pages/Documents/Details.cshtml:136`: link không truyền documentId.
- `Pages/Documents/Questions/Review.cshtml.cs:29,38,54,73`: lấy ID từ request thay vì từ câu hỏi đã được kiểm tra quyền.
- Hai ca `Review_page_uses_question_document_for_navigation` với 0 và 999 đều thất bại: mong đợi 1, nhận 0/999.

Hướng sửa: xác định `DocumentId` từ dữ liệu câu hỏi đã được service kiểm tra quyền, ở cả GET và POST; dùng ID đó cho điều hướng. Đây là lỗi điều hướng, chưa thấy vượt quyền đọc hoặc duyệt câu hỏi khác.

## LIST-01 — Cột người tải lên trống (P3)

Tái hiện: mở danh sách tài liệu của môn đã có upload; tên người tải lên trống dù tài khoản tồn tại.

Nguyên nhân: repository ListByCourseAsync/ListByUserAsync không nạp navigation UploadedBy; service ánh xạ tên sang chuỗi rỗng trong request có DbContext mới.

Bằng chứng:

- `Repositories/DocumentRepository.cs:35,43`.
- `Pages/Documents/Index.cshtml:67` hiển thị UploadedByName.
- `Document_list_includes_the_uploader_display_name`: mong đợi `Test`, thực tế chuỗi rỗng.

Hướng sửa: nạp/projection dữ liệu tên tài khoản trong truy vấn danh sách, giữ kiểm tra quyền tại Business.

## Dữ liệu và bằng chứng chạy

Dùng môn/lớp/phiên/tài liệu có mã riêng `ASM2CHECK20261015173913` để tránh sửa dữ liệu người dùng. Sau kiểm thử, các bản ghi test và file upload test đã được xóa. Số dòng của courses, exam_sessions, academic_classes, questions, documents, generated_questions, document_chunks, exam_viva_settings trở lại giá trị trước test.

Kết quả máy đọc được (được Git bỏ qua):

- `.runtime/test-results/task1-task3-20261016.trx`: 213/213 trước khi bổ sung ca.
- `.runtime/test-results/task3-additional-20261016.trx`: 3 ca tái hiện lỗi.
- `.runtime/test-results/task1-task3-full-20261016.trx`: 213 passed / 3 failed.

Trong lượt này chỉ bổ sung test và báo cáo; code chức năng giữ nguyên để bảo toàn hiện trạng lỗi. Bốn file test ASM1 cũ vẫn được giữ nguyên ngoài bộ test hiện hành.

## Giới hạn kiểm tra

Đã kiểm tra HTTP form, dữ liệu MySQL, worker/AI thật và WebSocket. Chưa kiểm tra hiển thị pixel trên trình duyệt, tải đồng thời nhiều người, tài liệu lớn sát giới hạn upload, PDF scan/OCR hoặc chất lượng sư phạm trên nhiều bộ giáo trình. Kết quả task Thai áp dụng cho luồng và module hiện có, không đại diện cho phần phòng thi giọng nói/AI hỏi xoáy chưa được nhóm tích hợp.
