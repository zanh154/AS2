# Dữ liệu mẫu cho Bộ câu hỏi

Hai tệp này dùng để chạy thử ngân hàng câu hỏi mà không cần tự soạn dữ liệu.

| Tệp | Dùng làm gì |
|---|---|
| `bai-giang-01.pdf` | Tài liệu môn, đóng vai trò **một chủ đề** |
| `cau-hoi-prn212.csv` | 18 câu trắc nghiệm, cột `material` trỏ vào tệp PDF trên |

## Trước khi bắt đầu

Cần bảng `question_options` trong database đang dùng — đã có sẵn trong dump ở
`database/dump/`. Thiếu bảng này thì cả thêm câu hỏi lẫn nhập CSV đều báo
"Không thể lưu câu hỏi."

## Thứ tự chạy

**Phải tải PDF lên trước**, vì CSV ghép chủ đề theo *tên tệp đã lưu*. Nhập CSV trước
thì câu hỏi vẫn vào nhưng không có chủ đề, và lọc theo chủ đề sẽ không thấy gì.

1. `Tài liệu môn → Tải lên tài liệu`, chọn môn, chọn `bai-giang-01.pdf`.
   Giữ nguyên tên tệp — đổi tên là CSV không khớp được nữa.
2. `Ngân hàng câu hỏi → Nhập từ CSV`, chọn **đúng môn vừa gắn tài liệu**, chọn
   `cau-hoi-prn212.csv`. Kết quả mong đợi: `Đã nhập 18 câu hỏi`, không có dòng lỗi.
3. Lọc thử theo chủ đề và theo độ khó để kiểm tra bộ lọc.

## Nội dung bộ câu hỏi

18 câu về C#/.NET, đủ để phát đề cho 3 sinh viên × 5 câu mà vẫn còn dư:

| Độ khó | Số câu |
|---|---|
| Easy | 5 |
| Medium | 9 |
| Hard | 4 |

Trải đều 4 mức Bloom (Remember, Understand, Apply, Analyze), mỗi câu 4 phương án và
đúng một đáp án đúng.

## Thử luồng phát đề

Tạo một lịch thi cho môn đó với `Số câu hỏi chính = 5` và một lớp có ≥ 2 sinh viên.
Hệ thống tự phát đề ngay sau khi tạo; vào `chi tiết phiên thi → Ngân hàng câu hỏi →
Xem và phát đề` để kiểm tra không sinh viên nào trùng câu với sinh viên khác.

Muốn thử trường hợp **ngân hàng thiếu câu**: đặt số câu chính là 10 với lớp 3 sinh viên
(cần 30 câu, chỉ có 18). Lịch thi vẫn được tạo, hệ thống báo thiếu và đưa sang màn hình
cấu hình để chọn lại phạm vi.

## Ghi chú

Nội dung trong PDF viết không dấu vì phông Helvetica mặc định của PDF không có sẵn
dấu tiếng Việt. Không ảnh hưởng gì tới việc nhập câu hỏi — hệ thống chỉ dùng tên tệp.
