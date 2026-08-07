import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const unityRoot = path.join(repoRoot, "unity", "Haruki3DMV");

test("repository contains a Unity 2022.3 WebGL MV project", () => {
  assert.match(
    fs.readFileSync(path.join(unityRoot, "ProjectSettings", "ProjectVersion.txt"), "utf8"),
    /^m_EditorVersion: 2022\.3\.62f2$/m
  );

  const packages = JSON.parse(
    fs.readFileSync(path.join(unityRoot, "Packages", "manifest.json"), "utf8")
  );
  assert.equal(packages.dependencies["com.unity.timeline"], "1.7.6");
  assert.match(packages.dependencies["com.unity.render-pipelines.universal"], /^14\./);

  for (const relativePath of [
    "Assets/Haruki/MV/Runtime/Haruki.MV.Runtime.asmdef",
    "Assets/Haruki/MV/Runtime/IMvPlaybackParticipant.cs",
    "Assets/Haruki/MV/Runtime/MvPlaybackCoordinator.cs",
    "Assets/Haruki/MV/Runtime/MvTimelineBinding.cs",
    "Assets/Haruki/MV/Runtime/MvTimelineNode.cs",
    "Assets/Haruki/MV/Runtime/MvTimelinePlaybackParticipant.cs",
    "Assets/Haruki/MV/Runtime/MvTimelineLifecycle.cs",
    "Assets/Haruki/MV/Runtime/MvMotionSequence.cs",
    "Assets/Haruki/MV/Runtime/MvCharacterNode.cs",
    "Assets/Haruki/MV/Runtime/MvStageNode.cs",
    "Assets/Haruki/MV/Runtime/MvCameraAdjustment.cs",
    "Assets/Haruki/MV/Runtime/MvPlayerAssembler.cs",
    "Assets/Haruki/MV/Runtime/MvOfficialRuntimeData.cs",
    "Assets/Haruki/MV/Runtime/MvPlayerRenderSettings.cs",
    "Assets/Haruki/MV/Runtime/MvSceneBundleLoader.cs",
    "Assets/Haruki/MV/Runtime/MvBundleSetLoader.cs",
    "Assets/Haruki/MV/Runtime/HarukiMvBridge.cs",
    "Assets/Haruki/MV/Plugins/WebGL/HarukiMvBridge.jslib",
    "Assets/Haruki/MV/Editor/BuildWebGL.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvPlaybackCoordinatorTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvTimelineNodeTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvMotionSequenceTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvCharacterNodeTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvCameraAdjustmentTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvOfficialRuntimeDataTests.cs",
  ]) {
    assert.equal(fs.existsSync(path.join(unityRoot, relativePath)), true, relativePath);
  }

  const build = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Editor/BuildWebGL.cs"),
    "utf8"
  );
  assert.match(build, /entry\.kind\?\.StartsWith\("timeline_"/);
  assert.match(build, /RemapRecoveredTimelineScriptGuids/);
  assert.match(build, /"timeline\.playable",\s*SearchOption\.AllDirectories/);
  assert.doesNotMatch(build, /Directory\.GetFiles\(importedRoot, "\*"/);
  assert.match(build, /addressableNames = new\[\] \{ "timeline" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "decoration" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "penlight" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "body" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "face" \}/);
  assert.match(build, /deps = entry\.dependencies/);
  assert.doesNotMatch(build, /new GameObject\("Main Camera"\)/);
  assert.doesNotMatch(build, /new GameObject\("Preview Light"\)/);
  assert.doesNotMatch(build, /fieldOfView\s*=/);

  const bundleLoader = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Runtime/MvBundleSetLoader.cs"),
    "utf8"
  );
  assert.doesNotMatch(bundleLoader, /frameCamera|FrameWithMainCamera/);
  assert.doesNotMatch(bundleLoader, /Camera\.main|fieldOfView/);
});

test("GitHub Actions builds and validates the real Unity WebGL artifact", () => {
  const workflow = fs.readFileSync(
    path.join(repoRoot, ".github", "workflows", "unity-mv.yml"),
    "utf8"
  );
  assert.match(workflow, /game-ci\/unity-test-runner@[0-9a-f]{40} # v4/);
  assert.match(workflow, /game-ci\/unity-builder@[0-9a-f]{40} # v4/);
  assert.match(workflow, /environment: unity-build/);
  assert.match(workflow, /buildMethod: Haruki\.MV\.Editor\.BuildWebGL\.PerformBuild/);
  assert.match(workflow, /secrets\.UNITY_LICENSE/);
  assert.match(workflow, /HarukiMV\.wasm\.gz/);
  assert.match(workflow, /actions\/upload-artifact@v4/);
});

test("Unity activation material cannot be tracked by accident", () => {
  const ignore = fs.readFileSync(path.join(repoRoot, ".gitignore"), "utf8");
  assert.match(ignore, /^\*\.alf$/m);
  assert.match(ignore, /^\*\.ulf$/m);
  assert.match(ignore, /^UnityEntitlementLicense\.xml$/m);

  const skippedDirectories = new Set([
    ".git", "Build", "Library", "Logs", "Temp", "TestResults",
    "UserSettings", "dist", "dist-consumer", "node_modules",
  ]);
  const activationFiles = [];
  const scan = (directory) => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      if (entry.isDirectory() && !skippedDirectories.has(entry.name)) {
        scan(path.join(directory, entry.name));
      } else if (
        entry.isFile()
        && /^(?:UnityEntitlementLicense\.xml|.+\.(?:alf|ulf))$/i.test(entry.name)
      ) {
        activationFiles.push(path.relative(repoRoot, path.join(directory, entry.name)));
      }
    }
  };
  scan(repoRoot);
  assert.deepEqual(activationFiles, []);
});

test("Unity build wrapper refuses to fake a build without an editor", () => {
  const script = fs.readFileSync(path.join(repoRoot, "scripts", "build-unity-mv.sh"), "utf8");
  assert.match(script, /UNITY_EDITOR/);
  assert.match(script, /-executeMethod Haruki\.MV\.Editor\.BuildWebGL\.PerformBuild/);
  assert.match(script, /Haruki-Unity-LicenseClient-\$\$/);
  assert.doesNotMatch(script, /Unity-LicenseClient-root-2022\.3\.62/);
  assert.doesNotMatch(script, /touch .*\.wasm|echo .*\.wasm/);
});
