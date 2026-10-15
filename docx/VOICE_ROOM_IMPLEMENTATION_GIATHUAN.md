# Phòng thi giọng nói — GiaThuan

## Repository và phạm vi

- Repository PRN222: `https://github.com/zanh154/AS2.git`.
- Khi bắt đầu: local `main` ở `bcbf4b3`; fetch cho thấy `origin/main` ở `54da0c3`, trước local 93 commit.
- Tạo nhánh `GiaThuan` từ `54da0c3` trong worktree `.worktrees/GiaThuan`. Thư mục gốc, `.gitignore` đang sửa và tài liệu đầu vào của người dùng được giữ nguyên, không stage vào commit tính năng.
- Chỉ commit/push `GiaThuan`; không merge main, không force push, không xóa file/bảng/dữ liệu hay chạy SQL dump. Không dùng database cloud trong kiểm thử.
- Đọc `PRN222_VOICE_ROOM_CODEX_GIATHUAN.md` và `MySQL_AIVES.sql` từ `docx` của thư mục gốc. Giữ nguyên hai file đầu vào.

## Kiến trúc thực tế

Giữ nguyên .NET 10 và kiến trúc Presentation → Business → DataAccess. Presentation có MVC lẫn Razor Pages; luồng mới dùng Razor Pages, không đưa DbContext/entity DataAccess vào PageModel hoặc Hub.

| Layer | Phần triển khai |
|---|---|
| Presentation | `/VoiceRoom/{candidateId}`, PageModel kiểm tra session/CSRF/đầu vào, MediaRecorder, TTS/STT adapter, SignalR Hub và JS recovery |
| Business | `IVoiceRoomService`, `VoiceRoomService`: xác thực sinh viên, điều phối câu hỏi, deadline, nháp/nộp/timeout, metadata audio và tải có quyền |
| DataAccess | `IVoiceRoomRepository`, `VoiceRoomRepository`, `IVoiceAudioStore`, `LocalVoiceAudioStore`, mapping bảng audit sẵn có |

Tái sử dụng lịch thi, quy tắc mở ca, phân đề của `IQuestionService`, `ExamSessionRules`, `VivaBudgetRules`, cấu hình `exam_viva_settings` và lifecycle kỳ thi. Không tạo lại lịch thi hoặc engine phân đề/chấm điểm.

## Đối chiếu schema đã cung cấp

| Bảng | Cách sử dụng |
|---|---|
| `exam_sessions`, `exam_candidates` | Khung giờ, thời lượng, ownership, trạng thái, started/finished; dùng mapping sẵn có |
| `exam_viva_settings` | `seconds_per_question`; nếu chưa có row dùng mặc định 120 giây, luôn chặn theo giờ kết thúc ca |
| `exam_questions`, `questions` | Đề đã phân, `order_no`, `asked_at`, `is_completed`; chỉ trả câu đang mở và các câu đã nộp, không lộ đáp án chuẩn |
| `answers` | Transcript, path audio, timestamp và duration phía server; giữ UNIQUE `exam_question_id`, không lưu binary vào MySQL |
| `exam_logs` | Mapping bổ sung trên bảng đã có; action `voice-room.started` ghi nhận chế độ và ngăn lẫn với trắc nghiệm |
| `follow_up_questions`, `exam_recordings`, `ai_evaluations` | Không ghi giả dữ liệu; không thay đổi engine chấm điểm hoặc tự sinh câu hỏi đào sâu |

Không có migration/schema mới cần thực thi cho tính năng này nếu database khớp dump đầu vào. Không tự động tạo/sửa bảng. Các migration sẵn có của đồng đội phải được chủ database kiểm tra riêng nếu database triển khai chưa đồng bộ.

## Hành vi đã triển khai

