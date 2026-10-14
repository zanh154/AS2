# Tích hợp ThaiBranch, HungNguye và triNguyen — 15/10/2026

## Nhánh nguồn

- ThaiBranch: 83049c4 — Razor Pages môn học/phiên thi, cấu hình vấn đáp, realtime theo quyền.
- HungNguye: 9a93575 — rubric, duyệt câu hỏi, thông báo, chuyển BusinessRules sang Policies và đưa các quy tắc riêng vào service.
- triNguyen: 4e3a22d — tài liệu, RAG/Ollama, background xử lý tài liệu và DocumentProcessingHub.

Các merge được xử lý trên ThaiBranch. Nhánh cá nhân của thành viên không bị sửa và main chưa được merge.

## Cách giải quyết

- Giữ cả DI của rubric/thông báo/tài liệu và các service hiện có.
- Giữ đăng nhập Razor Pages; bỏ AccountController cũ để tránh trùng route.
- Theo cấu trúc Policies của nhánh Hùng; chuyển VivaBudgetRules và namespace của các trang/test tương ứng.
- Giữ NotifyProgressAsync khi ghép QuestionService với các quy tắc nội bộ mới của Hùng.
- Ghép đầy đủ cấu hình DbContext: bảng hiện có, cấu hình vấn đáp, rubric/thông báo và documents/document_chunks/generated_questions.
- Chuyển hợp đồng tài liệu từ Services.cs cũ vào Interfaces/DocumentContracts.cs và DocumentService vào Services.
- Đổi tên enum CLR trong module RAG thành GeneratedQuestionStatus/DocumentBloomLevel, giữ tên thuộc tính BloomLevel và giá trị trạng thái. Hai enum của RAG khác giá trị ngân hàng câu hỏi; không ép kiểu giữa hai bộ enum này.
- Giữ MaterialStorage và bổ sung DocumentStorage/Ollama/Rag vào cấu hình mẫu. Không ghi đè mật khẩu/cấu hình local.
- DocumentProcessingHub dùng xác thực session thay cho Authorize của cookie authentication chưa được cấu hình. Kiểm tra môn sở hữu trước khi join nhóm tài liệu/môn.
- Các trang tài liệu dùng StaffPage; guard câu hỏi không tồn tại để trang review không bị NullReferenceException.
- Dùng PdfPig 0.1.16 từ registry NuGet chính thức để tránh bộ phụ thuộc alpha bị resolve sang phiên bản custom; bỏ System.Net.Http.Json vốn có trong runtime.

## Kiểm chứng

- Build bản ghép thành công.
- 196/196 test đạt qua project tạm có AssemblyName AssignmentPRN.Tests để chạy các test internal mới của Hùng. Chỉ dùng test source theo dõi bởi Git; không chạy test cũ chưa commit hoặc file binary output.
- HTTP test xác nhận thêm trang Rubrics/Documents và review câu hỏi không tồn tại render được.
- WebSocket thật xác nhận DocumentProcessingHub nhận session đăng nhập, từ chối join môn của giảng viên khác, cho phép join môn sở hữu. Hub phiên thi và event cấu hình vẫn hoạt động.
- TRX: `%TEMP%/as2-integration-tests/TestResults/integrated-20261015.trx`.
- Dữ liệu kiểm thử dùng SQLite riêng; không sửa Aiven. Host test được dừng.

## Giới hạn

- Chưa demo UI bằng trình duyệt hoặc gọi sinh câu hỏi/embedding thật qua Ollama.
- Chưa áp dụng migrations mới của rubric/thông báo/vấn đáp/tài liệu vào database dùng chung. Nhánh tài liệu bổ sung model EF nhưng chưa có script migration trong commit nguồn; cần chuẩn bị schema tương ứng trước khi chạy module này trên MySQL.
- Module RAG vẫn có các phần cần review nghiệp vụ riêng: quyền đọc/sửa tài liệu ở service, lưu file trong wwwroot, đưa câu hỏi GeneratedQuestion đã duyệt vào ngân hàng Question và phát thông báo tiến độ thực tế. Việc hết conflict không xác nhận các luồng này đã hoàn thiện.
- API tài liệu hiện vẫn dùng IFormFile và enum DataAccess trong contract; chưa refactor ranh giới này vì cần thống nhất với chủ module.
