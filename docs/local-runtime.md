# Chạy ASM2 tại máy hiện tại

Đã cấu hình MySQL Aiven, schema ASM2 và Ollama cục bộ. Connection string nằm trong `src/AssignmentPRN.Presentation/appsettings.json`, được Git bỏ qua. Không đưa file này lên GitHub.

## Khởi động lại sau khi tắt máy

Mở PowerShell tại thư mục project:

```powershell
Set-Location 'D:\PRN212&222\as1'
.\scripts\start-local-ai.ps1
dotnet run --project src/AssignmentPRN.Presentation
```

Web: http://localhost:49367/Account/Login. Ollama: http://localhost:11434. Nếu web đang chạy, dừng bằng Ctrl+C trước khi build/test để tránh khóa DLL.

Runtime portable và models lưu tại `.runtime/` trên ổ D, được Git bỏ qua. Script khởi động AI kiểm tra server hiện có trước khi mở một tiến trình mới; không cần cửa sổ Ollama hiển thị.

## Tại máy khác

Lấy Ollama Windows portable từ https://docs.ollama.com/windows và giải nén binary vào `.runtime/ollama/`. Chạy script AI rồi tải models:

```powershell
$env:OLLAMA_MODELS = Join-Path (Get-Location).Path '.runtime/models'
.\.runtime\ollama\ollama.exe pull nomic-embed-text
.\.runtime\ollama\ollama.exe pull qwen2.5:3b
.\.runtime\ollama\ollama.exe pull qwen2.5:7b-instruct
```

Bản 7b dùng cho hỏi xoáy và chấm điểm vấn đáp; bản 3b dùng cho sinh câu hỏi từ tài liệu.
Máy không tải được bản 7b thì để trống `Ollama:VivaModel`, phần vấn đáp sẽ chạy bằng model
nhỏ — vẫn chạy, nhưng câu hỏi xoáy hầu như không dùng được (xem phần đo trong
`docs/ai-hoi-xoay-cham-diem-ket-qua.md`).

Dùng `appsettings.json.example` để cấu hình connection string riêng. Cấu hình đang sử dụng:

- `DocumentStorage:BasePath`: `../AssignmentPRN.DataAccess/Storage/rag` (đường dẫn tương đối theo Presentation).
- `Ollama:BaseUrl`: `http://localhost:11434`.
- `Ollama:EmbeddingModel`: `nomic-embed-text`.
- `Ollama:GenerationModel`: `qwen2.5:3b` (sinh câu hỏi từ tài liệu).
- `Ollama:VivaModel`: `qwen2.5:7b-instruct` (hỏi xoáy và chấm điểm vấn đáp).
- `Rag:TopK`: 5; `Rag:SimilarityThreshold`: 0.3.

Nếu dùng database ASM1 riêng, chạy migration theo `database/README.md`. Database Aiven hiện tại đã được cập nhật.

## Test trên web

Đăng nhập tài khoản giảng viên đã có. Vào **Tài liệu & AI**, chọn môn do mình phụ trách, tải DOCX/PDF có văn bản hoặc PPTX. Đợi trạng thái hoàn tất, sinh câu hỏi rồi vào danh sách câu hỏi để sửa và duyệt/từ chối. SignalR cập nhật trạng thái xử lý, có kiểm tra lại khi mất kết nối.

PDF chỉ chứa ảnh chưa có OCR. Model AI sinh nội dung có thể cần giảng viên chỉnh trước khi duyệt. Câu hỏi sinh từ tài liệu được lưu trong `generated_questions`; chưa tự chuyển vào bộ đề trắc nghiệm của ASM1. Giao diện thi bằng giọng nói và AI hỏi đào sâu còn là phần triển khai riêng của nhóm.

## Kiểm thử tự động

```powershell
dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj
```

203 test hiện hành đã đạt ngày 15/10/2026. Bốn file test ASM1 cũ còn ở máy được giữ làm tham khảo, loại khỏi compile vì trỏ tới controller/contract đã được thay thế. Chi tiết: `docs/asm2-runtime-test-report.md`.
