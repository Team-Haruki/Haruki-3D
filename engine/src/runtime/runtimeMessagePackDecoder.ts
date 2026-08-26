import brotliWasmUrl from "./brotliWasmAsset";

const workerThresholdBytes = 64 * 1024;
const workerDecodeTimeoutMs = 30_000;

type PendingDecode = {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
};

let decodeWorker: Worker | null = null;
let nextDecodeId = 1;
const pendingDecodes = new Map<number, PendingDecode>();
let decodeWatchdogHandle: ReturnType<typeof setTimeout> | null = null;

export async function decodeRuntimeMessagePackBrotli(bytes: ArrayBuffer) {
  if (bytes.byteLength < workerThresholdBytes || typeof Worker === "undefined") {
    return decodeDirect(bytes);
  }
  const worker = getDecodeWorker();
  if (!worker) {
    return decodeDirect(bytes);
  }
  const id = nextDecodeId++;
  return new Promise<unknown>((resolve, reject) => {
    pendingDecodes.set(id, { resolve, reject });
    if (pendingDecodes.size === 1) armDecodeWatchdog();
    worker.postMessage({ id, bytes, wasmUrl: brotliWasmUrl }, [bytes]);
  });
}

async function decodeDirect(bytes: ArrayBuffer) {
  const { decodeRuntimeMessagePackBrotliDirect } = await import("./runtimeMessagePackDecodeCore");
  return decodeRuntimeMessagePackBrotliDirect(bytes, brotliWasmUrl);
}

function getDecodeWorker() {
  if (decodeWorker) return decodeWorker;
  try {
    decodeWorker = new Worker(new URL("./runtimeDecodeWorker.ts", import.meta.url), {
      type: "module",
      name: "haruki-runtime-decoder",
    });
    decodeWorker.onmessage = ({ data }: MessageEvent<{ id: number; value?: unknown; error?: string }>) => {
      const pending = pendingDecodes.get(data.id);
      if (!pending) return;
      pendingDecodes.delete(data.id);
      if (pendingDecodes.size === 0) clearDecodeWatchdog();
      else armDecodeWatchdog();
      if (data.error) pending.reject(new Error(data.error));
      else pending.resolve(data.value);
    };
    decodeWorker.onerror = () => resetDecodeWorker("Runtime decode worker failed.");
    return decodeWorker;
  } catch {
    decodeWorker = null;
    return null;
  }
}

function armDecodeWatchdog() {
  if (decodeWatchdogHandle !== null) clearTimeout(decodeWatchdogHandle);
  decodeWatchdogHandle = setTimeout(() => {
    decodeWatchdogHandle = null;
    resetDecodeWorker(
      `Runtime decode worker made no progress within ${workerDecodeTimeoutMs} ms; rejecting ${pendingDecodes.size} pending decode(s).`
    );
  }, workerDecodeTimeoutMs);
}

function clearDecodeWatchdog() {
  if (decodeWatchdogHandle !== null) {
    clearTimeout(decodeWatchdogHandle);
    decodeWatchdogHandle = null;
  }
}

function resetDecodeWorker(message: string) {
  clearDecodeWatchdog();
  decodeWorker?.terminate();
  decodeWorker = null;
  for (const pending of pendingDecodes.values()) {
    pending.reject(new Error(message));
  }
  pendingDecodes.clear();
}
