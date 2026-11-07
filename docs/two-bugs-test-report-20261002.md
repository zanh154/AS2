# Kết quả kiểm thử hai bản sửa — 02/10/2026

Commit: `e1a7c8b` trên `ThaiBranch`.

Kết quả chạy thực tế: **94 passed, 0 failed, 0 skipped**. Trong đó 12 trường hợp trực tiếp thuộc `IssuedPaperEditTests` và `SharedQuestionConfigurationTests`; 82 trường hợp hồi quy khác.

## Kiểm tra trực tiếp

| Trường hợp | Kết quả |
|---|---|
| Đổi môn khi đã phát đề bị chặn ở service và repository | Pass |
| Đổi số câu khi đã phát đề bị chặn ở service và repository | Pass |
| Đổi cả môn và số câu khi đã phát đề bị chặn | Pass |
| Sửa tên khi đã phát đề vẫn được, giữ nguyên đề | Pass |
| Hủy đề chưa bắt đầu rồi đổi cấu hình tại repository | Pass |
| Có sinh viên đang thi thì không hủy được đề | Pass |
| Bộ lọc giữ sau khi đọc lại database, sinh viên thêm sau nhận đúng phạm vi và số câu, không trùng câu | Pass |
| Số câu gửi lên khác cấu hình phiên bị từ chối | Pass |
| Đã có đề thì không đổi được bộ lọc; hủy đề rồi đổi được | Pass |
| Thiếu câu khi phát đề không lưu cấu hình/đề dở dang | Pass |
| Thêm sinh viên kiểm tra số câu còn lại trong phạm vi đã lưu | Pass |
| Chủ đề thuộc môn khác bị từ chối | Pass |

## Phạm vi và giới hạn

- Kiểm thử tự động dùng SQLite trong bộ nhớ, gọi service/repository thực. Không tạo hoặc sửa dữ liệu trong Aiven.
- Chạy các file test đã được Git theo dõi qua project test tạm. Các file test cũ chưa commit có API không tương thích được giữ nguyên và không nằm trong kết quả này.
- Không chạy lại build dependency khi web đang hoạt động; dùng bản ứng dụng đã build từ cùng commit ở lần triển khai trước.
- Chưa thao tác toàn bộ testcase qua trình duyệt; chưa xác nhận nút readonly, preview bộ lọc và thông báo hiển thị bằng kiểm thử UI.
- Chưa kiểm thử tải đồng thời hoặc khóa dòng MySQL. Kết quả này không chứng minh toàn bộ tình huống trên database production.
- File kết quả máy đọc: `C:/Users/ASUS/AppData/Local/Temp/aives-scope-full-tests/TestResults/two-bugs-20261002.trx`.
