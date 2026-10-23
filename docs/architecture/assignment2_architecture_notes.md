# Assignment PRN222 – Assignment 2 System Architecture

Phân tích source tại workspace ngày 17/10/2026. Phạm vi: `AssignmentPRN.slnx`, `src/`, `tests/`; không tính `.worktrees/`, `bin/`, `obj/` hay tài liệu thiết kế như code đã triển khai. Không khởi động ứng dụng, kết nối database hoặc thay đổi cấu hình. Không công bố giá trị connection string hay secrets.

## 1. Kiến trúc AS-IS và số liệu

Ứng dụng web monolith, 3 tầng, mỗi tầng là một project/assembly riêng. Presentation kết hợp MVC và Razor Pages; Business chứa nghiệp vụ; DataAccess dùng Repository Pattern và EF Core. Không thấy API project độc lập, microservice hoặc dự án tích hợp AI.

| Project | Assembly mặc định | Dependency trực tiếp trong `.csproj` |
|---|---|---|
| `AssignmentPRN.Presentation` | `AssignmentPRN.Presentation.dll` (web host) | Business |
| `AssignmentPRN.Business` | `AssignmentPRN.Business.dll` | DataAccess |
| `AssignmentPRN.DataAccess` | `AssignmentPRN.DataAccess.dll` | Không tham chiếu project ứng dụng khác |

Các đường trong sơ đồ AS-IS mô tả lời gọi lúc chạy; dependency build là bảng trên. Presentation còn sử dụng kiểu DAL qua tham chiếu truyền tiếp: `Models/ExamSessionViewModels.cs` import `AssignmentPRN.DataAccess.Enums`; `AuthService` trả entity `User` lên `LoginModel`. Vì vậy ranh giới tầng chưa hoàn toàn độc lập. Interface repository cũng nằm ở DAL, Business phụ thuộc DAL theo kiến trúc phân tầng truyền thống, chưa áp dụng dependency inversion đầy đủ.

| Thành phần | Số lượng và cách đếm |
|---|---|
| Project ứng dụng trong solution | 3 |
| Project kiểm thử | 1, nằm trong `tests/`, chưa được liệt kê trong `.slnx` |
| MVC Controller | 7 class kế thừa `Controller`; không tính `DashboardBuilder` |
| Razor Page / PageModel | 4 cặp có `@page`: Login, Register, Logout, AccessDenied; không tính `_ViewImports` |
| SignalR Hub | 1: `ExamUpdatesHub` |
| Business service | 3 implementation + 3 interface; không tính helper/DI |
| Repository | 3 implementation + 3 interface |
| Entity / DbSet | 7 / 7 |
| Hỗ trợ khởi động DAL | `DatabaseInitializer`, `DatabaseInitializerHostedService`; không cộng vào 3 business service |

### Presentation

Đường dẫn gốc: [`src/AssignmentPRN.Presentation`](../../src/AssignmentPRN.Presentation).

- Controllers: `HomeController`, `AdminController`, `LecturerController`, `StudentController`, `ExamSessionsController`, `ClassesController`, `StudentScheduleController`.
- `Controllers/DashboardBuilder.cs`: helper tổng hợp dashboard, không phải controller.
- `Pages/Account/`: `LoginModel`, `RegisterModel`, `LogoutModel`, `AccessDeniedModel` và 4 trang tương ứng.
- `Views/`: giao diện dashboard, lịch thi, lớp, lịch cá nhân; `Models/`: view model tài khoản, dashboard, lớp, lịch thi và thành phần chọn ngày/giờ.
- `Hubs/ExamUpdatesHub.cs`: hub đẩy thông báo, không chứa nghiệp vụ thi hay xử lý audio.
- `Filters/SessionAuthorizeAttribute.cs`, `Security/RoleAccessRules.cs`: kiểm tra session, role và quyền sở hữu bản ghi; POST thay đổi dữ liệu ở controller có antiforgery.
- `Program.cs`: composition root; đăng ký MVC, Razor Pages, SignalR, Business, cache trong bộ nhớ và session 30 phút. Routes: Razor Pages, `/hubs/exam-updates`, default controller route.
- `wwwroot/js/site.js`, `Views/Shared/_Layout.cshtml`: kết nối hub và hiển thị thông báo cập nhật.

