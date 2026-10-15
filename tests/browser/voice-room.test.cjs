const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

class Element extends EventTarget {
  constructor() { super(); this.value = ""; this.textContent = ""; this.dataset = {}; }
  querySelector() { return { value: "csrf-test-token" }; }
}
const turn = () => new Promise(resolve => setImmediate(resolve));
const state = (id = 10, transcript = "") => ({ candidateId: 1, examId: 2, completedCount: id === 10 ? 0 : 1,
  totalCount: 2, completed: false, serverNow: new Date().toISOString(),
  question: { examQuestionId: id, text: `Question ${id}`, transcript, deadline: new Date(Date.now() + 60000).toISOString() } });
function harness(recovery = []) {
  const elements = new Map();
  const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
  get("voice-room").dataset.sync = "/VoiceRoom/1?handler=Sync";
  const window = new EventTarget();
  window.voiceRecovery = { list: async () => recovery, put: async () => true };
  window.voiceCapture = { beforeSubmit: async () => {}, setState: () => {} };
  const document = new EventTarget(); document.getElementById = get;
  const requests = [], intervals = [];
  const context = { window, document, console, Date, JSON, Promise, AbortSignal,
    setInterval: (callback, delay) => { intervals.push({ callback, delay }); }, setTimeout: () => {},
    fetch: (url, options) => new Promise((resolve, reject) => requests.push({
      body: JSON.parse(options.body), resolve: data => resolve({ ok: true, json: async () => data }), reject })) };
  vm.runInNewContext(readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/wwwroot/js/voice-room.js"), "utf8"), context);
  return { get, requests, window, intervals, type(value) { get("voice-transcript").value = value; get("voice-transcript").dispatchEvent(new Event("input")); } };
}

test("submit queued behind an in-flight autosave retains the newer transcript", async () => {
  const h = harness(); await turn(); h.requests[0].resolve(state()); await turn();
  h.type("first draft"); h.get("voice-save").dispatchEvent(new Event("click")); await turn();
  assert.equal(h.requests[1].body.transcript, "first draft");
  h.type("newer spoken answer"); h.get("voice-answer").dispatchEvent(new Event("submit", { cancelable: true })); await turn();
  assert.equal(h.requests.length, 2, "submit must wait for the existing request");
  h.requests[1].resolve(state(10, "first draft")); await turn();
  assert.equal(h.requests[2].body.transcript, "newer spoken answer");
  assert.equal(h.requests[2].body.submit, true);
  assert.equal(h.requests[2].body.examQuestionId, 10);
  h.requests[2].resolve(state(11)); await turn();
  assert.equal(h.get("voice-transcript").value, "");
  assert.equal(h.get("voice-question").textContent, "Question 11");
});

test("lost submit response retries the same question instead of submitting the next one", async () => {
  const h = harness(); await turn(); h.requests[0].resolve(state()); await turn();
  h.type("final answer"); h.get("voice-answer").dispatchEvent(new Event("submit", { cancelable: true })); await turn();
  h.requests[1].reject(new Error("connection lost")); await turn();
  h.get("voice-retry").dispatchEvent(new Event("click")); await turn();
  assert.equal(h.requests[2].body.examQuestionId, 10);
  assert.equal(h.requests[2].body.submit, true);
  h.requests[2].resolve(state(11)); await turn();
  h.get("voice-retry").dispatchEvent(new Event("click")); await turn();
  assert.equal(h.requests[3].body, null);
  h.requests[3].resolve(state(11)); await turn();
});

test("reload restores only a locally pending draft for the server's current question", async () => {
  const h = harness([{ id: "text/10", value: "recovered speech", synced: false, submit: true }]);
  await turn(); h.requests[0].resolve(state()); await turn();
  assert.equal(h.get("voice-transcript").value, "recovered speech");
  h.get("voice-retry").dispatchEvent(new Event("click")); await turn();
  assert.equal(h.requests[1].body.examQuestionId, 10);
  assert.equal(h.requests[1].body.transcript, "recovered speech");
  h.requests[1].resolve(state(11)); await turn();
  const alreadyAccepted = harness([{ id: "text/10", value: "old answer", synced: false, submit: true }]);
  await turn(); alreadyAccepted.requests[0].resolve(state(11)); await turn();
  assert.equal(alreadyAccepted.get("voice-transcript").value, "");
  alreadyAccepted.get("voice-retry").dispatchEvent(new Event("click")); await turn();
  assert.equal(alreadyAccepted.requests[1].body, null);
  alreadyAccepted.requests[1].resolve(state(11)); await turn();
});
