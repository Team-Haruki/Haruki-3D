import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import vm from "node:vm";

const repoRoot = path.resolve(import.meta.dirname, "..");

class FakeWorker {
  constructor() {
    this.onmessage = null;
    this.onerror = null;
    this.posted = [];
    this.terminated = false;
    this.terminateCount = 0;
  }

  postMessage(message, transfer) {
    this.posted.push({ message, transfer });
  }

  terminate() {
    this.terminated = true;
    this.terminateCount += 1;
  }
}

function loadDecoder({ timeoutMs }) {
  const source = fs.readFileSync(
    path.join(repoRoot, "src/runtime/runtimeMessagePackDecoder.ts"),
    "utf8"
  );
  const start = source.indexOf("let decodeWorker");
  assert.notEqual(start, -1, "missing source marker: let decodeWorker");
  const snippet = source
    .slice(start)
    .replace("let decodeWorker: Worker | null = null;", "let decodeWorker = null;")
    .replace("new Map<number, PendingDecode>()", "new Map()")
    .replace(
      "let decodeWatchdogHandle: ReturnType<typeof setTimeout> | null = null;",
      "let decodeWatchdogHandle = null;"
    )
    .replace(
      "export async function decodeRuntimeMessagePackBrotli(bytes: ArrayBuffer)",
      "async function decodeRuntimeMessagePackBrotli(bytes)"
    )
    .replace("new Promise<unknown>(", "new Promise(")
    .replace("async function decodeDirect(bytes: ArrayBuffer)", "async function decodeDirect(bytes)")
    .replace(
      '({ data }: MessageEvent<{ id: number; value?: unknown; error?: string }>)',
      "({ data })"
    )
    .replace('new URL("./runtimeDecodeWorker.ts", import.meta.url)', "workerScriptUrl")
    .replace("function resetDecodeWorker(message: string)", "function resetDecodeWorker(message)");
  const workers = [];
  const context = vm.createContext({
    Worker: class extends FakeWorker {
      constructor() {
        super();
        workers.push(this);
      }
    },
    brotliWasmUrl: "brotli_wasm_bg.wasm",
    clearTimeout,
    setTimeout,
    workerDecodeTimeoutMs: timeoutMs,
    workerScriptUrl: "runtimeDecodeWorker.js",
    workerThresholdBytes: 64 * 1024,
  });
  vm.runInContext(snippet, context);
  return {
    workers,
    context,
    decode: vm.runInContext("decodeRuntimeMessagePackBrotli", context),
  };
}

test("a burst that keeps completing decodes outlasts the watchdog window without timing out", async () => {
  const { workers, decode } = loadDecoder({ timeoutMs: 90 });

  const burst = Array.from({ length: 8 }, () => decode(new ArrayBuffer(64 * 1024)));
  assert.equal(workers.length, 1);
  assert.equal(workers[0].posted.length, 8);

  const startedAt = Date.now();
  for (const posted of workers[0].posted) {
    await new Promise((resolve) => setTimeout(resolve, 15));
    workers[0].onmessage({ data: { id: posted.message.id, value: `decoded ${posted.message.id}` } });
  }
  assert.ok(
    Date.now() - startedAt > 90,
    "the paced burst must take longer than a single watchdog window"
  );

  const values = await Promise.all(burst);
  assert.equal(values.length, 8);
  assert.equal(workers[0].terminated, false, "steady progress must keep the worker alive");
});

test("total silence fires the watchdog once, rejects all pending, and resets the worker", async () => {
  const { workers, decode } = loadDecoder({ timeoutMs: 25 });

  const first = decode(new ArrayBuffer(64 * 1024));
  const second = decode(new ArrayBuffer(64 * 1024));
  assert.equal(workers.length, 1);
  assert.equal(workers[0].posted.length, 2);

  await assert.rejects(
    first,
    /Runtime decode worker made no progress within 25 ms; rejecting 2 pending decode\(s\)\./
  );
  await assert.rejects(second, /made no progress within 25 ms/);
  assert.equal(workers[0].terminated, true);

  await new Promise((resolve) => setTimeout(resolve, 60));
  assert.equal(workers[0].terminateCount, 1, "the watchdog must fire exactly once");

  const retry = decode(new ArrayBuffer(64 * 1024));
  assert.equal(workers.length, 2, "a decode after the reset launches a fresh worker");
  const posted = workers[1].posted[0];
  workers[1].onmessage({ data: { id: posted.message.id, value: "decoded" } });
  assert.equal(await retry, "decoded");
});

test("settling to zero clears the watchdog and a fresh enqueue re-arms it", async () => {
  const { workers, decode } = loadDecoder({ timeoutMs: 40 });

  const first = decode(new ArrayBuffer(64 * 1024));
  workers[0].onmessage({ data: { id: workers[0].posted[0].message.id, value: "first" } });
  assert.equal(await first, "first");

  await new Promise((resolve) => setTimeout(resolve, 100));
  assert.equal(workers[0].terminated, false, "no stale watchdog may fire after pending drained to zero");

  const second = decode(new ArrayBuffer(64 * 1024));
  assert.equal(workers.length, 1, "the drained worker is reused for the next enqueue");
  await assert.rejects(second, /made no progress within 40 ms/);
  assert.equal(workers[0].terminated, true, "the re-armed watchdog must reset the silent worker");
});

test("a decode answered in time is not rejected or reset later", async () => {
  const { workers, decode } = loadDecoder({ timeoutMs: 25 });

  const bytes = new ArrayBuffer(64 * 1024);
  const pending = decode(bytes);
  const posted = workers[0].posted[0];
  assert.equal(posted.message.wasmUrl, "brotli_wasm_bg.wasm");
  assert.equal(posted.transfer[0], bytes);
  workers[0].onmessage({ data: { id: posted.message.id, value: "decoded in time" } });
  assert.equal(await pending, "decoded in time");

  await new Promise((resolve) => setTimeout(resolve, 60));
  assert.equal(workers[0].terminated, false, "the watchdog must be cleared on completion");
});

test("small payloads keep using the synchronous fallback without any worker", async () => {
  const { workers, context, decode } = loadDecoder({ timeoutMs: 25 });
  vm.runInContext(
    "decodeDirect = async (bytes) => ({ directByteLength: bytes.byteLength });",
    context
  );

  const result = await decode(new ArrayBuffer(16));
  assert.equal(result.directByteLength, 16);
  assert.equal(workers.length, 0);
});