### Business Logic

Đường dẫn gốc: [`src/AssignmentPRN.Business`](../../src/AssignmentPRN.Business).

- `Services.cs`: `IAuthService`, `IExamSessionService`, `IClassManagementService`.
- `AuthService.cs`: đăng nhập, kiểm tra tài khoản hoạt động, BCrypt, đăng ký vai trò Student.
- `ExamSessionService.cs`: danh sách/chi tiết lịch thi, lựa chọn tạo lịch, danh sách lớp, tạo lịch, đổi giờ thí sinh, xóa lịch, tìm sinh viên, lịch cá nhân.
- `ClassManagementService.cs`: đọc lớp và danh sách sinh viên, thêm/xóa sinh viên khỏi lớp.
- `ExamSchedulePlanner.cs`: sinh các slot tuần tự, kiểm tra thời lượng và giao nhau; `ScheduleSlot` là kết quả tính toán.
- `Contracts.cs`: request/response DTO, `ServiceResponse`, `BusinessValidationException`; `Validation.cs`: `BusinessValidation`, `ServiceExecutor` xử lý validation và lỗi.
- `ServiceCollectionExtensions.cs`: `AddBusiness` gọi `AddDataAccess`, đăng ký 3 business service scoped.

### Data Access

Đường dẫn gốc: [`src/AssignmentPRN.DataAccess`](../../src/AssignmentPRN.DataAccess).

- `Repositories/`: `UserRepository`, `CatalogRepository`, `ExamSessionRepository` và các interface tương ứng.
- `Data/AivesDbContext.cs`: ánh xạ 7 entity và quan hệ: `User`, `Role`, `Course`, `AcademicClass`, `ClassStudent`, `ExamSession`, `ExamCandidate`.
- `Contracts/`: read model, aggregate input cho repository; `Enums/ExamEnums.cs`: trạng thái lịch thi/thí sinh; `Common/ExamScheduleConflictException.cs`: lỗi trùng lịch.
- `ExamSessionRepository`: tạo/đổi giờ có transaction và kiểm tra trùng slot giảng viên/sinh viên; thao tác dữ liệu qua DbContext.
- `Extensions/ServiceCollectionExtensions.cs`: `UseMySql`, 3 repository scoped, DbContext scoped, initializer và hosted service.
- `Services/DatabaseInitializer.cs`: chỉ kiểm tra kết nối, đọc số môn/giảng viên/sinh viên và ghi log. Không tạo schema, seed hoặc chạy migration dù tên class có thể gây hiểu nhầm.

## 2. Mapping sáu nhóm chức năng Assignment 2

