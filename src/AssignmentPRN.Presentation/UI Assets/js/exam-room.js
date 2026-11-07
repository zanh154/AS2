// The clock on a slot being sat, and the guard that keeps a student on the page.
//
// The countdown starts from a number of seconds the server put in the markup, not from
// the browser clock, which can be minutes out. At 00:00 the paper is submitted on its
// own; the server still has the final say and refuses anything past its own grace.
//
// Markup contract:
//   <div id="exam-clock" data-exam-seconds="900">…<strong id="exam-clock-value"></strong></div>
//   <form data-exam-form data-exam-guard>…</form>
(() => {
  const WARNING_SECONDS = 60;

  const pad = (value) => String(value).padStart(2, "0");

  const setup = () => {
    const form = document.querySelector("[data-exam-form]");
    const clock = document.getElementById("exam-clock");
    const value = document.getElementById("exam-clock-value");

    let submitting = false;
    let dirty = false;
    let saving = false;
    let version = 0;
    let saveTimer;
    const draftStatus = form?.querySelector("[data-draft-status]");
    const saveDraft = async () => {
      if (!dirty || saving || submitting || !draftStatus) return;
      saving = true;
      const snapshotVersion = version;
      draftStatus.textContent = "Đang lưu đáp án…";
      const controller = new AbortController();
      const timeout = window.setTimeout(() => controller.abort(), 10000);
      try {
        const response = await fetch(form.dataset.draftUrl, {
          method: "POST", body: new FormData(form), credentials: "same-origin", signal: controller.signal
        });
        const result = await response.json();
        if (!response.ok || !result.success) throw new Error(result.error || "Không thể lưu đáp án.");
        if (snapshotVersion === version) {
          dirty = false;
          draftStatus.textContent = "Đã lưu đáp án · " + new Date().toLocaleTimeString("vi-VN") + ". Bạn vẫn cần nộp bài.";
        }
      } catch {
        draftStatus.textContent = "Chưa lưu được đáp án. Giữ trang mở; hệ thống sẽ thử lại khi có kết nối.";
      } finally {
        window.clearTimeout(timeout);
        saving = false;
        if (dirty && !submitting) saveTimer = window.setTimeout(saveDraft, 3000);
      }
    };
    if (draftStatus) {
      form.addEventListener("change", (event) => {
        if (!event.target.matches('input[type="radio"][name^="answers["]')) return;
        dirty = true;
        version++;
        draftStatus.textContent = "Có đáp án mới, đang chờ lưu…";
        window.clearTimeout(saveTimer);
        saveTimer = window.setTimeout(saveDraft, 400);
      });
      window.addEventListener("online", saveDraft);
    }

    if (form && form.hasAttribute("data-exam-guard")) {
      // The confirm dialog in site.js hands the form off with the native submit(), which
      // fires no event. Wrapping the method is the one place every path goes through.
      const nativeSubmit = form.submit.bind(form);
      form.submit = () => {
        submitting = true;
        nativeSubmit();
      };
      form.addEventListener("submit", () => {
        submitting = true;
      });

      window.addEventListener("beforeunload", (event) => {
        if (submitting || !dirty) {
          return;
        }
        event.preventDefault();
        // Browsers show their own wording; a non-empty value is what triggers it.
        event.returnValue = "";
      });
    }

    if (!clock || !value) {
      return;
    }

    let remaining = Number.parseInt(clock.dataset.examSeconds ?? "0", 10);
    if (!Number.isFinite(remaining) || remaining < 0) {
      remaining = 0;
    }

    // Wall-clock arithmetic, so a throttled background tab still lands on the right
    // second when it wakes up instead of drifting by however long it slept.
    const deadline = Date.now() + remaining * 1000;
    let finished = false;

    const paint = (seconds) => {
      const minutes = Math.floor(seconds / 60);
      value.textContent = `${pad(minutes)}:${pad(seconds % 60)}`;
      clock.classList.toggle("is-warning", seconds > 0 && seconds <= WARNING_SECONDS);
      clock.classList.toggle("is-over", seconds === 0);
    };

    const finish = () => {
      if (finished) {
        return;
      }
      finished = true;
      paint(0);

      if (!form) {
        window.location.reload();
        return;
      }

      // Hand in whatever has been ticked. The confirm dialog is skipped: this is the
      // clock running out, not the student choosing to stop.
      form.removeAttribute("data-confirm");
      form.querySelectorAll("button[type=submit]").forEach((button) => {
        button.disabled = true;
      });
      form.submit();
    };

    const tick = () => {
      const left = Math.max(0, Math.round((deadline - Date.now()) / 1000));
      paint(left);
      if (left === 0) {
        window.clearInterval(timer);
        finish();
      }
    };

    const timer = window.setInterval(tick, 1000);
    tick();
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", setup);
  } else {
    setup();
  }
})();
