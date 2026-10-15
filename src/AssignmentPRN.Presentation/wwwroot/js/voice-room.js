(() => {
  "use strict";
  const root = document.getElementById("voice-room");
  if (!root) return;
  const status = document.getElementById("voice-status");
  const transcript = document.getElementById("voice-transcript");
  const form = document.getElementById("voice-answer");
  const saved = document.getElementById("voice-save-status");
  const token = form.querySelector('[name="__RequestVerificationToken"]').value;
  const connectionStatus = document.getElementById("voice-connection");
  let state, offset = 0, dirty = false, version = 0, pendingSubmit, submitting = false;
  let queue = Promise.resolve(), queued = 0, hub, startingHub = false, closed = false;
  form.hidden = true;
  const localDraft = (questionId, value, synced, submit = false) => window.voiceRecovery?.put({
    id: `text/${questionId}`, questionId, kind: "text", value, synced, submit });
  const paintDraft = () => {
    const count = document.getElementById("voice-transcript-count");
    if (count) count.textContent = `${transcript.value.length.toLocaleString("vi-VN")} / 16.000 ký tự`;
    const textBadge = document.getElementById("voice-text-badge");
    if (textBadge) textBadge.textContent = dirty ? "Chờ lưu thay đổi" : state?.completed ? "Đã nộp" : transcript.value.trim() ? "Đã lưu tạm" : "Chưa có nội dung";
    const audioBadge = document.getElementById("voice-audio-badge");
    if (audioBadge) audioBadge.textContent = window.voiceCapture?.hasPendingAudio ? "Có bản thu chưa gửi" : state?.question?.hasAudio ? "Đã lưu tạm" : state?.completed && state.savedAnswers?.some(x => x.hasAudio) ? "Đã nộp" : "Chưa có bản thu";
  };
  const paintClock = () => {
    const left = state?.question ? Math.max(0, Math.ceil((Date.parse(state.question.deadline) - Date.now() - offset) / 1000)) : 0;
    document.getElementById("voice-clock").textContent = `${String(Math.floor(left / 60)).padStart(2, "0")}:${String(left % 60).padStart(2, "0")}`;
    root.dataset.timeWarning = String(left > 0 && left <= 15);
    return left;
  };
  const sync = (answer = null, requestVersion = version) => {
    // Preserve writes in order; coalesce redundant polls instead of dropping a submit.
    if (!answer && queued) return queue;
    const sentVersion = requestVersion;
    queued++;
    const operation = async () => {
      try {
        const sent = Date.now();
        const response = await fetch(root.dataset.sync, { method: "POST", credentials: "same-origin",
          headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
          body: JSON.stringify(answer), signal: AbortSignal.timeout(15000) });
        if (!response.ok) {
          const error = await response.json().catch(() => ({}));
          throw new Error(error.error || "Không thể đồng bộ. Kiểm tra đăng nhập/kết nối rồi thử lại.");
        }
        const next = await response.json();
        offset = Date.parse(next.serverNow) - (sent + Date.now()) / 2;
        if (state?.question?.examQuestionId !== next.question?.examQuestionId) {
          const initial = !state;
          transcript.value = next.question?.transcript || "";
          dirty = false; version++;
          if (initial && next.question) {
            const recovered = (await window.voiceRecovery?.list() || []).find(x => x.id === `text/${next.question.examQuestionId}`);
            if (recovered && !recovered.synced) {
              transcript.value = recovered.value; dirty = true;
              saved.textContent = "Đã khôi phục nội dung chưa gửi từ trình duyệt. Kiểm tra rồi lưu/nộp lại.";
              if (recovered.submit) pendingSubmit = { examQuestionId: next.question.examQuestionId, transcript: recovered.value, submit: true };
            }
          }
        } else if (answer && sentVersion === version) {
          dirty = false; saved.textContent = "Đã lưu trên máy chủ.";
          await localDraft(answer.examQuestionId, answer.transcript, true);
        }
        if (answer?.submit && pendingSubmit?.examQuestionId === answer.examQuestionId) pendingSubmit = null;
        state = next;
        root.dataset.completed = String(next.completed);
        window.voiceCapture?.setState(next);
        paintDraft(); paintClock();
        const examName = document.getElementById("voice-exam-name"); if (examName && next.examName) examName.textContent = next.examName;
        const questionNumber = document.getElementById("voice-question-number");
        if (questionNumber) questionNumber.textContent = next.completed ? "ĐÃ HOÀN THÀNH" : `CÂU HỎI ${String(next.question?.orderNo || next.completedCount + 1).padStart(2, "0")}`;
        const progress = document.getElementById("voice-progress"); if (progress) progress.value = next.totalCount ? Math.round(next.completedCount / next.totalCount * 100) : 0;
        status.textContent = next.completed ? "Đã hoàn thành lượt thi." : `Câu ${next.completedCount + 1}/${next.totalCount}`;
        document.getElementById("voice-question").textContent = next.question?.text || "Cảm ơn bạn đã hoàn thành bài thi.";
        const history = document.getElementById("voice-history");
        const historyEmpty = document.getElementById("voice-history-empty"); if (historyEmpty) historyEmpty.hidden = !!next.savedAnswers?.length;
        if (history && document.createElement) {
          history.replaceChildren();
          for (const answer of next.savedAnswers || []) {
            const row = document.createElement("article"), title = document.createElement("h3"), body = document.createElement("p");
            title.textContent = `Câu ${answer.orderNo}: ${answer.text}`;
            body.textContent = answer.transcript || "Chưa có nội dung nhận dạng.";
            row.append(title, body);
            if (answer.hasAudio) {
              const link = document.createElement("a"), url = new URL(root.dataset.audio, location.href);
              url.searchParams.set("questionId", answer.examQuestionId);
              link.href = url; link.textContent = "Nghe bản thu đã nộp"; link.target = "_blank"; link.rel = "noopener"; row.append(link);
            }
            history.append(row);
          }
        }
        form.hidden = next.completed;
        if (next.completed) document.getElementById("voice-clock").textContent = "00:00";
        startRealtime();
        return true;
      } catch (error) { status.textContent = error.message; return false; }
      finally { queued--; }
    };
    queue = queue.then(operation, operation);
    return queue;
  };
  const save = async submit => {
    if (!state?.question) return false;
    const answer = { examQuestionId: state.question.examQuestionId, transcript: transcript.value, submit };
    const requestVersion = version;
    if (submit) pendingSubmit = answer;
    await localDraft(answer.examQuestionId, answer.transcript, false, submit);
    return sync(answer, requestVersion);
  };
  const poll = () => {
    if (submitting || closed) return;
    return pendingSubmit ? sync(pendingSubmit) : dirty ? save(false) : sync();
  };
  const startRealtime = async () => {
    if (hub || startingHub || closed || !state) return;
    if (!window.signalR) { connectionStatus.textContent = "Đang dùng đồng bộ định kỳ; SignalR chưa tải được."; return; }
    startingHub = true;
    const connection = new signalR.HubConnectionBuilder().withUrl("/hubs/voice-room")
      .withAutomaticReconnect([0, 2000, 5000, 10000]).build();
    connection.on("VoiceRoomChanged", () => poll());
    connection.onreconnecting(() => { connectionStatus.textContent = "Mất kết nối trực tiếp. Bản nháp vẫn được giữ; đang kết nối lại…"; });
    connection.onreconnected(async () => {
      try { await connection.invoke("Join", state.candidateId); connectionStatus.textContent = "Đã kết nối lại."; poll(); }
      catch { connectionStatus.textContent = "Không thể đăng ký lại phòng. Hãy kiểm tra đăng nhập."; }
    });
    connection.onclose(() => { hub = null; if (!closed) setTimeout(startRealtime, 5000); });
    try {
      await connection.start(); await connection.invoke("Join", state.candidateId);
      hub = connection; connectionStatus.textContent = "Đã kết nối trực tiếp.";
    } catch {
      await connection.stop(); connectionStatus.textContent = "Chưa có kết nối trực tiếp. Đang đồng bộ định kỳ.";
    } finally { startingHub = false; }
  };
  transcript.addEventListener("input", () => {
    dirty = true; version++; saved.textContent = "Có nội dung chưa lưu lên máy chủ.";
    paintDraft();
    if (state?.question) localDraft(state.question.examQuestionId, transcript.value, false);
  });
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (submitting || !state?.question) return;
    submitting = true;
    document.getElementById("voice-submit").disabled = true;
    try { await window.voiceCapture?.beforeSubmit(); await save(true); }
    catch (error) { status.textContent = error.message; }
    finally { submitting = false; document.getElementById("voice-submit").disabled = false; }
  });
  root.addEventListener("voice-audio-saved", event => {
    saved.textContent = "Đã lưu bản thu trên máy chủ.";
    if (state?.question && event.detail?.question?.examQuestionId === state.question.examQuestionId)
      state.question.hasAudio = event.detail.question.hasAudio;
    paintDraft();
  });
  root.addEventListener("voice-draft-ready", event => {
    if (state?.question?.examQuestionId === event.detail.questionId && !submitting) save(false);
  });
  root.addEventListener("voice-capture-state", paintDraft);
  document.getElementById("voice-save").addEventListener("click", async () => {
    try { await window.voiceCapture?.beforeSubmit(); await save(false); }
    catch (error) { status.textContent = error.message; }
  });
  document.getElementById("voice-retry").addEventListener("click", poll);
  window.addEventListener("online", poll);
  document.addEventListener("visibilitychange", () => { if (!document.hidden) poll(); });
  window.addEventListener("beforeunload", event => { if (dirty || pendingSubmit) { event.preventDefault(); event.returnValue = ""; } });
  window.addEventListener("pagehide", () => { closed = true; hub?.stop(); });
  setInterval(() => {
    if (!state?.question) return;
    const left = paintClock();
    if (!left) sync();
    else if (left <= 2 && dirty && !queued) save(false);
  }, 500);
  setInterval(() => { if (!state?.completed || pendingSubmit) poll(); }, 5000);
  sync();
})();
