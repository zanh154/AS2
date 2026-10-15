# Câu hỏi đào sâu và kết quả thi

Trạng thái: **đã code xong, đã chạy script trên DB chung, đã có unit test và kiểm thử trên trình duyệt.**
Nhánh: `TriMinhDev`. Ngày cập nhật: 2026-09-29.

---

## 1. Cài đặt cho thành viên mới

```bash
git checkout TriMinhDev
dotnet build AssignmentPRN.slnx
dotnet test AssignmentPRN.slnx
```

Phần này cần cột `exam_questions.parent_exam_question_id`. Cột này đã nằm sẵn trong dump
ở [`database/dump/`](../database/dump/) — import dump là đủ, xem
[database/README.md](../database/README.md).

> Chưa chạy script thì phòng thi và trang kết quả báo lỗi, vì EF đọc cột
> `exam_questions.parent_exam_question_id` không tồn tại.

---

## 2. Ý tưởng và luồng nghiệp vụ

Đề bài yêu cầu "hỏi xoáy" dựa trên câu trả lời. Hệ thống thi hiện là trắc nghiệm nộp một
lần, nên Câu hỏi đào sâu và kết quả chọn cách **thi 2 vòng**, không phải viết lại phòng thi:

```
Vào thi ──► Vòng 1: câu chính ──nộp──► chấm ngay phía server
                                      │
          ┌───────────────────────────┴───────────────────────────┐
          │ có MaxFollowUpCount > 0, còn ≥ 1 phút, ngân hàng có câu │
          ▼                                                       ▼ không
   Vòng 2: câu đào sâu (cùng đồng hồ ca thi) ──nộp──►   Hoàn thành ──► Kết quả
```

### Luật chọn câu đào sâu (`FollowUpPlanner`)

| Luật | Chi tiết |
|---|---|
| Cùng chủ đề | Câu đào sâu phải cùng tài liệu (`source_material_id`) với câu chính nó hỏi xoáy |
| Sai / bỏ trống | Hỏi câu **dễ hơn hoặc bằng**: thử mức gần nhất bên dưới trước, cuối cùng mới đến cùng mức |
| Đúng | Hỏi câu **khó hơn hoặc bằng**: thử mức gần nhất bên trên trước, cuối cùng mới đến cùng mức |
| Thứ tự ưu tiên | Câu sai trước, câu đúng sau, cùng nhóm thì theo thứ tự trong đề |
| Giới hạn | Tổng số câu ≤ `MaxFollowUpCount` của phiên; mỗi câu chính tối đa 1 câu đào sâu |
| Không lặp | Không lặp câu trong một bài. Trong cùng phiên thì **ưu tiên** câu chưa ai được hỏi, nhưng vẫn cho dùng lại nếu ngân hàng hết câu, để ngân hàng nhỏ vẫn phục vụ được mọi sinh viên |
| Thời gian | Chỉ mở vòng 2 khi ca còn ≥ 1 phút (`FollowUpPlanner.MinimumTimeLeft`). Hết giờ tự nộp ở vòng 1 thì bài đóng luôn |

### Tính điểm (`ExamScoring`)

- Câu chính có trọng số **1**, câu đào sâu có trọng số **0,5**.
- Điểm = tổng trọng số các câu đúng / tổng trọng số × 10, làm tròn **1 chữ số** (half away from zero).
- Ví dụ: đúng 1/3 câu chính và 1/2 câu đào sâu thì (1 + 0,5) / (3 + 1) × 10 = 3,75, làm tròn **3,8**.

> So với bản kế hoạch: điểm làm tròn 1 chữ số thay vì 2, để khớp với trang kết quả
> và test có sẵn của Bộ câu hỏi.

### Kết quả cho giảng viên / admin

- **Bảng điểm phiên:** số câu chính đúng, số câu đào sâu đúng, điểm, trạng thái. Kèm tổng hợp số đã thi, số vắng và điểm trung bình (chỉ tính bài đã hoàn thành).
- **Bài làm từng sinh viên:** câu chính, câu đào sâu kèm nhãn "Đào sâu cho câu X", phương án đã chọn, đáp án đúng. Có thêm **đáp án mong đợi** (`expected_answer`), chỉ hiện cho giám khảo, sinh viên không bao giờ thấy.
- **Chốt ca đã hết giờ** (`OverdueSlotRules`):
  - Ca đã hết giờ mà sinh viên vẫn ở trạng thái Chờ thi: chuyển sang **Vắng**.
  - Ca đã hết giờ cộng 2 phút ân hạn mà sinh viên vẫn ở trạng thái Đang thi (rời trang không nộp): chuyển sang **Đã thi**, chấm trên phần đã lưu.
  - Nếu không có bước này, phiên thi không bao giờ chuyển sang Hoàn thành được, vì quy tắc Môn học và phiên thi cấm hoàn thành khi còn sinh viên Chờ thi hoặc Đang thi.
