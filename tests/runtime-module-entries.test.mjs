import assert from "node:assert/strict";
import test from "node:test";

import * as base from "../dist/haruki-3d-engine-base.js";
import * as costumeShop from "../dist/haruki-3d-engine-costume-shop.js";
import {
  createHarukiMvRuntime,
  resolveUnityWebGLBuild,
} from "../dist/haruki-3d-engine-mv.js";

test("named runtime entries expose their own responsibilities", () => {
  assert.equal(typeof base.createHarukiBaseCharacterRuntime, "function");
  assert.equal(typeof base.buildUnityPrefabSourceGraph, "function");
  assert.equal(typeof costumeShop.createCostumeShopKernel, "function");
  assert.equal(typeof costumeShop.CostumeShopEngine, "function");
  assert.equal(typeof costumeShop.resolveCostumeShopModelScale, "function");
  assert.equal(typeof createHarukiMvRuntime, "function");
});

test("MV runtime owns one original Unity instance lifecycle", async () => {
  const calls = [];
  const unity = {
    SendMessage(gameObject, method, parameter) {
      calls.push(["message", gameObject, method, parameter]);
    },
    SetFullscreen(value) {
      calls.push(["fullscreen", value]);
    },
    async Quit() {
      calls.push(["quit"]);
    },
  };
  const canvas = {};
  const build = {
    dataUrl: "mv.data",
    frameworkUrl: "mv.framework.js",
    codeUrl: "mv.wasm",
  };
  let creates = 0;
  const runtime = createHarukiMvRuntime({
    canvas,
    build,
    async createUnityInstance(receivedCanvas, receivedBuild, onProgress) {
      creates += 1;
      assert.equal(receivedCanvas, canvas);
      assert.deepEqual(receivedBuild, build);
      onProgress?.(0.5);
      return unity;
    },
    onProgress(progress) {
      calls.push(["progress", progress]);
    },
  });

  const [first, second] = await Promise.all([runtime.prepare(), runtime.prepare()]);
  assert.equal(first, unity);
  assert.equal(second, unity);
  assert.equal(creates, 1);

  runtime.sendMessage("HarukiMvBridge", "LoadLive", "001");
  runtime.setFullscreen(true);
  await runtime.destroy();
  await runtime.destroy();

  assert.deepEqual(calls, [
    ["progress", 0.5],
    ["message", "HarukiMvBridge", "LoadLive", "001"],
    ["fullscreen", 1],
    ["quit"],
  ]);
  await assert.rejects(runtime.prepare(), /destroyed/);
});

test("MV build resolver produces one deployable Unity WebGL build contract", () => {
  assert.deepEqual(resolveUnityWebGLBuild({
    buildBaseUrl: "/3dmv/Build",
    streamingAssetsUrl: "/3dmv/StreamingAssets",
    buildName: "HarukiMV",
    compression: "gzip",
    companyName: "Team Haruki",
    productName: "Haruki 3DMV",
    productVersion: "1.0.0",
  }), {
    loaderUrl: "/3dmv/Build/HarukiMV.loader.js",
    config: {
      dataUrl: "/3dmv/Build/HarukiMV.data.gz",
      frameworkUrl: "/3dmv/Build/HarukiMV.framework.js.gz",
      codeUrl: "/3dmv/Build/HarukiMV.wasm.gz",
      streamingAssetsUrl: "/3dmv/StreamingAssets",
      companyName: "Team Haruki",
      productName: "Haruki 3DMV",
      productVersion: "1.0.0",
    },
  });
});

