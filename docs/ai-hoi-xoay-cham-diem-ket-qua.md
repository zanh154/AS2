# AI hỏi xoáy, chấm điểm và kết quả (ASM2 — phần 6)

Trạng thái: **đã code xong, đã test, đã chạy thử với Ollama thật trên database cục bộ; chưa chạy trên database chung.**
Nhánh: `TriDev`, cắt từ `GiaThuan` (`200f5c0`). Ngày cập nhật: 2026-10-15.

Tài liệu này chỉ mô tả phần việc số 6 trong bảng phân công ASM2. Các phần đã có nằm ở
[README](../README.md); không chép lại nội dung của chúng ở đây.

---

## 1. Phần việc theo bảng phân công

| # | Đầu việc | Diễn giải |
|---|---|---|
| 1 | Phân tích transcript để phát hiện thiếu ý / mơ hồ / mâu thuẫn và sinh câu hỏi tiếp | AI đọc transcript câu trả lời bằng giọng nói, quyết định có hỏi xoáy không và hỏi gì |
| 2 | Tuân thủ giới hạn lượt / thời gian | Không vượt `exam_viva_settings.max_follow_ups_per_question`, không vượt deadline câu và giờ kết thúc ca |
| 3 | Đánh giá theo rubric | Đối chiếu transcript với `rubrics` / `rubric_criteria` của câu hỏi |
| 4 | Lưu điểm từng tiêu chí và nhận xét | Điểm + nhận xét theo từng tiêu chí, kèm điểm mạnh / điểm yếu / ý còn thiếu |
| 5 | Chuyển trang kết quả sang Razor Pages | `ExamResultsController` (MVC) → `Pages/ExamResults/*` |
| 6 | Cho giảng viên xem và điều chỉnh điểm, có lưu dấu vết | Human-in-the-loop: AI đề xuất, giảng viên chốt; mọi lần sửa đều ghi log |
| 7 | Thông báo tiến độ / kết quả qua SignalR | Tiến độ chấm AI và kết quả chốt đẩy realtime |

---

## 2. Hiện trạng làm nền

| Thành phần | Có sẵn | Nguồn |
|---|---|---|
| Phòng thi giọng nói, TTS/STT, transcript, audio riêng tư | ✅ | `VoiceRoomService`, `Pages/VoiceRoom`, `VoiceRoomHub` (GiaThuan) |
| Cấu hình thời gian/câu và số lượt hỏi xoáy | ✅ | `exam_viva_settings`, `Pages/ExamSessions/Settings` |
| Quy tắc ngân sách hỏi xoáy (thuần, test được) | ✅ nhưng **chưa ai gọi** | `VivaBudgetRules.CanAskFollowUp` |
| Rubric + tiêu chí + trang soạn rubric | ✅ | `Rubric`, `RubricCriteria`, `RubricService`, `Pages/Rubrics` |
| AI cục bộ (Ollama, structured output) | ✅ | `DocumentService.CallOllamaGenerateAsync`, `OllamaOptions` |
| Kết quả phiên thi + bài làm + chốt ca hết giờ | ✅ nhưng **MVC và chỉ cho trắc nghiệm** | `ExamResultService`, `Controllers/ExamResultsController` |
| Hỏi xoáy | ⚠️ chỉ là **trắc nghiệm 2 vòng** chọn từ ngân hàng, không đọc transcript | `QuestionService.PlanFollowUps` |
| Chấm điểm | ⚠️ chỉ đếm đáp án trắc nghiệm đúng/sai | `ExamScoring` |
| Bảng `follow_up_questions`, `ai_evaluations`, `final_results`, `exam_recordings` | ❌ có trong DB, **không có entity, không ai dùng** | `database/dump/aives_20261002.sql` |

---

## 3. Khoảng cách so với mục tiêu đề tài

| # | Khoảng cách | Mức |
|---|---|---|
| G1 | Hỏi xoáy hiện không dựa trên câu trả lời: chọn câu trắc nghiệm có sẵn theo đúng/sai và độ khó, không đọc transcript | Cao |
| G2 | Không có chấm điểm theo rubric. `ExamScoring` chỉ cộng câu đúng; rubric soạn xong không được dùng ở đâu | Cao |
| G3 | Không có điểm AI, nhận xét, điểm mạnh/yếu, ý còn thiếu cho câu trả lời nói | Cao |
| G4 | Không có vòng human-in-the-loop: không có nơi giảng viên sửa điểm, không có dấu vết sửa | Cao |
| G5 | Trang kết quả còn ở MVC, trong khi ASM2 yêu cầu Razor Pages | Trung bình |
| G6 | Phòng thi giọng nói chỉ hỏi hết các câu đã phát rồi kết thúc; `MaxFollowUpsPerQuestion` không có tác dụng | Cao |
| G7 | Không có thông báo realtime cho tiến độ chấm / kết quả | Trung bình |
| G8 | Sinh viên chưa xem được báo cáo sau thi (điểm từng câu, nhận xét) | Trung bình |

### Lỗi kỹ thuật phát hiện khi khảo sát (ngoài phạm vi phần 6, cần báo nhóm)

