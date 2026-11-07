// Presentation only: reuse the existing capture, draft and realtime state.
(() => {
  "use strict";
  const root = document.getElementById("voice-room");
  if (!root) return;
  const byId = id => document.getElementById(id);
  const header = root.querySelector(".voice-room__header");
  const grid = root.querySelector(".voice-room__grid");
  const capture = byId("voice-capture"), form = byId("voice-answer");
  const actions = root.querySelector(".voice-form-actions");
  const storage = root.querySelector(".voice-storage-details");
  const history = root.querySelector(".voice-history-details");
  const metadata = document.createElement("div");
  metadata.className = "voice-room__metadata";
  metadata.append(root.querySelector(".voice-room__progress"), root.querySelector(".voice-clockbox"));
  header.insertBefore(metadata, root.querySelector(".voice-room__connection"));
  // Keep reading and keyboard order consistent with the single-column layout.
  byId("voice-submit").setAttribute("form", form.id);
  grid.append(root.querySelector(".voice-question-panel"), capture, form, actions, storage);
  let permission = "prompt";
  const label = (node, value) => { if (node.textContent !== value) node.textContent = value; };
  const isSaved = id => byId(id).textContent === "Đã lưu tạm";
  const update = () => {
    const phase = root.dataset.captureState;
    const speech = root.dataset.speechState;
    const completed = root.dataset.completed === "true";
    const busy = ["recording", "permission", "processing", "saving", "reading"].includes(phase);
    const hasText = !!byId("voice-transcript").value.trim();
    const hasAudio = isSaved("voice-audio-badge");
    const readyToSubmit = (hasText || hasAudio) && !busy && !window.voiceCapture?.hasPendingAudio;
    const record = byId("voice-record"), stop = byId("voice-stop"), submit = byId("voice-submit");
    record.hidden = ["recording", "permission", "processing", "saving"].includes(phase) || completed;
    stop.hidden = !["recording", "permission", "processing", "saving"].includes(phase) || completed;
    stop.classList.add("btn-primary");
    stop.classList.toggle("btn-outline-secondary", false);
    label(stop, phase === "saving" ? "Đang lưu bản thu…" : phase === "processing" ? "Đang nhận dạng…" : phase === "permission" ? "Chờ quyền mic…" : "Dừng thu âm");
    label(record, permission === "granted" || hasAudio ? "Bắt đầu trả lời" : "Cho phép mic");
    record.classList.toggle("btn-primary", !readyToSubmit);
    record.classList.toggle("btn-outline-primary", readyToSubmit);
    submit.hidden = !readyToSubmit || completed;
    actions.hidden = completed;
    const saveDisabled = busy || completed || (!hasText && !hasAudio);
    if (byId("voice-save").disabled !== saveDisabled) byId("voice-save").disabled = saveDisabled;
    label(byId("voice-read"), phase === "reading" ? "Dừng đọc" : "Nghe câu hỏi");
    const speechIssue = ["error", "unsupported", "empty", "reconnecting"].includes(speech);
    root.querySelector(".voice-speech-note").hidden = !speechIssue;
    byId("voice-stt-retry").hidden = !speechIssue || byId("voice-stt-retry").disabled || speech === "unsupported";
    const captureMessage = byId("voice-capture-status").textContent;
    // Routine messages duplicate the activity label; error and recovery messages stay visible.
    byId("voice-capture-status").hidden = !captureMessage || /^(Đang lưu bản thu…|Đã lưu bản thu trên máy chủ\.|Đang chờ bạn cho phép microphone…|Đang thu âm\.)/.test(captureMessage);
    const connection = byId("voice-connection").textContent;
    const connectionIssue = !/^(Đã kết nối trực tiếp\.|Đã kết nối lại\.)$/.test(connection);
    root.querySelector(".voice-room__connection").hidden = !connectionIssue;
    byId("voice-retry").hidden = !connectionIssue && /^Câu \d+\/\d+$/.test(byId("voice-status").textContent);
    // Successful reconnect must not hide a failed HTTP save/sync.
    const syncIssue = !/^(Câu \d+\/\d+|Đã hoàn thành lượt thi\.)$/.test(byId("voice-status").textContent);
    if (syncIssue) { root.querySelector(".voice-room__connection").hidden = false; byId("voice-retry").hidden = false; }
    root.querySelector(".voice-backups").hidden = Number(byId("voice-backup-count").textContent) === 0;
    const localError = root.dataset.localState === "error";
    const draftMessage = byId("voice-save-status").textContent;
    const recoveryMessage = /khôi phục/.test(draftMessage);
    const textSaved = isSaved("voice-text-badge");
    label(byId("voice-draft-summary"), localError ? "Lỗi lưu dự phòng · xem chi tiết" : recoveryMessage ? draftMessage :
      window.voiceCapture?.hasPendingAudio ? "Bản thu chưa gửi · thử lưu lại" :
      hasText && !textSaved ? "Văn bản chờ lưu lên máy chủ" :
      textSaved && hasAudio ? "Đã lưu audio và văn bản trên máy chủ" :
      hasAudio ? "Đã lưu audio trên máy chủ" : textSaved ? "Đã lưu văn bản trên máy chủ" :
      completed ? "Đã nộp bài" : "Chưa có bản nháp · tự lưu khi trả lời");
    if (localError) storage.open = true;
    if (completed) history.open = true;
    const activity = byId("voice-record-state");
    if (phase === "ready") label(activity, completed ? "Đã hoàn thành" : speechIssue ? "Kiểm tra nhận dạng" :
      hasText && !textSaved ? "Chờ lưu" : textSaved || hasAudio ? "Đã lưu" : "Sẵn sàng");
  };
  new MutationObserver(update).observe(root, { subtree: true, childList: true, characterData: true,
    attributes: true, attributeFilter: ["disabled", "data-capture-state", "data-speech-state", "data-completed", "data-local-state"] });
  byId("voice-transcript").addEventListener("input", update);
  if (navigator.permissions?.query) navigator.permissions.query({ name: "microphone" }).then(result => {
    permission = result.state; result.addEventListener("change", () => { permission = result.state; update(); }); update();
  }).catch(() => {});
  update();
})();
