import assert from "node:assert/strict";
import test from "node:test";

import {
  readCharacterHairMaterialController,
} from "../dist/haruki-3d-engine-internal.js";

test("SekaiCharacterHair offset crosses the Unity-to-Three boundary exactly once", () => {
  const controller = readCharacterHairMaterialController({
    characterControllers: {
      hair: {
        offset: { x: -0.07, y: 0.01, z: 0.02 },
        headTransform: {
          name: "Head",
          transformPath: "face/Position/Hip/Waist/Spine/Chest/Neck/Head",
        },
      },
    },
  });

  assert.ok(controller);
  assert.deepEqual(controller.offset.toArray(), [0.07, 0.01, 0.02]);
  assert.equal(controller.headTransformName, "Head");
  assert.equal(
    controller.headTransformPath,
    "face/Position/Hip/Waist/Spine/Chest/Neck/Head"
  );
});
