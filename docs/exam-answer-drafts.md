# Lưu tạm đáp án

## Phân tầng sau chỉnh sửa

- `QuestionService` nhận yêu cầu và chuyển dữ liệu sang `ExamDraftValidation` của Business. Bộ kiểm tra quyết định quyền sở hữu, trạng thái, thời gian, vòng hiện tại, câu hỏi/đáp án hợp lệ và khóa đáp án đã nộp.
- `QuestionRepository.SaveDraftAsync` mở transaction Serializable, đọc `ExamDraftState` hiện tại, gọi hàm kiểm tra do Business truyền vào, rồi mới ghi đáp án. Callback chỉ nhận dữ liệu; không nhận DbContext. DataAccess không tham chiếu Business.
- Mọi đáp án phải hợp lệ trước khi repository bắt đầu thay đổi entity. Lỗi kiểm tra không để lại đáp án dở dang trong change tracker.
- 125/125 test đạt sau chỉnh sửa, bao gồm các test mới kiểm tra transaction, yêu cầu có cả đáp án hợp lệ và không hợp lệ, lượt thi không tồn tại và đáp án đã nộp. Dữ liệu kiểm thử SQLite riêng, chưa kiểm thử cạnh tranh trên MySQL.

- Sau khi chọn đáp án, trang chờ 400 ms rồi gửi lưu tạm vào database. Các lần lưu trên cùng trang được gửi tuần tự; lỗi mạng sẽ được thử lại sau 3 giây khi trang còn mở.
- Trạng thái hiển thị phân biệt đang chờ lưu, đang lưu, đã lưu và chưa lưu được. Chỉ lựa chọn đã báo lưu thành công mới được bảo đảm khôi phục khi tải lại trang. Không có lưu offline sau khi đóng tab.
- Dùng bảng `answers` hiện có; bản tạm có `FinishedAt = null`. Không cần thay đổi schema. Lưu tạm không kết thúc lượt thi hay sinh câu đào sâu.
- Endpoint POST có kiểm tra session sinh viên, anti-forgery, quyền sở hữu lượt thi, giờ thi, trạng thái phiên/lượt thi, câu hỏi thuộc vòng hiện tại và đáp án thuộc câu hỏi.
- Nộp bài ghi nhận lựa chọn cuối từ form và khóa vòng đã nộp. Lưu tạm đến sau khi nộp hoặc vào vòng cũ bị từ chối. Khi chốt ca quá hạn, kết quả dùng đáp án đã được lưu theo luồng hiện có.
- Không hỗ trợ hợp nhất đáp án khi làm cùng lượt thi trên nhiều tab/thiết bị; nên chỉ mở một trang làm bài.

## Kiểm chứng ngày 02/10/2026

104/104 kiểm thử tự động đạt, gồm 10 trường hợp mới: lưu/đọc lại/sửa đáp án; sai chủ sở hữu; trước giờ/hết giờ; lượt đã hoàn thành; phiên hủy; câu hỏi lạ; đáp án lạ; nộp sau lưu tạm và từ chối lưu muộn; vòng đào sâu không sửa được vòng chính.

Kiểm thử dùng SQLite trong bộ nhớ; không ghi dữ liệu test vào Aiven. JavaScript đã kiểm tra cú pháp. Chưa kiểm thử trình duyệt thực hoặc tải đồng thời trên MySQL. Các test cũ chưa commit, không tương thích API hiện tại, không được tính trong kết quả này.
