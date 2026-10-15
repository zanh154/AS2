# Bộ câu hỏi (Question bank)

Trạng thái: **đã code xong, đã nối vào luồng tạo lịch thi, đã có unit test.**
Ngày cập nhật: 2026-09-29. Đã merge `TriNguyen` vào `QuocAnh_Dev`.

---

## 1. Cài đặt cho thành viên mới

```bash
git checkout QuocAnh_Dev
dotnet build AssignmentPRN.slnx
dotnet test AssignmentPRN.slnx
```

Phần này cần bảng `question_options` và cột `answers.selected_option_id`. Cả hai đã nằm
sẵn trong dump ở [`database/dump/`](../database/dump/) — import dump là đủ, xem
[database/README.md](../database/README.md).

> **Chưa chạy script thì thêm câu hỏi sẽ báo "Không thể lưu câu hỏi."**
> Đó là thông báo dự phòng của `ServiceExecutor` khi có lỗi ngoài dự kiến; lỗi thật
> là MySQL không tìm thấy bảng `question_options`. Nhập từ CSV cũng hỏng y như vậy.
> Chạy xong script, 4 câu `SELECT` ở cuối file phải trả về đúng như phần "Kết quả
> mong đợi" ghi trong đó.

---

## 2. Phạm vi đã làm

| Tầng | Nội dung |
|---|---|
| DataAccess | Entity `Question`, `QuestionOption`, `CourseMaterial`, `ExamQuestion`; enum; mapping `AivesDbContext`; repository + contract |
| Business | `QuestionService`, `CourseMaterialService`, `QuestionPicker`, `QuestionCsvReader` |
| Presentation | `QuestionsController`, `CourseMaterialsController`, màn hình phát đề của `ExamSessionsController`, viewmodel, 6 view, menu, SCSS |
| Tests | `tests/AssignmentPRN.Tests`: `QuestionPickerTests`, `QuestionCsvReaderTests` |

Các controller mới chỉ hiện với vai trò `Admin` và `Lecturer`.

### Quy tắc đã chốt

- **Phân quyền:** `Admin` quản lý toàn bộ. `Lecturer` chỉ thao tác trên môn mình phụ
  trách; service chặn ở tầng Business, không phụ thuộc việc ẩn nút trên giao diện.
- **Chủ đề bắt buộc:** mỗi câu hỏi phải gắn đúng 1 tài liệu
  (`questions.source_material_id`). `CourseMaterial` chính là "chủ đề".
- **Không có bước duyệt:** câu hỏi trắc nghiệm tạo ra là `Approved` ngay.
  Dùng `Archived` để ngừng cấp phát, **không xóa** câu đã gán cho lượt thi.
- **Random:** mỗi sinh viên có bộ câu riêng, không trùng nhau trong cùng phiên.
  Kết quả được ghi snapshot vào `exam_questions` ngay khi tạo lượt thi.
- **Tài liệu:** chỉ PDF/DOCX/PPTX, tối đa 20 MB. CSV tối đa 2 MB.
  File lưu ở `src/AssignmentPRN.DataAccess/Storage/materials` (ngoài `wwwroot` để không bị phục vụ tĩnh), `file_name` giữ tên gốc (để CSV trỏ tới được),
  `file_path` chứa tên GUID duy nhất trên đĩa.
- `course_materials.processing_status` luôn ghi `Completed` vì không có bước xử lý;
  cột được giữ lại cho tính năng đọc nội dung tài liệu bằng AI ở module sau.

---

## 3. Thay đổi database

Hai thay đổi schema, các phần còn lại là sửa dữ liệu:

| # | Loại | Nội dung |
|---|---|---|
| 0 | ALTER | `answers.selected_option_id` — phương án sinh viên đã chọn (script 20260930) |
| 1 | CREATE | Bảng `question_options` — lưu các đáp án A/B/C/D |
| 2 | INDEX | `idx_questions_bank (course_id, question_type, status)` cho truy vấn lấy ngân hàng câu hỏi |
| 3 | UPDATE | Câu `Main` còn `Draft`/`PendingReview` → `Approved` |
| 4 | UPDATE | Tài liệu còn `processing_status = 'Pending'` → `'Completed'` |

