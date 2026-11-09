const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

function loadVoiceCapture() {
  const elements = new Map();
  class Element extends EventTarget {
    constructor(id) { super(); this.dataset = {}; this.children = []; this.value = ""; this.textContent = ""; this.id = id; }
    set id(value) { this._id = value; if (value) elements.set(value, this); }
    get id() { return this._id; }
    append(...items) { this.children.push(...items); }
    replaceChildren() { this.children = []; }
    querySelector() { return { value: "csrf" }; }
  }
  for (const id of ["voice-room", "voice-capture-status", "voice-record", "voice-stop", "voice-upload", "voice-read",
    "voice-preview", "voice-transcript", "voice-local-recordings", "voice-download-local", "voice-stt-status", "voice-capture", "voice-stored-audio"]) {
    new Element(id);
  }
  elements.get("voice-room").dataset.upload = "/upload";
  elements.get("voice-room").dataset.audio = "/audio";

  const window = new EventTarget();
  let spokenUtterance = null;
  const voices = [
    { name: "Microsoft HoaiMy Online (Natural) - Vietnamese (Vietnam)", lang: "vi-VN" },
    { name: "Microsoft NamMinh Online (Natural) - Vietnamese (Vietnam)", lang: "vi-VN" },
    { name: "Microsoft Jenny Online (Natural) - English (United States)", lang: "en-US" },
    { name: "Microsoft David - English (United States)", lang: "en-US" }
  ];
  const speech = {
    cancel() {},
    getVoices: () => voices,
    speak(value) { spokenUtterance = value; }
  };
  class Recorder {
    static isTypeSupported() { return true; }
    constructor() { this.state = "inactive"; }
    start() { this.state = "recording"; }
    stop() { this.state = "inactive"; }
  }
  Object.assign(window, {
    MediaRecorder: Recorder,
    SpeechSynthesisUtterance: class {
      constructor(text) { this.text = text; }
    },
    speechSynthesis: speech,
    createVoiceSpeechAdapter: () => ({ start() {}, stop() {} }),
    voiceRecovery: { put: async () => true, list: async () => [] }
  });

  const context = {
    window,
    document: { getElementById: id => elements.get(id), createElement: () => new Element() },
    navigator: { mediaDevices: { async getUserMedia() { return { getTracks: () => [] }; } } },
    MediaRecorder: Recorder,
    SpeechSynthesisUtterance: window.SpeechSynthesisUtterance,
    speechSynthesis: speech,
    performance,
    Blob,
    FormData,
    URL,
    location: { href: "https://example.test/VoiceRoom/1" },
    Date,
    Math,
    Event,
    CustomEvent,
    AbortSignal,
    setInterval() {},
    fetch: async () => ({ ok: true, json: async () => ({}) })
  };

  vm.runInNewContext(readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js/voice-capture.js"), "utf8"), context);

  return {
    window,
    elements,
    speech,
    getSpoken: () => spokenUtterance
  };
}

test("detects Vietnamese questions and uses Vietnamese voice", async () => {
  const harness = loadVoiceCapture();
  const detectLanguage = harness.window.voiceCapture.detectLanguage;
  assert.equal(detectLanguage("Em hãy giải thích khái niệm khóa chính (Primary Key)?"), "vi-VN");
  assert.equal(detectLanguage("Nêu sự khác biệt giữa inner join và outer join trong SQL Server?"), "vi-VN");
  assert.equal(detectLanguage("Cho biết các dạng chuẩn hóa cơ sở dữ liệu?"), "vi-VN");

  await harness.window.voiceCapture.setState({
    serverNow: new Date().toISOString(),
    completed: false,
    question: {
      examQuestionId: 101,
      text: "Em hãy nêu định nghĩa và vai trò của ràng buộc khóa ngoại (Foreign Key)?",
      deadline: new Date(Date.now() + 60000).toISOString()
    }
  });

  harness.elements.get("voice-read").dispatchEvent(new Event("click"));
  const spoken = harness.getSpoken();
  assert.ok(spoken, "Utterance should be created and spoken");
  assert.equal(spoken.lang, "vi-VN", "Language should be vi-VN for Vietnamese question");
  assert.ok(spoken.voice, "Voice should be selected");
  assert.equal(spoken.voice.lang, "vi-VN", "Voice language must match vi-VN");
  assert.match(spoken.voice.name, /HoaiMy|NamMinh/, "Natural Vietnamese voice should be prioritized");
});

test("detects English questions and uses English voice", async () => {
  const harness = loadVoiceCapture();
  const detectLanguage = harness.window.voiceCapture.detectLanguage;
  assert.equal(detectLanguage("Explain the difference between clustered and non-clustered indexes."), "en-US");
  assert.equal(detectLanguage("What is ACID properties in database management systems?"), "en-US");
  assert.equal(detectLanguage("Describe the normalization process from 1NF to 3NF."), "en-US");

  await harness.window.voiceCapture.setState({
    serverNow: new Date().toISOString(),
    completed: false,
    question: {
      examQuestionId: 102,
      text: "What is the purpose of an index in SQL Server? Explain how it improves query performance.",
      deadline: new Date(Date.now() + 60000).toISOString()
    }
  });

  harness.elements.get("voice-read").dispatchEvent(new Event("click"));
  const spoken = harness.getSpoken();
  assert.ok(spoken, "Utterance should be created and spoken");
  assert.equal(spoken.lang, "en-US", "Language should be en-US for English question");
  assert.ok(spoken.voice, "Voice should be selected");
  assert.equal(spoken.voice.lang, "en-US", "Voice language must match en-US");
  assert.match(spoken.voice.name, /Jenny|David/, "English voice should be chosen");
});