- Phân quyền: giảng viên chỉ xem được phiên mình phụ trách, kiểm tra ở tầng Business.

---

## 3. Thay đổi database

| Loại | Nội dung |
|---|---|
| ALTER | `exam_questions.parent_exam_question_id INT NULL`: NULL là câu chính, có giá trị là câu đào sâu của câu chính đó |
| FK | `fk_exam_questions_parent` tự tham chiếu, `ON DELETE CASCADE` (huỷ đề thì câu đào sâu mất theo) |

Không cần bảng mới: câu đào sâu vẫn là một dòng `exam_questions` và câu trả lời vẫn nằm ở `answers`.

---

## 4. Thay đổi theo tầng

| Tầng | File | Nội dung |
|---|---|---|
| DB | Cột `exam_questions.parent_exam_question_id` + khoá ngoại | Đã có trong dump; script gốc xem ở lịch sử Git |
| DataAccess | `Entities/ExamQuestion.cs`, `Data/AivesDbContext.cs` | Property và mapping `ParentExamQuestionId` |
| | `Repositories/QuestionRepository.cs` | Lưu loại câu; `ListFollowUpPoolAsync`; `SubmitAnswersAsync` nhận thêm vòng 2 (1 transaction); đọc `ParentExamQuestionId`, `MaterialId`, `ExpectedAnswer` (chỉ khi giám khảo xem) |
| | `Repositories/ExamResultRepository.cs` (mới) | Đọc bảng điểm, chấm ngay trong SQL, cập nhật trạng thái khi chốt ca |
| | `Contracts/ExamResultContracts.cs` (mới) | Read model cho kết quả |
| Business | `Services/QuestionService.cs` → `PlanFollowUps` | Luật chọn câu đào sâu, logic thuần |
| | `Interfaces/Contracts.cs` → `ExamScoring` | Tính điểm có trọng số |
| | `Services/ExamResultService.cs` → `CloseOverdueSlots` | Luật chốt ca hết giờ |
| | `Services/ExamResultService.cs` (mới) + `Interfaces/IExamResultService` | Bảng điểm, bài làm, chốt ca, phân quyền |
| | `Services/QuestionService.cs` | `SubmitExamAsync` chia vòng: `CleanAnswers`, `PlanFollowUpsAsync`; kiểm tra loại câu khi lưu |
| Presentation | `Controllers/ExamResultsController.cs` (mới) | `Index/{examId}`, `Candidate/{candidateId}`, `CloseOverdue` (POST, có anti-forgery) |
| | `Views/ExamResults/Index`, `Candidate` (mới) | Dùng `_DataTable`, `metric-card`, `content-card`, breadcrumb có sẵn |
| | `Views/ExamRoom/_ExamRoomQuestion`, `Views/Shared/_ExamResultQuestion` (mới) | Partial dùng chung cho 2 vòng, cho cả sinh viên và giảng viên |
| | `Views/ExamRoom/Index`, `Result` | Hiện vòng 2, tách câu chính và câu đào sâu |
| | `Views/Questions/Edit`, `Index` | Ô "Loại câu hỏi", nhãn "Đào sâu" |
| | `Views/ExamSessions/Details` | Thêm khối "Kết quả thi" có nút "Xem kết quả" |
| | `Styles/_exam-result.scss` (mới) | Chỉ dùng token trong `_tokens.scss`, không có mã màu nào viết cứng |

Bộ câu hỏi chỉ bị sửa nhẹ:
- Form câu hỏi có thêm ô **Loại câu** (Chính / Đào sâu).
- Câu đào sâu bắt buộc có chủ đề.
- Không đổi được loại của câu đã phát cho lượt thi.
- CSV giữ nguyên định dạng, câu nhập từ CSV vẫn là câu chính.

---

## 5. Đường dẫn

| Method | Đường dẫn | Ai dùng | Công dụng |
|---|---|---|---|
| POST | `/ExamRoom/Submit/{candidateId}` | Sinh viên | Nộp vòng đang làm; nộp vòng 1 có thể mở vòng 2 |
| GET | `/ExamResults/Index/{examId}` | Admin, Giảng viên | Bảng điểm phiên |
| GET | `/ExamResults/Candidate/{candidateId}` | Admin, Giảng viên | Bài làm một sinh viên |
| POST | `/ExamResults/CloseOverdue/{examId}` | Admin, Giảng viên | Chốt các ca đã hết giờ |

---

## 6. Kiểm thử

### Unit test: 67/67 pass (trước Câu hỏi đào sâu và kết quả là 33)

