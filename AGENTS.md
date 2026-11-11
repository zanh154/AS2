# Quy Chuẩn Phối Hợp & Vai Trò AI Agents (AGENTS.md)

Tài liệu này định nghĩa vai trò, nguyên tắc làm việc và quy trình phối hợp giữa các AI Agents (và Developers) khi làm việc trên mã nguồn của dự án **AIVES**.

---

## 1. Danh Sách Vai Trò Của Các Agents (Agent Roles)

| Tên Vai Trò | Phạm Vi Trách Nhiệm | Công Nghệ Phụ Trách |
| :--- | :--- | :--- |
| **System Architect** | Giám sát ranh giới 3 lớp (`Presentation` $\rightarrow$ `Business` $\rightarrow$ `DataAccess`), bảo đảm không có circular dependencies và Presentation không gọi DataAccess. | .NET Core, Solution Structure, Dependency Injection |
| **Voice & Frontend Engineer** | Phát triển và tối ưu giao diện phòng thi `/VoiceRoom`, xử lý âm thanh Web Audio API, Canvas 60 FPS visualizer, Web Speech API (TTS & STT). | JavaScript (ES6+), Web Audio API, HTML5 Canvas, SCSS |
| **AI Integration Specialist** | Kết nối mô hình ngôn ngữ lớn (Ollama Local), viết prompts cho bài toán Viva, câu hỏi đào sâu (Follow-up probes) và rubric grading. | Ollama Client, JSON Schema, RAG, Prompt Engineering |
| **Proctoring & Security Auditor** | Kiểm tra liêm chính phòng thi, phát hiện chuyển tab, ngăn chặn gian lận, ghi vết nhật ký audit logs (`exam_logs`). | Web Event Listeners, Session Tracking, Audit Trail |
| **QA / Test Automation Specialist** | Duy trì và mở rộng bộ kiểm thử tự động, đảm bảo mọi lần commit đều vượt qua toàn bộ test cases C# và Node.js. | xUnit, Moq, `node:test`, Headless test harnesses |

---

## 2. Nguyên Tắc Làm Việc Bắt Buộc Đối Với Agents (Agent Operating Rules)

1. **Bảo toàn tính toàn vẹn tài liệu & kiến trúc (Architecture Integrity):**
   - Giữ nguyên các comment giải thích kiến trúc và docstrings có sẵn trong mã nguồn.
   - Không được phá vỡ quy tắc 3 tầng: Mọi nghiệp vụ phải nằm trong `AssignmentPRN.Business`.
2. **Quy trình Xác thực 3 Bước (Verification Loop):**
   Mọi thay đổi code trước khi hoàn thành phải trải qua:
   - Bước 1: `dotnet build` $\rightarrow$ Đảm bảo 0 Warnings, 0 Errors.
   - Bước 2: `dotnet test` $\rightarrow$ Vượt qua toàn bộ 462+ unit tests.
   - Bước 3: `node --test tests/browser/*.test.cjs` $\rightarrow$ Vượt qua toàn bộ browser tests.
3. **Minh bạch và báo cáo rõ ràng (Transparent Communication):**
   - Sau mỗi lần sửa đổi hoặc thêm tính năng, Agent phải review cụ thể:
     - Đã sửa/thêm những file nào.
     - Cách thức hoạt động của tính năng mới.
     - Kết quả kiểm thử tự động.
4. **An toàn dữ liệu & Không gây lỗi phá vỡ (Non-breaking changes):**
   - Không tự ý xóa các trường database trừ khi có yêu cầu dứt khoát từ người dùng.
   - Dùng các cơ chế fallback an toàn (safe defaults) cho các API trình duyệt phòng trường hợp thiết bị sinh viên không hỗ trợ.
