# Cấu hình đề dùng chung

- Số câu chính lấy từ `ExamSession.MainQuestionCount`. Trang phát đề chỉ hiển thị số này; backend từ chối số gửi lên khác cấu hình phiên.
- Chủ đề và độ khó được lưu trong `exam_sessions.question_scope_json` cùng transaction phát đề thành công. Phát đề thất bại không đổi cấu hình.
- Sinh viên vào sau dùng lại cấu hình đã lưu; thêm sinh viên kiểm tra ngân hàng còn đủ câu trong phạm vi đó.
- Khi đã có đề, thay đổi phạm vi bị từ chối. Hủy đề trước khi chọn phạm vi mới; không hủy đề khi đã có sinh viên bắt đầu thi.
- Hủy đề giữ lại bộ lọc để phát lại. Đổi môn khi chưa có đề sẽ xóa bộ lọc cũ.
- Nút xem số câu khả dụng chỉ xem thử, không lưu bộ lọc. Bộ lọc được lưu khi phát đề thành công.

## Cập nhật database

Cần cột `exam_sessions.question_scope_json`, thêm vào ngày 01/10/2026. Cột này đã có trong dump ở [`database/dump/`](../database/dump/) và trong database Aiven của nhóm.

Cấu hình bộ lọc của đề đã phát ở bản cũ không được lưu nên không thể khôi phục chính xác. Phiên cũ chưa bắt đầu nên hủy đề và phát lại để có cấu hình thống nhất. Không tự sửa đề hoặc câu trả lời của phiên đã bắt đầu. Cấu hình chưa lưu mặc định lấy toàn bộ chủ đề/độ khó của môn.

Kiểm thử `SharedQuestionConfigurationTests` sử dụng SQLite trong bộ nhớ. Kiểm thử transaction SQLite không thay thế kiểm thử tải đồng thời/khóa dòng trên MySQL.
