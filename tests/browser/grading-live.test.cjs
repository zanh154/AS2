const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

// Màn hình giảng viên khi AI chấm bài. Điều đáng kiểm: trang chỉ phản ứng với đúng phiên
// và đúng bài của nó, tự cập nhật khi chấm xong, và không bao giờ tự tải lại khi giảng
// viên đang gõ dở điểm hay nhận xét — tải lại lúc đó là xoá mất phần họ đang nhập.

const source = readFileSync(join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js/grading-live.js"), "utf8");

function load() {
  const window = {};
  vm.runInNewContext(source, { window, document: {}, location: {} });
  return window.aivesGradingLive;
}

const root = dataset => ({ dataset: { livePage: "ExamResults", ...dataset } });
const paper = root({ examId: "36", candidateId: "76" });
const sheet = root({ examId: "36" });

const done = (overrides = {}) => ({ examId: 36, candidateId: 76, kind: "grading-done", graded: 3, skipped: 0, ...overrides });
const progress = (done = 1, total = 3) => ({ examId: 36, candidateId: 76, kind: "grading-progress", done, total });

function page() {
  const elements = {
    "grading-live-status": { hidden: true, textContent: "" },
    "realtime-notice": { hidden: true },
    "realtime-notice-message": { textContent: "" }
  };
  let reloads = 0;
  const env = { document: { getElementById: id => elements[id] ?? null }, reload: () => { reloads++; } };
  return { elements, env, reloads: () => reloads };
}

test("the paper reloads itself once the AI has finished marking it", () => {
  const live = load();
  const view = page();

  live.apply(done(), paper, false, view.env);

  assert.equal(view.reloads(), 1);
});

test("the paper never reloads over a mark the lecturer is typing", () => {
  const live = load();
  const view = page();

  const action = live.apply(done(), paper, true, view.env);

  assert.equal(view.reloads(), 0);
  assert.equal(action.kind, "notice");
  assert.equal(view.elements["realtime-notice"].hidden, false);
  assert.match(view.elements["realtime-notice-message"].textContent, /chấm xong/);
});

test("the paper shows how far the marking has got", () => {
  const live = load();
  const view = page();

  live.apply(progress(2, 3), paper, false, view.env);

  assert.equal(view.reloads(), 0);
  assert.equal(view.elements["grading-live-status"].hidden, false);
  assert.equal(view.elements["grading-live-status"].textContent, "AI đang chấm bài này: 2/3 câu.");
});

test("a paper ignores the marking of another student's paper", () => {
  const live = load();
  const view = page();

  live.apply(done({ candidateId: 77 }), paper, false, view.env);
  live.apply({ ...progress(), candidateId: 77 }, paper, false, view.env);

  assert.equal(view.reloads(), 0);
  assert.equal(view.elements["grading-live-status"].hidden, true);
});

test("a page ignores events from another session", () => {
  const live = load();
  const view = page();

  live.apply(done({ examId: 99 }), paper, false, view.env);
  live.apply(done({ examId: 99 }), sheet, false, view.env);

  assert.equal(view.reloads(), 0);
});

test("the score sheet reloads when any paper in its session is marked", () => {
  const live = load();
  const view = page();

  live.apply(done({ candidateId: 80 }), sheet, false, view.env);

  assert.equal(view.reloads(), 1);
});

test("the score sheet does not reload for progress, only for a finished paper", () => {
  const live = load();
  const view = page();

  live.apply(progress(), sheet, false, view.env);

  assert.equal(view.reloads(), 0);
});

test("pages that are not results pages do nothing", () => {
  const live = load();
  const view = page();

  live.apply(done(), { dataset: { livePage: "ExamSessions", examId: "36" } }, false, view.env);
  live.apply(done(), null, false, view.env);
  live.apply(null, paper, false, view.env);

  assert.equal(view.reloads(), 0);
});
