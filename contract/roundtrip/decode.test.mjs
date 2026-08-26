import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { brotliDecompressSync } from "node:zlib";

import { decodeRuntimeMessagePack } from "../../engine/runtime-binary-codec.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(readFileSync(path.join(here, "fixture-manifest.json"), "utf8"));
const fixturePath = path.join(here, "out", "fixture.msgpack.br");

const { document: expectedDocument, expectations } = manifest;
const extPathLists = [
  ["float32", expectations.float32ExtPaths],
  ["uint16", expectations.uint16ExtPaths],
  ["uint32", expectations.uint32ExtPaths],
  ["plain", expectations.plainArrayPaths],
];

function classifyArrayPath(pathKey) {
  const matches = extPathLists.filter(([, paths]) => paths.includes(pathKey));
  assert.equal(
    matches.length,
    1,
    `array path "${pathKey}" must appear in exactly one expectation list, found ${matches.length}`,
  );
  return matches[0][0];
}

function collectArrayPaths(value, pathKey, found) {
  if (Array.isArray(value)) {
    found.add(pathKey);
    for (const item of value) {
      collectArrayPaths(item, pathKey, found);
    }
    return;
  }
  if (value !== null && typeof value === "object") {
    for (const [key, child] of Object.entries(value)) {
      collectArrayPaths(child, pathKey.length === 0 ? key : `${pathKey}.${key}`, found);
    }
  }
}

function walk(actual, expected, pathKey) {
  if (Array.isArray(expected)) {
    const representation = classifyArrayPath(pathKey);
    if (representation === "float32") {
      assert.ok(actual instanceof Float32Array, `"${pathKey}" must decode as Float32Array`);
      assert.equal(actual.length, expected.length, `"${pathKey}" length`);
      expected.forEach((value, index) => {
        assert.ok(
          Object.is(actual[index], Math.fround(value)),
          `"${pathKey}"[${index}]: got ${actual[index]}, want fround(${value}) = ${Math.fround(value)}`,
        );
      });
      return;
    }
    if (representation === "uint16" || representation === "uint32") {
      const constructor = representation === "uint16" ? Uint16Array : Uint32Array;
      assert.ok(actual instanceof constructor, `"${pathKey}" must decode as ${constructor.name}`);
      assert.equal(actual.length, expected.length, `"${pathKey}" length`);
      expected.forEach((value, index) => {
        assert.equal(actual[index], value, `"${pathKey}"[${index}]`);
      });
      return;
    }
    assert.ok(Array.isArray(actual), `"${pathKey}" must stay an ordinary MessagePack array`);
    assert.equal(actual.length, expected.length, `"${pathKey}" length`);
    expected.forEach((value, index) => walk(actual[index], value, pathKey));
    return;
  }
  if (expected !== null && typeof expected === "object") {
    assert.ok(
      actual !== null &&
        typeof actual === "object" &&
        !Array.isArray(actual) &&
        !ArrayBuffer.isView(actual),
      `"${pathKey}" must decode as a map`,
    );
    assert.deepEqual(
      Object.keys(actual).sort(),
      Object.keys(expected).sort(),
      `"${pathKey}" keys`,
    );
    for (const [key, value] of Object.entries(expected)) {
      walk(actual[key], value, pathKey.length === 0 ? key : `${pathKey}.${key}`);
    }
    return;
  }
  if (typeof expected === "number") {
    assert.ok(
      Object.is(actual, expected),
      `"${pathKey}": got ${actual}, want ${expected}`,
    );
    return;
  }
  assert.equal(actual, expected, `"${pathKey}"`);
}

function decodeFixture() {
  let compressed;
  try {
    compressed = readFileSync(fixturePath);
  } catch {
    assert.fail(`missing ${fixturePath}; run contract/roundtrip/run.sh to emit it first`);
  }
  return decodeRuntimeMessagePack(brotliDecompressSync(compressed));
}

