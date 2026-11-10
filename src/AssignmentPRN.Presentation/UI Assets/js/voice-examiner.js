// Virtual examiner: reads every new question or probe aloud, then opens the microphone
// so the student answers by voice, the way a lecturer asks and waits for a reply.
// Presentation only. It drives the existing capture through window.voiceCapture and
// learns about new items from the "voice-item-changed" event voice-room.js raises; the
// server stays the only judge of time, budgets and what is open.
(() => {
  "use strict";
  const root = document.getElementById("voice-room");
  if (!root) return;
  const byId = id => document.getElementById(id);
  const toggle = byId("voice-examiner-auto");
  const status = byId("voice-examiner-status");
  const modal = byId("voice-mic-modal");
  const storageKey = "voice_examiner_auto";
  const itemKey = question => question ? `${question.examQuestionId}:${question.followUpId || 0}` : null;
  // Pause between the end of the question and the microphone opening, so the last
  // syllable of speech synthesis is not caught by speech recognition.
  const settleMs = 450;
  let current = null, enabled = true;

  const say = text => { if (status) { status.textContent = text; status.hidden = !text; } };
  try { enabled = localStorage.getItem(storageKey) !== "off"; } catch { /* Storage blocked: keep the default. */ }
  if (toggle) {
    toggle.checked = enabled;
    toggle.addEventListener("change", () => {
      enabled = toggle.checked;
      try { localStorage.setItem(storageKey, enabled ? "on" : "off"); } catch { /* Preference only. */ }
      say(enabled ? "Giám khảo ảo sẽ đọc câu hỏi kế tiếp và tự bật ghi âm."
        : "Đã tắt giám khảo ảo. Bấm Nghe câu hỏi và Bắt đầu trả lời khi bạn sẵn sàng.");
    });
  }

  // Speech synthesis needs a click on the page first; the microphone check gives one.
  // Until it is closed the examiner waits rather than talking over the test.
  const micReady = new Promise(resolve => {
    if (!modal || modal.hidden) { resolve(); return; }
    root.addEventListener("voice-mic-ready", () => resolve(), { once: true });
  });
  const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
  const stillOpen = (key, question) => enabled && current === key
    && Date.parse(question.deadline) - Date.now() > 3000;

  const ask = async (question, initial) => {
    const key = itemKey(question);
    await micReady;
    if (!stillOpen(key, question)) return;
    // Back after a reload with an answer already under way: do not start it over.
    if (initial && (question.transcript || question.hasAudio)) {
      say("Bạn đang trả lời dở câu này. Tiếp tục ghi âm hoặc chỉnh văn bản rồi nộp.");
      return;
    }
    const capture = window.voiceCapture;
    if (!capture?.speak) return;
    say(question.followUpId ? "Giám khảo đang hỏi thêm…" : "Giám khảo đang đọc câu hỏi…");
    const heard = await capture.speak();
    if (current !== key) return;
    if (!heard) {
      // Blocked, unsupported or interrupted by the student: leave the microphone to
      // them instead of opening it on a question they may not have heard.
      say(capture.recording ? "" : "Đọc câu hỏi trên màn hình, rồi bấm Bắt đầu trả lời khi sẵn sàng.");
      return;
    }
    await wait(settleMs);
    if (!stillOpen(key, question) || capture.recording || capture.reading) return;
    say("Đang ghi âm câu trả lời của bạn. Bấm Nộp câu trả lời khi nói xong.");
    await capture.startRecording();
    if (current === key && !capture.recording) say("Chưa bật được microphone. Bấm Bắt đầu trả lời để thử lại.");
  };

  const farewell = () => {
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return;
    const utterance = new window.SpeechSynthesisUtterance("Cảm ơn em. Buổi vấn đáp đã kết thúc.");
    utterance.lang = "vi-VN";
    window.speechSynthesis.speak(utterance);
  };

  root.addEventListener("voice-item-changed", event => {
    const { question, completed, finishedNow, initial } = event.detail || {};
    current = itemKey(question);
    if (completed) {
      say("");
      if (finishedNow && enabled) farewell();
      return;
    }
    if (!question) { say(""); return; }
    if (!enabled) return;
    ask(question, initial).catch(() => say("Đọc câu hỏi trên màn hình, rồi bấm Bắt đầu trả lời khi sẵn sàng."));
  });

  window.voiceExaminer = { get enabled() { return enabled; }, itemKey };
})();
