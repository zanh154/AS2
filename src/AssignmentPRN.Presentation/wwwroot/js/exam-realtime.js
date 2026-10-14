document.addEventListener("DOMContentLoaded", () => {
  if (!window.signalR) return;
  const root = document.querySelector("[data-live-page]");
  const connection = new signalR.HubConnectionBuilder().withUrl("/hubs/exam-updates").withAutomaticReconnect().build();
  let busy = false;
  let dirty = false;
  root?.addEventListener("input", event => { if (event.target.closest("form")) dirty = true; });
  root?.addEventListener("change", event => { if (event.target.closest("form")) dirty = true; });
  const refresh = async () => {
    if (!root || busy || dirty) return;
    const snapshot = root.dataset.snapshotUrl;
    if (!snapshot || !/^(\/(ExamSessions|Courses)(\/Index)?\/?|\/ExamSessions\/Details(?:\/\d+)?)$/i.test(location.pathname)) return;
    busy = true;
    try { const response = await fetch(snapshot, { headers: { "X-Requested-With": "XMLHttpRequest" } }); if (response.ok && !response.redirected) {
      const html = await response.text();
      if (root.querySelector("[data-live-region]")) {
        const fragment = new DOMParser().parseFromString(html, "text/html");
        root.querySelectorAll("[data-live-region]").forEach(region => {
          const incoming = fragment.querySelector(`[data-live-region="${region.dataset.liveRegion}"]`);
          if (incoming) { region.replaceWith(incoming); window.initAivesDatePickers?.(incoming); }
        });
      } else root.innerHTML = html;
      root.dispatchEvent(new Event("live-content-updated"));
    } }
    finally { busy = false; }
  };
  connection.on("ExamChanged", async event => {
    const current = Number(root?.dataset.examId);
    if (current && current !== event.examId) return;
    await refresh().catch(() => {});
    const notice = document.getElementById("realtime-notice");
    if (notice && !/^\/(ExamSessions|Courses)(\/Index)?\/?$/i.test(location.pathname)) { notice.hidden = false; document.getElementById("realtime-notice-message").textContent = "Phiên thi vừa thay đổi. Cập nhật để xem dữ liệu mới."; }
  });
  connection.on("ExamListChanged", () => {
    const notice = document.getElementById("realtime-notice");
    if (notice) { notice.hidden = false; document.getElementById("realtime-notice-message").textContent = "Danh sách lớp vừa thay đổi."; }
  });
  const join = async () => { const id = Number(root?.dataset.examId); if (id > 0) await connection.invoke("JoinExam", id); };
  connection.onreconnected(async () => { await join().catch(() => {}); await refresh().catch(() => {}); });
  const start = async () => { try { await connection.start(); await join(); } catch { setTimeout(start, 5000); } };
  start();
});