| Nhóm | Class/file có thật | Mức triển khai quan sát được |
|---|---|---|
| 1. Nền tảng, phiên thi, SignalR, tích hợp | `Program.cs`, `ExamSessionsController`, `ExamSessionService`, `ExamSchedulePlanner`, `ExamSessionRepository`, `ExamUpdatesHub`, `site.js` | Có tạo/xem/xóa lịch, đổi giờ thí sinh, sinh slot và báo thay đổi. Chưa phải toàn bộ vòng đời phòng thi; chưa có tích hợp AI/voice. |
| 2. Tài khoản, phân quyền, lớp, sinh viên | `Pages/Account/*`, `AuthService`, `UserRepository`, `SessionAuthorizeAttribute`, `RoleAccessRules`, `ClassesController`, `ClassManagementService`, `CatalogRepository`, `StudentScheduleController` | Có đăng nhập/đăng ký/đăng xuất, role Admin/Lecturer/Student, dashboard, xem lớp, thêm/xóa thành viên và tìm lịch sinh viên. Chưa thấy CRUD đầy đủ lớp/tài khoản hoặc màn hình quản trị gán role. |
| 3. Tài liệu, RAG, sinh câu hỏi AI | Không tìm thấy module tương ứng trong `src/` | Planned. `MaterialStorage:RootPath` là cấu hình có sẵn nhưng không thấy code sử dụng; không đủ để kết luận đã có upload/storage/RAG. |
| 4. Ngân hàng câu hỏi, rubric, duyệt, phát đề | `ExamSession.MainQuestionCount`, `MaxFollowUpCount` và DTO/form tương ứng | Chỉ lưu cấu hình số câu; chưa có entity/service câu hỏi, rubric, duyệt hoặc phát đề. Planned. |
| 5. Thi giọng nói, STT/TTS, audio/transcript | Không tìm thấy trang phòng thi, recorder hoặc speech API | Planned. Trang lịch thi và `ExamCandidate` không phải triển khai phòng thi vấn đáp. |
| 6. AI hỏi xoáy, chấm điểm, kết quả | Chỉ có cấu hình `MaxFollowUpCount`; dashboard tổng hợp lịch | Chưa có gọi LLM, sinh câu phụ, lưu câu trả lời, chấm điểm hay báo cáo kết quả. Planned. |

## 3. Luồng xử lý hiện có

### HTTP và tài khoản

1. Browser gửi HTTP tới Razor Page hoặc MVC action; session được nạp sau routing.
2. `LoginModel` gọi `IAuthService` → `AuthService` → `IUserRepository` → `UserRepository` → `AivesDbContext` → MySQL. BCrypt kiểm tra mật khẩu trong Business.
3. PageModel lưu UserId/FullName/Email/Role trong session và chuyển hướng dashboard theo role. Đăng ký tạo tài khoản Student; logout xóa session.
4. Controller dùng `SessionAuthorizeAttribute` và kiểm tra quyền theo `RoleAccessRules`; view model được render thành HTML hoặc một số action trả JSON. Không có `[ApiController]` độc lập được tìm thấy.

Session hiện dùng `AddDistributedMemoryCache`: dữ liệu nằm trong bộ nhớ tiến trình dù API có tên “distributed”. Chưa thấy cấu hình Redis, ASP.NET Core Identity, JWT, `AddAuthentication`/`UseAuthentication` hoặc policy authorization chuẩn. `LogoutModel` có gắn `SessionAuthorize`, nhưng attribute triển khai `IAuthorizationFilter`; hiệu lực trên Razor Pages cần kiểm tra riêng, không coi đó là bằng chứng mọi PageModel đã được bảo vệ.

### Quản lý lịch thi — chưa có luồng thi vấn đáp

1. `ExamSessionsController.Create` kiểm tra form/role; giảng viên được ràng buộc về tài khoản hiện tại.
2. `ExamSessionService.BuildCreateInputAsync` kiểm tra lớp/môn/giảng viên, sinh viên hoạt động, thời điểm, số câu và thời lượng. `ExamSchedulePlanner.Generate` xếp slot tuần tự; service từ chối lịch vượt ngày thi.
3. Service chuyển aggregate input cho `ExamSessionRepository.CreateAsync`; repository kiểm tra trùng lịch giảng viên/sinh viên trong transaction, lưu `ExamSession` và `ExamCandidate`, đọc chi tiết và commit.
4. Kết quả quay về Business → Controller; controller phát SignalR sau khi service thành công rồi redirect đến chi tiết. Đổi giờ và xóa lịch cũng có thông báo sau thành công.
5. Lịch cá nhân được đọc qua `StudentScheduleController` → `IExamSessionService`; chưa có bước vào phòng, thu audio, hỏi/đáp hay tính điểm.

### SignalR

