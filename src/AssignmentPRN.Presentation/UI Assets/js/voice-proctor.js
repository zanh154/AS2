(() => {
  "use strict";

  const root = document.getElementById("voice-room");
  if (!root) return;

  const cheatModal = document.getElementById("voice-cheat-modal");
  const cheatOverlay = document.getElementById("voice-disqualified-overlay");
  const cheatCountEl = document.getElementById("voice-cheat-count");
  const cheatAckBtn = document.getElementById("voice-cheat-ack-btn");
  const cheatReasonEl = document.getElementById("voice-cheat-reason");

  const identity = document.getElementById("voice-identity");
  const candidateId = identity?.dataset.candidate || "default";
  const token = root.querySelector('[name="__RequestVerificationToken"]')?.value || "";
  const proctorUrl = root.dataset.proctor;

  const storageKey = `voice_proctor_violations_${candidateId}`;
  let violations = parseInt(sessionStorage.getItem(storageKey) || "0", 10);
  let lastTriggerTime = 0;
  let isDisqualified = sessionStorage.getItem(`voice_disqualified_${candidateId}`) === "1";

  const showDisqualifiedScreen = (msg) => {
    isDisqualified = true;
    sessionStorage.setItem(`voice_disqualified_${candidateId}`, "1");
    root.dataset.disqualified = "true";

    // Dừng mọi thao tác thu âm / đọc câu hỏi
    if (window.speechSynthesis) window.speechSynthesis.cancel();
    const stopBtn = document.getElementById("voice-stop");
    if (stopBtn && !stopBtn.disabled && typeof stopBtn.click === "function") stopBtn.click();

    // Khóa form
    const transcript = document.getElementById("voice-transcript");
    if (transcript) transcript.readOnly = true;

    // Ẩn modal cảnh báo nhẹ (nếu đang mở) và mở màn hình đình chỉ
    if (cheatModal) cheatModal.setAttribute("hidden", "");
    if (cheatOverlay) {
      cheatOverlay.removeAttribute("hidden");
      requestAnimationFrame(() => cheatOverlay.classList.add("is-visible"));
      const desc = document.getElementById("voice-disqualified-desc");
      if (desc && msg) desc.textContent = msg;
    }
  };

  if (isDisqualified) {
    showDisqualifiedScreen("Bài thi này đã bị đình chỉ và cho 0 điểm do vi phạm quy chế thi trước đó.");
    return;
  }

  const triggerViolation = async (reason) => {
    const isCompleted = root.dataset.completed === "true";
    if (isCompleted || isDisqualified) return;

    const now = Date.now();
    // Debounce 1.5 giây để tránh kích hoạt kép khi cả visibilitychange và blur cùng bắn
    if (now - lastTriggerTime < 1500) return;
    lastTriggerTime = now;

    violations++;
    sessionStorage.setItem(storageKey, violations.toString());

    // Báo cáo vi phạm về Server
    let serverRes = null;
    if (proctorUrl) {
      try {
        const resp = await fetch(proctorUrl, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": token
          },
          body: JSON.stringify({ count: violations, reason })
        });
        if (resp.ok) {
          serverRes = await resp.json();
        }
      } catch (err) {
        console.warn("Could not report proctor violation to server:", err);
      }
    }

    if (violations >= 4 || serverRes?.disqualified) {
      showDisqualifiedScreen("Bạn đã rời khỏi màn hình làm bài quá 3 lần (chuyển tab, mở ứng dụng khác). Hệ thống đã lập biên bản đình chỉ thi và ghi nhận 0 ĐIỂM.");
    } else {
      // Hiển thị modal cảnh báo cho lần 1, 2, 3
      if (cheatCountEl) cheatCountEl.textContent = `${violations} / 3`;
      if (cheatReasonEl) cheatReasonEl.textContent = reason;
      if (cheatModal) {
        cheatModal.removeAttribute("hidden");
        requestAnimationFrame(() => cheatModal.classList.add("is-visible"));
      }
    }
  };

  // 1. Bắt sự kiện chuyển tab (Tab Visibility)
  document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
      triggerViolation("Chuyển sang tab hoặc ứng dụng khác");
    }
  });

  // 2. Bắt sự kiện rời cửa sổ (Window Blur: Alt+Tab, click ngoài màn hình)
  window.addEventListener("blur", () => {
    // Chỉ kích hoạt nếu modal kiểm tra mic không đang mở
    const micModal = document.getElementById("voice-mic-modal");
    if (micModal && !micModal.hasAttribute("hidden")) return;
    triggerViolation("Rời khỏi cửa sổ phòng thi (Alt+Tab hoặc click chuột ra ngoài)");
  });

  // Nút xác nhận đã hiểu cảnh báo
  cheatAckBtn?.addEventListener("click", () => {
    if (cheatModal) {
      cheatModal.classList.remove("is-visible");
      setTimeout(() => cheatModal.setAttribute("hidden", ""), 250);
    }
  });

  window.voiceProctor = {
    get violations() { return violations; },
    get isDisqualified() { return isDisqualified; },
    triggerViolation
  };
})();
