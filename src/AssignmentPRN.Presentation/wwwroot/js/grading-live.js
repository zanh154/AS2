// Màn hình giảng viên đi theo việc AI chấm bài, nhờ sự kiện "GradingChanged" mà
// SignalRGradingNotifier gửi tới nhóm của phiên thi. Sự kiện không mang điểm hay
// transcript: trang tự tải lại số liệu qua service, nơi quyền xem được kiểm tra lại.
//
// exam-realtime.js giữ kết nối và gọi apply() mỗi khi có sự kiện; mọi quyết định nằm ở
// decide(), không đụng DOM, để test được mà không cần trình duyệt.
(function () {
  const PROGRESS = "grading-progress";
  const DONE = "grading-done";

  // Trang đang mở là gì: bảng điểm của một phiên (chỉ có examId) hay bài làm của một
  // sinh viên (có thêm candidateId). Không phải trang kết quả thì trả về null.
  const pageOf = root => {
    if (root?.dataset?.livePage !== "ExamResults") return null;
    const examId = Number(root.dataset.examId);
    if (!(examId > 0)) return null;
    const candidateId = Number(root.dataset.candidateId);
    return { examId, candidateId: candidateId > 0 ? candidateId : null };
  };

  // Một sự kiện thì trang nên làm gì. dirty: giảng viên đang nhập dở điểm hoặc nhận xét.
  const decide = (event, page, dirty) => {
    if (!event || !page || event.examId !== page.examId) return { kind: "ignore" };

    // Trang bài làm chỉ quan tâm tới đúng bài của mình.
    if (page.candidateId !== null) {
      if (event.candidateId !== page.candidateId) return { kind: "ignore" };
      if (event.kind === PROGRESS) {
        return { kind: "progress", text: `AI đang chấm bài này: ${event.done}/${event.total} câu.` };
      }
      if (event.kind !== DONE) return { kind: "ignore" };
      // Tự tải lại sẽ xoá điểm và nhận xét giảng viên đang gõ dở. Hỏi trước.
      return dirty
        ? { kind: "notice", text: "AI vừa chấm xong bài này. Lưu phần đang nhập rồi bấm Cập nhật để xem." }
        : { kind: "reload" };
    }

    // Bảng điểm: một bài trong phiên chấm xong thì cả bảng đổi.
    if (event.kind !== DONE) return { kind: "ignore" };
    return dirty
      ? { kind: "notice", text: "AI vừa chấm xong một bài. Bấm Cập nhật để xem điểm mới." }
      : { kind: "reload" };
  };

  const apply = (event, root, dirty, env = {}) => {
    const doc = env.document ?? document;
    const reload = env.reload ?? (() => location.reload());
    const action = decide(event, pageOf(root), dirty);

    if (action.kind === "progress") {
      const status = doc.getElementById("grading-live-status");
      if (status) { status.textContent = action.text; status.hidden = false; }
    } else if (action.kind === "notice") {
      const notice = doc.getElementById("realtime-notice");
      if (notice) { notice.hidden = false; doc.getElementById("realtime-notice-message").textContent = action.text; }
    } else if (action.kind === "reload") {
      reload();
    }
    return action;
  };

  window.aivesGradingLive = { pageOf, decide, apply };
})();
