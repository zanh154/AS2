const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");
const turn = () => new Promise(resolve => setImmediate(resolve));

function harness({ denied = false } = {}) {
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
    "voice-preview", "voice-transcript", "voice-local-recordings", "voice-download-local", "voice-stt-status", "voice-capture", "voice-stored-audio"])
    new Element(id);
  const get = id => elements.get(id);
  get("voice-room").dataset.upload = "/upload"; get("voice-room").dataset.audio = "/audio";
  const window = new EventTarget(), intervals = [], uploads = [], local = [], recorders = [];
  let adapterCallbacks, utterance, released = 0;
  const speech = { cancel() {}, getVoices: () => [{ lang: "vi-VN" }], speak(value) { utterance = value; } };
  class Recorder {
    static isTypeSupported(type) { return type === "audio/webm;codecs=opus"; }
    constructor() { this.state = "inactive"; recorders.push(this); }
    start() { this.state = "recording"; }
    stop() {
      this.state = "inactive";
      queueMicrotask(() => {
        this.ondataavailable({ data: new Blob(["fake audio for a mocked recorder"], { type: "audio/webm" }) });
        this.onstop();
      });
    }
  }
  const track = { stop() { released++; } };
  const navigator = { mediaDevices: { async getUserMedia() {
    if (denied) throw Object.assign(new Error("denied"), { name: "NotAllowedError" });
    return { getTracks: () => [track] };
  } } };
  const document = { getElementById: get, createElement: () => new Element() };
  Object.assign(window, { MediaRecorder: Recorder, SpeechSynthesisUtterance: class {}, speechSynthesis: speech,
    createVoiceSpeechAdapter: () => ({ start(callbacks) { adapterCallbacks = callbacks; }, stop() {} }),
    voiceRecovery: { put: async record => { local.push(record); return true; }, list: async () => [] } });
  const context = { window, document, navigator, MediaRecorder: Recorder, SpeechSynthesisUtterance: window.SpeechSynthesisUtterance,
    speechSynthesis: speech, performance, Blob, FormData, URL, location: { href: "https://example.test/VoiceRoom/1" },
    Date, Math, Event, CustomEvent, AbortSignal,
    setInterval(callback) { intervals.push(callback); },
    fetch: async (url, options) => { uploads.push(options.body); return { ok: true, json: async () => ({}) }; } };
  vm.runInNewContext(readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/wwwroot/js/voice-capture.js"), "utf8"), context);
  const state = id => ({ serverNow: new Date().toISOString(), completed: false,
    question: { examQuestionId: id, text: `Question ${id}`, deadline: new Date(Date.now() + 30000).toISOString() } });
  return { get, window, recorders, uploads, local, state, intervals, released: () => released,
    callbacks: () => adapterCallbacks, utterance: () => utterance };
}

test("TTS disables recording, then stop uploads audio and releases microphone", async () => {
  const h = harness(); await h.window.voiceCapture.setState(h.state(10));
  h.get("voice-read").dispatchEvent(new Event("click"));
  assert.equal(h.get("voice-record").disabled, true);
  assert.equal(h.utterance().lang, "vi-VN");
  h.utterance().onend();
  assert.equal(h.get("voice-record").disabled, false);
  h.get("voice-record").dispatchEvent(new Event("click")); await turn();
  assert.equal(h.recorders[0].state, "recording");
  assert.equal(h.get("voice-read").disabled, true);
  h.callbacks().onText("xin chào"); assert.equal(h.get("voice-transcript").value, "xin chào");
  await h.window.voiceCapture.beforeSubmit();
  assert.equal(h.uploads.length, 1);
  assert.equal(h.uploads[0].get("questionId"), "10");
  assert.ok(h.released() > 0);
  assert.ok(h.local.some(record => record.kind === "audio" && record.synced));
});

test("microphone denial stays recoverable and starts no recorder", async () => {
  const h = harness({ denied: true }); await h.window.voiceCapture.setState(h.state(10));
  h.get("voice-record").dispatchEvent(new Event("click")); await turn();
  assert.match(h.get("voice-capture-status").textContent, /chưa cho phép/);
  assert.equal(h.recorders.length, 0);
  assert.equal(h.get("voice-record").disabled, false);
});

test("late speech callbacks cannot overwrite the next question's transcript", async () => {
  const h = harness(); await h.window.voiceCapture.setState(h.state(10));
  h.get("voice-record").dispatchEvent(new Event("click")); await turn();
  const old = h.callbacks();
  await h.window.voiceCapture.setState(h.state(11)); await turn();
  h.get("voice-transcript").value = "new question draft";
  old.onText("late result for old question");
  assert.equal(h.get("voice-transcript").value, "new question draft");
  assert.equal(h.get("voice-record").disabled, false, "an old recording must not block the next question");
});
