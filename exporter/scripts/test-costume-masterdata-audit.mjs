import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { auditCostumeMasterdata } from "./audit-costume-masterdata.mjs";

test("audit passes a minimal valid costume master set", () => {
  const dir = makeMasterFixture({
    costume3ds: [
      costume(10, "head", 1, 100, 1),
      costume(20, "hair", 1, 100, 1),
      costume(30, "body", 1, 100, 1),
      costume(31, "body", 1, 100, 2),
    ],
    costume3dModels: [
      model(10, "light_sound", "01/0001", "head_and_hair"),
      model(20, "light_sound", "01/0001n", "head_and_hair"),
      model(30, "light_sound", "99/0001"),
      model(31, "light_sound", "99/0001", null, "01"),
    ],
    cards: [
      { id: 900, characterId: 1, cardRarityType: "rarity_4", prefix: "fixture" },
    ],
    cardCostume3ds: [
      { cardId: 900, costume3dId: 10, isInitialObtainHair: false },
      { cardId: 900, costume3dId: 30, isInitialObtainHair: false },
    ],
    notAvailablePatterns: [],
    defaultHairs: [
      pattern(10, 20, "light_sound"),
    ],
  });

  try {
    const result = runAudit(dir);
    assert.equal(result.errors.length, 0);
    assert.equal(result.warnings.length, 0);
    assert.equal(result.notes.find((note) => note.code === "compatibility_pattern_stats").notAvailableKeys, 0);
    assert.equal(result.notes.find((note) => note.code === "compatibility_pattern_stats").defaultHairKeys, 1);
    assert.equal(result.counts.cardUnlockPartsets["body+head"], 1);
    assert.equal(result.counts.costumeGroupColorCounts["2"], 1);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("audit fails broken hard references", () => {
  const dir = makeMasterFixture({
    costume3ds: [
      costume(10, "head", 1, 100, 1),
      costume(20, "hair", 2, 100, 1),
    ],
    costume3dModels: [
      model(999, "light_sound", "99/9999"),
    ],
    cards: [
      { id: 900, characterId: 1, cardRarityType: "rarity_4", prefix: "fixture" },
    ],
    cardCostume3ds: [
      { cardId: 900, costume3dId: 20, isInitialObtainHair: false },
      { cardId: 901, costume3dId: 10, isInitialObtainHair: false },
      { cardId: 900, costume3dId: 999, isInitialObtainHair: false },
    ],
    notAvailablePatterns: [],
    defaultHairs: [],
  });

  try {
    const result = runAudit(dir);
    const errorCodes = result.errors.map((error) => error.code).sort();
    assert.deepEqual(errorCodes, [
      "cardCostume3ds_character_mismatch",
      "cardCostume3ds_missing_cards",
      "cardCostume3ds_missing_costume3ds",
    ]);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("audit allows model inventory that is not selectable in this region", () => {
  const dir = makeMasterFixture({
    costume3ds: [],
    costume3dModels: [
      model(999, "light_sound", "99/9999"),
    ],
    cards: [],
    cardCostume3ds: [],
    notAvailablePatterns: [],
    defaultHairs: [],
  });

  try {
    const result = runAudit(dir);
    assert.equal(result.errors.length, 0);
    assert.equal(
      result.notes.find((note) => note.code === "costume3dModels_unlisted_inventory").uniqueCount,
      1,
    );
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("audit normalizes split Nuverse costume tables", () => {
  const dir = makeMasterFixture({
    costume3ds: [
      { id: 10, costume3dGroupId: 100, partType: "head", colorId: 1 },
    ],
    costume3dGroups: [
      { groupId: 100, characterId: 1, name: "split costume", rarity: "normal" },
    ],
    costume3dColors: [
      { id: 1, name: "original" },
    ],
    costume3dModels: [
      model(10, "light_sound", "01/0001", "head_and_hair"),
    ],
    cards: [
      { id: 900, characterId: 1, cardRarityType: "rarity_4", prefix: "fixture" },
    ],
    cardCostume3ds: [
      { cardId: 900, costume3dId: 10, isInitialObtainHair: false },
    ],
    notAvailablePatterns: [],
    defaultHairs: [],
  });

  try {
    const result = runAudit(dir);
    assert.equal(result.errors.length, 0);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("audit reports a blacklisted default hair as fatal", () => {
  const dir = makeMasterFixture({
    costume3ds: [
      costume(10, "head", 1, 100, 1),
      costume(20, "hair", 1, 100, 1),
    ],
    costume3dModels: [
      model(10, "light_sound", "01/0001", "head_and_hair"),
      model(20, "light_sound", "01/0001n", "head_and_hair"),
    ],
    cards: [],
    cardCostume3ds: [],
    notAvailablePatterns: [
      pattern(10, 20, "light_sound"),
    ],
    defaultHairs: [
      pattern(10, 20, "light_sound"),
    ],
  });

  try {
    const result = runAudit(dir);
    assert.deepEqual(result.errors.map((error) => error.code), [
      "defaultHair_notAvailable_conflicts",
    ]);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

function runAudit(masterDir) {
  return auditCostumeMasterdata({ masterDir });
}

function makeMasterFixture(files) {
  const dir = mkdtempSync(path.join(os.tmpdir(), "haruki-costume-master-"));
  const fileNames = {
    costume3ds: "costume3ds.json",
    costume3dGroups: "costume3dGroups.json",
    costume3dColors: "costume3dColors.json",
    costume3dModels: "costume3dModels.json",
    cards: "cards.json",
    cardCostume3ds: "cardCostume3ds.json",
    notAvailablePatterns: "costume3dModelNotAvailablePatterns.json",
    defaultHairs: "costume3dModelDefaultHairs.json",
  };
  for (const [key, fileName] of Object.entries(fileNames)) {
    writeFileSync(path.join(dir, fileName), `${JSON.stringify(files[key] ?? [], null, 2)}\n`);
  }
  return dir;
}

function costume(id, partType, characterId, groupId, colorId) {
  return {
    id,
    costume3dGroupId: groupId,
    partType,
    characterId,
    colorId,
    colorName: `color ${colorId}`,
    name: `costume ${id}`,
    costume3dType: "normal",
    costume3dRarity: "normal",
    assetbundleName: `cos${id}`,
  };
}

function model(
  costume3dId,
  unit,
  assetbundleName,
  headCostume3dAssetbundleType = null,
  colorAssetbundleName = null,
  part = null,
) {
  return {
    id: costume3dId * 10,
    costume3dId,
    unit,
    assetbundleName,
    headCostume3dAssetbundleType,
    colorAssetbundleName,
    part,
    thumbnailAssetbundleName: null,
  };
}

function pattern(headCostume3dId, hairCostume3dId, unit, isDefault = false) {
  return {
    id: headCostume3dId * 100000 + hairCostume3dId,
    headCostume3dId,
    hairCostume3dId,
    unit,
    isDefault,
  };
}