1. Sinh viên đăng nhập → **Lịch thi của tôi** → **Thi giọng nói**. Server lấy studentId từ session rồi đối chiếu candidate và tài khoản Active/Student trong DB.
2. Entry POST có CSRF, dùng quy tắc ca thi hiện hữu và phân đề đã có. Không chuyển sang voice nếu đã trả lời/nộp ở chế độ khác.
3. Câu hỏi chạy theo thứ tự đã phân. `asked_at` chỉ ghi lần đầu. Deadline = min(asked_at + seconds_per_question, kết thúc ca, kết thúc phiên, started_at + thời lượng).
4. F5/reconnect tải trạng thái DB, không reset deadline. Hết giờ server chốt nháp đã lưu; hết cả ca chốt các câu còn lại và cập nhật lifecycle kỳ thi.
5. **Đọc câu hỏi** dùng TTS `vi-VN`; khóa thu âm khi TTS đang đọc. Trình duyệt không hỗ trợ thì đọc nội dung trên màn hình.
6. **Cho phép mic và thu âm** mới xin quyền microphone. Xử lý từ chối quyền, không có thiết bị, thiết bị bận, bị ngắt, thiếu MediaRecorder/định dạng phù hợp.
7. Thu bằng WebM/Opus, Ogg/Opus hoặc MP4 tùy trình duyệt. Có dừng, nghe lại, tải về máy và thử upload lại. Microphone được giải phóng khi dừng/rời trang; tự dừng gần deadline.
8. `createVoiceSpeechAdapter()` là contract thay thế STT: `supported`, `start({onText,onError})`, `stop()`. Adapter hiện tại dùng Web Speech, `vi-VN`, cho phép sửa/nhập transcript và vẫn lưu audio khi STT lỗi.
9. Upload kiểm tra quyền, câu hỏi, cửa sổ thời gian, size ≤12 MiB, loại MIME, magic header, duration khai báo hữu hạn và không vượt ngân sách/thời gian server đã trôi. Request multipart bị giới hạn 13 MiB.
10. Audio được giữ ở `<MaterialStorage:RootPath>/voice-audio`, ngoài `wwwroot`. Tên file dùng candidate/question/hash; không dùng tên upload từ client. Đường dẫn file không xuất ra JSON. Endpoint Audio kiểm tra quyền và đặt `private, no-store`, `nosniff`.
11. File hoàn chỉnh được tái sử dụng khi retry; file dở do upload/ghi lỗi được giữ nguyên và tạo bản hoàn chỉnh khác. Không tự xóa/ghi đè file cũ. Chỉ một answer cho mỗi exam question.
12. Transcript autosave 5 giây, có lưu thủ công; nộp chốt câu hỏi. Request được xếp hàng để nộp không bị bỏ qua khi autosave đang chạy. Retry submit giữ examQuestionId cũ, không nộp nhầm câu kế tiếp.
13. Upload hết giờ có thể gắn audio vào câu vừa timeout trong `ExamSessionRules.SubmitGrace` hiện có; không mở lại câu, không nhận transcript chỉnh sửa sau deadline. Câu nộp tay chỉ chấp nhận retry cùng bản thu đã lưu.
14. SignalR `/hubs/voice-room`: Join đối chiếu user/candidate và chế độ thi; group riêng theo sinh viên/lượt thi. `VoiceRoomChanged` chỉ gửi invalidation sau commit, client tải lại trạng thái thật. Notification lỗi không hoàn tác bài đã nhận. Có reconnect/rejoin và polling 5 giây khi SignalR/CDN lỗi.
15. IndexedDB giữ nháp và các chunk audio theo user/candidate/question; khôi phục sau F5 khi server đã xác nhận quyền. Ghi chưa xác nhận có nút thử gửi/tải xuống. Không tự xóa bản dự phòng. Khi IndexedDB bị chặn/hết quota, UI báo giữ trang mở hoặc tải về.
16. Các đường phòng/kết quả trắc nghiệm cũ chuyển lượt voice về Razor Page; endpoint ghi trắc nghiệm không được ghi đè voice. Lịch thi có **Xem bài đã nộp**. Trang voice có lịch sử transcript/audio của chính sinh viên.

## Chạy và demo

Làm việc trong worktree `D:\AIVES_PRN222_AS\AS2\.worktrees\GiaThuan`.

1. Dùng cấu hình môi trường/secret store của dự án cho `ConnectionStrings:DefaultConnection`, trỏ DB local/test phù hợp schema. Không copy secret vào source/tài liệu.
2. Có thể đặt `MaterialStorage:RootPath` thành đường dẫn lưu riêng, ngoài `wwwroot`; process cần quyền ghi. Nhiều app instance cần cùng DB và storage dùng chung, cùng cấu hình session/Data Protection.
3. Chạy `dotnet run --project src/AssignmentPRN.Presentation`. Dùng HTTPS hoặc localhost cho microphone.
4. Trên dữ liệu test, giảng viên tạo ca và phân sinh viên, có ngân hàng câu hỏi Approved. Chỉnh thời gian mỗi câu trước khi phát đề/vào thi theo quy tắc hiện hữu.
5. Sinh viên vào đúng khung ca → Thi giọng nói → Đọc câu hỏi → Cho phép mic → nói → Dừng thu và lưu → kiểm tra transcript → Nộp.
6. Kiểm tra F5 giữ deadline; tắt/bật mạng rồi Đồng bộ lại; bấm nộp lại không nhảy thêm câu; từ chối mic vẫn nhập được transcript; đợi timeout; xem lại audio sau hoàn thành.
7. Đăng nhập sinh viên khác và thử cùng candidate/question: không được truy cập câu hỏi/audio hoặc Join group.

