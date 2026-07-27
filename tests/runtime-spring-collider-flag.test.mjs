import assert from "node:assert/strict";
import test from "node:test";

import { officialColliderFlagPrefixes } from "../dist/haruki-3d-engine-internal.js";

// Official SpringBoneSetup (VA 0x...5D04) tests each group bit with
// (flags & bit) != 0: 1 Hip, 2 Chest, 4 L_Arm, 8 R_Arm, 16 L_Elbow,
// 32 R_Elbow. A serialized -1 therefore selects EVERY group — the exporter's
// old `> 0` filter dropped those bones entirely (Shizuku hair 208 back/BS
// chains shipped with flag=-1 and lost all 6 groups).
test("collider flag bits map to the official CL_ groups", () => {
  assert.deepEqual(officialColliderFlagPrefixes(14), [
    "CL_Chest",
    "CL_Left_Arm",
    "CL_Right_Arm",
  ]);
  assert.deepEqual(officialColliderFlagPrefixes(2), ["CL_Chest"]);
  assert.deepEqual(officialColliderFlagPrefixes(0), []);
});

test("negative collider flags select every official CL_ group", () => {
  assert.deepEqual(officialColliderFlagPrefixes(-1), [
    "CL_Hip",
    "CL_Chest",
    "CL_Left_Arm",
    "CL_Right_Arm",
    "CL_Left_Elbow",
    "CL_Right_Elbow",
  ]);
});
