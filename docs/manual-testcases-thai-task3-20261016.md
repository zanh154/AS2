# Test thủ công — Task Thai và task 3

Áp dụng cho nền `e7c4fd9` và bản sửa task 3 tại máy, ngày 16/10/2026. Kết quả sau sửa: xem `task3-fix-test-report-20261016.md`. Đây là kịch bản để người dùng thao tác lại trên trình duyệt; kết quả mong đợi không phải cam kết bản hiện tại đã đạt mọi ca.

## Chuẩn bị

1. Mở http://localhost:49367/Account/Login. Giảng viên A có thể dùng tài khoản test `lecturer2@aives.com` đã được cấp; dùng mật khẩu hiện có. Chuẩn bị thêm tài khoản giảng viên B và sinh viên để test phân quyền.
2. Để kiểm tra cùng một tài khoản ở hai cửa sổ, mở hai tab rồi đăng nhập A. Để dùng hai tài khoản khác nhau, dùng cửa sổ thường và ẩn danh hoặc hai trình duyệt.
3. Tạo dữ liệu riêng có tên `TEST_THAI_1610`: môn đang hoạt động của A, lớp đang hoạt động thuộc môn đó, ít nhất 1 sinh viên test. Có 2 câu chính đã duyệt trong ngân hàng và đủ phương án đáp án để phát đề. Không sửa/xóa dữ liệu thật đang dùng.
4. Chuẩn bị DOCX có văn bản, PDF chọn/copy được chữ và PPTX có text. Có thể dùng đoạn kiến thức OOP phía cuối file này. Chuẩn bị thêm file TXT không được hỗ trợ, file PDF 0 byte và file TXT đổi đuôi thành DOCX để thử lỗi.
5. Lịch test thường đặt trong tương lai, thời lượng 20 phút, 2 câu chính và tổng tối đa 4 câu đào sâu. Riêng test vào thi cần phiên đang trong khung giờ thi; nên tạo phiên riêng có đúng 1 sinh viên test.
6. Ghi lại ID phiên, ID tài liệu từ URL để dùng cho các ca phân quyền. Không cần thay đổi trực tiếp database.

## A. Task Thai — 13 ca

| ID | Nội dung | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| T1-01 | Layout/điều hướng Razor Pages | Đăng nhập A. Lần lượt mở Môn học, Lịch thi, Tài liệu & AI, rồi dùng nút quay lại. | Mở đúng trang; layout/menu không mất; không lỗi 404/500. |
| T1-02 | CRUD môn học | Tạo môn test có mã riêng; đổi tên rồi tải lại trang. Tạo thêm một môn trống khác để thử xóa. | Môn lưu đúng tên/mã, đúng giảng viên A; tên sửa giữ sau tải lại; môn trống xóa được. |
| T1-03 | Mã môn trùng và dữ liệu thiếu | Tạo môn trùng mã đã có; thử bỏ trống mã hoặc tên. | Báo lỗi; không tạo thêm môn trùng/rỗng. |
| T1-04 | Quyền chủ sở hữu | A ghi URL sửa môn/chi tiết phiên. Mở các URL đó dưới tài khoản B. Sau đó dùng sinh viên mở /Courses và /ExamSessions. | B không quản lý được dữ liệu của A; sinh viên không vào được màn hình quản trị. |
| T1-05 | Tạo phiên thi | Vào Lịch thi → Tạo lịch thi. Chọn môn/lớp test, ngày giờ tương lai, 20 phút, 2 câu chính và 4 câu đào sâu. Lưu rồi mở lại chi tiết. | Phiên được tạo, đúng môn/lớp/giảng viên, sinh viên và các thông số. Nếu chọn giảng viên, A chỉ có lựa chọn hợp lệ của mình. |
| T1-06 | Validation số câu/thời lượng | Tạo hoặc sửa phiên trước phát đề; thử số câu chính 0, số âm, thời lượng 0 hoặc bỏ trống tên. | Không lưu giá trị sai; báo lỗi phù hợp; phiên cũ giữ nguyên dữ liệu đã lưu. |
| T1-07 | Lưu cấu hình vấn đáp | Tại chi tiết phiên, bấm Cấu hình vấn đáp. Nhập 60 giây/câu, 2 lượt hỏi xoáy/câu chính; lưu, rời trang và mở lại. | Giữ đúng 60 và 2. |
| T1-08 | Biên cấu hình | Trong phiên 20 phút, lần lượt thử 0, 14, 1201, 1801 giây; thử -1 hoặc 6 lượt hỏi xoáy. Sau đó thử 15 giây/0 lượt và 60 giây/5 lượt. | Các giá trị sai bị chặn; 1201 giây bị chặn vì vượt thời lượng 1200 giây. Hai cấu hình hợp lệ lưu được. Kết thúc đưa về 60 giây/2 lượt. |
| T1-09 | Khóa sau phát đề | Phát đề cho phiên test. Thử đổi số câu chính hoặc môn, rồi thử sửa cấu hình vấn đáp. | Không thay đổi được các cấu hình làm sai đề đã phát. Những thông tin khác được phép sửa không thuộc ca này. |
| T1-10 | Hủy phiên | Dùng phiên chưa có sinh viên đang làm bài. Chuyển sang Đã hủy rồi tải lại; thử sửa cấu hình và tìm lựa chọn quay lại Xếp lịch. | Trạng thái hủy được giữ; cấu hình không bị sửa; không có chuyển ngược từ trạng thái kết thúc. |
| T1-11 | Trạng thái theo bài thi | Tạo phiên riêng đúng giờ có 1 sinh viên test và đủ đề. Sinh viên vào thi rồi nộp bài; giảng viên kiểm tra phiên. | Khi vào thi: Đang diễn ra. Khi lượt cuối kết thúc: Hoàn thành. Với nhiều sinh viên, còn người chưa kết thúc thì chưa hoàn thành toàn phiên. |
| T1-12 | Realtime hai cửa sổ | A mở danh sách Lịch thi ở tab 1 và sửa tên một phiên chưa phát đề tại tab 2. Lưu; quay sang tab 1, không F5. Có thể thử thêm sinh viên vào phiên ở tab 2, trong khi tab 1 mở chi tiết. | Danh sách cập nhật tên mới; danh sách/số sinh viên trong chi tiết được cập nhật theo sự kiện. Nếu trang thông báo cần cập nhật, kiểm tra nút cập nhật hoạt động. Không gõ dữ liệu vào tab đang chờ cập nhật. |
| T1-13 | Quyền join nhóm SignalR | Dùng A thử tham gia nhóm phiên của A, rồi phiên thuộc B bằng Console theo hướng dẫn dưới. Với sinh viên, thử phiên có mình và phiên không có mình. | Chủ phiên/sinh viên được xếp trong phiên tham gia được; người ngoài bị từ chối. Admin được phép quản lý. |