## Kiểm thử và các giới hạn cần biết

Các lệnh đã dùng:

```powershell
dotnet restore tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj
dotnet build AssignmentPRN.slnx --no-restore
dotnet build AssignmentPRN.slnx --configuration Release --no-restore
dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj --no-restore --verbosity quiet
node --test tests/browser/voice-room.test.cjs tests/browser/voice-capture.test.cjs
```

- Kết quả cuối: build Debug/Release thành công, Release 0 warning/0 error; 227/227 test .NET và 7/7 test JavaScript đạt.
- Bộ .NET: 227 test; gồm 24 test voice, dùng SQLite local/in-memory, HTTP localhost và WebSocket thực để kiểm tra CSRF, JSON/multipart binding, group trái quyền, notification, audio ownership, deadline, idempotency, lifecycle và file dở. Không dùng DB cloud.
- 7 test JavaScript dùng DOM/MediaRecorder/STT giả lập: submit trong autosave, IndexedDB ghi chậm, mất response, khôi phục nháp, TTS/record loại trừ nhau, mic bị từ chối và callback STT trễ. Đây không phải kiểm thử microphone thật hay full browser automation.
- Chưa thử microphone/giọng TTS/STT trên thiết bị thực; chưa chạy integration/concurrency trên MySQL 8.4. MySQL repository dùng transaction Serializable và khóa candidate trước khi đọc; cần xác minh tải thực trên môi trường test của nhóm.
- Web Speech không có ở mọi trình duyệt, có thể cần mạng/dịch vụ của hãng trình duyệt và có thể gửi âm thanh tới dịch vụ đó. Không tạo tài khoản trả phí hoặc API key. Không tuyên bố STT offline hay chính xác tuyệt đối. TTS `vi-VN` còn phụ thuộc voice cài trên máy.
- Giới hạn duration được kiểm tra bằng clock/ngân sách server và metadata, kèm size/header. Chưa có decoder media phía server để xác minh toàn bộ codec hoặc duration thực bên trong container của một client cố tình giả metadata.
- Không có contract chấm điểm transcript/sinh câu hỏi đào sâu tương thích giọng nói trong code hiện hữu; không tái sử dụng điểm trắc nghiệm cho lời nói. Module khác có thể đọc answer đã chốt trong `answers`; `IVoiceRoomNotifier` là contract sự kiện sau commit. Không tự tạo câu đào sâu mới hoặc điểm AI giả. Các câu đã phân mới là nguồn đề thi.
- Hết giờ khi mất mạng: server không gia hạn; transcript đã lưu được chốt. Bản thu phục hồi quá thời gian upload grace chỉ còn tải xuống để xử lý thủ công, không được thay đổi bài đã chốt.
- IndexedDB phụ thuộc quota/chính sách trình duyệt; chunk cuối trước F5 có thể chưa kịp ghi. Bản dự phòng/file orphan được giữ lại theo yêu cầu không xóa; retention/cleanup cần người dùng đồng ý trước. Không dùng máy dùng chung cho bản ghi riêng tư mà không có quy trình quản lý profile trình duyệt.

## Các phần đã push

- `64d4647`: phòng thi có phân quyền, thứ tự câu hỏi và deadline server; 214 test đạt.
- `5fb8d03`: TTS/mic/STT, lưu audio riêng tư và HTTP tests; 223 test đạt.
- `491fc26`: SignalR, reconnect, IndexedDB recovery và test queue/retry; 224 test .NET + 3 test JS đạt.
- Commit hoàn thiện: chặn đường phòng cũ, lịch sử bài đã nộp, retry file dở, kiểm thử bổ sung và tài liệu này.

Không merge sang `main`. Những thay đổi ban đầu của người dùng vẫn ở thư mục gốc.