- Client tải SignalR JS từ jsDelivr, kết nối `/hubs/exam-updates` với automatic reconnect.
- `ExamUpdatesHub.OnConnectedAsync` ngắt kết nối nếu thiếu UserId trong session. Hub không có method nghiệp vụ phía client.
- `ExamSessionsController` và `ClassesController` inject `IHubContext<ExamUpdatesHub>`, phát `ExamListChanged` bằng `Clients.All` sau thay đổi lịch/danh sách lớp; chưa chia group theo lớp hay lịch thi.
- `site.js` hiện banner với thông điệp; người dùng bấm làm mới để reload HTTP. Không tự đồng bộ bảng dữ liệu, không truyền âm thanh qua hub.
- Chưa thấy backplane hay cấu hình nhiều instance; kiểm tra session chỉ lúc kết nối, chưa thấy cơ chế thu hồi kết nối ngay khi logout.

### AI

Không có luồng AI đang chạy để mô tả trong AS-IS. Luồng **Proposed**: tải tài liệu → kiểm tra/lưu trữ → trích xuất/chunk/index → RAG → sinh câu hỏi → giảng viên duyệt → phát đề → STT câu trả lời → AI hỏi phụ/chấm theo rubric → lưu kết quả → hiển thị báo cáo. Đây là định hướng, chưa phải class hay hành vi đã có.

## 4. Công nghệ và external dependencies

| Thành phần | Bằng chứng/cấu hình trong source |
|---|---|
| .NET / ASP.NET Core | Cả 3 project target `net10.0`; Web SDK ở Presentation; MVC + Razor Pages + SignalR |
| ORM / MySQL | EF Core `9.0.0`, Pomelo MySQL `9.0.0`; `MySqlServerVersion(8,4,8)` trong DI. Đây là phiên bản server được khai báo cho provider, chưa xác minh server thực tế. |
| Mật khẩu | BCrypt.Net-Next `4.0.3` |
| UI | Bootstrap, jQuery, jQuery Validation trong `wwwroot/lib`; SCSS qua AspNetCore.SassCompiler `1.77.8` |
| SignalR client | `@microsoft/signalr` `8.0.7` qua jsDelivr trong `_Layout.cshtml`; server dùng shared framework ASP.NET Core |
| Hosted service | Hosting/Logging Abstractions `9.0.0` ở DAL; kiểm tra DB khi khởi động |
| Cấu hình | `appsettings.json` có ConnectionStrings, MaterialStorage, Logging, AllowedHosts; chỉ xác nhận tên section, không sao chép giá trị nhạy cảm |
| File storage | Chỉ có `MaterialStorage:RootPath`; chưa tìm thấy implementation quản lý tài liệu/audio |
| AI / RAG / STT / TTS | Chưa có provider/SDK/API call tương ứng; không gán tên nhà cung cấp giả định |
| Tests | xUnit `2.9.2`, Test SDK `17.11.1`, SQLite EF Core `9.0.20`, coverlet `6.0.2`; SQLite chỉ dùng kiểm thử, không phải database production |

Các kiểm thử hiện có: `RoleAccessRulesTests`, `ExamSessionRepositoryTests`, `ExamSchedulePlannerTests`. Lần làm tài liệu này không chạy test ứng dụng; sơ đồ mô tả source, không xác nhận trạng thái deployment hoặc kết nối dịch vụ thực tế.

## 5. Kiến trúc Proposed cho Assignment 2

Giữ 3 project/tầng hiện tại. Trang **Proposed** trong draw.io mô tả các module dự kiến bằng viền nét đứt và nhãn Planned; đó là tên module định hướng, không phải tên class đang tồn tại.

| Tầng | Giữ thành phần có thật | Planned / Proposed |
|---|---|---|
| Presentation | Controllers, 4 Account PageModels, Hub, view models | Razor Pages/PageModels cho tài liệu, câu hỏi/rubric, duyệt/phát đề, phòng thi voice và kết quả; dùng service qua interface |
| Business | Auth, quản lý lớp, lịch thi, planner, validation | Document/RAG, Question/Rubric/Approval, Voice Exam orchestration, Follow-up/Scoring/Report; DTO, validation, business rules và hợp đồng tích hợp |
| Data Access | Repositories, DbContext, 7 entity, kiểm tra DB | Persistence cho tài liệu/câu hỏi/câu trả lời/điểm; file storage implementation cho tài liệu/audio; thiết kế schema và migrations ở một nhiệm vụ riêng |
| External | MySQL, CDN client | File storage, LLM/embedding/index, STT/TTS hoặc browser speech API; chưa chốt provider |