| # | Vấn đề | Bằng chứng | Mức |
|---|---|---|---|
| B1 | Thư mục output `bin-alt/` bị commit; Web SDK nhận các file `*.json` trong đó làm `Content` rồi copy vào `bin/Debug/net10.0/bin-alt/Debug/net10.0/...`, **nhân đôi độ sâu đường dẫn**. Ở thư mục sâu thì vượt 260 ký tự của Windows → 4 lỗi `MSB3030`; ở thư mục bình thường thì build sạch | Build lỗi ở thư mục tạm dài; build **thành công 0 lỗi 0 warning** tại `PRN222/AS2` | Trung bình |
| B2 | 181 file nhị phân (`*.dll`, `*.exe`, `*.pdb`, runtime của sass) bị commit từ `9a93575`, có mặt cả trên `main` | `git ls-files \| grep bin-alt` | Cao |
| B3 | Đường dẫn trong `bin-alt/.../runtimes/linux-arm64/...` vượt giới hạn 260 ký tự của Windows → `git clone` thất bại nếu chưa bật `core.longpaths` | Lần clone đầu báo `Filename too long` | Cao |
| B4 | `builder.Services.AddRazorPages()` bị gọi hai lần trong `Program.cs` | `Program.cs:15` và `Program.cs:33` | Thấp |
| B5 | `database/` có 4 script rời và 1 dump; `docs/asm2-integration` ghi nhận các migration ASM2 **chưa chạy trên DB dùng chung** | `database/README.md` | Trung bình |

---

## 4. Thiết kế đề xuất

### 4.1 Nguyên tắc

- Giữ nguyên 3 tầng Presentation → Business → DataAccess; luật nghiệp vụ nằm ở lớp static thuần để test không cần DB, theo đúng cách `FollowUpPlanner` / `ExamScoring` / `OverdueSlotRules` đang làm.
- **Không sửa lại phòng thi giọng nói của GiaThuan**: chỉ cắm thêm bước "hỏi xoáy" vào sau khi một câu được chốt.
- AI là **đề xuất**, không bao giờ là điểm cuối. Điểm cuối luôn do giảng viên chốt (`final_results`).
- AI hỏng (Ollama tắt, timeout, JSON sai) **không được làm hỏng bài thi**: bỏ qua hỏi xoáy, chấm để trạng thái "chờ chấm", ghi log.
- Không ghi dữ liệu giả vào `ai_evaluations` / `follow_up_questions`.

### 4.2 Database

| Loại | Nội dung | Lý do |
|---|---|---|
| Dùng lại | `follow_up_questions` | Mỗi câu hỏi xoáy là 1 dòng gắn với `answer_id`, có `question_text`, `answer_transcript`, `audio_path`, `reason`, `order_no`, `asked_at`, `answered_at` — vừa đủ |
| Dùng lại | `ai_evaluations` | Điểm AI tổng của 1 answer + feedback/strengths/weaknesses/missing_points |
| Dùng lại | `final_results` | `ai_score`, `lecturer_score`, `final_score`, `lecturer_comment`, `finalized_by`, `finalized_at` |
| Dùng lại | `exam_logs` | Dấu vết sửa điểm (`action = 'result.adjusted'`) |
| **Thêm mới** | `ai_evaluation_criteria(evaluation_id, criteria_id, score, comment)` | `ai_evaluations` chỉ có điểm tổng, đề bài yêu cầu **điểm từng tiêu chí** |

Script mới: `database/20261016_ai_evaluation_criteria.sql`, additive, `CREATE TABLE IF NOT EXISTS`, không sửa/xoá bảng cũ.

### 4.3 Luồng hỏi xoáy (cắm vào phòng thi giọng nói)

```
Sinh viên nộp câu N (transcript đã lưu)
        │
        ▼
VivaBudgetRules.CanAskFollowUp(đã hỏi cho câu N, đã hỏi trong ca, còn giờ?)
        │ không ──► sang câu N+1 như hiện nay
        ▼ có
FollowUpAnalyzer (Ollama, structured output)
   vào: câu hỏi gốc + expected_answer + rubric + transcript
   ra:  { needsFollowUp, reason: Thiếu ý|Mơ hồ|Mâu thuẫn|Đủ, question }
        │ needsFollowUp = false, hoặc AI lỗi/timeout ──► sang câu N+1
        ▼ true
Ghi follow_up_questions (asked_at) ──► TTS đọc ──► sinh viên trả lời ──► answer_transcript
        │
        └──► lặp lại tối đa MaxFollowUpsPerQuestion lần, rồi sang câu N+1
```

### 4.4 Luồng chấm điểm

```
Lượt thi chuyển Completed
        │
        ▼ hàng đợi nền (BackgroundService, giống DocumentProcessingBackgroundService)
Với mỗi answer:  rubric của câu hỏi + transcript chính + transcript các câu hỏi xoáy
        │
        ▼ Ollama structured output
{ criteria: [{criteriaId, score, comment}], strengths, weaknesses, missingPoints, confidence }
        │
        ▼ RubricScoring (static, thuần)
  - kẹp score vào [0, max_score] của tiêu chí
  - tiêu chí thiếu trong phản hồi AI = 0 điểm, ghi chú "AI không đánh giá"
  - điểm answer = Σ score / Σ max_score × 10
  - điểm lượt thi = trung bình có trọng số: câu chính 1, câu hỏi xoáy 0,5 (giữ đúng ExamScoring hiện có)
        │
        ▼
ai_evaluations + ai_evaluation_criteria + final_results.ai_score
        │
        ▼ SignalR "grading-progress" / "grading-done" tới giảng viên
```

### 4.5 Human-in-the-loop và dấu vết

- Trang bài làm của giảng viên hiện điểm AI từng tiêu chí + nhận xét, và ô nhập điểm của giảng viên.
- Lưu: `final_results.lecturer_score`, `lecturer_comment`, `finalized_by`, `finalized_at`; `final_score = lecturer_score ?? ai_score`.
- Mỗi lần lưu ghi `exam_logs`: ai sửa, điểm cũ → điểm mới, lúc nào. Không xoá bản ghi cũ.
- Chỉ giảng viên phụ trách phiên hoặc admin được sửa (kiểm tra ở tầng Business như `ExamResultService` đang làm).

