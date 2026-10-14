document.addEventListener("DOMContentLoaded", () => {
  // ── Custom confirm dialog (replaces window.confirm) ─────────────────────
  const backdrop = document.getElementById("confirm-backdrop");
  const messageEl = document.getElementById("confirm-message");
  const okBtn = document.getElementById("confirm-ok");
  const cancelBtn = document.getElementById("confirm-cancel");
  let resolveConfirm = null;

  const showConfirm = (message) => new Promise((resolve) => {
    resolveConfirm = resolve;
    messageEl.textContent = message;
    backdrop.classList.add("is-open");
    backdrop.setAttribute("aria-hidden", "false");
    okBtn.focus();
  });

  const closeConfirm = (result) => {
    backdrop.classList.remove("is-open");
    backdrop.setAttribute("aria-hidden", "true");
    if (resolveConfirm) {
      resolveConfirm(result);
      resolveConfirm = null;
    }
  };

  okBtn.addEventListener("click", () => closeConfirm(true));
  cancelBtn.addEventListener("click", () => closeConfirm(false));
  backdrop.addEventListener("click", (event) => {
    if (event.target === backdrop) closeConfirm(false);
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && backdrop.classList.contains("is-open")) {
      event.preventDefault();
      closeConfirm(false);
    }
  });

  // Any form marked data-confirm asks before submitting.
  document.querySelectorAll("form[data-confirm]").forEach((form) => {
    const handler = async (event) => {
      event.preventDefault();
      const confirmed = await showConfirm(form.dataset.confirm || "Bạn có chắc chắn?");
      if (confirmed) {
        form.removeEventListener("submit", handler);
        form.submit();
      }
    };
    form.addEventListener("submit", handler);
  });

  // ── Shared helpers ───────────────────────────────────────────────────────
  const pad = (value) => String(value).padStart(2, "0");

  // ── Realtime list changes ────────────────────────────────────────────────
  const notice = document.getElementById("realtime-notice");
  if (notice && window.signalR) {
    const message = document.getElementById("realtime-notice-message");
    const refresh = document.getElementById("realtime-notice-refresh");
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/exam-updates")
      .withAutomaticReconnect()
      .build();

    connection.on("ExamListChanged", (event) => {
      message.textContent = event?.message || "Dữ liệu vừa thay đổi.";
      notice.hidden = false;
    });
    refresh.addEventListener("click", () => window.location.reload());
    connection.start().catch(() => {
      // The application remains usable if the realtime transport is unavailable.
    });
  }

  // ── Date-time picker ─────────────────────────────────────────────────────
  // Initialise a single date-time picker root element.
  // Self-contained: each picker reads/writes its own [data-date-time-value] input.
  const initDateTimePicker = (root) => {
    const trigger = root.querySelector("[data-date-time-trigger]");
    const label = root.querySelector("[data-date-time-label]");
    const panel = root.querySelector("[data-date-time-panel]");
    const monthLabel = root.querySelector("[data-date-time-month]");
    const days = root.querySelector("[data-date-time-days]");
    const hourInput = root.querySelector("[data-date-time-hour]");
    const minuteInput = root.querySelector("[data-date-time-minute]");
    const previousButton = root.querySelector("[data-date-time-previous]");
    const valueInput = root.querySelector("[data-date-time-value]");

    const minimumDateTime = () => {
      const minimum = new Date();
      minimum.setSeconds(0, 0);
      minimum.setMinutes(minimum.getMinutes() + 1);
      return minimum;
    };

    const parseValue = (value) => {
      const parts = value?.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
      if (!parts) return new Date();
      return new Date(
        Number(parts[1]), Number(parts[2]) - 1, Number(parts[3]),
        Number(parts[4]), Number(parts[5])
      );
    };

    const toInputValue = (date) =>
      `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
      `T${pad(date.getHours())}:${pad(date.getMinutes())}`;

    const toLabel = (date) =>
      `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}` +
      ` · ${pad(date.getHours())}:${pad(date.getMinutes())}`;

    const isSameDay = (a, b) =>
      a.getFullYear() === b.getFullYear() &&
      a.getMonth() === b.getMonth() &&
      a.getDate() === b.getDate();

    let selected = parseValue(valueInput.value);
    let visibleMonth = new Date(selected.getFullYear(), selected.getMonth(), 1);

    const readTime = () => {
      const hour = Math.min(23, Math.max(0, Number(hourInput.value) || 0));
      const minute = Math.min(59, Math.max(0, Number(minuteInput.value) || 0));
      selected.setHours(hour, minute, 0, 0);
      hourInput.value = pad(selected.getHours());
      minuteInput.value = pad(selected.getMinutes());
    };

    const render = () => {
      const formatter = new Intl.DateTimeFormat("vi-VN", { month: "long", year: "numeric" });
      const title = formatter.format(visibleMonth);
      monthLabel.textContent = title.charAt(0).toUpperCase() + title.slice(1);
      hourInput.value = pad(selected.getHours());
      minuteInput.value = pad(selected.getMinutes());
      days.innerHTML = "";

      const year = visibleMonth.getFullYear();
      const month = visibleMonth.getMonth();
      const mondayOffset = (new Date(year, month, 1).getDay() + 6) % 7;
      const firstDate = new Date(year, month, 1 - mondayOffset);
      const today = new Date();
      const todayStart = new Date(today.getFullYear(), today.getMonth(), today.getDate());
      previousButton.disabled = visibleMonth <= new Date(today.getFullYear(), today.getMonth(), 1);

      for (let i = 0; i < 42; i++) {
        const date = new Date(firstDate.getFullYear(), firstDate.getMonth(), firstDate.getDate() + i);
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "date-time-picker__day";
        btn.textContent = date.getDate();
        btn.setAttribute("aria-label", date.toLocaleDateString("vi-VN"));
        const isPast = date < todayStart;
        btn.disabled = isPast;
        btn.classList.toggle("is-outside", date.getMonth() !== month);
        btn.classList.toggle("is-today", isSameDay(date, today));
        btn.classList.toggle("is-selected", isSameDay(date, selected));
        btn.classList.toggle("is-disabled", isPast);
        btn.addEventListener("click", () => {
          readTime();
          selected.setFullYear(date.getFullYear(), date.getMonth(), date.getDate());
          visibleMonth = new Date(date.getFullYear(), date.getMonth(), 1);
          render();
        });
        days.appendChild(btn);
      }
    };

    const open = () => {
      selected = parseValue(valueInput.value);
      const minimum = minimumDateTime();
      if (selected < minimum) selected = minimum;
      visibleMonth = new Date(selected.getFullYear(), selected.getMonth(), 1);
      render();
      panel.setAttribute("aria-hidden", "false");
      trigger.setAttribute("aria-expanded", "true");
      root.classList.add("is-open");
    };

    const close = (restoreFocus = false) => {
      panel.setAttribute("aria-hidden", "true");
      trigger.setAttribute("aria-expanded", "false");
      root.classList.remove("is-open");
      if (restoreFocus) trigger.focus();
    };

    const commit = () => {
      readTime();
      valueInput.value = toInputValue(selected);
      label.textContent = toLabel(selected);
      valueInput.dispatchEvent(new Event("input", { bubbles: true }));
      valueInput.dispatchEvent(new Event("change", { bubbles: true }));
      close(true);
    };

    trigger.addEventListener("click", () => {
      root.classList.contains("is-open") ? close() : open();
    });
    previousButton.addEventListener("click", () => {
      visibleMonth = new Date(visibleMonth.getFullYear(), visibleMonth.getMonth() - 1, 1);
      render();
    });
    root.querySelector("[data-date-time-next]").addEventListener("click", () => {
      visibleMonth = new Date(visibleMonth.getFullYear(), visibleMonth.getMonth() + 1, 1);
      render();
    });
    root.querySelector("[data-date-time-today]").addEventListener("click", () => {
      selected = minimumDateTime();
      visibleMonth = new Date(selected.getFullYear(), selected.getMonth(), 1);
      render();
    });
    root.querySelector("[data-date-time-done]").addEventListener("click", commit);

    [hourInput, minuteInput].forEach((input) => {
      input.addEventListener("change", readTime);
      input.addEventListener("keydown", (event) => {
        if (event.key === "Enter") { event.preventDefault(); commit(); }
      });
    });

    panel.addEventListener("keydown", (event) => {
      if (event.key === "Escape") { event.preventDefault(); close(true); }
    });
    document.addEventListener("mousedown", (event) => {
      if (!root.contains(event.target)) close();
    });
  };

  // Init all date-time pickers present on the page (any view).
  document.querySelectorAll("[data-date-time-picker]").forEach(initDateTimePicker);

  // ── Create exam session form (ExamSessions/Create only) ──────────────────
  const scheduleForm = document.querySelector("[data-schedule-form]");
  if (!scheduleForm) return;

  const rows = document.getElementById("participant-rows");
  const empty = document.getElementById("participant-empty");
  const startInput = scheduleForm.querySelector("[data-schedule-start]");
  const durationInput = scheduleForm.querySelector("[data-schedule-duration]");
  const classPicker = scheduleForm.querySelector("[data-class-picker]");
  const classInput = scheduleForm.querySelector('[name="ClassId"]');

  // Mirrors the server-side planner: every slot lasts the same TimePerStudent
  // and starts exactly where the previous one ended.
  const updatePreviews = () => {
    const start = startInput.value ? new Date(startInput.value) : null;
    const duration = Number(durationInput.value);
    const valid = start && !Number.isNaN(start.getTime()) && Number.isFinite(duration) && duration >= 1;

    let cursor = valid ? start.getTime() : Number.NaN;

    rows.querySelectorAll("[data-participant-row]").forEach((row) => {
      const preview = row.querySelector("[data-slot-preview]");
      if (!valid) {
        preview.textContent = "—";
        return;
      }

      const from = new Date(cursor);
      const to = new Date(cursor + duration * 60000);
      const sameDay = from.toDateString() === start.toDateString()
        && to.toDateString() === start.toDateString();

      preview.textContent =
        `${pad(from.getHours())}:${pad(from.getMinutes())} – ${pad(to.getHours())}:${pad(to.getMinutes())}` +
        (sameDay ? "" : " (sang ngày khác)");

      cursor = to.getTime();
    });
  };

  const syncSelectPicker = (name, value) => {
    const select = scheduleForm.querySelector(`[name="${name}"]`);
    const root = select?.closest("[data-select-picker]");
    const option = root?.querySelector(`[data-select-option][data-value="${value}"]`);
    const label = root?.querySelector("[data-select-label]");
    if (!select || !option || !label) return;

    select.value = String(value);
    label.textContent = option.dataset.label;
    label.classList.remove("is-placeholder");
    root.querySelectorAll("[data-select-option]").forEach((item) => {
      const isSelected = item === option;
      item.classList.toggle("is-selected", isSelected);
      item.setAttribute("aria-selected", isSelected ? "true" : "false");
    });
  };

  const renderRoster = (students) => {
    rows.innerHTML = "";

    students.forEach((student, index) => {
      const row = document.createElement("div");
      row.className = "participant-row participant-row--roster";
      row.dataset.participantRow = "";

      const number = document.createElement("div");
      number.className = "participant-number";
      number.textContent = `Ca ${index + 1}`;

      const fields = document.createElement("div");
      fields.className = "participant-fields";

      const identity = document.createElement("div");
      identity.className = "student-identity participant-student";
      const avatar = document.createElement("div");
      avatar.className = "avatar";
      avatar.setAttribute("aria-hidden", "true");
      avatar.textContent = student.name?.trim().charAt(0).toLocaleUpperCase("vi") || "?";
      const text = document.createElement("div");
      const name = document.createElement("strong");
      name.textContent = student.name;
      const email = document.createElement("span");
      email.textContent = student.email;
      text.append(name, email);
      identity.append(avatar, text);

      const preview = document.createElement("div");
      preview.className = "slot-preview";
      const previewLabel = document.createElement("span");
      previewLabel.textContent = "Khung giờ dự kiến";
      const previewValue = document.createElement("strong");
      previewValue.dataset.slotPreview = "";
      previewValue.textContent = "—";
      preview.append(previewLabel, previewValue);

      fields.append(identity, preview);
      row.append(number, fields);
      rows.appendChild(row);
    });

    empty.classList.toggle("visible", students.length === 0);
    empty.textContent = students.length === 0
      ? "Lớp này chưa có sinh viên đang hoạt động."
      : "";
    updatePreviews();
  };

  let rosterRequest = 0;
  const loadRoster = async () => {
    const classId = Number(classInput?.value);
    rows.innerHTML = "";
    if (!classId || !classPicker?.dataset.rosterUrl) {
      empty.textContent = "Chọn lớp học để tải danh sách sinh viên.";
      empty.classList.add("visible");
      return;
    }

    const ticket = ++rosterRequest;
    empty.textContent = "Đang tải danh sách sinh viên…";
    empty.classList.add("visible");

    try {
      const response = await fetch(`${classPicker.dataset.rosterUrl}?classId=${classId}`, {
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error("Không thể tải lớp học");

      const data = await response.json();
      if (ticket !== rosterRequest) return;

      syncSelectPicker("CourseId", data.courseId);
      syncSelectPicker("LecturerId", data.lecturerId);
      renderRoster(Array.isArray(data.students) ? data.students : []);
    } catch {
      if (ticket === rosterRequest) {
        empty.textContent = "Không thể tải danh sách sinh viên của lớp.";
        empty.classList.add("visible");
      }
    }
  };

  classInput?.addEventListener("change", loadRoster);
  startInput.addEventListener("input", updatePreviews);
  durationInput.addEventListener("input", updatePreviews);
  loadRoster();
});