test("MV runtime can load a generated Unity loader without host glue", async () => {
  const originalDocument = globalThis.document;
  const originalFactory = globalThis.createUnityInstance;
  const calls = [];
  const unity = {
    SendMessage() {},
    async Quit() {
      calls.push(["quit"]);
    },
  };
  const scripts = [];
  globalThis.document = {
    createElement(tag) {
      assert.equal(tag, "script");
      const script = {};
      scripts.push(script);
      return script;
    },
    head: {
      appendChild(script) {
        calls.push(["script", script.src]);
        globalThis.createUnityInstance = async (canvas, config, onProgress) => {
          calls.push(["create", canvas, config]);
          onProgress?.(1);
          return unity;
        };
        script.onload();
      },
    },
  };

  try {
    const canvas = {};
    const runtime = createHarukiMvRuntime({
      canvas,
      loaderUrl: "/3dmv/Build/HarukiMV.loader.js",
      build: {
        dataUrl: "/3dmv/Build/HarukiMV.data.gz",
        frameworkUrl: "/3dmv/Build/HarukiMV.framework.js.gz",
        codeUrl: "/3dmv/Build/HarukiMV.wasm.gz",
      },
      onProgress(progress) {
        calls.push(["progress", progress]);
      },
    });

    assert.equal(runtime.state, "idle");
    await runtime.prepare();
    assert.equal(runtime.state, "ready");
    await runtime.destroy();
    assert.equal(runtime.state, "destroyed");
    assert.deepEqual(calls, [
      ["script", "/3dmv/Build/HarukiMV.loader.js"],
      ["create", canvas, runtime.build],
      ["progress", 1],
      ["quit"],
    ]);
    assert.equal(scripts.length, 1);
  } finally {
    globalThis.document = originalDocument;
    globalThis.createUnityInstance = originalFactory;
  }
});

test("MV runtime exposes Unity heap usage without leaking the instance", async () => {
  const memory = {
    totalWASMHeapSize: 512,
    usedWASMHeapSize: 320,
    totalJSHeapSize: 128,
    usedJSHeapSize: 64,
  };
  const runtime = createHarukiMvRuntime({
    canvas: {},
    build: {
      dataUrl: "mv.data",
      frameworkUrl: "mv.framework.js",
      codeUrl: "mv.wasm",
    },
    async createUnityInstance() {
      return {
        SendMessage() {},
        GetMemoryInfo() {
          return memory;
        },
        async Quit() {},
      };
    },
  });

  assert.equal(runtime.getMemoryInfo(), null);
  await runtime.prepare();
  assert.deepEqual(runtime.getMemoryInfo(), memory);
  await runtime.destroy();
  assert.equal(runtime.getMemoryInfo(), null);
});

test("MV runtime retries a failed Unity instance creation", async () => {
  let attempts = 0;
  const runtime = createHarukiMvRuntime({
    canvas: {},
    build: {
      dataUrl: "mv.data",
      frameworkUrl: "mv.framework.js",
      codeUrl: "mv.wasm",
    },
    async createUnityInstance() {
      attempts += 1;
      if (attempts === 1) {
        throw new Error("WebGL context lost during startup");
      }
      return { SendMessage() {}, async Quit() {} };
    },
  });

  await assert.rejects(runtime.prepare(), /context lost/);
  assert.equal(runtime.state, "failed");
  await runtime.prepare();
  assert.equal(runtime.state, "ready");
  assert.equal(attempts, 2);
  await runtime.destroy();
});

test("destroy waits for an in-flight Unity creation and quits it once", async () => {
  let finishCreation;
  const gate = new Promise((resolve) => {
    finishCreation = resolve;
  });
  let quits = 0;
  const runtime = createHarukiMvRuntime({
    canvas: {},
    build: {
      dataUrl: "mv.data",
      frameworkUrl: "mv.framework.js",
      codeUrl: "mv.wasm",
    },
    async createUnityInstance() {
      await gate;
      return {
        SendMessage() {},
        async Quit() {
          quits += 1;
        },
      };
    },
  });

  const preparing = runtime.prepare();
  const firstDestroy = runtime.destroy();
  const secondDestroy = runtime.destroy();
  assert.equal(firstDestroy, secondDestroy);
  assert.equal(runtime.state, "destroying");
  finishCreation();
  await preparing;
  await firstDestroy;
  assert.equal(runtime.state, "destroyed");
  assert.equal(quits, 1);
});

test("failed Unity Quit still releases the MV host state", async () => {
  const runtime = createHarukiMvRuntime({
    canvas: {},
    build: {
      dataUrl: "mv.data",
      frameworkUrl: "mv.framework.js",
      codeUrl: "mv.wasm",
    },
    async createUnityInstance() {
      return {
        SendMessage() {},
        async Quit() {
          throw new Error("Unity shutdown failed");
        },
      };
    },
  });

  await runtime.prepare();
  await assert.rejects(runtime.destroy(), /shutdown failed/);
  assert.equal(runtime.state, "destroyed");
  assert.equal(runtime.getMemoryInfo(), null);
  await assert.rejects(runtime.prepare(), /destroyed/);
});
