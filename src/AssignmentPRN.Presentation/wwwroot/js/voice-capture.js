(() => {
  "use strict";
  const root = document.getElementById("voice-room");
  if (!root) return;
  const byId = id => document.getElementById(id);
  const status = byId("voice-capture-status"), record = byId("voice-record"), stop = byId("voice-stop");
  const retry = byId("voice-upload"), read = byId("voice-read"), preview = byId("voice-preview");
  const text = byId("voice-transcript"), adapter = window.createVoiceSpeechAdapter();
  let question, recorder, stream, chunks = [], pending, previewUrl, started, stopPromise, stopResolve;
  let reading = false, requesting = false, uploading = false, failure;
  let stopping = false;
  let recoveryId, recovered = false, stateVersion = 0;
  const recordings = new Map();
  const renderRecovery = item => {
    recordings.set(item.id, item);
    let row = document.getElementById(`recovery-${item.id}`);
    if (!row) {
      row = document.createElement("p"); row.id = `recovery-${item.id}`;
      byId("voice-local-recordings").append(row);
    }
    if (row.dataset.url) URL.revokeObjectURL(row.dataset.url);
    row.replaceChildren();
    const url = URL.createObjectURL(item.blob); row.dataset.url = url;
    const download = document.createElement("a"); download.href = url; download.download = `voice-${item.questionId}`;
    download.textContent = `Tải bản thu câu ${item.questionId}${item.synced ? " (đã lưu máy chủ)" : " (chưa xác nhận lưu)"}`;
    row.append(download);
    if (!item.synced) {
      const button = document.createElement("button"); button.type = "button"; button.className = "btn btn-outline-secondary";
      button.textContent = "Thử gửi bản thu này";
      button.addEventListener("click", () => {
        if (active() || uploading) return;
        pending = { id: item.questionId, blob: item.blob, duration: item.duration, recoveryId: item.id };
        upload();
      });
      row.append(button);
    }
  };
  const active = () => recorder?.state === "recording" || requesting || stopping;
  const release = () => { stream?.getTracks().forEach(track => { track.onended = null; track.stop(); }); stream = null; };
  const buttons = () => {
    root.dataset.captureState = uploading ? "saving" : stopping ? "processing" : recorder?.state === "recording" ? "recording" : requesting ? "permission" : reading ? "reading" : "ready";
    text.readOnly = active();
    record.disabled = !question || active() || reading || uploading || pending?.id === question.examQuestionId;
    read.disabled = !question || active() || uploading;
    read.textContent = reading ? "Dừng đọc" : "Đọc câu hỏi";
    stop.disabled = !active() || requesting || stopping;
    retry.disabled = !pending || uploading;
    const speechRetry = byId("voice-stt-retry");
    if (speechRetry) speechRetry.disabled = recorder?.state !== "recording" || stopping;
    const badge = byId("voice-record-state");
    if (badge) badge.textContent = ({ recording: "Đang thu âm", saving: "Đang lưu", processing: "Đang xử lý", permission: "Chờ quyền mic", reading: "Đang đọc câu hỏi", ready: "Sẵn sàng" })[root.dataset.captureState];
  };
  const upload = async () => {
    if (!pending || uploading) return;
    uploading = true; failure = null; buttons();
    status.textContent = "Đang lưu bản thu…";
    const snapshot = pending;
    try {
      const data = new FormData();
      data.append("questionId", snapshot.id);
      data.append("durationSeconds", snapshot.duration);
      data.append("audio", snapshot.blob, "recording");
      const response = await fetch(root.dataset.upload, { method: "POST", body: data, credentials: "same-origin",
        headers: { RequestVerificationToken: root.querySelector('[name="__RequestVerificationToken"]').value },
        signal: AbortSignal.timeout(30000) });
      const result = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(result.error || "Lưu âm thanh thất bại.");
      const acknowledged = { id: snapshot.recoveryId, questionId: snapshot.id, kind: "audio",
        blob: snapshot.blob, duration: snapshot.duration, synced: true };
      if (snapshot.recoveryId) { await window.voiceRecovery?.put(acknowledged); renderRecovery(acknowledged); }
      if (pending === snapshot) pending = null;
      status.textContent = "Đã lưu bản thu trên máy chủ.";
      root.dispatchEvent(new CustomEvent("voice-audio-saved", { detail: result }));
    } catch (error) { failure = error; status.textContent = `${error.message} Bản thu còn trên trang; bấm Thử lưu lại hoặc tải xuống trước khi rời trang.`; }
    finally { uploading = false; buttons(); }
  };
  const stopRecording = async (save = true) => {
    const targetId = question?.examQuestionId;
    if (requesting) throw new Error("Đang chờ quyền microphone.");
    const speechStopped = Promise.resolve(adapter.stop());
    if (recorder?.state === "recording") { stopping = true; recorder.stop(); buttons(); }
    if (stopping) await stopPromise;
    await speechStopped;
    root.dispatchEvent(new CustomEvent("voice-draft-ready", { detail: { questionId: targetId } }));
    if (save && pending?.id === targetId) {
      await upload();
      if (failure || pending?.id === targetId) throw failure || new Error("Bản thu chưa lưu. Hãy thử lưu lại.");
    }
  };
  const stopReading = () => { window.speechSynthesis?.cancel(); reading = false; buttons(); };
  read.addEventListener("click", () => {
    if (reading) { stopReading(); return; }
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) { status.textContent = "Trình duyệt chưa hỗ trợ đọc tự động; hãy đọc câu hỏi trên màn hình."; return; }
    if (!question || active()) return;
    const utterance = new SpeechSynthesisUtterance(question.text);
    utterance.lang = "vi-VN";
    const voice = speechSynthesis.getVoices().find(item => item.lang.toLowerCase().startsWith("vi"));
    if (voice) utterance.voice = voice;
    reading = true; buttons();
    utterance.onend = () => { reading = false; buttons(); };
    utterance.onerror = () => { reading = false; status.textContent = "Không đọc được tự động. Bạn có thể đọc câu hỏi trên màn hình."; buttons(); };
    speechSynthesis.speak(utterance);
  });
  record.addEventListener("click", async () => {
    if (!question || active() || pending?.id === question.examQuestionId) return;
    if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) { status.textContent = "Cần HTTPS/localhost và trình duyệt hỗ trợ MediaRecorder. Bạn vẫn có thể nhập nội dung trả lời."; return; }
    const id = question.examQuestionId;
    requesting = true; buttons(); stopReading();
    status.textContent = "Đang chờ bạn cho phép microphone…";
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (question?.examQuestionId !== id) { release(); return; }
      const mime = ["audio/webm;codecs=opus", "audio/ogg;codecs=opus", "audio/mp4"].find(t => MediaRecorder.isTypeSupported(t));
      if (!mime) throw new Error("Trình duyệt không có định dạng thu âm phù hợp.");
      chunks = []; started = performance.now(); failure = null;
      recoveryId = `audio/${id}/${Date.now()}-${Math.random().toString(36).slice(2)}`;
      recorder = new MediaRecorder(stream, { mimeType: mime, audioBitsPerSecond: 48000 });
      stopPromise = new Promise(resolve => { stopResolve = resolve; });
      recorder.ondataavailable = event => {
        if (event.data.size) chunks.push(event.data);
        const local = { id: recoveryId, questionId: id, kind: "audio", blob: new Blob(chunks, { type: mime }),
          duration: Math.max(0.01, (performance.now() - started) / 1000), synced: false };
        recordings.set(local.id, local);
        window.voiceRecovery?.put(local);
        if (chunks.reduce((sum, chunk) => sum + chunk.size, 0) >= 11 * 1024 * 1024) stopRecording().catch(error => { status.textContent = error.message; });
      };
      recorder.onstop = async () => {
        const duration = Math.max(0.01, (performance.now() - started) / 1000);
        await Promise.resolve(adapter.stop());
        const blob = new Blob(chunks, { type: mime });
        pending = blob.size ? { id, duration, blob, recoveryId } : null;
        if (blob.size) {
          const local = { id: recoveryId, questionId: id, kind: "audio", blob, duration, synced: false };
          await window.voiceRecovery?.put(local); renderRecovery(local);
        }
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        previewUrl = URL.createObjectURL(blob); preview.src = previewUrl; preview.hidden = false;
        const download = byId("voice-download-local");
        download.href = previewUrl; download.download = `voice-${id}.${mime.startsWith("audio/mp4") ? "m4a" : mime.startsWith("audio/ogg") ? "ogg" : "webm"}`; download.hidden = false;
        stopping = false; release(); buttons(); stopResolve();
      };
      recorder.onerror = () => { status.textContent = "Thiết bị thu âm gặp lỗi; hãy kiểm tra microphone."; stopRecording().catch(() => {}); };
      stream.getTracks().forEach(track => track.onended = () => { status.textContent = "Microphone bị ngắt; đang lưu phần đã thu."; stopRecording().catch(() => {}); });
      recorder.start(1000);
      startSpeech(id);
      status.textContent = "Đang thu âm. Bấm Dừng để lưu hoặc Nộp để kết thúc câu trả lời.";
    } catch (error) {
      release();
      status.textContent = ({ NotAllowedError: "Bạn chưa cho phép microphone. Hãy cấp quyền và thử lại.",
        NotFoundError: "Không tìm thấy microphone.", NotReadableError: "Microphone đang bận hoặc không đọc được." })[error.name] || error.message;
    } finally { requesting = false; buttons(); }
  });
  const startSpeech = id => {
      const prefix = text.value.trim();
      adapter.start({ onText: value => {
        if (question?.examQuestionId !== id) return;
        text.value = [prefix, value].filter(Boolean).join(" ").slice(0, 16000); text.dispatchEvent(new Event("input")); },
        onError: message => { if (question?.examQuestionId === id) byId("voice-stt-status").textContent = message; },
        onState: (state, message) => {
          if (question?.examQuestionId !== id) return;
          root.dataset.speechState = state;
          byId("voice-stt-status").textContent = message;
          const label = byId("voice-stt-badge");
          if (label) label.textContent = ({ starting: "Đang kết nối", listening: "Đang nghe", recognized: "Đã nhận văn bản", processing: "Đang hoàn tất", ready: "Đã nhận văn bản", empty: "Chưa có văn bản", error: "Cần kiểm tra", unsupported: "Chưa hỗ trợ", reconnecting: "Đang nối lại" })[state];
        } });
  };
  byId("voice-stt-retry")?.addEventListener("click", async () => {
    if (recorder?.state !== "recording" || stopping) return;
    const id = question.examQuestionId;
    await Promise.resolve(adapter.stop());
    if (question?.examQuestionId === id && recorder?.state === "recording") startSpeech(id);
  });
  stop.addEventListener("click", () => stopRecording().catch(error => { status.textContent = error.message; }));
  retry.addEventListener("click", upload);
  window.addEventListener("online", upload);
  window.addEventListener("beforeunload", event => { if (active() || pending) { event.preventDefault(); event.returnValue = ""; } });
  window.addEventListener("pagehide", () => { adapter.stop(); release(); stopReading(); });
  setInterval(() => {
    const elapsed = byId("voice-record-time");
    if (elapsed && recorder?.state === "recording") {
      const seconds = Math.floor((performance.now() - started) / 1000);
      elapsed.textContent = `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
    }
    if (question && recorder?.state === "recording" && Date.now() >= question.captureDeadline)
      stopRecording().catch(error => { status.textContent = error.message; });
  }, 200);
  window.voiceCapture = {
    beforeSubmit: () => stopRecording(true),
    get busy() { return active() || uploading; },
    async setState(state) {
      const snapshotVersion = ++stateVersion;
      if (!recovered) {
        recovered = true;
        for (const item of await window.voiceRecovery?.list() || []) {
          if (item.kind !== "audio" || !item.blob?.size) continue;
          renderRecovery(item);
          if (!item.synced && item.questionId === state.question?.examQuestionId)
            pending = { id: item.questionId, blob: item.blob, duration: item.duration, recoveryId: item.id };
        }
      }
      if (snapshotVersion !== stateVersion) return;
      if (question?.examQuestionId !== state.question?.examQuestionId) {
        stopReading(); adapter.stop();
        if (active()) stopRecording(false).catch(() => {});
      }
      question = state.question;
      if (question) question.captureDeadline = Date.now() + Math.max(0, Date.parse(question.deadline) - Date.parse(state.serverNow) - 750);
      const link = byId("voice-stored-audio");
      link.hidden = !question?.hasAudio;
      if (question?.hasAudio) { const url = new URL(root.dataset.audio, location.href); url.searchParams.set("questionId", question.examQuestionId); link.href = url; }
      byId("voice-capture").hidden = state.completed && !pending && !recordings.size;
      buttons();
    }
  };
  buttons();
})();
