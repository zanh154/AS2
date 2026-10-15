# Kiểm thử sau khi chỉnh tầng 1–3 — 02/10/2026

## Kết quả

- 139/139 kiểm thử hồi quy đạt; 0 thất bại, 0 bỏ qua.
- 14/14 kiểm tra HTTP tích hợp đạt, sử dụng controller, Razor view, Business và DataAccess hiện tại.
- Không phát hiện lỗi chức năng trong các luồng được kiểm tra.

## HTTP tích hợp

| Kiểm tra | Kết quả |
|---|---|
| Đăng nhập và xem lịch thi sinh viên | Pass |
| Vào thi và render form thi thật | Pass |
| Câu hỏi/phương án xuất hiện trong HTML | Pass |
| Phiên chuyển sang Đang diễn ra khi vào thi | Pass |
| Từ chối lưu tạm thiếu anti-forgery token | Pass |
| Lưu tạm qua endpoint thật | Pass |
| GET lại trang khôi phục radio đã chọn từ database | Pass |
| Sinh viên khác không ghi đè được đáp án | Pass |
| Đáp án không thuộc câu hỏi bị từ chối | Pass |
| Nộp bài và render kết quả | Pass |
| Nộp bài cuối cùng chuyển phiên sang Đã hoàn thành | Pass |
| Lưu tạm đến sau khi nộp bị từ chối | Pass |
| Chốt ca quá hạn ghi nhận sinh viên vắng | Pass |
| Phiên được hoàn thành sau khi chốt lượt quá hạn cuối cùng | Pass |

## Môi trường và giới hạn

- Host test riêng ở localhost:49380, database SQLite mới trong thư mục tạm, tài khoản giả. Không tạo/sửa dữ liệu test trong Aiven. Host test đã được dừng sau kiểm tra.
- Host test nạp assembly MVC/Razor của ứng dụng; đăng ký các service thật, thay DbContext bằng SQLite. Gửi form đăng nhập, giữ cookie session và dùng anti-forgery token của form thật.
- Bộ hồi quy chạy các file test theo dõi bởi Git cùng các test mới qua project tạm; không gồm các test cũ chưa commit không tương thích API.
- Công cụ trình duyệt lỗi khởi tạo (`failed to write kernel assets`). Do đó chưa xác nhận thao tác chuột, hình thức giao diện, timer/debounce/retry JavaScript bằng trình duyệt thực. HTTP trực tiếp tới endpoint lưu tạm không tương đương kiểm chứng trình duyệt tự gửi lưu tạm.
- Chưa kiểm thử tải đồng thời/khóa dòng và khác biệt truy vấn trên MySQL.
- Script HTTP ban đầu phải chỉnh cách lấy token của tài khoản không có lịch và nhãn hiển thị `Đã hoàn thành`; không cần sửa code ứng dụng. Đã chạy lại toàn bộ trên database test mới và đạt 14/14.

Kết quả TRX: `%TEMP%/aives-scope-full-tests/TestResults/layers-20261002.trx`.
Host/script HTTP: `%TEMP%/aives-http-check/`.
