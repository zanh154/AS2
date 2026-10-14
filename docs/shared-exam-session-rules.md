# Quy tắc phiên thi dùng chung

`Business/Policies/ExamSessionRules.cs` và `ExamLifecycleRules.cs` chứa quy tắc sửa, chuyển trạng thái, hủy, vào thi, thời gian nộp bù, hoàn thành phiên và chốt quá hạn.

- Giữ ba tầng `Presentation → Business → DataAccess`, không cần project Domain.
- DataAccess khai báo hợp đồng `Contracts/IExamStatePolicy`; không chứa implementation quy tắc này và không tham chiếu Business.
- Business cung cấp `ExamStatePolicy`, chuyển enum dữ liệu sang enum nghiệp vụ rồi gọi các quy tắc thuần. `AddBusiness` đăng ký implementation vào DI.
- Repository nhận policy qua constructor, đọc trạng thái trong transaction rồi gọi policy. Các ranh giới transaction hiện có được giữ nguyên, bao gồm cập nhật lượt thi và đồng bộ trạng thái phiên.
- Enum hai tầng vẫn giữ hợp đồng cũ; test kiểm tra tên/giá trị và mapping quyết định.
- Các nơi tự tạo repository trong test phải truyền policy. Không có policy mặc định tại DataAccess.
- Validation lưu tạm vẫn ở Business, được gọi trong transaction trước khi ghi.

Không thay đổi schema. Bộ kiểm thử dùng SQLite riêng; chưa kiểm thử khóa dòng/tải đồng thời MySQL.

Kiểm chứng sau khi chuyển policy về Business: build thành công và 140/140 test đạt qua project test tạm (các test theo dõi bởi Git; không gồm test cũ chưa commit). Có test xác nhận policy chạy trong transaction và lỗi policy rollback cập nhật lượt thi. Chưa kiểm thử lại bằng trình duyệt.
