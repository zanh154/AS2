document.addEventListener("DOMContentLoaded", () => {
  if (!window.signalR || window.__aivesExamRealtimeInitialized) return;
  window.__aivesExamRealtimeInitialized = true;

  const root = document.querySelector("[data-live-page]");
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/exam-updates")
    .withAutomaticReconnect()
    .build();
  let busy = false;
  let dirty = false;

  // Theo dõi toàn trang, kể cả các màn hình chưa có data-live-page. Nhờ vậy
  // realtime không tải lại và làm mất nội dung một form đang được nhập dở.
  document.addEventListener("input", event => { if (event.target.closest?.("form")) dirty = true; });
  document.addEventListener("change", event => { if (event.target.closest?.("form")) dirty = true; });

  const isQuestionPage = () => /^\/(Questions)(\/Index)?\/?$/i.test(location.pathname);
  const isRefreshablePage = () => /^(\/Questions|\/ExamSessions|\/Courses)(\/Index)?\/?$/i.test(location.pathname)
    || /^\/ExamSessions\/Details(?:\/\d+)?\/?$/i.test(location.pathname);

  const showNotice = message => {
    const notice = document.getElementById("realtime-notice");
    const messageElement = document.getElementById("realtime-notice-message");
    if (!notice || !messageElement) return;
    if (!notice.hidden && messageElement.textContent === message) return;
    messageElement.textContent = message;
    notice.hidden = false;
  };

  const refresh = async () => {
    if (!root || busy || dirty || !isRefreshablePage()) return false;
    const snapshot = root.dataset.snapshotUrl;
    if (!snapshot) return false;

    busy = true;
    try {
      const response = await fetch(snapshot, { headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!response.ok || response.redirected) return false;

      const html = await response.text();
      const fragment = new DOMParser().parseFromString(html, "text/html");
      if (root.querySelector("[data-live-region]")) {
        root.querySelectorAll("[data-live-region]").forEach(region => {
          const incoming = fragment.querySelector(`[data-live-region="${region.dataset.liveRegion}"]`);
          if (incoming) { region.replaceWith(incoming); window.initAivesDatePickers?.(incoming); }
        });
      } else {
        root.innerHTML = html;
        window.initAivesDatePickers?.(root);
      }
      root.dispatchEvent(new Event("live-content-updated"));
      return true;
    } finally {
      busy = false;
    }
  };

  const updatePage = async message => {
    if (dirty) {
      showNotice(`${message} Hãy lưu phần đang nhập để xem dữ liệu mới.`);
      return;
    }

    if (await refresh().catch(() => false)) {
      showNotice(`${message} Dữ liệu đã tự động cập nhật.`);
      return;
    }

    // Các màn hình chưa có snapshot vẫn phải thể hiện realtime khi demo.
    // Chỉ tải lại khi không có form đang nhập dở.
    location.reload();
  };

  connection.on("ExamChanged", async event => {
    const kind = String(event?.kind ?? "");

    if (kind === "questions") {
      const currentCourseId = Number(root?.dataset.courseId);
      const eventCourseId = Number(event?.courseId);
      if (!isQuestionPage() || !currentCourseId || currentCourseId !== eventCourseId) return;
      await updatePage("Ngân hàng câu hỏi của môn học vừa thay đổi.");
      return;
    }

    if (kind === "rubrics") return;

    if (!root) return;
    const currentExamId = Number(root?.dataset.examId);
    if (currentExamId && currentExamId !== Number(event?.examId)) return;
    await updatePage("Phiên thi vừa thay đổi.");
  });

  // AI chấm xong hoặc đang chấm một bài: grading-live.js quyết định tải lại hay chỉ báo.
  connection.on("GradingChanged", event => window.aivesGradingLive?.apply(event, root, dirty));

  connection.on("ExamListChanged", async () => {
    if (!/^\/(Classes|ExamSessions)(?:\/|$)/i.test(location.pathname)) return;
    await updatePage("Danh sách lớp vừa thay đổi.");
  });

  const join = async () => {
    const courseId = Number(root?.dataset.courseId);
    const examId = Number(root?.dataset.examId);
    if (courseId > 0) await connection.invoke("JoinCourse", courseId);
    else if (examId > 0) await connection.invoke("JoinExam", examId);
  };

  connection.onreconnected(async () => {
    await join().catch(() => {});
    if (root) await updatePage("Kết nối realtime đã được khôi phục.");
  });

  const start = async () => {
    try { await connection.start(); await join(); }
    catch { setTimeout(start, 5000); }
  };
  start();
});
