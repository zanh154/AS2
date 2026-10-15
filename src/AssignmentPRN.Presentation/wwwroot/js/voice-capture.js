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
  const active = () => recorder?.state === "recording" || requesting || stopping;
  const release = () => { stream?.getTracks().forEach(track => { track.onended = null; track.stop(); }); stream = null; };
  const buttons = () => {
    record.disabled = !question || active() || reading || uploading || !!pending;
    read.disabled = !question || active() || reading || uploading;
    stop.disabled = !active();
    retry.disabled = !pending || uploading;
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
      if (pending === snapshot) pending = null;
      status.textContent = "Đã lưu bản thu trên máy chủ.";
      root.dispatchEvent(new CustomEvent("voice-audio-saved", { detail: result }));
    } catch (error) { failure = error; status.textContent = `${error.message} Bản thu còn trên trang; bấm Thử lưu lại hoặc tải xuống trước khi rời trang.`; }
    finally { uploading = false; buttons(); }
  };
  const stopRecording = async (save = true) => {
    if (requesting) throw new Error("Đang chờ quyền microphone.");
    if (recorder?.state === "recording") { stopping = true; adapter.stop(); recorder.stop(); }
    if (stopping) await stopPromise;
    if (save) await upload();
    if (failure || (save && pending)) throw failure || new Error("Bản thu chưa lưu. Hãy thử lưu lại.");
  };
  const stopReading = () => { window.speechSynthesis?.cancel(); reading = false; buttons(); };
  read.addEventListener("click", () => {
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) { status.textContent = "Trình duyệt chưa hỗ trợ đọc tự động; hãy đọc câu hỏi trên màn hình."; return; }
    if (!question || active()) return;
    const utterance = new SpeechSynthesisUtterance(question.text);
    utterance.lang = "vi-VN";
    const voice = speechSynthesis.getVoices().find(item => item.lang.toLowerCase().startsWith("vi"));
    if (voice) utterance.voice = voice;
    reading = true; buttons();
    utterance.onend = utterance.onerror = () => { reading = false; buttons(); };
    speechSynthesis.speak(utterance);
  });
  record.addEventListener("click", async () => {
    if (!question || active() || pending) return;
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
      recorder = new MediaRecorder(stream, { mimeType: mime, audioBitsPerSecond: 48000 });
      stopPromise = new Promise(resolve => { stopResolve = resolve; });
      recorder.ondataavailable = event => {
        if (event.data.size) chunks.push(event.data);
        if (chunks.reduce((sum, chunk) => sum + chunk.size, 0) >= 11 * 1024 * 1024) stopRecording().catch(error => { status.textContent = error.message; });
      };
      recorder.onstop = () => {
        adapter.stop();
        const duration = Math.max(0.01, (performance.now() - started) / 1000);
        const blob = new Blob(chunks, { type: mime });
        pending = blob.size ? { id, duration, blob } : null;
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        previewUrl = URL.createObjectURL(blob); preview.src = previewUrl; preview.hidden = false;
        const download = byId("voice-download-local");
        download.href = previewUrl; download.download = `voice-${id}.${mime.startsWith("audio/mp4") ? "m4a" : mime.startsWith("audio/ogg") ? "ogg" : "webm"}`; download.hidden = false;
        stopping = false; release(); buttons(); stopResolve();
      };
      recorder.onerror = () => { status.textContent = "Thiết bị thu âm gặp lỗi; hãy kiểm tra microphone."; stopRecording().catch(() => {}); };
      stream.getTracks().forEach(track => track.onended = () => { status.textContent = "Microphone bị ngắt; đang lưu phần đã thu."; stopRecording().catch(() => {}); });
      recorder.start(1000);
      const prefix = text.value.trim();
      adapter.start({ onText: value => { text.value = [prefix, value].filter(Boolean).join(" ").slice(0, 16000); text.dispatchEvent(new Event("input")); },
        onError: message => { byId("voice-stt-status").textContent = message; } });
      status.textContent = "Đang thu âm. Bấm Dừng để lưu hoặc Nộp để kết thúc câu trả lời.";
    } catch (error) {
      release();
      status.textContent = ({ NotAllowedError: "Bạn chưa cho phép microphone. Hãy cấp quyền và thử lại.",
        NotFoundError: "Không tìm thấy microphone.", NotReadableError: "Microphone đang bận hoặc không đọc được." })[error.name] || error.message;
    } finally { requesting = false; buttons(); }
  });
  stop.addEventListener("click", () => stopRecording().catch(error => { status.textContent = error.message; }));
  retry.addEventListener("click", upload);
  window.addEventListener("online", upload);
  window.addEventListener("beforeunload", event => { if (active() || pending) { event.preventDefault(); event.returnValue = ""; } });
  window.addEventListener("pagehide", () => { adapter.stop(); release(); stopReading(); });
  setInterval(() => {
    if (question && recorder?.state === "recording" && Date.now() >= question.captureDeadline)
      stopRecording().catch(error => { status.textContent = error.message; });
  }, 200);
  window.voiceCapture = {
    beforeSubmit: () => stopRecording(true),
    get busy() { return active() || uploading; },
    setState(state) {
      if (question?.examQuestionId !== state.question?.examQuestionId) {
        stopReading(); adapter.stop();
        if (active()) stopRecording(false).catch(() => {});
      }
      question = state.question;
      if (question) question.captureDeadline = Date.now() + Math.max(0, Date.parse(question.deadline) - Date.parse(state.serverNow) - 750);
      const link = byId("voice-stored-audio");
      link.hidden = !question?.hasAudio;
      if (question?.hasAudio) { const url = new URL(root.dataset.audio, location.href); url.searchParams.set("questionId", question.examQuestionId); link.href = url; }
      byId("voice-capture").hidden = state.completed && !pending;
      buttons();
    }
  };
  buttons();
})();