test("fixture manifest exercises every contract case", () => {
  const meshes = expectedDocument.nativeMeshes.meshes;
  const floats = meshes[0].positions;
  assert.ok(floats.length >= 8, "float32 ext arrays require at least 8 values");
  assert.ok(floats.some((value) => Object.is(value, -0)), "float fixture must include -0");
  for (const required of [0, 1, -1, 0.5, 3.14159274, 1e-7, 3.4028235e38]) {
    assert.ok(floats.includes(required), `float fixture must include ${required}`);
  }
  assert.notEqual(Math.fround(3.14159274), 3.14159274, "pi case must actually round in float32");
  assert.notEqual(Math.fround(1e-7), 1e-7, "1e-7 case must actually round in float32");

  const narrow = meshes[0].skinIndices;
  assert.ok(narrow.length >= 16, "index ext arrays require at least 16 values");
  assert.ok(narrow.includes(0) && narrow.includes(65535), "uint16 fixture must span 0..65535");
  assert.ok(narrow.every((value) => value <= 65535), "uint16 fixture must stay narrow");

  const wide = meshes[0].submeshes[0].indices;
  assert.ok(wide.length >= 16, "index ext arrays require at least 16 values");
  assert.ok(wide.includes(65536), "uint32 fixture must include 65536");
  assert.ok(wide.includes(4294967295), "uint32 fixture must include a large value");

  assert.equal(meshes[0].normals.length, 0, "empty array case must stay in the fixture");
  assert.ok(
    expectedDocument.unrelated.positions.length >= 8,
    "collision fixture must be large enough that only the path rule keeps it ordinary",
  );
  assert.ok(
    expectedDocument.clips.tracks[0].times.length >= 8,
    "cross-schema collision fixture must be large enough to trigger ext under UnityMotion",
  );

  const arrayPaths = new Set();
  collectArrayPaths(expectedDocument, "", arrayPaths);
  for (const pathKey of arrayPaths) {
    classifyArrayPath(pathKey);
  }
});

test("production emit decodes to the manifest document via the production codec", () => {
  walk(decodeFixture(), expectedDocument, "");
});

test("targeted decoded-value assertions", () => {
  const decoded = decodeFixture();
  const mesh = decoded.nativeMeshes.meshes[0];

  assert.ok(mesh.positions instanceof Float32Array);
  assert.ok(Object.is(mesh.positions[0], 0));
  assert.ok(Object.is(mesh.positions[1], -0), "float32 -0 sign must survive the round trip");
  assert.equal(mesh.positions[5], Math.fround(3.14159274));
  assert.equal(mesh.positions[6], Math.fround(1e-7));
  assert.equal(mesh.positions[7], Math.fround(3.4028235e38));

  assert.ok(mesh.skinIndices instanceof Uint16Array);
  assert.equal(mesh.skinIndices[0], 0);
  assert.equal(mesh.skinIndices[15], 65535);

  assert.ok(mesh.submeshes[0].indices instanceof Uint32Array);
  assert.equal(mesh.submeshes[0].indices[0], 65536);
  assert.equal(mesh.submeshes[0].indices[1], 4294967295);

  assert.ok(Array.isArray(mesh.normals), "below-threshold ext-path arrays stay ordinary");
  assert.equal(mesh.normals.length, 0);

  const collision = decoded.unrelated.positions;
  assert.ok(Array.isArray(collision), "name collision on an unrelated schema stays ordinary");
  assert.equal(collision[5], 3.14159274, "ordinary arrays keep full float64 precision");
  assert.notEqual(collision[5], Math.fround(3.14159274));

  assert.ok(
    Array.isArray(decoded.clips.tracks[0].times),
    "UnityMotion paths stay ordinary under the PartRuntime schema",
  );

  assert.equal(decoded.meta.name, "haruki-3d-roundtrip-fixture");
  assert.equal(decoded.meta.unicode, expectedDocument.meta.unicode);
  assert.equal(decoded.meta.nullField, null);
  assert.equal(decoded.meta.flags.enabled, true);
  assert.equal(decoded.meta.flags.disabled, false);
  assert.equal(decoded.meta.counts.int32Min, -2147483648);
  assert.equal(decoded.meta.counts.uint32Max, 4294967295);
  assert.equal(decoded.meta.counts.maxSafe, 9007199254740991);
  assert.equal(decoded.meta.counts.half, 2.5);
  assert.equal(decoded.meta.counts.pi64, 3.14159274);
});
