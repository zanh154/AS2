(() => {
  const script = document.currentScript;
  if (!script) return;
  const id = Number(script.dataset.documentId);
  const status = script.dataset.documentStatus;
  let dirty = false;
  let checking = false;
  let reloading = false;
  let pollTimer;
  document.addEventListener('input', event => { if (event.isTrusted) dirty = true; });
  const reloadOnce = () => {
    if (dirty || reloading) return;
    reloading = true;
    clearInterval(pollTimer);
    // A slow navigation must not be cancelled by the next poll or SignalR event.
    location.reload();
  };
  const check = async () => {
    if (checking || dirty || reloading) return;
    checking = true;
    try {
      const url = new URL(location.href);
      url.searchParams.set('handler', 'Status');
      const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (!response.ok || response.redirected) return;
      const next = await response.json();
      if (next.documentId === id && next.status && next.status !== status) reloadOnce();
    } finally { checking = false; }
  };
  // Polling also works if the SignalR library cannot load or a connection drops.
  if (status === 'Pending' || status === 'Processing') pollTimer = setInterval(() => check().catch(() => {}), 5000);
  if (!window.signalR) return;
  const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/document-processing').withAutomaticReconnect().build();
  connection.on('DocumentProgress', event => { if (event.documentId === id && event.status !== status) reloadOnce(); });
  const join = () => connection.invoke('JoinDocumentGroup', id);
  connection.onreconnected(async () => { try { await join(); await check(); } catch { /* Polling remains active. */ } });
  const start = async () => {
    try { await connection.start(); await join(); }
    catch { await connection.stop().catch(() => {}); setTimeout(start, 5000); }
  };
  start();
})();
