# Hướng Dẫn & Quy Tắc Ngữ Cảnh Dự Án AIVES (GEMINI.md)

Tài liệu này cung cấp toàn bộ ngữ cảnh kỹ thuật, kiến trúc, quy tắc phát triển và hướng dẫn kiểm thử cho Trợ lý AI (Gemini / Antigravity) và các lập trình viên tham gia dự án **AIVES - Hệ Thống Vấn Đáp Trực Tuyến Tích Hợp Trí Tuệ Nhân Tạo**.

---

## 1. Tổng Quan Kiến Trúc & Công Nghệ

* **Nền tảng:** .NET 10.0 (`net10.0`), ASP.NET Core Razor Pages.
* **Cơ sở dữ liệu:** MySQL (sử dụng Entity Framework Core 9.x/10.x & `MySqlConnector`).
* **Trí tuệ nhân tạo cục bộ (Local AI):**
  * Runtime: **Ollama Local** (`http://localhost:11434`).
  * Mô hình sinh văn bản & chấm điểm: `qwen3.5:4b`.
  * Mô hình nhúng vector (RAG / Embeddings): `nomic-embed-text`.
* **Frontend & Giao diện:**
  * Razor Pages, Bootstrap 5, Sass (`AspNetCore.SassCompiler` tự động build từ `Styles/` sang `UI Assets/css/`).
  * `WebRoot` tùy chỉnh: `<WebRoot>UI Assets</WebRoot>`.
  * Web Speech API (SpeechSynthesis & SpeechRecognition), Web Audio API (`AudioContext`, `AnalyserNode`), HTML5 Canvas (60 FPS Waveform Visualizer).

---

## 2. Quy Tắc Kiến Trúc 3 Lớp Bắt Buộc (Strict 3-Tier Layering)

Hệ thống được thiết kế theo nguyên tắc phân tầng nghiêm ngặt:
$$\text{AssignmentPRN.Presentation} \longrightarrow \text{AssignmentPRN.Business} \longrightarrow \text{AssignmentPRN.DataAccess}$$

> [!CAUTION]
> **Ràng buộc quan trọng:** Tầng **Presentation** bị cấu hình chặn ở cấp compiler (`HideDataAccessFromPresentation` trong `.csproj`), **tuyệt đối không được gọi trực tiếp `DataAccess` hoặc `AivesDbContext`**.
> Mọi thao tác truy xuất dữ liệu từ Presentation bắt buộc phải thông qua Services và Interfaces của tầng **Business**.

* **Tầng DataAccess (`AssignmentPRN.DataAccess`):**
  * Quản lý thực thể (`Entities/`), `AivesDbContext`, Enums (`CandidateStatus`, `ExamSessionStatus`).
  * Thực thi các Repository (`VoiceRoomRepository`, `GradingRepository`, `QuestionRepository`,...).
* **Tầng Business (`AssignmentPRN.Business`):**
  * Định nghĩa interfaces hợp đồng (`Interfaces/`), DTOs, Business Rules & Policies.
  * Xử lý nghiệp vụ thi vấn đáp (`VoiceRoomService`), dịch vụ chấm điểm AI (`GradingService`, `OllamaAnswerGrader`), dịch vụ nền (`GradingBackgroundService`).
* **Tầng Presentation (`AssignmentPRN.Presentation`):**
  * Xử lý giao diện Razor Pages (`Pages/VoiceRoom`, `Pages/ExamResults`, `Pages/StudentSchedule`).
  * Các client scripts: `voice-capture.js`, `voice-speech.js`, `voice-mic-check.js`, `voice-proctor.js`, `voice-room.js`, `grading-live.js`.

---

## 3. Các Tính Năng Cốt Lõi Cần Chú Ý Khi Sửa Code

### 3.1. Phòng thi giọng nói (`/VoiceRoom/{id}`)
* **Pre-flight Mic Check (`voice-mic-check.js`):** Kiểm tra thiết bị microphone trước khi vào làm bài. Dải sóng âm Equalizer & Level Meter đo volume thời gian thực, ghi âm 3s cho sinh viên nghe lại giọng mình. Ghi nhớ trạng thái qua `sessionStorage`.
* **Live Audio Waveform Visualizer (`voice-capture.js`):** Khi sinh viên bấm thu âm trả lời, thẻ `<canvas id="voice-live-canvas">` chạy 60 FPS vẽ 3 dải sóng Neon uốn lượn đa lớp phản hồi theo âm lượng thực tế.
* **Tự động nhận diện ngôn ngữ đọc câu hỏi (TTS & STT):**
  * Hàm `detectLanguage()` phân biệt câu hỏi Tiếng Việt (`vi-VN`) hay Tiếng Anh (`en-US`).
  * Tự động chọn Natural Voices chuẩn (*HoaiMy, NamMinh, Google Tiếng Việt*), không để trình duyệt dùng giọng tiếng Anh của OS đọc sai ngữ âm tiếng Việt.
* **Giám sát liêm chính & Chống gian lận (`voice-proctor.js`):**
  * Theo dõi sự kiện `visibilitychange` và `window.blur` khi sinh viên chuyển tab hoặc Alt+Tab.
  * **Lần vi phạm 1 – 3:** Hiển thị Modal Cảnh báo, ghi log kiểm toán vào `exam_logs`.
  * **Lần vi phạm 4 (quá 3 lần):** Đình chỉ thi ngay lập tức, khóa màn hình, chấm **0 điểm** (`FinalScore = 0`), gửi thông báo tới Giảng viên.

### 3.2. AI Tự Động Chấm Điểm (AI Auto-Grading)
* Sau khi sinh viên nộp bài thi, `VoiceRoomService` đẩy vào `GradingQueue`.
* `GradingBackgroundService` chạy nền độc lập, gọi `OllamaAnswerGrader` gửi câu hỏi, câu trả lời, và tiêu chí Rubric cho Ollama `qwen3.5:4b`.
* Đánh giá trả về: Điểm từng tiêu chí, Điểm mạnh (Strengths), Điểm yếu (Weaknesses), Ý còn thiếu (Missing Points).
* Real-time cập nhật bảng điểm qua SignalR (`SignalRGradingNotifier` & `grading-live.js`).

---

## 4. Quy Trình Kiểm Thử & Lệnh Thường Dùng

Mỗi khi chỉnh sửa hoặc thêm mới tính năng, bắt buộc phải chạy bộ test để đảm bảo không bị hồi quy (regression):

```powershell
# 1. Chạy toàn bộ 462+ unit/integration tests C#
dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj

# 2. Chạy toàn bộ 36+ browser/client tests JavaScript
node --test tests/browser/*.test.cjs

# 3. Build Presentation và biên dịch Sass SCSS
dotnet build src/AssignmentPRN.Presentation/AssignmentPRN.Presentation.csproj
```

---

## 5. Quy Ước Nhánh & Đồng Bộ Git
* **Nhánh làm việc chính của bạn:** `GiaThuan` (remote: `origin/GiaThuan`).
* **Nhánh kiến trúc:** `Design/Architeture`.
* Trước khi push: Luôn kiểm tra `git status`, test pass 100%, commit rõ ràng theo chuẩn Conventional Commits (ví dụ: `feat(voice): ...`, `fix(proctor): ...`).
