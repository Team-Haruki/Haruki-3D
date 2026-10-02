import assert from "node:assert/strict";
import test from "node:test";

import * as base from "../dist/haruki-3d-engine-base.js";
import * as costumeShop from "../dist/haruki-3d-engine-costume-shop.js";

test("named runtime entries expose their own responsibilities", () => {
  assert.equal(typeof base.createHarukiBaseCharacterRuntime, "function");
  assert.equal(typeof base.buildUnityPrefabSourceGraph, "function");
  assert.equal(typeof costumeShop.createCostumeShopKernel, "function");
  assert.equal(typeof costumeShop.CostumeShopEngine, "function");
  assert.equal(typeof costumeShop.resolveCostumeShopModelScale, "function");
});
