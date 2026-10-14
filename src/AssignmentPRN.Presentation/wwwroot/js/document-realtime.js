(() => {
  const script = document.currentScript;
  if (!window.signalR || !script) return;
  const id = Number(script.dataset.documentId);
  let status = script.dataset.documentStatus;
  const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/document-processing').withAutomaticReconnect().build();
  let dirty = false;
  document.addEventListener('input', () => { dirty = true; });
  const check = async () => {
    const response = await fetch(location.href, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    if (!response.ok || response.redirected) return;
    const html = new DOMParser().parseFromString(await response.text(), 'text/html');
    const next = html.querySelector('[data-document-status]')?.dataset.documentStatus;
    if (next && next !== status && !dirty) location.reload();
  };
  connection.on('DocumentProgress', event => { if (event.documentId === id && event.status !== status && !dirty) location.reload(); });
  const join = () => connection.invoke('JoinDocumentGroup', id);
  connection.onreconnected(async () => { await join(); await check(); });
  const start = async () => { try { await connection.start(); await join(); } catch { setTimeout(start, 5000); } };
  start();
  if (status === 'Pending' || status === 'Processing') setInterval(() => check().catch(() => {}), 5000);
})();