### Thao tác bổ sung cho T1-13

Trên trình duyệt đã đăng nhập tài khoản cần kiểm tra, mở F12 → Console. Thay số 123 bằng ID phiên muốn thử:

```javascript
const hubTest = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/exam-updates').build();
await hubTest.start();
await hubTest.invoke('JoinExam', 123);
await hubTest.stop();
```

Phiên được phép: invoke hoàn tất không có lỗi. Phiên không được phép: nhận lỗi không có quyền; nếu lệnh bị dừng vì lỗi, chạy riêng `await hubTest.stop()` để đóng kết nối test. Không dùng tài khoản Admin để kiểm tra tình huống người ngoài.

## B. Task 3 — 13 ca

Chạy bằng nút trên trình duyệt để kiểm tra cả form và điều hướng. Nếu T3-01 không qua, ghi lỗi này trước; các ca cần tài liệu đã hoàn tất có thể dùng tài liệu test sẵn có mà A được phép quản lý.

| ID | Nội dung | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| T3-01 | Upload DOCX qua giao diện | A mở Tài liệu & AI → chọn môn test → Xem tài liệu → Tải lên tài liệu mới. Chọn DOCX có chữ, bấm Tải lên và xử lý. | Nút gửi hoạt động; tải đúng môn đã chọn; xuất hiện tài liệu mới, không báo sai/mất môn học. |
| T3-02 | Xử lý nền/realtime | Ngay khi upload xong, mở chi tiết tài liệu và chờ, không F5. | Trạng thái từ chờ/đang xử lý sang hoàn tất; số chunks lớn hơn 0, có thể sinh câu hỏi. Có thể không nhìn thấy trạng thái trung gian nếu xử lý nhanh. |
| T3-03 | PDF/PPTX | Lặp lại T3-01,02 với PDF có chữ và PPTX có text. | Cả hai được xử lý hoàn tất, có chunks. PDF scan chỉ có ảnh không thuộc ca này. |
| T3-04 | File rỗng/sai đuôi | Lần lượt thử tải TXT và PDF 0 byte. Nếu hộp chọn chỉ hiện file hỗ trợ, chọn All files để thử TXT. | Báo lỗi định dạng/file rỗng; không tạo tài liệu hợp lệ và không hiện thành công giả. |
| T3-05 | Nội dung file hỏng | Upload TXT đã đổi đuôi thành DOCX. | Hệ thống báo lỗi xử lý/Failed; không báo hoàn tất, không sinh được câu hỏi từ file hỏng. |
| T3-06 | Sinh câu hỏi thật | Chọn tài liệu hoàn tất → Số lượng 2 → Bloom Hiểu → chủ đề đúng tài liệu → Sinh câu hỏi. Chờ yêu cầu hoàn tất. | Tạo 2 câu ở trạng thái chờ duyệt, có nội dung/đáp án/giải thích và gắn đúng tài liệu. Đánh giá thêm nội dung có bám tài liệu, không chỉ nhìn số lượng. |
| T3-07 | Giới hạn số câu | Lần lượt thử số lượng 0 và 21, sau đó mở lại danh sách câu hỏi. | Không tạo thêm câu; trình duyệt hoặc server chặn giá trị không hợp lệ. |
| T3-08 | Sửa và duyệt | Đi từ danh sách câu hỏi của tài liệu vào một câu chờ duyệt. Sửa nội dung, đáp án, giải thích, Bloom, chủ đề; chọn Duyệt; lưu rồi mở lại. | Các trường đã sửa được giữ nguyên; trạng thái Đã duyệt. |
| T3-09 | Từ chối/duyệt lại | Chọn câu thứ hai, ghi lý do và Từ chối. Với một câu đã duyệt, thử thay quyết định nếu giao diện còn cho thao tác. | Câu thứ hai thành Đã từ chối, có ghi chú. Câu đã có quyết định không bị ghi đè bằng quyết định duyệt lại. |
| T3-10 | Phân quyền tài liệu/câu hỏi | A ghi URL chi tiết tài liệu và URL duyệt câu hỏi. Dùng B hoặc sinh viên mở lại. | Không đọc được nội dung ngoài quyền; không sửa/duyệt/xóa tài liệu hoặc câu hỏi của A. |
| T3-11 | Điều hướng Xem/Duyệt trực tiếp — hồi quy lỗi đã sửa | Từ CHI TIẾT TÀI LIỆU, bấm Xem/Duyệt tại một câu vừa sinh. Bấm Quay lại danh sách; lặp lại với lưu quyết định nếu còn câu chờ duyệt. | Mong đợi quay về danh sách câu hỏi của đúng tài liệu. Bản trước sửa dùng documentId=0 và báo không tìm thấy tài liệu; bản sửa phải quay về đúng tài liệu. Lưu có thể đã thành công dù trang quay lại lỗi. |
| T3-12 | Tên người tải lên — hồi quy lỗi đã sửa | Mở danh sách tài liệu của môn vừa upload; nhìn cột Người tải lên. | Mong đợi hiện tên người upload. Bản trước sửa để trống; bản sửa phải có tên. |
| T3-13 | Xóa và giữ tài liệu khác | Chọn tài liệu test riêng, bấm Xóa, xác nhận. Tải lại danh sách và thử URL chi tiết cũ; kiểm tra một tài liệu khác vẫn xem được. | Tài liệu đã xóa không còn truy cập được; tài liệu khác giữ nguyên. Chỉ xóa dữ liệu test của mình. |

