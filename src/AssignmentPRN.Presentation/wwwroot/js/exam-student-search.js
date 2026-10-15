(() => {
  const roster = document.querySelector('[data-roster-filter]');
  if (roster) {
    const query = roster.querySelector('[data-roster-query]');
    const status = roster.querySelector('[data-roster-status]');
    let timer;
    const filter = () => {
      const rows = [...roster.querySelectorAll('[data-roster-row]')];
      const term = query.value.trim().toLocaleLowerCase('vi');
      let count = 0;
      rows.forEach(row => {
        row.hidden = !row.dataset.search.toLocaleLowerCase('vi').includes(term)
          || (status.value !== '' && row.dataset.status !== status.value);
        if (!row.hidden) count++;
      });
      roster.querySelector('[data-roster-count]').textContent = `${count}/${rows.length} sinh viên`;
      roster.querySelector('[data-roster-empty]').hidden = count > 0 || rows.length === 0;
    };
    roster.addEventListener('live-content-updated', filter);
    query.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(filter, 300); });
    status.addEventListener('change', filter);
    roster.querySelector('[data-roster-reset]').addEventListener('click', () => {
      clearTimeout(timer);
      query.value = '';
      status.value = '';
      status.dispatchEvent(new Event('sync'));
      filter();
      query.focus();
    });
  }
  const root = document.querySelector('[data-exam-student-search]');
  if (!root) return;
  const form = root.querySelector('[data-search-form]');
  const results = root.querySelector('[data-search-results]');
  const message = root.querySelector('[data-search-message]');
  let pending, version = 0;
  // Drops whatever is still in flight, so a slow earlier reply cannot land
  // on top of a newer one.
  const invalidate = () => { pending?.abort(); version++; };
  const load = async url => {
    invalidate();
    const ticket = version;
    pending = new AbortController();
    message.textContent = 'Đang tìm kiếm…';
    results.setAttribute('aria-busy', 'true');
    try {
      const response = await fetch(url, { signal: pending.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (response.redirected) { window.location.assign(response.url); return; }
      if (!response.ok) throw new Error('Search failed');
      const html = await response.text();
      if (ticket !== version) return;
      results.innerHTML = html;
      history.replaceState(null, '', url);
      message.textContent = '';
    } catch (error) {
      if (ticket === version && error.name !== 'AbortError') message.textContent = 'Không thể tải kết quả. Bấm Tìm kiếm để thử lại.';
    } finally {
      if (ticket === version) results.removeAttribute('aria-busy');
    }
  };
  const search = () => {
    if (!form.reportValidity()) { results.removeAttribute('aria-busy'); message.textContent = ''; return; }
    load(`${form.action}?${new URLSearchParams(new FormData(form))}`);
  };
  form.addEventListener('submit', event => { event.preventDefault(); search(); });
  results.addEventListener('click', event => {
    const link = event.target.closest('[data-search-page]');
    if (link && !event.ctrlKey && !event.metaKey && !event.shiftKey) { event.preventDefault(); load(link.href); }
  });
})();
