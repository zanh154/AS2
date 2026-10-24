// Searchable dropdown for picking a student.
//
// A plain <select> is unusable once the student list grows, so this asks the
// server for matches as you type and shows them as "email - name".
//
// Markup contract:
//   <div class="combobox" data-combobox data-combobox-url="/ExamSessions/SearchStudents">
//     <input data-combobox-input ... />        visible text field
//     <input type="hidden" data-combobox-value ... />   optional: receives the email
//     <div data-combobox-panel></div>          results drop down here
//   </div>
//
// Without a hidden field the visible input itself carries the chosen email,
// which is what the lookup page needs.
(() => {
  const DEBOUNCE_MS = 200;

  const setup = (root) => {
    if (root.dataset.comboboxReady === "1") {
      return;
    }
    root.dataset.comboboxReady = "1";

    const input = root.querySelector("[data-combobox-input]");
    const hidden = root.querySelector("[data-combobox-value]");
    const panel = root.querySelector("[data-combobox-panel]");
    const url = root.dataset.comboboxUrl;
    if (!input || !panel || !url) {
      return;
    }

    let items = [];
    let activeIndex = -1;
    let timer = null;
    let requestId = 0;
    let loading = false;

    const close = () => {
      window.clearTimeout(timer);
      requestId++;
      loading = false;
      panel.hidden = true;
      panel.innerHTML = "";
      items = [];
      activeIndex = -1;
      input.setAttribute("aria-expanded", "false");
      input.removeAttribute("aria-activedescendant");
    };

    const commit = (item) => {
      if (hidden) {
        hidden.value = item.email;
        input.setCustomValidity("");
        input.value = `${item.email} - ${item.name}`;
      } else {
        input.value = item.email;
      }
      close();
      input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const paintActive = () => {
      [...panel.children].forEach((node, index) => {
        node.classList.toggle("is-active", index === activeIndex);
        node.setAttribute("aria-selected", index === activeIndex ? "true" : "false");
      });
      if (panel.children[activeIndex]?.id) {
        input.setAttribute("aria-activedescendant", panel.children[activeIndex].id);
      }
    };

    const render = () => {
      panel.innerHTML = "";

      if (items.length === 0) {
        const empty = document.createElement("div");
        empty.className = "combobox__empty";
        empty.textContent = "Không tìm thấy sinh viên phù hợp";
        panel.appendChild(empty);
        panel.hidden = false;
        input.setAttribute("aria-expanded", "true");
        return;
      }

      items.forEach((item, index) => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "combobox__option";
        option.setAttribute("role", "option");
        option.id = `${panel.id}-option-${index}`;
        option.tabIndex = -1;

        const email = document.createElement("strong");
        email.textContent = item.email;
        const separator = document.createElement("span");
        separator.className = "combobox__separator";
        separator.textContent = " - ";
        const name = document.createElement("span");
        name.textContent = item.name;

        option.append(email, separator, name);
        // mousedown fires before blur, so the click is not lost to closing.
        option.addEventListener("mousedown", (event) => {
          event.preventDefault();
        });
        option.addEventListener("click", () => commit(item));
        option.addEventListener("mouseenter", () => {
          activeIndex = index;
          paintActive();
        });

        panel.appendChild(option);
      });

      activeIndex = 0;
      paintActive();
      panel.hidden = false;
      input.setAttribute("aria-expanded", "true");
    };

    const search = async (term) => {
      const ticket = ++requestId;
      loading = true;
      panel.textContent = "Đang tìm sinh viên…";
      panel.hidden = false;
      input.setAttribute("aria-expanded", "true");
      try {
        const response = await fetch(`${url}${url.includes("?") ? "&" : "?"}q=${encodeURIComponent(term)}`, {
          headers: { Accept: "application/json" }
        });
        if (!response.ok) {
          throw new Error(response.statusText);
        }

        const data = await response.json();
        // A slower earlier request must not overwrite newer results.
        if (ticket !== requestId) {
          return;
        }

        items = Array.isArray(data) ? data : [];
        render();
      } catch {
        if (ticket === requestId) {
          close();
          panel.textContent = "Không thể tải gợi ý. Vui lòng gõ lại để thử lại.";
          panel.hidden = false;
          input.setAttribute("aria-expanded", "true");
        }
      } finally {
        if (ticket === requestId) {
          loading = false;
        }
      }
    };

    input.setAttribute("role", "combobox");
    input.setAttribute("aria-autocomplete", "list");
    input.setAttribute("aria-expanded", "false");
    if (!panel.id) panel.id = `student-options-${Math.random().toString(36).slice(2)}`;
    panel.setAttribute("role", "listbox");
    input.setAttribute("aria-controls", panel.id);
    if (hidden) input.setCustomValidity("Vui lòng chọn sinh viên từ danh sách gợi ý.");

    input.addEventListener("input", () => {
      close();
      // Typing invalidates any previous pick until a new one is made.
      if (hidden) {
        hidden.value = "";
        input.setCustomValidity("Vui lòng chọn sinh viên từ danh sách gợi ý.");
      }

      const term = input.value.trim();
      window.clearTimeout(timer);
      timer = window.setTimeout(() => search(term), DEBOUNCE_MS);
    });

    const open = () => {
      if (panel.hidden && !loading) {
        // A selected row displays "email - name", so reopen with the full list.
        // A free-text lookup keeps its current text as the filter.
        search(hidden?.value ? "" : input.value.trim());
      }
    };

    input.addEventListener("focus", open);
    input.addEventListener("click", open);

    input.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        close();
        return;
      }
      if (panel.hidden || items.length === 0) {
        return;
      }

      if (event.key === "ArrowDown" || event.key === "ArrowUp") {
        event.preventDefault();
        const step = event.key === "ArrowDown" ? 1 : -1;
        activeIndex = (activeIndex + step + items.length) % items.length;
        paintActive();
        panel.children[activeIndex]?.scrollIntoView({ block: "nearest" });
      } else if (event.key === "Enter") {
        event.preventDefault();
        commit(items[activeIndex]);
      } else if (event.key === "Escape") {
        close();
      }
    });

    input.addEventListener("blur", () => window.setTimeout(close, 120));
  };

  const setupAll = (scope) =>
    (scope || document).querySelectorAll("[data-combobox]").forEach(setup);

  const setupSelectPicker = (root) => {
    // Wiring twice would give the trigger two click handlers, so the panel would
    // open and close again on the same click.
    if (root.dataset.selectPickerReady === "1") {
      return;
    }
    root.dataset.selectPickerReady = "1";

    const trigger = root.querySelector("[data-select-trigger]");
    const label = root.querySelector("[data-select-label]");
    const value = root.querySelector("[data-select-value]");
    const panel = root.querySelector("[data-select-panel]");
    const search = root.querySelector("[data-select-search]");
    const options = [...root.querySelectorAll("[data-select-option]")];
    const empty = root.querySelector("[data-select-empty]");

    if (!trigger || !label || !value || !panel || !search || !empty) {
      return;
    }

    const visibleOptions = () => options.filter((option) => !option.hidden);

    const filter = () => {
      const term = search.value.trim().toLocaleLowerCase("vi");
      options.forEach((option) => {
        // An option ruled out by an owning filter (e.g. class-by-course) stays
        // hidden no matter what the search box says.
        option.hidden = option.dataset.excluded === "true"
          || !option.dataset.searchText.toLocaleLowerCase("vi").includes(term);
      });
      empty.hidden = visibleOptions().length !== 0;
    };

    const open = () => {
      panel.setAttribute("aria-hidden", "false");
      trigger.setAttribute("aria-expanded", "true");
      root.classList.add("is-open");
      search.value = "";
      filter();
      window.requestAnimationFrame(() => search.focus());
    };

    const close = (restoreFocus = false) => {
      panel.setAttribute("aria-hidden", "true");
      trigger.setAttribute("aria-expanded", "false");
      root.classList.remove("is-open");
      if (restoreFocus) {
        trigger.focus();
      }
    };

    // Redraws the trigger and the ticks from whatever the hidden select holds.
    // A script that sets .value itself (a reset button) calls this by
    // dispatching a "sync" event on the select — plain assignment fires nothing.
    const placeholderText = value.querySelector('option[value=""]')?.textContent ?? "";
    const sync = () => {
      const current = options.find((item) => item.dataset.value === value.value);
      label.textContent = current ? current.dataset.label : placeholderText;
      label.classList.toggle("is-placeholder", !current);
      options.forEach((item) => {
        const selected = item === current;
        item.classList.toggle("is-selected", selected);
        item.setAttribute("aria-selected", selected ? "true" : "false");
      });
    };
    value.addEventListener("sync", sync);
    value.addEventListener("change", sync);
    value.form?.addEventListener("reset", () => window.setTimeout(sync));

    const select = (option) => {
      value.value = option.dataset.value;
      sync();
      value.dispatchEvent(new Event("change", { bubbles: true }));
      close(true);
    };

    trigger.addEventListener("click", () => {
      if (!root.classList.contains("is-open")) {
        open();
      } else {
        close();
      }
    });

    search.addEventListener("input", filter);
    search.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        close(true);
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        visibleOptions()[0]?.focus();
      } else if (event.key === "Enter") {
        const first = visibleOptions()[0];
        if (first) {
          event.preventDefault();
          select(first);
        }
      }
    });

    options.forEach((option, index) => {
      option.addEventListener("click", () => select(option));
      option.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          close(true);
          return;
        }

        if (event.key !== "ArrowDown" && event.key !== "ArrowUp") {
          return;
        }

        event.preventDefault();
        const visible = visibleOptions();
        const current = visible.indexOf(options[index]);
        const step = event.key === "ArrowDown" ? 1 : -1;
        visible[(current + step + visible.length) % visible.length]?.focus();
      });
    });

    document.addEventListener("mousedown", (event) => {
      if (!root.contains(event.target)) {
        close();
      }
    });
  };

  // Convert the remaining plain Bootstrap selects to the same predictable picker.
  // Native Windows/Chromium popups can clip long Vietnamese labels and render outside
  // the control. The original select stays in the form as the source of truth.
  const upgradeNativeSelect = (value) => {
    if (value.multiple || value.size > 1 || value.dataset.nativeSelect === "true"
        || value.closest("[data-select-picker]")) return;

    const root = document.createElement("div");
    root.className = "select-picker select-picker--plain";
    root.dataset.selectPicker = "";

    const trigger = document.createElement("button");
    trigger.type = "button";
    trigger.className = "select-picker__trigger";
    trigger.dataset.selectTrigger = "";
    trigger.setAttribute("aria-haspopup", "listbox");
    trigger.setAttribute("aria-expanded", "false");

    const label = document.createElement("span");
    label.className = "select-picker__value";
    label.dataset.selectLabel = "";
    const chevron = document.createElement("span");
    chevron.className = "select-picker__chevron";
    chevron.setAttribute("aria-hidden", "true");
    trigger.append(label, chevron);

    const panel = document.createElement("div");
    panel.className = "select-picker__panel";
    panel.dataset.selectPanel = "";
    panel.setAttribute("role", "listbox");
    panel.setAttribute("aria-hidden", "true");

    const searchWrap = document.createElement("div");
    searchWrap.className = "select-picker__search-wrap select-picker__search-wrap--full";
    const search = document.createElement("input");
    search.type = "search";
    search.className = "select-picker__search";
    search.placeholder = "Tìm trong danh sách";
    search.autocomplete = "off";
    search.dataset.selectSearch = "";
    search.setAttribute("aria-label", "Tìm trong danh sách");
    searchWrap.append(search);

    const optionsRoot = document.createElement("div");
    optionsRoot.className = "select-picker__options";
    optionsRoot.dataset.selectOptions = "";
    [...value.options].forEach((nativeOption) => {
      const option = document.createElement("button");
      option.type = "button";
      option.className = "select-picker__option";
      option.dataset.selectOption = "";
      option.dataset.value = nativeOption.value;
      option.dataset.label = nativeOption.text;
      option.dataset.searchText = nativeOption.text;
      option.setAttribute("role", "option");
      option.disabled = nativeOption.disabled;

      const identity = document.createElement("span");
      identity.className = "select-picker__identity";
      const text = document.createElement("strong");
      text.textContent = nativeOption.text;
      identity.append(text);
      const check = document.createElement("span");
      check.className = "select-picker__check";
      check.setAttribute("aria-hidden", "true");
      check.textContent = "✓";
      option.append(identity, check);
      optionsRoot.append(option);
    });

    const empty = document.createElement("div");
    empty.className = "select-picker__empty";
    empty.dataset.selectEmpty = "";
    empty.hidden = true;
    empty.textContent = "Không tìm thấy lựa chọn phù hợp";
    optionsRoot.append(empty);
    panel.append(searchWrap, optionsRoot);

    value.before(root);
    value.classList.remove("form-select");
    value.classList.add("visually-hidden");
    value.tabIndex = -1;
    value.dataset.selectValue = "";
    root.append(trigger, value, panel);
    setupSelectPicker(root);
    value.dispatchEvent(new Event("sync"));
  };

  document.addEventListener("DOMContentLoaded", () => {
    setupAll();
    document.querySelectorAll("select.form-select").forEach(upgradeNativeSelect);
    document.querySelectorAll("[data-select-picker]").forEach(setupSelectPicker);
  });

  // Rows added after load (the "Thêm sinh viên" button) need wiring too.
  window.AivesCombobox = { setupAll };
})();