### 4.6 Presentation

| Từ | Sang |
|---|---|
| `Controllers/ExamResultsController.cs` + `Views/ExamResults/*` | `Pages/ExamResults/Index.cshtml(.cs)`, `Pages/ExamResults/Candidate.cshtml(.cs)` |
| — | `Pages/ExamResults/MyResult.cshtml` — sinh viên xem báo cáo sau thi |
| — | Hub `ExamUpdatesHub` thêm nhóm chấm điểm, hoặc `GradingHub` riêng |

Route cũ `/ExamResults/Index/{id}` giữ redirect sang Razor Page để không gãy link của đồng đội.

---

## 5. Roadmap và danh sách task

Mỗi task = 1 commit lên `TriDev`. Tổng 14 commit.

| # | Task | Phụ thuộc | Kết quả |
|---|---|---|---|
| T0 | Dọn B1–B3: gỡ `bin-alt` khỏi Git, bổ sung `.gitignore`, ghi chú `core.longpaths` trong README | — | Clone nhẹ hơn, build được cả ở thư mục sâu |
| T1 | Script `20261016_ai_evaluation_criteria.sql` + entity + mapping cho 4 bảng chưa map | — | DB sẵn sàng |
| T2 | `IFollowUpAnalyzer` + `OllamaFollowUpAnalyzer` (phân tích thiếu ý/mơ hồ/mâu thuẫn) + fallback khi AI lỗi | T1 | Có contract AI, test bằng fake |
| T3 | `FollowUpTurnRules` (static, thuần): ngân sách lượt + thời gian, dùng `VivaBudgetRules` | T1 | Unit test đầy đủ |
| T4 | Cắm hỏi xoáy vào `VoiceRoomService` + repository + contract trạng thái | T2, T3 | Phòng thi hỏi xoáy được |
| T5 | UI phòng thi: hiện câu hỏi xoáy, nhãn "Hỏi thêm", TTS đọc, đồng hồ riêng | T4 | Sinh viên trả lời được |
| T6 | `RubricScoring` (static, thuần): kẹp điểm, tiêu chí thiếu, quy đổi thang 10 | T1 | Unit test đầy đủ |
| T7 | `IAnswerGrader` + `OllamaAnswerGrader` + lưu `ai_evaluations` / `ai_evaluation_criteria` | T6 | Chấm được 1 answer |
| T8 | `GradingBackgroundService` + hàng đợi + `final_results.ai_score` | T7 | Chấm cả lượt thi |
| T9 | SignalR tiến độ/kết quả chấm | T8 | Giảng viên thấy realtime |
| T10 | Chuyển `ExamResults` sang Razor Pages (giữ redirect route cũ) | — | Đúng yêu cầu ASM2 |
| T11 | Màn hình giảng viên: xem điểm AI từng tiêu chí, sửa điểm, lưu `final_results` + `exam_logs` | T10, T8 | Human-in-the-loop |
| T12 | Trang kết quả của sinh viên (điểm từng câu, nhận xét AI, không lộ `expected_answer`) | T11 | Nhóm chức năng 6 |
| T13 | Kiểm thử end-to-end + rà soát toàn hệ thống + cập nhật tài liệu | tất cả | Báo cáo cuối |

Tất cả T0–T13 đã làm xong, mỗi task một commit trên `TriDev`.

### Task đầu tiên và lý do ưu tiên

**T1 — script `ai_evaluation_criteria` + entity/mapping cho 4 bảng chưa map.** Lý do: mọi task AI
(T2, T4, T7, T8, T11, T12) đều đọc/ghi `follow_up_questions`, `ai_evaluations`, `final_results`,
`exam_recordings` — có sẵn trong DB nhưng **không có entity nào**. Task này độc lập, không đụng code
của đồng đội, dễ review và dễ hoàn tác.

**T0 không còn là việc chặn.** Đo thực tế 15/10/2026 tại `PRN222/AS2`: `dotnet build AssignmentPRN.slnx`
→ **thành công, 0 lỗi 0 warning**; `dotnet test` → **227/227 pass**. `bin-alt` chỉ gây lỗi khi project
nằm dưới thư mục sâu, và `git clone` vẫn cần `core.longpaths` trên Windows. Vì vậy T0 hạ thành việc
dọn dẹp, làm kèm hoặc bỏ nếu nhóm chưa muốn đụng file chung.

---

## 6. Tiêu chí nghiệm thu

| # | Tiêu chí |
|---|---|
| A1 | Clone sạch `TriDev` → `dotnet build` và `dotnet test` đều pass (nền hiện tại: 227/227), không cần thao tác tay nào ngoài `appsettings.json` |
| A2 | Khi `max_follow_ups_per_question > 0` và transcript thiếu ý, hệ thống đọc **câu hỏi xoáy do AI sinh**, gắn đúng `answer_id`, có `reason` |
| A3 | Không bao giờ vượt `max_follow_ups_per_question`, không bao giờ vượt deadline câu hoặc giờ kết thúc ca |
| A4 | Tắt Ollama: bài thi vẫn chạy trọn vẹn, chỉ không có hỏi xoáy; có log, không có dòng rác trong DB |
| A5 | Mỗi answer có `ai_evaluations` + 1 dòng `ai_evaluation_criteria` cho **mỗi tiêu chí** của rubric; Σ điểm ≤ Σ `max_score` |
| A6 | Giảng viên sửa điểm → `final_results` cập nhật, `exam_logs` có đúng 1 dòng ghi điểm cũ → mới + người sửa |
| A7 | Giảng viên A không mở được kết quả phiên của giảng viên B; sinh viên mở `/ExamResults` → AccessDenied |
| A8 | Sinh viên xem được điểm từng câu và nhận xét của mình, **không** thấy `expected_answer` và không thấy bài người khác |
| A9 | Trang kết quả chạy trên Razor Pages; route MVC cũ redirect, không 404 |
| A10 | SignalR báo tiến độ chấm; mất kết nối thì polling vẫn ra đúng trạng thái |
| A11 | Giao diện 375px dùng lại token/`_DataTable`/`metric-card` sẵn có, không hard-code màu |

