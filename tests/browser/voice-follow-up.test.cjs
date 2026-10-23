const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

// The room client seen from the outside: what it posts and what it shows while the AI
// examiner is probing. A probe shares the examQuestionId of the question it probes, so
// the client has to keep the two apart by the probe id or it will answer the wrong one.

class Element extends EventTarget {
  constructor() { super(); this.value = ""; this.textContent = ""; this.dataset = {}; this.hidden = false; }
  querySelector() { return { value: "csrf-test-token" }; }
  append() {}
  replaceChildren() {}
}

const turn = () => new Promise(resolve => setImmediate(resolve));

const question = (overrides = {}) => ({
  candidateId: 1, examId: 2, completedCount: 0, totalCount: 2, completed: false,
  awaitingFollowUp: false, serverNow: new Date().toISOString(),
  question: { examQuestionId: 10, orderNo: 1, text: "Question 10", transcript: "",
    deadline: new Date(Date.now() + 60000).toISOString() },
  ...overrides
});

const probe = (followUpId = 7, transcript = "") => question({
  completedCount: 1,
  question: { examQuestionId: 10, orderNo: 1, followUpId, followUpReason: 1, text: "Nói rõ hơn phần khoá?",
    transcript, deadline: new Date(Date.now() + 40000).toISOString() }
});

const awaiting = () => question({ completedCount: 1, awaitingFollowUp: true, question: null });

function harness(recovery = []) {
  const elements = new Map();
  const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
  get("voice-room").dataset.sync = "/VoiceRoom/1?handler=Sync";
  const window = new EventTarget();
  window.voiceRecovery = { list: async () => recovery, put: async record => { recovery.push(record); return true; } };
  window.voiceCapture = { beforeSubmit: async () => {}, setState: () => {} };
  const document = new EventTarget();
  document.getElementById = get;
  document.createElement = () => new Element();
  const requests = [], intervals = [];
  const context = { window, document, console, Date, JSON, Promise, AbortSignal, URL, location: { href: "https://test/" },
    setInterval: (callback, delay) => { intervals.push({ callback, delay }); }, setTimeout: () => {},
    fetch: (url, options) => new Promise((resolve, reject) => requests.push({
      body: JSON.parse(options.body), resolve: data => resolve({ ok: true, json: async () => data }), reject })) };
  vm.runInNewContext(
    readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js/voice-room.js"), "utf8"), context);
  return {
    get, requests, window, intervals, recovery,
    type(value) { get("voice-transcript").value = value; get("voice-transcript").dispatchEvent(new Event("input")); },
    submit() { get("voice-answer").dispatchEvent(new Event("submit", { cancelable: true })); }
  };
}

test("an answer to a probe is posted with the probe id, not as the question again", async () => {
  const h = harness(); await turn();
  h.requests[0].resolve(probe(7)); await turn();

  h.type("Khoá bi quan trước khi đọc.");
  h.submit(); await turn();

  assert.equal(h.requests[1].body.examQuestionId, 10, "audio still belongs beside the answer");
  assert.equal(h.requests[1].body.followUpId, 7, "without this the server would re-submit the question");
  assert.equal(h.requests[1].body.submit, true);
});

test("a probe replacing its question clears the box typed for the question", async () => {
  const h = harness(); await turn();
  h.requests[0].resolve(question()); await turn();
  h.type("Câu trả lời chính.");

  // Same examQuestionId, so only the probe id tells the client this is a new turn.
  h.requests[1] ? null : h.get("voice-retry").dispatchEvent(new Event("click"));
  await turn();
  h.requests[1].resolve(probe(7)); await turn();

  assert.equal(h.get("voice-transcript").value, "", "the probe starts with an empty answer");
  assert.equal(h.get("voice-question").textContent, "Nói rõ hơn phần khoá?");
});

test("a probe is announced with its reason and the turn it belongs to", async () => {
  const h = harness(); await turn();
  h.requests[0].resolve(probe(7)); await turn();

  assert.equal(h.get("voice-room").dataset.followUp, "true");
  assert.equal(h.get("voice-question-number").textContent, "HỎI THÊM · CÂU 01");
  assert.equal(h.get("voice-followup-reason").hidden, false);
  assert.match(h.get("voice-followup-reason").textContent, /thiếu ý/);
  assert.match(h.get("voice-followup-reason").textContent, /thời gian của câu 1/);
});

test("waiting for a decision hides the answer form instead of showing an empty question", async () => {
  const h = harness(); await turn();
  h.requests[0].resolve(awaiting()); await turn();

  assert.equal(h.get("voice-answer").hidden, true, "there is nothing to answer yet");
  assert.equal(h.get("voice-awaiting").hidden, false);
  assert.match(h.get("voice-status").textContent, /đang xem câu trả lời/i);
});

test("a room that is waiting asks again sooner than the heartbeat", async () => {
  const h = harness(); await turn();
  h.requests[0].resolve(awaiting()); await turn();

  const quick = h.intervals.find(x => x.delay === 1500);
  assert.ok(quick, "a waiting room needs its own poll");
  quick.callback(); await turn();
  assert.equal(h.requests.length, 2);
  assert.equal(h.requests[1].body, null, "a poll carries no answer");
});

test("an ordinary question is not dressed up as a probe", async () => {
  const h = harness(); await turn();
  h.requests[0].resolve(question()); await turn();

  assert.equal(h.get("voice-room").dataset.followUp, "false");
  assert.equal(h.get("voice-followup-reason").hidden, true);
  assert.equal(h.get("voice-awaiting").hidden, true);
  assert.equal(h.get("voice-question-number").textContent, "CÂU HỎI 01");
});

test("a draft saved for a probe is not offered back on its question", async () => {
  // Both are examQuestionId 10, so a shared key would put the probe's answer into the
  // question's box after a reload.
  const stored = [{ id: "text/10:7", questionId: 10, kind: "text", value: "trả lời câu hỏi thêm", synced: false }];
  const h = harness(stored); await turn();
  h.requests[0].resolve(question()); await turn();

  assert.equal(h.get("voice-transcript").value, "");
});

test("a draft saved before probes existed is still restored", async () => {
  const stored = [{ id: "text/10", questionId: 10, kind: "text", value: "đã gõ trước khi cập nhật", synced: false }];
  const h = harness(stored); await turn();
  h.requests[0].resolve(question()); await turn();

  assert.equal(h.get("voice-transcript").value, "đã gõ trước khi cập nhật");
});