Lý do cần bảng `question_options`: cột `questions.expected_answer` chỉ chứa được
một chuỗi đoạn, không lưu được danh sách đáp án có đánh dấu đúng/sai và thứ tự.

Script có sẵn 4 câu `SELECT` kiểm tra ở cuối, chạy xong nhìn kết quả là biết đã đúng.

---

## 4. Luồng CSV import

Tải mẫu tại `/Questions/Template`. File `.csv` hoặc `.txt`, phân cách bằng `;`
hoặc `,`, có dòng tiêu đề:

```
question_text;difficulty;bloom;material;expected_answer;option_a;option_b;option_c;option_d;correct
```

- `difficulty`: `Easy` | `Medium` | `Hard`
- `bloom`: `Remember` | `Understand` | `Apply` | `Analyze`
- `material`: tên file tài liệu, ví dụ `demo.pdf` (bắt buộc)
- `correct`: `A`–`H` hoặc `1`–`8`, trỏ vào cột `option_...`
- Ô trống được bỏ qua; ô có dấu nháy kép hỗ trợ dấu `;` bên trong.

Mỗi dòng lỗi được báo riêng kèm số dòng, **dòng hợp lệ vẫn được nhập**. Không có câu hỏi
nào bị ghi nửa vời: toàn bộ thao tác của một dòng nằm trong một transaction.

---

## 5. API chính

| Method | Đường dẫn | Công dụng |
|---|---|---|
| GET | `/Questions?courseId=&keyword=&difficulty=&materialId=&page=` | Danh sách, lọc, phân trang |
| GET | `/Questions/Create?courseId=` | Tạo câu hỏi |
| POST | `/Questions/Save` | Lưu (dùng chung tạo và sửa) |
| POST | `/Questions/Archive/{id}` | Ngừng cấp phát |
| GET | `/Questions/Import?courseId=` | Nhập CSV |
| GET | `/Questions/Template` | Tải file mẫu |
| GET | `/CourseMaterials` | Danh sách tài liệu |
| GET/POST | `/CourseMaterials/Upload` | Tải tài liệu lên |
| GET | `/CourseMaterials/Download/{id}` | Mở tài liệu |
| GET/POST | `/CourseMaterials/Edit/{id}` | Sửa môn học và tên tệp |
| POST | `/CourseMaterials/Delete/{id}` | Xóa (chặn nếu câu hỏi còn dùng) |

Luật khi sửa tài liệu:

- **Đổi môn** chỉ được khi tài liệu chưa là chủ đề của câu hỏi nào. Còn câu hỏi thì
  chặn, vì câu hỏi và chủ đề của nó phải nằm cùng một môn, mà chuyển cả câu hỏi theo
  có thể làm hỏng đề đã phát.
- **Đổi tên** thì lúc nào cũng được, kể cả khi đang có câu hỏi — nhưng phải giữ nguyên
  phần mở rộng, vì `file_type` được ghi từ đuôi tệp lúc tải lên và tệp trên đĩa không
  đổi. Tên này cũng là giá trị cột `material` của file CSV, đổi tên thì phải sửa CSV.
- Tên tệp không được trùng với tài liệu khác trong cùng môn.
- Tệp trên đĩa không bị đụng tới: `file_path` là GUID, `file_name` chỉ là nhãn.

Phát đề cho lượt thi nằm trong `ExamSessionsController`:

| Method | Đường dẫn | Công dụng |
|---|---|---|
| GET | `/ExamSessions/Questions/{id}` | Giao diện cấu hình + xem đề từng sinh viên |
| POST | `/ExamSessions/AssignQuestions` | Phát đề ngẫu nhiên theo cấu hình |
| POST | `/ExamSessions/ClearQuestions/{id}` | Huỷ đề để phát lại |

---

## 5b. Luồng phát đề

Đề **rút khi sinh viên vào thi**, không phải khi giảng viên bấm. Tạo lịch thi không sinh
đề gì cả.

### Sinh viên tự vào thi (luồng chính)