## 7. Kế hoạch kiểm thử

**Unit (xUnit, không cần DB)** — mục tiêu ≥ 40 test mới:

| Nhóm | Ca kiểm thử |
|---|---|
| `FollowUpTurnRules` | hết lượt; hết giờ câu; hết giờ ca; `MaxFollowUps = 0`; AI trả `needsFollowUp = false`; AI timeout; AI trả JSON sai; câu hỏi rỗng/quá dài |
| `RubricScoring` | điểm vượt `max_score` bị kẹp; điểm âm bị kẹp; thiếu tiêu chí = 0; thừa `criteriaId` lạ bị bỏ; rubric rỗng; quy đổi thang 10; trọng số câu chính/hỏi xoáy; làm tròn 1 chữ số |
| `ResultAdjustmentRules` | `final_score` theo `lecturer_score`; chưa chấm; điểm ngoài [0,10]; không phải chủ phiên |

**Integration (SQLite riêng, như `VoiceRoomHttpTests` đang làm)**: nộp câu → sinh hỏi xoáy (fake analyzer) → trả lời → chấm (fake grader) → giảng viên sửa điểm → log; CSRF; phân quyền chéo; nhóm SignalR trái quyền.

**Thủ công trên trình duyệt**: 1 ca thi thật với Ollama bật, 1 ca với Ollama tắt, kiểm tra A2–A4, A8, A11.

**Điều kiện hoàn thành**: toàn bộ test cũ (227 .NET + 13 JS) vẫn pass, test mới pass, A1–A11 đạt, tài liệu này cập nhật kết quả thực đo.

---

## 8. Kết quả thực hiện

### Đo được sau khi làm xong (15/10/2026)

| Mục | Trước | Sau |
|---|---|---|
| Test .NET | 227 | **355**, pass hết |
| Test JavaScript | 13 | **21**, pass hết |
| Build Debug/Release | sạch | sạch, 0 warning |

### Những gì đã chạy được

| Đầu việc trong bảng phân công | Chỗ cài đặt |
|---|---|
| Phân tích transcript, sinh câu hỏi tiếp | `IFollowUpAnalyzer` + `OllamaFollowUpAnalyzer`, cắm vào `VoiceRoomService` |
| Giới hạn lượt / thời gian | `FollowUpTurnRules` (dùng lại `VivaBudgetRules` của nhóm) |
| Đánh giá theo rubric | `IAnswerGrader` + `OllamaAnswerGrader`, luật ở `RubricScoring` |
| Lưu điểm từng tiêu chí và nhận xét | `ai_evaluations` + `ai_evaluation_criteria` |
| Trang kết quả sang Razor Pages | `Pages/ExamResults/*`, URL giữ nguyên |
| Giảng viên sửa điểm có dấu vết | `ResultReviewService` + `final_results` + `exam_logs` (`result.adjusted`) |
| Thông báo tiến độ qua SignalR | Server: `SignalRGradingNotifier`, nhóm `exam:{id}`. Trình duyệt: `wwwroot/js/grading-live.js` — trang bài làm hiện "AI đang chấm x/y câu" rồi tự tải lại khi xong; bảng điểm tự tải lại khi một bài trong phiên chấm xong; đang gõ dở điểm/nhận xét thì chỉ báo, không tải lại |

### Quyết định đáng chú ý

- **AI không bao giờ quyết định điểm.** Giảng viên nhập điểm là điểm đó thắng, và chấm lại
  sau này cũng không ghi đè lên nó.
- **AI hỏng không làm hỏng bài thi.** Ollama tắt, timeout, trả JSON sai đều ra cùng một kết
  quả: không hỏi xoáy, không chấm, bài thi chạy tiếp. Có 4 test riêng cho trường hợp này.
- **Không bao giờ ghi điểm 0 thay cho "chưa chấm".** Câu không có rubric, sinh viên không
  nói gì, hoặc model không đọc được đều được báo là *chưa chấm* để giảng viên tự xử lý.
- **Câu tiếp theo không mở khi AI đang nghĩ**, nếu không đồng hồ của nó chạy trong lúc sinh
  viên ngồi chờ. Quá 25 giây không có quyết định thì tự mở.
- **Câu hỏi xoáy dùng thời gian còn lại của chính câu nó hỏi xoáy**, không cộng thêm giờ.

### Lỗi phát hiện trong lúc làm

| # | Vấn đề | Xử lý |
|---|---|---|
| 1 | Lượt thi do giảng viên **chốt ca hết giờ** không ai đưa vào hàng đợi chấm, phải đợi khởi động lại app mới được quét | Đã sửa: `CloseOverdueSlotsAsync` đẩy vào hàng đợi |
| 2 | Thẻ `<a>` ghi cả `asp-page` lẫn `asp-controller` thì ASP.NET Core ném lỗi, kể cả khi giá trị rỗng | Tách thành hai nhánh markup trong `_DataTable` |
| 3 | `id` của câu hỏi xoáy trả về client bằng 0 ở giao dịch tạo ra nó, nên client không trả lời được | Đọc lại trạng thái ở giao dịch riêng sau khi lưu |
| 4 | `AddRazorPages()` gọi hai lần trong `Program.cs` | Đã bỏ một |
| 5 | `bin-alt/` bị commit, clone trên Windows cần `core.longpaths` | Đã gỡ khỏi Git, ghi chú trong README |

