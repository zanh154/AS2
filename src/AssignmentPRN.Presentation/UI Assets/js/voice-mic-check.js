(() => {
  "use strict";

  const root = document.getElementById("voice-room");
  const modal = document.getElementById("voice-mic-modal");
  if (!modal) return;

  const byId = id => document.getElementById(id);
  const startBtn = byId("voice-mic-start-btn");
  const confirmBtn = byId("voice-mic-confirm-btn");
  const skipBtn = byId("voice-mic-skip-btn");
  const retestBtn = byId("voice-mic-retest-btn");
  const statusIndicator = byId("voice-mic-status-indicator");
  const statusText = byId("voice-mic-status-text");
  const levelFill = byId("voice-mic-level-fill");
  const waveBars = modal.querySelectorAll(".voice-mic-waves .wave-bar");
  const playbackWrap = byId("voice-mic-playback-wrap");
  const playbackAudio = byId("voice-mic-playback");
  const candidateId = root?.querySelector("#voice-identity")?.dataset.candidate || "default";
  const storageKey = `voice_mic_tested_${candidateId}`;

  let stream = null;
  let audioCtx = null;
  let analyser = null;
  let animId = null;
  let recorder = null;
  let audioChunks = [];
  let isTesting = false;
  let testRecordedBlob = null;
  let speechDetected = false;
  let speechTimer = null;

  const setStatus = (type, html) => {
    if (statusIndicator) {
      statusIndicator.className = "voice-mic-status-indicator";
      if (type) statusIndicator.classList.add(`is-${type}`);
    }
    if (statusText) statusText.innerHTML = html;
  };

  const cleanupAudio = () => {
    if (animId) {
      cancelAnimationFrame(animId);
      animId = null;
    }
    if (recorder && recorder.state !== "inactive") {
      try { recorder.stop(); } catch {}
      recorder = null;
    }
    if (stream) {
      stream.getTracks().forEach(track => {
        track.onended = null;
        track.stop();
      });
      stream = null;
    }
    if (audioCtx) {
      audioCtx.close().catch(() => {});
      audioCtx = null;
    }
    analyser = null;
    isTesting = false;
    clearTimeout(speechTimer);

    // Reset visualizer bars
    if (levelFill) levelFill.style.width = "0%";
    waveBars.forEach(b => { b.style.transform = "scaleY(0.2)"; });
  };

  // Read by voice-examiner.js at the moment it wants to speak, so the order the scripts
  // load in does not matter. Set synchronously; the CSS transition is only cosmetic.
  const markOpen = open => { if (root) root.dataset.micCheck = open ? "open" : "closed"; };

  const openModal = () => {
    markOpen(true);
    modal.removeAttribute("hidden");
    // Trigger transition
    requestAnimationFrame(() => {
      modal.classList.add("is-visible");
    });
  };

  const closeModal = () => {
    markOpen(false);
    cleanupAudio();
    modal.classList.remove("is-visible");
    setTimeout(() => {
      modal.setAttribute("hidden", "");
    }, 250);
  };

  const finishAndEnter = () => {
    try {
      sessionStorage.setItem(storageKey, "1");
    } catch {}
    closeModal();
    // The click that closed the check is what lets the virtual examiner speak.
    root?.dispatchEvent(new CustomEvent("voice-mic-ready"));
  };

  const startMicTest = async () => {
    cleanupAudio();
    setStatus("listening", "Đang kết nối micro của bạn…");
    startBtn.disabled = true;

    try {
      if (!navigator.mediaDevices?.getUserMedia) {
        throw new Error("Trình duyệt không hỗ trợ truy cập microphone.");
      }

      stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });

      // Thiết lập Web Audio API để đo volume thời gian thực
      const AudioContextClass = window.AudioContext || window.webkitAudioContext;
      if (AudioContextClass) {
        audioCtx = new AudioContextClass();
        analyser = audioCtx.createAnalyser();
        analyser.fftSize = 64;
        const source = audioCtx.createMediaStreamSource(stream);
        source.connect(analyser);
      }

      isTesting = true;
      speechDetected = false;
      audioChunks = [];

      // Chuẩn bị MediaRecorder để thu âm thử mẫu 3 giây
      const mime = ["audio/webm;codecs=opus", "audio/ogg;codecs=opus", "audio/mp4", "audio/webm"]
        .find(t => window.MediaRecorder && MediaRecorder.isTypeSupported(t));

      if (window.MediaRecorder && mime) {
        recorder = new MediaRecorder(stream, { mimeType: mime });
        recorder.ondataavailable = e => {
          if (e.data && e.data.size > 0) audioChunks.push(e.data);
        };
        recorder.onstop = () => {
          if (audioChunks.length > 0) {
            testRecordedBlob = new Blob(audioChunks, { type: mime });
            if (playbackAudio) {
              if (playbackAudio.src) URL.revokeObjectURL(playbackAudio.src);
              playbackAudio.src = URL.createObjectURL(testRecordedBlob);
            }
            if (playbackWrap) playbackWrap.removeAttribute("hidden");
          }
        };
        recorder.start();
      }

      setStatus("listening", "<strong>Đang lắng nghe:</strong> Hãy nói 1 câu vào mic (Ví dụ: <em>\"Alo 1 2 3, em đã sẵn sàng!\"</em>)");
      startBtn.innerHTML = `
        <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
        <span>Đang nghe…</span>`;

      const dataArray = analyser ? new Uint8Array(analyser.frequencyBinCount) : null;
      let voiceActivityCount = 0;

      const visualLoop = () => {
        if (!isTesting) return;

        if (analyser && dataArray) {
          analyser.getByteFrequencyData(dataArray);
          let sum = 0;
          for (let i = 0; i < dataArray.length; i++) sum += dataArray[i];
          const avg = sum / dataArray.length;
          const pct = Math.min(100, Math.round((avg / 128) * 100));

          if (levelFill) levelFill.style.width = `${pct}%`;

          // Gán độ co giãn cho 8 vạch sóng
          waveBars.forEach((bar, idx) => {
            const freqVal = dataArray[idx % dataArray.length] || 0;
            const scale = Math.max(0.18, Math.min(1.4, freqVal / 90));
            bar.style.transform = `scaleY(${scale})`;
          });

          // Phát hiện giọng nói thực tế (threshold)
          if (avg > 14) {
            voiceActivityCount++;
            if (voiceActivityCount >= 8 && !speechDetected) {
              speechDetected = true;
              onSpeechConfirmed();
            }
          }
        }

        animId = requestAnimationFrame(visualLoop);
      };

      visualLoop();

      // Sau tối đa 3.5 giây nếu có tiếng hoặc hết thời gian thu mẫu
      speechTimer = setTimeout(() => {
        if (isTesting) {
          if (!speechDetected && voiceActivityCount < 4) {
            setStatus("warning", "Chưa nhận rõ âm thanh. Hãy kiểm tra mic không bị tắt (Mute) và nói to hơn, sau đó bấm <strong>Thử lại</strong>.");
            startBtn.disabled = false;
            startBtn.innerHTML = `
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/>
                <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/>
              </svg>
              <span>Thử lại</span>`;
            // Vẫn cho phép vào phòng nếu sinh viên muốn
            confirmBtn.disabled = false;
          } else {
            onSpeechConfirmed();
          }
        }
      }, 3500);

    } catch (err) {
      cleanupAudio();
      let msg = "Không thể truy cập microphone. Vui lòng cấp quyền trong trình duyệt.";
      if (err.name === "NotAllowedError" || err.name === "PermissionDeniedError") {
        msg = "Trình duyệt chưa được cấp quyền dùng Microphone. Hãy nhấp vào biểu tượng ổ khóa cạnh thanh địa chỉ để <strong>Cho phép Micro</strong> rồi thử lại.";
      } else if (err.name === "NotFoundError" || err.name === "DevicesNotFoundError") {
        msg = "Không tìm thấy thiết bị Microphone trên máy tính của bạn. Hãy cắm mic và thử lại.";
      }
      setStatus("error", msg);
      startBtn.disabled = false;
      startBtn.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/>
          <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/>
        </svg>
        <span>Thử lại</span>`;
      confirmBtn.disabled = false;
    }
  };

  const onSpeechConfirmed = () => {
    if (recorder && recorder.state === "recording") {
      try { recorder.stop(); } catch {}
    }
    setStatus("success", "<strong>✓ Microphone hoạt động rất tốt!</strong> Tín hiệu giọng nói rõ ràng.");
    startBtn.disabled = false;
    startBtn.innerHTML = `
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/>
        <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/>
      </svg>
      <span>Thử lại</span>`;
    confirmBtn.disabled = false;
    confirmBtn.classList.remove("btn-secondary");
    confirmBtn.classList.add("btn-primary");
    confirmBtn.focus();
  };

  // Event Listeners
  startBtn?.addEventListener("click", startMicTest);
  confirmBtn?.addEventListener("click", finishAndEnter);
  skipBtn?.addEventListener("click", finishAndEnter);
  retestBtn?.addEventListener("click", () => {
    openModal();
    startMicTest();
  });

  // Không tự mở ở đây nữa: voice-room.js mở hộp này khi máy chủ báo câu đầu tiên đang chờ
  // (AwaitingReady). Mở giữa giờ thi sẽ ăn vào thời gian trả lời, nên chỉ mở khi sinh viên
  // tự bấm "Kiểm tra Mic".
  markOpen(false);

  window.voiceMicCheck = {
    open: openModal,
    close: closeModal,
    startTest: startMicTest
  };
})();
