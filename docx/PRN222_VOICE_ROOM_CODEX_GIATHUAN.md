# CODEX + MCP — PRN222 / AIVES / Voice Exam Room

> **Mục tiêu:** Triển khai tính năng **Phòng thi bằng giọng nói** trên dự án **PRN222** (độc lập với dự án SWD), giữ nguyên kiến trúc **ASP.NET Core 3-Layer** đang có. Tự chủ triển khai từng phần nhỏ, **commit và push ngay sau mỗi phần lên DUY NHẤT nhánh `GiaThuan`**. Tối ưu token. **Chỉ hỏi tôi trước khi xóa**, và phải giải thích vì sao cần xóa.

## 0. Quyền tự chủ & hàng rào an toàn (bắt buộc)

- Bạn được **đọc/analyze code, dùng MCP khả dụng, tạo/sửa file, viết code, cài/restore dependency hợp lý, build/test, tạo commit và push** mà không phải xin phép từng bước.
- **Không được xóa** file, thư mục, code cũ, bảng/cột/dữ liệu, migration, branch, hay lịch sử Git nếu **chưa được tôi đồng ý rõ ràng**. Áp dụng cả xóa gián tiếp do rename/refactor hoặc migration. Trước khi xóa, nêu ngắn gọn: `(1) sẽ xóa gì; (2) vì sao; (3) ảnh hưởng; (4) phương án không xóa; (5) cách khôi phục`; sau đó **DỪNG CHỜ**.
- Tuyệt đối không chạy `rm`, `del`, `git clean`, `git reset --hard`, `git push --force`, `git branch -D`, `DROP/TRUNCATE/DELETE`, migration phá dữ liệu, hoặc lệnh tương đương khi chưa được tôi chấp thuận. Không xóa nhánh `main`/`dev`.
- **Không commit/push lên `main`, `dev`, `develop`** hay branch khác. Không tự tạo PR/merge. **Chỉ commit/push `GiaThuan`**.
- Không tự động sửa schema/dữ liệu của database cloud/shared/production; nếu cần, chỉ **đề xuất migration an toàn**, làm việc trên DB local/test khi có cấu hình rõ. File SQL dump **chỉ để đọc, tuyệt đối không thực thi** (có thể chứa `DROP TABLE`/GTID).
- Không đọc/in/commit secret hoặc ghi API key/connection string/password thật vào source; tôn trọng `.gitignore`, dùng cấu hình môi trường/secret store có sẵn.
- Không sửa code của thành viên khác nếu không cần thiết. Luôn kiểm tra `AGENTS.md`, quy ước dự án, `.codex/skills` và git diff trước khi commit. Không tự `git add .` toàn repo; stage các file thuộc phạm vi chức năng.
- Nếu gặp **blocker không thể tự xử lý an toàn** (working tree có thay đổi chưa commit của người khác, thiếu quyền push, thiếu credentials, xung đột branch, cần trả phí dịch vụ, nguy cơ mất dữ liệu), dừng và hỏi **một câu ngắn đúng trọng tâm**. Không giả vờ hoàn thành.

## 1. Quy trình Git: thực hiện TRƯỚC KHI SỬA CODE

1. Kiểm tra `pwd`, `git status --short --branch`, `git remote -v`, `git branch -a -vv`, `git log -1 --oneline`, rồi `git fetch origin` nếu có quyền. Xác nhận đây là **repo PRN222**, không phải SWD.
2. Người dùng **đã pull `main` về local**. Kiểm tra `main`/`origin/main`, nhưng **không reset/ghi đè** gì.
3. Bảo đảm tất cả thay đổi từ nhiệm vụ này nằm trên `GiaThuan`:
   - Nếu local `GiaThuan` đã tồn tại: `git switch GiaThuan` và **giữ nguyên lịch sử**.
   - Nếu chỉ tồn tại `origin/GiaThuan`: `git switch --track -c GiaThuan origin/GiaThuan`.
   - Nếu chưa có ở đâu: từ local `main` đã được cập nhật, `git switch -c GiaThuan`.
   - Nếu working tree không sạch, **không tự stash/ghi đè/commit hộ thay đổi cũ**; báo blocker.
   - Nếu lịch sử branch khác nhau: không rebase/reset/force-push; chỉ tích hợp `main` khi cần và có phương án merge an toàn, không xóa dữ liệu.
4. Trước **mỗi** commit, xác minh `git branch --show-current` bằng `GiaThuan`. Sau khi build/tests cần thiết: stage đúng file -> `git diff --cached --check` -> `git diff --cached --stat` -> tạo **1 commit có ý nghĩa** -> **`git push -u origin GiaThuan` (lần đầu) hoặc `git push origin GiaThuan` (những lần sau)**.
5. Nếu push thất bại: thử xử lý lỗi thông thường không phá lịch sử; nếu chưa thành công thì **dừng**, báo commit hash và lỗi. **Không tiếp tục tạo commit mới khi commit trước chưa push được**.
6. Không tạo commit rỗng hoặc commit trùng cho phần đã tồn tại. Nếu có code sẵn, dùng lại, kiểm thử và bỏ qua commit không cần thiết. Số commit **ước tính 14**, không ép đúng 14 bằng mọi giá.

