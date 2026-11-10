const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

// The virtual examiner seen from the outside: given a new question or probe it reads it
// aloud and then opens the microphone, never the other way round, and it stays quiet
// whenever opening the microphone would surprise the student.

const flush = async () => { for (let i = 0; i < 6; i++) await new Promise(resolve => setImmediate(resolve)); };

function harness({ modalHidden = true, stored = null, heard = true, recordingStarts = true } = {}) {
  const elements = new Map();
  class Element extends EventTarget {
    constructor() { super(); this.textContent = ""; this.hidden = false; this.checked = false; this.dataset = {}; }
  }
  const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
  // What voice-mic-check.js publishes, synchronously, when it opens or closes the check.
  get("voice-room").dataset.micCheck = modalHidden ? "closed" : "open";
  const calls = [], spoken = [];
  let recording = false;
  const capture = {
    // Handing in an answer stops the recorder before the next item arrives.
    async speak() { calls.push("speak"); recording = false; return heard; },
    async startRecording() { calls.push("record"); recording = recordingStarts; },
    get recording() { return recording; },
    get reading() { return false; }
  };
  const store = new Map(stored ? [["voice_examiner_auto", stored]] : []);
  const window = { voiceCapture: capture, speechSynthesis: { speak: value => spoken.push(value.text) },
    SpeechSynthesisUtterance: class { constructor(text) { this.text = text; } } };
  const document = { getElementById: get };
  const context = { window, document, CustomEvent, Date, Promise,
    localStorage: { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value) },
    setTimeout: callback => { callback(); return 0; } };
  vm.runInNewContext(readFileSync(join(__dirname,
    "../../src/AssignmentPRN.Presentation/UI Assets/js/voice-examiner.js"), "utf8"), context);
  const room = get("voice-room");
  return {
    calls, spoken, store, get,
    item(detail) { room.dispatchEvent(new CustomEvent("voice-item-changed", { detail })); },
    micReady() { room.dataset.micCheck = "closed"; room.dispatchEvent(new CustomEvent("voice-mic-ready")); }
  };
}

const open = (overrides = {}) => ({ examQuestionId: 10, orderNo: 1, text: "Lock dùng để làm gì?", transcript: "",
  hasAudio: false, deadline: new Date(Date.now() + 60000).toISOString(), ...overrides });

test("a new question is read aloud first, then the microphone opens", async () => {
  const room = harness();
  room.item({ question: open(), completed: false, initial: true });
  await flush();
  assert.deepEqual(room.calls, ["speak", "record"]);
  assert.match(room.get("voice-examiner-status").textContent, /ghi âm/);
});

test("a probe is asked the same way, as a new item on the same question", async () => {
  const room = harness();
  room.item({ question: open(), completed: false, initial: true });
  await flush();
  room.item({ question: open({ followUpId: 7, text: "Em nói rõ hơn được không?" }), completed: false });
  await flush();
  assert.deepEqual(room.calls, ["speak", "record", "speak", "record"]);
});

test("nothing is said while the microphone check is still open", async () => {
  const room = harness({ modalHidden: false });
  room.item({ question: open(), completed: false, initial: true });
  await flush();
  assert.deepEqual(room.calls, []);
  room.micReady();
  await flush();
  assert.deepEqual(room.calls, ["speak", "record"]);
});

test("a question that could not be read aloud never opens the microphone by itself", async () => {
  const room = harness({ heard: false });
  room.item({ question: open(), completed: false, initial: true });
  await flush();
  assert.deepEqual(room.calls, ["speak"]);
  assert.match(room.get("voice-examiner-status").textContent, /Bắt đầu trả lời/);
});

test("an answer already under way after a reload is not started over", async () => {
  const room = harness();
  room.item({ question: open({ transcript: "Em đang nói dở" }), completed: false, initial: true });
  await flush();
  assert.deepEqual(room.calls, []);
});

test("a question about to run out is read but the microphone stays closed", async () => {
  const room = harness();
  room.item({ question: open({ deadline: new Date(Date.now() + 1000).toISOString() }), completed: false, initial: true });
  await flush();
  assert.deepEqual(room.calls, []);
});

test("switched off, the examiner leaves both buttons to the student and remembers it", async () => {
  const room = harness({ stored: "off" });
  assert.equal(room.get("voice-examiner-auto").checked, false);
  room.item({ question: open(), completed: false, initial: true });
  await flush();
  assert.deepEqual(room.calls, []);

  room.get("voice-examiner-auto").checked = true;
  room.get("voice-examiner-auto").dispatchEvent(new Event("change"));
  assert.equal(room.store.get("voice_examiner_auto"), "on");
});