## Đoạn nội dung mẫu

Có thể chép đoạn sau vào Word và lưu DOCX, hoặc đưa lên slide để tạo PPTX có text:

> Lập trình hướng đối tượng tổ chức chương trình thành các lớp và đối tượng. Lớp mô tả thuộc tính và phương thức. Đóng gói che giấu trạng thái bên trong và cung cấp các thao tác có kiểm soát. Kế thừa cho phép lớp con tái sử dụng hành vi của lớp cha. Đa hình cho phép cùng một giao diện có nhiều cách triển khai. Trong C#, constructor khởi tạo đối tượng; interface mô tả hợp đồng mà lớp triển khai cần tuân theo. Những khái niệm này giúp chương trình dễ bảo trì và tái sử dụng hơn.

## Cách ghi kết quả

Với mỗi ID, ghi: `Đạt / Lỗi / Chưa test`, tài khoản, ID phiên/tài liệu, thao tác, kết quả thực tế và thời điểm. Khi lỗi, chụp trang và giữ URL. Nếu một bước chuẩn bị thất bại, các ca phụ thuộc ghi Chưa test; không tính là đã đạt.

Ưu tiên chạy: T1-05 → T1-07 → T1-08 → T1-12 → T3-01 → T3-02 → T3-06 → T3-08 → T3-11 → T3-12. Các ca hủy/xóa chạy sau cùng hoặc dùng dữ liệu khác.

Lưu ý phạm vi: cấu hình giây/câu và lượt hỏi xoáy đang được lưu để phục vụ luồng AI vấn đáp; phòng trắc nghiệm ASM1 chưa dùng đồng hồ riêng từng câu. Câu hỏi AI đã duyệt trong khu vực tài liệu chưa tự đưa vào ngân hàng trắc nghiệm ASM1. T3-11 và T3-12 là các ca tái hiện lỗi cũ đã được sửa; các bước bằng trình duyệt ở những ca còn lại vẫn cần người test đánh dấu kết quả.


## Hồi quy bổ sung sau sửa

- T3-14: Upload DOCX lớn hơn 30 MB và nhỏ hơn 50 MB; mong đợi gửi và xử lý được nếu nội dung hợp lệ.
- T3-15: Thử chặn thư viện SignalR hoặc ngắt/kết nối lại mạng; trang phải cập nhật trạng thái khi truy cập server được trở lại.
- T3-16: Khi mạng chậm, chờ trạng thái chuyển; không được có vòng lặp tải lại khiến trang mãi Processing.
- T3-17: Kiểm tra tên người duyệt và giờ tạo/xử lý theo giờ local sau khi duyệt.