Hướng phụ thuộc đề xuất: UI → Business services → Repository → EF Core → MySQL. Business điều phối AI qua interface/adapter tích hợp riêng, có timeout và xử lý lỗi; không mặc định mọi API bên ngoài đều thuộc DAL. File storage implementation có thể ở DAL, còn chính sách tài liệu và quyền truy cập thuộc Business. Browser speech API nếu được chọn chạy ở client; provider STT/TTS phía server đi qua adapter được Business điều phối. SignalR Hub tiếp tục ở Presentation; chỉ bổ sung event/group sau khi xác định nghiệp vụ và quyền truy cập.

Ưu tiên cải thiện ranh giới bằng DTO thay cho `User` entity và trạng thái DAL lộ lên UI. Việc chuyển các màn hình MVC sang Razor Pages là kế hoạch tăng dần, không có nghĩa source hiện tại đã chuyển toàn bộ. Không thực hiện refactor trong nhiệm vụ này.

Chưa xác minh: server MySQL/version/schema đang chạy, khả năng build/runtime của tổ hợp package, transport SignalR được chọn khi triển khai, trạng thái bảo vệ Razor Pages, hiệu quả chống trùng lịch khi nhiều request đồng thời, dịch vụ storage/AI thực tế. Không suy diễn từ config hoặc enum rằng chức năng đã hoạt động.

## 6. Khác biệt với Assignment 1

Không có sơ đồ Assignment 1 được cung cấp để đối chiếu trực tiếp. So với mô tả nền trong `Architecture.txt`, source vẫn giữ 3-Layer + MySQL + EF Core + Repository nhưng có UI MVC/Razor Pages kết hợp, hub thông báo thay đổi, quản lý lớp và lập lịch thi với slot/transaction. Không khẳng định các phần này đều mới được thêm ở Assignment 2 vì chưa kiểm tra lịch sử Assignment 1. Số lượng thành phần trong tài liệu được đếm từ source hiện tại, không lấy từ sơ đồ cũ. AI/RAG/voice/scoring là phạm vi mở rộng dự kiến, chưa được đưa vào AS-IS.

## 7. Lời thuyết trình ngắn

“Hệ thống hiện tại là ứng dụng ASP.NET Core .NET 10 gồm ba project. Presentation nhận request qua MVC và bốn Razor Pages tài khoản; Business xử lý đăng nhập, lớp và lịch thi; DataAccess truy vấn MySQL qua repository và EF Core. Khi tạo lịch, hệ thống chia slot cho sinh viên, kiểm tra trùng lịch và lưu trong transaction. Sau thành công, controller dùng SignalR báo cho các client rằng dữ liệu đã đổi; người dùng bấm làm mới để lấy dữ liệu mới. Phòng thi giọng nói, RAG, ngân hàng câu hỏi và chấm điểm AI chưa triển khai; trang Proposed thể hiện cách bổ sung các module này vào ba tầng, còn trang AS-IS chỉ thể hiện code đang có.”

## 8. Sử dụng và kiểm tra sơ đồ

Mở [`assignment2_system_architecture.drawio`](assignment2_system_architecture.drawio) bằng diagrams.net. Có hai trang landscape: **AS-IS** và **Proposed**. Đường liền là request/lời gọi; đường đứt là response hoặc thông báo real-time theo nhãn. Viền khối nét đứt kèm Planned chỉ thành phần dự kiến; không dùng riêng kiểu nét để suy ra trạng thái triển khai.

File XML được kiểm tra cú pháp, ID duy nhất, parent/source/target hợp lệ và khung trang landscape. Chưa kiểm tra bằng giao diện diagrams.net. Không có lệnh export draw.io trong PATH tại thời điểm kiểm tra nên không cài thêm công cụ và không xuất PNG/SVG.
