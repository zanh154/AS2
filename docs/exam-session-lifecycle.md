# Trạng thái phiên thi

## Phân tầng

Quy tắc quyết định trạng thái nằm trong `Business/Policies/ExamLifecycleRules.cs`, thuần logic và không có truy cập database. `ExamResultService.CloseOverdueSlots` dùng quy tắc chung để tính ca quá hạn. Các repository đọc dữ liệu mới nhất trong transaction, gọi implementation Business qua `IExamStatePolicy` và lưu kết quả; không tự định nghĩa lại điều kiện hoàn thành/vắng thi. Transaction bao quanh cập nhật lượt thi và đồng bộ trạng thái phiên được giữ nguyên.

## Quy tắc

- Tạo phiên: Scheduled (Đã xếp lịch).
- Sinh viên đầu tiên vào thi đúng khung giờ: lượt thi và phiên chuyển sang InProgress (Đang diễn ra).
- Nộp vòng chính rồi còn vòng đào sâu: lượt thi và phiên vẫn InProgress.
- Nộp bài cuối cùng: nếu không còn lượt Waiting/InProgress, phiên tự Completed (Hoàn thành).
- Còn sinh viên Waiting hoặc InProgress: không tự hoàn thành phiên.
- Giảng viên chốt ca quá hạn tại trang kết quả: Waiting hết giờ thành Absent; InProgress quá thời gian nộp bù thành Completed. Sau đó tự hoàn thành phiên nếu mọi lượt đều đã kết thúc, kể cả tất cả sinh viên vắng.
- Draft, Cancelled và Completed không được tự mở lại. Phiên rỗng không được tự hoàn thành.
- Giảng viên vẫn được bắt đầu phiên thủ công khi đến giờ; quy tắc hủy và chuyển trạng thái thủ công hiện có vẫn được áp dụng.
- Hủy phiên bị chặn khi có người đang thi; các bước hủy, vào thi, nộp bài và chốt ca dùng transaction.

## Lưu ý vận hành

Đây là đồng bộ theo thao tác, không có tác vụ tự chạy theo đồng hồ. Nếu chưa ai vào thi, phiên vẫn Scheduled trừ khi giảng viên bắt đầu thủ công. Nếu mọi người bỏ thi hoặc đóng trang mà không nộp, giảng viên cần chốt ca quá hạn.

Dữ liệu cũ đã được đồng bộ một lần bằng script chốt phiên: chỉ hoàn thành phiên Scheduled/InProgress có ít nhất một lượt thi và toàn bộ lượt đã Completed/Absent/Cancelled; không thay đổi đáp án, điểm hoặc trạng thái sinh viên. Dump hiện tại đã phản ánh kết quả đó, không cần chạy lại và không cần đổi schema. Script gốc nằm trong lịch sử Git.

## Kiểm thử 02/10/2026

113/113 trường hợp đạt trên bộ test hiện tại được theo dõi bởi Git cùng test mới, chạy bằng SQLite riêng. 9 trường hợp mới kiểm tra bắt đầu phiên, vào lại giữ giờ bắt đầu, nộp cuối cùng, còn sinh viên chờ, vòng đào sâu, tất cả vắng, chốt ca với trạng thái đã thay đổi, và từ chối vào phiên Draft/Cancelled/Completed.

Chưa kiểm thử tải đồng thời trên MySQL hoặc toàn bộ thao tác trình duyệt. Các file test cũ chưa commit không tương thích API không được đưa vào bộ chạy này.
