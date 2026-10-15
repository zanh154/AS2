(() => {
  "use strict";
  const identity = document.getElementById("voice-identity");
  if (!identity) return;
  const scope = `${identity.dataset.user}/${identity.dataset.candidate}`;
  let opening;
  const open = () => opening ||= new Promise((resolve, reject) => {
    if (!window.indexedDB) { reject(new Error("IndexedDB unavailable")); return; }
    const request = indexedDB.open("aives-voice-recovery", 1);
    request.onupgradeneeded = () => {
      const store = request.result.createObjectStore("records", { keyPath: "key" });
      store.createIndex("scope", "scope");
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error("Browser recovery storage blocked"));
  });
  const warn = () => {
    document.getElementById("voice-local-status").textContent = "Trình duyệt không lưu được bản dự phòng. Giữ trang mở, lưu lên máy chủ hoặc tải bản thu về máy trước khi F5.";
    const badge = document.getElementById("voice-local-badge"); if (badge) badge.textContent = "Chưa lưu được";
    const room = document.getElementById("voice-room"); if (room) room.dataset.localState = "error";
  };
  window.voiceRecovery = {
    async put(record) {
      try {
        const db = await open();
        await new Promise((resolve, reject) => {
          const tx = db.transaction("records", "readwrite");
          tx.objectStore("records").put({ ...record, key: `${scope}/${record.id}`, scope, updatedAt: Date.now() });
          tx.oncomplete = resolve; tx.onerror = tx.onabort = () => reject(tx.error);
        });
        const badge = document.getElementById("voice-local-badge"); if (badge) badge.textContent = "Đã giữ trên thiết bị";
        const room = document.getElementById("voice-room");
        if (room?.dataset.localState === "error") document.getElementById("voice-local-status").textContent = "Đã giữ bản dự phòng. Bạn có thể tiếp tục khi tải lại trang.";
        if (room) room.dataset.localState = "saved";
        return true;
      } catch { warn(); return false; }
    },
    async list() {
      try {
        const db = await open();
        const records = await new Promise((resolve, reject) => {
          const request = db.transaction("records").objectStore("records").index("scope").getAll(scope);
          request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error);
        });
        const badge = document.getElementById("voice-local-badge");
        if (badge) badge.textContent = records.length ? "Đã giữ trên thiết bị" : "Sẵn sàng lưu";
        return records;
      } catch { warn(); return []; }
    }
  };
})();
