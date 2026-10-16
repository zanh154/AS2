# Kết quả sửa và kiểm thử sâu task 3 — 16/10/2026

Bản sửa tiếp nối nền code `e7c4fd9`, đã tích hợp `as2/main` tại `82722b2` để tạo pull request từ ThaiBranch vào main. Phạm vi là tài liệu/RAG/sinh và duyệt câu hỏi; không triển khai thêm phòng thi giọng nói.

## Kết quả cuối

- **253/253 test tự động đạt** trước khi tích hợp main; sau khi tích hợp main `82722b2`, **421/421 test .NET đạt**, 0 failed, 0 skipped, và **29/29 test JavaScript đạt**.
- Luồng trình duyệt Edge headless với MySQL Aiven và Ollama thật: **đạt**.
- Upload và xử lý DOCX, PDF có văn bản, PPTX có text: **đạt**.
- Upload DOCX 32.01 MB, lớn hơn giới hạn request mặc định khoảng 30 MB trước đây: **đạt** sau khi đồng bộ cấu hình.
- Thử thư viện SignalR không tải được và một lần tải trang chậm 8 giây: **đạt**, không bị vòng lặp tải lại.
- Các dữ liệu test và file upload test được dọn sau mỗi lượt. Số dòng courses, exam_sessions, academic_classes, questions, documents, generated_questions, document_chunks, exam_viva_settings trở về trước test.
- Web tiếp tục chạy tại http://localhost:49367/Account/Login.

## Các lỗi đã sửa

| Nhóm | Lỗi/ảnh hưởng | Sửa đổi |
|---|---|---|
| Upload giao diện | Click nút làm disabled trước thao tác mặc định, form không gửi POST | Khóa nút trong sự kiện submit; khôi phục khi quay lại trang |
| Trường môn học | HTML gửi CourseId nhưng PageModel đọc Input.CourseId, dẫn đến không tìm thấy môn | Đồng bộ hidden field với binding; nạp đúng môn có quyền ở GET/POST; giữ tên môn khi có lỗi |
| Điều hướng duyệt | Mất/giả documentId làm nút quay lại và redirect đi sai tài liệu | Lấy ID tài liệu từ câu hỏi đã kiểm tra quyền ở cả GET và POST; bổ sung tham số cho link |
| Hiển thị tài khoản | Trống tên người tải lên/người duyệt và thông tin môn trong danh sách | Nạp các navigation cần thiết tại repository |
| Form duyệt | Dữ liệu thiếu có thể bị bỏ qua; giao diện còn cho sửa sau quyết định | Kiểm tra ModelState/nội dung; giữ dữ liệu lỗi; khóa fieldset sau duyệt/từ chối; xử lý rõ việc xóa giá trị tùy chọn |
| Xóa tài liệu | Có thể mất môn đang chọn sau redirect | Giữ courseId trong form và URL quay lại |
| Realtime | Tài liệu đã Completed nhưng trang đứng Processing; timer 5 giây liên tục hủy navigation chậm | Dùng một hàm reloadOnce, dừng timer khi bắt đầu chuyển trang, không chồng request kiểm tra |
| Fallback | Không có SignalR thì không kiểm tra trạng thái | Polling độc lập với thư viện SignalR; endpoint JSON trạng thái có kiểm tra quyền; nối lại nhóm an toàn |
| AI trả dữ liệu sai | Nhận thiếu/thừa câu, Bloom không đúng, câu trùng trong đợt, trường quá dài | Schema số lượng/Bloom và validation toàn bộ response trước khi lưu; lỗi rõ ràng, không lưu một phần |
| Lưu câu hỏi | Lưu từng câu có thể để lại kết quả dở dang khi câu sau lỗi | SaveChanges một lần cho toàn bộ batch, transaction của EF bảo đảm rollback |
| File và lỗi DB | Upload/copy lỗi để lại file rác; xóa DB thất bại có thể mất file trước | Dọn đúng file do request tạo khi upload lỗi; xóa bản ghi trước khi dọn file; ghi log nếu cần dọn lại |
| Worker/cấu hình | Overlap >= chunk size có thể lặp vô hạn; lỗi lưu trạng thái có thể dừng worker | Kiểm tra cấu hình và chunker; xử lý hủy đúng; lỗi một tài liệu không dừng xử lý tài liệu tiếp theo |
| Quyền lịch sử | Người từng upload vẫn có thể liệt kê tài liệu sau khi môn chuyển chủ | Lọc theo chủ sở hữu môn hiện tại |
| Giới hạn upload | UI quảng bá 50 MB nhưng giới hạn request mặc định thấp hơn | Đồng bộ giới hạn Kestrel/form với cấu hình và phần overhead multipart; Business kiểm tra kích thước file |
| Thời gian | Thời điểm UTC hiện thẳng ra giao diện, lệch giờ/ngày ở máy Việt Nam | Chuyển sang giờ local để hiển thị; dữ liệu UTC trong DB giữ nguyên |

