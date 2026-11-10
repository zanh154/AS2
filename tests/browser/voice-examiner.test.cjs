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
  get("voice-mic-modal").hidden = modalHidden;
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
    micReady() { room.dispatchEvent(new CustomEvent("voice-mic-ready")); }
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
