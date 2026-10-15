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

  root?.addEventListener("input", event => { if (event.target.closest("form")) dirty = true; });
  root?.addEventListener("change", event => { if (event.target.closest("form")) dirty = true; });

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
    if (!root || busy || dirty || !isRefreshablePage()) return;
    const snapshot = root.dataset.snapshotUrl;
    if (!snapshot) return;

    busy = true;
    try {
      const response = await fetch(snapshot, { headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!response.ok || response.redirected) return;

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
    } finally {
      busy = false;
    }
  };

  connection.on("ExamChanged", async event => {
    const kind = String(event?.kind ?? "");

    if (kind === "questions") {
      const currentCourseId = Number(root?.dataset.courseId);
      const eventCourseId = Number(event?.courseId);
      if (!isQuestionPage() || !currentCourseId || currentCourseId !== eventCourseId) return;
      await refresh().catch(() => {});
      showNotice("Ngân hàng câu hỏi của môn học vừa thay đổi. Dữ liệu đã được cập nhật.");
      return;
    }

    if (kind === "rubrics") return;

    const currentExamId = Number(root?.dataset.examId);
    if (currentExamId && currentExamId !== Number(event?.examId)) return;
    await refresh().catch(() => {});
    if (!/^\/(ExamSessions|Courses)(\/Index)?\/?$/i.test(location.pathname)) {
      showNotice("Phiên thi vừa thay đổi. Cập nhật để xem dữ liệu mới.");
    }
  });

  connection.on("ExamListChanged", () => showNotice("Danh sách lớp vừa thay đổi."));

  const join = async () => {
    const courseId = Number(root?.dataset.courseId);
    const examId = Number(root?.dataset.examId);
    if (courseId > 0) await connection.invoke("JoinCourse", courseId);
    else if (examId > 0) await connection.invoke("JoinExam", examId);
  };

  connection.onreconnected(async () => {
    await join().catch(() => {});
    await refresh().catch(() => {});
  });

  const start = async () => {
    try { await connection.start(); await join(); }
    catch { setTimeout(start, 5000); }
  };
  start();
});
