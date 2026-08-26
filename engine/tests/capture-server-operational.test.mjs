import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import vm from "node:vm";

const repoRoot = path.resolve(import.meta.dirname, "..");

function readSource(relativePath) {
  return fs.readFileSync(path.join(repoRoot, relativePath), "utf8");
}

function sourceSlice(source, startMarker, endMarker) {
  const start = source.indexOf(startMarker);
  const end = source.indexOf(endMarker, start);
  assert.notEqual(start, -1, `missing source marker: ${startMarker}`);
  assert.notEqual(end, -1, `missing source marker: ${endMarker}`);
  return source.slice(start, end);
}

function loadEnqueue() {
  const source = readSource("capture-server.mjs");
  const snippet = sourceSlice(source, "let queue = Promise.resolve();", "function sendJson");
  const context = vm.createContext({});
  vm.runInContext(snippet, context);
  return vm.runInContext("enqueue", context);
}

test("a full capture queue is reported as 429 instead of a plain failure", async () => {
  const enqueue = loadEnqueue();
  let release;
  const gate = new Promise((resolve) => {
    release = resolve;
  });
  const running = [];
  for (let index = 0; index < 16; index += 1) {
    running.push(enqueue(() => gate));
  }

  assert.throws(
    () => enqueue(() => gate),
    (error) => {
      assert.equal(error.statusCode, 429);
      assert.match(error.message, /Capture queue is full/);
      return true;
    }
  );

  release();
  await Promise.all(running);
  assert.equal(await enqueue(async () => "drained"), "drained");
});

test("invalid capture requests still map to 400, not 429", () => {
  const source = readSource("capture-server.mjs");
  assert.match(source, /invalidRequest\.statusCode = 400;/);
  assert.match(
    source,
    /sendJson\(res, Number\.isInteger\(error\?\.statusCode\) \? error\.statusCode : 500, \{/
  );
});

class FakeChromium {
  constructor() {
    this.killed = false;
    this.stderr = { on() {} };
  }

  once(event, listener) {
    if (event === "close" && this.killed) {
      listener();
    }
    return this;
  }

  kill() {
    this.killed = true;
  }
}

function loadCaptureRuntimeSession(state) {
  const source = readSource("capture-server.mjs");
  const snippet = sourceSlice(source, "class CaptureRuntimeSession", "const captureSession");
  const context = vm.createContext({
    DevToolsSocket: class {
      async connect() {}

      async send() {
        return {};
      }

      close() {}
    },
    URLSearchParams,
    chromiumPath: "/usr/bin/fake-chromium",
    clearTimeout,
    console,
    defaultCameraPreset: "capture",
    defaultCameraProfile: "full-body",
    defaultClip: "motion_loop",
    defaultHeight: 1024,
    defaultPhase: 0.5,
    defaultProjectedShadow: {
      width: 0.72,
      height: 1.06,
      opacity: 0.28,
      crossSize: 0.46,
      crossOpacity: 0.22,
      floorY: 0,
      adjustShadow: false,
      adjustAlpha: true,
      invisibleHeight: 0.2,
      directionalShadow: false,
    },
    defaultScale: 1,
    defaultSpringRuntimeMode: "unity-prefab",
    defaultTimeoutMs: 50,
    defaultWarmupFrames: 60,
    defaultWarmupMode: "runtime",
    defaultWarmupMs: 250,
    defaultWidth: 1024,
    getFreePort: async () => 9333,
    makeTempDir: () => "/tmp/fake-capture-session",
    path: { join: (...parts) => parts.join("/") },
    port: 43110,
    removePathWithRetry: async () => {},
    setTimeout,
    spawn: () => new FakeChromium(),
    waitForPageTarget: async () => {
      if (state.failPageTarget) {
        throw new Error("Timed out waiting for Chromium page target.");
      }
      return { webSocketDebuggerUrl: "ws://127.0.0.1:1/devtools/page/test" };
    },
    waitForRuntimeReady: async () => {},
  });
  vm.runInContext(snippet, context);
  const CaptureRuntimeSession = vm.runInContext("CaptureRuntimeSession", context);
  return new CaptureRuntimeSession();
}

test("a session that never started reports healthy while lazily idle", () => {
  const session = loadCaptureRuntimeSession({ failPageTarget: false });

  assert.equal(session.healthy(), true);
  assert.deepEqual({ ...session.status() }, {
    ready: false,
    restarting: false,
    idleStopped: false,
  });
});

test("a failed Chromium launch marks the session unhealthy until a start succeeds", async () => {
  const state = { failPageTarget: true };
  const session = loadCaptureRuntimeSession(state);

  await assert.rejects(session.ensureStarted(50), /Chromium page target/);
  assert.equal(session.healthy(), false);
  assert.equal(session.status().ready, false);
  assert.match(session.status().lastStartError, /Chromium page target/);

  state.failPageTarget = false;
  await session.ensureStarted(50);
  assert.equal(session.healthy(), true);
  assert.deepEqual({ ...session.status() }, {
    ready: true,
    restarting: false,
    idleStopped: false,
  });
});

test("an idle-stopped session stays healthy because captures relaunch Chromium", async () => {
  const session = loadCaptureRuntimeSession({ failPageTarget: false });

  await session.ensureStarted(50);
  await session.stop(true);
  assert.equal(session.healthy(), true);
  assert.deepEqual({ ...session.status() }, {
    ready: false,
    restarting: false,
    idleStopped: true,
  });
});

test("healthz maps session health onto 200 versus 503 with the same body fields", () => {
  const source = readSource("capture-server.mjs");
  assert.match(source, /const healthy = captureSession\.healthy\(\);/);
  assert.match(
    source,
    /sendJson\(res, healthy \? 200 : 503, \{ ok: healthy, \.\.\.captureSession\.status\(\) \}\);/
  );
});