| File | Nội dung |
|---|---|
| `FollowUpPlannerTests` (14) | Sai thì dễ hơn, đúng thì khó hơn, lùi về cùng mức, sai không bao giờ khó hơn, ưu tiên câu sai, giới hạn số câu, không lặp, ưu tiên câu mới trong phiên, bỏ qua câu không có chủ đề, pool rỗng, luật 1 phút |
| `ExamScoringTests` (5) | Trọng số, làm tròn, bài rỗng |
| `OverdueSlotRulesTests` (10) | Vắng, ân hạn khi đang thi, không đụng ca đã chốt, điểm trung bình |
| `ExamResultResponseTests` (+2) | Điểm có câu đào sâu, chia vòng trong phòng thi |
| `QuestionEditViewModelTests` (+3) | Câu đào sâu bắt buộc có chủ đề |

### Kiểm thử end-to-end trên trình duyệt (DB Aiven, dữ liệu `[TEST-M6]`)

| # | Kịch bản | Kết quả |
|---|---|---|
| 1 | Sinh viên đúng 1, sai 2 câu chính (Trung bình) | Vòng 2 có 2 câu **Dễ**, gắn đúng vào 2 câu sai; `parent_exam_question_id` đúng trong DB ✅ |
| 2 | Chèn input ẩn để sửa đáp án vòng 1 khi đang ở vòng 2 | Server bỏ qua, vòng 1 không đổi ✅ |
| 3 | Điểm 1/3 câu chính + 1/2 câu đào sâu | 3,8, khớp tính tay ✅ |
| 4 | Sinh viên đúng hết 3 câu chính | Vòng 2 có 2 câu **Khó** ✅ |
| 5 | Nộp vòng 2 để trống | Hoàn thành, tính là bỏ trống, 7,5 điểm ✅ |
| 6 | Giảng viên xem bảng điểm và bài làm | Số liệu đúng, có "Đáp án mong đợi"; trang của sinh viên không có ✅ |
| 7 | Chốt ca: sinh viên B không vào thi | B chuyển sang Vắng, nút chốt biến mất ✅ |
| 8 | Giảng viên mở kết quả hoặc bài làm của phiên người khác | Bị chặn: "Bạn không có quyền…" ✅ |
| 9 | Sinh viên mở `/ExamResults` | Chuyển đến trang AccessDenied ✅ |
| 10 | Tạo câu đào sâu không có chủ đề; đổi loại câu đã phát | Bị chặn, thông báo đúng; có chủ đề thì lưu được ✅ |
| 11 | Giao diện mobile 375px | Thẻ thống kê xếp 1 cột, câu đào sâu bỏ thụt lề ✅ |

---

## 7. Đánh giá kết quả

**Đã đạt**
- Đủ 6 đầu việc của Câu hỏi đào sâu và kết quả trong bảng phân công: logic đào sâu/thích ứng, giới hạn số câu, lưu câu đã hỏi và câu trả lời, kết quả phiên thi, giao diện, test.
- Giữ đúng kiến trúc 3 tầng:
  - Luật nghiệp vụ nằm trong 3 lớp static thuần (`FollowUpPlanner`, `ExamScoring`, `OverdueSlotRules`) nên test được mà không cần DB.
  - Controller chỉ điều hướng.
  - Presentation không gọi repository.
- An toàn:
  - Đáp án được chấm phía server.
  - Vòng đã nộp không sửa được.
  - Lưu đáp án và phát vòng 2 nằm trong 1 transaction.
  - "Đáp án mong đợi" chỉ được đọc khi giám khảo xem.
  - Phân quyền theo chủ phiên thi.
- Giao diện đồng bộ: dùng lại component chung (`_DataTable`, `metric-card`, `status-chip`, hộp xác nhận chung); màu và font lấy hoàn toàn từ `_tokens.scss`.

**Hạn chế**
- Câu đào sâu vẫn là trắc nghiệm do giảng viên soạn sẵn, chưa phải AI sinh câu hay chấm câu trả lời tự luận/giọng nói.
- Mỗi câu chính chỉ có tối đa 1 câu đào sâu (một tầng), chưa hỏi xoáy nhiều tầng.
- Ngân hàng câu đào sâu nhỏ thì sinh viên sau có thể gặp lại câu mà sinh viên trước đã được hỏi. Đây là lựa chọn có chủ đích, để không có sinh viên nào bị thiếu câu.
- Chưa có xuất bảng điểm ra Excel.

**Hướng mở rộng**
- Thay nguồn câu trong `PlanFollowUpsAsync` bằng một service AI (sinh câu từ tài liệu và câu trả lời) mà giữ nguyên luồng 2 vòng và cách tính điểm.
- Dùng cột `answers.transcript` / `audio_path` có sẵn cho phần vấn đáp bằng giọng nói.