## 2. Đọc repository và kiểm tra nguồn sự thật

- Đọc `.sln`, `.csproj`, entrypoint, DI, DbContext, Entities, Repositories, Interfaces, Services, Pages, Hubs, JS, auth/roles, cấu hình storage/STT, tests và Git history gần đây **đủ dùng**, không dump toàn bộ file vào hội thoại.
- Xác nhận kiến trúc thực tế: `Presentation` (ưu tiên Razor Pages theo đề PRN222), `Business Logic`, `Data Access`. **Tuân theo cấu trúc hiện hữu**, không tạo thêm kiến trúc hoặc lớp trùng chỉ để đúng tên ví dụ.
- Tìm `MySQL_AIVES.sql` trong repo/nơi Codex đọc được; nếu không có, dùng Entities/DbContext làm nguồn chính và thông báo ngắn **file SQL chưa có trong workspace**. **Không giả định file đính kèm ChatGPT tự nằm trong repo**.
- Tham chiếu schema AIVES được cung cấp:
  - `exam_sessions`: `exam_id`, `start_time`, `end_time`, `time_per_student`, `status`, `main_question_count`, `max_follow_up_count`.
  - `exam_candidates`: `candidate_id`, `exam_id`, `student_id`, `scheduled_time`, `status`, `started_at`, `finished_at`.
  - `exam_viva_settings`: `exam_id`, `seconds_per_question`, `max_follow_ups_per_question`.
  - `exam_questions`: `exam_question_id`, `exam_id`, `candidate_id`, `question_id`, `order_no`, `asked_at`, `is_completed`, `parent_exam_question_id`.
  - `questions`: `question_id`, `question_text`, `question_type`, `status`.
  - `answers`: `answer_id`, `exam_question_id`, `candidate_id`, `transcript`, `audio_path`, `started_at`, `finished_at`, `duration_seconds`. **UNIQUE(`exam_question_id`)**: tránh tạo bản ghi lặp.
  - `follow_up_questions`: `answer_id`, `question_text`, `answer_transcript`, `audio_path`, `order_no`, `asked_at`, `answered_at`.
  - `exam_recordings`, `exam_logs` dùng khi đúng mục đích.
  - Kiểm tra code/migration thực tế trước khi kết luận các cột đã map đủ.
- **Không tự viết engine AI chấm điểm / tự sinh câu hỏi tiếp theo** nếu module khác phụ trách. Tìm contract tích hợp từ chức năng của đồng đội, mock có interface rõ khi chưa sẵn.
- Đầu tiên báo **tóm tắt tối đa 10 dòng**: kiến trúc phát hiện, các phần đã có/chưa có, dependencies, rủi ro và kế hoạch commit rút gọn. **Sau đó tự triển khai liên tục**, không chờ OK trừ trường hợp phải hỏi theo mục 0.

## 3. Yêu cầu nghiệp vụ / nguyên tắc triển khai

Luồng chính: Sinh viên đăng nhập -> xem lịch thi -> xác thực đủ điều kiện tham gia -> vào phòng thi -> nhận câu hỏi theo thứ tự -> TTS đọc câu hỏi -> sinh viên cho phép microphone, thu âm -> STT chuyển tiếng nói thành transcript -> lưu audio/transcript tạm an toàn -> hiển thị đếm ngược mỗi câu -> nộp hoặc hết giờ -> nhận câu tiếp theo qua SignalR -> hoàn thành bài thi.

**Phân lớp:**
- **Presentation:** Razor Pages/PageModel, giao diện/JS (`MediaRecorder`, TTS/browser STT nếu chọn), SignalR Hub/client. PageModel và Hub chỉ orchestration/validation đầu vào; không chứa SQL/business rules.
- **Business Logic:** dịch vụ xét quyền vào phòng, điều phối câu hỏi, tính deadline ở server, câu trả lời, upload metadata, STT abstraction, trạng thái bài thi, idempotency.
- **Data Access:** EF Core/Repositories cho các bảng sẵn có, transaction khi cần; không để presentation gọi DbContext trực tiếp nếu cấu trúc dự án không cho phép.

