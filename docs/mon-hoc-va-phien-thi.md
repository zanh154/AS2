# Môn học và phiên thi

## Chạy dự án

Yêu cầu .NET 10 SDK. Sao chép `src/AssignmentPRN.Presentation/appsettings.json.example` thành `appsettings.json`, điền thông tin MySQL của nhóm. File cấu hình thật được Git bỏ qua.

```powershell
dotnet run --project src/AssignmentPRN.Presentation
dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj
```

Cần hai bảng `academic_classes` và `class_students`; cả hai đã có trong dump ở [`database/dump/`](../database/dump/). Unit test chỉ kiểm tra các luật nghiệp vụ thuần, không kết nối database. Dữ liệu để chạy thử cả luồng: `database/seed_test_flow.sql` (xem [báo cáo kiểm thử](kiem-thu-mon-hoc-va-phien-thi.md)).

## Môn học

- Vào **Môn học** từ menu hoặc bảng điều khiển.
- Admin quản lý toàn bộ môn; giảng viên quản lý môn do mình phụ trách.
- Tạo/sửa mã môn, tên môn, mô tả, giảng viên, trạng thái hoạt động.
- Mã môn được trim, chuyển thành chữ hoa, kiểm tra trùng; giới hạn mã 50, tên 200, mô tả 1000 ký tự.
- Chỉ chọn giảng viên đang hoạt động.
- Môn có lớp, phiên thi hoặc dữ liệu liên quan không được xoá; dùng **Ngừng hoạt động** để giữ dữ liệu cũ.
- Môn ngừng hoạt động không được chọn để tạo phiên mới. Phiên cũ vẫn được giữ nguyên môn đó khi chỉnh sửa.

## Phiên thi

- Vào **Lịch thi → Tạo lịch thi**. Chọn môn, giảng viên, lớp và thời gian; danh sách sinh viên lấy từ lớp.
- Ngày bắt đầu không trong quá khứ, thời lượng 1–1440 phút, toàn bộ lượt thi phải kết thúc trong cùng ngày.
- Ngân hàng câu hỏi phải đủ câu: cần **số sinh viên × số câu hỏi chính** câu chính (đã duyệt) khác nhau, vì trong một phiên không sinh viên nào trùng câu với người khác. Thiếu thì bị chặn khi tạo phiên, khi sửa số câu/đổi môn, và khi thêm sinh viên.
- Đổi giờ một ca: giờ mới không được nằm trong quá khứ và phải trong ngày thi.
- Thêm sinh viên lẻ: sinh viên phải thuộc ít nhất một lớp đang hoạt động của môn và chưa có trong phiên.
- Vào chi tiết → **Sửa phiên thi** để sửa tên, môn, mô tả, ngày giờ, thời lượng và số câu hỏi.
- Sửa không thay danh sách sinh viên hoặc giảng viên. Đổi môn vẫn giữ nguyên danh sách sinh viên hiện có.
- Nếu ngày giờ/thời lượng thay đổi, xếp lại các ca liên tiếp theo thứ tự hiện tại, giữ nguyên mã lượt thi; kiểm tra trùng lịch trước khi ghi. Nếu chỉ sửa thông tin, giữ các khung giờ đã điều chỉnh riêng.
- Chỉ sửa/xoá phiên Nháp hoặc Đã xếp lịch khi tất cả sinh viên còn Chờ thi. Giảng viên chỉ thao tác phiên mình phụ trách; admin thao tác toàn bộ.
- Các thao tác ghi dùng POST và chống giả mạo yêu cầu.

## Trạng thái

Phiên mới ở trạng thái **Đã xếp lịch**. Quy tắc chuyển:

| Hiện tại | Có thể chuyển sang |
|---|---|
| Nháp | Đã xếp lịch, Đã huỷ |
| Đã xếp lịch | Đang diễn ra, Đã huỷ |
| Đang diễn ra | Đã hoàn thành, Đã huỷ |
| Đã hoàn thành / Đã huỷ | Không chuyển tiếp |

Không bắt đầu trước giờ thi. Chỉ hoàn thành khi không còn sinh viên Chờ thi/Đang thi; việc cập nhật kết quả từng sinh viên thuộc module kết quả. Phiên đã huỷ không xuất hiện trong lịch sinh viên và không chiếm khung giờ. Không mở lại hoặc xoá phiên đã huỷ/hoàn thành.

Huỷ phiên: bị chặn khi còn sinh viên **Đang thi** (đợi họ nộp bài, hoặc chốt ca khi hết giờ). Khi huỷ được, các lượt **Chờ thi** chuyển sang **Đã huỷ**; bài đã nộp và lượt vắng giữ nguyên.

Phát đề: đề được rút khi sinh viên vào thi, hoặc giảng viên phát trước ở màn **Xem và phát đề**. Mọi lần phát đề của một phiên chạy nối tiếp (khoá dòng phiên thi trong database), nên bấm đúp hay nhiều sinh viên vào cùng lúc cũng không bị trùng câu hoặc phát hai lần. Không huỷ đề được khi đã có sinh viên vào thi.

## Kiểm tra thủ công

1. Đăng nhập admin, thêm môn có mã chưa dùng; sửa thông tin rồi xoá môn chưa được sử dụng.
2. Thử mã môn trùng và xoá môn đã có lớp: phải có thông báo, dữ liệu không mất.
3. Tạo phiên cho lớp có sinh viên; mở chi tiết và đối chiếu ngày giờ/thời lượng.
4. Sửa ngày giờ/thời lượng: kiểm tra danh sách và mã lượt thi được giữ nguyên. Sửa sang giờ trùng phiên khác: bị từ chối, lịch cũ không đổi.
5. Chuyển trạng thái theo bảng; thử bắt đầu sớm và hoàn thành khi còn sinh viên chờ thi: bị từ chối.
6. Dùng giảng viên khác mở URL sửa/xoá phiên hoặc môn không thuộc mình: bị từ chối. Sinh viên không được vào màn hình quản lý.

Các thao tác làm thay đổi dữ liệu nên thử trên dữ liệu thử nghiệm của nhóm, không dùng phiên thi thật.
