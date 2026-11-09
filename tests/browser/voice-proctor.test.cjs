const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

function loadVoiceProctor({ initialViolations = 0 } = {}) {
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
      this.id = id;
      this.hidden = false;
      this.attributes = new Map();
    }
    set id(value) { this._id = value; if (value) elements.set(value, this); }
    get id() { return this._id; }
    setAttribute(name, val) { this.attributes.set(name, val); if (name === "hidden") this.hidden = true; }
    removeAttribute(name) { this.attributes.delete(name); if (name === "hidden") this.hidden = false; }
    hasAttribute(name) { return this.attributes.has(name); }
    querySelector() { return { value: "csrf" }; }
    click() { this.dispatchEvent(new Event("click")); }
  }

  for (const id of [
    "voice-room", "voice-cheat-modal", "voice-disqualified-overlay",
    "voice-cheat-count", "voice-cheat-ack-btn", "voice-cheat-reason",
    "voice-identity", "voice-stop", "voice-transcript", "voice-disqualified-desc"
  ]) {
    new Element(id);
  }

  const room = elements.get("voice-room");
  room.dataset.proctor = "/proctor";
  room.dataset.completed = "false";
  elements.get("voice-identity").dataset.candidate = "99";

  const storage = new Map();
  if (initialViolations > 0) storage.set("voice_proctor_violations_99", initialViolations.toString());

  const window = new EventTarget();
  const document = new EventTarget();
  document.getElementById = id => elements.get(id);

  const posts = [];
  const context = {
    window,
    document,
    sessionStorage: {
      getItem: key => storage.get(key) || null,
      setItem: (key, val) => storage.set(key, val)
    },
    fetch: async (url, options) => {
      posts.push(JSON.parse(options.body));
      const count = JSON.parse(options.body).count;
      return {
        ok: true,
        json: async () => ({ disqualified: count >= 4 })
      };
    },
    requestAnimationFrame: cb => setTimeout(cb, 5),
    setTimeout,
    clearTimeout,
    Date,
    parseInt,
    console
  };

  vm.runInNewContext(readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js/voice-proctor.js"), "utf8"), context);

  return { window, document, elements, storage, posts };
}

test("shows cheat warning modal for first 3 violations", async () => {
  const h = loadVoiceProctor({ initialViolations: 0 });
  await h.window.voiceProctor.triggerViolation("Chuyển tab 1");
  assert.equal(h.window.voiceProctor.violations, 1);
  assert.equal(h.elements.get("voice-cheat-count").textContent, "1 / 3");
  assert.equal(h.elements.get("voice-cheat-modal").hidden, false);
  assert.equal(h.elements.get("voice-disqualified-overlay").hidden, false || h.elements.get("voice-disqualified-overlay").hasAttribute("hidden"));
});

test("disqualifies candidate on 4th violation", async () => {
  const h = loadVoiceProctor({ initialViolations: 3 });
  await h.window.voiceProctor.triggerViolation("Chuyển tab lần thứ 4");
  assert.equal(h.window.voiceProctor.violations, 4);
  assert.equal(h.window.voiceProctor.isDisqualified, true);
  assert.equal(h.elements.get("voice-room").dataset.disqualified, "true");
  assert.equal(h.elements.get("voice-disqualified-overlay").hidden, false);
});
