# Codex + MCP — Tối ưu UI/UX Phòng thi bằng giọng nói (PRN222)

## Bối cảnh
Đây là project **PRN222 AIVES**, độc lập với SWD, dùng kiến trúc **3-Layers: Presentation / Business Logic / Data Access**. Nhiệm vụ **CHỈ tối ưu giao diện Phòng thi bằng giọng nói hiện có**, theo Razor Pages / công nghệ Presentation thực tế trong repo. Không tạo lại trang, không chuyển framework, không thay đổi logic nghiệp vụ nếu không thực sự cần để giữ tương thích.

## Mục tiêu UI
Gọn gàng, nhất quán, dễ thao tác khi thi; **không thừa và không thiếu**. Giữ nguyên đầy đủ: thông tin môn thi, câu số/tổng câu, câu hỏi, thời gian còn lại, nút đọc câu hỏi (TTS), xin quyền microphone, bắt đầu/dừng thu âm, thời lượng ghi âm, transcript STT tiếng Việt, khả năng xem/sửa transcript *theo quy định hiện có*, lưu tạm, nộp câu trả lời, xử lý lỗi và phục hồi khi gián đoạn.

### Bố cục yêu cầu
- Header ngắn: `Thi vấn đáp [môn]` | `Câu X/Y` | `Thời gian còn lại` (nổi bật, dễ đọc). Bỏ thanh 3 bước chiếm chiều cao nếu nó chỉ là hướng dẫn lặp; nếu đang gắn logic, hỏi trước khi gỡ.
- Desktop: **2 cột**. Trái: một thẻ câu hỏi, nút `Nghe câu hỏi`, vùng transcript gọn (nếu đặt bên trái giúp đọc và sửa tốt hơn). Phải: một thẻ điều khiển thu âm gồm microphone, thời lượng, nút chính theo trạng thái và hành động nộp. **Không hiển thị transcript trùng ở hai nơi.**
- Mobile/tablet: chuyển về **1 cột**, thứ tự câu hỏi → thu âm → transcript → nộp, không tràn ngang.
- Rút ngắn vùng trống lớn của textarea nhưng vẫn dễ đọc/chỉnh sửa; thống nhất spacing, typography, đường viền, trạng thái nút và tương phản.
- Chỉ **một trạng thái hoạt động dễ hiểu** (vd. `Sẵn sàng`, `Đang ghi âm`, `Đang nhận dạng`, `Đã lưu`, `Mất kết nối`) thay vì nhiều nhãn `Sẵn sàng` và khung thông báo lặp.
- Thay thẻ `Trạng thái lưu tạm` dài bằng **một dòng lưu tạm nhỏ**; chỉ hiển thị `Đã lưu` nếu có xác nhận thật từ luồng lưu hiện có. `Bản thu dự phòng` chỉ xuất hiện khi có dữ liệu phục hồi; `Thử nhận dạng lại` chỉ xuất hiện khi cần.
- Hiển thị CTA đúng trạng thái: chưa có quyền → `Cho phép mic`; sẵn sàng → `Bắt đầu trả lời`; đang thu → `Dừng thu âm`; đã có nội dung hợp lệ → `Nộp câu trả lời`. Không để nhiều nút chính cạnh tranh hoặc hành động không khả dụng.
- Trạng thái lỗi phải rõ: mic bị từ chối, không hỗ trợ STT, TTS lỗi, upload/lưu thất bại, mất mạng, reconnect. Không che thông tin cần thiết khi có sự cố.
- Nút có nhãn dễ hiểu, keyboard focus, accessible labels, disabled/loading state phù hợp. Không chỉ dựa vào màu để báo trạng thái.

## Ràng buộc kỹ thuật tuyệt đối
1. **Đọc code thật trước**: tìm Razor Page, CSS/JS, service, các event SignalR hiện có và trạng thái UI; tái sử dụng component/style. Không phỏng đoán tên file hoặc tạo UI demo tách biệt.
2. **Không thay đổi business logic/API/schema/DB**, TTS, STT, MediaRecorder, timer backend, quyền thi, luồng câu hỏi, SignalR, autosave, submit hoặc dữ liệu đã lưu. Nếu thấy bug thực sự ngoài UI, mô tả và xin phép trước khi mở rộng phạm vi.
3. Không hiển thị trạng thái giả, không hardcode môn học/câu hỏi/thời gian; bind dữ liệu từ nguồn hiện có.
4. Không tự ý **xóa file, class, method, component, đoạn code, migration, bảng hay dữ liệu**. Nếu cần xóa bất kỳ thứ gì để tinh giản UI: **DỪNG, hỏi tôi trước**, nêu chính xác thứ cần xóa, tại sao, tác động và phương án không xóa; chỉ tiếp tục sau khi tôi đồng ý. Ưu tiên gộp/ẩn theo trạng thái và tái sử dụng.
5. Không chỉnh sửa file/commit ngoài scope; không ghi đè thay đổi chưa commit của người khác; không lộ secret. Không thêm dependency nếu CSS/JS hiện có đã đủ.

## Git: bắt buộc GiaThuan
- Kiểm tra `git status`, branch hiện tại, remote và `git fetch` an toàn nếu cần. Tôi **đã pull main**, không tự `pull`/merge/rebase main thêm nếu không cần thiết.
- Nếu branch `GiaThuan` đã có, checkout và giữ nguyên lịch sử; nếu chỉ có `origin/GiaThuan`, tạo local tracking branch; nếu chưa có, tạo `GiaThuan` từ HEAD phù hợp (ưu tiên main đã được cập nhật). Nếu working tree có thay đổi chưa commit, không chuyển branch hoặc ghi đè trước khi báo tôi.
- Chỉ commit **các file thuộc UI đã thay đổi cho nhiệm vụ này**, không `git add .` nếu có file khác; sau khi build/test thành công: `git commit -m "refactor(ui): simplify voice exam room"`; `git push -u origin GiaThuan` (hoặc `git push origin GiaThuan` nếu upstream đã có).
- **Tuyệt đối không push/merge vào main**, không force-push, reset --hard, clean hay rewrite history. Nếu push lỗi, báo lỗi và phương án, không tự phá lịch sử.

## Quy trình làm việc — tiết kiệm token
1. Đọc đúng file liên quan, báo ngắn **kế hoạch 3–6 gạch đầu dòng** và các phần UI thừa/sẽ gộp; không giải thích dài.
2. Thực hiện tối ưu trong scope theo nguyên tắc trên; chỉ hỏi khi cần xóa hoặc có thay đổi phá vỡ/thiếu thông tin bắt buộc.
3. Build/test có sẵn; kiểm tra thủ công các trạng thái: trước thu, xin quyền mic, đang thu, nhận dạng, lưu tạm, nộp, lỗi, reconnect; kiểm tra desktop/mobile. Nếu không thể test trình duyệt, nói rõ.
4. Nếu pass, commit và push lên **GiaThuan**. Báo cáo tối đa 8 dòng: file sửa, điểm cải thiện, build/test, commit hash, branch/push, vấn đề còn lại. Nếu build/test fail, sửa trong phạm vi; nếu chưa khắc phục, không commit như thể đã hoàn tất.

**Tiêu chí hoàn thành:** Giao diện gọn hơn rõ rệt, không còn trạng thái/khung lặp, thao tác thu âm → STT → kiểm tra → nộp dễ nhận biết; mọi chức năng và dữ liệu hiện có vẫn hoạt động, giữ đúng 3-Layers và Git branch `GiaThuan`.