---

## 9. Đo thật với Ollama (15/10/2026)

Chạy thật một lần trên máy cá nhân, không dùng database chung: MySQL 8.4.9 cục bộ (dump
ASM1 + cả 5 migration + `seed_test_flow.sql`), Ollama 0.40.4, `qwen2.5:3b`, Chromium.
Máy: i7-13620H, 32 GB RAM, **RTX 4060 Laptop — model nạp trọn 36/36 lớp lên GPU**, nên các
số dưới đây là *cận tốt*; máy chỉ có CPU sẽ chậm hơn nhiều.

Cách thi: hai lượt thi giọng nói thật trên trình duyệt (SV1, SV2 của phiên `[TFLOW-1]`,
`seconds_per_question` 300, `max_follow_ups_per_question` 2, rubric 3 tiêu chí / 10 điểm cho
cả 12 câu Chính của môn TEST). Transcript nhập bằng tay vào ô "Văn bản nhận dạng" — ô này
cho sửa, nên không cần microphone; phần nhận dạng giọng nói của trình duyệt vẫn chưa được đo.

### (a) Độ trễ từ lúc nộp câu tới lúc câu kế tiếp mở

| Tình huống | Đo được |
|---|---|
| **Lần gọi đầu tiên sau khi bật máy** (model chưa nạp) | **33,4 s** — nạp model mất 32 s, vượt ngân sách 20 s của `OllamaFollowUpAnalyzer.Budget`, nên **không có câu hỏi xoáy nào** và câu kế tiếp chỉ mở sau khi hết giờ chờ |
| Model đã nạp (mọi lượt sau) | **0,5 – 3,0 s** đo tại trình duyệt; bản thân model chỉ 0,55 – 1,76 s cho 26 – 200 token |

Kết luận: **ngân sách 20 s là đủ khi model đã nạp, và chắc chắn không đủ cho lần gọi đầu.**
Cần "làm nóng" model trước ca thi (một lần gọi `/api/generate` bất kỳ) — nếu không, câu đầu
của phiên thi đầu tiên mỗi ngày luôn mất câu hỏi xoáy và sinh viên ngồi chờ hơn 30 giây.

### (b) Chất lượng câu hỏi xoáy tiếng Việt — điểm yếu lớn nhất

