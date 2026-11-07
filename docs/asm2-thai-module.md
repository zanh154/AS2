# ASM2 — phần Thai: nền Razor Pages, phiên thi và realtime

## Phạm vi đã triển khai

- Razor Pages: `/Courses`, `/Courses/Create`, `/Courses/Edit/{id?}`; `/ExamSessions`, `/ExamSessions/Create`, `/ExamSessions/Edit/{id?}`, `/ExamSessions/Details/{id?}`, `/ExamSessions/Questions/{id?}`, `/ExamSessions/Settings/{id?}`.
- Hai controller MVC và view cũ của các module này được chuyển sang PageModel và partial template trong Pages. Các module còn lại của nhóm vẫn có thể dùng MVC trong thời gian chuyển đổi.
- `Pages/_ViewStart.cshtml` dùng layout chung. Các trang của module yêu cầu Admin hoặc Lecturer qua StaffPageAttribute; quyền sở hữu được kiểm tra trước thao tác.
- PageModel gọi các service hiện có; không truy cập DbContext. Tầng Business và DataAccess vẫn giữ chiều `Presentation → Business → DataAccess`.
- Giữ luồng trạng thái hiện có: vào thi → đang diễn ra; tất cả lượt kết thúc → hoàn thành; lượt quá hạn được giảng viên chốt tại màn hình kết quả.
- Bài vấn đáp dùng lượt nối tiếp. Không đổi thuật toán thành thi trắc nghiệm cả lớp cùng giờ.

## Cấu hình vấn đáp

Chạy `database/20261015_exam_viva_settings.sql` trong database ASM2 trước khi mở trang cấu hình. Script tạo bảng mới có khóa ngoại và có thể chạy lại. Không sửa các bảng cũ hoặc dữ liệu bài thi cũ. Migration chưa được áp dụng vào database chung trong lượt triển khai này.

`IExamVivaSettingsService.GetAsync/SaveAsync`:
- `SecondsPerQuestion`: 15–1800 giây, không vượt thời lượng lượt thi.
- `MaxFollowUpsPerQuestion`: 0–5; 0 tắt hỏi xoáy.
- Không được sửa sau khi phát đề hoặc khi đã có sinh viên kết thúc/bắt đầu lượt thi. Repository đọc trạng thái và gọi validation Business trong transaction Serializable trước khi lưu.
- Phiên chưa có cấu hình dùng 120 giây (tối đa bằng thời lượng lượt) và 1 lượt hỏi xoáy.
- `MaxFollowUpCount` hiện có vẫn là giới hạn tổng số câu đào sâu mỗi sinh viên.

`VivaBudgetRules.QuestionDeadline` chặn deadline ở giờ kết thúc lượt thi. `CanAskFollowUp` kiểm tra đồng thời thời gian câu/lượt, giới hạn câu chính và giới hạn toàn phiên.

**Hợp đồng cho người 5–6:** gọi các quy tắc này tại backend trước khi bắt đầu câu hoặc sinh câu hỏi tiếp; lưu deadline/count trong DB của luồng vấn đáp. Phòng thi trắc nghiệm hiện tại chưa được chuyển thành phòng thi bằng giọng nói và chưa áp dụng timer riêng mỗi câu. Chưa triển khai TTS/STT, RAG, rubric hoặc AI hỏi xoáy trong phần này.

## SignalR

- Endpoint: `/hubs/exam-updates`.
- Kết nối kiểm tra session và tài khoản đang hoạt động; lưu danh tính trên connection. Sinh viên không tự chọn userId/role.
- `JoinExam(examId)`: chỉ Admin, giảng viên quản lý phiên hoặc sinh viên thuộc phiên được tham gia. Kết nối lại phải join lại.
- `IExamRealtimeNotifier` nằm ở Business; `SignalRExamNotifier` ở Presentation. Business không tham chiếu Hub/ASP.NET.
- Sự kiện `ExamChanged`: `{ examId, kind }`. Không chứa đề, đáp án, audio, transcript, điểm hoặc danh sách sinh viên.
- Gửi đến nhóm admin và nhóm người dùng có liên quan; không broadcast mọi kết nối. Nhóm exam không dùng để phát dữ liệu nhạy cảm vì quyền roster có thể thay đổi giữa các request.
- Kind hiện có: created, updated, status, rescheduled, deleted, roster, entered, submitted, closed-overdue, settings, courses. Các thay đổi phát đề cần nhóm câu hỏi nối vào notifier khi hoàn thiện module của họ.
- Lưu thành công rồi mới gửi thông báo. Lỗi gửi SignalR được log và không biến thao tác lưu thành thất bại.
- JS cập nhật snapshot danh sách phiên/môn và vùng số liệu/roster tại chi tiết phiên. Khi form có thay đổi chưa lưu, giữ form và hiện thông báo yêu cầu cập nhật. Khi reconnect, tải lại snapshot để bắt cập nhật bị bỏ lỡ. Các trang còn lại hiển thị thông báo.
- ClassListChanged cũ được chuyển thành thông báo ExamListChanged có phạm vi. Khi admin sửa lớp, module lớp nên gửi thêm tới giảng viên sở hữu lớp để hoàn thiện thông báo.

## Kiểm thử 15/10/2026

- Build thành công, không có warning/error.
- 176/176 test đạt qua project tạm gồm test Git-tracked và 4 file test mới của phần này; không gồm các test cũ chưa commit không tương thích API.
- Test HTTP mở host riêng với SQLite tạm, cookie session và antiforgery thật. Xác nhận đăng nhập, danh sách chỉ hiện phiên của giảng viên, snapshot, chặn phiên người khác, chặn POST thiếu token, môn mới thuộc giảng viên đang đăng nhập, lưu cấu hình, tải roster, tạo phiên và chặn sinh viên vào trang quản lý.
- Test WebSocket dùng protocol SignalR thật: join phiên người khác bị từ chối; join phiên của mình thành công; lưu cấu hình nhận ExamChanged. Có 7 case kiểm tra ma trận quyền Hub, test validation/khóa cấu hình và budget.
- JavaScript đã kiểm tra syntax. Chưa demo giao diện bằng hai trình duyệt thật; chưa kiểm tra khóa dòng/tải đồng thời MySQL.
- Host test đã dừng; dữ liệu test không ghi vào Aiven.
- TRX: `%TEMP%/as2-task-tests/TestResults/asm2-task-20261015.trx`.

## Demo tích hợp nhóm

1. Áp dụng migration vào DB ASM2, khởi động app.
2. Giảng viên mở danh sách/chi tiết phiên ở cửa sổ A; mở cấu hình cùng phiên ở cửa sổ B.
3. Lưu cấu hình ở B: A nhận thay đổi mà không phải tải lại toàn trang (nếu không đang sửa form).
4. Giảng viên khác không được mở chi tiết/join cùng phiên.
5. Khi sinh viên vào/nộp bài, trạng thái ở trang chi tiết giảng viên cập nhật. Audio, AI và rubric được các module tương ứng tích hợp tiếp.