| Method | Đường dẫn | Công dụng |
|---|---|---|
| POST | `/ExamRoom/Enter/{candidateId}` | Vào ca thi: rút đề nếu chưa có, bắt đầu tính giờ |
| GET | `/ExamRoom/Index/{candidateId}` | Làm bài / xem lại bài, không ghi gì thêm |
| POST | `/ExamRoom/Submit/{candidateId}` | Nộp bài: lưu đáp án và kết thúc lượt thi |

1. `Lịch thi của tôi` hiện nút **Vào thi** khi tới lượt. Nút chỉ hiện đúng khoảng thời
   gian mà service cho phép, nên bấm được là vào được.
2. Bấm vào → `IQuestionService.EnterExamAsync` kiểm tra lần lượt: đúng chủ lượt thi,
   lượt chưa bị huỷ/vắng/đã xong, phiên thi đang `Scheduled` hoặc `InProgress`, và đồng
   hồ nằm **trong đúng khung giờ của ca** (`ExamSessionRules.IsSlotOpen`, không vào sớm
   được phút nào).
3. Qua hết → rút `MainQuestionCount` câu, **loại mọi câu đã phát cho sinh viên khác
   trong cùng phiên**, ghi vào `exam_questions`, chuyển lượt sang `InProgress` và ghi
   `started_at`.
4. Vào lại hoặc F5 → giữ nguyên bộ câu cũ và giữ nguyên `started_at`.
5. Chọn đáp án rồi bấm **Nộp bài** → `SubmitExamAsync` ghi một dòng `answers` cho mỗi
   câu (`selected_option_id`, câu bỏ trống thì `NULL`), đặt `exam_questions.is_completed`,
   chuyển lượt sang `Completed` và ghi `finished_at`. Tất cả trong một `SaveChanges`.
6. **Nộp xong là kết thúc**: vào lại chỉ xem được bài làm, không sửa và không nộp lại.
7. Ngân hàng không đủ câu → báo sinh viên liên hệ giảng viên, không ghi nửa vời.

### Đồng hồ và việc khoá màn hình thi

- Phòng thi dùng layout riêng `_ExamLayout`: **không sidebar, không breadcrumb, không nút
  đăng xuất**. Trong lúc đang làm bài, lối ra duy nhất trên trang là nút Nộp bài. Nộp xong
  thì link quay lại lịch thi mới hiện ra.
- Đồng hồ đếm ngược lấy `SecondsRemaining` do **server** tính (`EndTime - DateTime.Now`),
  không lấy giờ máy sinh viên — máy lệch giờ cũng không ăn gian được. `exam-room.js` đếm
  theo mốc `Date.now() + seconds` nên tab bị trình duyệt tiết chế vẫn về đúng số giây.
- Về `00:00` → tự động nộp bài. Server vẫn là nơi quyết định: `CanAnswer` tính lại theo
  `IsSlotOpen` mỗi lần tải trang, nên hết giờ là không sửa được nữa dù client làm gì.
- `ExamSessionRules.SubmitGrace` = **2 phút**: cho phép nộp muộn trong 2 phút sau khi hết
  giờ, để request tự nộp kịp tới nơi. Quá đó thì từ chối. Lưu ý đây là điều kiện **nộp**,
  khác với điều kiện **vào thi** (`IsSlotOpen`, không vào sớm hay muộn được phút nào).
- `beforeunload` cảnh báo khi rời trang lúc chưa nộp.

> Giới hạn thật cần biết: **trình duyệt không cho phép khoá cứng**. Sinh viên vẫn có thể
> đóng tab, gõ URL khác hay tắt máy — chỉ nhận được hộp thoại cảnh báo của trình duyệt.
> Muốn chặn thật thì phải giám sát phía server (ghi nhận rời phòng thi) hoặc dùng chế độ
> khoá của hệ điều hành, cả hai đều nằm ngoài Bộ câu hỏi.

> Nếu sinh viên đóng trình duyệt và không quay lại, lượt thi nằm mãi ở `InProgress` vì
> không có gì chạy nền để chốt. Giảng viên xử lý tay trong màn hình phiên thi.

Hai điều được bảo đảm bằng cấu trúc chứ không nhờ nhớ kiểm tra:

- Kiểu `ExamRoomQuestion` / `ExamRoomOption` mà sinh viên nhận **không có trường đánh dấu
  đáp án đúng**, nên đáp án không thể lọt ra theo đường này.