test("a microphone that does not start is reported, not hidden", async () => {
  const room = harness({ recordingStarts: false });
  room.item({ question: open(), completed: false, initial: true });
  await flush();
  assert.match(room.get("voice-examiner-status").textContent, /Chưa bật được microphone/);
});

test("finishing the viva gets a closing line, a reload of a finished viva does not", async () => {
  const room = harness();
  room.item({ question: null, completed: true, finishedNow: true });
  room.item({ question: null, completed: true, finishedNow: false, initial: true });
  assert.equal(room.spoken.length, 1);
  assert.match(room.spoken[0], /kết thúc/);
});

// ---- Thứ tự tải script thật trên trang ------------------------------------------------
// voice-examiner.js loads BEFORE voice-mic-check.js. On 69bfdf9 the examiner looked at the
// dialog once, at load, saw the hidden attribute from the markup and assumed the check
// was over; the check then opened, and the examiner spoke and opened the microphone on
// top of it. These tests load both real scripts in page order.

function page({ heard = true } = {}) {
  const elements = new Map();
  class ClassList { constructor() { this.set = new Set(); } add(c) { this.set.add(c); } remove(c) { this.set.delete(c); } contains(c) { return this.set.has(c); } }
  class Element extends EventTarget {
    constructor() {
      super(); this.textContent = ""; this.innerHTML = ""; this.checked = true; this.disabled = false;
      this.dataset = {}; this.classList = new ClassList(); this.attributes = new Map(); this.hidden = false; this.style = {};
    }
    setAttribute(name, value) { this.attributes.set(name, value); if (name === "hidden") this.hidden = true; }
    removeAttribute(name) { this.attributes.delete(name); if (name === "hidden") this.hidden = false; }
    querySelector() { return { dataset: { candidate: "1" } }; }
    querySelectorAll() { return []; }
    focus() {}
  }
  const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
  get("voice-mic-modal").setAttribute("hidden", "");   // as rendered by Index.cshtml
  const calls = [];
  let recording = false;
  const window = new EventTarget();
  window.voiceCapture = {
    async speak() { calls.push("speak"); recording = false; return heard; },
    async startRecording() { calls.push("record"); recording = true; },
    get recording() { return recording; }, get reading() { return false; }
  };
  const store = new Map();
  const timers = [];
  const context = { window, document: { getElementById: get, querySelectorAll: () => [] }, CustomEvent, Event, Date, Promise,
    localStorage: { getItem: () => null, setItem: () => {} },
    sessionStorage: { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value) },
    navigator: {}, requestAnimationFrame: () => 0, cancelAnimationFrame: () => {}, clearTimeout: () => {},
    setTimeout: (callback, ms) => { if (!ms || ms > 1000) callback(); else timers.push(callback); return 0; } };
  context.window.voiceMicCheck = undefined;
  for (const script of ["voice-examiner.js", "voice-mic-check.js"])
    vm.runInNewContext(readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js", script), "utf8"), context);
  const runTimers = () => { while (timers.length) timers.shift()(); };
  const room = get("voice-room");
  return {
    calls, get, runTimers, check: context.window.voiceMicCheck,
    item(detail) { room.dispatchEvent(new CustomEvent("voice-item-changed", { detail })); },
    click(id) { get(id).dispatchEvent(new Event("click")); }
  };
}

test("in page order, nothing is read or recorded while the microphone check is open", async () => {
  const p = page();
  p.check.open();                       // what voice-room.js does while the first question waits
  p.item({ question: open(), completed: false, initial: false });
  await flush(); p.runTimers(); await flush();
  assert.deepEqual(p.calls, []);
  assert.equal(p.get("voice-mic-modal").hidden, false);

  p.click("voice-mic-skip-btn");        // the student is done with the check
  await flush(); p.runTimers(); await flush();
  assert.deepEqual(p.calls, ["speak", "record"]);
});

test("in page order, reopening the check mid-question keeps the microphone closed until it is shut", async () => {
  const p = page();
  p.item({ question: open(), completed: false, initial: false });
  await flush(); p.runTimers(); await flush();
  assert.deepEqual(p.calls, ["speak", "record"]);

  p.click("voice-mic-retest-btn");
  p.item({ question: open({ followUpId: 3, text: "Em nói rõ hơn?" }), completed: false });
  await flush(); p.runTimers(); await flush();
  assert.deepEqual(p.calls, ["speak", "record"]);

  p.click("voice-mic-confirm-btn");
  await flush(); p.runTimers(); await flush();
  assert.deepEqual(p.calls, ["speak", "record", "speak", "record"]);
});
