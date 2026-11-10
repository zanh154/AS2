(() => {
  "use strict";
  const root = document.getElementById("voice-room");
  if (!root) return;
  const byId = id => document.getElementById(id);
  const status = byId("voice-capture-status"), record = byId("voice-record"), stop = byId("voice-stop");
  const retry = byId("voice-upload"), read = byId("voice-read"), preview = byId("voice-preview");
  const text = byId("voice-transcript"), adapter = window.createVoiceSpeechAdapter();
  let question, recorder, stream, chunks = [], pending, previewUrl, started, stopPromise, stopResolve;
  let reading = false, requesting = false, uploading = false, failure, uploadTask;
  let stopping = false;
  let recoveryId, recovered = false, stateVersion = 0;
  const recordings = new Map();
  const renderRecovery = item => {
    recordings.set(item.id, item);
    const count = byId("voice-backup-count"); if (count) count.textContent = recordings.size;
    let row = document.getElementById(`recovery-${item.id}`);
    if (!row) {
      row = document.createElement("p"); row.id = `recovery-${item.id}`;
      byId("voice-local-recordings").append(row);
    }
    if (row.dataset.url) URL.revokeObjectURL(row.dataset.url);
    row.replaceChildren();
    const url = URL.createObjectURL(item.blob); row.dataset.url = url;
    const download = document.createElement("a"); download.href = url; download.download = `voice-${item.questionId}`;
    download.textContent = `Tải bản thu dự phòng${item.synced ? " · Đã lưu máy chủ" : " · Chưa gửi thành công"}`;
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
    retry.hidden = !pending;
    const speechRetry = byId("voice-stt-retry");
    if (speechRetry) speechRetry.disabled = recorder?.state !== "recording" || stopping;
    const badge = byId("voice-record-state");
    if (badge) badge.textContent = ({ recording: "Đang thu âm", saving: "Đang lưu", processing: "Đang xử lý", permission: "Chờ quyền mic", reading: "Đang đọc câu hỏi", ready: "Sẵn sàng" })[root.dataset.captureState];
    root.dispatchEvent(new CustomEvent("voice-capture-state"));
  };
  const upload = () => {
    if (uploading) return uploadTask;
    if (!pending) return Promise.resolve();
    uploading = true; failure = null; buttons();
    status.textContent = "Đang lưu bản thu…";
    const snapshot = pending;
    uploadTask = (async () => {
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
    } catch (error) {
      failure = error; status.textContent = `${error.message} Bản thu còn trên trang; bấm Thử lưu lại hoặc tải xuống trước khi rời trang.`;
      const badge = byId("voice-audio-badge"); if (badge) badge.textContent = "Chờ gửi lại";
    }
    finally { uploading = false; buttons(); }
    })();
    return uploadTask;
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
  const detectLanguage = str => {
    if (!str || typeof str !== "string") return "vi-VN";
    const viDiacritics = /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđĐ]/i;
    if (viDiacritics.test(str)) return "vi-VN";
    const enKeywords = /\b(what|why|how|when|where|which|who|explain|describe|define|difference|between|compare|list|illustrate|advantages|disadvantages|purpose)\b/i;
    if (enKeywords.test(str)) return "en-US";
    const enCommon = /\b(the|is|are|in|of|to|with|for|and|that|this|it|an|as|by|from)\b/i;
    if (enCommon.test(str)) return "en-US";
    const viKeywords = /\b(cho|biet|neu|cau|hoi|tai|sao|hay|viet|bang|khoa)\b/i;
    if (viKeywords.test(str)) return "vi-VN";
    return "vi-VN";
  };
  let cachedVoices = [];
  const loadVoices = () => {
    if (typeof window.speechSynthesis === "undefined") return;
    const list = window.speechSynthesis.getVoices?.();
    if (list && list.length) cachedVoices = list;
  };
  loadVoices();
  if (typeof window.speechSynthesis !== "undefined" && window.speechSynthesis.onvoiceschanged !== undefined) {
    window.speechSynthesis.onvoiceschanged = loadVoices;
  }
  const pickVoice = lang => {
    loadVoices();
    const voices = cachedVoices.length ? cachedVoices : (window.speechSynthesis?.getVoices?.() || []);
    if (!voices.length) return null;
    const isVi = lang.toLowerCase().startsWith("vi");
    if (isVi) {
      const naturalVi = voices.find(v => v.lang?.toLowerCase().startsWith("vi") && /natural|online|hoaimy|namminh|an\b|google/i.test(v.name));
      if (naturalVi) return naturalVi;
      const anyVi = voices.find(v => v.lang?.toLowerCase().startsWith("vi") || /vietnam|tiếng việt/i.test(v.name));
      if (anyVi) return anyVi;
      return null;
    } else {
      const naturalEn = voices.find(v => v.lang?.toLowerCase().startsWith("en") && /natural|online|jenny|guy|aria|google/i.test(v.name));
      if (naturalEn) return naturalEn;
      const anyEn = voices.find(v => v.lang?.toLowerCase().startsWith("en"));
      if (anyEn) return anyEn;
      return null;
    }
  };
  let finishReading = null;
  const stopReading = () => {
    window.speechSynthesis?.cancel(); reading = false;
    finishReading?.(false); finishReading = null; buttons();
  };
  // Reads the open question aloud. Resolves true when it was read to the end, false when
  // it could not be read or was interrupted, so the virtual examiner knows whether the
  // student actually heard it before the microphone opens.
  const speak = () => new Promise(resolve => {
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) {
      status.textContent = "Trình duyệt chưa hỗ trợ đọc tự động; hãy đọc câu hỏi trên màn hình."; resolve(false); return;
    }
    if (!question || active() || reading) { resolve(false); return; }
    const textToRead = question.text;
    const lang = detectLanguage(textToRead);
    const utterance = new SpeechSynthesisUtterance(textToRead);
    utterance.lang = lang;
    const voice = pickVoice(lang);
    if (voice) {
      utterance.voice = voice;
    }
    reading = true; finishReading = resolve; buttons();
    const done = (ok, message) => {
      if (finishReading !== resolve) return;
      reading = false; finishReading = null;
      if (message) status.textContent = message;
      buttons(); resolve(ok);
    };
    utterance.onend = () => done(true);
    utterance.onerror = event => done(false, event?.error === "interrupted" || event?.error === "canceled" ? ""
      : "Không đọc được tự động. Bạn có thể đọc câu hỏi trên màn hình.");
    speechSynthesis.speak(utterance);
  });
  read.addEventListener("click", () => {
    if (reading) { stopReading(); return; }
    speak();
  });
  const startRecording = async () => {
    if (!question || active() || pending?.id === question.examQuestionId) return;
    if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) { status.textContent = "Cần HTTPS/localhost và trình duyệt hỗ trợ MediaRecorder. Bạn vẫn có thể nhập nội dung trả lời."; return; }
    const id = question.examQuestionId;
    preview.pause?.(); preview.hidden = true;
    requesting = true; buttons(); stopReading();
    status.textContent = "Đang chờ bạn cho phép microphone…";
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (question?.examQuestionId !== id) { release(); return; }
      const mime = ["audio/webm;codecs=opus", "audio/ogg;codecs=opus", "audio/mp4"].find(t => MediaRecorder.isTypeSupported(t));
      if (!mime) throw new Error("Trình duyệt không có định dạng thu âm phù hợp.");
      chunks = []; started = performance.now(); failure = null;
      const timer = byId("voice-record-time"); if (timer) timer.textContent = "00:00";
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
        if (question?.examQuestionId === id) {
          if (previewUrl) URL.revokeObjectURL(previewUrl);
          previewUrl = URL.createObjectURL(blob); preview.src = previewUrl; preview.hidden = false;
          const download = byId("voice-download-local");
          download.href = previewUrl; download.download = `voice-${id}.${mime.startsWith("audio/mp4") ? "m4a" : mime.startsWith("audio/ogg") ? "ogg" : "webm"}`; download.hidden = false;
        }
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
  };
  record.addEventListener("click", startRecording);
  const startSpeech = id => {
      const prefix = text.value.trim();
      const lang = detectLanguage(question?.text);
      adapter.start({ lang, onText: value => {
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
    detectLanguage,
    speak,
    startRecording,
    get reading() { return reading; },
    get recording() { return recorder?.state === "recording" || requesting; },
    beforeSubmit: () => stopRecording(true),
    get busy() { return active() || uploading; },
    get hasPendingAudio() { return pending?.id === question?.examQuestionId && !!pending; },
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
      // A probe shares the examQuestionId of the question it probes, so the follow-up id
      // is part of what makes the open item new.
      const itemOf = q => q ? `${q.examQuestionId}:${q.followUpId || 0}` : null;
      if (itemOf(question) !== itemOf(state.question)) {
        stopReading(); adapter.stop();
        if (active()) stopRecording(false).catch(() => {});
        preview.pause?.(); preview.hidden = true;
        byId("voice-download-local").hidden = true;
        const timer = byId("voice-record-time"); if (timer) timer.textContent = "00:00";
        const badge = byId("voice-stt-badge"); if (badge) badge.textContent = state.question?.transcript ? "Văn bản đã lưu" : "Sẵn sàng nhận dạng";
        root.dataset.speechState = "ready";
      }
      question = state.question;
      if (question) question.captureDeadline = Date.now() + Math.max(0, Date.parse(question.deadline) - Date.parse(state.serverNow) - 750);
      const link = byId("voice-stored-audio");
      link.hidden = !question?.hasAudio;
      if (question?.hasAudio) {
        const url = new URL(root.dataset.audio, location.href); url.searchParams.set("questionId", question.examQuestionId);
        if (question.followUpId) url.searchParams.set("followUpId", question.followUpId);
        link.href = url;
        if (preview.hidden && !active() && pending?.id !== question.examQuestionId) { preview.src = url.href; preview.hidden = false; }
      }
      byId("voice-capture").hidden = state.completed && !pending && !recordings.size;
      buttons();
    }
  };
  buttons();
})();