## Test tự động bổ sung

`DocumentGenerationRegressionTests.cs` cùng partial class `DocumentOperationalTests` kiểm tra:

- Batch hợp lệ 1, 2, 20 câu; schema giới hạn đúng số câu.
- Thiếu/thừa câu, JSON sai/null, đáp án/nội dung rỗng, trường quá dài, Bloom sai và trùng câu.
- HTTP 503 từ dịch vụ AI không ghi thêm dữ liệu.
- Lỗi DB trong câu thứ hai rollback toàn bộ batch (trigger SQLite chủ động gây lỗi).
- Copy file hoặc insert DB lỗi không để file upload rác.
- Xóa DB lỗi giữ file cũ.
- Quyền sau khi đổi chủ môn; tên tài khoản/môn/người duyệt được nạp đủ.
- Chunk overlap sai bị chặn ngay; chunk cuối không mất nội dung.
- Worker vẫn xử lý tài liệu tiếp theo khi không lưu được trạng thái thất bại của tài liệu trước.

`RazorModuleTwoHttpTests` mở rộng:

- Đọc đúng hidden field của HTML upload, multipart thật và giữ ngữ cảnh môn khi lỗi.
- Chặn upload vào môn người khác; status JSON không lộ nội dung và chặn tài liệu ngoài quyền.
- documentId sai/thiếu không làm sai điều hướng; nội dung rỗng không được duyệt; sửa trường tùy chọn lưu đúng.
- Các test cũ về phiên thi, cấu hình, antiforgery, phân quyền và WebSocket vẫn đạt.

Chạy lại:

```powershell
dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj
```

Dừng web trước khi build để tránh DLL đang được sử dụng. Kết quả chi tiết trước tích hợp lưu trong `.runtime/test-results/task3-fixed-full.trx`; kết quả sau tích hợp main lưu trong `.runtime/test-results/task3-pr-merged.trx` (Git bỏ qua). Test JavaScript: `node --test tests/browser/*.test.cjs`.

## Luồng trình duyệt thật đã chạy

1. Đăng nhập tài khoản giảng viên test và dùng môn test có mã riêng.
2. Không chọn file: form không gửi, nút vẫn dùng được.
3. TXT không hỗ trợ: báo lỗi và giữ môn học.
4. Click Upload DOCX: POST thật mang đúng môn; trạng thái xử lý tự hoàn tất; tên uploader hiển thị.
5. Sinh đúng 2 câu bằng qwen2.5:3b, lưu vào database thật.
6. Mở Xem/Duyệt không có query documentId: quay về đúng danh sách.
7. Sửa nội dung/đáp án/giải thích/chủ đề, giả documentId trong hidden field: vẫn lưu và quay đúng tài liệu; tên reviewer hiển thị; bản đã duyệt không còn cho sửa.
8. Từ chối câu thứ hai: trạng thái đúng.
9. DOCX 32.01 MB upload và xử lý thành công.
10. Chủ động chặn SignalR CDN, mô phỏng HTML Processing cũ trong khi server Completed, làm chậm lần điều hướng 8 giây: trang cập nhật được; chỉ một lần reload, không bị lượt poll tiếp theo hủy.
11. DOCX giả/hỏng: trạng thái Failed; fallback vẫn cập nhật được.
12. Bấm Xóa bằng giao diện: xác nhận, giữ môn đã chọn, tài liệu khác giữ nguyên.
13. Không có JavaScript exception trong luồng test. Ảnh xác nhận: `.runtime/task3/browser-after.png`.
14. PDF/PPTX được chạy riêng qua form HTTP thật; worker tạo chunks và embedding thật rồi hoàn tất.

## Phạm vi và giới hạn

Các ca response AI sai/20 câu dùng HTTP giả lập để kiểm soát dữ liệu lỗi; model thật đã chạy 2 câu. File DOCX 32 MB dùng nội dung ngắn kèm phần nhị phân đệm để kiểm tra giới hạn truyền file và khả năng đọc package, không phải bài kiểm tra RAG trên 32 MB toàn văn bản. Kiểm tra CDN/tải trang chậm là mô phỏng có chủ đích trên trình duyệt, trạng thái server được đọc thật.

Chưa đo tải nhiều người đồng thời, PDF scan/OCR, mọi bộ giáo trình dài hoặc chất lượng sư phạm của mọi câu AI sinh. Bốn test ASM1 cũ ngoài bộ hiện hành vẫn được giữ nguyên. Không thay đổi mật khẩu hay schema database trong lượt sửa này.
