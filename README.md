# assignment_prn

## Phân quyền

| Nghiệp vụ | Admin | Giảng viên | Sinh viên |
| --- | --- | --- | --- |
| Xem/quản lý lịch thi | Tất cả | Chỉ lịch mình phụ trách | Không |
| Xem/quản lý lớp | Tất cả | Chỉ lớp mình phụ trách | Không |
| Thêm, xoá, tìm sinh viên trong lớp | Có | Chỉ lớp mình phụ trách | Không |
| Tra cứu lịch sinh viên | Có | Có | Chỉ lịch của mình |
| Tự đăng ký tài khoản | Tạo tài khoản Sinh viên | Tạo tài khoản Sinh viên | Tạo tài khoản Sinh viên |

Đăng nhập, đăng ký, đăng xuất và trang từ chối truy cập nằm trong `Pages/Account`.
Các thao tác thay đổi lịch thi hoặc sĩ số lớp được phát qua SignalR tại
`/hubs/exam-updates` để các phiên đang mở nhận thông báo cập nhật.
