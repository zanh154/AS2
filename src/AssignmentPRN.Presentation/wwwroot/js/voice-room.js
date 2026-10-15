(() => {
  "use strict";
  const root = document.getElementById("voice-room");
  if (!root) return;
  const status = document.getElementById("voice-status");
  const transcript = document.getElementById("voice-transcript");
  const form = document.getElementById("voice-answer");
  const saved = document.getElementById("voice-save-status");
  const token = form.querySelector('[name="__RequestVerificationToken"]').value;
  let state, offset = 0, busy = false, dirty = false;
  const sync = async (answer = null) => {
    if (busy) return;
    busy = true;
    try {
      const response = await fetch(root.dataset.sync, { method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
        body: JSON.stringify(answer), signal: AbortSignal.timeout(15000) });
      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.error || "Không thể đồng bộ. Kiểm tra đăng nhập/kết nối rồi thử lại.");
      }
      const next = await response.json();
      offset = Date.parse(next.serverNow) - Date.now();
      if (state?.question?.examQuestionId !== next.question?.examQuestionId) {
        transcript.value = next.question?.transcript || "";
        dirty = false;
      } else if (answer) { dirty = false; saved.textContent = "Đã lưu trên máy chủ."; }
      state = next;
      window.voiceCapture?.setState(next);
      status.textContent = next.completed ? "Đã hoàn thành lượt thi." : `Câu ${next.completedCount + 1}/${next.totalCount}`;
      document.getElementById("voice-question").textContent = next.question?.text || "Cảm ơn bạn đã hoàn thành bài thi.";
      form.hidden = next.completed;
    } catch (error) { status.textContent = error.message; }
    finally { busy = false; }
  };
  const save = (submit) => state?.question && sync({ examQuestionId: state.question.examQuestionId,
    transcript: transcript.value, submit });
  transcript.addEventListener("input", () => { dirty = true; saved.textContent = "Có nội dung chưa lưu."; });
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (busy) return;
    try { await window.voiceCapture?.beforeSubmit(); await save(true); }
    catch (error) { status.textContent = error.message; }
  });
  root.addEventListener("voice-audio-saved", () => { saved.textContent = "Đã lưu bản thu trên máy chủ."; });
  document.getElementById("voice-save").addEventListener("click", () => save(false));
  document.getElementById("voice-retry").addEventListener("click", () => dirty ? save(false) : sync());
  window.addEventListener("online", () => dirty ? save(false) : sync());
  window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });
  setInterval(() => {
    if (!state?.question) return;
    const left = Math.max(0, Math.ceil((Date.parse(state.question.deadline) - Date.now() - offset) / 1000));
    document.getElementById("voice-clock").textContent = `${Math.floor(left / 60)}:${String(left % 60).padStart(2, "0")}`;
    if (!left) sync();
  }, 500);
  setInterval(() => { if (dirty) save(false); else if (!state?.completed) sync(); }, 5000);
  sync();
})();