- Mỗi đáp án gửi lên phải là phương án **của đúng câu hỏi đó**: `ExamDraftValidation.Validate`
  đối chiếu với `ExamDraftQuestion.OptionIds` trong ảnh chụp đọc được ngay trong transaction,
  nên sửa HTML để gán đáp án của câu khác sẽ bị từ chối và không ghi gì cả.

### Giảng viên phát trước (tuỳ chọn)

| Method | Đường dẫn | Công dụng |
|---|---|---|
| GET | `/ExamSessions/Questions/{id}` | Cấu hình + xem đề từng sinh viên |
| POST | `/ExamSessions/AssignQuestions` | Phát trước theo chủ đề / độ khó đã chọn |
| POST | `/ExamSessions/ClearQuestions/{id}` | Huỷ đề để phát lại |

Dùng khi muốn xem trước đề hoặc giới hạn phạm vi theo chủ đề/độ khó. Sinh viên đã có đề
thì lúc vào thi dùng luôn đề đó. `AssignToExamAsync` chỉ bù cho sinh viên **chưa có đề**,
nên không ai bị phát chồng. `ClearExamAssignmentAsync` **từ chối** khi đã có câu được hỏi
hoặc trả lời (`exam_questions.asked_at` / `is_completed`).

Vào màn hình này từ: `Lịch thi → chi tiết phiên thi → thẻ "Ngân hàng câu hỏi"`.

---

## 6. Kiểm thử đã thực hiện

| Hạng mục | Kết quả |
|---|---|
| Build DataAccess / Business / Presentation | 0 warning, 0 error |
| SQL chạy lần 1 và lần 2 | 0 lỗi, index không bị tạo trùng |
| GET trang List / Create / Import | HTTP 200 |
| Tạo tài liệu | `file_name=demo.pdf`, `file_path=/materials/{guid}.pdf`, `processing_status=Completed` |
| Tạo câu hỏi | 3 option, đúng 1 đáp án, `status=Approved`, đúng `source_material_id` |
| Từ chối 2 đáp án đúng / thiếu option / nội dung rỗng | Bị chặn, DB không thêm câu nào |
| Import CSV có dòng lỗi | Dòng hợp lệ vào DB, dòng lỗi báo kèm số dòng |
| Build toàn solution + `dotnet test` | 0 warning, 0 error, 23/23 test pass |

### Unit test tự động (`dotnet test AssignmentPRN.slnx`)

| File | Kiểm tra |
|---|---|
| `QuestionPickerTests` | Đúng số câu; không trùng trong một đề; loại câu đã phát; pool cạn thì trả về phần còn lại thay vì ném lỗi; có xáo thật; **phát cả phiên thi 8 sinh viên × 5 câu không ai trùng ai** |
| `QuestionCsvReaderTests` | Đọc đúng dòng hợp lệ; `correct` dạng chữ và dạng số; giá trị mặc định `Medium`/`Understand`; dòng lỗi báo kèm số dòng mà dòng tốt vẫn vào; ô có dấu nháy chứa `;`; file phân cách bằng `,`; tài liệu không khớp thì để trống chủ đề; file rỗng / thiếu cột |

### Chưa kiểm thử

- Nhập CSV với tên tài liệu tiếng Việt có dấu.
- Sửa / Archive / xóa tài liệu / tải tài liệu qua giao diện.
- Phát đề trên database thật với phiên thi nhiều sinh viên (mới test ở tầng logic).

---

## 7. Ghi chú cho người tiếp tục

- Câu hỏi đào sâu và kết quả chưa động tới: DB có `questions.question_type = 'FollowUp'` nhưng chưa có
  `parent_question_id`; bảng `follow_up_questions` đang liên kết qua `answers`. Cần thống
  nhất lại khi làm phần câu hỏi nối tiếp.
- `course_materials.processing_status` và `questions.expected_answer` được giữ lại dù
  Bộ câu hỏi chưa dùng tới, để khớp schema đã bàn giao.
- Cột `is_correct` không có ràng buộc "đúng 1 dòng" ở tầng database. Quy tắc này nằm ở
  service và viewmodel; nếu sau này có ai ghi thẳng bằng SQL thì phải tự giữ.