**Tiêu chí bắt buộc:**
- Chỉ đúng sinh viên đã được phân vào kỳ thi mới được vào và truy cập câu hỏi/file audio của chính mình. Không tin `studentId/candidateId` do client gửi nếu chưa đối chiếu user đã auth.
- Lịch thi/trạng thái truy xuất từ DB. Trước khi bắt đầu phải kiểm tra khung thời gian/quy tắc hiện có, không tự đặt một quy tắc mới gây bất tương thích.
- Thời gian câu hỏi có **deadline phía server**, không reset khi F5/reconnect, không vượt thời lượng buổi thi. Đồng bộ clock UI từ server; nộp/hết giờ phải chống xử lý trùng.
- TTS dùng `vi-VN` khi phù hợp; tránh việc TTS tự thu vào microphone (tách trạng thái đọc và ghi); fallback khi browser không hỗ trợ.
- Thu âm chỉ sau quyền đồng ý của người dùng; xử lý denied/no device/ended/unsupported; giới hạn size, duration, file type, tên file an toàn; **không lưu binary audio vào MySQL** nếu project đang dùng path storage.
- STT: **ưu tiên provider đã có trong repo**; nếu chưa có, tạo interface/adapter để có thể thay thế. Browser Web Speech API chỉ là MVP có giới hạn hỗ trợ; không được tuyên bố STT hoạt động ở mọi browser. Không tự tạo tài khoản dịch vụ trả phí, không tự bịa key.
- Lưu được audio/transcript tạm khi phù hợp, có cách retry an toàn; chỉ người có quyền truy cập file. Không expose thư mục upload công khai nếu không có yêu cầu.
- SignalR để **thông báo câu hỏi tiếp theo** và sự kiện realtime; server là nguồn sự thật. Sau reconnect phải tải trạng thái thực từ server; không nhảy câu, cấp thêm giờ hoặc lưu câu trả lời trùng. Authorize group và event theo candidate/exam.
- Có loading/error/retry hợp lý với lỗi STT, upload, microphone, mất kết nối. Không sửa/đụng AI scoring, module tạo kỳ thi hoặc rubric nếu không liên quan trực tiếp.

## 4. Kế hoạch commit gợi ý (mỗi commit phải push lên `origin/GiaThuan`)

Làm theo thứ tự nếu chưa có sẵn, điều chỉnh thứ tự hợp lý theo dependency trong repo. Mỗi mục là **vertical slice**, có thể bao gồm code ở cả 3 layer; không chia commit cơ học theo tầng.

1. `feat(voice-room): add student exam schedule page` — lịch thi sinh viên.
2. `feat(voice-room): enforce exam room entry authorization` — điều kiện vào thi và kiểm tra quyền.
3. `feat(voice-room): add exam room interface` — UI câu hỏi, điều khiển, trạng thái.
4. `feat(voice-room): load ordered candidate questions` — câu hỏi đúng candidate/thứ tự.
5. `feat(voice-room): enforce server-side question timing` — đồng hồ, timeout, F5.
6. `feat(voice-room): read questions aloud with TTS` — đọc câu hỏi và fallback.
7. `feat(voice-room): handle microphone permissions` — xin quyền, kiểm tra thiết bị/lỗi.
8. `feat(voice-room): record candidate audio` — MediaRecorder start/stop, preview nếu phù hợp.
9. `feat(voice-room): save temporary audio uploads` — validate, storage, API upload.
10. `feat(voice-room): transcribe voice answers` — STT qua provider/adapter khả dụng.
11. `feat(voice-room): persist transcripts and answers` — lưu và chống duplicate.
12. `feat(voice-room): receive next questions via SignalR` — authorized realtime events.
13. `fix(voice-room): recover from reconnect and retry` — state recovery, lỗi STT/network.
14. `test(voice-room): cover workflows and document demo` — tests/demo checklist.

**Quy tắc chất lượng mỗi commit:** build thành công (hoặc ghi rõ blocker khách quan); chạy test có liên quan; không commit file tạm, `bin/`, `obj/`, secrets; không làm hỏng luồng đã chạy; ghi rõ test đã thực hiện/chưa thực hiện. Không giả định trình duyệt/microphone đã được test nếu chưa test thật.

## 5. Giao tiếp với tôi: ngắn gọn, tiết kiệm token

- Không in nguyên file, log dài hoặc stack trace trừ khi cần. Dùng tìm kiếm chọn lọc (`rg`, tìm class/interface cụ thể), đọc đúng vùng cần sửa, giữ output terminal ngắn.
- **Sau mỗi commit + push**, báo tối đa **4 dòng**:
  `Commit n | <hash> | <message>`
  `Push: origin/GiaThuan OK/FAIL`
  `Build/Test: <kết quả ngắn>`
  `Lưu ý: <rủi ro còn lại hoặc 'Không'>`
- **Nếu phải xin xóa**, chỉ hỏi sau khi giải thích đủ 5 ý của mục 0; không lách bằng thay file trống, rewrite history hoặc rename có hiệu ứng xóa.
- Nếu thay đổi một yêu cầu thiết kế quan trọng mà không thể suy ra từ repo, nói rõ giả định hợp lý và chọn giải pháp ít xâm lấn nhất; chỉ hỏi khi thực sự không thể tiến hành an toàn.
- Kết thúc: tổng kết số commit đã push, các phần hoàn tất/chưa hoàn tất, hướng dẫn demo ngắn, các giới hạn STT/TTS/SignalR còn tồn tại. **Không merge sang main**.

## 6. BẮT ĐẦU

**Hãy kiểm tra repo/branch trước, chỉ thao tác trên `GiaThuan`, audit kiến trúc 3 Layers, rồi chủ động triển khai từng commit và PUSH NGAY SAU MỖI COMMIT.** Không hỏi duyệt từng bước. Nếu cần xóa bất cứ thứ gì, phải giải thích lý do/tác động/phương án không xóa và chờ tôi đồng ý. Luôn giữ câu trả lời ngắn, dành token cho công việc thực tế.