Trên **10 câu trả lời yếu được dựng có chủ ý** (mơ hồ, thiếu ý, tự mâu thuẫn, "em không
biết"), gọi đúng prompt của `OllamaFollowUpAnalyzer`:

- **0/10 lần sinh viên nhận được câu hỏi xoáy dùng được.**
- 7/10 lần model trả `reason = "Sufficient"` cho câu trả lời rõ ràng là thiếu ý — kể cả
  "Em không biết ạ." và "Vì IQueryable nhanh hơn ạ.".
- 2/10 lần model nhận ra vấn đề (`Contradictory`) nhưng để `question` rỗng, hoặc đặt
  `needsFollowUp: false` cùng lúc với một `reason` khác `Sufficient` → `Parse` loại bỏ,
  đúng theo thiết kế.
- 1/10 lần có `question`, nhưng **viết cho giảng viên chứ không hỏi sinh viên**: *"Sinh viên
  đã nói rằng… Bạn có thể hỏi sinh viên về nguyên nhân của sự mâu thuẫn này không?"* — dài
  hai câu, sai người nhận, không dùng được nếu đọc lên cho sinh viên.

Phần luật (`Parse`, `FollowUpTurnRules`) chạy đúng: cái gì không dùng được đều bị loại và bài
thi vẫn chạy trọn vẹn. Vấn đề nằm ở model: `qwen2.5:3b` quá nhỏ để làm giám khảo tiếng Việt.
**Đã sửa (15/10/2026)**, ba thay đổi cùng lúc:

1. **Tách model cho vấn đáp**: `Ollama:VivaModel` mặc định `qwen2.5:7b-instruct`, dùng cho
   hỏi xoáy và chấm điểm; `GenerationModel` (`qwen2.5:3b`) vẫn lo sinh câu hỏi từ tài liệu.
   Để trống thì quay về model nhỏ, nên máy chưa tải bản 7b vẫn chạy được.
2. **Viết lại prompt**: nêu rõ từng trường hợp chọn `reason` (nói không biết, nhắc lại từ
   trong câu hỏi, nói chung chung… đều **không** phải `Sufficient`), mặc định là phải hỏi
   thêm, và bắt `question` là lời nói trực tiếp với sinh viên ở ngôi thứ hai, mở đầu bằng
   "Em".
3. **Luật mới trong `Parse`**: câu không có dấu hỏi thì bị loại. Nó chặn đúng lỗi 1/10 ở
   trên — một câu mô tả việc cần hỏi, dành cho giảng viên đọc.

`Budget` và `num_predict` vẫn như cũ, vì độ trễ khi model đã nạp không phải là chỗ tắc.
Chưa đo lại 10 câu đó với model 7b.

### (c) Chấm điểm theo rubric

- Một lượt thi 3 câu, rubric 3 tiêu chí mỗi câu: **3,7 – 4,0 s model cho mỗi câu**
  (315 – 344 token ra), **~11,5 s** cho cả lượt; tính cả nạp lại model và vòng quét là
  20 – 25 s. Ngân sách 2 phút của `OllamaAnswerGrader` còn rất dư.
- Điểm đưa ra hợp lý: câu đúng và gọn 8/10, câu mơ hồ 2/10 và 4/10, câu sai hẳn 0/10;
  `confidence` 0,5 – 1,0. Nhận xét từng tiêu chí bằng tiếng Việt đọc được, có dẫn lại lời
  sinh viên.
- Ba điểm chưa ổn, giảng viên cần đọc lại trước khi chốt:
  1. Có lần model **nói sinh viên đã nêu đúng khái niệm trong khi sinh viên không hề nói ra
     từ khoá đó** ("nêu đúng khái niệm về kiểu dữ liệu int" cho câu trả lời không có chữ
     "int"), tuy điểm vẫn chấm thấp 1/5.
  2. Tiêu chí "Trình bày rõ ràng" nhiều lần được **2/2 dù nhận xét ngay bên cạnh ghi là lan
     man**.
  3. `missing_points` có lúc **lọt số id tiêu chí vào văn bản** cho giảng viên đọc: "mất
     điểm ở tiêu chí 87 và 99". **Đã sửa**: prompt dặn gọi tiêu chí bằng tên, và
     `OllamaAnswerGrader.Scrub` bỏ mọi chỗ nhắc mã tiêu chí khỏi cả bốn ô nhận xét —
     không dựa vào việc model nghe lời, vì sinh viên đọc trực tiếp những dòng này.

  Hai lỗi 1 và 2 **không sửa**: đó là bản chất của model nhỏ, và thiết kế đã có giảng viên
  duyệt lại trước khi chốt điểm (mục 6). Chúng nằm trong phần giới hạn ở mục 10.

### Lỗi phát hiện khi chạy thật

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | **Nộp xong lượt thi không kích hoạt chấm.** Hai lượt thi đều ở `Completed` nhiều phút mà không có dòng nào trong `ai_evaluations`; chỉ khi khởi động lại app, vòng quét đầu của `GradingBackgroundService` mới chấm. Tức là kết quả AI **không bao giờ xuất hiện trong ca thi đang diễn ra** | **Đã sửa** — xem mục 9.1 bên dưới |
| 2 | **Câu cuối của lượt thi không bao giờ được hỏi xoáy**: trả lời câu cuối làm lượt thi chuyển sang `Completed` nên không còn lượt nào để hỏi thêm | **Không phải lỗi** — xem mục 9.2 bên dưới |
| 3 | `scripts/start-local-ai.ps1` lưu UTF-8 không BOM nên **Windows PowerShell 5.1 không parse được** (lỗi ở dòng có tiếng Việt). Phải tự chạy `ollama serve`. PowerShell 7 không bị | Chưa sửa — thêm BOM hoặc bỏ tiếng Việt trong script |

### 9.1 Lỗi 1 — nguyên nhân và cách sửa

Nguyên nhân không nằm ở chỗ thiếu lời gọi `QueueCandidateAsync`: `SyncAsync` có gọi. Nó
nằm ở **chỗ đọc trạng thái**. `SyncAsync` quyết định "lượt thi vừa kết thúc" dựa trên giao
dịch ĐẦU, nhưng một lượt thi giọng nói hầu như luôn kết thúc ở giao dịch THỨ HAI: sau câu
cuối, phòng thi giữ chỗ chờ giám khảo quyết định (`AwaitingFollowUp`), và chính
`ReleaseHoldAsync` — hoặc giao dịch đọc lại sau khi ghi câu hỏi thêm — mới đặt
`Completed`. Ở giao dịch đầu lượt thi vẫn `InProgress`, nên không ai đưa nó vào hàng đợi.

Thêm một bài test tạm thời dựng lại logic cũ cho thấy phạm vi còn rộng hơn báo cáo ban
đầu: **không một lượt thi giọng nói nào từng được đưa vào hàng đợi**, kể cả đường trả lời
câu hỏi xoáy xong mới kết thúc. Và đường nộp bài trắc nghiệm
(`QuestionService.SubmitExamAsync`) thì chưa bao giờ có lời gọi nào cả.

Đã sửa ba chỗ:

1. `SyncAsync` đọc trạng thái **cuối** (`result.Data.Completed`) thay vì trạng thái sau
   giao dịch đầu.
2. `QuestionService.SubmitExamAsync` đẩy lượt thi vào hàng đợi khi bài nộp làm lượt thi
   đóng.
3. `GradingBackgroundService` quét lại mỗi phút (`SweepInterval`), không chỉ một lần lúc
   khởi động — lưới an toàn cho mọi đường kết thúc khác, ví dụ hết giờ ca thi. Kèm theo,
   `GradingQueue` tự bỏ bản trùng để một lượt thi không bị chấm hai lần bằng hai lần gọi
   model.

Bảo vệ bằng 7 bài test mới (`GradingQueueTests`, `VoiceFollowUpTests`, `ExamDraftTests`);
hai trong số đó đã được xác nhận là **thất bại trên code cũ**.

### 9.2 Lỗi 2 — không phải lỗi

Câu cuối **vẫn được hỏi xoáy**: `Apply` tính `awaiting` TRƯỚC khi đặt `Completed`, và chỉ
đóng lượt thi khi không còn câu hỏi thêm nào chờ. Hai bài test đã có từ trước chứng minh
điều này — `The_slot_is_not_finished_while_a_probe_on_the_last_answer_is_open` và
`Answering_the_last_probe_finishes_the_slot` — và cả hai vẫn xanh.

Trường hợp duy nhất câu cuối không được hỏi là khi **còn dưới 20 giây**
(`FollowUpTurnRules.MinimumTimeToProbe`) trước hạn câu hỏi hoặc hết ca thi: không đủ thời
gian để đọc câu hỏi lên và nghe sinh viên trả lời. Đó là thiết kế có chủ ý — câu hỏi thêm
không bao giờ mua thêm thời gian cho sinh viên. Lần đo không ghi lại câu cuối được trả lời
lúc còn bao nhiêu giây, nên chưa khẳng định được đây là điều đã xảy ra; lần đo sau nên ghi
thêm mốc đó.

---

### 9.3 Chạy thử lần 2, sau khi sửa (15/10/2026, 23:00–23:16)

Cùng máy, MySQL 8.4.9 cục bộ mới dựng (dump + 5 migration + seed + rubric 3 tiêu chí cho 12
câu Chính), app chạy thật, trình duyệt thật. **Vẫn dùng `qwen2.5:3b`** (đặt qua biến môi
trường `Ollama__VivaModel`) vì máy chưa tải bản 7b — nên phần chất lượng dưới đây là của
model nhỏ, với prompt mới.

**Chấm điểm trong ca thi — đã chạy:**

| Lượt thi | Nộp câu cuối | Có điểm AI | Ghi chú |
|---|---|---|---|
| SV1 (slot 75) | 23:01:51 | 23:02:33 (**42 s**) | Phải đợi 7 lượt cũ trong hàng đợi — xem lỗi A |
| SV2 (slot 76) | 23:13:20 | 23:13:45 (**25 s**) | Nộp 4 s sau một nhịp quét, nhịp kế tiếp ở 23:14:16 → chắc chắn do hàng đợi lúc nộp, không phải vòng quét |

Không khởi động lại app lần nào. Mỗi câu có đủ 3 dòng `ai_evaluation_criteria`,
`final_results` có điểm. Giảng viên chốt 4 điểm (AI đề xuất 1) → `final_results` giữ cả hai,
`exam_logs` có dòng "Điểm chốt: 1 → 4. Nhận xét: …", trang sinh viên hiện 4,0 kèm nhận xét
giảng viên. Không thấy mã tiêu chí nào trong nhận xét.

**Hỏi xoáy với prompt mới (vẫn 3b):** 4/6 câu có câu hỏi xoáy, cả 4 đều ở ngôi thứ hai
("Em có thể…"), không câu nào viết cho giảng viên. "Em không biết ạ." lần này được hỏi thêm
với lý do *thiếu ý* (trước đây 3b trả `Sufficient`). Ca thi cho tối đa 2 câu hỏi xoáy mỗi
lượt, nên 2 câu còn lại không được hỏi là đúng luật. Hai điểm còn kém:

- **2/4 câu hỏi xoáy lộ đáp án**, ví dụ *"Em có thể giải thích rõ hơn về khái niệm
  Dictionary<TKey, TValue> không?"* cho câu hỏi "Collection nào lưu cặp khoá - giá trị?", và
  *"…kiểu dữ liệu int lưu số nguyên 32 bit…"* cho câu hỏi "kiểu nào lưu số nguyên 32 bit?".
  Prompt đã cấm, model nhỏ vẫn làm. Chưa có luật nào trong code chặn được việc này.
- Câu trả lời tự mâu thuẫn ("int 32 bit… à không, int là 64 bit") được gắn nhãn *thiếu ý*
  thay vì *mâu thuẫn*. Không ảnh hưởng tới luồng thi, chỉ là nhãn hiển thị.

**Chấm điểm:** câu lạc đề 0/10, câu sai 1/10 — hợp lý. Nhưng một câu sinh viên **tự sửa đúng
ở phần hỏi thêm** vẫn bị 0/5 "Đúng khái niệm", và ô *Điểm mạnh* ("đã nêu đúng định nghĩa")
mâu thuẫn với ô *Điểm yếu* ("không nêu đúng định nghĩa"). Đây chính là trường hợp bước giảng
viên duyệt lại được thiết kế để bắt — và trong lần chạy này giảng viên đã sửa đúng nó.

**Lỗi tìm thấy trong lần chạy này:**

| # | Lỗi | Xử lý |
|---|---|---|
| A | **Vòng quét 1 phút (vừa thêm ở 9.1) đưa lại mãi các lượt không chấm được**: lượt có câu không rubric hoặc model không trả lời thì vẫn "chưa chấm" trong database, nên mỗi phút bị chấm lại — 7 lượt cũ lặp 3 lần trong 3 phút, chen trước lượt thi thật, và sẽ gọi model mỗi phút nếu model hỏng | **Đã sửa**: vòng quét nhớ những lượt đã thử trong lần chạy này. Mỗi lần khởi động app vẫn thử lại tất cả một lần như cũ. Test `GradingSweepTests` (đỏ trên code cũ) |
| B | **Trang kết quả sinh viên ghi "điểm giảng viên đã chốt" khi giảng viên chưa chốt gì** — điểm đó là điểm AI đề xuất | **Đã sửa**: chưa chốt thì ghi "Điểm tạm", "giảng viên chưa xác nhận nên có thể còn thay đổi" |
| C | **Hai trang kết quả của giảng viên mất tiêu đề tab (" · AIVES") và mất breadcrumb** — lỗi do việc chuyển sang Razor Pages: tiêu đề đặt trong partial, mà `<partial>` nhận bản sao `ViewData` | **Đã sửa**: đặt ở trang. Test HTTP kiểm tra cả hai (đỏ trên code cũ) |
| D | **`_Breadcrumb` ném lỗi 500 với mọi liên kết tới trang Courses / ExamSessions / ExamResults** — `asp-page` và `asp-controller` đặt cùng lúc bằng ternary; tag helper coi chúng loại trừ nhau kể cả khi giá trị null. Lỗi bị che vì chưa trang nào truyền breadcrumb tới được layout (lỗi C) | **Đã sửa**: tách hai nhánh markup, như đã làm với `_DataTable` |
| E | Trang bài làm của giảng viên có ô **"ĐIỂM 0,0"** (điểm trắc nghiệm cũ của ASM1, luôn 0 với bài thi giọng nói) nằm cạnh **"AI ĐỀ XUẤT 1,0"** — hai con số "điểm" mâu thuẫn trên cùng màn hình | **Chưa sửa** — cần nhóm thống nhất: ẩn ô cũ với bài giọng nói, hay đổi tên |
| G | **Thông báo SignalR chỉ có nửa server**: `SignalRGradingNotifier` gửi `GradingChanged` nhưng không trang nào nghe, và hai trang kết quả còn chưa vào nhóm SignalR của phiên — giảng viên phải tự F5 | **Đã sửa**: `grading-live.js` + `data-exam-id` trên hai trang. Chạy thật: SV2 nộp 00:02:33, trang giảng viên hiện 1/3 → 2/3 → 3/3 rồi tự tải lại lúc 00:02:51, không ai bấm gì. 8 test trình duyệt + kiểm HTTP |
| F | **Các trang Courses / ExamSessions (phần của thành viên khác) cũng đặt breadcrumb trong partial**, nên cũng không có breadcrumb; tiêu đề tab là chuỗi tạm như "ExamSessions · Details" | **Không sửa** — ngoài phần 6, báo nhóm. Sửa giống lỗi C; lỗi D đã sửa nên chuyển lên sẽ không còn 500 |

## 10. Giới hạn — cần biết trước khi demo

- **Chất lượng câu hỏi xoáy phụ thuộc model**: với `qwen2.5:3b`, 0/10 lần sinh ra câu hỏi
  dùng được (mục 9b). Đã đổi mặc định sang `qwen2.5:7b-instruct` và viết lại prompt, nhưng
  **chưa đo lại** — phải chạy lại 10 câu ở mục 9b trước khi demo. Máy nào chưa
  `ollama pull qwen2.5:7b-instruct` thì chức năng này coi như không có.
- **Làm nóng model**: `OllamaWarmUpService` gọi model một lần lúc khởi động, nên 32 giây nạp
  model được trả trước khi có ai thi (trước đây câu đầu tiên mỗi ngày mất hơn 30 giây và
  không có câu hỏi xoáy — mục 9a). App phải được khởi động trước ca thi, không phải cùng lúc.
- **Model chấm điểm vẫn có thể nhận xét sai** (mục 9c, lỗi 1 và 2): nói sinh viên đã nêu một
  từ khoá họ không nói, hoặc cho điểm trình bày lệch với nhận xét ngay bên cạnh. Điểm AI là
  **đề xuất**; giảng viên phải đọc lại trước khi chốt. Đây là lý do module có bước duyệt tay.
- **Mất kết nối SignalR thì trang kết quả không tự bắt kịp**: sự kiện chấm xong trong lúc
  mất kết nối bị lỡ, giảng viên phải F5. Trang không có polling dự phòng (tiêu chí A10 mới
  đạt phần realtime).
- **Sinh viên trong phiên cũng nhận sự kiện chấm điểm của bạn cùng phiên**, vì nhóm SignalR
  của phiên gồm cả sinh viên. Sự kiện không có điểm hay transcript, chỉ có mã lượt thi và số
  câu đã chấm; nếu cần kín hơn thì gửi riêng cho giảng viên và admin.
- **Câu hỏi xoáy có thể lộ đáp án** (mục 9.3) với model nhỏ. Prompt cấm nhưng code chưa chặn.
- **Model chấm có thể bỏ qua phần sinh viên tự sửa ở câu hỏi xoáy** (mục 9.3) — giảng viên
  cần đọc phần "Câu hỏi thêm" trước khi chốt.
- **Kết quả AI chạy trong ca thi**: lượt thi được đẩy vào hàng đợi ngay khi đóng, và có
  vòng quét mỗi phút làm lưới an toàn (mục 9.1). Chấm một lượt 3 câu mất khoảng 11 giây, nên
  giảng viên thấy điểm đề xuất trong vòng dưới một phút rưỡi sau khi sinh viên nộp.
- Script `20261016_ai_evaluation_criteria.sql` **đã chạy trên database chung** (Aiven) ngày
  15/10/2026; lần đo này cũng chạy sạch trên MySQL 8.4.9 cục bộ. Kiểm tra lại bằng
  `scripts/apply-migration.ps1 -WhatIf`.
- **Chưa đo trên máy chỉ có CPU.** Mọi số ở mục 9 đo khi model nằm trọn trên RTX 4060; máy
  không GPU sẽ chậm hơn nhiều và có thể vượt cả ngân sách 20 s khi model đã nạp.
- **Chưa đo phần nhận dạng giọng nói của trình duyệt**: transcript được nhập tay vào ô cho
  sửa, nên microphone và độ chính xác nhận dạng tiếng Việt vẫn chưa được thử.
- **Bản thu của câu hỏi xoáy chưa tải lại được** từ trang lịch sử: `DownloadAsync` mới đọc
  audio của câu chính. Dữ liệu vẫn được lưu, chỉ thiếu đường tải.
- Chấm chạy **tuần tự một lượt thi một lúc**, vì model cục bộ cấu hình một request. Với
  ~4 s/câu thì một lớp 30 sinh viên × 3 câu mất khoảng 6 phút trên máy có GPU.
- Không ghi hình, không xuất Excel — nằm ngoài phần việc 6.
- `exam_recordings` vẫn chưa có entity: phần việc 6 không dùng tới nó.
