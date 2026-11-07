(() => {
  "use strict";
  // Replaceable STT adapter: supported, start({onText,onError,onState}), async stop().
  // Web Speech may use the browser vendor's remote recognition service.
  class BrowserSpeechToTextAdapter {
    get supported() { return !!(window.SpeechRecognition || window.webkitSpeechRecognition); }
    start({ onText, onError, onState = () => {} }) {
      if (!this.supported) {
        const message = "Trình duyệt chưa hỗ trợ nhận dạng. Dùng Chrome/Edge có hỗ trợ hoặc nhập văn bản sau khi dừng thu.";
        onState("unsupported", message); onError(message); return;
      }
      this.token = (this.token || 0) + 1;
      try { this.recognition?.abort(); } catch { /* Previous session already ended. */ }
      this.callbacks = { onText, onError, onState };
      this.active = true; this.running = false; this.stopping = false;
      this.prefix = ""; this.text = ""; this.sessionText = ""; this.restarts = 0; this.failed = false;
      this.open(this.token);
    }
    open(token) {
      if (token !== this.token || !this.active) return;
      const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      const recognition = this.recognition = new Recognition();
      recognition.lang = "vi-VN";
      recognition.continuous = true;
      recognition.interimResults = true;
      this.sessionText = "";
      this.callbacks.onState("starting", "Đang kết nối dịch vụ nhận dạng tiếng Việt…");
      recognition.onstart = () => {
        if (token === this.token) this.callbacks.onState("listening", "Đang nghe tiếng Việt. Hãy nói rõ vào microphone.");
      };
      recognition.onresult = event => {
        if (token !== this.token) return;
        let text = "";
        for (let i = 0; i < event.results.length; i++) text += event.results[i][0].transcript + " ";
        this.sessionText = text.trim();
        this.text = [this.prefix, this.sessionText].filter(Boolean).join(" ");
        this.restarts = 0;
        this.callbacks.onText(this.text);
        this.callbacks.onState("recognized", "Đã nhận dạng thành văn bản. Nội dung đang được giữ tạm.");
      };
      recognition.onerror = event => {
        if (token !== this.token) return;
        const messages = {
          network: "Không kết nối được dịch vụ nhận dạng. Kiểm tra mạng; audio vẫn được thu và lưu, bạn có thể nhập văn bản sau khi dừng.",
          "not-allowed": "Quyền nhận dạng giọng nói bị từ chối. Cho phép microphone/nhận dạng trong trình duyệt rồi thử lại.",
          "service-not-allowed": "Trình duyệt không cho dùng dịch vụ nhận dạng. Thử Chrome hoặc kiểm tra chính sách trình duyệt.",
          "audio-capture": "Dịch vụ nhận dạng không truy cập được microphone.",
          "language-not-supported": "Dịch vụ này chưa hỗ trợ tiếng Việt; audio vẫn được lưu.",
          "no-speech": "Chưa nhận được lời nói. Hãy nói gần microphone hơn."
        };
        if (event.error === "aborted" && !this.active) return;
        if (event.error !== "no-speech") { this.active = false; this.failed = true; }
        const message = messages[event.error] || `Nhận dạng bị gián đoạn (${event.error}). Audio vẫn được giữ.`;
        this.callbacks.onState("error", message); this.callbacks.onError(message);
      };
      recognition.onend = () => {
        if (token !== this.token) return;
        this.running = false;
        this.prefix = this.text;
        if (this.active && !this.failed && this.restarts++ < 3) {
          this.callbacks.onState("reconnecting", "Đang tiếp tục nhận dạng…");
          this.restartTimer = setTimeout(() => this.open(token), 400);
        } else {
          this.active = false; this.finish();
        }
      };
      try { this.running = true; recognition.start(); }
      catch {
        this.running = false; this.active = false; this.failed = true;
        const message = "Không khởi động được nhận dạng. Audio vẫn được lưu; bấm Thử nhận dạng lại khi đang thu.";
        this.callbacks.onState("error", message); this.callbacks.onError(message); this.finish();
      }
    }
    stop() {
      this.active = false;
      clearTimeout(this.restartTimer);
      if (this.stopping) return this.stopPromise;
      if (!this.running) { this.finish(); return Promise.resolve(); }
      this.stopping = true;
      if (!this.failed) this.callbacks.onState("processing", "Đang hoàn tất phần lời nói cuối…");
      const pending = this.stopPromise = new Promise(resolve => { this.resolveStop = resolve; });
      // stop() can emit a final result before end; never submit ahead of that result.
      this.stopTimer = setTimeout(() => {
        this.token++; this.recognition?.abort(); this.finish();
      }, 2000);
      try { this.recognition.stop(); } catch { this.finish(); }
      return pending;
    }
    finish() {
      clearTimeout(this.stopTimer);
      this.running = false; this.stopping = false;
      if (this.callbacks && !this.failed) {
        this.callbacks.onState(this.text ? "ready" : "empty", this.text
          ? "Đã hoàn tất nhận dạng. Bạn có thể sửa văn bản trước khi nộp."
          : "Chưa nhận dạng được văn bản. Bản thu vẫn được giữ; có thể thu lại hoặc nhập nội dung trả lời.");
      }
      this.resolveStop?.(); this.resolveStop = null;
    }
  }
  window.createVoiceSpeechAdapter = () => new BrowserSpeechToTextAdapter();
})();
