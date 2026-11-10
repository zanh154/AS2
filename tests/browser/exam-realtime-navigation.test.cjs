const { test } = require("node:test");
const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const vm = require("node:vm");

const source = readFileSync(
  join(__dirname, "../../src/AssignmentPRN.Presentation/UI Assets/js/exam-realtime.js"),
  "utf8"
);

function loadRealtimeHarness() {
  const docTarget = new EventTarget();
  const winTarget = new EventTarget();
  const handlers = new Map();
  let reloads = 0;

  const document = {
    addEventListener: docTarget.addEventListener.bind(docTarget),
    dispatchEvent: docTarget.dispatchEvent.bind(docTarget),
    querySelector: sel => sel === "[data-live-page]" ? { dataset: { livePage: "StudentSchedule" }, dispatchEvent() {} } : null,
    getElementById: () => null
  };

  const window = {
    addEventListener: winTarget.addEventListener.bind(winTarget),
    dispatchEvent: winTarget.dispatchEvent.bind(winTarget),
    signalR: {
      HubConnectionBuilder: class {
        withUrl() { return this; }
        withAutomaticReconnect() { return this; }
        build() {
          return {
            on: (name, fn) => handlers.set(name, fn),
            onreconnected: () => {},
            start: async () => {},
            invoke: async () => {}
          };
        }
      }
    }
  };

  const location = {
    pathname: "/StudentSchedule/Mine",
    reload: () => { reloads++; }
  };

  const context = {
    window,
    signalR: window.signalR,
    document,
    location,
    Event,
    queueMicrotask,
    setTimeout,
    clearTimeout
  };

  vm.runInNewContext(source, context);
  document.dispatchEvent(new Event("DOMContentLoaded"));

  return {
    document,
    window,
    handlers,
    reloads: () => reloads
  };
}

test("ExamChanged during an in-flight form submit does not reload the schedule and abort navigation", async () => {
  const harness = loadRealtimeHarness();
  const submitEvent = new Event("submit", { cancelable: true });
  Object.defineProperty(submitEvent, "target", {
    value: { closest: sel => sel === "form" ? {} : null }
  });

  harness.document.dispatchEvent(submitEvent);
  await new Promise(r => queueMicrotask(r));

  await harness.handlers.get("ExamChanged")({ kind: "entered", examId: 61 });
  assert.equal(harness.reloads(), 0);
});

test("ExamChanged reloads the schedule when no navigation is in flight", async () => {
  const harness = loadRealtimeHarness();
  await harness.handlers.get("ExamChanged")({ kind: "entered", examId: 61 });
  assert.equal(harness.reloads(), 1);
});

test("ExamChanged still reloads if a form submit handler called preventDefault", async () => {
  const harness = loadRealtimeHarness();
  const submitEvent = new Event("submit", { cancelable: true });
  Object.defineProperty(submitEvent, "target", {
    value: { closest: sel => sel === "form" ? {} : null }
  });
  submitEvent.preventDefault();

  harness.document.dispatchEvent(submitEvent);
  await new Promise(r => queueMicrotask(r));

  await harness.handlers.get("ExamChanged")({ kind: "entered", examId: 61 });
  assert.equal(harness.reloads(), 1);
});
