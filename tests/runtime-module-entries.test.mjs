import assert from "node:assert/strict";
import test from "node:test";

import * as base from "../dist/haruki-3d-engine-base.js";
import * as costumeShop from "../dist/haruki-3d-engine-costume-shop.js";
import { createHarukiMvRuntime } from "../dist/haruki-3d-engine-mv.js";

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
      assert.equal(receivedBuild, build);
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
