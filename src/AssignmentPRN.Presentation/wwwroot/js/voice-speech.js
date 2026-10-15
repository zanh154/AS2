(() => {
  "use strict";
  // Replaceable STT adapter: supported, start({onText,onError}), stop().
  // Web Speech may use the browser vendor's remote recognition service.
  class BrowserSpeechToTextAdapter {
    get supported() { return !!(window.SpeechRecognition || window.webkitSpeechRecognition); }
    start({ onText, onError }) {
      if (!this.supported) { onError("Trình duyệt chưa hỗ trợ STT. Bản thu vẫn được lưu; hãy nhập nội dung trả lời."); return; }
      const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      this.recognition = new Recognition();
      this.recognition.lang = "vi-VN";
      this.recognition.continuous = true;
      this.recognition.interimResults = true;
      this.recognition.onresult = event => {
        let text = "";
        for (let i = 0; i < event.results.length; i++) text += event.results[i][0].transcript + " ";
        onText(text.trim());
      };
      this.recognition.onerror = event => onError(`STT: ${event.error}. Có thể nhập/sửa nội dung và tiếp tục thu âm.`);
      this.recognition.onend = () => onError("STT đã dừng. Kiểm tra nội dung nhận dạng trước khi nộp; bản thu vẫn tiếp tục nếu chưa bấm Dừng.");
      try { this.recognition.start(); } catch { onError("Không khởi động được STT; hãy nhập nội dung trả lời."); }
    }
    stop() {
      if (this.recognition) {
        this.recognition.onend = null;
        this.recognition.stop();
      }
    }
  }
  window.createVoiceSpeechAdapter = () => new BrowserSpeechToTextAdapter();
})();
