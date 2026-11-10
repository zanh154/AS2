const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

function loadMicCheck({ hasTested = false, denied = false } = {}) {
  const elements = new Map();
  class MockClassList {
    constructor() { this.classes = new Set(); }
    add(cls) { this.classes.add(cls); }
    remove(cls) { this.classes.delete(cls); }
    contains(cls) { return this.classes.has(cls); }
  }

  class Element extends EventTarget {
    constructor(id) {
      super();
      this.dataset = {};
      this.children = [];
      this.classList = new MockClassList();
      this.value = "";
      this.textContent = "";
      this.innerHTML = "";
      this.id = id;
      this.hidden = false;
      this.disabled = false;
      this.style = {};
      this.attributes = new Map();
    }
    set id(value) { this._id = value; if (value) elements.set(value, this); }
    get id() { return this._id; }
    setAttribute(name, val) { this.attributes.set(name, val); if (name === "hidden") this.hidden = true; }
    removeAttribute(name) { this.attributes.delete(name); if (name === "hidden") this.hidden = false; }
    querySelectorAll() { return [new Element(), new Element()]; }
    querySelector() { return { dataset: { candidate: "118" } }; }
    focus() {}
  }

  for (const id of [
    "voice-room", "voice-mic-modal", "voice-mic-start-btn", "voice-mic-confirm-btn",
    "voice-mic-skip-btn", "voice-mic-retest-btn", "voice-mic-status-indicator",
    "voice-mic-status-text", "voice-mic-level-fill", "voice-mic-playback-wrap",
    "voice-mic-playback", "voice-identity"
  ]) {
    new Element(id);
  }
  elements.get("voice-identity").dataset.candidate = "118";

  const storage = new Map();
  if (hasTested) storage.set("voice_mic_tested_118", "1");

  const window = new EventTarget();
  let mediaTracksReleased = 0;
  const track = { stop() { mediaTracksReleased++; }, onended: null };
  const stream = { getTracks: () => [track] };

  class MockAudioContext {
    createAnalyser() {
      return {
        fftSize: 64,
        frequencyBinCount: 32,
        getByteFrequencyData(arr) { arr.fill(50); }
      };
    }
    createMediaStreamSource() { return { connect() {} }; }
    async close() {}
  }

  class MockMediaRecorder {
    static isTypeSupported() { return true; }
    constructor() { this.state = "inactive"; }
    start() { this.state = "recording"; }
    stop() {
      this.state = "inactive";
      this.ondataavailable?.({ data: new Blob(["dummy test audio"]) });
      this.onstop?.();
    }
  }

  const navigator = {
    mediaDevices: {
      async getUserMedia() {
        if (denied) {
          const err = new Error("Permission denied");
          err.name = "NotAllowedError";
          throw err;
        }
        return stream;
      }
    }
  };

  const context = {
    window,
    document: {
      getElementById: id => elements.get(id),
      querySelectorAll: () => []
    },
    navigator,
    AudioContext: MockAudioContext,
    MediaRecorder: MockMediaRecorder,
    sessionStorage: {
      getItem: key => storage.get(key) || null,
      setItem: (key, val) => storage.set(key, val)
    },
    // unref: the level-meter loop runs for as long as a test leaves the check open, and must
    // not keep the test process alive after the last test.
    requestAnimationFrame: cb => setTimeout(cb, 10).unref(),
    cancelAnimationFrame: () => {},
    setTimeout,
    clearTimeout,
    Blob,
    URL: { createObjectURL: () => "blob:test", revokeObjectURL: () => {} },
    Event,
    CustomEvent
  };

  vm.runInNewContext(readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js/voice-mic-check.js"), "utf8"), context);

  return {
    window,
    elements,
    storage,
    getReleasedTracks: () => mediaTracksReleased
  };
}

test("does not open by itself: the room opens it while the first question waits", () => {
  // Opening it on load would also open it mid-question after a reload, and the clock of
  // that question would run while the student tests the microphone.
  const harness = loadMicCheck({ hasTested: false });
  const room = harness.elements.get("voice-room");
  assert.equal(room.dataset.micCheck, "closed");

  harness.window.voiceMicCheck.open();
  assert.equal(room.dataset.micCheck, "open");
  assert.equal(harness.elements.get("voice-mic-modal").hidden, false);
});

test("closing the check says so at once and tells the room it may start", () => {
  const harness = loadMicCheck({ hasTested: false });
  const room = harness.elements.get("voice-room");
  let started = 0;
  room.addEventListener("voice-mic-ready", () => started++);
  harness.window.voiceMicCheck.open();

  harness.elements.get("voice-mic-skip-btn").dispatchEvent(new Event("click"));

  // Synchronous, before the 250 ms fade, so nothing waits on the hidden attribute.
  assert.equal(room.dataset.micCheck, "closed");
  assert.equal(started, 1);
});

test("leaves modal hidden if student already tested in current session", () => {
  const harness = loadMicCheck({ hasTested: true });
  assert.equal(harness.storage.get("voice_mic_tested_118"), "1");
});

test("retest button opens modal and starts test", () => {
  const harness = loadMicCheck({ hasTested: true });
  const retestBtn = harness.elements.get("voice-mic-retest-btn");
  retestBtn.dispatchEvent(new Event("click"));
  const modal = harness.elements.get("voice-mic-modal");
  assert.equal(modal.attributes.has("hidden"), false);
});
