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
  const playBtn = byId("voice-mic-play-btn");
  const statusIndicator = byId("voice-mic-status-indicator");
  const statusText = byId("voice-mic-status-text");
  const levelFill = byId("voice-mic-level-fill");
  const waveBars = modal.querySelectorAll(".voice-mic-waves .wave-bar");
  const playbackWrap = byId("voice-mic-playback-wrap");
  const playbackAudio = byId("voice-mic-playback");
  const candidateId = root?.querySelector("#voice-identity")?.dataset.candidate || "default";
  const storageKey = `voice_mic_tested_${candidateId}`;
  const sampleDurationMs = 3000;

  let stream = null;
  let audioCtx = null;
  let analyser = null;
  let animId = null;
  let recorder = null;
  let audioChunks = [];
  let isTesting = false;
  let testRecordedBlob = null;
  let playbackUrl = null;
  let speechDetected = false;
  let speechTimer = null;

  const retryButtonHtml = `
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/>
        <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/>
      </svg>
      <span>Thử lại</span>`;

  const setStatus = (type, html) => {
    if (statusIndicator) {
      statusIndicator.className = "voice-mic-status-indicator";
      if (type) statusIndicator.classList.add(`is-${type}`);
    }
    if (statusText) statusText.innerHTML = html;
  };

  const resetPlayback = () => {
    if (playbackAudio) {
      try { playbackAudio.pause?.(); } catch {}
      playbackAudio.removeAttribute?.("src");
      playbackAudio.src = "";
    }
    if (playbackUrl) {
      try { URL.revokeObjectURL(playbackUrl); } catch {}
      playbackUrl = null;
    }
    testRecordedBlob = null;
    if (playbackWrap) playbackWrap.setAttribute("hidden", "");
    if (playBtn) playBtn.textContent = "▶ Nghe lại giọng vừa thu";
  };

  const releaseCapture = () => {
    if (animId) {
      cancelAnimationFrame(animId);
      animId = null;
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
    speechTimer = null;

    if (levelFill) levelFill.style.width = "0%";
    waveBars.forEach(b => { b.style.transform = "scaleY(0.2)"; });
  };

  const cleanupAudio = () => {
    if (recorder && recorder.state !== "inactive") {
      try { recorder.onstop = null; recorder.stop(); } catch {}
      recorder = null;
    }
    releaseCapture();
  };

  const attachRecordedAudio = (chunks, mimeHint) => {
    if (!chunks || chunks.length === 0) return false;
    const blobType = mimeHint || chunks[0]?.type || "audio/webm";
    testRecordedBlob = new Blob(chunks, { type: blobType });
    if (!testRecordedBlob || testRecordedBlob.size === 0) return false;

    if (playbackUrl) {
      try { URL.revokeObjectURL(playbackUrl); } catch {}
    }
    playbackUrl = URL.createObjectURL(testRecordedBlob);

    if (playbackAudio) {
      playbackAudio.preload = "auto";
      playbackAudio.muted = false;
      playbackAudio.volume = 1;
      playbackAudio.src = playbackUrl;
      if (typeof playbackAudio.load === "function") playbackAudio.load();
    }
    if (playbackWrap) playbackWrap.removeAttribute("hidden");
    return true;
  };

  const openModal = () => {
    modal.removeAttribute("hidden");
    requestAnimationFrame(() => {
      modal.classList.add("is-visible");
    });
  };

  const closeModal = () => {
    cleanupAudio();
    if (playbackAudio) {
      try { playbackAudio.pause?.(); } catch {}
    }
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
  };

  const finishSampleRecording = (detected) => {
    isTesting = false;
    clearTimeout(speechTimer);
    speechTimer = null;

    const finalizeUi = () => {
      releaseCapture();
      startBtn.disabled = false;
      startBtn.innerHTML = retryButtonHtml;
      confirmBtn.disabled = false;

      if (detected) {
        setStatus("success", "<strong>✓ Microphone hoạt động rất tốt!</strong> Bạn có thể bấm nghe lại giọng vừa thu bên dưới.");
        confirmBtn.classList.remove("btn-secondary");
        confirmBtn.classList.add("btn-primary");
        confirmBtn.focus();
      } else {
        setStatus("warning", "Chưa nhận rõ âm thanh. Hãy kiểm tra mic không bị tắt (Mute) và nói to hơn, sau đó bấm <strong>Thử lại</strong>.");
      }
    };

    if (recorder && recorder.state !== "inactive") {
      const activeRecorder = recorder;
      recorder = null;
      const mimeUsed = activeRecorder.mimeType;
      activeRecorder.onstop = () => {
        attachRecordedAudio(audioChunks, mimeUsed);
        finalizeUi();
      };
      try {
        if (typeof activeRecorder.requestData === "function") activeRecorder.requestData();
        activeRecorder.stop();
        return;
      } catch {}
    }

    attachRecordedAudio(audioChunks, null);
    finalizeUi();
  };

  const startMicTest = async () => {
    cleanupAudio();
    resetPlayback();
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

      const AudioContextClass = window.AudioContext || window.webkitAudioContext;
      if (AudioContextClass) {
        audioCtx = new AudioContextClass();
        if (audioCtx.state === "suspended" && typeof audioCtx.resume === "function") {
          await audioCtx.resume();
        }
        analyser = audioCtx.createAnalyser();
        analyser.fftSize = 64;
        const source = audioCtx.createMediaStreamSource(stream);
        source.connect(analyser);
      }

      isTesting = true;
      speechDetected = false;
      audioChunks = [];

      const hasMediaRecorder = typeof MediaRecorder !== "undefined";
      const mime = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg;codecs=opus", "audio/mp4"]
        .find(t => hasMediaRecorder && typeof MediaRecorder.isTypeSupported === "function" && MediaRecorder.isTypeSupported(t));

      if (hasMediaRecorder) {
        recorder = mime ? new MediaRecorder(stream, { mimeType: mime }) : new MediaRecorder(stream);
        recorder.ondataavailable = e => {
          if (e.data && e.data.size > 0) audioChunks.push(e.data);
        };
        recorder.start(200);
      }

      setStatus("listening", "<strong>Đang thu mẫu (3 giây):</strong> Hãy nói 1 câu vào mic (Ví dụ: <em>\"Alo 1 2 3, em đã sẵn sàng!\"</em>)");
      startBtn.innerHTML = `
        <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
        <span>Đang thu…</span>`;

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

          waveBars.forEach((bar, idx) => {
            const freqVal = dataArray[idx % dataArray.length] || 0;
            const scale = Math.max(0.18, Math.min(1.4, freqVal / 90));
            bar.style.transform = `scaleY(${scale})`;
          });

          if (avg > 10) {
            voiceActivityCount++;
            if (voiceActivityCount >= 6 && !speechDetected) {
              speechDetected = true;
              setStatus("listening", "<strong>✓ Đã nhận thấy giọng nói!</strong> Tiếp tục nói thêm 1–2 giây để nghe lại rõ hơn…");
            }
          }
        }

        animId = requestAnimationFrame(visualLoop);
      };

      visualLoop();

      // Thu đủ 3 giây rồi mới dừng để bảo đảm file âm thanh nghe lại luôn đầy đủ và hợp lệ
      speechTimer = setTimeout(() => {
        if (isTesting) {
          finishSampleRecording(speechDetected || voiceActivityCount >= 4);
        }
      }, sampleDurationMs);

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
      startBtn.innerHTML = retryButtonHtml;
      confirmBtn.disabled = false;
    }
  };

  playBtn?.addEventListener("click", async () => {
    if (!playbackAudio || !playbackAudio.src) return;
    try {
      playbackAudio.currentTime = 0;
      await playbackAudio.play();
    } catch {
      setStatus("warning", "Trình duyệt chặn tự phát âm thanh hoặc chưa tải được bản thu. Hãy bấm nút Play (▶) trên thanh phát audio hoặc thử thu lại.");
    }
  });

  playbackAudio?.addEventListener("play", () => {
    if (playBtn) playBtn.textContent = "Đang phát bản thu…";
  });
  playbackAudio?.addEventListener("ended", () => {
    if (playBtn) playBtn.textContent = "▶ Nghe lại giọng vừa thu";
  });
  playbackAudio?.addEventListener("pause", () => {
    if (playBtn) playBtn.textContent = "▶ Nghe lại giọng vừa thu";
  });

  // Event Listeners
  startBtn?.addEventListener("click", startMicTest);
  confirmBtn?.addEventListener("click", finishAndEnter);
  skipBtn?.addEventListener("click", finishAndEnter);
  retestBtn?.addEventListener("click", () => {
    openModal();
    startMicTest();
  });

  const hasTested = sessionStorage.getItem(storageKey);
  if (!hasTested) {
    openModal();
  }

  window.voiceMicCheck = {
    open: openModal,
    close: closeModal,
    startTest: startMicTest
  };
})();
